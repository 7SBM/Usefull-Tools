using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using BisDll.Model;
using BisDll.Model.MLOD;
using BisDll.Model.ODOL;

namespace P3DDebinarizer
{
    internal class Program
    {
        // ── Tool mode ─────────────────────────────────────────────────────────
        private enum ToolMode { None, P3D, AnmConvert, AnmInspect, AnmExport, PboExtract, RtmInspect }

        // ── Music ────────────────────────────────────────────────────────────
        private static SoundPlayer _player;
        private static Thread _musicThread;

        private static void StartTheme()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream wav = asm.GetManifestResourceStream("P3DDebinarizer.theme_loop.wav");
                if (wav == null) return;
                _player = new SoundPlayer(wav);
                _player.Load();
                _musicThread = new Thread(() =>
                {
                    while (true)
                    {
                        _player.PlaySync();
                        wav.Position = 0;
                        _player.Stream = wav;
                        _player.Load();
                    }
                });
                _musicThread.IsBackground = true;
                _musicThread.Start();
            }
            catch { }
        }

        // ── Scrolling banner ─────────────────────────────────────────────────
        private static readonly string[] BannerLines = new[]
        {
            @"________   ______   _______   __       __        _______    ______   _______       _______             __        __                ",
            @"|        \ /      \ |       \ |  \     /  \      |       \  /      \ |       \     |       \           |  \      |  \               ",
            @" \$$$$$$$$|  $$$$$$\| $$$$$$$\| $$\   /  $$      | $$$$$$$\|  $$$$$$\| $$$$$$$\    | $$$$$$$\  ______  | $$____   \$$ _______       ",
            @"    /  $$ | $$___\$$| $$__/ $$| $$$\ /  $$$      | $$__/ $$ \$$__| $$| $$  | $$    | $$  | $$ /      \ | $$    \ |  \|       \      ",
            @"   /  $$   \$$    \ | $$    $$| $$$$\  $$$$      | $$    $$  |     $$| $$  | $$    | $$  | $$|  $$$$$$\| $$$$$$$\| $$| $$$$$$$\     ",
            @"  /  $$    _\$$$$$$\| $$$$$$$\| $$\$$ $$ $$      | $$$$$$$  __\$$$$$\| $$  | $$    | $$  | $$| $$    $$| $$  | $$| $$| $$  | $$    ",
            @" /  $$    |  \__| $$| $$__/ $$| $$ \$$$| $$      | $$      |  \__| $$| $$__/ $$ __ | $$__/ $$| $$$$$$$$| $$__/ $$| $$| $$  | $$   ",
            @"|  $$      \$$    $$| $$    $$| $$  \$ | $$      | $$       \$$    $$| $$    $$|  \| $$    $$ \$$     \| $$    $$| $$| $$  | $$  ",
            @" \$$        \$$$$$$  \$$$$$$$  \$$      \$$       \$$        \$$$$$$  \$$$$$$$  \$$ \$$$$$$$   \$$$$$$$ \$$$$$$$  \$$ \$$   \$$  ",
        };

        private static CancellationTokenSource _bannerCts;
        private static Thread _bannerThread;
        private static int _bannerTopRow;

        private static void StartScrollBanner()
        {
            if (Console.IsOutputRedirected) return;
            _bannerCts = new CancellationTokenSource();
            _bannerThread = new Thread(() =>
            {
                try
                {
                    int winW = Math.Max(40, Console.WindowWidth - 1);
                    int artW = BannerLines.Max(l => l.Length);
                    int rows = BannerLines.Length;

                    for (int i = 0; i < rows; i++) Console.WriteLine();
                    _bannerTopRow = Console.CursorTop - rows;

                    var sb = new StringBuilder(winW + 2);
                    int offset = winW;

                    while (!_bannerCts.IsCancellationRequested)
                    {
                        for (int r = 0; r < rows; r++)
                        {
                            string line = BannerLines[r].PadRight(artW);
                            sb.Clear();
                            for (int col = 0; col < winW; col++)
                            {
                                int ac = col - offset;
                                sb.Append(ac >= 0 && ac < line.Length ? line[ac] : ' ');
                            }
                            Console.SetCursorPosition(0, _bannerTopRow + r);
                            Console.Write(sb.ToString());
                        }
                        offset -= 2;
                        if (offset < -artW) offset = winW;
                        Thread.Sleep(32);
                    }

                    string blank = new string(' ', winW);
                    for (int r = 0; r < rows; r++)
                    {
                        Console.SetCursorPosition(0, _bannerTopRow + r);
                        Console.Write(blank);
                    }
                    Console.SetCursorPosition(0, _bannerTopRow);
                }
                catch { }
            });
            _bannerThread.IsBackground = true;
            _bannerThread.Start();
        }

        private static void StopScrollBanner()
        {
            _bannerCts?.Cancel();
            _bannerThread?.Join(600);
            _bannerThread = null;
            _bannerCts = null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string ShortPath(string fullPath, int parts = 4)
        {
            char sep = Path.DirectorySeparatorChar;
            string[] segs = fullPath.Split(new[] { sep, Path.AltDirectorySeparatorChar }, StringSplitOptions.None);
            if (segs.Length <= parts) return fullPath;
            return "..." + sep + string.Join(sep.ToString(), segs.Skip(segs.Length - parts));
        }

        private static void SetColor(ConsoleColor c) { try { Console.ForegroundColor = c; } catch { } }
        private static void ResetColor() { SetColor(ConsoleColor.Green); }

        private static char ReadMenuKey(params char[] allowed)
        {
            while (true)
            {
                try
                {
                    ConsoleKeyInfo k = Console.ReadKey(true);
                    char c = char.ToUpper(k.KeyChar);
                    if (Array.IndexOf(allowed, c) >= 0) return c;
                }
                catch { Thread.Sleep(50); }
            }
        }

        private static bool ReadJN()
        {
            char c = ReadMenuKey('J', 'N');
            return c == 'J';
        }

        // ── P3D Conversion ────────────────────────────────────────────────────
        private enum ConvResult { Converted, AlreadyMLOD, SkippedExists, Failed }

        private static ConvResult ConvertFile(string srcPath, string dstPath, bool overwrite,
            out string errorMsg, out string debugLog)
        {
            errorMsg = null;
            debugLog = null;
            try
            {
                byte[] sig = new byte[4];
                using (var fs = File.OpenRead(srcPath))
                {
                    if (fs.Read(sig, 0, 4) < 4) { errorMsg = "Datei zu klein (< 4 Bytes)"; return ConvResult.Failed; }
                }
                string magic = Encoding.ASCII.GetString(sig);

                if (magic == "MLOD") return ConvResult.AlreadyMLOD;
                if (magic != "ODOL") { errorMsg = "Unbekanntes Format (weder ODOL noch MLOD)"; return ConvResult.Failed; }

                string outPath = dstPath ?? Path.Combine(
                    Path.GetDirectoryName(srcPath),
                    Path.GetFileNameWithoutExtension(srcPath) + "_mlod.p3d");

                if (!overwrite && File.Exists(outPath)) return ConvResult.SkippedExists;

                var errBuf = new StringWriter();
                var origErr = Console.Error;
                Console.SetError(errBuf);
                ODOL odol = null;
                MLOD mlod = null;
                try
                {
                    odol = new ODOL(srcPath);
                    mlod = Conversion.ODOL2MLOD(odol);
                }
                finally
                {
                    Console.SetError(origErr);
                    debugLog = errBuf.ToString();
                }

                mlod.writeToFile(outPath, overwrite);
                return ConvResult.Converted;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return ConvResult.Failed;
            }
        }

        private static List<string[]> ConvertFiles(string[] files, string dstFolder, bool overwrite = false)
        {
            int nConverted = 0, nMLOD = 0, nSkipped = 0, nFailed = 0;
            var failures = new List<string[]>();
            var convertedFiles = new List<string[]>();

            Console.WriteLine("Pruefe " + files.Length + " P3D-Datei(en)...\n");

            foreach (string file in files)
            {
                string dst = dstFolder == null ? null : Path.Combine(dstFolder, Path.GetFileName(file));
                string shortSrc = ShortPath(file, 4);
                string err, dbg;

                switch (ConvertFile(file, dst, overwrite, out err, out dbg))
                {
                    case ConvResult.Converted:
                        nConverted++;
                        string mlodPath = dst ?? Path.Combine(
                            Path.GetDirectoryName(file),
                            Path.GetFileNameWithoutExtension(file) + "_mlod.p3d");
                        convertedFiles.Add(new[] { file, mlodPath });
                        SetColor(ConsoleColor.Green);
                        Console.WriteLine(" [OK]  " + shortSrc);
                        break;
                    case ConvResult.AlreadyMLOD:
                        nMLOD++;
                        SetColor(ConsoleColor.DarkGray);
                        Console.WriteLine(" [--]  " + shortSrc + "  (bereits MLOD)");
                        break;
                    case ConvResult.SkippedExists:
                        nSkipped++;
                        SetColor(ConsoleColor.DarkYellow);
                        Console.WriteLine(" [>>]  " + shortSrc + "  (Ausgabe bereits vorhanden)");
                        break;
                    case ConvResult.Failed:
                        nFailed++;
                        failures.Add(new[] { shortSrc, err, dbg });
                        SetColor(ConsoleColor.Red);
                        Console.WriteLine(" [!!]  " + shortSrc + "  ->  " + err);
                        break;
                }
                ResetColor();
            }

            Console.WriteLine();
            SetColor(ConsoleColor.Cyan);
            Console.WriteLine("==========================================");
            Console.WriteLine("  Fertig  |  " + files.Length + " Datei(en) geprueft");
            Console.WriteLine("==========================================");
            SetColor(ConsoleColor.Green);
            Console.WriteLine("  [OK]  Konvertiert:    " + nConverted.ToString().PadLeft(4));
            SetColor(ConsoleColor.DarkGray);
            Console.WriteLine("  [--]  Bereits MLOD:  " + nMLOD.ToString().PadLeft(4) + "   (keine Konvertierung noetig)");
            SetColor(ConsoleColor.DarkYellow);
            Console.WriteLine("  [>>]  Uebersprungen: " + nSkipped.ToString().PadLeft(4) + "   (Ausgabe _mlod.p3d schon vorhanden)");
            if (nFailed > 0) { SetColor(ConsoleColor.Red); Console.WriteLine("  [!!]  Fehler:        " + nFailed.ToString().PadLeft(4) + "   (echte Parse-/Schreibfehler)"); }
            else { SetColor(ConsoleColor.Green); Console.WriteLine("  [!!]  Fehler:           0"); }
            SetColor(ConsoleColor.Cyan);
            Console.WriteLine("==========================================");
            ResetColor();

            if (nSkipped > 0)
            {
                SetColor(ConsoleColor.DarkYellow);
                Console.WriteLine("\n  Tipp: " + nSkipped + " Datei(en) uebersprungen, weil *_mlod.p3d bereits existiert.");
                Console.WriteLine("        Alte _mlod.p3d loeschen oder Ordner neu auswaehlen um nochmal zu konvertieren.");
                ResetColor();
            }

            if (failures.Count > 0)
            {
                SetColor(ConsoleColor.Red);
                Console.WriteLine("\n--- Fehler-Details ---");
                foreach (string[] f in failures)
                {
                    Console.WriteLine("\n  [!!] " + f[0]);
                    Console.WriteLine("       Grund: " + f[1]);
                    if (!string.IsNullOrWhiteSpace(f[2]))
                    {
                        SetColor(ConsoleColor.DarkGray);
                        Console.WriteLine("       Debug-Log:\n" + f[2].TrimEnd());
                        SetColor(ConsoleColor.Red);
                    }
                }
                ResetColor();
            }

            return convertedFiles;
        }

        // ── ANM: ANIMSET5 / ANIMSET6 ──────────────────────────────────────────

        private struct AnimQuat
        {
            public float X, Y, Z, W;
            public AnimQuat(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        }

        private struct AnimBone
        {
            public string Name;
            public AnimQuat Q;
            // Raw rotation vector (ANIMSET6 axis×angle) — NaN if ANIMSET5
            public float RX, RY, RZ;
            public bool HasRotVec;
            public int ParentIdx; // -1 = root / unbekannt
        }

        // Converts rotation vector (axis×angle radians) → unit quaternion
        private static AnimQuat RotVecToQuat(float rx, float ry, float rz)
        {
            double angle = Math.Sqrt(rx * (double)rx + ry * (double)ry + rz * (double)rz);
            if (angle < 1e-8) return new AnimQuat(0, 0, 0, 1);
            double s = Math.Sin(angle * 0.5) / angle;
            double w = Math.Cos(angle * 0.5);
            return new AnimQuat((float)(rx * s), (float)(ry * s), (float)(rz * s), (float)w);
        }

        private static readonly HashSet<string> KnownFullBodyBones =
            new HashSet<string>(StringComparer.Ordinal)
            { "Scene_Root", "Pelvis", "LeftUpLeg", "RightUpLeg", "Hips" };

        // Parst eine IK .txa Datei → bone name → AnimQuat(qx, qy, qz, qw)
        private static bool ParseTxa(string path,
            out Dictionary<string, AnimQuat> rotations,
            out string error)
        {
            rotations = new Dictionary<string, AnimQuat>(StringComparer.Ordinal);
            error = null;
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                var nodeRx  = new Regex(@"\$node\s+""([^""]+)""");
                var qRx     = new Regex(@"#q\s+([-\d.eE+]+)\s+([-\d.eE+]+)\s+([-\d.eE+]+)\s+([-\d.eE+]+)");
                var frameRx = new Regex(@"\$frame\s+\d+\s+\d+\s*\{([^}]*)\}", RegexOptions.Singleline);

                var nodes = nodeRx.Matches(text);
                for (int i = 0; i < nodes.Count; i++)
                {
                    string bone = nodes[i].Groups[1].Value;
                    int ss = nodes[i].Index + nodes[i].Length;
                    int se = i + 1 < nodes.Count ? nodes[i + 1].Index : text.Length;
                    string segment = text.Substring(ss, Math.Max(0, se - ss));

                    var fm = frameRx.Match(segment);
                    if (!fm.Success) continue;
                    var qm = qRx.Match(fm.Groups[1].Value);
                    if (!qm.Success) continue;

                    rotations[bone] = new AnimQuat(
                        float.Parse(qm.Groups[1].Value, CultureInfo.InvariantCulture),
                        float.Parse(qm.Groups[2].Value, CultureInfo.InvariantCulture),
                        float.Parse(qm.Groups[3].Value, CultureInfo.InvariantCulture),
                        float.Parse(qm.Groups[4].Value, CultureInfo.InvariantCulture));
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        // Liest ANIMSET5 oder ANIMSET6 HEAD Block: gibt Liste von AnimBone zurück
        private static bool ReadAnmHead(string path,
            out List<AnimBone> bones,
            out string error,
            out string formatInfo)
        {
            bones = new List<AnimBone>();
            error = null;
            formatInfo = null;
            try
            {
                byte[] d = File.ReadAllBytes(path);
                if (d.Length < 16)
                { error = "Datei zu klein (< 16 Bytes)"; return false; }

                // Hex-Dump Helper
                Func<int, int, string> hexAt = (off, len) =>
                {
                    var sb2 = new StringBuilder();
                    for (int bi = off; bi < Math.Min(off + len, d.Length); bi++)
                        sb2.Append(d[bi].ToString("X2")).Append(' ');
                    string ascii = new string(Enumerable.Range(off, Math.Min(len, d.Length - off))
                        .Select(bi => d[bi] >= 0x20 && d[bi] < 0x7F ? (char)d[bi] : '.')
                        .ToArray());
                    return sb2.ToString().TrimEnd() + "  \"" + ascii + "\"";
                };

                if (Encoding.ASCII.GetString(d, 0, 4) != "FORM")
                {
                    error = "Kein FORM Header\r\n" +
                            "  Offset 0x00: " + hexAt(0, 16) + "\r\n" +
                            "  → Datei ist kein ANIMSET5/6 — möglicherweise RTM oder anderes Format";
                    return false;
                }
                if (d.Length < 48)
                { error = "FORM-Header gefunden aber Datei zu klein"; return false; }

                string formType = Encoding.ASCII.GetString(d, 8, Math.Min(8, d.Length - 8));
                if (formType != "ANIMSET5" && formType != "ANIMSET6")
                {
                    error = "Unbekanntes Format an Position 0x08\r\n" +
                            "  Offset 0x00: " + hexAt(0,  16) + "\r\n" +
                            "  Offset 0x08: " + hexAt(8,   8) + "  ← hier erwartet: 'ANIMSET5' oder 'ANIMSET6'\r\n" +
                            "  Offset 0x10: " + hexAt(16, 16) + "\r\n" +
                            "  Offset 0x20: " + hexAt(32, 16) + "\r\n" +
                            "  → Gefundener Typ: \"" + formType + "\"";
                    return false;
                }
                if (Encoding.ASCII.GetString(d, 0x20, 4) != "HEAD")
                { error = "HEAD Chunk nicht an Position 0x20\r\n" +
                          "  Offset 0x20: " + hexAt(0x20, 8); return false; }

                uint headSize = (uint)((d[0x24] << 24) | (d[0x25] << 16) | (d[0x26] << 8) | d[0x27]);

                if (formType == "ANIMSET5")
                {
                    // ── ANIMSET5: feste 56-byte Records ──────────────────────────
                    // Layout: 32-byte name (null-term) | float qx,qy,qz,qw | 8 byte extra
                    int count5 = (int)(headSize / 56);
                    formatInfo = string.Format("ANIMSET5  |  HEAD={0} bytes  |  {1} Knochen  (Quaternionen)", headSize, count5);
                    for (int i = 0; i < count5; i++)
                    {
                        int off = 0x28 + i * 56;
                        if (off + 56 > d.Length) break;
                        int nl = 0; while (nl < 32 && d[off + nl] != 0) nl++;
                        bones.Add(new AnimBone
                        {
                            Name = Encoding.ASCII.GetString(d, off, nl),
                            Q    = new AnimQuat(
                                       BitConverter.ToSingle(d, off + 32),
                                       BitConverter.ToSingle(d, off + 36),
                                       BitConverter.ToSingle(d, off + 40),
                                       BitConverter.ToSingle(d, off + 44))
                        });
                    }
                }
                else
                {
                    // ── ANIMSET6: variable-length Records ────────────────────────
                    // Layout per Knochen:
                    //   uint32     : bone-ID / Eltern-Index
                    //   float×3    : Rotation-Vektor (Axis × Winkel in Radians)
                    //   float      : immer 1.0 (Scale)
                    //   float      : immer 0.0
                    //   uint16×4   : Metadaten
                    //   uint16 BE  : Namenslänge
                    //   char[N]    : Name (NICHT null-terminated)
                    // = 4+12+4+4+8+2 = 34 bytes Festteil + N bytes Name
                    int headEnd = 0x28 + (int)headSize;
                    int pos6 = 0x28;
                    int boneCount6 = 0;
                    while (pos6 + 34 <= headEnd && pos6 + 34 <= d.Length)
                    {
                        int nameLen = d[pos6 + 33]; // 1 Byte; d[+32] ist ein Typ-Flag (0x00=IK, 0x01=Multiframe)
                        if (nameLen <= 0 || pos6 + 34 + nameLen > d.Length) break;

                        int parentIdx6 = (int)BitConverter.ToUInt32(d, pos6 + 0);
                        float rx = BitConverter.ToSingle(d, pos6 + 4);
                        float ry = BitConverter.ToSingle(d, pos6 + 8);
                        float rz = BitConverter.ToSingle(d, pos6 + 12);
                        string bname = Encoding.ASCII.GetString(d, pos6 + 34, nameLen);

                        bones.Add(new AnimBone
                        {
                            Name       = bname,
                            RX         = rx, RY = ry, RZ = rz,
                            HasRotVec  = true,
                            Q          = RotVecToQuat(rx, ry, rz),
                            ParentIdx  = parentIdx6
                        });

                        pos6 += 34 + nameLen;
                        boneCount6++;
                    }
                    formatInfo = string.Format("ANIMSET6  |  HEAD={0} bytes  |  {1} Knochen  (RotVec→Quat konvertiert)", headSize, boneCount6);
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; formatInfo = null; return false; }
        }

        // Patcht HEAD-Quaternionen eines Template ANM mit TXA-Werten, schreibt outputPath
        private static bool PatchAnm(string templatePath, string outputPath,
            Dictionary<string, AnimQuat> rotations,
            out string error, out int patched, out int skipped, out List<string> notInTxa)
        {
            patched = 0; skipped = 0; notInTxa = new List<string>(); error = null;
            try
            {
                byte[] d = File.ReadAllBytes(templatePath);
                if (d.Length < 48 || Encoding.ASCII.GetString(d, 0, 4) != "FORM")
                { error = "Kein FORM Header — keine gültige ANIMSET5 Datei"; return false; }
                if (Encoding.ASCII.GetString(d, 8, 8) != "ANIMSET5")
                { error = "Kein ANIMSET5 Chunk gefunden"; return false; }
                if (Encoding.ASCII.GetString(d, 0x20, 4) != "HEAD")
                { error = "HEAD Chunk nicht an Position 0x20"; return false; }

                uint headSize = (uint)((d[0x24] << 24) | (d[0x25] << 16) | (d[0x26] << 8) | d[0x27]);
                int count = (int)(headSize / 56);

                for (int i = 0; i < count; i++)
                {
                    int off = 0x28 + i * 56;
                    if (off + 56 > d.Length) break;
                    int nl = 0; while (nl < 32 && d[off + nl] != 0) nl++;
                    string bone = Encoding.ASCII.GetString(d, off, nl);

                    AnimQuat q;
                    if (rotations.TryGetValue(bone, out q))
                    {
                        Array.Copy(BitConverter.GetBytes(q.X), 0, d, off + 32, 4);
                        Array.Copy(BitConverter.GetBytes(q.Y), 0, d, off + 36, 4);
                        Array.Copy(BitConverter.GetBytes(q.Z), 0, d, off + 40, 4);
                        Array.Copy(BitConverter.GetBytes(q.W), 0, d, off + 44, 4);
                        patched++;
                    }
                    else { notInTxa.Add(bone); skipped++; }
                }

                File.WriteAllBytes(outputPath, d);
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        // ── model.cfg Generator ───────────────────────────────────────────────

        private static string BuildModelCfg(string[] srcOdolPaths)
        {
            var skelSb  = new StringBuilder();
            var modelSb = new StringBuilder();
            var skelDone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in srcOdolPaths)
            {
                ODOL odol = null;
                try
                {
                    // Suppress BisDll stderr during this parse
                    var sink = new StringWriter();
                    var origErr = Console.Error;
                    Console.SetError(sink);
                    try   { odol = new ODOL(path); }
                    finally { Console.SetError(origErr); }
                }
                catch { continue; }

                string modelName = Path.GetFileNameWithoutExtension(path);
                Skeleton skel    = odol.Skeleton;
                string skelName  = (skel != null && !string.IsNullOrEmpty(skel.Name))
                                   ? skel.Name : "";

                // cfgSkeletons block — only once per unique skeleton name
                if (!string.IsNullOrEmpty(skelName) && skelDone.Add(skelName))
                {
                    skelSb.AppendLine("    class " + skelName);
                    skelSb.AppendLine("    {");
                    skelSb.AppendLine("        skeletonInherit = \"\";");
                    skelSb.AppendLine("        isDiscrete = " + (skel.isDiscrete ? "1" : "0") + ";");
                    skelSb.AppendLine("        SkeletonBones[] =");
                    skelSb.AppendLine("        {");
                    if (skel.bones != null)
                    {
                        for (int i = 0; i + 1 < skel.bones.Length; i += 2)
                            skelSb.AppendLine("            \"" + skel.bones[i] + "\", \"" + skel.bones[i + 1] + "\",");
                    }
                    skelSb.AppendLine("        };");
                    skelSb.AppendLine("    };");
                }

                // sections[] — IsSectional NamedSelections from all LODs (deduplicated)
                var sections = new List<string>();
                try
                {
                    foreach (var lodBase in odol.LODs)
                    {
                        var lod = lodBase as BisDll.Model.ODOL.LOD;
                        if (lod == null || lod.NamedSelections == null) continue;
                        foreach (var ns in lod.NamedSelections)
                        {
                            if (ns != null && ns.IsSectional &&
                                !string.IsNullOrEmpty(ns.Name) &&
                                !sections.Contains(ns.Name, StringComparer.OrdinalIgnoreCase))
                                sections.Add(ns.Name);
                        }
                    }
                }
                catch { }

                // CfgModels class block
                modelSb.AppendLine("    class " + modelName + " : Default");
                modelSb.AppendLine("    {");
                if (!string.IsNullOrEmpty(skelName))
                    modelSb.AppendLine("        skeletonName = \"" + skelName + "\";");
                modelSb.AppendLine("        sections[] =");
                modelSb.AppendLine("        {");
                foreach (string s in sections)
                    modelSb.AppendLine("            \"" + s + "\",");
                modelSb.AppendLine("        };");
                modelSb.AppendLine("    };");
            }

            var out_ = new StringBuilder();
            out_.AppendLine("// Generated by 7SBM P3D.DeBin v1.8");
            out_.AppendLine();
            out_.AppendLine("class cfgSkeletons");
            out_.AppendLine("{");
            out_.Append(skelSb);
            out_.AppendLine("};");
            out_.AppendLine();
            out_.AppendLine("class CfgModels");
            out_.AppendLine("{");
            out_.AppendLine("    class Default");
            out_.AppendLine("    {");
            out_.AppendLine("        sections[] = {};");
            out_.AppendLine("        sectionsInherit = \"\";");
            out_.AppendLine("        skeletonName = \"\";");
            out_.AppendLine("    };");
            out_.Append(modelSb);
            out_.AppendLine("};");
            return out_.ToString();
        }

        // ── PBO Unpacker ──────────────────────────────────────────────────────

        private const uint PBO_MAGIC_VERS = 0x56657273; // "Vers" LE = properties entry
        private const uint PBO_PACK_CPRS  = 0x43707273; // "Cprs" = LZH compressed

        private static byte[] PboDecompress(byte[] src, uint originalSize)
        {
            const int N = 4096;
            const int F = 18;
            byte[] ring = new byte[N + F - 1];
            int rp = N - F; // 0xFEE

            byte[] dst = new byte[originalSize];
            int si = 0, di = 0, dstLen = (int)originalSize;

            while (di < dstLen && si < src.Length)
            {
                byte flag = src[si++];
                for (int bit = 0; bit < 8 && di < dstLen && si < src.Length; bit++)
                {
                    if ((flag & (1 << bit)) != 0)
                    {
                        byte c = src[si++];
                        dst[di++] = c;
                        ring[rp] = c;
                        rp = (rp + 1) & (N - 1);
                    }
                    else
                    {
                        if (si + 1 >= src.Length) break;
                        int b0 = src[si++];
                        int b1 = src[si++];
                        int off = b0 | ((b1 & 0xF0) << 4);
                        int len = (b1 & 0x0F) + 3;
                        for (int j = 0; j < len && di < dstLen; j++)
                        {
                            byte c = ring[(off + j) & (N - 1)];
                            dst[di++] = c;
                            ring[rp] = c;
                            rp = (rp + 1) & (N - 1);
                        }
                    }
                }
            }
            return dst;
        }

        private struct PboEntry
        {
            public string Name;
            public uint   PackMethod;
            public uint   OriginalSize;
            public uint   DataSize;
        }

        private static string PboReadString(BinaryReader r)
        {
            var sb = new StringBuilder(64);
            byte b;
            while ((b = r.ReadByte()) != 0) sb.Append((char)b);
            return sb.ToString();
        }

        private static List<PboEntry> PboReadHeader(BinaryReader r,
            out Dictionary<string, string> props)
        {
            props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<PboEntry>();
            bool first = true;

            while (true)
            {
                string name      = PboReadString(r);
                uint packMethod  = r.ReadUInt32();
                uint origSize    = r.ReadUInt32();
                /* reserved */     r.ReadUInt32();
                /* timestamp */    r.ReadUInt32();
                uint dataSize    = r.ReadUInt32();

                // Properties entry (always first)
                if (first && packMethod == PBO_MAGIC_VERS && name.Length == 0)
                {
                    while (true)
                    {
                        string key = PboReadString(r);
                        if (key.Length == 0) break;
                        string val = PboReadString(r);
                        props[key] = val;
                    }
                    first = false;
                    continue;
                }
                first = false;

                // Terminator entry
                if (name.Length == 0 && packMethod == 0 && dataSize == 0)
                    break;

                entries.Add(new PboEntry
                {
                    Name       = name,
                    PackMethod = packMethod,
                    OriginalSize = origSize,
                    DataSize   = dataSize
                });
            }
            return entries;
        }

        /// <summary>
        /// Entpackt eine PBO in outputDir.
        /// Gibt Anzahl extrahierter Dateien zurück (-1 bei Fehler).
        /// </summary>
        private static readonly char[] InvalidPathChars = Path.GetInvalidFileNameChars()
            .Where(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar)
            .ToArray();

        private static string SanitizePboEntryName(string name)
        {
            string rel = name.Replace('/', Path.DirectorySeparatorChar)
                             .Replace('\\', Path.DirectorySeparatorChar);
            rel = rel.TrimStart(Path.DirectorySeparatorChar);
            if (rel.Length == 0) return null;

            string[] parts = rel.Split(Path.DirectorySeparatorChar);
            for (int i = 0; i < parts.Length; i++)
            {
                foreach (char c in InvalidPathChars)
                    parts[i] = parts[i].Replace(c, '_');
                if (parts[i].Length == 0) parts[i] = "_";
            }
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts);
        }

        private static int PboExtractAll(string pboPath, string outputDir,
            out Dictionary<string, string> props, out string error)
        {
            props = new Dictionary<string, string>();
            error = null;
            try
            {
                using (var fs = File.OpenRead(pboPath))
                using (var r  = new BinaryReader(fs, Encoding.ASCII))
                {
                    long fileLen = fs.Length;
                    List<PboEntry> entries = PboReadHeader(r, out props);
                    Directory.CreateDirectory(outputDir);

                    string outRoot = Path.GetFullPath(outputDir) + Path.DirectorySeparatorChar;
                    int extracted = 0;

                    foreach (PboEntry e in entries)
                    {
                        if (e.DataSize == 0) continue;

                        long remaining = fileLen - fs.Position;
                        if (e.DataSize > remaining)
                        {
                            fs.Position = fileLen;
                            break;
                        }

                        string rel = SanitizePboEntryName(e.Name);
                        if (rel == null)
                        {
                            fs.Position += e.DataSize;
                            continue;
                        }

                        string full;
                        try
                        {
                            full = Path.GetFullPath(Path.Combine(outputDir, rel));
                        }
                        catch
                        {
                            fs.Position += e.DataSize;
                            continue;
                        }

                        if (!full.StartsWith(outRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            fs.Position += e.DataSize;
                            continue;
                        }

                        try
                        {
                            string dir = Path.GetDirectoryName(full);
                            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                            byte[] data = r.ReadBytes((int)e.DataSize);
                            if (e.PackMethod == PBO_PACK_CPRS && e.OriginalSize > 0)
                                data = PboDecompress(data, e.OriginalSize);
                            File.WriteAllBytes(full, data);
                            extracted++;
                        }
                        catch
                        {
                            fs.Position += e.DataSize;
                        }
                    }
                    return extracted;
                }
            }
            catch (Exception ex) { error = ex.Message; return -1; }
        }

        // ── raP (config.bin) → config.cpp ────────────────────────────────────

        private static string RapReadStr(BinaryReader r)
        {
            var sb = new StringBuilder(64);
            byte b;
            while ((b = r.ReadByte()) != 0) sb.Append((char)b);
            return sb.ToString();
        }

        private static string RapEscape(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // raP class/array counts are variable-length encoded (CompactInt):
        // bit7=0 → value in bits 0-6, done (1 byte); bit7=1 → more bytes follow.
        private static uint RapReadCompact(BinaryReader r)
        {
            uint val = 0; int shift = 0; byte b;
            do { b = r.ReadByte(); val |= (uint)(b & 0x7F) << shift; shift += 7; }
            while ((b & 0x80) != 0 && shift < 35);
            return val;
        }

        private static void RapWriteEntry(BinaryReader r, StringBuilder sb, int indent)
        {
            string pad = new string('\t', indent);
            byte type;
            try { type = r.ReadByte(); } catch { return; }

            switch (type)
            {
                case 0: // class { body stored at absolute offset }
                {
                    string name = RapReadStr(r);
                    uint bodyOff = r.ReadUInt32();
                    long ret = r.BaseStream.Position;
                    r.BaseStream.Position = bodyOff;
                    string inh = RapReadStr(r);
                    uint cnt = RapReadCompact(r);
                    sb.Append(pad + "class " + name);
                    if (inh.Length > 0) sb.Append(" : " + inh);
                    sb.AppendLine();
                    sb.AppendLine(pad + "{");
                    for (uint i = 0; i < cnt; i++) RapWriteEntry(r, sb, indent + 1);
                    sb.AppendLine(pad + "};");
                    r.BaseStream.Position = ret;
                    break;
                }
                case 1: // scalar value
                {
                    byte sub = r.ReadByte();
                    string name = RapReadStr(r);
                    switch (sub)
                    {
                        case 0:
                            sb.AppendLine(pad + name + " = \"" + RapEscape(RapReadStr(r)) + "\";");
                            break;
                        case 1:
                            sb.AppendLine(pad + name + " = " +
                                r.ReadSingle().ToString("G7", CultureInfo.InvariantCulture) + ";");
                            break;
                        case 2:
                        case 3:
                            sb.AppendLine(pad + name + " = " + r.ReadInt32() + ";");
                            break;
                        default:
                            sb.AppendLine(pad + "// [?] unbekannter Subtyp " + sub + " für " + name);
                            break;
                    }
                    break;
                }
                case 2: // array
                {
                    string name = RapReadStr(r);
                    uint cnt = RapReadCompact(r);
                    sb.AppendLine(pad + name + "[] =");
                    sb.AppendLine(pad + "{");
                    string ep = new string('\t', indent + 1);
                    for (uint i = 0; i < cnt; i++)
                    {
                        byte et = r.ReadByte();
                        switch (et)
                        {
                            case 0:
                                sb.AppendLine(ep + "\"" + RapEscape(RapReadStr(r)) + "\",");
                                break;
                            case 1:
                                sb.AppendLine(ep + r.ReadSingle().ToString("G7",
                                    CultureInfo.InvariantCulture) + ",");
                                break;
                            case 2:
                                sb.AppendLine(ep + r.ReadInt32() + ",");
                                break;
                            case 3: // nested array (rare)
                            {
                                uint nc = RapReadCompact(r);
                                string np = new string('\t', indent + 2);
                                sb.AppendLine(ep + "{");
                                for (uint j = 0; j < nc; j++)
                                {
                                    byte nt = r.ReadByte();
                                    switch (nt)
                                    {
                                        case 0: sb.AppendLine(np + "\"" + RapEscape(RapReadStr(r)) + "\","); break;
                                        case 1: sb.AppendLine(np + r.ReadSingle().ToString("G7",
                                            CultureInfo.InvariantCulture) + ","); break;
                                        case 2: sb.AppendLine(np + r.ReadInt32() + ","); break;
                                    }
                                }
                                sb.AppendLine(ep + "},");
                                break;
                            }
                            default: break;
                        }
                    }
                    sb.AppendLine(pad + "};");
                    break;
                }
                case 3: // extern class forward declaration
                    sb.AppendLine(pad + "class " + RapReadStr(r) + ";");
                    break;
                case 4: // delete class
                    sb.AppendLine(pad + "delete " + RapReadStr(r) + ";");
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Konvertiert config.bin (raP-Format) → lesbaren config.cpp-Text.
        /// Vollständig eigene Implementierung — keine externen Tools nötig.
        /// </summary>
        private static bool TryRapToCpp(string binPath, out string cppText, out string error)
        {
            cppText = null;
            error   = null;
            try
            {
                byte[] data = File.ReadAllBytes(binPath);
                if (data.Length < 16)
                { error = "Datei zu klein für raP-Header"; return false; }
                if (data[0] != 0 || data[1] != 'r' || data[2] != 'a' || data[3] != 'P')
                { error = "Kein raP-Header — keine binarisierte Config"; return false; }

                var sb = new StringBuilder();
                sb.AppendLine("// 7SBM P3D.DeBin v1.9  |  by 7SpeedBlendMaster");
                sb.AppendLine("// raP → cpp  |  Quelle: " + Path.GetFileName(binPath));
                sb.AppendLine();

                using (var ms = new MemoryStream(data))
                using (var r  = new BinaryReader(ms, Encoding.ASCII))
                {
                    r.BaseStream.Position = 16; // 16-Byte Header: \0raP + 3×uint32
                    RapReadStr(r);              // root inherits — immer leer
                    uint rootCount = RapReadCompact(r);
                    for (uint i = 0; i < rootCount; i++)
                        RapWriteEntry(r, sb, 0);
                }

                cppText = sb.ToString();
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        // ── Main ──────────────────────────────────────────────────────────────
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Console.BufferWidth = 200;
                Console.WindowWidth = Math.Min(200, Console.LargestWindowWidth);
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Clear();
            }
            catch { }

            StartTheme();
            StartScrollBanner();

            string titleBox =
"  +-+-+-+-+ +-+-+-+ +-+-+-+-+-+\r\n" +
"  |7|S|B|M| |P|3|D| |D|e|B|i|n|\r\n" +
"  +-+-+-+-+ +-+-+-+ +-+-+-+-+-+\r\n" +
"  DayZ + Arma3  |  v1.9  |  by 7SpeedBlendMaster\r\n" +
"==========================================";

            string respektText =
"\r\n" +
"## Respektiere die Arbeit anderer Entwickler\r\n" +
"\r\n" +
"Dieses Tool wurde entwickelt, um das Lernen, Verstehen und Analysieren von\r\n" +
"P3D-Dateien zu erleichtern. Nutze dieses Wissen, um eigene Fähigkeiten zu\r\n" +
"erweitern, neue Techniken zu erlernen und eigene hochwertige Assets, Modelle\r\n" +
"und Projekte zu erschaffen.\r\n" +
"\r\n" +
"Hinter jedem Modell, jeder Textur und jeder Modifikation steckt oft eine\r\n" +
"erhebliche Menge an Zeit, Erfahrung und Kreativität. Bitte respektiere die\r\n" +
"Arbeit der ursprünglichen Autoren und verwende dieses Tool nicht, um fremde\r\n" +
"Inhalte unerlaubt zu kopieren, weiterzuverbreiten oder als eigene Arbeit\r\n" +
"auszugeben.\r\n" +
"\r\n" +
"Das Tool ist für Ausbildungs-, Forschungs-, Analyse- und\r\n" +
"Kompatibilitätszwecke gedacht. Stelle sicher, dass deine Nutzung mit den\r\n" +
"jeweiligen Lizenzbedingungen, Urheberrechten und den geltenden Gesetzen\r\n" +
"vereinbar ist.\r\n" +
"\r\n" +
"Der Autor unterstützt weder Softwarepiraterie noch die unerlaubte\r\n" +
"Veröffentlichung oder kommerzielle Verwertung fremder Inhalte. Die\r\n" +
"Verantwortung für die Nutzung dieses Tools liegt ausschließlich beim Anwender.\r\n" +
"\r\n" +
"Nutze das gewonnene Wissen, um die Community durch eigene Ideen, eigene\r\n" +
"Projekte und eigene Kreativität zu bereichern.\r\n" +
"\r\n" +
"==========================================================================";

            string usageP3D =
"P3D Usage:\r\n" +
"  Debinarizer.exe                            - Modus-Dialog (interaktiv)\r\n" +
"  Debinarizer.exe path/model.p3d             - einzelne Datei konvertieren\r\n" +
"  Debinarizer.exe inputFolder [outputFolder] - alle p3d rekursiv konvertieren\r\n";

            bool firstRun = true;
            while (true)
            {
            var toolMode = ToolMode.None;
            string[] filesToConvert = null;
            string dstFolder = null;
            bool hasWork = false;

            string txaFile         = null;
            string[] txaFiles      = null;   // [A] Ordner-Modus
            string templateAnmFile = null;
            string outputAnmFile   = null;
            string inspectAnmFile  = null;
            string[] inspectAnmFiles = null;
            string exportAnmFile   = null;
            string exportTxaFile   = null;
            string[] exportAnmFiles = null;  // [E] Ordner-Modus

            string pboFile      = null;
            string pboOutputDir = null;
            string[] pboFiles   = null;      // [B] Ordner-Modus
            string[] inspectRtmFiles = null; // [R] Ordner-Modus

            try
            {
                switch (args.Length)
                {
                    case 0:
                    {
                        // ── Hauptmenü ─────────────────────────────────────────
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("  Was möchtest du tun?\r\n");
                        Console.WriteLine("    [P]  P3D  debinarisieren     (ODOL → MLOD)");
                        Console.WriteLine("    [A]  ANM  konvertieren       (TXA + Template → ANM)");
                        Console.WriteLine("    [E]  ANM  exportieren        (ANM → TXA)");
                        Console.WriteLine("    [I]  ANM  inspizieren        (Bone-Namen + Quaternionen anzeigen)");
                        Console.WriteLine("    [R]  RTM  inspizieren        (Arma3 BMTR — Bone-Namen + Quaternionen)");
                        Console.WriteLine("    [B]  PBO  entpacken          (PBO → Dateien)");
                        Console.WriteLine();
                        Console.Write("  →  ");
                        ResetColor();

                        char choice = ReadMenuKey('P', 'A', 'E', 'I', 'R', 'B');

                        if (choice == 'P')
                        {
                            toolMode = ToolMode.P3D;
                            Console.WriteLine("P3D debinarisieren\r\n");

                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("  Was möchtest du konvertieren?\r\n");
                            Console.WriteLine("    [O]  Ordner auswählen  (alle P3D rekursiv)");
                            Console.WriteLine("    [D]  Einzelne Datei(en) auswählen");
                            Console.WriteLine();
                            Console.Write("  →  ");
                            ResetColor();

                            char sub = ReadMenuKey('O', 'D');
                            if (sub == 'O')
                            {
                                Console.WriteLine("Ordner\r\n");
                                var dlg = new FolderBrowserDialog();
                                dlg.Description = "Ordner mit P3D-Dateien auswählen (inklusive Unterordner)";
                                if (dlg.ShowDialog() == DialogResult.OK)
                                {
                                    filesToConvert = Directory.GetFiles(dlg.SelectedPath, "*.p3d", SearchOption.AllDirectories);
                                    hasWork = true;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Einzelne Datei(en)\r\n");
                                var dlg = new OpenFileDialog();
                                dlg.Title = "P3D-Datei(en) auswählen";
                                dlg.Filter = "P3D-Dateien (*.p3d)|*.p3d";
                                dlg.Multiselect = true;
                                if (dlg.ShowDialog() == DialogResult.OK)
                                {
                                    filesToConvert = dlg.FileNames;
                                    hasWork = true;
                                }
                            }
                        }
                        else if (choice == 'A')
                        {
                            toolMode = ToolMode.AnmConvert;
                            Console.WriteLine("ANM konvertieren\r\n");

                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("    [O]  Ordner auswählen   (alle TXA → ANM, ein Template für alle)");
                            Console.WriteLine("    [D]  Datei auswählen    (einzelne TXA → ANM)");
                            Console.WriteLine();
                            Console.Write("  →  ");
                            ResetColor();
                            char subA = ReadMenuKey('O', 'D');
                            if (subA == 'O')
                            {
                                Console.WriteLine("Ordner\r\n");
                                var folderDlg = new FolderBrowserDialog();
                                folderDlg.Description = "Ordner mit TXA-Dateien auswählen";
                                if (folderDlg.ShowDialog() == DialogResult.OK)
                                {
                                    txaFiles = Directory.GetFiles(folderDlg.SelectedPath, "*.txa",
                                        SearchOption.TopDirectoryOnly);
                                    Array.Sort(txaFiles);
                                    if (txaFiles.Length > 0)
                                    {
                                        SetColor(ConsoleColor.DarkGray);
                                        Console.WriteLine("  Template ANM auswählen  (gilt für alle TXA)\r\n");
                                        ResetColor();
                                        var anmDlg = new OpenFileDialog();
                                        anmDlg.Title = "Template ANM auswählen";
                                        anmDlg.Filter = "ANM-Dateien (*.anm)|*.anm|Alle Dateien (*.*)|*.*";
                                        anmDlg.InitialDirectory = folderDlg.SelectedPath;
                                        if (anmDlg.ShowDialog() == DialogResult.OK)
                                        {
                                            templateAnmFile = anmDlg.FileName;
                                            hasWork = true;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("Datei\r\n");
                                SetColor(ConsoleColor.DarkGray);
                                Console.WriteLine("  Schritt 1/3 — TXA-Datei auswählen  (Blender IK-Export)\r\n");
                                ResetColor();

                                var txaDlg = new OpenFileDialog();
                                txaDlg.Title = "TXA-Datei auswählen (Blender IK-Export)";
                                txaDlg.Filter = "TXA-Dateien (*.txa)|*.txa|Alle Dateien (*.*)|*.*";
                                if (txaDlg.ShowDialog() == DialogResult.OK)
                                {
                                    txaFile = txaDlg.FileName;

                                    SetColor(ConsoleColor.DarkGray);
                                    Console.WriteLine("  Schritt 2/3 — Template ANM auswählen  (z.B. ump.anm, cz61.anm)\r\n");
                                    ResetColor();

                                    var anmDlg = new OpenFileDialog();
                                    anmDlg.Title = "Template ANM auswählen";
                                    anmDlg.Filter = "ANM-Dateien (*.anm)|*.anm|Alle Dateien (*.*)|*.*";
                                    anmDlg.InitialDirectory = Path.GetDirectoryName(txaFile);
                                    if (anmDlg.ShowDialog() == DialogResult.OK)
                                    {
                                        templateAnmFile = anmDlg.FileName;

                                        SetColor(ConsoleColor.DarkGray);
                                        Console.WriteLine("  Schritt 3/3 — Output ANM speichern als...\r\n");
                                        ResetColor();

                                        var saveDlg = new SaveFileDialog();
                                        saveDlg.Title = "Output ANM speichern";
                                        saveDlg.Filter = "ANM-Dateien (*.anm)|*.anm";
                                        saveDlg.FileName = Path.GetFileNameWithoutExtension(txaFile) + ".anm";
                                        saveDlg.InitialDirectory = Path.GetDirectoryName(txaFile);
                                        if (saveDlg.ShowDialog() == DialogResult.OK)
                                        {
                                            outputAnmFile = saveDlg.FileName;
                                            hasWork = true;
                                        }
                                    }
                                }
                            }
                        }
                        else if (choice == 'I')
                        {
                            toolMode = ToolMode.AnmInspect;
                            Console.WriteLine("ANM inspizieren\r\n");

                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("    [O]  Ordner auswählen   (alle ANM → kombinierte Ausgabe)");
                            Console.WriteLine("    [D]  Datei(en) auswählen");
                            Console.WriteLine();
                            Console.Write("  →  ");
                            ResetColor();

                            char subI = ReadMenuKey('O', 'D');
                            if (subI == 'O')
                            {
                                Console.WriteLine("Ordner\r\n");
                                var folderDlg = new FolderBrowserDialog();
                                folderDlg.Description = "Ordner mit ANM-Dateien auswählen";
                                if (folderDlg.ShowDialog() == DialogResult.OK)
                                {
                                    inspectAnmFiles = Directory.GetFiles(folderDlg.SelectedPath, "*.anm",
                                        SearchOption.TopDirectoryOnly);
                                    Array.Sort(inspectAnmFiles);
                                    if (inspectAnmFiles.Length > 0) hasWork = true;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Datei(en)\r\n");
                                var dlg = new OpenFileDialog();
                                dlg.Title = "ANM-Datei(en) auswählen";
                                dlg.Filter = "ANM-Dateien (*.anm)|*.anm|Alle Dateien (*.*)|*.*";
                                dlg.Multiselect = true;
                                if (dlg.ShowDialog() == DialogResult.OK)
                                {
                                    inspectAnmFiles = dlg.FileNames;
                                    hasWork = true;
                                }
                            }
                        }
                        else if (choice == 'E')
                        {
                            toolMode = ToolMode.AnmExport;
                            Console.WriteLine("ANM exportieren  (ANM → TXA)\r\n");

                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("    [O]  Ordner auswählen   (alle ANM → TXA, gleicher Ordner)");
                            Console.WriteLine("    [D]  Datei auswählen    (einzelne ANM → TXA)");
                            Console.WriteLine();
                            Console.Write("  →  ");
                            ResetColor();
                            char subE = ReadMenuKey('O', 'D');
                            if (subE == 'O')
                            {
                                Console.WriteLine("Ordner\r\n");
                                var folderDlg = new FolderBrowserDialog();
                                folderDlg.Description = "Ordner mit ANM-Dateien auswählen";
                                if (folderDlg.ShowDialog() == DialogResult.OK)
                                {
                                    exportAnmFiles = Directory.GetFiles(folderDlg.SelectedPath, "*.anm",
                                        SearchOption.TopDirectoryOnly);
                                    Array.Sort(exportAnmFiles);
                                    if (exportAnmFiles.Length > 0) hasWork = true;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Datei\r\n");
                                SetColor(ConsoleColor.DarkGray);
                                Console.WriteLine("  Schritt 1/2 — ANM-Datei auswählen\r\n");
                                ResetColor();

                                var exportDlg = new OpenFileDialog();
                                exportDlg.Title = "ANM-Datei auswählen";
                                exportDlg.Filter = "ANM-Dateien (*.anm)|*.anm|Alle Dateien (*.*)|*.*";
                                if (exportDlg.ShowDialog() == DialogResult.OK)
                                {
                                    exportAnmFile = exportDlg.FileName;

                                    SetColor(ConsoleColor.DarkGray);
                                    Console.WriteLine("  Schritt 2/2 — TXA-Datei speichern als...\r\n");
                                    ResetColor();

                                    var saveDlg = new SaveFileDialog();
                                    saveDlg.Title = "TXA-Datei speichern";
                                    saveDlg.Filter = "TXA-Dateien (*.txa)|*.txa|Alle Dateien (*.*)|*.*";
                                    saveDlg.FileName = Path.GetFileNameWithoutExtension(exportAnmFile) + ".txa";
                                    saveDlg.InitialDirectory = Path.GetDirectoryName(exportAnmFile);
                                    if (saveDlg.ShowDialog() == DialogResult.OK)
                                    {
                                        exportTxaFile = saveDlg.FileName;
                                        hasWork = true;
                                    }
                                }
                            }
                        }
                        else if (choice == 'R')
                        {
                            toolMode = ToolMode.RtmInspect;
                            Console.WriteLine("RTM inspizieren  (Arma3 BMTR)\r\n");

                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("    [O]  Ordner auswählen   (alle RTM → TXA, gleicher Ordner)");
                            Console.WriteLine("    [D]  Datei auswählen    (einzelne RTM — Inspect + Optionen)");
                            Console.WriteLine();
                            Console.Write("  →  ");
                            ResetColor();
                            char subR = ReadMenuKey('O', 'D');
                            if (subR == 'O')
                            {
                                Console.WriteLine("Ordner\r\n");
                                var folderDlg = new FolderBrowserDialog();
                                folderDlg.Description = "Ordner mit RTM-Dateien auswählen";
                                if (folderDlg.ShowDialog() == DialogResult.OK)
                                {
                                    inspectRtmFiles = Directory.GetFiles(folderDlg.SelectedPath, "*.rtm",
                                        SearchOption.TopDirectoryOnly);
                                    Array.Sort(inspectRtmFiles);
                                    if (inspectRtmFiles.Length > 0) hasWork = true;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Datei\r\n");
                                var rtmDlg = new OpenFileDialog();
                                rtmDlg.Title = "RTM-Datei auswählen (Arma3 BMTR)";
                                rtmDlg.Filter = "RTM-Dateien (*.rtm)|*.rtm|Alle Dateien (*.*)|*.*";
                                if (rtmDlg.ShowDialog() == DialogResult.OK)
                                {
                                    inspectAnmFile = rtmDlg.FileName;
                                    hasWork = true;
                                }
                            }
                        }
                        else // 'B'
                        {
                            toolMode = ToolMode.PboExtract;
                            Console.WriteLine("PBO entpacken\r\n");

                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("    [O]  Ordner auswählen   (alle PBO → je ein Unterordner)");
                            Console.WriteLine("    [D]  Datei auswählen    (einzelne PBO)");
                            Console.WriteLine();
                            Console.Write("  →  ");
                            ResetColor();
                            char subB = ReadMenuKey('O', 'D');
                            if (subB == 'O')
                            {
                                Console.WriteLine("Ordner\r\n");
                                var folderDlg = new FolderBrowserDialog();
                                folderDlg.Description = "Ordner mit PBO-Dateien auswählen";
                                if (folderDlg.ShowDialog() == DialogResult.OK)
                                {
                                    pboFiles = Directory.GetFiles(folderDlg.SelectedPath, "*.pbo",
                                        SearchOption.TopDirectoryOnly);
                                    Array.Sort(pboFiles);
                                    // pboOutputDir = parent folder (each PBO → subfolder inside it)
                                    pboOutputDir = folderDlg.SelectedPath;
                                    if (pboFiles.Length > 0) hasWork = true;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Datei\r\n");
                                SetColor(ConsoleColor.DarkGray);
                                Console.WriteLine("  Schritt 1/2 — PBO-Datei auswählen\r\n");
                                ResetColor();

                                var pboDlg = new OpenFileDialog();
                                pboDlg.Title = "PBO-Datei auswählen";
                                pboDlg.Filter = "PBO-Dateien (*.pbo)|*.pbo|Alle Dateien (*.*)|*.*";
                                if (pboDlg.ShowDialog() == DialogResult.OK)
                                {
                                    pboFile = pboDlg.FileName;

                                    SetColor(ConsoleColor.DarkGray);
                                    Console.WriteLine("  Schritt 2/2 — Zielordner auswählen\r\n");
                                    ResetColor();

                                    var outDlg = new FolderBrowserDialog();
                                    outDlg.Description = "Zielordner für entpackte Dateien auswählen";
                                    outDlg.SelectedPath = Path.GetDirectoryName(pboFile);
                                    if (outDlg.ShowDialog() == DialogResult.OK)
                                    {
                                        pboOutputDir = Path.Combine(outDlg.SelectedPath,
                                            Path.GetFileNameWithoutExtension(pboFile));
                                        hasWork = true;
                                    }
                                }
                            }
                        }
                        break;
                    }

                    case 1:
                    {
                        toolMode = ToolMode.P3D;
                        string path = args[0];
                        if (File.Exists(path))
                        {
                            if (Path.GetExtension(path).ToLower() == ".p3d")
                            { filesToConvert = new[] { Path.GetFullPath(path) }; hasWork = true; }
                            else
                                Console.WriteLine("Fehler: Keine .p3d Datei: " + path);
                        }
                        else if (Directory.Exists(path))
                        {
                            filesToConvert = Directory.EnumerateFiles(path, "*.p3d", SearchOption.AllDirectories).ToArray();
                            hasWork = true;
                        }
                        else
                        {
                            Console.WriteLine("Fehler: Datei oder Ordner nicht gefunden: " + path);
                        }
                        break;
                    }

                    case 2:
                    {
                        toolMode = ToolMode.P3D;
                        string src = args[0];
                        string dst = args[1];
                        if (!Directory.Exists(src)) { Console.WriteLine("Fehler: Quellordner nicht gefunden: " + src); break; }
                        if (!Directory.Exists(dst)) { Console.WriteLine("Fehler: Zielordner nicht gefunden: " + dst); break; }
                        filesToConvert = Directory.EnumerateFiles(src, "*.p3d", SearchOption.TopDirectoryOnly).ToArray();
                        dstFolder = dst;
                        hasWork = true;
                        break;
                    }

                    default: break;
                }
            }
            catch (Exception ex)
            {
                StopScrollBanner();
                Console.WriteLine("FEHLER: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("\nTaste druecken zum Beenden...");
                try { Console.ReadKey(); } catch { }
                return 1;
            }

            // ── Respektiere Pause Screen — nur beim ersten Start ──────────────
            if (firstRun)
            {
                StopScrollBanner();
                try { Console.Clear(); } catch { }
                Console.WriteLine(titleBox);
                Console.WriteLine(respektText);
                Console.WriteLine();
                SetColor(ConsoleColor.DarkGray);
                Console.WriteLine("  Weiter mit beliebiger Taste...");
                ResetColor();
                try { if (!Console.IsInputRedirected) Console.ReadKey(true); } catch { }
                try { Console.Clear(); } catch { }
                firstRun = false;
            }

            // ── Arbeit ───────────────────────────────────────────────────────
            Console.WriteLine(titleBox);
            Console.WriteLine();

            if (!hasWork)
            {
                SetColor(ConsoleColor.DarkYellow);
                Console.WriteLine("  Abgebrochen — keine Dateien ausgewählt.");
                ResetColor();
            }
            else
            {
                switch (toolMode)
                {
                    // ──────────────────────────────────────────────────────────
                    case ToolMode.P3D:
                    {
                        Console.WriteLine(usageP3D);
                        if (filesToConvert.Length == 0)
                        {
                            SetColor(ConsoleColor.DarkYellow);
                            Console.WriteLine("Keine .p3d Dateien gefunden.");
                            ResetColor();
                            break;
                        }

                        var converted = ConvertFiles(filesToConvert, dstFolder, false);

                        if (converted.Count > 0 && !Console.IsInputRedirected)
                        {
                            // Frage 0: model.cfg generieren? (BEFORE deletion — needs originals)
                            Console.WriteLine();
                            SetColor(ConsoleColor.DarkYellow);
                            Console.Write("  model.cfg generieren?  (cfgSkeletons + CfgModels aus allen P3Ds)  [J] Ja   [N] Nein  →  ");
                            ResetColor();
                            try
                            {
                                bool genCfg = ReadJN();
                                SetColor(genCfg ? ConsoleColor.Green : ConsoleColor.DarkGray);
                                Console.WriteLine(genCfg ? "Ja" : "Nein");
                                ResetColor();
                                if (genCfg)
                                {
                                    string[] srcPaths = converted.Select(p => p[0]).ToArray();
                                    string cfgDir = dstFolder ?? Path.GetDirectoryName(srcPaths[0]);
                                    string cfgPath = Path.Combine(cfgDir, "model.cfg");
                                    string cfgContent = BuildModelCfg(srcPaths);
                                    File.WriteAllText(cfgPath, cfgContent, Encoding.UTF8);
                                    SetColor(ConsoleColor.Green);
                                    Console.WriteLine("  model.cfg geschrieben: " + ShortPath(cfgPath, 4));
                                    ResetColor();
                                }
                            }
                            catch (Exception ex)
                            {
                                SetColor(ConsoleColor.Red);
                                Console.WriteLine("  Fehler beim Generieren: " + ex.Message);
                                ResetColor();
                            }

                            // Frage 1: Originale behalten?
                            Console.WriteLine();
                            SetColor(ConsoleColor.DarkYellow);
                            Console.Write("  Originale binarisierte Dateien behalten?  [J] Ja   [N] Nein  →  ");
                            ResetColor();
                            bool keep = true;
                            try
                            {
                                keep = ReadJN();
                                SetColor(keep ? ConsoleColor.Green : ConsoleColor.Red);
                                Console.WriteLine(keep ? "Ja — Originale bleiben." : "Nein — Originale werden gelöscht.");
                                ResetColor();
                                if (!keep)
                                {
                                    int deleted = 0;
                                    foreach (string[] pair in converted)
                                        try { File.Delete(pair[0]); deleted++; } catch { }
                                    SetColor(ConsoleColor.DarkGray);
                                    Console.WriteLine("  " + deleted + " Originaldatei(en) gelöscht.");
                                    ResetColor();
                                }
                            }
                            catch { }

                            // Frage 2: _mlod umbenennen?
                            if (!keep && dstFolder == null)
                            {
                                Console.WriteLine();
                                SetColor(ConsoleColor.DarkYellow);
                                Console.Write("  _mlod Suffix entfernen?  (z.B. Suppressor_mlod.p3d → Suppressor.p3d)  [J] Ja   [N] Nein  →  ");
                                ResetColor();
                                try
                                {
                                    bool rename = ReadJN();
                                    SetColor(rename ? ConsoleColor.Green : ConsoleColor.DarkGray);
                                    Console.WriteLine(rename ? "Ja — Dateien werden umbenannt." : "Nein — _mlod bleibt.");
                                    ResetColor();
                                    if (rename)
                                    {
                                        int renamed = 0;
                                        foreach (string[] pair in converted)
                                        {
                                            string mlod = pair[1];
                                            string target = Path.Combine(Path.GetDirectoryName(mlod), Path.GetFileName(pair[0]));
                                            if (File.Exists(mlod) && !File.Exists(target))
                                                try { File.Move(mlod, target); renamed++; } catch { }
                                        }
                                        SetColor(ConsoleColor.DarkGray);
                                        Console.WriteLine("  " + renamed + " Datei(en) umbenannt.");
                                        ResetColor();
                                    }
                                }
                                catch { }
                            }
                        }
                        break;
                    }

                    // ──────────────────────────────────────────────────────────
                    case ToolMode.AnmConvert:
                    {
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("  ANM konvertieren  (TXA → ANM)");
                        Console.WriteLine("==========================================");
                        ResetColor();
                        Console.WriteLine();

                        // ── Ordner-Modus: alle TXA im Ordner batchweise konvertieren ─
                        if (txaFiles != null && txaFiles.Length > 0)
                        {
                            Console.WriteLine("  Template: " + ShortPath(templateAnmFile, 3));
                            Console.WriteLine("  Dateien:  " + txaFiles.Length + " TXA-Dateien");
                            Console.WriteLine();

                            // Template einmalig lesen
                            List<AnimBone> batchTemplate; string batchTmplErr; string batchTmplFmt;
                            if (!ReadAnmHead(templateAnmFile, out batchTemplate, out batchTmplErr, out batchTmplFmt))
                            {
                                SetColor(ConsoleColor.Red);
                                Console.WriteLine("  [!!] Template Fehler: " + batchTmplErr);
                                ResetColor();
                                break;
                            }
                            Console.WriteLine("  Template: " + batchTemplate.Count + " Knochen  (" + batchTmplFmt + ")");
                            Console.WriteLine();

                            int batchOk = 0, batchErr2 = 0;
                            foreach (string tFile in txaFiles)
                            {
                                string outAnm = Path.Combine(Path.GetDirectoryName(tFile),
                                    Path.GetFileNameWithoutExtension(tFile) + ".anm");
                                Dictionary<string, AnimQuat> batchRots; string batchTxaErr;
                                if (!ParseTxa(tFile, out batchRots, out batchTxaErr))
                                {
                                    SetColor(ConsoleColor.Red);
                                    Console.WriteLine("  [!!] " + Path.GetFileName(tFile) + "  →  " + batchTxaErr);
                                    ResetColor();
                                    batchErr2++;
                                    continue;
                                }
                                string bpErr; int bp; int bs; List<string> bNotIn;
                                if (!PatchAnm(templateAnmFile, outAnm, batchRots, out bpErr, out bp, out bs, out bNotIn))
                                {
                                    SetColor(ConsoleColor.Red);
                                    Console.WriteLine("  [!!] " + Path.GetFileName(tFile) + "  →  " + bpErr);
                                    ResetColor();
                                    batchErr2++;
                                    continue;
                                }
                                SetColor(ConsoleColor.Green);
                                Console.WriteLine("  [OK]  " + Path.GetFileName(tFile) + "  →  " + Path.GetFileName(outAnm) +
                                    "  (" + bp + " Knochen)");
                                ResetColor();
                                batchOk++;
                            }
                            Console.WriteLine();
                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("==========================================");
                            Console.WriteLine("  Fertig  |  " + batchOk + " OK  |  " + batchErr2 + " Fehler");
                            Console.WriteLine("==========================================");
                            ResetColor();
                            break;
                        }

                        // ── Einzel-Modus (original) ───────────────────────────────
                        Console.WriteLine("  TXA:      " + ShortPath(txaFile, 3));
                        Console.WriteLine("  Template: " + ShortPath(templateAnmFile, 3));
                        Console.WriteLine("  Output:   " + ShortPath(outputAnmFile, 3));
                        Console.WriteLine();

                        // TXA parsen
                        Dictionary<string, AnimQuat> rotations;
                        string txaError;
                        if (!ParseTxa(txaFile, out rotations, out txaError))
                        {
                            SetColor(ConsoleColor.Red);
                            Console.WriteLine("  [!!] TXA Parse-Fehler: " + txaError);
                            ResetColor();
                            break;
                        }

                        Console.WriteLine("  TXA: " + rotations.Count + " Knochen mit Rotation gefunden");

                        if (rotations.Count == 0)
                        {
                            SetColor(ConsoleColor.Red);
                            Console.WriteLine();
                            Console.WriteLine("  WARNUNG: Keine Knochen gefunden!");
                            Console.WriteLine("  Ist die .txa als 'Survivor IK 2h' exportiert? (nicht 'Full Body')");
                            Console.WriteLine("  IK2H hat 'LeftHandRing' als ersten Node — Full Body hat 'Scene_Root'.");
                            ResetColor();
                            break;
                        }

                        // Full Body Erkennung
                        var fbFound = KnownFullBodyBones.Intersect(rotations.Keys).ToList();
                        if (fbFound.Count > 0)
                        {
                            SetColor(ConsoleColor.Red);
                            Console.WriteLine();
                            Console.WriteLine("  FEHLER: Sieht nach 'Full Body' Export aus — nicht IK2H!");
                            Console.WriteLine("  Full-Body Knochen: " + string.Join(", ", fbFound));
                            Console.WriteLine("  In Blender neu exportieren: Export → DayZ Anim (.txa) → Typ: 'Survivor IK 2h'");
                            ResetColor();
                            break;
                        }

                        // Template ANM lesen
                        List<AnimBone> templateBones;
                        string readErr;
                        string tmplFmt;
                        if (!ReadAnmHead(templateAnmFile, out templateBones, out readErr, out tmplFmt))
                        {
                            SetColor(ConsoleColor.Red);
                            Console.WriteLine("  [!!] Template ANM Fehler: " + readErr);
                            ResetColor();
                            break;
                        }
                        Console.WriteLine("  Template: " + templateBones.Count + " Knochen im HEAD Block  (" + tmplFmt + ")");
                        Console.WriteLine();

                        // Output ≠ Template prüfen
                        if (string.Equals(Path.GetFullPath(outputAnmFile), Path.GetFullPath(templateAnmFile),
                            StringComparison.OrdinalIgnoreCase))
                        {
                            SetColor(ConsoleColor.Red);
                            Console.WriteLine("  FEHLER: Output-Pfad = Template-Pfad — Template würde überschrieben!");
                            Console.WriteLine("  Bitte anderen Output-Pfad wählen.");
                            ResetColor();
                            break;
                        }

                        // Patch anwenden
                        string patchErr; int patched; int skipped; List<string> notInTxa;
                        if (!PatchAnm(templateAnmFile, outputAnmFile, rotations,
                            out patchErr, out patched, out skipped, out notInTxa))
                        {
                            SetColor(ConsoleColor.Red);
                            Console.WriteLine("  [!!] Patch-Fehler: " + patchErr);
                            ResetColor();
                            break;
                        }

                        SetColor(ConsoleColor.Green);
                        Console.WriteLine("  [OK]  " + patched + " Knochen mit TXA-Werten ersetzt");
                        if (skipped > 0)
                        {
                            SetColor(ConsoleColor.DarkYellow);
                            Console.WriteLine("  [--]  " + skipped + " Knochen ohne TXA-Eintrag (Template-Wert beibehalten):");
                            foreach (string n in notInTxa)
                                Console.WriteLine("        - " + n);
                        }
                        ResetColor();

                        long outSize = new FileInfo(outputAnmFile).Length;
                        Console.WriteLine();
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("==========================================");
                        Console.WriteLine("  Fertig  |  " + Path.GetFileName(outputAnmFile) +
                            "  (" + (outSize / 1024.0).ToString("F1") + " KB)");
                        Console.WriteLine("==========================================");
                        ResetColor();
                        break;
                    }

                    // ──────────────────────────────────────────────────────────
                    case ToolMode.AnmInspect:
                    {
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("  ANM inspizieren");
                        Console.WriteLine("==========================================");
                        ResetColor();
                        Console.WriteLine();

                        if (inspectAnmFiles == null || inspectAnmFiles.Length == 0)
                        {
                            SetColor(ConsoleColor.Red);
                            Console.WriteLine("  [!!] Keine Dateien ausgewählt.");
                            ResetColor();
                            break;
                        }

                        bool multiFile = inspectAnmFiles.Length > 1;

                        // Parse all files
                        var fileBonesList = new List<List<AnimBone>>();
                        var fileFmts      = new List<string>();
                        var fileErrs      = new List<string>();
                        var fileOk        = new List<bool>();
                        for (int fi = 0; fi < inspectAnmFiles.Length; fi++)
                        {
                            List<AnimBone> fb; string ferr; string ffmt;
                            bool ok = ReadAnmHead(inspectAnmFiles[fi], out fb, out ferr, out ffmt);
                            fileOk.Add(ok);
                            fileBonesList.Add(ok ? fb : null);
                            fileFmts.Add(ok ? ffmt : null);
                            fileErrs.Add(ok ? null : ferr);
                        }

                        // Combined unique bone list (ordered by first appearance)
                        var allBoneNames = new List<string>();
                        var boneSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        for (int fi = 0; fi < fileBonesList.Count; fi++)
                        {
                            if (!fileOk[fi]) continue;
                            foreach (AnimBone b in fileBonesList[fi])
                                if (!string.IsNullOrEmpty(b.Name) && boneSet.Add(b.Name))
                                    allBoneNames.Add(b.Name);
                        }

                        // Build report header
                        var inspReport = new StringBuilder();
                        inspReport.AppendLine("7SBM P3D.DeBin v1.9  —  ANM Inspect");
                        if (multiFile)
                        {
                            string folderPath = Path.GetDirectoryName(inspectAnmFiles[0]);
                            inspReport.AppendLine("Ordner: " + folderPath);
                            inspReport.AppendLine("Dateien: " + inspectAnmFiles.Length);
                        }
                        else
                        {
                            inspReport.AppendLine("Datei:  " + inspectAnmFiles[0]);
                            inspReport.AppendLine("Größe:  " + (new FileInfo(inspectAnmFiles[0]).Length / 1024.0).ToString("F1") + " KB");
                        }
                        inspReport.AppendLine();

                        if (multiFile)
                        {
                            // Per-file summary
                            Console.WriteLine("  Dateien:");
                            inspReport.AppendLine("Dateien:");
                            for (int fi = 0; fi < inspectAnmFiles.Length; fi++)
                            {
                                string fname = Path.GetFileName(inspectAnmFiles[fi]);
                                if (fileOk[fi])
                                {
                                    string line = string.Format("  {0,3}.  {1}  ({2} Knochen, {3})",
                                        fi + 1, fname.PadRight(40), fileBonesList[fi].Count, fileFmts[fi] ?? "?");
                                    SetColor(ConsoleColor.Green);
                                    Console.WriteLine(line);
                                    inspReport.AppendLine(line.TrimStart());
                                }
                                else
                                {
                                    string errShort = (fileErrs[fi] ?? "Fehler").Split(new[]{'\r','\n'})[0];
                                    string line = string.Format("  {0,3}.  {1}  [!!] {2}", fi + 1, fname.PadRight(40), errShort);
                                    SetColor(ConsoleColor.Red);
                                    Console.WriteLine(line);
                                    inspReport.AppendLine(line.TrimStart());
                                }
                            }
                            ResetColor();
                            Console.WriteLine();
                            inspReport.AppendLine();

                            // Combined unique bone list
                            SetColor(ConsoleColor.Cyan);
                            string combHeader = "  Kombinierte Knochen-Liste (" + allBoneNames.Count + " einzigartig):";
                            Console.WriteLine(combHeader);
                            Console.WriteLine("  " + new string('─', 50));
                            inspReport.AppendLine(combHeader.TrimStart());
                            inspReport.AppendLine(new string('─', 50));
                            ResetColor();
                            for (int bi = 0; bi < allBoneNames.Count; bi++)
                            {
                                string boneLine = string.Format("  {0,3}.  {1}", bi, allBoneNames[bi]);
                                Console.WriteLine(boneLine);
                                inspReport.AppendLine(boneLine.TrimStart());
                            }
                        }
                        else
                        {
                            // Single file — detailed display
                            if (!fileOk[0])
                            {
                                SetColor(ConsoleColor.Red);
                                string[] errLines = (fileErrs[0] ?? "Fehler").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                                Console.WriteLine("  [!!] Fehler: " + errLines[0]);
                                for (int li = 1; li < errLines.Length; li++)
                                    Console.WriteLine("       " + errLines[li]);
                                ResetColor();
                                break;
                            }

                            List<AnimBone> bones = fileBonesList[0];
                            string fmtInfo = fileFmts[0];
                            Console.WriteLine("  Datei: " + ShortPath(inspectAnmFiles[0], 3));
                            Console.WriteLine("  Größe: " + (new FileInfo(inspectAnmFiles[0]).Length / 1024.0).ToString("F1") + " KB");
                            Console.WriteLine();

                            bool isAnimset6 = fmtInfo != null && fmtInfo.StartsWith("ANIMSET6");
                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("  Format: " + fmtInfo);
                            Console.WriteLine();
                            string inspHeader6 = "  #    Name                                    rx          ry          rz         |  qx          qy          qz          qw";
                            string inspHeader5 = "  #    Name                                    qx          qy          qz          qw";
                            if (isAnimset6)
                            {
                                Console.WriteLine(inspHeader6);
                                Console.WriteLine("  " + new string('─', 118));
                            }
                            else
                            {
                                Console.WriteLine(inspHeader5);
                                Console.WriteLine("  " + new string('─', 90));
                            }
                            ResetColor();

                            inspReport.AppendLine("Format: " + fmtInfo);
                            inspReport.AppendLine();
                            inspReport.AppendLine(isAnimset6 ? inspHeader6.TrimStart() : inspHeader5.TrimStart());
                            inspReport.AppendLine(new string('─', isAnimset6 ? 118 : 90));

                            for (int i = 0; i < bones.Count; i++)
                            {
                                AnimBone b = bones[i];
                                string nameCol = b.Name.PadRight(38);
                                bool isIdentity = Math.Abs(b.Q.X) < 1e-6f && Math.Abs(b.Q.Y) < 1e-6f &&
                                                  Math.Abs(b.Q.Z) < 1e-6f && Math.Abs(b.Q.W - 1f) < 1e-4f;
                                SetColor(isIdentity ? ConsoleColor.DarkGray : ConsoleColor.Green);
                                string boneLine;
                                if (isAnimset6 && b.HasRotVec)
                                {
                                    boneLine = string.Format(CultureInfo.InvariantCulture,
                                        "  {0,3}  {1}{2,8:F4}  {3,8:F4}  {4,8:F4}  |  {5,8:F4}  {6,8:F4}  {7,8:F4}  {8,8:F4}",
                                        i, nameCol, b.RX, b.RY, b.RZ, b.Q.X, b.Q.Y, b.Q.Z, b.Q.W);
                                }
                                else
                                {
                                    boneLine = string.Format(CultureInfo.InvariantCulture,
                                        "  {0,3}  {1}{2,11:F5}  {3,10:F5}  {4,10:F5}  {5,10:F5}",
                                        i, nameCol, b.Q.X, b.Q.Y, b.Q.Z, b.Q.W);
                                }
                                Console.WriteLine(boneLine);
                                inspReport.AppendLine(boneLine.TrimStart());
                            }
                            ResetColor();
                            Console.WriteLine();
                            SetColor(ConsoleColor.DarkGray);
                            string inspFooter = isAnimset6
                                ? "  rx/ry/rz = Rotation-Vektor (Axis×Winkel rad)  |  Quaternion rechts davon berechnet  |  Grau = keine Rotation"
                                : "  Grau = Identität (0,0,0,1) — Template-Standardwert, nicht aus TXA gesetzt";
                            Console.WriteLine(inspFooter);
                            inspReport.AppendLine();
                            inspReport.AppendLine(inspFooter.TrimStart());
                            ResetColor();
                        }

                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("==========================================");
                        ResetColor();
                        Console.WriteLine();

                        // Skeleton name for model.cfg
                        string skelName;
                        if (multiFile)
                        {
                            string folder = Path.GetDirectoryName(inspectAnmFiles[0]);
                            skelName = (Path.GetFileName(folder) ?? "Combined") + "_Skeleton";
                        }
                        else
                        {
                            skelName = Path.GetFileNameWithoutExtension(inspectAnmFiles[0]) + "_Skeleton";
                        }

                        // Save options
                        SetColor(ConsoleColor.Yellow);
                        Console.Write("  [T] Inspect-Bericht (.txt)   [M] model.cfg (CfgSkeletons)   [W/X]  > ");
                        ResetColor();
                        ConsoleKey inspKey = Console.ReadKey(true).Key;
                        Console.WriteLine();
                        if (inspKey == ConsoleKey.T || inspKey == ConsoleKey.S || inspKey == ConsoleKey.M)
                        {
                            bool wantModelCfg = (inspKey == ConsoleKey.M);
                            string defaultDir = Path.GetDirectoryName(inspectAnmFiles[0]);
                            string defaultName = wantModelCfg ? "model.cfg"
                                : (multiFile
                                    ? (Path.GetFileName(Path.GetDirectoryName(inspectAnmFiles[0])) ?? "combined") + "_inspect.txt"
                                    : Path.GetFileNameWithoutExtension(inspectAnmFiles[0]) + "_inspect.txt");
                            string savePath = null;
                            Thread stSave = new Thread(() =>
                            {
                                var sfd = new SaveFileDialog
                                {
                                    Title = wantModelCfg ? "model.cfg speichern" : "Inspect-Bericht speichern",
                                    Filter = wantModelCfg
                                        ? "Model Config (*.cfg)|*.cfg|Alle Dateien (*.*)|*.*"
                                        : "Textdatei (*.txt)|*.txt|Alle Dateien (*.*)|*.*",
                                    FileName = defaultName,
                                    InitialDirectory = defaultDir
                                };
                                if (sfd.ShowDialog() == DialogResult.OK)
                                    savePath = sfd.FileName;
                            });
                            stSave.SetApartmentState(ApartmentState.STA);
                            stSave.Start();
                            stSave.Join();

                            if (!string.IsNullOrEmpty(savePath))
                            {
                                string outText;
                                if (wantModelCfg)
                                {
                                    var cfg = new StringBuilder();
                                    cfg.AppendLine("// Generiert von 7SBM P3D.DeBin v1.9");
                                    if (multiFile)
                                        cfg.AppendLine("// Ordner: " + Path.GetDirectoryName(inspectAnmFiles[0]));
                                    else
                                        cfg.AppendLine("// Quelle: " + Path.GetFileName(inspectAnmFiles[0]));
                                    cfg.AppendLine("// Knochen: " + allBoneNames.Count);
                                    cfg.AppendLine("// Hinweis: Parent-Angaben stammen aus dem ANM-Record (uint32 @+0).");
                                    cfg.AppendLine("//          Prüfe die Hierarchie in Object Builder / Blender.");
                                    cfg.AppendLine();
                                    cfg.AppendLine("class CfgSkeletons");
                                    cfg.AppendLine("{");
                                    cfg.AppendLine("\tclass " + skelName);
                                    cfg.AppendLine("\t{");
                                    cfg.AppendLine("\t\tisDiscrete=0;");
                                    cfg.AppendLine("\t\tskeletonInherit=\"\";");
                                    cfg.AppendLine("\t\tskeletonBones[]=");
                                    cfg.AppendLine("\t\t{");
                                    for (int bi = 0; bi < allBoneNames.Count; bi++)
                                    {
                                        string comma = (bi < allBoneNames.Count - 1) ? "," : "";
                                        cfg.AppendLine(string.Format("\t\t\t\"{0}\", \"\"{1}", allBoneNames[bi], comma));
                                    }
                                    cfg.AppendLine("\t\t};");
                                    cfg.AppendLine("\t};");
                                    cfg.AppendLine("};");
                                    cfg.AppendLine();
                                    cfg.AppendLine("class CfgModels");
                                    cfg.AppendLine("{");
                                    cfg.AppendLine("\tclass P3dFilenameNoExtension");
                                    cfg.AppendLine("\t{");
                                    cfg.AppendLine("\t\tskeletonName=" + skelName + ";");
                                    cfg.AppendLine("\t\tsectionsInherit=\"\";");
                                    cfg.AppendLine("\t\tsections[]={};");
                                    cfg.AppendLine("\t\tclass Animations");
                                    cfg.AppendLine("\t\t{");
                                    cfg.AppendLine("\t\t};");
                                    cfg.AppendLine("\t};");
                                    cfg.AppendLine("};");
                                    outText = cfg.ToString();
                                }
                                else
                                {
                                    outText = inspReport.ToString();
                                }
                                File.WriteAllText(savePath, outText, new UTF8Encoding(false));
                                SetColor(ConsoleColor.Green);
                                Console.WriteLine("  [OK]  Gespeichert: " + savePath);
                                ResetColor();
                                Console.WriteLine();
                            }
                        }
                        break;
                    }

                    // ──────────────────────────────────────────────────────────
                    case ToolMode.AnmExport:
                    {
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("  ANM exportieren  (ANM → TXA)");
                        Console.WriteLine("==========================================");
                        ResetColor();
                        Console.WriteLine();

                        // ── Ordner-Modus ──────────────────────────────────────────
                        if (exportAnmFiles != null && exportAnmFiles.Length > 0)
                        {
                            Console.WriteLine("  Dateien: " + exportAnmFiles.Length + " ANM-Dateien");
                            Console.WriteLine();
                            int eOk = 0, eErr = 0;
                            foreach (string anmF in exportAnmFiles)
                            {
                                List<AnimBone> eb; string ee; string ef;
                                if (!ReadAnmHead(anmF, out eb, out ee, out ef))
                                {
                                    SetColor(ConsoleColor.Red);
                                    Console.WriteLine("  [!!] " + Path.GetFileName(anmF) + "  →  " + ee.Split('\n')[0]);
                                    ResetColor();
                                    eErr++;
                                    continue;
                                }
                                string outTxa = Path.Combine(Path.GetDirectoryName(anmF),
                                    Path.GetFileNameWithoutExtension(anmF) + ".txa");
                                var etxa = new StringBuilder();
                                etxa.AppendLine("// Exportiert von 7SBM P3D.DeBin v1.9");
                                etxa.AppendLine("// Quelle: " + Path.GetFileName(anmF));
                                etxa.AppendLine("// Format: " + ef);
                                etxa.AppendLine();
                                string eStem = Path.GetFileNameWithoutExtension(anmF);
                                etxa.AppendLine("$animation \"" + eStem + "\" {");
                                etxa.AppendLine(" #fps 30");
                                etxa.AppendLine(" #numFrames 1");
                                int cnt = 0;
                                foreach (AnimBone b in eb)
                                {
                                    if (string.IsNullOrEmpty(b.Name)) continue;
                                    bool isId = Math.Abs(b.Q.X) < 1e-6f && Math.Abs(b.Q.Y) < 1e-6f &&
                                                Math.Abs(b.Q.Z) < 1e-6f && Math.Abs(b.Q.W - 1f) < 1e-4f;
                                    if (isId) continue;
                                    etxa.AppendLine(" $node \"" + b.Name + "\" {");
                                    etxa.AppendLine("   $keys t q s {");
                                    etxa.AppendLine("     $frame 0 {");
                                    etxa.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                        "       #q {0:F7} {1:F7} {2:F7} {3:F7}", b.Q.X, b.Q.Y, b.Q.Z, b.Q.W));
                                    etxa.AppendLine("     }");
                                    etxa.AppendLine("   }");
                                    etxa.AppendLine(" }");
                                    cnt++;
                                }
                                etxa.AppendLine("}");
                                File.WriteAllText(outTxa, etxa.ToString(), new UTF8Encoding(false));
                                SetColor(ConsoleColor.Green);
                                Console.WriteLine("  [OK]  " + Path.GetFileName(anmF) + "  →  " +
                                    Path.GetFileName(outTxa) + "  (" + cnt + " Knochen)");
                                ResetColor();
                                eOk++;
                            }
                            Console.WriteLine();
                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("==========================================");
                            Console.WriteLine("  Fertig  |  " + eOk + " OK  |  " + eErr + " Fehler");
                            Console.WriteLine("==========================================");
                            ResetColor();
                            break;
                        }

                        // ── Einzel-Modus ──────────────────────────────────────────
                        Console.WriteLine("  ANM:    " + ShortPath(exportAnmFile, 3));
                        Console.WriteLine("  Output: " + ShortPath(exportTxaFile, 3));
                        Console.WriteLine();

                        List<AnimBone> expBones; string expErr; string expFmt;
                        if (!ReadAnmHead(exportAnmFile, out expBones, out expErr, out expFmt))
                        {
                            SetColor(ConsoleColor.Red);
                            string[] errLines = expErr.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                            Console.WriteLine("  [!!] Fehler: " + errLines[0]);
                            for (int li = 1; li < errLines.Length; li++)
                                Console.WriteLine("       " + errLines[li]);
                            ResetColor();
                            break;
                        }

                        Console.WriteLine("  Format: " + expFmt);
                        Console.WriteLine();

                        // TXA schreiben
                        var txa = new StringBuilder();
                        txa.AppendLine("// Exportiert von 7SBM P3D.DeBin v1.9");
                        txa.AppendLine("// Quelle: " + Path.GetFileName(exportAnmFile));
                        txa.AppendLine("// Format: " + expFmt);
                        txa.AppendLine();
                        string expStem = Path.GetFileNameWithoutExtension(exportAnmFile);
                        txa.AppendLine("$animation \"" + expStem + "\" {");
                        txa.AppendLine(" #fps 30");
                        txa.AppendLine(" #numFrames 1");

                        int exported = 0;
                        foreach (AnimBone b in expBones)
                        {
                            if (string.IsNullOrEmpty(b.Name)) continue;
                            // Identität weglassen — Blender braucht nur Knochen mit echter Rotation
                            bool isIdentity = Math.Abs(b.Q.X) < 1e-6f && Math.Abs(b.Q.Y) < 1e-6f &&
                                              Math.Abs(b.Q.Z) < 1e-6f && Math.Abs(b.Q.W - 1f) < 1e-4f;
                            if (isIdentity) continue;

                            txa.AppendLine(" $node \"" + b.Name + "\" {");
                            txa.AppendLine("   $keys t q s {");
                            txa.AppendLine("     $frame 0 {");
                            txa.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                "       #q {0:F7} {1:F7} {2:F7} {3:F7}",
                                b.Q.X, b.Q.Y, b.Q.Z, b.Q.W));
                            txa.AppendLine("     }");
                            txa.AppendLine("   }");
                            txa.AppendLine(" }");
                            exported++;
                        }
                        txa.AppendLine("}");
                        File.WriteAllText(exportTxaFile, txa.ToString(), new UTF8Encoding(false));

                        SetColor(ConsoleColor.Green);
                        Console.WriteLine("  [OK]  " + exported + " Knochen exportiert");
                        if (expBones.Count - exported > 0)
                        {
                            SetColor(ConsoleColor.DarkGray);
                            Console.WriteLine("  [--]  " + (expBones.Count - exported) + " Knochen übersprungen (Identität / kein Name)");
                        }
                        ResetColor();

                        long outSz = new FileInfo(exportTxaFile).Length;
                        Console.WriteLine();
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("==========================================");
                        Console.WriteLine("  Fertig  |  " + Path.GetFileName(exportTxaFile) +
                            "  (" + (outSz / 1024.0).ToString("F1") + " KB)");
                        Console.WriteLine("==========================================");
                        ResetColor();
                        break;
                    }

                    // ──────────────────────────────────────────────────────────
                    case ToolMode.PboExtract:
                    {
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("  PBO entpacken");
                        Console.WriteLine("==========================================");
                        ResetColor();
                        Console.WriteLine();

                        // ── Ordner-Modus ──────────────────────────────────────────
                        if (pboFiles != null && pboFiles.Length > 0)
                        {
                            Console.WriteLine("  Ordner: " + ShortPath(pboOutputDir, 3));
                            Console.WriteLine("  PBOs:   " + pboFiles.Length);
                            Console.WriteLine();
                            int pOk = 0, pErr = 0;
                            foreach (string pf in pboFiles)
                            {
                                string outDir = Path.Combine(pboOutputDir,
                                    Path.GetFileNameWithoutExtension(pf));
                                Dictionary<string, string> pp; string pe;
                                int cnt = PboExtractAll(pf, outDir, out pp, out pe);
                                if (cnt < 0)
                                {
                                    SetColor(ConsoleColor.Red);
                                    Console.WriteLine("  [!!] " + Path.GetFileName(pf) + "  →  " + pe);
                                    ResetColor();
                                    pErr++;
                                    continue;
                                }
                                SetColor(ConsoleColor.Green);
                                Console.WriteLine("  [OK]  " + Path.GetFileName(pf) +
                                    "  →  " + Path.GetFileName(outDir) + "\\  (" + cnt + " Dateien)");
                                ResetColor();

                                // config.bin → config.cpp
                                try
                                {
                                    string[] cBins = Directory.GetFiles(outDir,
                                        "config.bin", SearchOption.AllDirectories);
                                    foreach (string cb in cBins)
                                    {
                                        string cppP = Path.Combine(
                                            Path.GetDirectoryName(cb), "config.cpp");
                                        string rapC; string rapE;
                                        if (TryRapToCpp(cb, out rapC, out rapE))
                                        {
                                            File.WriteAllText(cppP, rapC,
                                                new UTF8Encoding(false));
                                            SetColor(ConsoleColor.Cyan);
                                            Console.WriteLine("        → config.cpp  (" +
                                                (rapC.Length / 1024.0).ToString("F1") + " KB)");
                                            ResetColor();
                                        }
                                    }
                                }
                                catch { }

                                // binarisierte .rvmat → lesbares raP-Textformat
                                try
                                {
                                    string[] rvmats = Directory.GetFiles(outDir,
                                        "*.rvmat", SearchOption.AllDirectories);
                                    foreach (string rv in rvmats)
                                    {
                                        string rvC; string rvE;
                                        if (TryRapToCpp(rv, out rvC, out rvE))
                                        {
                                            File.WriteAllText(rv, rvC,
                                                new UTF8Encoding(false));
                                            SetColor(ConsoleColor.Cyan);
                                            Console.WriteLine("        → " +
                                                Path.GetFileName(rv) + " debinarisiert  (" +
                                                (rvC.Length / 1024.0).ToString("F1") + " KB)");
                                            ResetColor();
                                        }
                                    }
                                }
                                catch { }

                                pOk++;
                            }
                            Console.WriteLine();
                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("==========================================");
                            Console.WriteLine("  Fertig  |  " + pOk + " OK  |  " + pErr + " Fehler");
                            Console.WriteLine("==========================================");
                            ResetColor();
                            break;
                        }

                        // ── Einzel-Modus ──────────────────────────────────────────
                        Console.WriteLine("  PBO:    " + ShortPath(pboFile, 3));
                        Console.WriteLine("  Output: " + ShortPath(pboOutputDir, 3));
                        Console.WriteLine();

                        Dictionary<string, string> pboProps;
                        string pboErr;
                        int count = PboExtractAll(pboFile, pboOutputDir, out pboProps, out pboErr);

                        if (count < 0)
                        {
                            SetColor(ConsoleColor.Red);
                            Console.WriteLine("  [!!] Fehler: " + pboErr);
                            ResetColor();
                            break;
                        }

                        // Properties anzeigen (max 10, Rest zusammenfassen)
                        if (pboProps.Count > 0)
                        {
                            SetColor(ConsoleColor.DarkGray);
                            Console.WriteLine("  Properties:");
                            int pShown = 0;
                            foreach (var kv in pboProps)
                            {
                                if (pShown >= 10)
                                {
                                    Console.WriteLine("    ... +" + (pboProps.Count - 10) + " weitere");
                                    break;
                                }
                                string val = kv.Value.Length > 60
                                    ? kv.Value.Substring(0, 57) + "..."
                                    : kv.Value;
                                Console.WriteLine("    " + kv.Key + " = " + val);
                                pShown++;
                            }
                            ResetColor();
                            Console.WriteLine();
                        }

                        // Dateien auflisten (max 50, Rest zusammenfassen)
                        SetColor(ConsoleColor.Green);
                        try
                        {
                            string[] extracted = Directory.GetFiles(pboOutputDir, "*",
                                SearchOption.AllDirectories);
                            int fShown = 0;
                            foreach (string f in extracted)
                            {
                                if (fShown >= 50)
                                {
                                    Console.WriteLine("  ...  +" + (extracted.Length - 50) +
                                        " weitere Dateien");
                                    break;
                                }
                                long sz = new FileInfo(f).Length;
                                string rel = f.Substring(pboOutputDir.Length).TrimStart(
                                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                Console.WriteLine(string.Format("  [OK]  {0,-50}  {1,8} B",
                                    rel, sz));
                                fShown++;
                            }
                        }
                        catch { }
                        ResetColor();

                        // Auto-convert config.bin → config.cpp (wie Mikero ExtractPBO)
                        try
                        {
                            string[] configBins = Directory.GetFiles(pboOutputDir,
                                "config.bin", SearchOption.AllDirectories);
                            foreach (string cb in configBins)
                            {
                                string cppPath = Path.Combine(
                                    Path.GetDirectoryName(cb), "config.cpp");
                                string rapCpp; string rapErr;
                                if (TryRapToCpp(cb, out rapCpp, out rapErr))
                                {
                                    File.WriteAllText(cppPath, rapCpp, new UTF8Encoding(false));
                                    SetColor(ConsoleColor.Cyan);
                                    string rapRel = cb.Substring(pboOutputDir.Length)
                                        .TrimStart(Path.DirectorySeparatorChar,
                                                   Path.AltDirectorySeparatorChar);
                                    Console.WriteLine(string.Format(
                                        "  [raP]  {0,-44}  → config.cpp  ({1:F1} KB)",
                                        rapRel, rapCpp.Length / 1024.0));
                                    ResetColor();
                                }
                                else
                                {
                                    SetColor(ConsoleColor.DarkYellow);
                                    Console.WriteLine("  [raP]  config.bin nicht konvertiert: " + rapErr);
                                    ResetColor();
                                }
                            }
                        }
                        catch { }

                        // binarisierte .rvmat → lesbares raP-Textformat (wie Mikero DeRap)
                        try
                        {
                            string[] rvmatsSingle = Directory.GetFiles(pboOutputDir,
                                "*.rvmat", SearchOption.AllDirectories);
                            foreach (string rv in rvmatsSingle)
                            {
                                string rvC; string rvE;
                                if (TryRapToCpp(rv, out rvC, out rvE))
                                {
                                    File.WriteAllText(rv, rvC, new UTF8Encoding(false));
                                    SetColor(ConsoleColor.Cyan);
                                    string rvRel = rv.Substring(pboOutputDir.Length)
                                        .TrimStart(Path.DirectorySeparatorChar,
                                                   Path.AltDirectorySeparatorChar);
                                    Console.WriteLine(string.Format(
                                        "  [raP]  {0,-44}  → debinarisiert  ({1:F1} KB)",
                                        rvRel, rvC.Length / 1024.0));
                                    ResetColor();
                                }
                            }
                        }
                        catch { }

                        Console.WriteLine();
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("==========================================");
                        Console.WriteLine("  Fertig  |  " + count + " Datei(en) entpackt");
                        Console.WriteLine("  →  " + pboOutputDir);
                        Console.WriteLine("==========================================");
                        ResetColor();
                        break;
                    }

                    // ──────────────────────────────────────────────────────────
                    case ToolMode.RtmInspect:
                    {
                        // ── Ordner-Modus: alle RTM → TXA auto-exportieren ─────────
                        if (inspectRtmFiles != null && inspectRtmFiles.Length > 0)
                        {
                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("  RTM Batch-Export  (RTM → TXA)");
                            Console.WriteLine("==========================================");
                            ResetColor();
                            Console.WriteLine();
                            Console.WriteLine("  Dateien: " + inspectRtmFiles.Length + " RTM-Dateien");
                            Console.WriteLine();
                            int rOk = 0, rErr = 0;
                            foreach (string rf in inspectRtmFiles)
                            {
                                byte[] rrd = File.ReadAllBytes(rf);
                                if (rrd.Length < 0x25 || System.Text.Encoding.ASCII.GetString(rrd, 0, 4) != "BMTR")
                                {
                                    SetColor(ConsoleColor.Red);
                                    Console.WriteLine("  [!!] " + Path.GetFileName(rf) + "  — kein BMTR");
                                    ResetColor();
                                    rErr++;
                                    continue;
                                }
                                int rNF = BitConverter.ToInt32(rrd, 8);
                                var rBones = new List<string>();
                                int rp = 0x25;
                                while (rp < rrd.Length)
                                {
                                    int re = rp;
                                    while (re < rrd.Length && rrd[re] != 0) re++;
                                    if (re == rp) { break; }
                                    rBones.Add(System.Text.Encoding.ASCII.GetString(rrd, rp, re - rp));
                                    rp = re + 1;
                                }
                                var rPhases = new List<float>();
                                var rQuats  = new List<float[][]>();
                                for (int fi2 = 0; fi2 < rNF; fi2++)
                                {
                                    if (rp + 20 > rrd.Length) break;
                                    float rPh = BitConverter.ToSingle(rrd, rp);
                                    int rNB   = BitConverter.ToInt32(rrd, rp + 16);
                                    rp += 20;
                                    rPhases.Add(rPh);
                                    float[][] fq2 = new float[rNB < 0 ? 0 : rNB][];
                                    for (int bi2 = 0; bi2 < rNB && rp + 14 <= rrd.Length; bi2++)
                                    {
                                        float qx2 = BitConverter.ToInt16(rrd, rp + 0) / 16384f;
                                        float qy2 = BitConverter.ToInt16(rrd, rp + 2) / 16384f;
                                        float qz2 = BitConverter.ToInt16(rrd, rp + 4) / 16384f;
                                        float qw2 = BitConverter.ToInt16(rrd, rp + 6) / 16384f;
                                        fq2[bi2] = new float[] { qx2, qy2, qz2, qw2 };
                                        rp += 14;
                                    }
                                    rQuats.Add(fq2);
                                }
                                // TXA schreiben
                                string outTxa2 = Path.Combine(Path.GetDirectoryName(rf),
                                    Path.GetFileNameWithoutExtension(rf) + ".txa");
                                var rtxa = new StringBuilder();
                                rtxa.AppendLine("// Exportiert von 7SBM P3D.DeBin v1.9  —  RTM → TXA");
                                rtxa.AppendLine("// Quelle: " + Path.GetFileName(rf));
                                rtxa.AppendLine("// Knochen: " + rBones.Count + "   Frames: " + rQuats.Count);
                                rtxa.AppendLine();
                                string rStem2 = Path.GetFileNameWithoutExtension(rf);
                                rtxa.AppendLine("$animation \"" + rStem2 + "\" {");
                                rtxa.AppendLine(" #fps 30");
                                rtxa.AppendLine(" #numFrames " + rQuats.Count);
                                int rExp = 0;
                                for (int bi2 = 0; bi2 < rBones.Count; bi2++)
                                {
                                    bool any = false;
                                    for (int fi2 = 0; fi2 < rQuats.Count; fi2++)
                                    {
                                        if (bi2 >= rQuats[fi2].Length || rQuats[fi2][bi2] == null) continue;
                                        float[] fq2 = rQuats[fi2][bi2];
                                        if (Math.Abs(fq2[0]) > 1e-4f || Math.Abs(fq2[1]) > 1e-4f ||
                                            Math.Abs(fq2[2]) > 1e-4f || Math.Abs(fq2[3] - 1f) > 1e-3f)
                                        { any = true; break; }
                                    }
                                    if (!any) continue;
                                    rtxa.AppendLine(" $node \"" + rBones[bi2] + "\" {");
                                    rtxa.AppendLine("   $keys t q s {");
                                    for (int fi2 = 0; fi2 < rQuats.Count; fi2++)
                                    {
                                        if (bi2 >= rQuats[fi2].Length || rQuats[fi2][bi2] == null) continue;
                                        float[] fq2 = rQuats[fi2][bi2];
                                        rtxa.AppendLine("     $frame " + fi2 + " {");
                                        rtxa.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                            "       #q {0:F7} {1:F7} {2:F7} {3:F7}", fq2[0], fq2[1], fq2[2], fq2[3]));
                                        rtxa.AppendLine("     }");
                                    }
                                    rtxa.AppendLine("   }");
                                    rtxa.AppendLine(" }");
                                    rExp++;
                                }
                                rtxa.AppendLine("}");
                                File.WriteAllText(outTxa2, rtxa.ToString(), new UTF8Encoding(false));
                                SetColor(ConsoleColor.Green);
                                Console.WriteLine("  [OK]  " + Path.GetFileName(rf) + "  →  " +
                                    Path.GetFileName(outTxa2) + "  (" + rExp + " Knochen)");
                                ResetColor();
                                rOk++;
                            }
                            Console.WriteLine();
                            SetColor(ConsoleColor.Cyan);
                            Console.WriteLine("==========================================");
                            Console.WriteLine("  Fertig  |  " + rOk + " OK  |  " + rErr + " Fehler");
                            Console.WriteLine("==========================================");
                            ResetColor();
                            break;
                        }

                        // ── Einzel-Modus ──────────────────────────────────────────
                        string rtmPath = inspectAnmFile;
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("  RTM inspizieren  (Arma3 BMTR)");
                        Console.WriteLine("==========================================");
                        ResetColor();
                        Console.WriteLine();
                        Console.WriteLine("  RTM:  " + ShortPath(rtmPath, 3));
                        Console.WriteLine();

                        byte[] rd = File.ReadAllBytes(rtmPath);
                        if (rd.Length < 0x25 || System.Text.Encoding.ASCII.GetString(rd, 0, 4) != "BMTR")
                        {
                            SetColor(ConsoleColor.Red);
                            Console.WriteLine("  [!!] Keine BMTR-Datei (fehlendes Magic).");
                            ResetColor();
                            break;
                        }

                        int rtmVersion   = BitConverter.ToInt32(rd, 4);
                        int rtmNumFrames = BitConverter.ToInt32(rd, 8);
                        float rtmPhase   = BitConverter.ToSingle(rd, 12);

                        // Knochennamen lesen (null-terminiert ab 0x25)
                        // WICHTIG: kein rpos++ beim leeren String — das erste 0x00-Byte
                        // gehört bereits zum Frame-Header (Phase-Float 0.0 = 0x00000000)
                        var rtmBones = new List<string>();
                        int rpos = 0x25;
                        while (rpos < rd.Length)
                        {
                            int rend = rpos;
                            while (rend < rd.Length && rd[rend] != 0) rend++;
                            if (rend == rpos) { break; }
                            rtmBones.Add(System.Text.Encoding.ASCII.GetString(rd, rpos, rend - rpos));
                            rpos = rend + 1;
                        }

                        SetColor(ConsoleColor.White);
                        Console.WriteLine(string.Format("  Version: {0}   Frames: {1}   Phase: {2}",
                            rtmVersion, rtmNumFrames, rtmPhase.ToString("F4", CultureInfo.InvariantCulture)));
                        Console.WriteLine(string.Format("  Knochen: {0}", rtmBones.Count));
                        ResetColor();
                        Console.WriteLine();

                        // Tabellenheader
                        string rtmHeader = "  #    Name                        qx          qy          qz          qw         px     py     pz";
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine(rtmHeader);
                        Console.WriteLine("  " + new string('─', 97));
                        ResetColor();

                        var rtmReport = new StringBuilder();
                        rtmReport.AppendLine("7SBM P3D.DeBin v1.9  —  RTM Inspect (Arma3 BMTR)");
                        rtmReport.AppendLine("RTM:     " + rtmPath);
                        rtmReport.AppendLine("Größe:   " + (rd.Length / 1024.0).ToString("F1") + " KB");
                        rtmReport.AppendLine(string.Format("Version: {0}   Frames: {1}   Phase: {2}",
                            rtmVersion, rtmNumFrames, rtmPhase.ToString("F4", CultureInfo.InvariantCulture)));
                        rtmReport.AppendLine(string.Format("Knochen: {0}", rtmBones.Count));
                        rtmReport.AppendLine();
                        rtmReport.AppendLine(rtmHeader.TrimStart());
                        rtmReport.AppendLine(new string('─', 97));

                        // Frame-Daten: 20-Byte Frame-Header + numBones × 14 Byte pro Knochen
                        // Frame-Header LE: float phase(4) + int32×3 unbekannt(12) + int32 numBones(4)
                        // Gleichzeitig Daten für TXA-Export speichern
                        var rtmFramePhases = new List<float>();
                        var rtmFrameQuats  = new List<float[][]>(); // [frame][bone] = float[4]{qx,qy,qz,qw}
                        bool rtmFrameOk = true;
                        for (int fi = 0; fi < rtmNumFrames && rtmFrameOk; fi++)
                        {
                            if (rpos + 20 > rd.Length) { rtmFrameOk = false; break; }
                            float framePh       = BitConverter.ToSingle(rd, rpos);
                            int numBonesInFrame = BitConverter.ToInt32(rd, rpos + 16);
                            rpos += 20;

                            rtmFramePhases.Add(framePh);
                            float[][] frameQuats = new float[numBonesInFrame < 0 ? 0 : numBonesInFrame][];

                            if (rtmNumFrames > 1)
                            {
                                SetColor(ConsoleColor.DarkGray);
                                string frameHdr = string.Format("--- Frame {0}  phase={1}  bones={2} ---",
                                    fi, framePh.ToString("F4", CultureInfo.InvariantCulture), numBonesInFrame);
                                Console.WriteLine("  " + frameHdr);
                                rtmReport.AppendLine(frameHdr);
                                ResetColor();
                            }

                            for (int bi = 0; bi < numBonesInFrame && rpos + 14 <= rd.Length; bi++)
                            {
                                short ix = BitConverter.ToInt16(rd, rpos + 0);
                                short iy = BitConverter.ToInt16(rd, rpos + 2);
                                short iz = BitConverter.ToInt16(rd, rpos + 4);
                                short iw = BitConverter.ToInt16(rd, rpos + 6);
                                float qx = ix / 16384f;
                                float qy = iy / 16384f;
                                float qz = iz / 16384f;
                                float qw = iw / 16384f;
                                short px = BitConverter.ToInt16(rd, rpos + 8);
                                short py = BitConverter.ToInt16(rd, rpos + 10);
                                short pz = BitConverter.ToInt16(rd, rpos + 12);
                                rpos += 14;

                                frameQuats[bi] = new float[] { qx, qy, qz, qw };

                                string boneName = (bi < rtmBones.Count) ? rtmBones[bi] : ("bone" + bi);
                                bool isIdentity = Math.Abs(qx) < 1e-4f && Math.Abs(qy) < 1e-4f &&
                                                  Math.Abs(qz) < 1e-4f && Math.Abs(qw - 1f) < 1e-3f;
                                SetColor(isIdentity ? ConsoleColor.DarkGray : ConsoleColor.Green);
                                string boneLine = string.Format(CultureInfo.InvariantCulture,
                                    "  {0,3}  {1,-26}  {2,8:F4}  {3,8:F4}  {4,8:F4}  {5,8:F4}   {6,5}  {7,5}  {8,5}",
                                    bi, boneName, qx, qy, qz, qw, px, py, pz);
                                Console.WriteLine(boneLine);
                                ResetColor();
                                rtmReport.AppendLine(boneLine.TrimStart());
                            }
                            rtmFrameQuats.Add(frameQuats);
                            if (rtmNumFrames > 1)
                            {
                                Console.WriteLine();
                                rtmReport.AppendLine();
                            }
                        }

                        ResetColor();
                        Console.WriteLine();
                        SetColor(ConsoleColor.DarkGray);
                        Console.WriteLine("  Grau = Identität (0,0,0,1) — keine Rotation");
                        rtmReport.AppendLine();
                        rtmReport.AppendLine("Grau = Identität (0,0,0,1) — keine Rotation");
                        ResetColor();
                        SetColor(ConsoleColor.Cyan);
                        Console.WriteLine("==========================================");
                        Console.WriteLine("  Fertig  |  " + rtmBones.Count + " Knochen  |  " + rtmNumFrames + " Frame(s)");
                        Console.WriteLine("==========================================");
                        ResetColor();
                        Console.WriteLine();

                        // Skeleton-Name für model.cfg
                        string rtmSkelName = Path.GetFileNameWithoutExtension(rtmPath) + "_Skeleton";

                        // Speicher-Optionen
                        SetColor(ConsoleColor.Yellow);
                        Console.Write("  [S] Bericht (.txt)   [M] model.cfg   [T] TXA exportieren   [W/X]  > ");
                        ResetColor();
                        ConsoleKey rtmSaveKey = Console.ReadKey(true).Key;
                        Console.WriteLine();
                        if (rtmSaveKey == ConsoleKey.S || rtmSaveKey == ConsoleKey.M || rtmSaveKey == ConsoleKey.T)
                        {
                            bool rtmWantCfg = (rtmSaveKey == ConsoleKey.M);
                            bool rtmWantTxa = (rtmSaveKey == ConsoleKey.T);
                            string rtmSavePath = null;
                            Thread rtmSaveThread = new Thread(() =>
                            {
                                var sdlg = new SaveFileDialog();
                                sdlg.Title = rtmWantCfg ? "model.cfg speichern"
                                           : rtmWantTxa ? "TXA-Datei speichern"
                                           : "RTM-Bericht speichern";
                                sdlg.Filter = rtmWantCfg
                                    ? "Model Config (*.cfg)|*.cfg|Alle Dateien (*.*)|*.*"
                                    : rtmWantTxa
                                    ? "DayZ Animation Source (*.txa)|*.txa|Alle Dateien (*.*)|*.*"
                                    : "Text-Dateien (*.txt)|*.txt|Alle Dateien (*.*)|*.*";
                                sdlg.FileName = rtmWantCfg ? "model.cfg"
                                              : rtmWantTxa ? Path.GetFileNameWithoutExtension(rtmPath) + ".txa"
                                              : Path.GetFileNameWithoutExtension(rtmPath) + "_rtm.txt";
                                sdlg.InitialDirectory = Path.GetDirectoryName(rtmPath);
                                if (sdlg.ShowDialog() == DialogResult.OK)
                                    rtmSavePath = sdlg.FileName;
                            });
                            rtmSaveThread.SetApartmentState(ApartmentState.STA);
                            rtmSaveThread.Start();
                            rtmSaveThread.Join();
                            if (rtmSavePath != null)
                            {
                                string rtmOutText;
                                if (rtmWantCfg)
                                {
                                    var cfg = new StringBuilder();
                                    cfg.AppendLine("// Generiert von 7SBM P3D.DeBin v1.9");
                                    cfg.AppendLine("// Quelle: " + Path.GetFileName(rtmPath));
                                    cfg.AppendLine("// Knochen: " + rtmBones.Count + "  (Arma3 Player-Skelett)");
                                    cfg.AppendLine("// Hinweis: Prüfe die Hierarchie in Object Builder.");
                                    cfg.AppendLine();
                                    cfg.AppendLine("class CfgSkeletons");
                                    cfg.AppendLine("{");
                                    cfg.AppendLine("\tclass " + rtmSkelName);
                                    cfg.AppendLine("\t{");
                                    cfg.AppendLine("\t\tisDiscrete=0;");
                                    cfg.AppendLine("\t\tskeletonInherit=\"\";");
                                    cfg.AppendLine("\t\tskeletonBones[]=");
                                    cfg.AppendLine("\t\t{");
                                    for (int bi = 0; bi < rtmBones.Count; bi++)
                                    {
                                        string comma = (bi < rtmBones.Count - 1) ? "," : "";
                                        cfg.AppendLine(string.Format("\t\t\t\"{0}\", \"\"{1}", rtmBones[bi], comma));
                                    }
                                    cfg.AppendLine("\t\t};");
                                    cfg.AppendLine("\t};");
                                    cfg.AppendLine("};");
                                    cfg.AppendLine();
                                    cfg.AppendLine("class CfgModels");
                                    cfg.AppendLine("{");
                                    cfg.AppendLine("\tclass P3dFilenameNoExtension");
                                    cfg.AppendLine("\t{");
                                    cfg.AppendLine("\t\tskeletonName=" + rtmSkelName + ";");
                                    cfg.AppendLine("\t\tsectionsInherit=\"\";");
                                    cfg.AppendLine("\t\tsections[]={};");
                                    cfg.AppendLine("\t\tclass Animations");
                                    cfg.AppendLine("\t\t{");
                                    cfg.AppendLine("\t\t};");
                                    cfg.AppendLine("\t};");
                                    cfg.AppendLine("};");
                                    rtmOutText = cfg.ToString();
                                }
                                else if (rtmWantTxa)
                                {
                                    // RTM → TXA: $animation-Block mit $keys t q s pro Knochen
                                    var txa = new StringBuilder();
                                    txa.AppendLine("// Exportiert von 7SBM P3D.DeBin v1.9  —  RTM → TXA");
                                    txa.AppendLine("// Quelle: " + Path.GetFileName(rtmPath));
                                    txa.AppendLine("// Knochen: " + rtmBones.Count + "   Frames: " + rtmFrameQuats.Count);
                                    txa.AppendLine("// Hinweis: Arma3-Skelett (BMTR). Für DayZ-Blender-Plugin ggf. Knochen-Mapping anpassen.");
                                    txa.AppendLine();
                                    string rtmStem = Path.GetFileNameWithoutExtension(rtmPath);
                                    txa.AppendLine("$animation \"" + rtmStem + "\" {");
                                    txa.AppendLine(" #fps 30");
                                    txa.AppendLine(" #numFrames " + rtmFrameQuats.Count);
                                    int txaExported = 0;
                                    for (int bi = 0; bi < rtmBones.Count; bi++)
                                    {
                                        // Prüfen ob dieser Knochen in mindestens einem Frame nicht-Identität hat
                                        bool anyNonIdentity = false;
                                        for (int fi2 = 0; fi2 < rtmFrameQuats.Count; fi2++)
                                        {
                                            if (bi >= rtmFrameQuats[fi2].Length) continue;
                                            float[] fq = rtmFrameQuats[fi2][bi];
                                            if (fq == null) continue;
                                            if (Math.Abs(fq[0]) > 1e-4f || Math.Abs(fq[1]) > 1e-4f ||
                                                Math.Abs(fq[2]) > 1e-4f || Math.Abs(fq[3] - 1f) > 1e-3f)
                                            { anyNonIdentity = true; break; }
                                        }
                                        if (!anyNonIdentity) continue;

                                        txa.AppendLine(" $node \"" + rtmBones[bi] + "\" {");
                                        txa.AppendLine("   $keys t q s {");
                                        for (int fi2 = 0; fi2 < rtmFrameQuats.Count; fi2++)
                                        {
                                            if (bi >= rtmFrameQuats[fi2].Length || rtmFrameQuats[fi2][bi] == null) continue;
                                            float[] fq = rtmFrameQuats[fi2][bi];
                                            txa.AppendLine("     $frame " + fi2 + " {");
                                            txa.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                "       #q {0:F7} {1:F7} {2:F7} {3:F7}", fq[0], fq[1], fq[2], fq[3]));
                                            txa.AppendLine("     }");
                                        }
                                        txa.AppendLine("   }");
                                        txa.AppendLine(" }");
                                        txaExported++;
                                    }
                                    txa.AppendLine("}");
                                    txa.AppendLine("// " + txaExported + " von " + rtmBones.Count + " Knochen exportiert (Identität weggelassen)");
                                    rtmOutText = txa.ToString();
                                }
                                else
                                {
                                    rtmOutText = rtmReport.ToString();
                                }
                                File.WriteAllText(rtmSavePath, rtmOutText, new UTF8Encoding(false));
                                SetColor(ConsoleColor.Green);
                                Console.WriteLine("  [OK]  Gespeichert: " + rtmSavePath);
                                ResetColor();
                                Console.WriteLine();
                            }
                        }
                        break;
                    }
                }
            }

            // ── Weitermachen oder Beenden ─────────────────────────────────────
            Console.WriteLine();
            if (args.Length == 0 && !Console.IsInputRedirected)
            {
                SetColor(ConsoleColor.Cyan);
                Console.Write("  [W] Weiteres Werkzeug   [X] Beenden  →  ");
                ResetColor();
                char cont = ReadMenuKey('W', 'X');
                SetColor(cont == 'W' ? ConsoleColor.Green : ConsoleColor.DarkGray);
                Console.WriteLine(cont == 'W' ? "Weitermachen" : "Beenden");
                ResetColor();
                if (cont == 'X') break;
                try { Console.Clear(); } catch { }
            }
            else break;
            } // end while(true)
            return 0;
        }
    }
}
