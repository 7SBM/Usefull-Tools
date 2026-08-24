using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class STPairCompressed : STPair, IDeserializable
{
	public void ReadObject(BinaryReaderEx input)
	{
		base.S.readCompressed(input);
		base.T.readCompressed(input);
	}
}
