import argparse
import os

import onnx
import torch

from train_dncnn import DnCNN


def main():
	parser = argparse.ArgumentParser(description="Export a trained DnCNN denoiser to a single-file embedded ONNX model.")
	parser.add_argument("--model", type=str, default="dncnn_model.pt", help="Path to the saved PyTorch weights (.pt)")
	parser.add_argument("--output", type=str, default="dncnn_model.onnx", help="Path for the ONNX model")
	parser.add_argument("--channels", type=int, default=3, choices=[1, 3], help="Input image channels")
	parser.add_argument("--height", type=int, default=256, help="Example input height")
	parser.add_argument("--width", type=int, default=256, help="Example input width")
	parser.add_argument("--opset", type=int, default=17, help="ONNX opset version")
	args = parser.parse_args()

	model = DnCNN(channels=args.channels)
	model.load_state_dict(torch.load(args.model, map_location="cpu"))
	model.eval()
	model.ir_version = 13

	dummy_input = torch.randn(1, args.channels, args.height, args.width)
	temp_path = args.output + ".tmp"
	torch.onnx.export(
		model,
		dummy_input,
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
	model_onnx.ir_version = 13
	onnx.save_model(
		model_onnx,
		args.output,
		save_as_external_data=False,
		all_tensors_to_one_file=True,
	)

	os.remove(temp_path)
	os.remove(temp_path + ".data")

	print(f"Exported ONNX model to {args.output}")


if __name__ == "__main__":
	main()
