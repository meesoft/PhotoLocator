import argparse

import torch

from train_dncnn import DnCNN


def main():
	parser = argparse.ArgumentParser(description="Export a trained DnCNN denoiser to ONNX.")
	parser.add_argument("--model", type=str, required=True, help="Path to the saved PyTorch weights (.pt)")
	parser.add_argument("--output", type=str, required=True, help="Path for the ONNX model")
	parser.add_argument("--channels", type=int, default=3, choices=[1, 3], help="Input image channels")
	parser.add_argument("--height", type=int, default=256, help="Example input height")
	parser.add_argument("--width", type=int, default=256, help="Example input width")
	parser.add_argument("--opset", type=int, default=17, help="ONNX opset version")
	args = parser.parse_args()

	model = DnCNN(channels=args.channels)
	model.load_state_dict(torch.load(args.model, map_location="cpu"))
	model.eval()

	dummy_input = torch.randn(1, args.channels, args.height, args.width)
	torch.onnx.export(
		model,
		dummy_input,
		args.output,
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

	print(f"Exported ONNX model to {args.output}")


if __name__ == "__main__":
	main()
