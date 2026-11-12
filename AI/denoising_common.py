from pathlib import Path

import cv2
import numpy as np
import torch
from torch.utils.data import Dataset

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

		height, width = noisy.shape[:2]
		if height < self.patch_size or width < self.patch_size:
			raise ValueError(f"Image too small for patch size {self.patch_size}: {noisy_path}")

		if self.use_augment:
			top = np.random.randint(0, height - self.patch_size + 1)
			left = np.random.randint(0, width - self.patch_size + 1)
			noisy = noisy[top:top + self.patch_size, left:left + self.patch_size]
			clean = clean[top:top + self.patch_size, left:left + self.patch_size]

			if np.random.rand() > 0.5:
				noisy = noisy[:, ::-1, :].copy()
				clean = clean[:, ::-1, :].copy()

			if np.random.rand() > 0.5:
				noisy = noisy[::-1, :, :].copy()
				clean = clean[::-1, :, :].copy()
		else:
			noisy = noisy[0:self.patch_size, 0:self.patch_size]
			clean = clean[0:self.patch_size, 0:self.patch_size]

		noisy = noisy.transpose(2, 0, 1).copy()
		clean = clean.transpose(2, 0, 1).copy()
		return torch.from_numpy(noisy), torch.from_numpy(clean)


class RGBPairedImageDataset(PairedImageDataset):
	def _load_image(self, path: Path):
		image = super()._load_image(path)
		if self.channels == 3:
			image = image[:, :, ::-1].copy()
		return image


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
	edge_margin = min(getattr(model, "tile_edge_margin", getattr(model, "num_layers", 0)), tile_size // 2)
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


def calculate_validation_loss(model, noisy_image: np.ndarray, clean_image: np.ndarray, channels: int, device: str, tile_size: int, tile_overlap: int):
	prediction = denoise_image(model, noisy_image, channels, device, tile_size, tile_overlap)
	prediction = prediction.astype(np.float32) / _image_max_value(prediction)
	prediction = np.power(prediction, MONITOR_GAMMA)
	if prediction.ndim == 2:
		prediction = prediction[:, :, None]
	if prediction.shape != clean_image.shape:
		raise ValueError(f"Validation prediction and target shapes differ: {prediction.shape} vs {clean_image.shape}")
	return float(np.mean(np.square(prediction - clean_image), dtype=np.float64))
