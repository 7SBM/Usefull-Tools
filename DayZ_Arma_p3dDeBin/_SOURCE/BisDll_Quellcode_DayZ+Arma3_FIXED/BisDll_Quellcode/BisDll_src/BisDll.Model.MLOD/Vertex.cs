using BisDll.Stream;

namespace BisDll.Model.MLOD;

public class Vertex
{
	public int PointIndex { get; private set; }

	public int NormalIndex { get; private set; }

	public float U { get; private set; }

	public float V { get; private set; }

	public Vertex(BinaryReaderEx input)
	{
		read(input);
	}

	public Vertex(int point, int normal, float u, float v)
	{
		PointIndex = point;
		NormalIndex = normal;
		U = u;
		V = v;
	}

	public void read(BinaryReaderEx input)
	{
		PointIndex = input.ReadInt32();
		NormalIndex = input.ReadInt32();
		U = input.ReadSingle();
		V = input.ReadSingle();
	}

	public void write(BinaryWriter output)
	{
		output.Write(PointIndex);
		output.Write(NormalIndex);
		output.Write(U);
		output.Write(V);
	}
}
