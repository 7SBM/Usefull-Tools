namespace BisDll.Model.ODOL;

public struct VertexIndex
{
	private int value;

	public static implicit operator int(VertexIndex vi)
	{
		return vi.value;
	}

	public static implicit operator VertexIndex(ushort vi)
	{
		VertexIndex result = default(VertexIndex);
		result.value = ((vi == ushort.MaxValue) ? (-1) : vi);
		return result;
	}

	public static implicit operator VertexIndex(int vi)
	{
		VertexIndex result = default(VertexIndex);
		result.value = vi;
		return result;
	}
}
