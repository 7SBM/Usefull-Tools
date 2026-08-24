using System;
using System.IO;
using BisDll.Stream;

namespace BisDll.Model.MLOD;

public class MLOD : P3D
{
	private MLOD_LOD[] lods;

	public override P3D_LOD[] LODs => lods;

	public override float Mass
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public MLOD(string fileName)
	{
		byte[] array = File.ReadAllBytes(fileName);
		BinaryReaderEx binaryReaderEx = new BinaryReaderEx(new MemoryStream(array, 0, array.Length, writable: false, publiclyVisible: true));
		read(binaryReaderEx);
		binaryReaderEx.Close();
	}

	public MLOD(System.IO.Stream stream)
	{
		read(new BinaryReaderEx(stream));
	}

	public MLOD(MLOD_LOD[] lods)
	{
		this.lods = lods;
	}

	private void read(BinaryReaderEx input)
	{
		if (input.ReadAscii(4) != "MLOD")
		{
			throw new Exception("MLOD signature expected");
		}
		base.Version = input.ReadUInt32();
		if (base.Version != 257)
		{
			throw new Exception("Unknown MLOD version");
		}
		uint num = input.ReadUInt32();
		lods = new MLOD_LOD[num];
		for (int i = 0; i < num; i++)
		{
			lods[i] = new MLOD_LOD();
			lods[i].read(input);
		}
	}

	private void write(BisDll.Stream.BinaryWriter output)
	{
		output.writeAscii("MLOD", 4u);
		output.Write(257);
		output.Write(lods.Length);
		for (int i = 0; i < lods.Length; i++)
		{
			lods[i].write(output);
		}
	}

	public void writeToFile(string file, bool allowOverwriting = false)
	{
		FileMode mode = ((!allowOverwriting) ? FileMode.CreateNew : FileMode.Create);
		BisDll.Stream.BinaryWriter binaryWriter = new BisDll.Stream.BinaryWriter(new FileStream(file, mode));
		write(binaryWriter);
		binaryWriter.Close();
	}

	public MemoryStream writeToMemory()
	{
		MemoryStream memoryStream = new MemoryStream(100000);
		BisDll.Stream.BinaryWriter binaryWriter = new BisDll.Stream.BinaryWriter(memoryStream);
		write(binaryWriter);
		binaryWriter.Position = 0L;
		return memoryStream;
	}

	public void writeToStream(System.IO.Stream stream)
	{
		BisDll.Stream.BinaryWriter output = new BisDll.Stream.BinaryWriter(stream);
		write(output);
	}
}
