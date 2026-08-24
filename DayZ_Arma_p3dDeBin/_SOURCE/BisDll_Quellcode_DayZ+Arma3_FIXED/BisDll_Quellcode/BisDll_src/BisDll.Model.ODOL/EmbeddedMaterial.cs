using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BisDll.Common;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class EmbeddedMaterial : IDeserializable
{
	private enum EFogMode
	{
		FM_None,
		FM_Fog,
		FM_Alpha,
		FM_FogAlpha
	}

	private enum EMainLight
	{
		ML_None,
		ML_Sun,
		ML_Sky,
		ML_Horizon,
		ML_Stars,
		ML_SunObject,
		ML_SunHaloObject,
		ML_MoonObject,
		ML_MoonHaloObject
	}

	public enum PixelShaderID : uint
	{
		PSNormal = 0u,
		PSNormalDXTA = 1u,
		PSNormalMap = 2u,
		PSNormalMapThrough = 3u,
		PSNormalMapGrass = 4u,
		PSNormalMapDiffuse = 5u,
		PSDetail = 6u,
		PSInterpolation = 7u,
		PSWater = 8u,
		PSWaterSimple = 9u,
		PSWhite = 10u,
		PSWhiteAlpha = 11u,
		PSAlphaShadow = 12u,
		PSAlphaNoShadow = 13u,
		PSDummy0 = 14u,
		PSDetailMacroAS = 15u,
		PSNormalMapMacroAS = 16u,
		PSNormalMapDiffuseMacroAS = 17u,
		PSNormalMapSpecularMap = 18u,
		PSNormalMapDetailSpecularMap = 19u,
		PSNormalMapMacroASSpecularMap = 20u,
		PSNormalMapDetailMacroASSpecularMap = 21u,
		PSNormalMapSpecularDIMap = 22u,
		PSNormalMapDetailSpecularDIMap = 23u,
		PSNormalMapMacroASSpecularDIMap = 24u,
		PSNormalMapDetailMacroASSpecularDIMap = 25u,
		PSTerrain1 = 26u,
		PSTerrain2 = 27u,
		PSTerrain3 = 28u,
		PSTerrain4 = 29u,
		PSTerrain5 = 30u,
		PSTerrain6 = 31u,
		PSTerrain7 = 32u,
		PSTerrain8 = 33u,
		PSTerrain9 = 34u,
		PSTerrain10 = 35u,
		PSTerrain11 = 36u,
		PSTerrain12 = 37u,
		PSTerrain13 = 38u,
		PSTerrain14 = 39u,
		PSTerrain15 = 40u,
		PSTerrainSimple1 = 41u,
		PSTerrainSimple2 = 42u,
		PSTerrainSimple3 = 43u,
		PSTerrainSimple4 = 44u,
		PSTerrainSimple5 = 45u,
		PSTerrainSimple6 = 46u,
		PSTerrainSimple7 = 47u,
		PSTerrainSimple8 = 48u,
		PSTerrainSimple9 = 49u,
		PSTerrainSimple10 = 50u,
		PSTerrainSimple11 = 51u,
		PSTerrainSimple12 = 52u,
		PSTerrainSimple13 = 53u,
		PSTerrainSimple14 = 54u,
		PSTerrainSimple15 = 55u,
		PSGlass = 56u,
		PSNonTL = 57u,
		PSNormalMapSpecularThrough = 58u,
		PSGrass = 59u,
		PSNormalMapThroughSimple = 60u,
		PSNormalMapSpecularThroughSimple = 61u,
		PSRoad = 62u,
		PSShore = 63u,
		PSShoreWet = 64u,
		PSRoad2Pass = 65u,
		PSShoreFoam = 66u,
		PSNonTLFlare = 67u,
		PSNormalMapThroughLowEnd = 68u,
		PSTerrainGrass1 = 69u,
		PSTerrainGrass2 = 70u,
		PSTerrainGrass3 = 71u,
		PSTerrainGrass4 = 72u,
		PSTerrainGrass5 = 73u,
		PSTerrainGrass6 = 74u,
		PSTerrainGrass7 = 75u,
		PSTerrainGrass8 = 76u,
		PSTerrainGrass9 = 77u,
		PSTerrainGrass10 = 78u,
		PSTerrainGrass11 = 79u,
		PSTerrainGrass12 = 80u,
		PSTerrainGrass13 = 81u,
		PSTerrainGrass14 = 82u,
		PSTerrainGrass15 = 83u,
		PSCrater1 = 84u,
		PSCrater2 = 85u,
		PSCrater3 = 86u,
		PSCrater4 = 87u,
		PSCrater5 = 88u,
		PSCrater6 = 89u,
		PSCrater7 = 90u,
		PSCrater8 = 91u,
		PSCrater9 = 92u,
		PSCrater10 = 93u,
		PSCrater11 = 94u,
		PSCrater12 = 95u,
		PSCrater13 = 96u,
		PSCrater14 = 97u,
		PSSprite = 98u,
		PSSpriteSimple = 99u,
		PSCloud = 100u,
		PSHorizon = 101u,
		PSSuper = 102u,
		PSMulti = 103u,
		PSTerrainX = 104u,
		PSTerrainSimpleX = 105u,
		PSTerrainGrassX = 106u,
		PSTree = 107u,
		PSTreePRT = 108u,
		PSTreeSimple = 109u,
		PSSkin = 110u,
		PSCalmWater = 111u,
		PSTreeAToC = 112u,
		PSGrassAToC = 113u,
		PSTreeAdv = 114u,
		PSTreeAdvSimple = 115u,
		PSTreeAdvTrunk = 116u,
		PSTreeAdvTrunkSimple = 117u,
		PSTreeAdvAToC = 118u,
		PSTreeAdvSimpleAToC = 119u,
		PSTreeSN = 120u,
		PSSpriteExtTi = 121u,
		PSTerrainSNX = 122u,
		PSSimulWeatherClouds = 123u,
		PSSimulWeatherCloudsWithLightning = 124u,
		PSSimulWeatherCloudsCPU = 125u,
		PSSimulWeatherCloudsWithLightningCPU = 126u,
		PSSuperExt = 127u,
		PSSuperAToC = 128u,
		NPixelShaderID = 129u,
		PSNone = 129u,
		PSUninitialized = uint.MaxValue
	}

	public enum VertexShaderID
	{
		VSBasic,
		VSNormalMap,
		VSNormalMapDiffuse,
		VSGrass,
		VSDummy1,
		VSDummy2,
		VSShadowVolume,
		VSWater,
		VSWaterSimple,
		VSSprite,
		VSPoint,
		VSNormalMapThrough,
		VSDummy3,
		VSTerrain,
		VSBasicAS,
		VSNormalMapAS,
		VSNormalMapDiffuseAS,
		VSGlass,
		VSNormalMapSpecularThrough,
		VSNormalMapThroughNoFade,
		VSNormalMapSpecularThroughNoFade,
		VSShore,
		VSTerrainGrass,
		VSSuper,
		VSMulti,
		VSTree,
		VSTreeNoFade,
		VSTreePRT,
		VSTreePRTNoFade,
		VSSkin,
		VSCalmWater,
		VSTreeAdv,
		VSTreeAdvTrunk,
		VSSimulWeatherClouds,
		VSSimulWeatherCloudsCPU,
		NVertexShaderID
	}

	public string materialName;

	private uint version;

	private ColorP emissive;

	private ColorP ambient;

	private ColorP diffuse;

	private ColorP forcedDiffuse;

	private ColorP specular;

	private ColorP specularCopy;

	public float specularPower;

	public PixelShaderID pixelShader;

	public VertexShaderID vertexShader;

	private EMainLight mainLight;

	private EFogMode fogMode;

	public string surfaceFile;

	private uint nRenderFlags;

	private uint renderFlags;

	private uint nStages;

	private uint nTexGens;

	public StageTexture[] stageTextures;

	public StageTransform[] stageTransforms;

	private StageTexture stageTI = new StageTexture();

	public void writeToFile(string fileName)
	{
		List<string> list = new List<string>();
		string item = string.Concat("Emissive[] = ", emissive, ";");
		string item2 = string.Concat("Ambient[] = ", ambient, ";");
		string item3 = string.Concat("Diffuse[] = ", diffuse, ";");
		string item4 = string.Concat("forcedDiffuse[] = ", forcedDiffuse, ";");
		string item5 = string.Concat("Specular[] = ", specular, ";");
		string item6 = "specularPower = " + specularPower.ToString(new CultureInfo("en-GB").NumberFormat) + ";";
		string text = Enum.GetName(pixelShader.GetType(), pixelShader);
		string text2 = Enum.GetName(vertexShader.GetType(), vertexShader);
		if (text == "")
		{
			text = string.Concat("Unknown PixelShaderID (", pixelShader, ")");
		}
		if (text2 == "")
		{
			text2 = string.Concat("Unknown VertexShaderID (", vertexShader, ")");
		}
		string item7 = "PixelShader = " + text + ";";
		string item8 = "VertexShader = " + text2 + ";";
		list.Add(item);
		list.Add(item2);
		list.Add(item3);
		list.Add(item4);
		list.Add(item5);
		list.Add(item6);
		list.Add(item7);
		list.Add(item8);
		if (surfaceFile != "")
		{
			list.Add("surfaceInfo = " + surfaceFile + ";");
		}
		if (stageTextures != null)
		{
			for (int i = 0; i < stageTextures.Length; i++)
			{
				list.Add("class Stage" + i + 1);
				list.Add("{");
				list.Add("\tfilter = " + Enum.GetName(stageTextures[i].textureFilter.GetType(), stageTextures[i].textureFilter) + ";");
				list.Add("\ttexture = " + stageTextures[i].texture + ";");
				list.Add(string.Concat("\tuvSource = ", stageTransforms[i].uvSource, ";"));
				list.Add("\tclass uvTransform");
				list.Add("\t{");
				list.Add(string.Concat("\t\taside[] = ", stageTransforms[i].transformation.Orientation.Aside, ";"));
				list.Add(string.Concat("\t\tup[] = ", stageTransforms[i].transformation.Orientation.Up, ";"));
				list.Add(string.Concat("\t\tdir[] = ", stageTransforms[i].transformation.Orientation.Dir, ";"));
				list.Add(string.Concat("\t\tpos[] = ", stageTransforms[i].transformation.Position, ";"));
				list.Add("\t};");
				list.Add("};");
			}
		}
		File.WriteAllLines(fileName, list.ToArray());
	}

	public void ReadObject(BinaryReaderEx input)
	{
		materialName = input.ReadAsciiz();
		version = input.ReadUInt32();
		emissive.read(input);
		ambient.read(input);
		diffuse.read(input);
		forcedDiffuse.read(input);
		specular.read(input);
		specularCopy.read(input);
		if (!input.IsArma3Format && version > 10)
		{
			// Two additional color fields present in DayZ material version >= 11
			// (not present in Arma 3 EmbeddedMaterial format)
			new ColorP(input);
			new ColorP(input);
		}
		specularPower = input.ReadSingle();
		if (!input.IsArma3Format)
		{
			if (version >= 20)
			{
				// 72 bytes of additional rendering parameters (18 x uint32) added in version 20
				for (int skip = 0; skip < 18; skip++) input.ReadUInt32();
			}
			else if (version > 10)
			{
				// 24 bytes of additional rendering parameters (6 x uint32) present in versions 11-19
				for (int skip = 0; skip < 6; skip++) input.ReadUInt32();
			}
		}
		pixelShader = (PixelShaderID)input.ReadUInt32();
		vertexShader = (VertexShaderID)input.ReadUInt32();
		mainLight = (EMainLight)input.ReadUInt32();
		fogMode = (EFogMode)input.ReadUInt32();
		if (version == 3)
		{
			input.ReadBoolean();
		}
		if (version >= 6)
		{
			surfaceFile = input.ReadAsciiz();
		}
		if (version >= 4)
		{
			nRenderFlags = input.ReadUInt32();
			renderFlags = input.ReadUInt32();
		}
		if (version > 6)
		{
			nStages = input.ReadUInt32();
		}
		if (version > 8)
		{
			nTexGens = input.ReadUInt32();
		}
		stageTextures = new StageTexture[nStages];
		stageTransforms = new StageTransform[nTexGens];
		if (version < 8)
		{
			for (int i = 0; i < nStages; i++)
			{
				stageTransforms[i] = new StageTransform(input);
				stageTextures[i].read(input, version);
			}
		}
		else
		{
			for (int j = 0; j < nStages; j++)
			{
				stageTextures[j] = new StageTexture();
				stageTextures[j].read(input, version);
			}
			for (int k = 0; k < nTexGens; k++)
			{
				stageTransforms[k] = new StageTransform(input);
			}
		}
		if (version >= 10)
		{
			stageTI.read(input, version);
		}
	}
}
