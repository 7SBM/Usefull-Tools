////////////////////////////////////////////////////////////////////
//DeRap: config.bin
//Produced from mikero's Dos Tools Dll version 10.13
//https://mikero.bytex.digital/Downloads
//'now' is Mon May 18 04:11:11 2026 : 'file' last modified on Wed Jun 26 12:37:57 2024
////////////////////////////////////////////////////////////////////

#define _ARMA_

class CfgPatches
{
	class Waterfall
	{
		units[] = {};
		weapons[] = {};
		requiredVersion = 0.1;
		requiredAddons[] = {"DZ_Data"};
	};
};
class CfgMods
{
	class Waterfall
	{
		dir = "\Waterfall";
		picture = "";
		action = "";
		hideName = 1;
		hidePicture = 1;
		name = "Waterfall";
		credits = "Tyson";
		author = "Tyson";
		authorID = "0";
		version = "1.0";
		extra = 0;
		type = "mod";
		dependencies[] = {"Game","World","Mission"};
		class defs
		{
			class gameScriptModule
			{
				value = "";
				files[] = {"Waterfall/scripts/3_Game"};
			};
			class worldScriptModule
			{
				value = "";
				files[] = {"Waterfall/scripts/4_World"};
			};
			class missionScriptModule
			{
				value = "";
				files[] = {"Waterfall/scripts/5_Mission"};
			};
		};
	};
};
class CfgVehicles
{
	class HouseNoDestruct;
	class RaG_WaterFall_Base: HouseNoDestruct
	{
		scope = 0;
		model = "\Waterfall\Waterfall.p3d";
		forceFarBubble = "true";
		storageCategory = 10;
	};
	class RaG_WaterFall: RaG_WaterFall_Base
	{
		scope = 1;
	};
	class RaG_WaterFall1: RaG_WaterFall_Base
	{
		scope = 1;
	};
	class RaG_WaterFall2: RaG_WaterFall_Base
	{
		scope = 1;
	};
	class RaG_WaterFall3: RaG_WaterFall_Base
	{
		scope = 1;
	};
	class RaG_WaterFall4: RaG_WaterFall_Base
	{
		scope = 1;
	};
	class RaG_WaterFall5: RaG_WaterFall_Base
	{
		scope = 1;
	};
	class RaG_WaterFallLarge: RaG_WaterFall_Base
	{
		scope = 1;
	};
};
class CfgSoundShaders
{
	class rag_waterfall_base_SoundShader
	{
		range = 100;
		rangeCurve = "defaultLFECurve";
		volume = 1;
	};
	class rag_waterfall_SoundShader: rag_waterfall_base_SoundShader
	{
		samples[] = {{"\Waterfall\sounds\waterfall",1}};
	};
};
class CfgSoundSets
{
	class rag_waterfall_base_SoundSet
	{
		sound3DProcessingType = "character3DProcessingType";
		volumeCurve = "characterAttenuationCurve";
		spatial = 1;
		doppler = 0;
		loop = 1;
	};
	class rag_waterfall_SoundSet: rag_waterfall_base_SoundSet
	{
		soundShaders[] = {"rag_waterfall_SoundShader"};
	};
};
