import argparse
import math
from pathlib import Path

import cv2
import numpy as np
import torch
from torch import nn
from torch.utils.data import DataLoader, Dataset


class PairedImageDataset(Dataset):
	def __init__(self, image_dir: str, clean_postfix: str = "_clean", patch_size: int = 64, channels: int = 3, patches_per_image: int = 1, use_augment: bool = True):
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

		common_names = sorted(set(noisy_map).intersection(clean_map))
		if not common_names:
			raise FileNotFoundError(
				f"No matching noisy/clean image pairs found in {image_dir} using clean postfix '{self.clean_postfix}'."
			)

		self.samples = [(noisy_map[name], clean_map[name]) for name in common_names]

	def __len__(self):
		return len(self.samples) * self.patches_per_image

	def _load_image(self, path: Path):
		image = cv2.imread(str(path), cv2.IMREAD_UNCHANGED)
		if image is None:
			try:
				encoded = path.read_bytes()
				image = cv2.imdecode(np.frombuffer(encoded, dtype=np.uint8), cv2.IMREAD_UNCHANGED)
			except Exception:
				image = None
		if image is None:
			raise FileNotFoundError(f"Unable to read image: {path}")

		if image.ndim == 2:
			image = image[:, :, None]

		if image.shape[2] == 1 and self.channels == 3:
			image = np.repeat(image, 3, axis=2)
		elif image.shape[2] == 3 and self.channels == 1:
			image = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)[:, :, None]

		if image.shape[2] != self.channels:
			raise ValueError(f"Unexpected channel count {image.shape[2]} for channel setting {self.channels}: {path}")

		image = image.astype(np.float32) / 255.0
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


class DnCNN(nn.Module):
	def __init__(self, channels: int = 3, num_layers: int = 17, features: int = 64):
		super().__init__()
		self.channels = channels
		self.num_layers = num_layers

		layers = [nn.Conv2d(channels, features, kernel_size=3, stride=1, padding=1, bias=False)]
		layers.append(nn.ReLU(inplace=True))

		for _ in range(num_layers - 2):
			layers.append(nn.Conv2d(features, features, kernel_size=3, stride=1, padding=1, bias=False))
			layers.append(nn.BatchNorm2d(features))
			layers.append(nn.ReLU(inplace=True))

		layers.append(nn.Conv2d(features, channels, kernel_size=3, stride=1, padding=1, bias=False))
		self.model = nn.Sequential(*layers)

	def forward(self, x):
		residual = self.model(x)
		return x - residual


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
	parser.add_argument("--checkpoint-interval", type=int, default=100, help="Save a model checkpoint every N epochs (set to 0 to disable intermediate saves)")
	parser.add_argument("--channels", type=int, default=3, choices=[1, 3], help="Model input channels: 1 for grayscale, 3 for RGB")
	parser.add_argument("--patch-size", type=int, default=64, help="Training patch size")
	parser.add_argument("--patches-per-image", type=int, default=10, help="Number of random patches sampled from each image per epoch")
	parser.add_argument("--batch-size", type=int, default=8, help="Training batch size")
	parser.add_argument("--epochs", type=int, default=100, help="Number of training epochs")
	parser.add_argument("--lr", type=float, default=1e-4, help="Learning rate")
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

	model = DnCNN(channels=args.channels).to(args.device)
	optimizer = torch.optim.AdamW(model.parameters(), lr=args.lr)
	criterion = nn.MSELoss()

	for epoch in range(args.epochs):
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

		if args.checkpoint_interval > 0 and args.checkpoint_interval <args.epochs and (epoch + 1) % args.checkpoint_interval == 0:
			checkpoint_path = f"{Path(args.output).with_suffix('')}_epoch_{epoch + 1}.pt"
			torch.save(model.state_dict(), checkpoint_path)
			print(f"Saved checkpoint to {checkpoint_path}")

	torch.save(model.state_dict(), args.output)
	print(f"Saved trained model to {args.output}")


if __name__ == "__main__":
	main()
