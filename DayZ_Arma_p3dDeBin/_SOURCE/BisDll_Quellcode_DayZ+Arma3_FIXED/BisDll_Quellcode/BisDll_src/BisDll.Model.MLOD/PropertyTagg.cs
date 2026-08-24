using BisDll.Stream;

namespace BisDll.Model.MLOD;

public class PropertyTagg : Tagg
{
	public string name;

	public string value;

	public void read(BinaryReaderEx input)
	{
		name = input.ReadAscii(64);
		value = input.ReadAscii(64);
	}

	public void write(BinaryWriter output)
	{
		output.Write(value: true);
		output.writeAsciiz(base.Name);
		output.Write(base.DataSize);
		output.writeAscii(name, 64u);
		output.writeAscii(value, 64u);
	}
}
