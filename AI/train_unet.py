import argparse
from pathlib import Path

import cv2
import torch
import torch.nn.functional as functional
from torch import nn
from torch.utils.data import DataLoader

from train_dncnn import PairedImageDataset, checkpoint_model_state, denoise_image, validate_dataset


class RGBPairedImageDataset(PairedImageDataset):
	def _load_image(self, path: Path):
		image = super()._load_image(path)
		if self.channels == 3:
			image = image[:, :, ::-1].copy()
		return image


class ConvBlock(nn.Module):
	def __init__(self, input_channels: int, output_channels: int):
		super().__init__()
		self.layers = nn.Sequential(
			nn.Conv2d(input_channels, output_channels, kernel_size=3, padding=1),
			nn.ReLU(inplace=True),
			nn.Conv2d(output_channels, output_channels, kernel_size=3, padding=1),
			nn.ReLU(inplace=True),
		)

	def forward(self, x):
		return self.layers(x)


class UNetDenoiser(nn.Module):
	def __init__(self, channels: int = 3, base_features: int = 32, levels: int = 3):
		super().__init__()
		if levels < 1:
			raise ValueError("U-Net levels must be at least 1.")
		self.channels = channels
		self.base_features = base_features
		self.levels = levels
		self.tile_edge_margin = min(32, 2 ** (levels + 1))
		widths = [base_features * 2 ** level for level in range(levels)]

		self.encoders = nn.ModuleList()
		previous_channels = channels
		for width in widths:
			self.encoders.append(ConvBlock(previous_channels, width))
			previous_channels = width

		self.pool = nn.MaxPool2d(kernel_size=2, stride=2)
		self.bottleneck = ConvBlock(widths[-1], widths[-1] * 2)
		self.decoders = nn.ModuleList()
		previous_channels = widths[-1] * 2
		for width in reversed(widths):
			self.decoders.append(ConvBlock(previous_channels + width, width))
			previous_channels = width
		self.output_conv = nn.Conv2d(base_features, channels, kernel_size=1)

	def forward(self, x):
		original = x
		skips = []
		for encoder in self.encoders:
			x = encoder(x)
			skips.append(x)
			x = self.pool(x)

		x = self.bottleneck(x)
		for decoder, skip in zip(self.decoders, reversed(skips)):
			x = functional.interpolate(x, size=skip.shape[-2:], mode="bilinear", align_corners=False)
			x = decoder(torch.cat((skip, x), dim=1))

		return original - self.output_conv(x)


def main():
	parser = argparse.ArgumentParser(description="Train a U-Net denoiser from paired noisy/clean images.")
	parser.add_argument("--image-dir", type=str, required=True, help="Directory containing noisy images and matching clean images with a suffix")
	parser.add_argument("--clean-postfix", type=str, default="_clean", help="Suffix used by each clean image, e.g. '-denoise'")
	parser.add_argument("--output", type=str, default="unet_model.pt", help="Path for the trained PyTorch checkpoint")
	parser.add_argument("--resume", type=str, help="Optional U-Net checkpoint to resume from")
	parser.add_argument("--validation-image", type=str, help="Optional noisy image to denoise whenever a checkpoint is saved")
	parser.add_argument("--validation-output-dir", type=str, help="Directory for validation previews; defaults to a 'validation' folder beside --output")
	parser.add_argument("--checkpoint-interval", type=int, default=20, help="Save a checkpoint every N epochs; set to 0 to disable intermediate saves")
	parser.add_argument("--channels", type=int, default=3, choices=[1, 3], help="Model input channels")
	parser.add_argument("--base-features", type=int, default=32, help="Number of feature maps in the first U-Net level")
	parser.add_argument("--levels", type=int, default=3, help="Number of downsampling levels")
	parser.add_argument("--patch-size", type=int, default=640, help="Training patch size")
	parser.add_argument("--patches-per-image", type=int, default=4, help="Random patches sampled from each image per epoch")
	parser.add_argument("--batch-size", type=int, default=8, help="Training batch size")
	parser.add_argument("--epochs", type=int, default=1000, help="Total target epochs, including epochs in a resumed checkpoint")
	parser.add_argument("--lr", type=float, default=1e-4, help="Learning rate")
	parser.add_argument("--weight-decay", type=float, default=0.001, help="AdamW weight decay")
	parser.add_argument("--tile-size", type=int, default=512, help="Tile size for validation-image inference")
	parser.add_argument("--tile-overlap", type=int, default=64, help="Overlap for validation-image inference")
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

	model = UNetDenoiser(args.channels, args.base_features, args.levels).to(args.device)
	optimizer = torch.optim.AdamW(model.parameters(), lr=args.lr, weight_decay=args.weight_decay)
	start_epoch = 0
	if args.resume:
		checkpoint = torch.load(args.resume, map_location=args.device)
		architecture = checkpoint.get("architecture", {}) if isinstance(checkpoint, dict) else {}
		expected = {"channels": args.channels, "base_features": args.base_features, "levels": args.levels}
		if architecture and architecture != expected:
			raise ValueError(f"Checkpoint architecture {architecture} does not match requested architecture {expected}.")
		model.load_state_dict(checkpoint_model_state(checkpoint))
		if isinstance(checkpoint, dict) and "optimizer_state_dict" in checkpoint:
			optimizer.load_state_dict(checkpoint["optimizer_state_dict"])
			for group in optimizer.param_groups:
				group["lr"] = args.lr
				group["weight_decay"] = args.weight_decay
			start_epoch = int(checkpoint.get("epoch", 0))
			print(f"Resumed model at epoch {start_epoch} from {args.resume}")
		else:
			print(f"Loaded model weights from {args.resume}; optimizer starts fresh")

	validation_image = None
	validation_output_dir = None
	if args.validation_image:
		validation_image = cv2.imread(args.validation_image, cv2.IMREAD_UNCHANGED)
		if validation_image is None:
			raise FileNotFoundError(f"Unable to read validation image: {args.validation_image}")
		validation_output_dir = Path(args.validation_output_dir) if args.validation_output_dir else Path(args.output).parent / "validation"
		validation_output_dir.mkdir(parents=True, exist_ok=True)

	criterion = nn.MSELoss()
	def save_checkpoint(path: Path, completed_epochs: int):
		torch.save({
			"epoch": completed_epochs,
			"architecture": {"channels": args.channels, "base_features": args.base_features, "levels": args.levels},
			"model_state_dict": model.state_dict(),
			"optimizer_state_dict": optimizer.state_dict(),
		}, path)
		print(f"Saved checkpoint to {path}")
		if validation_image is not None and validation_output_dir is not None:
			inference_image = validation_image
			if args.channels == 3:
				if validation_image.shape[2] == 4:
					inference_image = cv2.cvtColor(validation_image, cv2.COLOR_BGRA2RGBA)
				else:
					inference_image = cv2.cvtColor(validation_image, cv2.COLOR_BGR2RGB)
			preview = denoise_image(model, inference_image, args.channels, args.device, args.tile_size, args.tile_overlap)
			if args.channels == 3:
				preview = cv2.cvtColor(preview, cv2.COLOR_RGB2BGR)
			preview_path = validation_output_dir / f"{path.stem}_validation.png"
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