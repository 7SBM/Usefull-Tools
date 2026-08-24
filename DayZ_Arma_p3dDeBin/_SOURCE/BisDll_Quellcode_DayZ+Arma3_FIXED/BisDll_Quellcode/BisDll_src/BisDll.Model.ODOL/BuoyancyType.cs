using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class BuoyancyType : IDeserializable
{
	public float Volume { get; private set; }

	public void ReadObject(BinaryReaderEx input)
	{
		Volume = input.ReadSingle();
	}
}
