using BisDll.Common;
using BisDll.Common.Math;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class ODOL_ModelInfo
{
	public int special { get; private set; }

	public float BoundingSphere { get; private set; }

	public float GeometrySphere { get; private set; }

	public int remarks { get; private set; }

	public int andHints { get; private set; }

	public int orHints { get; private set; }

	public Vector3P AimingCenter { get; private set; }

	public PackedColor color { get; private set; }

	public PackedColor colorType { get; private set; }

	public float viewDensity { get; private set; }

	public Vector3P bboxMin { get; private set; }

	public Vector3P bboxMax { get; private set; }

	public float propertyLodDensityCoef { get; private set; }

	public float propertyDrawImportance { get; private set; }

	public Vector3P bboxMinVisual { get; private set; }

	public Vector3P bboxMaxVisual { get; private set; }

	public Vector3P boundingCenter { get; private set; }

	public Vector3P geometryCenter { get; private set; }

	public Vector3P centerOfMass { get; private set; }

	public Matrix3P invInertia { get; private set; }

	public bool autoCenter { get; private set; }

	public bool lockAutoCenter { get; private set; }

	public bool canOcclude { get; private set; }

	public bool canBeOccluded { get; private set; }

	public bool AICovers { get; private set; }

	public float htMin { get; private set; }

	public float htMax { get; private set; }

	public float afMax { get; private set; }

	public float mfMax { get; private set; }

	public float mFact { get; private set; }

	public float tBody { get; private set; }

	public bool forceNotAlphaModel { get; private set; }

	public SBSource sbSource { get; private set; }

	public bool prefershadowvolume { get; private set; }

	public float shadowOffset { get; private set; }

	public bool animated { get; private set; }

	public Skeleton skeleton { get; private set; }

	public MapType mapType { get; private set; }

	public float[] massArray { get; private set; }

	public float mass { get; private set; }

	public float invMass { get; private set; }

	public float armor { get; private set; }

	public float invArmor { get; private set; }

	public float propertyExplosionShielding { get; private set; }

	public byte memory { get; private set; }

	public byte geometry { get; private set; }

	public byte geometrySimple { get; private set; }

	public byte geometryPhys { get; private set; }

	public byte geometryFire { get; private set; }

	public byte geometryView { get; private set; }

	public byte geometryViewPilot { get; private set; }

	public byte geometryViewGunner { get; private set; }

	public byte geometryViewCargo { get; private set; }

	public byte landContact { get; private set; }

	public byte roadway { get; private set; }

	public byte paths { get; private set; }

	public byte hitpoints { get; private set; }

	public byte minShadow { get; private set; }

	public bool canBlend { get; private set; }

	public string propertyClass { get; private set; }

	public string propertyDamage { get; private set; }

	public bool propertyFrequent { get; private set; }

	public int[] preferredShadowVolumeLod { get; private set; }

	public int[] preferredShadowBufferLod { get; private set; }

	public int[] preferredShadowBufferLodVis { get; private set; }

	internal ODOL_ModelInfo(BinaryReaderEx input, int nLods)
	{
		read(input, nLods);
	}

	public void read(BinaryReaderEx input, int nLods)
	{
		int version = input.Version;
		special = input.ReadInt32();
		BoundingSphere = input.ReadSingle();
		GeometrySphere = input.ReadSingle();
		remarks = input.ReadInt32();
		andHints = input.ReadInt32();
		orHints = input.ReadInt32();
		AimingCenter = new Vector3P(input);
		color = new PackedColor(input.ReadUInt32());
		colorType = new PackedColor(input.ReadUInt32());
		viewDensity = input.ReadSingle();
		bboxMin = new Vector3P(input);
		bboxMax = new Vector3P(input);
		if (version >= 70)
		{
			propertyLodDensityCoef = input.ReadSingle();
		}
		if (version >= 71)
		{
			propertyDrawImportance = input.ReadSingle();
		}
		if (version >= 52)
		{
			bboxMinVisual = new Vector3P(input);
			bboxMaxVisual = new Vector3P(input);
		}
		boundingCenter = new Vector3P(input);
		geometryCenter = new Vector3P(input);
		centerOfMass = new Vector3P(input);
		invInertia = new Matrix3P(input);
		autoCenter = input.ReadBoolean();
		lockAutoCenter = input.ReadBoolean();
		canOcclude = input.ReadBoolean();
		canBeOccluded = input.ReadBoolean();
		if (version >= 73)
		{
			AICovers = input.ReadBoolean();
		}
		if ((version >= 42 && version < 10000) || version >= 10042)
		{
			htMin = input.ReadSingle();
			htMax = input.ReadSingle();
			afMax = input.ReadSingle();
			mfMax = input.ReadSingle();
		}
		if ((version >= 43 && version < 10000) || version >= 10043)
		{
			mFact = input.ReadSingle();
			tBody = input.ReadSingle();
		}
		if (version >= 33)
		{
			forceNotAlphaModel = input.ReadBoolean();
		}
		if (version >= 37)
		{
			sbSource = (SBSource)input.ReadInt32();
			prefershadowvolume = input.ReadBoolean();
		}
		if (version >= 48)
		{
			shadowOffset = input.ReadSingle();
		}
		if (version >= 49 && version <= 55)
		{
			// Field present in ODOL v49-v55 (DayZ and early Arma 3).
			// Removed in ODOL v56+ (Arma 3 mid/late).
			input.ReadSingle();
			input.ReadBoolean();
		}
		animated = input.ReadBoolean();
		skeleton = new Skeleton(input);
		mapType = (MapType)input.ReadByte();
		massArray = input.ReadCompressedFloatArray();
		mass = input.ReadSingle();
		invMass = input.ReadSingle();
		armor = input.ReadSingle();
		invArmor = input.ReadSingle();
		if (version >= 72)
		{
			propertyExplosionShielding = input.ReadSingle();
		}
		if (version >= 53)
		{
			geometrySimple = input.ReadByte();
		}
		if (version >= 54)
		{
			geometryPhys = input.ReadByte();
		}
		memory = input.ReadByte();
		geometry = input.ReadByte();
		geometryFire = input.ReadByte();
		geometryView = input.ReadByte();
		geometryViewPilot = input.ReadByte();
		geometryViewGunner = input.ReadByte();
		input.ReadSByte();
		geometryViewCargo = input.ReadByte();
		landContact = input.ReadByte();
		roadway = input.ReadByte();
		paths = input.ReadByte();
		hitpoints = input.ReadByte();
		minShadow = (byte)input.ReadUInt32();
		if (version >= 38 && version < 50)
		{
			canBlend = input.ReadBoolean();
		}
		propertyClass = input.ReadAsciiz();
		propertyDamage = input.ReadAsciiz();
		propertyFrequent = input.ReadBoolean();
		if (version >= 31)
		{
			input.ReadUInt32();
		}
		if (version >= 57)
		{
			preferredShadowVolumeLod = new int[nLods];
			preferredShadowBufferLod = new int[nLods];
			preferredShadowBufferLodVis = new int[nLods];
			for (int i = 0; i < nLods; i++)
			{
				preferredShadowVolumeLod[i] = input.ReadInt32();
			}
			for (int j = 0; j < nLods; j++)
			{
				preferredShadowBufferLod[j] = input.ReadInt32();
			}
			for (int k = 0; k < nLods; k++)
			{
				preferredShadowBufferLodVis[k] = input.ReadInt32();
			}
		}
	}
}
