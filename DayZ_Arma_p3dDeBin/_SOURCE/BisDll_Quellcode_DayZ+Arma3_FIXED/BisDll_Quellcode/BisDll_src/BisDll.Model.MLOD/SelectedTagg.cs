using BisDll.Stream;

namespace BisDll.Model.MLOD;

public class SelectedTagg : Tagg
{
	public byte[] weightedPoints;

	public byte[] faces;

	public void read(BinaryReaderEx input, int nPoints, int nFaces)
	{
		weightedPoints = new byte[nPoints];
		for (int i = 0; i < nPoints; i++)
		{
			weightedPoints[i] = input.ReadByte();
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
		for (int i = 0; i < weightedPoints.Length; i++)
		{
			output.Write(weightedPoints[i]);
		}
		for (int j = 0; j < faces.Length; j++)
		{
			output.Write(faces[j]);
		}
	}
}
