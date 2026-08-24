using BisDll.Common.Math;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class StageTransform
{
	public enum UVSource
	{
		UVNone,
		UVTex,
		UVTexWaterAnim,
		UVPos,
		UVNorm,
		UVTex1,
		UVWorldPos,
		UVWorldNorm,
		UVTexShoreAnim,
		NUVSource
	}

	public UVSource uvSource;

	public Matrix4P transformation;

	public StageTransform(BinaryReaderEx input)
	{
		uvSource = (UVSource)input.ReadUInt32();
		transformation = new Matrix4P(input);
	}
}
