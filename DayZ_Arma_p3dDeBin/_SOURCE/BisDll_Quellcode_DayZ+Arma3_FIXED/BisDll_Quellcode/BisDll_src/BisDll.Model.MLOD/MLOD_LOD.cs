using System;
using System.Collections.Generic;
using System.Linq;
using BisDll.Common.Math;
using BisDll.Stream;

namespace BisDll.Model.MLOD;

public class MLOD_LOD : P3D_LOD
{
	public uint unk1;

	public Point[] points;

	public Vector3P[] normals;

	public Face[] faces;

	public List<Tagg> taggs;

	public override Vector3P[] Points => points;

	public override Vector3P[] Normals => normals;

	public override string[] Textures => faces.Select((Face f) => f.Texture).Distinct().ToArray();

	public override string[] MaterialNames => faces.Select((Face f) => f.Material).Distinct().ToArray();

	public MLOD_LOD()
	{
	}

	public MLOD_LOD(float resolution)
	{
		base.resolution = resolution;
	}

	private Tagg readTagg(BinaryReaderEx input)
	{
		Tagg tagg = new MassTagg();
		if (!input.ReadBoolean())
		{
			throw new Exception("Deactivated Tagg?");
		}
		tagg.Name = input.ReadAsciiz();
		tagg.DataSize = input.ReadUInt32();
		switch (tagg.Name)
		{
		case "#SharpEdges#":
		{
			SharpEdgesTagg sharpEdgesTagg = new SharpEdgesTagg();
			sharpEdgesTagg.Name = "#SharpEdges#";
			sharpEdgesTagg.DataSize = tagg.DataSize;
			sharpEdgesTagg.read(input);
			return sharpEdgesTagg;
		}
		case "#Property#":
		{
			PropertyTagg propertyTagg = new PropertyTagg();
			propertyTagg.Name = "#Property#";
			propertyTagg.DataSize = tagg.DataSize;
			propertyTagg.read(input);
			return propertyTagg;
		}
		case "#Mass#":
		{
			MassTagg massTagg = new MassTagg();
			massTagg.Name = "#Mass#";
			massTagg.DataSize = tagg.DataSize;
			massTagg.read(input);
			return massTagg;
		}
		case "#UVSet#":
		{
			UVSetTagg uVSetTagg = new UVSetTagg();
			uVSetTagg.Name = "#UVSet#";
			uVSetTagg.DataSize = tagg.DataSize;
			uVSetTagg.read(input, faces);
			return uVSetTagg;
		}
		case "#Lock#":
		{
			LockTagg lockTagg = new LockTagg();
			lockTagg.Name = "#Lock#";
			lockTagg.DataSize = tagg.DataSize;
			lockTagg.read(input, Points.Length, faces.Length);
			return lockTagg;
		}
		case "#Selected#":
		{
			SelectedTagg selectedTagg = new SelectedTagg();
			selectedTagg.Name = "#Selected#";
			selectedTagg.DataSize = tagg.DataSize;
			selectedTagg.read(input, Points.Length, faces.Length);
			return selectedTagg;
		}
		case "#Animation#":
		{
			AnimationTagg animationTagg = new AnimationTagg();
			animationTagg.Name = "#Animation#";
			animationTagg.DataSize = tagg.DataSize;
			animationTagg.read(input);
			return animationTagg;
		}
		case "#EndOfFile#":
			return tagg;
		default:
		{
			NamedSelectionTagg namedSelectionTagg = new NamedSelectionTagg();
			namedSelectionTagg.Name = tagg.Name;
			namedSelectionTagg.DataSize = tagg.DataSize;
			namedSelectionTagg.read(input, Points.Length, faces.Length);
			return namedSelectionTagg;
		}
		}
	}

	private void writeTagg(BinaryWriter output, Tagg tagg)
	{
		switch (tagg.Name)
		{
		case "#SharpEdges#":
			((SharpEdgesTagg)tagg).write(output);
			break;
		case "#Property#":
			((PropertyTagg)tagg).write(output);
			break;
		case "#Mass#":
			((MassTagg)tagg).write(output);
			break;
		case "#UVSet#":
			((UVSetTagg)tagg).write(output);
			break;
		case "#Lock#":
			((LockTagg)tagg).write(output);
			break;
		case "#Selected#":
			((SelectedTagg)tagg).write(output);
			break;
		case "#Animation#":
			((AnimationTagg)tagg).write(output);
			break;
		default:
			((NamedSelectionTagg)tagg).write(output);
			break;
		case "#EndOfFile#":
			break;
		}
	}

	public void read(BinaryReaderEx input)
	{
		if (input.ReadAscii(4) != "P3DM")
		{
			throw new Exception("Only P3DM LODs are supported");
		}
		if (input.ReadUInt32() != 28 || input.ReadUInt32() != 256)
		{
			throw new Exception("Unknown P3DM version");
		}
		uint num = input.ReadUInt32();
		uint num2 = input.ReadUInt32();
		uint num3 = input.ReadUInt32();
		unk1 = input.ReadUInt32();
		points = new Point[num];
		normals = new Vector3P[num2];
		faces = new Face[num3];
		for (int i = 0; i < num; i++)
		{
			points[i] = new Point(input);
		}
		for (int j = 0; j < num2; j++)
		{
			normals[j] = new Vector3P(input);
		}
		for (int k = 0; k < num3; k++)
		{
			faces[k] = new Face(input);
		}
		if (input.ReadAscii(4) != "TAGG")
		{
			throw new Exception("TAGG expected");
		}
		taggs = new List<Tagg>();
		Tagg tagg;
		do
		{
			tagg = readTagg(input);
			if (tagg.Name != "#EndOfFile#")
			{
				taggs.Add(tagg);
			}
		}
		while (tagg.Name != "#EndOfFile#");
		resolution = input.ReadSingle();
	}

	public void write(BinaryWriter output)
	{
		int num = points.Length;
		int num2 = normals.Length;
		int num3 = faces.Length;
		output.writeAscii("P3DM", 4u);
		output.Write(28);
		output.Write(256);
		output.Write(num);
		output.Write(num2);
		output.Write(num3);
		output.Write(unk1);
		for (int i = 0; i < num; i++)
		{
			points[i].write(output);
		}
		for (int j = 0; j < num2; j++)
		{
			normals[j].write(output);
		}
		for (int k = 0; k < num3; k++)
		{
			faces[k].write(output);
		}
		output.writeAscii("TAGG", 4u);
		foreach (Tagg tagg in taggs)
		{
			writeTagg(output, tagg);
		}
		output.Write(value: true);
		output.writeAsciiz("#EndOfFile#");
		output.Write(0);
		output.Write(resolution);
	}

	public float getHeight()
	{
		int num = Points.Length;
		if ((long)num <= 1L)
		{
			return 0f;
		}
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		for (int i = 0; i < num; i++)
		{
			float y = points[i].Y;
			if (y > num3)
			{
				num3 = y;
			}
			if (y < num2)
			{
				num2 = y;
			}
		}
		return num3 - num2;
	}
}
