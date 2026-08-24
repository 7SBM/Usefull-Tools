using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class STPairUncompressed : STPair, IDeserializable
{
	public void ReadObject(BinaryReaderEx input)
	{
		base.S.ReadObject(input);
		base.T.ReadObject(input);
	}
}
