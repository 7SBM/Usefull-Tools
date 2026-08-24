using BisDll.Common.Math;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class Keyframe : IDeserializable
{
	public float time;

	public Vector3P[] points;

	public void ReadObject(BinaryReaderEx input)
	{
		time = input.ReadSingle();
		uint num = input.ReadUInt32();
		points = new Vector3P[num];
		for (int i = 0; i < num; i++)
		{
			points[i] = new Vector3P(input);
		}
	}
}
