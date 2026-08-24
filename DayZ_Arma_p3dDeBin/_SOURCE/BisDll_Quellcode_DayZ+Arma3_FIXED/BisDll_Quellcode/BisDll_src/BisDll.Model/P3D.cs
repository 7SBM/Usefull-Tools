using System;
using System.IO;
using System.Linq;
using BisDll.Model.MLOD;
using BisDll.Model.ODOL;
using BisDll.Stream;

namespace BisDll.Model;

public abstract class P3D
{
	public uint Version { get; protected set; }

	public abstract P3D_LOD[] LODs { get; }

	public abstract float Mass { get; }

	public static P3D GetInstance(string fileName)
	{
		return GetInstance(File.OpenRead(fileName));
	}

	public static P3D GetInstance(System.IO.Stream stream)
	{
		string text = new BinaryReaderEx(stream).ReadAscii(4);
		stream.Position -= 4L;
		if (text == "ODOL")
		{
			return new BisDll.Model.ODOL.ODOL(stream);
		}
		if (text == "MLOD")
		{
			return new BisDll.Model.MLOD.MLOD(stream);
		}
		throw new FormatException();
	}

	public virtual P3D_LOD getLOD(float resolution)
	{
		return LODs.FirstOrDefault((P3D_LOD lod) => lod.Resolution == resolution);
	}
}
