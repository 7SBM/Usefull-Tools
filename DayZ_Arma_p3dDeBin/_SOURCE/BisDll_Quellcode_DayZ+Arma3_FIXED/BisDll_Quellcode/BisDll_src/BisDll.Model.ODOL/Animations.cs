using System;
using BisDll.Common.Math;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class Animations
{
	public class AnimationClass : IDeserializable
	{
		public enum AnimType
		{
			Rotation,
			RotationX,
			RotationY,
			RotationZ,
			Translation,
			TranslationX,
			TranslationY,
			TranslationZ,
			Direct,
			Hide
		}

		public enum AnimAddress
		{
			AnimClamp,
			AnimLoop,
			AnimMirror,
			NAnimAddress
		}

		public AnimType animType;

		private string animName;

		private string animSource;

		private float minValue;

		private float maxValue;

		private float minPhase;

		private float maxPhase;

		private float animPeriod;

		private float initPhase;

		private AnimAddress sourceAddress;

		private float angle0;

		private float angle1;

		private float offset0;

		private float offset1;

		private Vector3P axisPos;

		private Vector3P axisDir;

		private float angle;

		private float axisOffset;

		private float hideValue;

		public void ReadObject(BinaryReaderEx input)
		{
			int version = input.Version;
			animType = (AnimType)input.ReadUInt32();
			animName = input.ReadAsciiz();
			animSource = input.ReadAsciiz();
			minPhase = input.ReadSingle();
			maxPhase = input.ReadSingle();
			minValue = input.ReadSingle();
			maxValue = input.ReadSingle();
			if (version >= 56)
			{
				animPeriod = input.ReadSingle();
				initPhase = input.ReadSingle();
			}
			sourceAddress = (AnimAddress)input.ReadUInt32();
			switch (animType)
			{
			case AnimType.Rotation:
			case AnimType.RotationX:
			case AnimType.RotationY:
			case AnimType.RotationZ:
				angle0 = input.ReadSingle();
				angle1 = input.ReadSingle();
				break;
			case AnimType.Translation:
			case AnimType.TranslationX:
			case AnimType.TranslationY:
			case AnimType.TranslationZ:
				offset0 = input.ReadSingle();
				offset1 = input.ReadSingle();
				break;
			case AnimType.Direct:
				axisPos = new Vector3P(input);
				axisDir = new Vector3P(input);
				angle = input.ReadSingle();
				axisOffset = input.ReadSingle();
				break;
			case AnimType.Hide:
				hideValue = input.ReadSingle();
				if (version >= 55)
				{
					input.ReadSingle();
				}
				break;
			default:
				throw new UnknownAnimTypeException("Unknown AnimType encountered: " + (uint)animType);
			}
		}
	}

	private AnimationClass[] animationClasses;

	private int nAnimLODs;

	private uint[][][] Bones2Anims;

	private int[][] Anims2Bones;

	private Vector3P[][][] axisData;

	public void read(BinaryReaderEx input)
	{
		animationClasses = input.ReadArray<AnimationClass>();
		int num = animationClasses.Length;
		nAnimLODs = input.ReadInt32();
		Bones2Anims = new uint[nAnimLODs][][];
		for (int i = 0; i < nAnimLODs; i++)
		{
			uint num2 = input.ReadUInt32();
			Bones2Anims[i] = new uint[num2][];
			for (int j = 0; j < num2; j++)
			{
				uint num3 = input.ReadUInt32();
				Bones2Anims[i][j] = new uint[num3];
				for (int k = 0; k < num3; k++)
				{
					Bones2Anims[i][j][k] = input.ReadUInt32();
				}
			}
		}
		Anims2Bones = new int[nAnimLODs][];
		axisData = new Vector3P[nAnimLODs][][];
		for (int l = 0; l < nAnimLODs; l++)
		{
			Anims2Bones[l] = new int[num];
			axisData[l] = new Vector3P[num][];
			for (int m = 0; m < num; m++)
			{
				Anims2Bones[l][m] = input.ReadInt32();
				if (Anims2Bones[l][m] != -1 && animationClasses[m].animType != AnimationClass.AnimType.Direct && animationClasses[m].animType != AnimationClass.AnimType.Hide)
				{
					axisData[l][m] = new Vector3P[2];
					axisData[l][m][0] = new Vector3P(input);
					axisData[l][m][1] = new Vector3P(input);
				}
			}
		}
	}
}
