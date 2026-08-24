using BisDll.Common.Math;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class BuoyantPoint : IDeserializable
{
	public Vector3P Coords { get; private set; }

	public float SphereRadius { get; private set; }

	public float TypicalSurface { get; private set; }

	public void ReadObject(BinaryReaderEx input)
	{
		Coords = new Vector3P(input);
		SphereRadius = input.ReadSingle();
		TypicalSurface = input.ReadSingle();
	}
}
