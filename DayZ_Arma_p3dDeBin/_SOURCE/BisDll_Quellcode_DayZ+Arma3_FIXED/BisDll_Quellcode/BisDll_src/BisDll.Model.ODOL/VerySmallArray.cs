using BisDll.Stream;

namespace BisDll.Model.ODOL;

public abstract class VerySmallArray : IDeserializable
{
	protected int nSmall;

	protected byte[] smallSpace;

	public void ReadObject(BinaryReaderEx input)
	{
		nSmall = input.ReadInt32();
		smallSpace = input.ReadBytes(8);
	}
}
