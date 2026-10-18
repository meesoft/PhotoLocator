import argparse
from pathlib import Path

import cv2
import torch

from train_dncnn import DnCNN, denoise_image


def main():
	parser = argparse.ArgumentParser(description="Denoise an image using a trained PyTorch DnCNN checkpoint.")
	parser.add_argument("--model", type=str, required=True, help="Path to the trained PyTorch checkpoint (.pt)")
	parser.add_argument("--input", type=str, required=True, help="Path to the image to denoise")
	parser.add_argument("--output", type=str, required=True, help="Path for the denoised image")
	parser.add_argument("--channels", type=int, default=3, choices=[1, 3], help="Model input channels")
	parser.add_argument("--tile-size", type=int, default=512, help="Maximum tile width and height")
	parser.add_argument("--overlap", type=int, default=64, help="Overlap between adjacent inference tiles")
	parser.add_argument("--device", type=str, default="cuda" if torch.cuda.is_available() else "cpu", help="Inference device, e.g. cpu or cuda")
	args = parser.parse_args()

	image = cv2.imread(args.input, cv2.IMREAD_UNCHANGED)
	if image is None:
		raise FileNotFoundError(f"Unable to read input image: {args.input}")

	model = DnCNN(channels=args.channels).to(args.device)
	model.load_state_dict(torch.load(args.model, map_location=args.device))
	print(f"Inference device: {args.device}")
	result = denoise_image(model, image, args.channels, args.device, args.tile_size, args.overlap)
	if result.shape[2] == 1:
		result = result[:, :, 0]

	output_path = Path(args.output)
	output_path.parent.mkdir(parents=True, exist_ok=True)
	if not cv2.imwrite(str(output_path), result):
		raise OSError(f"Unable to save denoised image: {output_path}")
	print(f"Saved denoised image to {output_path}")


if __name__ == "__main__":
	main()
