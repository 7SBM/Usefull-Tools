using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class Polygon : IDeserializable
{
	public VertexIndex[] VertexIndices { get; private set; }

	public void ReadObject(BinaryReaderEx input)
	{
		_ = input.Version;
		byte b = input.ReadByte();
		VertexIndices = new VertexIndex[b];
		for (int i = 0; i < b; i++)
		{
			VertexIndices[i] = input.ReadVertexIndex();
		}
	}
}
