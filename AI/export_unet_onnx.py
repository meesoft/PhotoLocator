import argparse
import os

import onnx
import torch

from train_dncnn import checkpoint_model_state
from train_unet import UNetDenoiser


def main():
	parser = argparse.ArgumentParser(description="Export a trained U-Net denoiser to ONNX.")
	parser.add_argument("--model", type=str, default="unet_model.pt", help="Path to a trained U-Net checkpoint")
	parser.add_argument("--output", type=str, default="unet_model.onnx", help="Path for the ONNX model")
	parser.add_argument("--channels", type=int, choices=[1, 3], help="Override input channels; otherwise read from checkpoint")
	parser.add_argument("--base-features", type=int, help="Override base features; otherwise read from checkpoint")
	parser.add_argument("--levels", type=int, help="Override U-Net levels; otherwise read from checkpoint")
	parser.add_argument("--height", type=int, default=256, help="Example input height")
	parser.add_argument("--width", type=int, default=256, help="Example input width")
	parser.add_argument("--opset", type=int, default=17, help="ONNX opset version")
	args = parser.parse_args()

	checkpoint = torch.load(args.model, map_location="cpu")
	architecture = checkpoint.get("architecture", {}) if isinstance(checkpoint, dict) else {}
	channels = args.channels if args.channels is not None else architecture.get("channels", 3)
	base_features = args.base_features if args.base_features is not None else architecture.get("base_features", 32)
	levels = args.levels if args.levels is not None else architecture.get("levels", 3)
	model = UNetDenoiser(channels, base_features, levels)
	model.load_state_dict(checkpoint_model_state(checkpoint))
	model.eval()

	dummy_input = torch.randn(1, channels, args.height, args.width)
	temp_path = args.output + ".tmp"
	torch.onnx.export(
		model,
		(dummy_input,),
		temp_path,
		export_params=True,
		opset_version=args.opset,
		do_constant_folding=True,
		input_names=["input"],
		output_names=["output"],
		dynamic_axes={
			"input": {0: "batch_size", 2: "height", 3: "width"},
			"output": {0: "batch_size", 2: "height", 3: "width"},
		},
	)

	model_onnx = onnx.load(temp_path)
	onnx.checker.check_model(model_onnx)
	onnx.save_model(model_onnx, args.output, save_as_external_data=False, all_tensors_to_one_file=True)
	os.remove(temp_path)
	if os.path.exists(temp_path + ".data"):
		os.remove(temp_path + ".data")
	print(f"Exported U-Net ONNX model to {args.output}")


if __name__ == "__main__":
	main()