using BisDll.Common.Math;
using BisDll.Stream;

namespace BisDll.Model.MLOD;

public class Point : Vector3P
{
	public PointFlags PointFlags { get; private set; }

	public Point(Vector3P pos, PointFlags flags)
		: base(pos.X, pos.Y, pos.Z)
	{
		PointFlags = flags;
	}

	public Point(BinaryReaderEx input)
		: base(input)
	{
		PointFlags = (PointFlags)input.ReadUInt32();
	}

	public new void write(BinaryWriter output)
	{
		base.write(output);
		output.Write((uint)PointFlags);
	}
}
