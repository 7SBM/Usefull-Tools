using System.IO;

namespace BisDll.Stream;

public class BinaryWriter : System.IO.BinaryWriter
{
	public long Position
	{
		get
		{
			return BaseStream.Position;
		}
		set
		{
			BaseStream.Position = value;
		}
	}

	public BinaryWriter(System.IO.Stream dstStream)
		: base(dstStream)
	{
	}

	public void writeAscii(string text, uint len)
	{
		Write(text.ToCharArray());
		uint num = (uint)(len - text.Length);
		for (int i = 0; i < num; i++)
		{
			Write('\0');
		}
	}

	public void writeAsciiz(string text)
	{
		Write(text.ToCharArray());
		Write('\0');
	}
}
