using System;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class UVSet
{
	private bool isDiscretized;

	private float minU;

	private float minV;

	private float maxU;

	private float maxV;

	private uint nVertices;

	private bool defaultFill;

	private byte[] defaultValue;

	private byte[] uvData;

	public float[] UVData
	{
		get
		{
			float[] array = new float[nVertices * 2];
			float num = 0f;
			float num2 = 0f;
			double num3 = 1.0;
			double num4 = 1.0;
			if (isDiscretized)
			{
				num3 = maxU - minU;
				num4 = maxV - minV;
			}
			if (defaultFill)
			{
				if (isDiscretized)
				{
					num = scale(BitConverter.ToInt16(defaultValue, 0), num3, minU);
					num2 = scale(BitConverter.ToInt16(defaultValue, 2), num4, minV);
				}
				else
				{
					num = BitConverter.ToSingle(defaultValue, 0);
					num2 = BitConverter.ToSingle(defaultValue, 4);
				}
			}
			for (int i = 0; i < nVertices; i++)
			{
				if (isDiscretized)
				{
					array[i * 2] = (defaultFill ? num : scale(BitConverter.ToInt16(uvData, i * 4), num3, minU));
					array[i * 2 + 1] = (defaultFill ? num2 : scale(BitConverter.ToInt16(uvData, i * 4 + 2), num4, minV));
				}
				else
				{
					array[i * 2] = (defaultFill ? num : BitConverter.ToSingle(uvData, i * 8));
					array[i * 2 + 1] = (defaultFill ? num2 : BitConverter.ToSingle(uvData, i * 8 + 4));
				}
			}
			return array;
		}
	}

	private float scale(short value, double scale, float min)
	{
		return (float)(1.52587890625E-05 * (double)(value + 32767) * scale) + min;
	}

	public void read(BinaryReaderEx input, uint odolVersion)
	{
		isDiscretized = false;
		if (odolVersion >= 45)
		{
			isDiscretized = true;
			minU = input.ReadSingle();
			minV = input.ReadSingle();
			maxU = input.ReadSingle();
			maxV = input.ReadSingle();
		}
		nVertices = input.ReadUInt32();
		defaultFill = input.ReadBoolean();
		int num = ((odolVersion >= 45) ? 4 : 8);
		if (defaultFill)
		{
			defaultValue = input.ReadBytes(num);
		}
		else
		{
			uvData = input.ReadCompressed((uint)(nVertices * num));
		}
	}
}
