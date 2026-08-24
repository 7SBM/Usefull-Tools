using System.IO;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class Skeleton
{
	public string Name { get; }

	public bool isDiscrete { get; }

	public string[] bones { get; }

	public string pivotsNameObsolete { get; }

	public Skeleton(BinaryReaderEx input)
	{
		int version = input.Version;
		Name = input.ReadAsciiz();
		if (!(Name == ""))
		{
			if (version >= 23)
			{
				isDiscrete = input.ReadBoolean();
			}
			int num = input.ReadInt32();
			if (num < 0 || num > 100000)
				throw new InvalidDataException($"Skeleton: invalid bone count {num}");
			bones = new string[num * 2];
			for (int i = 0; i < num; i++)
			{
				bones[i * 2] = input.ReadAsciiz();
				bones[i * 2 + 1] = input.ReadAsciiz();
			}
			if (version > 40)
			{
				pivotsNameObsolete = input.ReadAsciiz();
			}
		}
	}
}
