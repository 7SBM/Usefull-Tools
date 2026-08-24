using BisDll.Common.Math;

namespace BisDll.Model.ODOL;

public abstract class STPair
{
	public Vector3P S { get; } = new Vector3P();

	public Vector3P T { get; } = new Vector3P();
}
