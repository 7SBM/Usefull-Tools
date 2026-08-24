using BisDll.Common.Math;

namespace BisDll.Model;

public abstract class P3D_LOD
{
	protected float resolution;

	public string Name => resolution.getLODName();

	public float Resolution => resolution;

	public abstract Vector3P[] Points { get; }

	public abstract Vector3P[] Normals { get; }

	public abstract string[] Textures { get; }

	public abstract string[] MaterialNames { get; }
}
