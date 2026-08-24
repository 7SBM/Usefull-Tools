using System;
using BisDll.Stream;

namespace BisDll.Model.MLOD;

public class SharpEdgesTagg : Tagg
{
	public uint[,] pointIndices;

	public void read(BinaryReaderEx input)
	{
		uint num = base.DataSize / 8;
		pointIndices = new uint[(int)(IntPtr)num, 2];
		for (int i = 0; i < num; i++)
		{
			pointIndices[i, 0] = input.ReadUInt32();
			pointIndices[i, 1] = input.ReadUInt32();
		}
	}

	public void write(BinaryWriter output)
	{
		output.Write(value: true);
		output.writeAsciiz(base.Name);
		output.Write(base.DataSize);
		uint num = base.DataSize / 8;
		for (int i = 0; i < num; i++)
		{
			output.Write(pointIndices[i, 0]);
			output.Write(pointIndices[i, 1]);
		}
	}
}
