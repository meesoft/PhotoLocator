import argparse
from pathlib import Path

import cv2
import torch
from torch import nn
from torch.utils.data import DataLoader

from denoising_common import RGBPairedImageDataset, checkpoint_model_state, denoise_image, validate_dataset


class ResidualBlock(nn.Module):
	def __init__(self, features: int):
		super().__init__()
		self.conv1 = nn.Conv2d(features, features, kernel_size=3, stride=1, padding=1, bias=True)
		self.relu = nn.ReLU(inplace=True)
		self.conv2 = nn.Conv2d(features, features, kernel_size=3, stride=1, padding=1, bias=True)

	def forward(self, x):
		return x + self.conv2(self.relu(self.conv1(x)))


class DnCNN(nn.Module):
	def __init__(self, channels: int = 3, num_layers: int = 20, features: int = 80):
		super().__init__()
		self.channels = channels
		self.num_layers = num_layers
		residual_block_count = max(1, (num_layers - 2) // 2)
		self.input_conv = nn.Conv2d(channels, features, kernel_size=3, stride=1, padding=1, bias=True)
		self.input_relu = nn.ReLU(inplace=True)
		self.residual_blocks = nn.Sequential(*(ResidualBlock(features) for _ in range(residual_block_count)))
		self.output_conv = nn.Conv2d(features, channels, kernel_size=3, stride=1, padding=1, bias=True)

	def forward(self, x):
		features = self.input_relu(self.input_conv(x))
		features = self.residual_blocks(features)
		residual = self.output_conv(features)
		return x - residual


def main():
	parser = argparse.ArgumentParser(description="Train a DnCNN denoiser from paired noisy/clean images stored in the same folder.")
	parser.add_argument("--image-dir", type=str, required=True, help="Directory containing noisy images and matching clean images with a suffix")
	parser.add_argument("--clean-postfix", type=str, default="_clean", help="File suffix used by the clean image, e.g. '_clean' for 'image_clean.png'")
	parser.add_argument("--output", type=str, default="dncnn_model.pt", help="Path for the trained PyTorch model")
	parser.add_argument("--validation-image", type=str, help="Optional noisy image to denoise whenever a checkpoint is saved")
	parser.add_argument("--resume", type=str, help="Optional checkpoint to resume from; legacy weights-only files restart the optimizer")
	parser.add_argument("--validation-output-dir", type=str, help="Directory for checkpoint validation previews; defaults to a 'validation' folder beside --output")
	parser.add_argument("--checkpoint-interval", type=int, default=20, help="Save a model checkpoint every N epochs (set to 0 to disable intermediate saves)")
	parser.add_argument("--channels", type=int, default=3, choices=[1, 3], help="Model input channels: 1 for grayscale, 3 for RGB")
	parser.add_argument("--patch-size", type=int, default=384, help="Training patch size")
	parser.add_argument("--patches-per-image", type=int, default=4, help="Number of random patches sampled from each image per epoch")
	parser.add_argument("--batch-size", type=int, default=8, help="Training batch size")
	parser.add_argument("--epochs", type=int, default=2000, help="Total target number of epochs, including epochs in a resumed checkpoint")
	parser.add_argument("--lr", type=float, default=1e-4, help="Learning rate")
	parser.add_argument("--weight-decay", type=float, default=0.01, help="AdamW weight decay")
	parser.add_argument("--device", type=str, default="cuda" if torch.cuda.is_available() else "cpu", help="Device to train on")
	args = parser.parse_args()

	if not Path(args.image_dir).exists():
		raise FileNotFoundError(f"Training dataset directory does not exist: {args.image_dir}")
	validate_dataset(args.image_dir, args.clean_postfix)

	dataset = RGBPairedImageDataset(
		args.image_dir,
		clean_postfix=args.clean_postfix,
		patch_size=args.patch_size,
		channels=args.channels,
		patches_per_image=args.patches_per_image,
	)
	loader = DataLoader(dataset, batch_size=args.batch_size, shuffle=True, num_workers=0, pin_memory=torch.cuda.is_available())

	print(f"Image pairs: {len(dataset.samples)}")
	print(f"Training device: {args.device}")

	model = DnCNN(channels=args.channels).to(args.device)
	optimizer = torch.optim.AdamW(model.parameters(), lr=args.lr, weight_decay=args.weight_decay)
	start_epoch = 0
	if args.resume:
		checkpoint = torch.load(args.resume, map_location=args.device)
		model.load_state_dict(checkpoint_model_state(checkpoint))
		if isinstance(checkpoint, dict) and "model_state_dict" in checkpoint:
			if "optimizer_state_dict" in checkpoint:
				optimizer.load_state_dict(checkpoint["optimizer_state_dict"])

				for group in optimizer.param_groups:
					group["lr"] = args.lr
					group["weight_decay"] = args.weight_decay

			start_epoch = int(checkpoint.get("epoch", 0))
			print(f"Resumed optimizer and model at epoch {start_epoch} from {args.resume}, lr={optimizer.param_groups[0]['lr']}, weight_decay={optimizer.param_groups[0]['weight_decay']}")
		else:
			print(f"Loaded model weights from {args.resume}; optimizer starts fresh")
	criterion = nn.MSELoss()
	validation_image = None
	validation_output_dir = None
	if args.validation_image:
		validation_image = cv2.imread(args.validation_image, cv2.IMREAD_UNCHANGED)
		if validation_image is None:
			raise FileNotFoundError(f"Unable to read validation image: {args.validation_image}")
		if args.channels == 3 and validation_image.ndim == 3:
			if validation_image.shape[2] == 4:
				validation_image = cv2.cvtColor(validation_image, cv2.COLOR_BGRA2RGB)
			else:
				validation_image = cv2.cvtColor(validation_image, cv2.COLOR_BGR2RGB)
		validation_output_dir = Path(args.validation_output_dir) if args.validation_output_dir else Path(args.output).parent / "validation"
		validation_output_dir.mkdir(parents=True, exist_ok=True)

	def save_checkpoint(checkpoint_path: Path, completed_epochs: int):
		torch.save({
			"epoch": completed_epochs,
			"model_state_dict": model.state_dict(),
			"optimizer_state_dict": optimizer.state_dict(),
		}, checkpoint_path)
		print(f"Saved checkpoint to {checkpoint_path}")
		if validation_image is not None and validation_output_dir is not None:
			preview = denoise_image(model, validation_image, args.channels, args.device)
			if args.channels == 3:
				preview = cv2.cvtColor(preview, cv2.COLOR_RGB2BGR)
			preview_path = validation_output_dir / f"{checkpoint_path.stem}_validation.png"
			if not cv2.imwrite(str(preview_path), preview):
				raise OSError(f"Unable to save validation preview: {preview_path}")
			print(f"Saved validation preview to {preview_path}")

	for epoch in range(start_epoch, args.epochs):
		model.train()
		running_loss = 0.0
		for noisy_batch, clean_batch in loader:
			noisy_batch = noisy_batch.to(args.device)
			clean_batch = clean_batch.to(args.device)

			optimizer.zero_grad()
			predictions = model(noisy_batch)
			loss = criterion(predictions, clean_batch)
			loss.backward()
			optimizer.step()

			running_loss += loss.item() * noisy_batch.size(0)

		epoch_loss = running_loss / len(dataset)
		print(f"epoch={epoch + 1}/{args.epochs} loss={epoch_loss:.6f}")

		if args.checkpoint_interval > 0 and (epoch + 1) % args.checkpoint_interval == 0:
			checkpoint_path = Path(f"{Path(args.output).with_suffix('')}_epoch_{epoch + 1}.pt")
			save_checkpoint(checkpoint_path, epoch + 1)

	save_checkpoint(Path(args.output), max(start_epoch, args.epochs))


if __name__ == "__main__":
	main()
