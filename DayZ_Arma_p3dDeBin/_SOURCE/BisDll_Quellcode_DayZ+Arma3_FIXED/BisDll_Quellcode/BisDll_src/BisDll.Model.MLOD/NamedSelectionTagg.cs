using BisDll.Stream;

namespace BisDll.Model.MLOD;

public class NamedSelectionTagg : Tagg
{
	public byte[] points;

	public byte[] faces;

	public void read(BinaryReaderEx input, int nPoints, int nFaces)
	{
		points = new byte[nPoints];
		for (int i = 0; i < nPoints; i++)
		{
			points[i] = input.ReadByte();
		}
		faces = new byte[nFaces];
		for (int j = 0; j < nFaces; j++)
		{
			faces[j] = input.ReadByte();
		}
	}

	public void write(BinaryWriter output)
	{
		output.Write(value: true);
		output.writeAsciiz(base.Name);
		output.Write(base.DataSize);
		for (int i = 0; i < points.Length; i++)
		{
			output.Write(points[i]);
		}
		for (int j = 0; j < faces.Length; j++)
		{
			output.Write(faces[j]);
		}
	}
}
