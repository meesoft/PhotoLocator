import argparse
import math
from pathlib import Path

import cv2
import numpy as np
import torch
from torch import nn
from torch.utils.data import DataLoader, Dataset


class PairedImageDataset(Dataset):
	def __init__(self, noisy_dir: str, clean_dir: str, patch_size: int = 64, channels: int = 3, use_augment: bool = True):
		self.noisy_dir = Path(noisy_dir)
		self.clean_dir = Path(clean_dir)
		self.patch_size = patch_size
		self.channels = channels
		self.use_augment = use_augment

		noisy_files = sorted(self.noisy_dir.glob("*"))
		clean_files = sorted(self.clean_dir.glob("*"))
		noisy_map = {p.stem: p for p in noisy_files if p.is_file()}
		clean_map = {p.stem: p for p in clean_files if p.is_file()}

		common_names = sorted(set(noisy_map).intersection(clean_map))
		if not common_names:
			raise FileNotFoundError(f"No matching noisy/clean image pairs found in {noisy_dir} and {clean_dir}.")

		self.samples = [(noisy_map[name], clean_map[name]) for name in common_names]

	def __len__(self):
		return len(self.samples)

	def _load_image(self, path: Path):
		image = cv2.imread(str(path), cv2.IMREAD_UNCHANGED)
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
		noisy_path, clean_path = self.samples[index]
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
				noisy = noisy[:, ::-1, :]
				clean = clean[:, ::-1, :]

			if np.random.rand() > 0.5:
				noisy = noisy[::-1, :, :]
				clean = clean[::-1, :, :]

		noisy = noisy.transpose(2, 0, 1)
		clean = clean.transpose(2, 0, 1)

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


def validate_dataset(noisy_dir: str, clean_dir: str):
	noisy_files = sorted(Path(noisy_dir).glob("*")) if Path(noisy_dir).exists() else []
	clean_files = sorted(Path(clean_dir).glob("*")) if Path(clean_dir).exists() else []
	if not noisy_files or not clean_files:
		raise FileNotFoundError(f"Training dataset directories must exist and contain image files: {noisy_dir}, {clean_dir}")


def main():
	parser = argparse.ArgumentParser(description="Train a DnCNN denoiser from paired noisy/clean images.")
	parser.add_argument("--noisy-dir", type=str, required=True, help="Directory with noisy images")
	parser.add_argument("--clean-dir", type=str, required=True, help="Directory with clean target images")
	parser.add_argument("--output", type=str, default="dncnn_model.pt", help="Path for the trained PyTorch model")
	parser.add_argument("--channels", type=int, default=3, choices=[1, 3], help="Model input channels: 1 for grayscale, 3 for RGB")
	parser.add_argument("--patch-size", type=int, default=64, help="Training patch size")
	parser.add_argument("--batch-size", type=int, default=8, help="Training batch size")
	parser.add_argument("--epochs", type=int, default=20, help="Number of training epochs")
	parser.add_argument("--lr", type=float, default=1e-4, help="Learning rate")
	parser.add_argument("--device", type=str, default="cuda" if torch.cuda.is_available() else "cpu", help="Device to train on")
	args = parser.parse_args()

	validate_dataset(args.noisy_dir, args.clean_dir)

	dataset = PairedImageDataset(args.noisy_dir, args.clean_dir, patch_size=args.patch_size, channels=args.channels)
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

	torch.save(model.state_dict(), args.output)
	print(f"Saved trained model to {args.output}")


if __name__ == "__main__":
	main()
