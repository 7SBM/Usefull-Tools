using System;
using System.Linq;
using BisDll.Common.Math;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class LOD : P3D_LOD, IComparable<LOD>
{
	private struct PointWeight(int index, byte weight)
	{
		public int pointIndex = index;

		public byte weight = weight;
	}

	private uint odolVersion;

	private Proxy[] proxies;

	private int[] subSkeletonsToSkeleton;

	private SubSkeletonIndexSet[] skeletonToSubSkeleton;

	private uint vertexCount;

	private float faceArea;

	private ClipFlags[] clipOldFormat;

	private ClipFlags[] clip;

	private ClipFlags orHints;

	private ClipFlags andHints;

	private Vector3P bMin;

	private Vector3P bMax;

	private Vector3P bCenter;

	private float bRadius;

	private string[] textures;

	private EmbeddedMaterial[] materials;

	private VertexIndex[] pointToVertex;

	private VertexIndex[] vertexToPoint;

	private Polygons polygons;

	private Section[] sections;

	private NamedSelection[] namedSelections;

	private uint nNamedProperties;

	private string[,] namedProperties;

	private Keyframe[] frames;

	private int colorTop;

	private int color;

	private int special;

	private bool vertexBoneRefIsSimple;

	private uint sizeOfRestData;

	private uint nUVSets;

	private UVSet[] uvSets;

	private Vector3P[] vertices;

	private Vector3P[] normals;

	private STPair[] STCoords;

	private AnimationRTWeight[] vertexBoneRef;

	private VertexNeighborInfo[] neighborBoneRef;

	public NamedSelection[] NamedSelections => namedSelections;

	public override string[] MaterialNames => materials.Select((EmbeddedMaterial m) => m.materialName).ToArray();

	public EmbeddedMaterial[] Materials => materials;

	public int VertexCount => vertices.Length;

	public int SectionCount => sections.Length;

	public int TextureCount => textures.Length;

	public int PolygonCount => polygons.Faces.Length;

	public int MaterialCount => materials.Length;

	public AnimationRTWeight[] VertexBoneRef => vertexBoneRef;

	public VertexNeighborInfo[] NeighborBoneRef => neighborBoneRef;

	public ClipFlags[] ClipFlags
	{
		get
		{
			if (odolVersion < 50)
			{
				return clipOldFormat;
			}
			return clip;
		}
	}

	public Vector3P[] Vertices => vertices;

	public override Vector3P[] Normals => normals;

	public Section[] Sections => sections;

	public UVSet[] UVSets => uvSets;

	public Polygon[] Faces => polygons.Faces;

	public string[,] NamedProperties => namedProperties;

	public Keyframe[] Frames => frames;

	public int[] SubSkeletonsToSkeleton => subSkeletonsToSkeleton;

	public Proxy[] Proxies => proxies;

	public override Vector3P[] Points => Vertices;

	public override string[] Textures => textures;

	public void read(BinaryReaderEx input, float resolution)
	{
		odolVersion = (uint)input.Version;
		base.resolution = resolution;
		Console.Error.WriteLine($"  LOD.read start pos={input.Position} res={resolution}");
		proxies = input.ReadArray<Proxy>();
		Console.Error.WriteLine($"  after proxies({proxies.Length}) pos={input.Position}");
		subSkeletonsToSkeleton = input.ReadIntArray();
		Console.Error.WriteLine($"  after subSkel({subSkeletonsToSkeleton.Length}) pos={input.Position}");
		skeletonToSubSkeleton = input.ReadArray<SubSkeletonIndexSet>();
		Console.Error.WriteLine($"  after skelToSub({skeletonToSubSkeleton.Length}) pos={input.Position}");
		if (odolVersion >= 50)
		{
			vertexCount = input.ReadUInt32();
		}
		else
		{
			int[] array = input.ReadCondensedIntArray();
			clipOldFormat = Array.ConvertAll(array, (int item) => (ClipFlags)item);
		}
		if (odolVersion >= 51)
		{
			faceArea = input.ReadSingle();
		}
		orHints = (ClipFlags)input.ReadInt32();
		andHints = (ClipFlags)input.ReadInt32();
		bMin = new Vector3P(input);
		bMax = new Vector3P(input);
		bCenter = new Vector3P(input);
		bRadius = input.ReadSingle();
		textures = input.ReadStringArray();
		Console.Error.WriteLine($"  after textures({textures.Length}) pos={input.Position}");
		int matCount = input.ReadInt32();
		Console.Error.WriteLine($"  materials count={matCount} at pos={input.Position-4}");
		if (matCount < 0 || matCount > 10000) throw new Exception($"Invalid materials count {matCount} at pos {input.Position-4} - LOD format mismatch");
		materials = input.ReadArray<EmbeddedMaterial>(matCount);
		pointToVertex = input.ReadCompressedVertexIndexArray();
		vertexToPoint = input.ReadCompressedVertexIndexArray();
		polygons = new Polygons(input);
		sections = input.ReadArray<Section>();
		namedSelections = input.ReadArray<NamedSelection>();
		nNamedProperties = input.ReadUInt32();
		namedProperties = new string[nNamedProperties, 2];
		for (int num = 0; num < nNamedProperties; num++)
		{
			namedProperties[num, 0] = input.ReadAsciiz();
			namedProperties[num, 1] = input.ReadAsciiz();
		}
		frames = input.ReadArray<Keyframe>();
		colorTop = input.ReadInt32();
		color = input.ReadInt32();
		special = input.ReadInt32();
		vertexBoneRefIsSimple = input.ReadBoolean();
		sizeOfRestData = input.ReadUInt32();
		if (odolVersion >= 50)
		{
			int[] array2 = input.ReadCondensedIntArray();
			clip = Array.ConvertAll(array2, (int item) => (ClipFlags)item);
		}
		UVSet uVSet = new UVSet();
		uVSet.read(input, odolVersion);
		nUVSets = input.ReadUInt32();
		uvSets = new UVSet[nUVSets];
		uvSets[0] = uVSet;
		for (int num2 = 1; num2 < nUVSets; num2++)
		{
			uvSets[num2] = new UVSet();
			uvSets[num2].read(input, odolVersion);
		}
		vertices = input.ReadCompressedObjectArray<Vector3P>(12);
		if (odolVersion >= 45)
		{
			Vector3PCompressed[] array3 = input.ReadCondensedObjectArray<Vector3PCompressed>(4);
			normals = Array.ConvertAll(array3, (Converter<Vector3PCompressed, Vector3P>)((Vector3PCompressed item) => item));
		}
		else
		{
			normals = input.ReadCondensedObjectArray<Vector3P>(12);
		}
		STCoords = (STPair[])((odolVersion >= 45) ? ((Array)input.ReadCompressedObjectArray<STPairCompressed>(8)) : ((Array)input.ReadCompressedObjectArray<STPairUncompressed>(24)));
		vertexBoneRef = input.ReadCompressedObjectArray<AnimationRTWeight>(12);
		neighborBoneRef = input.ReadCompressedObjectArray<VertexNeighborInfo>(32);
		if (odolVersion >= 67)
		{
			input.ReadUInt32();
		}
		if (odolVersion >= 68)
		{
			input.ReadByte();
		}
	}

	public void write(BinaryWriter output)
	{
		throw new NotImplementedException();
	}

	public int CompareTo(LOD other)
	{
		return resolution.CompareTo(other.resolution);
	}
}
