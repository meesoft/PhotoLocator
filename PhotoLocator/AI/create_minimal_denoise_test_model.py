from pathlib import Path

import onnx
from onnx import TensorProto, helper, numpy_helper


def create_identity_model(output_path: Path):
	input_tensor = helper.make_tensor_value_info("input", TensorProto.FLOAT, ["batch", "channels", "height", "width"])
	output_tensor = helper.make_tensor_value_info("output", TensorProto.FLOAT, ["batch", "channels", "height", "width"])

	identity_node = helper.make_node(
		"Identity",
		inputs=["input"],
		outputs=["output"],
		name="identity_test",
	)

	graph = helper.make_graph(
		[identity_node],
		"minimal_denoise_test_graph",
		[input_tensor],
		[output_tensor],
	)

	model = helper.make_model(graph, producer_name="PhotoLocatorTest")
	model.opset_import[0].version = 17

	output_path.parent.mkdir(parents=True, exist_ok=True)
	onnx.save(model, str(output_path))
	print(f"Saved minimal ONNX smoke-test model to {output_path}")


def main():
	repo_root = Path(__file__).resolve().parents[1]
	output_path = repo_root / "PhotoLocatorTest" / "TestData" / "dncnn_test.onnx"
	create_identity_model(output_path)


if __name__ == "__main__":
	main()
