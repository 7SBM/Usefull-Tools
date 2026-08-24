using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BisDll.Stream;

namespace BisDll.Model.ODOL;

public class ODOL : P3D
{
	public const int LATEST_VERSION = 75;

	public const int MINIMAL_VERSION = 28;

	private string muzzleFlash;

	private uint appID;

	private int nLods;

	private float[] resolutions;

	public ODOL_ModelInfo modelInfo;

	private bool hasAnims;

	private Animations animations = new Animations();

	private uint[] lodStartAdresses;

	private uint[] lodEndAdresses;

	private bool[] permanent;

	private List<LoadableLodInfo> LoadableLodInfos;

	private LOD[] lods;

	public Skeleton Skeleton => modelInfo.skeleton;

	public override float Mass => modelInfo.mass;

	public override P3D_LOD[] LODs => lods;

	public ODOL(string fileName)
		: this(File.OpenRead(fileName))
	{
	}

	public ODOL(System.IO.Stream stream)
	{
		read(new BinaryReaderEx(stream));
	}

	public bool isSnappable()
	{
		LOD lOD = lods.FirstOrDefault((LOD l) => l.Resolution.getLODType() == LodName.Memory);
		if (lOD != null && lOD.NamedSelections.Where((NamedSelection ns) => ns.Name.Equals("lb", StringComparison.InvariantCultureIgnoreCase) || ns.Name.Equals("le", StringComparison.InvariantCultureIgnoreCase) || ns.Name.Equals("pb", StringComparison.InvariantCultureIgnoreCase) || ns.Name.Equals("pe", StringComparison.InvariantCultureIgnoreCase)).Count() >= 4)
		{
			return true;
		}
		return false;
	}

	private void read(BinaryReaderEx input)
	{
		string text = input.ReadAscii(4);
		if ("ODOL" != text)
		{
			throw new FormatException("ODOL signature is missing");
		}
		base.Version = input.ReadUInt32();
		if (base.Version > 75)
		{
			throw new FormatException("Unknown ODOL version");
		}
		if (base.Version < 28)
		{
			throw new FormatException("Old ODOL version is currently not supported");
		}
		input.Version = (int)base.Version;
		if (base.Version >= 44)
		{
			input.UseLZOCompression = true;
		}
		if (base.Version >= 64)
		{
			input.UseCompressionFlag = true;
		}
		if (base.Version >= 59)
		{
			appID = input.ReadUInt32();
		}
		if (base.Version >= 74)
		{
			// ODOL v74+ has 2 extra uint32 fields (8 zero bytes) between appID and muzzleFlash.
			// Skip them when all 8 bytes are zero — 8 consecutive nulls cannot be a valid muzzleFlash
			// string (even empty muzzleFlash is 1 null byte, followed by nLods > 0 for any real model).
			// This also covers accessory/proxy models with empty muzzleFlash (previous heuristic missed them
			// because it required the 9th byte to be non-zero).
			long peekPos = input.Position;
			byte[] peek = input.ReadBytes(8);
			input.Position = peekPos;
			bool allZero = true;
			for (int p = 0; p < 8; p++) if (peek[p] != 0) { allZero = false; break; }
			if (allZero)
			{
				input.ReadUInt32();
				input.ReadUInt32();
				Console.Error.WriteLine("  ODOL v74+: skipping 2 extra uint32 fields before muzzleFlash");
			}
		}
		if (base.Version >= 58)
		{
			muzzleFlash = input.ReadAsciiz();
		}
		nLods = input.ReadInt32();
		resolutions = new float[nLods];
		for (int i = 0; i < nLods; i++)
		{
			resolutions[i] = input.ReadSingle();
		}
		modelInfo = new ODOL_ModelInfo(input, nLods);
		bool animReadFailed = false;
		long posAfterAnimsFlag = input.Position;
		if (base.Version >= 30)
		{
			// Arma3 and DayZ ODOL have slight ModelInfo format differences.
			// hasAnims must be 0 or 1. If the byte at current pos is not 0/1,
			// skip forward up to 8 bytes to find it.
			byte hasByte = input.ReadByte();
			int skipCount = 0;
			while (hasByte > 1 && skipCount < 8)
			{
				hasByte = input.ReadByte();
				skipCount++;
			}
			if (skipCount > 0)
			{
				Console.Error.WriteLine($"WARNING: ModelInfo alignment adjusted by {skipCount} byte(s) (Arma3/DayZ format difference)");
				input.IsArma3Format = true;
			}
			hasAnims = hasByte != 0;
			posAfterAnimsFlag = input.Position;
			if (hasAnims)
			{
				try
				{
					animations.read(input);
				}
				catch (UnknownAnimTypeException ex)
				{
					Console.Error.WriteLine("WARNING: " + ex.Message);
					Console.Error.WriteLine("File may be Arma 3 ODOL format. Attempting LOD address recovery...");
					animReadFailed = true;
				}
			}
		}
		lodStartAdresses = new uint[nLods];
		lodEndAdresses = new uint[nLods];
		permanent = new bool[nLods];

		// DayZ-binarized v55 has an extra pivot/sub-skeleton section between the animation
		// data and the LOD address table. Additionally the addresses are stored in DECREASING
		// order (largest LOD last in the file). Use the scan in both this case and the Arma3
		// animation-failure fallback.
		bool needsScan = animReadFailed || (base.Version == 55 && !input.IsArma3Format);
		if (needsScan)
		{
			long fileLen = input.BaseStream.Length;
			// Always scan from right after the hasAnims byte.
			// For DayZ v55 + hasAnims=true, input.Position is past the LOD table after animations.read(),
			// so using it as scanFrom would miss the table entirely.
			long scanFrom = posAfterAnimsFlag;
			if (!TryScanLodAddresses(input, scanFrom, nLods, fileLen,
				out lodStartAdresses, out lodEndAdresses))
			{
				string reason = animReadFailed ? "animation read failure" : "ODOL v55 DayZ extra section";
				throw new Exception($"Could not locate LOD address table ({reason}). File may be unsupported.");
			}
			if (!animReadFailed)
			{
				// DayZ v55: permanent flags sit immediately after the address table.
				for (int l = 0; l < nLods; l++)
					permanent[l] = input.ReadBoolean();
			}
			// animReadFailed case: permanent flags cannot be reliably located; leave as all-false.
		}
		else
		{
			if (base.Version == 55 && input.IsArma3Format)
			{
				// Arma3-binarized v55 has one extra byte between hasAnims and the LOD table.
				input.ReadByte();
			}
			for (int j = 0; j < nLods; j++)
				lodStartAdresses[j] = input.ReadUInt32();
			for (int k = 0; k < nLods; k++)
				lodEndAdresses[k] = input.ReadUInt32();
			for (int l = 0; l < nLods; l++)
				permanent[l] = input.ReadBoolean();
		}
		LoadableLodInfos = new List<LoadableLodInfo>(nLods);
		lods = new LOD[nLods];
		long position = input.Position;
		for (int m = 0; m < nLods; m++)
		{
			if (!permanent[m])
			{
				LoadableLodInfo loadableLodInfo = new LoadableLodInfo();
				loadableLodInfo.ReadObject(input);
				LoadableLodInfos.Add(loadableLodInfo);
				position = input.Position;
			}
			input.Position = lodStartAdresses[m];
			lods[m] = new LOD();
			lods[m].read(input, resolutions[m]);
			input.Position = position;
		}
		if (lodEndAdresses.Length > 0)
			input.Position = lodEndAdresses.Max();
		input.Close();
	}

	/// <summary>
	/// Scans forward in the stream to find nLods start+end address pairs.
	/// Handles two address layouts:
	///   Increasing (Arma3): startAddr[0] &lt; startAddr[1] &lt; ... LODs stored front-to-back.
	///   Decreasing (DayZ):  startAddr[0] &gt; startAddr[1] &gt; ... LODs stored back-to-front;
	///                        endAddr[0] == fileLen, endAddr[i] == startAddr[i-1] for i&gt;0.
	/// </summary>
	private static bool TryScanLodAddresses(BinaryReaderEx input, long scanFrom, int nLods,
		long fileLen, out uint[] startAddrs, out uint[] endAddrs)
	{
		startAddrs = new uint[nLods];
		endAddrs = new uint[nLods];

		input.Position = scanFrom;
		long scanEnd = fileLen - (long)(nLods * 2 * 4 + nLods);

		long minLodStart = scanFrom + (long)(nLods * 2 * 4 + nLods) + 64;
		const uint maxLodGap = 10 * 1024 * 1024;
		const uint minLodSize = 64;

		while (input.Position <= scanEnd)
		{
			long tryPos = input.Position;

			// --- Try increasing layout (Arma3) ---
			bool validIncr = true;
			uint[] tryStartIncr = new uint[nLods];
			for (int i = 0; i < nLods; i++)
			{
				tryStartIncr[i] = input.ReadUInt32();
				if (tryStartIncr[i] < (uint)minLodStart || tryStartIncr[i] >= (uint)fileLen)
					{ validIncr = false; break; }
				if (i > 0 && tryStartIncr[i] <= tryStartIncr[i - 1])
					{ validIncr = false; break; }
				if (i > 0 && tryStartIncr[i] - tryStartIncr[i - 1] > maxLodGap)
					{ validIncr = false; break; }
			}
			if (validIncr)
			{
				uint[] tryEndIncr = new uint[nLods];
				for (int i = 0; i < nLods; i++)
				{
					tryEndIncr[i] = input.ReadUInt32();
					if (tryEndIncr[i] < tryStartIncr[i] + minLodSize || tryEndIncr[i] > (uint)fileLen)
						{ validIncr = false; break; }
				}
				if (validIncr)
				{
					startAddrs = tryStartIncr;
					endAddrs = tryEndIncr;
					Console.Error.WriteLine($"  Recovery: found LOD address table at offset {tryPos}");
					return true;
				}
			}

			// --- Try DayZ binarized layout ---
			// DayZ: endAddr[0]==fileLen. Each endAddr[i] (i>=1) equals some startAddr[j].
			// LODs may be stored out of table-index order, so the chain is not strictly
			// endAddr[i]==startAddr[i-1] — use set membership instead.
			input.Position = tryPos;
			bool validDecr = true;
			uint[] tryStartDecr = new uint[nLods];
			for (int i = 0; i < nLods; i++)
			{
				tryStartDecr[i] = input.ReadUInt32();
				if (tryStartDecr[i] == 0 || tryStartDecr[i] >= (uint)fileLen)
					{ validDecr = false; break; }
			}
			if (validDecr)
			{
				uint[] tryEndDecr = new uint[nLods];
				for (int i = 0; i < nLods; i++)
					tryEndDecr[i] = input.ReadUInt32();
				if (tryEndDecr[0] == (uint)fileLen)
				{
					// Build a set of valid start addresses for O(1) membership check.
					var startSet = new System.Collections.Generic.HashSet<uint>(tryStartDecr);
					bool endOk = true;
					for (int i = 1; i < nLods; i++)
					{
						if (!startSet.Contains(tryEndDecr[i])) { endOk = false; break; }
					}
					if (endOk)
					{
						startAddrs = tryStartDecr;
						endAddrs = tryEndDecr;
						Console.Error.WriteLine($"  Recovery (DayZ): found LOD address table at offset {tryPos}");
						return true;
					}
				}
			}

			input.Position = tryPos + 1;
		}

		return false;
	}

	public string[] getModelCfg()
	{
		throw new NotImplementedException();
	}
}
