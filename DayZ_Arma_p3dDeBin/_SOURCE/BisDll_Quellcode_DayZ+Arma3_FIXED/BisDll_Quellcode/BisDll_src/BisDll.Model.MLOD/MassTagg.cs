using BisDll.Stream;

namespace BisDll.Model.MLOD;

public class MassTagg : Tagg
{
	public float[] mass;

	public void read(BinaryReaderEx input)
	{
		uint num = base.DataSize / 4;
		mass = new float[num];
		for (int i = 0; i < num; i++)
		{
			mass[i] = input.ReadSingle();
		}
	}

	public void write(BinaryWriter output)
	{
		output.Write(value: true);
		output.writeAsciiz(base.Name);
		output.Write(base.DataSize);
		uint num = base.DataSize / 4;
		for (int i = 0; i < num; i++)
		{
			output.Write(mass[i]);
		}
	}
}
