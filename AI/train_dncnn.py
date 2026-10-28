import argparse
import math
from pathlib import Path

import cv2
import numpy as np
import torch
from torch import nn
from torch.utils.data import DataLoader, Dataset

MONITOR_GAMMA = 1


class PairedImageDataset(Dataset):
	def __init__(self, image_dir: str, clean_postfix: str, patch_size: int, channels, patches_per_image, use_augment: bool = True):
		self.image_dir = Path(image_dir)
		self.clean_postfix = clean_postfix
		self.patch_size = patch_size
		self.channels = channels
		self.patches_per_image = max(1, patches_per_image)
		self.use_augment = use_augment

		image_files = sorted(self.image_dir.glob("*"))
		noisy_map = {}
		clean_map = {}

		for path in image_files:
			if not path.is_file():
				continue
			if path.name.endswith(f"{self.clean_postfix}{path.suffix}"):
				base_name = path.stem.removesuffix(self.clean_postfix)
				clean_map[base_name] = path
			else:
				noisy_map[path.stem] = path

		unmatched_noisy_names = sorted(set(noisy_map) - set(clean_map))
		for name in unmatched_noisy_names:
			print(f"No matching clean image for noisy file: {noisy_map[name].name}")

		common_names = sorted(set(noisy_map).intersection(clean_map))
		if not common_names:
			raise FileNotFoundError(
				f"No matching noisy/clean image pairs found in {image_dir} using clean postfix '{self.clean_postfix}'."
			)

		self.samples = [(noisy_map[name], clean_map[name]) for name in common_names]

	def __len__(self):
		return len(self.samples) * self.patches_per_image

	def _load_image(self, path: Path):
		#image = cv2.imread(str(path), cv2.IMREAD_UNCHANGED)
		#if image is None:
				encoded = path.read_bytes()
				image = cv2.imdecode(np.frombuffer(encoded, dtype=np.uint8), cv2.IMREAD_UNCHANGED)
		if image is None:
			raise FileNotFoundError(f"Unable to read image: {path}")

		if image.ndim == 2:
			image = image[:, :, None]
		image = image.astype(np.float32) / _image_max_value(image)
		image = np.power(image, MONITOR_GAMMA)

		if image.shape[2] == 1 and self.channels == 3:
			image = np.repeat(image, 3, axis=2)
		elif image.shape[2] == 3 and self.channels == 1:
			image = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)[:, :, None]

		if image.shape[2] != self.channels:
			raise ValueError(f"Unexpected channel count {image.shape[2]} for channel setting {self.channels}: {path}")

		return image

	def __getitem__(self, index):
		base_index = index // self.patches_per_image
		noisy_path, clean_path = self.samples[base_index]
		noisy = self._load_image(noisy_path)
		clean = self._load_image(clean_path)

		if noisy.shape[:2] != clean.shape[:2]:
			raise ValueError(f"Noisy and clean dimensions do not match for {noisy_path.name}: {noisy.shape[:2]} vs {clean.shape[:2]}")

		h, w = noisy.shape[:2]
		if h < self.patch_size or w < self.patch_size:
			raise ValueError(f"Image too small for patch size {self.patch_size}: {noisy_path}")

		if self.use_augment:
			top = np.random.randint(0, h - self.patch_size + 1)
			left = np.random.randint(0, w - self.patch_size + 1)
			noisy = noisy[top:top + self.patch_size, left:left + self.patch_size]
			clean = clean[top:top + self.patch_size, left:left + self.patch_size]

			if np.random.rand() > 0.5:
				noisy = noisy[:, ::-1, :].copy()
				clean = clean[:, ::-1, :].copy()

			if np.random.rand() > 0.5:
				noisy = noisy[::-1, :, :].copy()
				clean = clean[::-1, :, :].copy()
		else:
			top = (index % self.patches_per_image) * 0
			left = (index % self.patches_per_image) * 0
			noisy = noisy[0:self.patch_size, 0:self.patch_size]
			clean = clean[0:self.patch_size, 0:self.patch_size]

		noisy = noisy.transpose(2, 0, 1).copy()
		clean = clean.transpose(2, 0, 1).copy()

		return torch.from_numpy(noisy), torch.from_numpy(clean)


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


def checkpoint_model_state(checkpoint):
	if isinstance(checkpoint, dict) and "model_state_dict" in checkpoint:
		return checkpoint["model_state_dict"]
	return checkpoint


def _image_max_value(image: np.ndarray):
	if image.dtype not in (np.uint8, np.uint16):
		raise ValueError(f"Unsupported image dtype {image.dtype}; expected 8-bit or 16-bit unsigned pixels.")
	return np.iinfo(image.dtype).max


def _tile_starts(length: int, tile_size: int, overlap: int):
	if length <= tile_size:
		return [0]
	step = tile_size - overlap
	starts = list(range(0, length - tile_size + 1, step))
	if starts[-1] != length - tile_size:
		starts.append(length - tile_size)
	return starts


def denoise_image(model, image: np.ndarray, channels: int, device: str, tile_size: int = 512, overlap: int = 64):
	if tile_size <= 0 or overlap < 0 or overlap >= tile_size:
		raise ValueError("Tile size must be positive and overlap must be between 0 and tile size.")
	edge_margin = min(model.num_layers, tile_size // 2)
	if overlap < 2 * edge_margin and image.ndim >= 2 and max(image.shape[:2]) > tile_size:
		raise ValueError(f"Tile overlap must be at least {2 * edge_margin} pixels for this model.")

	if image.ndim == 2:
		image = image[:, :, None]
	output_dtype = image.dtype
	image_max_value = _image_max_value(image)
	if image.shape[2] == 4:
		image = image[:, :, :3]
	image = image.astype(np.float32) / image_max_value
	image = np.power(image, MONITOR_GAMMA)
	if image.shape[2] == 1 and channels == 3:
		image = np.repeat(image, 3, axis=2)
	elif image.shape[2] == 3 and channels == 1:
		image = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)[:, :, None]
	if image.shape[2] != channels:
		raise ValueError(f"Expected {channels} image channels, got {image.shape[2]}.")

	height, width = image.shape[:2]
	output = np.zeros((height, width, channels), dtype=np.float32)
	weights = np.zeros((height, width, 1), dtype=np.float32)
	was_training = model.training
	model.eval()
	try:
		with torch.inference_mode():
			for top in _tile_starts(height, tile_size, overlap):
				for left in _tile_starts(width, tile_size, overlap):
					bottom = min(top + tile_size, height)
					right = min(left + tile_size, width)
					tile = image[top:bottom, left:right].transpose(2, 0, 1).copy()
					tensor = torch.from_numpy(tile).unsqueeze(0).to(device)
					denoised = model(tensor).squeeze(0).cpu().numpy().transpose(1, 2, 0)
					tile_height, tile_width = denoised.shape[:2]
					tile_weights = np.ones((tile_height, tile_width), dtype=np.float32)
					if edge_margin:
						ramp = np.linspace(1.0 / (edge_margin + 1), 1.0, edge_margin, dtype=np.float32)
						if top > 0:
							tile_weights[:edge_margin, :] *= ramp[:, None]
						if bottom < height:
							tile_weights[-edge_margin:, :] *= ramp[::-1, None]
						if left > 0:
							tile_weights[:, :edge_margin] *= ramp[None, :]
						if right < width:
							tile_weights[:, -edge_margin:] *= ramp[None, ::-1]
					output[top:bottom, left:right] += denoised * tile_weights[:, :, None]
					weights[top:bottom, left:right] += tile_weights[:, :, None]
	finally:
		model.train(was_training)

	output = np.power(np.clip(output / weights, 0.0, 1.0), 1.0 / MONITOR_GAMMA)
	return np.rint(output * image_max_value).astype(output_dtype)


def validate_dataset(image_dir: str, clean_postfix: str):
	image_path = Path(image_dir)
	if not image_path.exists() or not image_path.is_dir():
		raise FileNotFoundError(f"Training dataset directory must exist: {image_dir}")

	files = sorted(image_path.glob("*"))
	if not any(path.is_file() for path in files):
		raise FileNotFoundError(f"Training dataset directory is empty: {image_dir}")

	matching_files = [path for path in files if path.is_file() and path.name.endswith(f"{clean_postfix}{path.suffix}")]
	if not matching_files:
		raise FileNotFoundError(f"No clean images with postfix '{clean_postfix}' were found in {image_dir}")


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
	parser.add_argument("--patch-size", type=int, default=256, help="Training patch size")
	parser.add_argument("--patches-per-image", type=int, default=4, help="Number of random patches sampled from each image per epoch")
	parser.add_argument("--batch-size", type=int, default=8, help="Training batch size")
	parser.add_argument("--epochs", type=int, default=1000, help="Total target number of epochs, including epochs in a resumed checkpoint")
	parser.add_argument("--lr", type=float, default=2e-4, help="Learning rate")
	parser.add_argument("--weight-decay", type=float, default=0.01, help="AdamW weight decay")
	parser.add_argument("--device", type=str, default="cuda" if torch.cuda.is_available() else "cpu", help="Device to train on")
	args = parser.parse_args()

	if not Path(args.image_dir).exists():
		raise FileNotFoundError(f"Training dataset directory does not exist: {args.image_dir}")
	validate_dataset(args.image_dir, args.clean_postfix)

	dataset = PairedImageDataset(
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
