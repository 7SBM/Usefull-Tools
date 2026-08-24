using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class SubSkeletonIndexSet : IDeserializable
{
	private int[] subSkeletons;

	public void ReadObject(BinaryReaderEx input)
	{
		subSkeletons = input.ReadIntArray();
	}
}
