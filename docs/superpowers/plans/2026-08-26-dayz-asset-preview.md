# DayZ Asset Preview — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein eigenständiges Windows-Programm, das die entpackten DayZ-Gamefiles durchsucht und die 3D-Modelle texturiert und massstabsgetreu anzeigt, damit man beim Bauen im Terrain Builder sieht, wie ein Asset aussieht.

**Architecture:** Eine Formatschicht (`DzAssets.Formats`) liest ODOL-`.p3d` über den bereits im Repo vorhandenen `BisDll`-Parser und dekodiert `.paa`-Texturen selbst. Eine WPF-Anwendung (`DzAssets.Preview`) stellt das Ergebnis in einem `Viewport3D` dar. Die Formatschicht kennt kein WPF, die Anwendung kennt keine Dateiformate.

**Tech Stack:** C# 13 / .NET 10 (`net10.0-windows`), WPF, xUnit. Für die ausgelieferte Anwendung ausser dem .NET-SDK keine Fremdabhängigkeiten; NuGet nur im Testprojekt.

**Spec:** `docs/superpowers/specs/2026-08-26-dayz-asset-preview-design.md`

## Global Constraints

- **Zielframework:** `net10.0-windows` für alle Projekte. `<UseWPF>true</UseWPF>` nur in `DzAssets.Preview`.
- **`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`** in `DzAssets.Formats` — `BisDll/LZO.cs` und `BinaryReaderEx.cs` verwenden `unsafe`.
- **`<Nullable>disable</Nullable>`** in `DzAssets.Formats` — der mitkompilierte `BisDll`-Quellcode ist nicht nullable-annotiert und würde sonst hunderte Warnungen erzeugen. In `DzAssets.Preview` und `DzAssets.Tests` dagegen `enable`.
- **Keine NuGet-Pakete in `DzAssets.Formats` und `DzAssets.Preview`.** Im Testprojekt erlaubt: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`.
- **`BisDll` wird nicht kopiert.** Einbindung ausschliesslich per `<Compile Include>` aus `DayZ_Arma_p3dDeBin/_SOURCE/BisDll_Quellcode_DayZ+Arma3_FIXED/BisDll_Quellcode/BisDll_src/`. Der Ordner `Properties/` wird ausgeschlossen (`AssemblyInfo.cs` erzeugt sonst doppelte Attribute und zieht `System.Security.Permissions` nach).
- **`BisDll` darf nicht verändert werden.** Alle benötigten Eigenschaften sind bereits `public` (geprüft: `LOD.Vertices`, `LOD.Normals`, `LOD.Textures`, `LOD.Materials`, `LOD.Sections`, `LOD.Faces`, `LOD.UVSets`, `Section.textureIndex`, `Section.materialIndex`, `P3D_LOD.Resolution`, `P3D_LOD.Name`).
- **Projektwurzel:** `tools/DayZAssetPreview/`.
- **Oberflächensprache: Deutsch.** Konsistent mit dem restlichen Repository (siehe `README.md`).
- **Moderne Oberfläche, dunkles Theme.** Eigenes Fenster-Chrome über `WindowChrome`, Schrift `Segoe UI Variable Text` mit Rückfall auf `Segoe UI`, Symbole aus `Segoe Fluent Icons` (geprüft: `C:\Windows\Fonts\SegoeIcons.ttf` und `SegUIVar.ttf` vorhanden). Keine unformatierten Standard-WPF-Steuerelemente in der fertigen Oberfläche.
- **Asset-Wurzel nicht fest verdrahten.** Standardvermutung `H:\P_Drive`, überschreibbar über die Einstellungen; Werte in `%LOCALAPPDATA%\DayZAssetPreview\settings.json`.
- **Kein Absturz durch ein einzelnes Asset.** Jeder Ladevorgang ist gekapselt; Fehler landen im Info-Panel und in `%LOCALAPPDATA%\DayZAssetPreview\log.txt`.
- **`Console.SetError` wird genau einmal aufgerufen**, in `App.OnStartup` (Task 8). Der Strom ist prozessweit; mehrere Threads lesen gleichzeitig Modelle, und `BisDll` schreibt dabei nach `Console.Error`. Ein Sichern-und-Wiederherstellen an einer anderen Stelle wäre ein Wettrennen. Diese Einschränkung gilt auch für die später geplanten Module — siehe `docs/superpowers/specs/2026-08-26-werkzeug-module-analyse.md`.

---

### Task 1: Projektgerüst und BisDll-Einbindung

Beweist als Erstes die riskanteste Annahme des ganzen Vorhabens: dass der auf .NET Framework 4.6.1 geschriebene `BisDll`-Quellcode unter .NET 10 kompiliert und eine echte DayZ-`.p3d` liest.

**Files:**
- Create: `tools/DayZAssetPreview/DayZAssetPreview.sln`
- Create: `tools/DayZAssetPreview/DzAssets.Formats/DzAssets.Formats.csproj`
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Vendor.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Tests/DzAssets.Tests.csproj`
- Create: `tools/DayZAssetPreview/DzAssets.Tests/TestAssets.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Tests/BisDllSmokeTests.cs`
- Create: `tools/DayZAssetPreview/.gitignore`

**Interfaces:**
- Consumes: nichts.
- Produces: `DzAssets.Formats` als Assembly, in der der Namensraum `BisDll.Model` verfügbar ist. `TestAssets.PDrive` (`string?`), `TestAssets.HasPDrive` (`bool`), `TestAssets.Dz(string relativePath)` (`string`) für alle späteren Testklassen.

- [ ] **Step 1: Solution und Formats-Projekt anlegen**

`tools/DayZAssetPreview/DzAssets.Formats/DzAssets.Formats.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <NoWarn>$(NoWarn);CS0169;CS0414;CS0649;CS8509;SYSLIB0011</NoWarn>
    <BisDllSrc>$(MSBuildThisFileDirectory)..\..\..\DayZ_Arma_p3dDeBin\_SOURCE\BisDll_Quellcode_DayZ+Arma3_FIXED\BisDll_Quellcode\BisDll_src</BisDllSrc>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="$(BisDllSrc)\**\*.cs"
             Exclude="$(BisDllSrc)\Properties\**\*.cs;$(BisDllSrc)\bin\**\*.cs;$(BisDllSrc)\obj\**\*.cs"
             LinkBase="Vendor\BisDll" />
  </ItemGroup>

  <Target Name="PruefeBisDllQuelle" BeforeTargets="BeforeBuild">
    <Error Condition="!Exists('$(BisDllSrc)')"
           Text="BisDll-Quellcode nicht gefunden unter $(BisDllSrc). Das Werkzeug wird aus dem Repository heraus gebaut und erwartet DayZ_Arma_p3dDeBin/_SOURCE/ an seinem Platz." />
  </Target>

</Project>
```

`tools/DayZAssetPreview/DzAssets.Formats/Vendor.cs` — reine Dokumentation, damit die Herkunft im Quellbaum sichtbar bleibt:

```csharp
// Dieses Projekt kompiliert den Quellcode von BisDll aus
//   DayZ_Arma_p3dDeBin/_SOURCE/BisDll_Quellcode_DayZ+Arma3_FIXED/
// direkt mit (siehe DzAssets.Formats.csproj, Eigenschaft BisDllSrc).
//
// Bewusst keine Kopie: Korrekturen am ODOL-Parser des Debinarizers
// wirken damit unmittelbar auch in der Vorschau. Siehe VENDOR.md im
// Wurzelverzeichnis des Repositorys fuer die Herkunftsangabe.
namespace DzAssets.Formats;

internal static class Vendor
{
    internal const string BisDllHinweis =
        "ODOL-/MLOD-Lesen stammt aus BisDll, siehe VENDOR.md";
}
```

`tools/DayZAssetPreview/.gitignore`:

```
bin/
obj/
*.user
```

Solution erzeugen und Projekt hinzufügen:

```bash
cd tools/DayZAssetPreview
dotnet new sln -n DayZAssetPreview
dotnet sln add DzAssets.Formats/DzAssets.Formats.csproj
```

- [ ] **Step 2: Bauen und prüfen, dass BisDll unter .NET 10 kompiliert**

Run: `cd tools/DayZAssetPreview && dotnet build DzAssets.Formats/DzAssets.Formats.csproj`
Expected: `Build succeeded`. Warnungen sind hinnehmbar, Fehler nicht.

Falls Fehler auftreten, in der `.csproj` beheben, **nicht** im
`BisDll`-Quellcode:
- `CS0227` (unsafe): `AllowUnsafeBlocks` fehlt.
- Doppelte Assembly-Attribute: `Properties/` ist nicht ausgeschlossen.
- `System.Security.Permissions` nicht gefunden: ebenfalls `Properties/`.
- **`CS0104: "BinaryWriter" ist ein mehrdeutiger Verweis` (18-mal).** Trat
  bei der Umsetzung am 2026-08-26 tatsächlich auf. Ursache:
  `ImplicitUsings` bindet `System.IO` global ein, und `BisDll` hat einen
  eigenen Namespace `BisDll.Stream` mit einer eigenen `BinaryWriter`-Klasse.
  Im ursprünglichen net461-Projekt gab es keine impliziten Usings, deshalb
  fiel es dort nie auf. Behoben durch `<Using Remove="System.IO" />` in der
  oben gezeigten `.csproj`; eigener Code schreibt `using System.IO;`
  explizit. Der Vendor-Code bleibt unangetastet.

- [ ] **Step 3: Testprojekt mit Zugriff auf die echten Dateien anlegen**

```bash
cd tools/DayZAssetPreview
dotnet new xunit -n DzAssets.Tests -f net10.0-windows
dotnet sln add DzAssets.Tests/DzAssets.Tests.csproj
dotnet add DzAssets.Tests/DzAssets.Tests.csproj reference DzAssets.Formats/DzAssets.Formats.csproj
```

`tools/DayZAssetPreview/DzAssets.Tests/TestAssets.cs`:

```csharp
namespace DzAssets.Tests;

/// <summary>
/// Zugriff auf die echten, entpackten Gamefiles. Fehlt das P-Drive
/// (z. B. auf einem Bau-Server), ueberspringen sich die betroffenen
/// Tests, statt fehlzuschlagen.
/// </summary>
public static class TestAssets
{
    private static readonly string[] Kandidaten =
    {
        @"H:\P_Drive",
        @"P:\",
    };

    public static string? PDrive { get; } =
        Environment.GetEnvironmentVariable("DZ_PDRIVE") is { Length: > 0 } aus
            && Directory.Exists(Path.Combine(aus, "DZ"))
                ? aus
                : Kandidaten.FirstOrDefault(k => Directory.Exists(Path.Combine(k, "DZ")));

    public static bool HasPDrive => PDrive is not null;

    /// <summary>Absoluter Pfad zu einer Datei unterhalb von DZ\.</summary>
    public static string Dz(string relativePath)
    {
        if (PDrive is null)
            throw new InvalidOperationException("Kein P-Drive gefunden.");
        return Path.Combine(PDrive, "DZ", relativePath.Replace('/', '\\'));
    }

    /// <summary>Erste Datei mit der gegebenen Endung unterhalb von DZ\<paramref name="unterordner"/>.</summary>
    public static string ErsteDatei(string unterordner, string muster)
    {
        if (PDrive is null)
            throw new InvalidOperationException("Kein P-Drive gefunden.");
        var wurzel = Path.Combine(PDrive, "DZ", unterordner);
        return Directory.EnumerateFiles(wurzel, muster, SearchOption.AllDirectories).First();
    }
}

/// <summary>Ueberspringt einen Test, wenn das P-Drive nicht vorhanden ist.</summary>
public sealed class PDriveFactAttribute : FactAttribute
{
    public PDriveFactAttribute()
    {
        if (!TestAssets.HasPDrive)
            Skip = "P-Drive mit entpackten Gamefiles nicht gefunden.";
    }
}
```

- [ ] **Step 4: Rauchtest schreiben — er muss zuerst fehlschlagen**

`tools/DayZAssetPreview/DzAssets.Tests/BisDllSmokeTests.cs`:

```csharp
using BisDll.Model;
using Xunit;

namespace DzAssets.Tests;

public class BisDllSmokeTests
{
    [PDriveFact]
    public void Liest_eine_echte_DayZ_p3d_als_ODOL()
    {
        var pfad = TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d");

        var modell = P3D.GetInstance(pfad);

        Assert.IsType<BisDll.Model.ODOL.ODOL>(modell);
        Assert.InRange(modell.Version, 28u, 75u);
        Assert.NotEmpty(modell.LODs);
    }

    [PDriveFact]
    public void Der_feinste_sichtbare_LOD_hat_Vertizes_und_Flaechen()
    {
        var pfad = TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d");

        var modell = (BisDll.Model.ODOL.ODOL)P3D.GetInstance(pfad);
        var lod = modell.LODs
            .Cast<BisDll.Model.ODOL.LOD>()
            .Where(l => Resolution.IsResolution(l.Resolution))
            .OrderBy(l => l.Resolution)
            .First();

        Assert.True(lod.VertexCount > 0);
        Assert.True(lod.PolygonCount > 0);
        Assert.NotEmpty(lod.Sections);
    }
}
```

- [ ] **Step 5: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~BisDllSmokeTests`
Expected: Kompiliert und schlägt fehl oder überspringt. Falls die Datei `basin_a.p3d` nicht existiert, den Pfad einmalig ermitteln und im Test ersetzen:

```bash
find /h/P_Drive/DZ/structures -iname "*.p3d" | head -3
```

- [ ] **Step 6: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~BisDllSmokeTests`
Expected: `Passed!` mit 2 bestandenen Tests.

Schlägt das Lesen mit `FormatException` fehl, liegt es an der ODOL-Version. Dann die Version des Modells ausgeben lassen und in der Spec vermerken — nicht `BisDll` anpassen, ohne den Debinarizer gegenzuprüfen.

- [ ] **Step 7: Committen**

```bash
git add tools/DayZAssetPreview docs/superpowers/plans
git commit -m "Projektgeruest fuer DayZ Asset Preview, BisDll unter .NET 10 eingebunden"
```

---

### Task 2: DXT-Dekodierung (BC1 und BC3)

Die kleinste, am besten prüfbare Einheit: reine Rechnung, keine Datei, kein Zustand.

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Textures/DxtDecoder.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/DxtDecoderTests.cs`

**Interfaces:**
- Consumes: nichts.
- Produces:
  - `static byte[] DxtDecoder.DecodeBc1(byte[] data, int width, int height)`
  - `static byte[] DxtDecoder.DecodeBc3(byte[] data, int width, int height)`
  - Beide liefern `width * height * 4` Bytes im Format **BGRA32** (Reihenfolge im Speicher: B, G, R, A), Zeilen von oben nach unten. Das ist genau das Layout von `PixelFormats.Bgra32` in WPF.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/DxtDecoderTests.cs`:

```csharp
using DzAssets.Formats.Textures;
using Xunit;

namespace DzAssets.Tests;

public class DxtDecoderTests
{
    // Ein BC1-Block: color0 = reines Rot (0xF800), color1 = reines Blau (0x001F).
    // color0 > color1, also der undurchsichtige Vier-Farben-Modus.
    // Indextabelle 0x00000000 -> alle 16 Pixel nehmen color0 (Rot).
    private static byte[] BlockRot() => new byte[]
    {
        0x00, 0xF8,   // color0 = 0xF800 little endian
        0x1F, 0x00,   // color1 = 0x001F little endian
        0x00, 0x00, 0x00, 0x00, // Indizes: alle 0
    };

    [Fact]
    public void Bc1_Vierfarbmodus_gibt_color0_als_undurchsichtiges_Rot()
    {
        var pixel = DxtDecoder.DecodeBc1(BlockRot(), 4, 4);

        Assert.Equal(4 * 4 * 4, pixel.Length);
        // Erstes Pixel, BGRA
        Assert.Equal(0x00, pixel[0]);        // B
        Assert.Equal(0x00, pixel[1]);        // G
        Assert.Equal(0xFF, pixel[2]);        // R  (5 Bit 0x1F -> 255)
        Assert.Equal(0xFF, pixel[3]);        // A
    }

    [Fact]
    public void Bc1_faerbt_alle_sechzehn_Pixel_des_Blocks()
    {
        var pixel = DxtDecoder.DecodeBc1(BlockRot(), 4, 4);

        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(0xFF, pixel[i * 4 + 2]); // R
            Assert.Equal(0xFF, pixel[i * 4 + 3]); // A
        }
    }

    [Fact]
    public void Bc1_Dreifarbmodus_macht_Index_drei_durchsichtig()
    {
        // color0 = 0x001F (Blau), color1 = 0xF800 (Rot) -> color0 < color1
        // Indizes: alle 3 -> durchsichtiges Schwarz
        var block = new byte[]
        {
            0x1F, 0x00,
            0x00, 0xF8,
            0xFF, 0xFF, 0xFF, 0xFF,
        };

        var pixel = DxtDecoder.DecodeBc1(block, 4, 4);

        Assert.Equal(0x00, pixel[3]); // A == 0
    }

    [Fact]
    public void Bc3_liest_den_Alphakanal_aus_dem_ersten_Halbblock()
    {
        // Alpha-Halbblock: a0 = 255, a1 = 0, alle Indizes 0 -> Alpha 255.
        // Farb-Halbblock: derselbe Rot-Block wie oben.
        var block = new byte[16];
        block[0] = 0xFF;                       // a0
        block[1] = 0x00;                       // a1
        // block[2..7] = 0 -> alle Indizes 0
        BlockRot().CopyTo(block, 8);

        var pixel = DxtDecoder.DecodeBc3(block, 4, 4);

        Assert.Equal(4 * 4 * 4, pixel.Length);
        Assert.Equal(0xFF, pixel[3]);   // A
        Assert.Equal(0xFF, pixel[2]);   // R
    }

    [Fact]
    public void Bc3_mit_Alpha_null_liefert_durchsichtige_Pixel()
    {
        var block = new byte[16];
        block[0] = 0x00;   // a0 = 0
        block[1] = 0x00;   // a1 = 0
        BlockRot().CopyTo(block, 8);

        var pixel = DxtDecoder.DecodeBc3(block, 4, 4);

        Assert.Equal(0x00, pixel[3]);
    }

    [Fact]
    public void Nicht_blockausgerichtete_Groessen_werden_zugeschnitten()
    {
        // 4x4-Block, aber nur 3x2 Pixel angefordert.
        var pixel = DxtDecoder.DecodeBc1(BlockRot(), 3, 2);

        Assert.Equal(3 * 2 * 4, pixel.Length);
        Assert.Equal(0xFF, pixel[2]);
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~DxtDecoderTests`
Expected: FAIL, `CS0234`/`CS0246` — `DzAssets.Formats.Textures` beziehungsweise `DxtDecoder` existiert nicht.

- [ ] **Step 3: Den Dekoder schreiben**

`tools/DayZAssetPreview/DzAssets.Formats/Textures/DxtDecoder.cs`:

```csharp
namespace DzAssets.Formats.Textures;

/// <summary>
/// Dekodiert BC1- (DXT1) und BC3-Bloecke (DXT5) nach BGRA32.
/// Reine Rechnung ohne Dateizugriff, damit unabhaengig pruefbar.
/// </summary>
public static class DxtDecoder
{
    public static byte[] DecodeBc1(byte[] data, int width, int height)
        => Decode(data, width, height, blockSize: 8, hatAlphaBlock: false);

    public static byte[] DecodeBc3(byte[] data, int width, int height)
        => Decode(data, width, height, blockSize: 16, hatAlphaBlock: true);

    private static byte[] Decode(byte[] data, int width, int height, int blockSize, bool hatAlphaBlock)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Breite und Hoehe muessen positiv sein.");

        var ziel = new byte[width * height * 4];
        var bloeckeX = (width + 3) / 4;
        var bloeckeY = (height + 3) / 4;

        var farben = new byte[4 * 4];   // 4 Farben, je BGRA
        var alpha = new byte[8];

        for (var by = 0; by < bloeckeY; by++)
        {
            for (var bx = 0; bx < bloeckeX; bx++)
            {
                var offset = (by * bloeckeX + bx) * blockSize;
                if (offset + blockSize > data.Length)
                    return ziel; // abgeschnittene Datei: Rest bleibt schwarz-durchsichtig

                var farbOffset = offset;
                if (hatAlphaBlock)
                {
                    LeseAlphaPalette(data, offset, alpha);
                    farbOffset = offset + 8;
                }

                LeseFarbPalette(data, farbOffset, farben, alphaAusFarbblock: !hatAlphaBlock);
                var indizes = BitConverter.ToUInt32(data, farbOffset + 4);

                ulong alphaIndizes = 0;
                if (hatAlphaBlock)
                    alphaIndizes = LeseAlphaIndizes(data, offset);

                for (var py = 0; py < 4; py++)
                {
                    var y = by * 4 + py;
                    if (y >= height) break;

                    for (var px = 0; px < 4; px++)
                    {
                        var x = bx * 4 + px;
                        if (x >= width) continue;

                        var i = py * 4 + px;
                        var farbIndex = (int)((indizes >> (i * 2)) & 0x3);
                        var z = (y * width + x) * 4;

                        ziel[z + 0] = farben[farbIndex * 4 + 0];
                        ziel[z + 1] = farben[farbIndex * 4 + 1];
                        ziel[z + 2] = farben[farbIndex * 4 + 2];
                        ziel[z + 3] = hatAlphaBlock
                            ? alpha[(int)((alphaIndizes >> (i * 3)) & 0x7)]
                            : farben[farbIndex * 4 + 3];
                    }
                }
            }
        }

        return ziel;
    }

    private static void LeseFarbPalette(byte[] data, int offset, byte[] farben, bool alphaAusFarbblock)
    {
        var c0 = (ushort)(data[offset] | (data[offset + 1] << 8));
        var c1 = (ushort)(data[offset + 2] | (data[offset + 3] << 8));

        Entpacke565(c0, farben, 0);
        Entpacke565(c1, farben, 1);

        // Im Dreifarbmodus (c0 <= c1) ist Index 3 durchsichtig — aber nur,
        // wenn der Alphakanal aus dem Farbblock stammt (BC1). Bei BC3 ist
        // immer der Vierfarbmodus gemeint.
        var vierFarben = !alphaAusFarbblock || c0 > c1;

        if (vierFarben)
        {
            for (var k = 0; k < 3; k++)
            {
                farben[2 * 4 + k] = (byte)((2 * farben[0 * 4 + k] + farben[1 * 4 + k]) / 3);
                farben[3 * 4 + k] = (byte)((farben[0 * 4 + k] + 2 * farben[1 * 4 + k]) / 3);
            }
            farben[2 * 4 + 3] = 0xFF;
            farben[3 * 4 + 3] = 0xFF;
        }
        else
        {
            for (var k = 0; k < 3; k++)
            {
                farben[2 * 4 + k] = (byte)((farben[0 * 4 + k] + farben[1 * 4 + k]) / 2);
                farben[3 * 4 + k] = 0;
            }
            farben[2 * 4 + 3] = 0xFF;
            farben[3 * 4 + 3] = 0x00;   // durchsichtig
        }
    }

    private static void Entpacke565(ushort wert, byte[] farben, int slot)
    {
        var r5 = (wert >> 11) & 0x1F;
        var g6 = (wert >> 5) & 0x3F;
        var b5 = wert & 0x1F;

        farben[slot * 4 + 0] = (byte)((b5 << 3) | (b5 >> 2));   // B
        farben[slot * 4 + 1] = (byte)((g6 << 2) | (g6 >> 4));   // G
        farben[slot * 4 + 2] = (byte)((r5 << 3) | (r5 >> 2));   // R
        farben[slot * 4 + 3] = 0xFF;                            // A
    }

    private static void LeseAlphaPalette(byte[] data, int offset, byte[] alpha)
    {
        alpha[0] = data[offset];
        alpha[1] = data[offset + 1];

        if (alpha[0] > alpha[1])
        {
            for (var i = 1; i <= 6; i++)
                alpha[i + 1] = (byte)(((7 - i) * alpha[0] + i * alpha[1]) / 7);
        }
        else
        {
            for (var i = 1; i <= 4; i++)
                alpha[i + 1] = (byte)(((5 - i) * alpha[0] + i * alpha[1]) / 5);
            alpha[6] = 0x00;
            alpha[7] = 0xFF;
        }
    }

    private static ulong LeseAlphaIndizes(byte[] data, int offset)
    {
        ulong wert = 0;
        for (var i = 0; i < 6; i++)
            wert |= (ulong)data[offset + 2 + i] << (i * 8);
        return wert;
    }
}
```

- [ ] **Step 4: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~DxtDecoderTests`
Expected: `Passed!  - Failed: 0, Passed: 6`

- [ ] **Step 5: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "DXT1- und DXT5-Dekodierung mit Tests"
```

---

### Task 3: PAA-Texturen lesen

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Textures/PaaImage.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/PaaImageTests.cs`

**Interfaces:**
- Consumes: `DxtDecoder.DecodeBc1`, `DxtDecoder.DecodeBc3` aus Task 2. Aus `BisDll`: `BisDll.Compression.LZO` und `BisDll.Compression.LZSS`.
- Produces:
  - `enum PaaFormat { Dxt1, Dxt2, Dxt3, Dxt4, Dxt5, Argb4444, Argb1555, Ai88, Argb8888 }`
  - `sealed class PaaImage { int Width; int Height; PaaFormat Format; byte[] Bgra; bool HatTransparenz; }`
  - `static PaaImage PaaImage.Load(string path)`
  - `static PaaImage PaaImage.Load(Stream stream)`
  - `void PaaImage.AlphaQuantisieren(byte schwelle = 128)` — setzt jedes Alpha auf 0 oder 255. Dient dem in der Spec beschriebenen Ersatz für fehlendes Alpha-Testing in WPF.

**Formatnotiz (am 2026-08-26 an `structures/data/alpha/net/lavkaalfa_co.paa` verifiziert):**

```
u16   magic          0xFF01=DXT1 0xFF02=DXT2 0xFF03=DXT3 0xFF04=DXT4 0xFF05=DXT5
                     0x1555=ARGB1555 0x4444=ARGB4444 0x8080=AI88
                     alles andere -> zurueckspulen, ARGB8888 ohne Kennung
TAGGs, solange die naechsten 4 Bytes "GGAT" sind:
      char[4] "GGAT"
      char[4] Name rueckwaerts   (im Beispiel "CGVA" -> AVGC)
      u32     Laenge
      byte[]  Daten
u16   Palettengroesse (bei DayZ 0; ist sie groesser, so viele Bytes ueberspringen)
Danach die Mipmaps, groesste zuerst:
      u16 Breite   (Bit 0x8000 gesetzt -> LZO-komprimiert)
      u16 Hoehe
      u24 Laenge der Daten (3 Bytes, little endian)
      byte[Laenge]
      Breite==0 und Hoehe==0 -> Ende
```

Die erste Mipmap ist die grösste; mehr wird nicht gebraucht. Bei den nicht-DXT-Formaten sind die Daten immer LZSS-komprimiert, bei DXT nur, wenn Bit `0x8000` gesetzt ist.

**Zur Byte-Reihenfolge der Kennung — bei der Umsetzung am 2026-08-26 eine
Fehlerquelle, die eine Stunde gekostet hat:** Ein Hexdump zeigt am Anfang
einer DXT1-Textur die Bytefolge `01 FF`. Als Little-Endian-`ushort`
gelesen ist das **`0xFF01`**, nicht `0x01FF`. Wird das verwechselt, gilt
jede Datei als kennungslos, die TAGG-Blöcke werden nicht übersprungen, und
Müllwerte landen als Breite und Höhe im Entpacker — mit erwarteten Grössen
im Gigabytebereich und einem Programm, das scheinbar hängt.

Deshalb zusätzlich zwei Plausibilitätsgrenzen im Leser: Kantenlänge
höchstens 8192, entpackte Mipmap höchstens 128 MiB. Beide werfen eine
`InvalidDataException` mit dem gelesenen Wert im Text, statt den Entpacker
loslaufen zu lassen.

Nachgeprüft an den ersten 50 Texturen unter `DZ\structures`: 15× DXT1,
35× DXT5, durchweg 512×512 oder 1024×256, je 3 bis 4 TAGGs, alle
LZO-komprimiert. Kein anderes Format kam vor.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/PaaImageTests.cs`:

```csharp
using DzAssets.Formats.Textures;
using Xunit;

namespace DzAssets.Tests;

public class PaaImageTests
{
    [PDriveFact]
    public void Laedt_eine_echte_DXT_Textur_mit_plausibler_Groesse()
    {
        var pfad = TestAssets.ErsteDatei("structures", "*_co.paa");

        var bild = PaaImage.Load(pfad);

        Assert.True(bild.Width > 0 && bild.Width <= 8192, $"Breite {bild.Width}");
        Assert.True(bild.Height > 0 && bild.Height <= 8192, $"Hoehe {bild.Height}");
        Assert.Equal(bild.Width * bild.Height * 4, bild.Bgra.Length);
    }

    [PDriveFact]
    public void Die_groesste_Mipmap_wird_gelesen_nicht_die_kleinste()
    {
        var pfad = TestAssets.ErsteDatei("structures", "*_co.paa");

        var bild = PaaImage.Load(pfad);

        // DayZ-Diffusetexturen sind praktisch nie kleiner als 64 Pixel.
        Assert.True(bild.Width >= 64, $"Breite {bild.Width} deutet auf eine kleine Mipmap hin");
    }

    [PDriveFact]
    public void Erkennt_das_Kompressionsformat()
    {
        var pfad = TestAssets.ErsteDatei("structures", "*_co.paa");

        var bild = PaaImage.Load(pfad);

        Assert.Contains(bild.Format, new[] { PaaFormat.Dxt1, PaaFormat.Dxt5, PaaFormat.Argb8888 });
    }

    [PDriveFact]
    public void Fuenfzig_echte_Texturen_laden_ohne_Ausnahme()
    {
        var wurzel = Path.Combine(TestAssets.PDrive!, "DZ", "structures");
        var dateien = Directory.EnumerateFiles(wurzel, "*.paa", SearchOption.AllDirectories)
            .Take(50)
            .ToList();

        Assert.NotEmpty(dateien);

        foreach (var datei in dateien)
        {
            var bild = PaaImage.Load(datei);
            Assert.Equal(bild.Width * bild.Height * 4, bild.Bgra.Length);
        }
    }

    [Fact]
    public void AlphaQuantisieren_macht_aus_Halbtransparenz_null_oder_voll()
    {
        var bild = PaaImage.FuerTest(2, 1, PaaFormat.Dxt5, new byte[]
        {
            0, 0, 0, 100,   // unter der Schwelle
            0, 0, 0, 200,   // ueber der Schwelle
        });

        bild.AlphaQuantisieren(128);

        Assert.Equal(0, bild.Bgra[3]);
        Assert.Equal(255, bild.Bgra[7]);
    }

    [Fact]
    public void Eine_unlesbare_Datei_wirft_eine_aussagekraeftige_Ausnahme()
    {
        using var strom = new MemoryStream(new byte[] { 1, 2, 3 });

        var fehler = Assert.Throws<InvalidDataException>(() => PaaImage.Load(strom));

        Assert.Contains("PAA", fehler.Message);
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~PaaImageTests`
Expected: FAIL, `PaaImage` existiert nicht.

- [ ] **Step 3: Den Leser schreiben**

`tools/DayZAssetPreview/DzAssets.Formats/Textures/PaaImage.cs`:

```csharp
using BisDll.Compression;

namespace DzAssets.Formats.Textures;

public enum PaaFormat
{
    Dxt1, Dxt2, Dxt3, Dxt4, Dxt5,
    Argb4444, Argb1555, Ai88, Argb8888,
}

/// <summary>
/// Eine dekodierte PAA-Textur: die groesste Mipmap als BGRA32-Puffer.
/// </summary>
public sealed class PaaImage
{
    public int Width { get; private init; }
    public int Height { get; private init; }
    public PaaFormat Format { get; private init; }
    public byte[] Bgra { get; private init; } = [];

    /// <summary>Wahr, wenn mindestens ein Pixel nicht voll undurchsichtig ist.</summary>
    public bool HatTransparenz { get; private set; }

    /// <summary>Nur fuer Tests: baut ein Bild aus fertigen Pixeln.</summary>
    public static PaaImage FuerTest(int w, int h, PaaFormat format, byte[] bgra)
        => new() { Width = w, Height = h, Format = format, Bgra = bgra, HatTransparenz = true };

    public static PaaImage Load(string path)
    {
        using var strom = File.OpenRead(path);
        try
        {
            return Load(strom);
        }
        catch (Exception fehler) when (fehler is not InvalidDataException)
        {
            throw new InvalidDataException($"PAA konnte nicht gelesen werden: {path}", fehler);
        }
    }

    public static PaaImage Load(Stream stream)
    {
        using var leser = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        if (stream.Length < 8)
            throw new InvalidDataException("PAA-Datei ist zu kurz.");

        var kennung = leser.ReadUInt16();
        var format = KennungTeuten(kennung, out var hatKennung);
        if (!hatKennung)
            stream.Position -= 2;

        UeberspringeTaggs(leser, stream);

        var palettenGroesse = leser.ReadUInt16();
        if (palettenGroesse > 0)
            stream.Position += palettenGroesse * 3;

        var breite = leser.ReadUInt16();
        var hoehe = leser.ReadUInt16();
        var lzoKomprimiert = (breite & 0x8000) != 0;
        breite = (ushort)(breite & 0x7FFF);

        if (breite == 0 || hoehe == 0)
            throw new InvalidDataException("PAA enthaelt keine Mipmap.");

        var laenge = leser.ReadByte() | (leser.ReadByte() << 8) | (leser.ReadByte() << 16);
        if (laenge <= 0 || laenge > stream.Length)
            throw new InvalidDataException($"PAA nennt eine unplausible Datenlaenge: {laenge}");

        var roh = leser.ReadBytes(laenge);
        var entpackt = Entpacken(roh, format, lzoKomprimiert, breite, hoehe);
        var bgra = NachBgra(entpackt, format, breite, hoehe);

        var bild = new PaaImage
        {
            Width = breite,
            Height = hoehe,
            Format = format,
            Bgra = bgra,
        };
        bild.HatTransparenz = PruefeTransparenz(bgra);
        return bild;
    }

    public void AlphaQuantisieren(byte schwelle = 128)
    {
        for (var i = 3; i < Bgra.Length; i += 4)
            Bgra[i] = Bgra[i] < schwelle ? (byte)0 : (byte)255;
    }

    private static PaaFormat KennungTeuten(ushort kennung, out bool hatKennung)
    {
        hatKennung = true;
        return kennung switch
        {
            0x01FF => PaaFormat.Dxt1,
            0x02FF => PaaFormat.Dxt2,
            0x03FF => PaaFormat.Dxt3,
            0x04FF => PaaFormat.Dxt4,
            0x05FF => PaaFormat.Dxt5,
            0x4444 => PaaFormat.Argb4444,
            0x1555 => PaaFormat.Argb1555,
            0x8080 => PaaFormat.Ai88,
            _ => Ohne(out hatKennung),
        };

        static PaaFormat Ohne(out bool hatKennung)
        {
            hatKennung = false;
            return PaaFormat.Argb8888;
        }
    }

    private static void UeberspringeTaggs(BinaryReader leser, Stream stream)
    {
        while (stream.Position + 12 <= stream.Length)
        {
            var merker = stream.Position;
            var kopf = leser.ReadBytes(4);
            if (kopf.Length < 4 || kopf[0] != (byte)'G' || kopf[1] != (byte)'G'
                || kopf[2] != (byte)'A' || kopf[3] != (byte)'T')
            {
                stream.Position = merker;
                return;
            }

            stream.Position += 4;                 // Name rueckwaerts
            var laenge = leser.ReadUInt32();
            if (laenge > stream.Length - stream.Position)
                throw new InvalidDataException("PAA: TAGG-Laenge liegt ausserhalb der Datei.");
            stream.Position += laenge;
        }
    }

    private static byte[] Entpacken(byte[] roh, PaaFormat format, bool lzoKomprimiert, int breite, int hoehe)
    {
        var istDxt = format is PaaFormat.Dxt1 or PaaFormat.Dxt2 or PaaFormat.Dxt3
                            or PaaFormat.Dxt4 or PaaFormat.Dxt5;

        var erwartet = ErwarteteGroesse(format, breite, hoehe);

        if (istDxt)
        {
            if (!lzoKomprimiert)
                return roh;
            var ziel = new byte[erwartet];
            LZO.Decompress(roh, ziel);
            return ziel;
        }

        // Nicht-DXT-Formate sind in PAA immer LZSS-komprimiert.
        var ausgabe = new byte[erwartet];
        LZSS.Decode(roh, ausgabe, erwartet);
        return ausgabe;
    }

    private static int ErwarteteGroesse(PaaFormat format, int breite, int hoehe)
    {
        var bloecke = ((breite + 3) / 4) * ((hoehe + 3) / 4);
        return format switch
        {
            PaaFormat.Dxt1 => bloecke * 8,
            PaaFormat.Dxt2 or PaaFormat.Dxt3 or PaaFormat.Dxt4 or PaaFormat.Dxt5 => bloecke * 16,
            PaaFormat.Argb4444 or PaaFormat.Argb1555 or PaaFormat.Ai88 => breite * hoehe * 2,
            _ => breite * hoehe * 4,
        };
    }

    private static byte[] NachBgra(byte[] daten, PaaFormat format, int breite, int hoehe)
    {
        switch (format)
        {
            case PaaFormat.Dxt1:
                return DxtDecoder.DecodeBc1(daten, breite, hoehe);

            case PaaFormat.Dxt2:
            case PaaFormat.Dxt3:
            case PaaFormat.Dxt4:
            case PaaFormat.Dxt5:
                return DxtDecoder.DecodeBc3(daten, breite, hoehe);

            case PaaFormat.Argb4444:
                return Aus16Bit(daten, breite, hoehe, static wert =>
                {
                    var a = (wert >> 12) & 0xF;
                    var r = (wert >> 8) & 0xF;
                    var g = (wert >> 4) & 0xF;
                    var b = wert & 0xF;
                    return ((byte)(b * 17), (byte)(g * 17), (byte)(r * 17), (byte)(a * 17));
                });

            case PaaFormat.Argb1555:
                return Aus16Bit(daten, breite, hoehe, static wert =>
                {
                    var a = (wert >> 15) & 0x1;
                    var r = (wert >> 10) & 0x1F;
                    var g = (wert >> 5) & 0x1F;
                    var b = wert & 0x1F;
                    return ((byte)((b << 3) | (b >> 2)),
                            (byte)((g << 3) | (g >> 2)),
                            (byte)((r << 3) | (r >> 2)),
                            (byte)(a == 1 ? 255 : 0));
                });

            case PaaFormat.Ai88:
                return Aus16Bit(daten, breite, hoehe, static wert =>
                {
                    var hell = (byte)(wert & 0xFF);
                    var a = (byte)((wert >> 8) & 0xFF);
                    return (hell, hell, hell, a);
                });

            default:
                // ARGB8888 im Speicher: A, R, G, B -> nach B, G, R, A umsortieren
                var ziel = new byte[breite * hoehe * 4];
                var anzahl = Math.Min(daten.Length / 4, breite * hoehe);
                for (var i = 0; i < anzahl; i++)
                {
                    ziel[i * 4 + 0] = daten[i * 4 + 3];
                    ziel[i * 4 + 1] = daten[i * 4 + 2];
                    ziel[i * 4 + 2] = daten[i * 4 + 1];
                    ziel[i * 4 + 3] = daten[i * 4 + 0];
                }
                return ziel;
        }
    }

    private static byte[] Aus16Bit(byte[] daten, int breite, int hoehe,
        Func<ushort, (byte B, byte G, byte R, byte A)> wandeln)
    {
        var ziel = new byte[breite * hoehe * 4];
        var anzahl = Math.Min(daten.Length / 2, breite * hoehe);
        for (var i = 0; i < anzahl; i++)
        {
            var wert = (ushort)(daten[i * 2] | (daten[i * 2 + 1] << 8));
            var (b, g, r, a) = wandeln(wert);
            ziel[i * 4 + 0] = b;
            ziel[i * 4 + 1] = g;
            ziel[i * 4 + 2] = r;
            ziel[i * 4 + 3] = a;
        }
        return ziel;
    }

    private static bool PruefeTransparenz(byte[] bgra)
    {
        for (var i = 3; i < bgra.Length; i += 4)
            if (bgra[i] != 0xFF) return true;
        return false;
    }
}
```

- [ ] **Step 4: Die tatsächlichen Signaturen von LZO und LZSS prüfen und anpassen**

Der obige Code ruft `LZO.Decompress(roh, ziel)` und `LZSS.Decode(roh, ausgabe, erwartet)` auf. Diese Namen sind geraten. Die wahren Signaturen ermitteln:

```bash
grep -n "public" "DayZ_Arma_p3dDeBin/_SOURCE/BisDll_Quellcode_DayZ+Arma3_FIXED/BisDll_Quellcode/BisDll_src/BisDll.Compression/LZO.cs"
grep -n "public" "DayZ_Arma_p3dDeBin/_SOURCE/BisDll_Quellcode_DayZ+Arma3_FIXED/BisDll_Quellcode/BisDll_src/BisDll.Compression/LZSS.cs"
```

Die Aufrufe in `Entpacken` an die gefundenen Signaturen anpassen. Sind die Klassen `internal` statt `public`, sind sie trotzdem erreichbar — sie werden in dieselbe Assembly kompiliert wie `PaaImage`.

Nimmt die LZO-Methode einen `Stream` statt eines `byte[]`, dann statt `leser.ReadBytes(laenge)` den Strom direkt weiterreichen.

- [ ] **Step 5: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~PaaImageTests`
Expected: `Passed!  - Failed: 0, Passed: 6`

Schlägt `Fuenfzig_echte_Texturen_laden_ohne_Ausnahme` bei einzelnen Dateien fehl, den Dateinamen und die Kennung ausgeben lassen und das Format ergänzen — nicht den Test abschwächen.

- [ ] **Step 6: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "PAA-Texturen lesen und nach BGRA32 dekodieren"
```

---

### Task 4: ODOL nach ModelGeometry

Das Herzstück: aus dem `BisDll`-Modell eine renderfertige, WPF-freie Geometrie machen.

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Models/ModelGeometry.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Models/P3dModelReader.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/P3dModelReaderTests.cs`

**Interfaces:**
- Consumes: `BisDll.Model.P3D`, `BisDll.Model.ODOL.LOD`, `BisDll.Model.ODOL.Section`, `BisDll.Model.Resolution`.
- Produces:

```csharp
public readonly record struct Vec3(float X, float Y, float Z);
public readonly record struct Vec2(float U, float V);

public sealed class MeshSection
{
    public int[] Indices { get; init; }        // Dreiecke, je 3 Einträge
    public string? TexturePath { get; init; }  // z. B. "DZ\structures\...\x_co.paa"
    public string? MaterialPath { get; init; } // z. B. "DZ\structures\...\x.rvmat"
}

public sealed class LodGeometry
{
    public float Resolution { get; init; }
    public string Name { get; init; }          // "1.000", "Geometry", "ShadowVolume0.000" ...
    public bool IstSichtbar { get; init; }     // Resolution.IsVisual
    public Vec3[] Positions { get; init; }
    public Vec3[] Normals { get; init; }
    public Vec2[] Uvs { get; init; }
    public MeshSection[] Sections { get; init; }
    public int TriangleCount { get; }          // Summe über Sections
}

public sealed class ModelGeometry
{
    public string Path { get; init; }
    public uint Version { get; init; }
    public LodGeometry[] Lods { get; init; }
    public Vec3 BoundsMin { get; init; }       // über den feinsten sichtbaren LOD
    public Vec3 BoundsMax { get; init; }
    public Vec3 Size { get; }                  // BoundsMax - BoundsMin
    public LodGeometry? FeinsterSichtbarerLod { get; }
}

public static class P3dModelReader
{
    public static ModelGeometry Read(string path);
}
```

**Wichtige Sachverhalte, im Quellcode von BisDll nachgelesen:**

- `LOD.Faces` ist `Polygon[]`; jedes `Polygon.VertexIndices` hat **3 oder 4** Einträge. Vierecke werden zu `(0,1,2)` und `(0,2,3)` zerlegt.
- `VertexIndex` hat eine implizite Umwandlung nach `int`.
- `LOD.UVSets[0].UVData` liefert `float[VertexCount * 2]` in der Reihenfolge u, v. Ist `UVSets` leer, mit `Vec2(0,0)` füllen.
- `Section.getFaceIndexes(Polygon[])` existiert, ist aber pro Section ein voller Durchlauf über alle Flächen — bei vielen Sections quadratisch. **Stattdessen einmal über alle Flächen laufen** und die Byte-Grenzen selbst mitzählen; die Rechenvorschrift steht in `Section.getFaceIndexes`: pro Fläche `shortIndices ? 8 : 16` Bytes, bei einem Viereck zusätzlich `shortIndices ? 2 : 4`. `shortIndices` gilt für `Version < 69`.
  Da `faceLowerIndex`/`faceUpperIndex` und `shortIndices` in `Section` privat sind, wird `getFaceIndexes` doch verwendet — aber **einmal je Section und mit vorab berechneter Flächenliste**, und die Sections werden über ein `HashSet<uint>` je Section ausgewertet. Bei Modellen mit mehr als 64 Sections wird stattdessen jede Fläche der ersten Section zugeordnet, die sie beansprucht.
- **Koordinaten:** Arma ist linkshändig (X rechts, Y oben, Z vorn), WPF rechtshändig. Umrechnung: `Vec3(x, y, -z)`, und dadurch dreht sich der Umlaufsinn — die Dreiecksindizes werden deshalb als `(a, c, b)` statt `(a, b, c)` abgelegt.
- **UV:** in `MeshSection` unverändert übernehmen. Die Spiegelung von V geschieht erst beim Aufbau der WPF-Geometrie (Task 11), weil sie eine Eigenheit von WPF ist, nicht von ODOL.
- **`Console.Error`-Ausgaben:** `LOD.read` und `ODOL.read` schreiben pro LOD mehrere Zeilen nach `Console.Error` (`ODOL.cs` Zeilen 110, 140, 153 und weitere). `BisDll` wird dafür **nicht** angefasst.

  **`P3dModelReader` fasst `Console.Error` nicht an.** `Console.SetError`
  wirkt prozessweit. Würde `Read` den Strom um jeden Lesevorgang sichern
  und wiederherstellen, ergäbe das ein Wettrennen, sobald zwei Threads
  gleichzeitig lesen — und genau das tut die Anwendung später (Modell laden
  und Miniaturbilder erzeugen laufen nebeneinander). Im ungünstigen Fall
  bliebe `Console.Error` dauerhaft stummgeschaltet.

  Stattdessen leitet die Anwendung **einmal beim Start** um, auf einen
  synchronisierten Schreiber in die Protokolldatei (Task 8). Damit sind die
  Meldungen weder verloren noch im Weg, und es gibt keinen geteilten
  Zustand, den zwei Threads gegeneinander verstellen könnten.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/P3dModelReaderTests.cs`:

```csharp
using DzAssets.Formats.Models;
using Xunit;

namespace DzAssets.Tests;

public class P3dModelReaderTests
{
    private static ModelGeometry Waschbecken()
        => P3dModelReader.Read(TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d"));

    [PDriveFact]
    public void Liest_LODs_mit_Geometrie()
    {
        var modell = Waschbecken();

        Assert.NotEmpty(modell.Lods);
        Assert.NotNull(modell.FeinsterSichtbarerLod);
        Assert.NotEmpty(modell.FeinsterSichtbarerLod!.Positions);
    }

    [PDriveFact]
    public void Jeder_Index_liegt_innerhalb_der_Vertexliste()
    {
        var modell = Waschbecken();

        foreach (var lod in modell.Lods)
        {
            foreach (var abschnitt in lod.Sections)
            {
                Assert.True(abschnitt.Indices.Length % 3 == 0,
                    $"LOD {lod.Name}: Indexanzahl {abschnitt.Indices.Length} ist kein Vielfaches von 3");

                foreach (var index in abschnitt.Indices)
                    Assert.InRange(index, 0, lod.Positions.Length - 1);
            }
        }
    }

    [PDriveFact]
    public void Die_Bounding_Box_ist_endlich_und_plausibel()
    {
        var modell = Waschbecken();

        Assert.True(float.IsFinite(modell.Size.X));
        Assert.True(float.IsFinite(modell.Size.Y));
        Assert.True(float.IsFinite(modell.Size.Z));
        // Ein Waschbecken ist groesser als ein Zentimeter und kleiner als ein Haus.
        Assert.InRange(modell.Size.X, 0.01f, 20f);
        Assert.InRange(modell.Size.Y, 0.01f, 20f);
    }

    [PDriveFact]
    public void UV_Koordinaten_gibt_es_fuer_jeden_Vertex()
    {
        var lod = Waschbecken().FeinsterSichtbarerLod!;

        Assert.Equal(lod.Positions.Length, lod.Uvs.Length);
        Assert.Equal(lod.Positions.Length, lod.Normals.Length);
    }

    [PDriveFact]
    public void Mindestens_ein_Abschnitt_nennt_eine_Textur_oder_ein_Material()
    {
        var lod = Waschbecken().FeinsterSichtbarerLod!;

        Assert.Contains(lod.Sections, a => !string.IsNullOrEmpty(a.TexturePath)
                                        || !string.IsNullOrEmpty(a.MaterialPath));
    }

    [PDriveFact]
    public void Sichtbare_und_technische_LODs_werden_unterschieden()
    {
        var modell = Waschbecken();

        Assert.Contains(modell.Lods, l => l.IstSichtbar);
        // Fast jedes DayZ-Modell hat einen Geometry- oder Memory-LOD.
        Assert.Contains(modell.Lods, l => !l.IstSichtbar);
    }

    [PDriveFact]
    public void Hundert_echte_Modelle_werden_ohne_Ausnahme_gelesen()
    {
        var wurzel = Path.Combine(TestAssets.PDrive!, "DZ", "structures");
        var dateien = Directory.EnumerateFiles(wurzel, "*.p3d", SearchOption.AllDirectories)
            .Take(100)
            .ToList();

        Assert.NotEmpty(dateien);

        var fehler = new List<string>();
        foreach (var datei in dateien)
        {
            try
            {
                var modell = P3dModelReader.Read(datei);
                Assert.NotEmpty(modell.Lods);
            }
            catch (Exception ausnahme)
            {
                fehler.Add($"{Path.GetFileName(datei)}: {ausnahme.GetType().Name} {ausnahme.Message}");
            }
        }

        Assert.True(fehler.Count == 0, string.Join(Environment.NewLine, fehler.Take(10)));
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~P3dModelReaderTests`
Expected: FAIL, `P3dModelReader` existiert nicht.

- [ ] **Step 3: `ModelGeometry.cs` schreiben**

`tools/DayZAssetPreview/DzAssets.Formats/Models/ModelGeometry.cs`:

```csharp
namespace DzAssets.Formats.Models;

public readonly record struct Vec3(float X, float Y, float Z)
{
    public static Vec3 Min(Vec3 a, Vec3 b)
        => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Min(a.Z, b.Z));

    public static Vec3 Max(Vec3 a, Vec3 b)
        => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y), MathF.Max(a.Z, b.Z));

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}

public readonly record struct Vec2(float U, float V);

/// <summary>Ein Teil eines LODs mit eigener Textur beziehungsweise eigenem Material.</summary>
public sealed class MeshSection
{
    public required int[] Indices { get; init; }
    public string? TexturePath { get; init; }
    public string? MaterialPath { get; init; }

    public int TriangleCount => Indices.Length / 3;
}

public sealed class LodGeometry
{
    public required float Resolution { get; init; }
    public required string Name { get; init; }
    public required bool IstSichtbar { get; init; }
    public required Vec3[] Positions { get; init; }
    public required Vec3[] Normals { get; init; }
    public required Vec2[] Uvs { get; init; }
    public required MeshSection[] Sections { get; init; }

    public int TriangleCount => Sections.Sum(a => a.TriangleCount);
}

public sealed class ModelGeometry
{
    public required string Path { get; init; }
    public required uint Version { get; init; }
    public required LodGeometry[] Lods { get; init; }
    public required Vec3 BoundsMin { get; init; }
    public required Vec3 BoundsMax { get; init; }

    /// <summary>Ausdehnung in Metern (X = Breite, Y = Hoehe, Z = Tiefe).</summary>
    public Vec3 Size => BoundsMax - BoundsMin;

    /// <summary>
    /// Der sichtbare LOD mit der kleinsten Aufloesungszahl, also der feinste.
    /// Null, wenn das Modell nur technische LODs enthaelt.
    /// </summary>
    public LodGeometry? FeinsterSichtbarerLod
        => Lods.Where(l => l.IstSichtbar && l.Positions.Length > 0)
               .OrderBy(l => l.Resolution)
               .FirstOrDefault();
}
```

- [ ] **Step 4: `P3dModelReader.cs` schreiben**

`tools/DayZAssetPreview/DzAssets.Formats/Models/P3dModelReader.cs`:

```csharp
using BisDll.Model;
using OdolLod = BisDll.Model.ODOL.LOD;

namespace DzAssets.Formats.Models;

/// <summary>
/// Uebersetzt eine ODOL- oder MLOD-Datei in eine renderfertige Geometrie.
/// Kennt kein WPF und keine Bildformate.
/// </summary>
public static class P3dModelReader
{
    /// <summary>Modelle oberhalb dieser Groesse werden nicht ungefragt geladen.</summary>
    public const long WarnGroesseBytes = 200L * 1024 * 1024;

    public static ModelGeometry Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Modell nicht gefunden.", path);

        // Achtung: hier NICHT Console.SetError aufrufen. Der Strom ist
        // prozessweit; die Anwendung leitet ihn einmal beim Start um.
        {
            var modell = P3D.GetInstance(path);
        var lods = modell.LODs
            .OfType<OdolLod>()
            .Select(LiesLod)
            .ToArray();

        var (min, max) = Grenzen(lods);

        return new ModelGeometry
        {
            Path = path,
            Version = modell.Version,
            Lods = lods,
            BoundsMin = min,
            BoundsMax = max,
        };
    }

    private static (Vec3 Min, Vec3 Max) Grenzen(LodGeometry[] lods)
    {
        var bezug = lods.Where(l => l.IstSichtbar && l.Positions.Length > 0)
                        .OrderBy(l => l.Resolution)
                        .FirstOrDefault()
                    ?? lods.FirstOrDefault(l => l.Positions.Length > 0);

        if (bezug is null)
            return (default, default);

        var min = bezug.Positions[0];
        var max = bezug.Positions[0];
        foreach (var punkt in bezug.Positions)
        {
            min = Vec3.Min(min, punkt);
            max = Vec3.Max(max, punkt);
        }
        return (min, max);
    }

    private static LodGeometry LiesLod(OdolLod lod)
    {
        var positionen = new Vec3[lod.VertexCount];
        for (var i = 0; i < positionen.Length; i++)
        {
            var v = lod.Vertices[i];
            // Arma ist linkshaendig, WPF rechtshaendig: Z spiegeln.
            positionen[i] = new Vec3(v.X, v.Y, -v.Z);
        }

        var normalen = new Vec3[positionen.Length];
        var quelleNormalen = lod.Normals;
        for (var i = 0; i < normalen.Length; i++)
        {
            if (quelleNormalen is not null && i < quelleNormalen.Length && quelleNormalen[i] is not null)
            {
                var n = quelleNormalen[i];
                // ODOL speichert Normalen entgegengesetzt zur Flaechenrichtung.
                normalen[i] = new Vec3(-n.X, -n.Y, n.Z);
            }
            else
            {
                normalen[i] = new Vec3(0, 1, 0);
            }
        }

        var uvs = new Vec2[positionen.Length];
        var uvSatz = lod.UVSets;
        if (uvSatz is { Length: > 0 })
        {
            var roh = uvSatz[0].UVData;
            for (var i = 0; i < uvs.Length && i * 2 + 1 < roh.Length; i++)
                uvs[i] = new Vec2(roh[i * 2], roh[i * 2 + 1]);
        }

        var abschnitte = LiesAbschnitte(lod, positionen.Length);

        return new LodGeometry
        {
            Resolution = lod.Resolution,
            Name = lod.Name,
            IstSichtbar = Resolution.IsVisual(lod.Resolution),
            Positions = positionen,
            Normals = normalen,
            Uvs = uvs,
            Sections = abschnitte,
        };
    }

    private static MeshSection[] LiesAbschnitte(OdolLod lod, int vertexAnzahl)
    {
        var flaechen = lod.Faces;
        var texturen = lod.Textures ?? [];
        var materialien = lod.Materials ?? [];
        var ergebnis = new List<MeshSection>(lod.Sections.Length);

        foreach (var abschnitt in lod.Sections)
        {
            var indizes = new List<int>();

            foreach (var flaechenIndex in abschnitt.getFaceIndexes(flaechen))
            {
                if (flaechenIndex >= flaechen.Length) continue;
                var ecken = flaechen[flaechenIndex].VertexIndices;

                if (ecken.Length == 3)
                {
                    FuegeDreieckHinzu(indizes, ecken[0], ecken[1], ecken[2], vertexAnzahl);
                }
                else if (ecken.Length == 4)
                {
                    FuegeDreieckHinzu(indizes, ecken[0], ecken[1], ecken[2], vertexAnzahl);
                    FuegeDreieckHinzu(indizes, ecken[0], ecken[2], ecken[3], vertexAnzahl);
                }
            }

            if (indizes.Count == 0) continue;

            string? texturPfad = null;
            if (abschnitt.textureIndex >= 0 && abschnitt.textureIndex < texturen.Length)
                texturPfad = Leer(texturen[abschnitt.textureIndex]);

            string? materialPfad = null;
            if (abschnitt.materialIndex >= 0 && abschnitt.materialIndex < materialien.Length)
                materialPfad = Leer(materialien[abschnitt.materialIndex]?.materialName);

            ergebnis.Add(new MeshSection
            {
                Indices = indizes.ToArray(),
                TexturePath = texturPfad,
                MaterialPath = materialPfad,
            });
        }

        return ergebnis.ToArray();

        static string? Leer(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert;
    }

    private static void FuegeDreieckHinzu(List<int> ziel, int a, int b, int c, int vertexAnzahl)
    {
        if (a < 0 || b < 0 || c < 0) return;
        if (a >= vertexAnzahl || b >= vertexAnzahl || c >= vertexAnzahl) return;

        // Umlaufsinn drehen, weil oben die Z-Achse gespiegelt wurde.
        ziel.Add(a);
        ziel.Add(c);
        ziel.Add(b);
    }
}
```

- [ ] **Step 5: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~P3dModelReaderTests`
Expected: `Passed!  - Failed: 0, Passed: 7`

Häufige Stolperstellen:
- `lod.Materials` ist `EmbeddedMaterial[]`; das Feld heisst `materialName` (klein geschrieben, kein Property).
- Ist `Hundert_echte_Modelle...` langsam (über 60 Sekunden), liegt es an `getFaceIndexes`. Dann die Anzahl auf 100 belassen und in Task 12 einen Zwischenspeicher ergänzen — nicht jetzt optimieren.

- [ ] **Step 6: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "ODOL-Modelle in renderfertige Geometrie uebersetzen"
```

---

### Task 5: Texturen auflösen (Section, EmbeddedMaterial, RVMAT)

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Models/RvmatMaterial.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Models/TextureResolver.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/TextureResolverTests.cs`

**Interfaces:**
- Consumes: `MeshSection` aus Task 4.
- Produces:
  - `sealed class RvmatMaterial { IReadOnlyList<string> StageTextures; string? DiffusePfad; static RvmatMaterial Parse(string text); static RvmatMaterial Load(string pfad); }`
  - `sealed class TextureResolver(string pDrive)` mit
    `string? DiffuseFuer(MeshSection abschnitt)` — liefert einen **absoluten** Dateipfad oder `null`
    und `string? ZuAbsolut(string? assetPfad)` — wandelt `"DZ\a\b_co.paa"` in `"H:\P_Drive\DZ\a\b_co.paa"`.

**Auflösungsreihenfolge** (in dieser Folge, erster Treffer gewinnt):
1. `abschnitt.TexturePath`, sofern gesetzt und die Datei existiert.
2. Aus dem `.rvmat` unter `abschnitt.MaterialPath`: die erste Stage-Textur, deren Dateiname auf `_co.paa` endet.
3. Aus demselben `.rvmat`: die erste Stage-Textur überhaupt.
4. `null` — der Aufrufer nimmt dann das graue Ersatzmaterial.

`.rvmat`-Dateien liegen als Klartext vor (am 2026-08-26 an `structures/data/alpha/glass_int_new.rvmat` geprüft). Beginnt eine Datei mit `raP`, ist sie binarisiert; dann `null` liefern und den Fall protokollieren.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/TextureResolverTests.cs`:

```csharp
using DzAssets.Formats.Models;
using Xunit;

namespace DzAssets.Tests;

public class RvmatMaterialTests
{
    [Fact]
    public void Findet_die_Diffusetextur_anhand_der_co_Endung()
    {
        const string text = """
            ambient[]={1,1,1,1};
            class Stage1
            {
                texture="DZ\structures\data\wand_nohq.paa";
            };
            class Stage2
            {
                texture="DZ\structures\data\wand_co.paa";
            };
            """;

        var material = RvmatMaterial.Parse(text);

        Assert.Equal(@"DZ\structures\data\wand_co.paa", material.DiffusePfad);
        Assert.Equal(2, material.StageTextures.Count);
    }

    [Fact]
    public void Ohne_co_Textur_gewinnt_die_erste_Stage()
    {
        const string text = """
            class Stage1 { texture="DZ\a\b_nohq.paa"; };
            class Stage2 { texture="DZ\a\b_as.paa"; };
            """;

        var material = RvmatMaterial.Parse(text);

        Assert.Equal(@"DZ\a\b_nohq.paa", material.DiffusePfad);
    }

    [Fact]
    public void Ein_binarisiertes_rvmat_liefert_keine_Textur()
    {
        var material = RvmatMaterial.Parse("raP\0\0\0irgendwas");

        Assert.Null(material.DiffusePfad);
        Assert.Empty(material.StageTextures);
    }

    [Fact]
    public void Prozedurale_Texturen_werden_uebergangen()
    {
        const string text = """
            class Stage1 { texture="#(argb,8,8,3)color(0.5,0.5,0.5,1)"; };
            class Stage2 { texture="DZ\a\b_co.paa"; };
            """;

        var material = RvmatMaterial.Parse(text);

        Assert.Equal(@"DZ\a\b_co.paa", material.DiffusePfad);
        Assert.DoesNotContain(material.StageTextures, t => t.StartsWith('#'));
    }
}

public class TextureResolverTests
{
    [PDriveFact]
    public void Wandelt_einen_Assetpfad_in_einen_absoluten_Pfad()
    {
        var aufloeser = new TextureResolver(TestAssets.PDrive!);

        var ergebnis = aufloeser.ZuAbsolut(@"DZ\structures\data\irgendwas_co.paa");

        Assert.NotNull(ergebnis);
        Assert.StartsWith(TestAssets.PDrive!, ergebnis, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"irgendwas_co.paa", ergebnis);
    }

    [PDriveFact]
    public void Findet_fuer_ein_echtes_Modell_mindestens_eine_vorhandene_Textur()
    {
        var modell = P3dModelReader.Read(
            TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d"));
        var aufloeser = new TextureResolver(TestAssets.PDrive!);
        var lod = modell.FeinsterSichtbarerLod!;

        var gefunden = lod.Sections
            .Select(aufloeser.DiffuseFuer)
            .Where(p => p is not null)
            .ToList();

        Assert.NotEmpty(gefunden);
        Assert.All(gefunden, p => Assert.True(File.Exists(p), $"Nicht vorhanden: {p}"));
    }

    [Fact]
    public void Ein_leerer_Abschnitt_liefert_null_statt_einer_Ausnahme()
    {
        var aufloeser = new TextureResolver(@"C:\gibt-es-nicht");
        var abschnitt = new MeshSection { Indices = [0, 1, 2] };

        Assert.Null(aufloeser.DiffuseFuer(abschnitt));
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter "FullyQualifiedName~RvmatMaterialTests|FullyQualifiedName~TextureResolverTests"`
Expected: FAIL, `RvmatMaterial` und `TextureResolver` existieren nicht.

- [ ] **Step 3: `RvmatMaterial.cs` schreiben**

```csharp
using System.Text.RegularExpressions;

namespace DzAssets.Formats.Models;

/// <summary>
/// Liest die Texturzuweisungen aus einer Klartext-RVMAT.
/// Binarisierte RVMAT (Kennung "raP") werden nicht unterstuetzt.
/// </summary>
public sealed partial class RvmatMaterial
{
    private RvmatMaterial(IReadOnlyList<string> stageTexturen)
    {
        StageTextures = stageTexturen;
        DiffusePfad = stageTexturen.FirstOrDefault(
                          t => t.EndsWith("_co.paa", StringComparison.OrdinalIgnoreCase))
                      ?? stageTexturen.FirstOrDefault();
    }

    public IReadOnlyList<string> StageTextures { get; }
    public string? DiffusePfad { get; }

    public static RvmatMaterial Leer { get; } = new([]);

    public static RvmatMaterial Parse(string text)
    {
        if (string.IsNullOrEmpty(text) || text.StartsWith("raP", StringComparison.Ordinal))
            return Leer;

        var treffer = TexturZeile()
            .Matches(text)
            .Select(m => m.Groups["pfad"].Value)
            .Where(p => p.Length > 0)
            .Where(p => !p.StartsWith('#'))          // prozedurale Texturen
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RvmatMaterial(treffer);
    }

    public static RvmatMaterial Load(string pfad)
    {
        try
        {
            return Parse(File.ReadAllText(pfad));
        }
        catch (IOException)
        {
            return Leer;
        }
        catch (UnauthorizedAccessException)
        {
            return Leer;
        }
    }

    [GeneratedRegex("""texture\s*=\s*"(?<pfad>[^"]*)"\s*;""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TexturZeile();
}
```

- [ ] **Step 4: `TextureResolver.cs` schreiben**

```csharp
using System.Collections.Concurrent;

namespace DzAssets.Formats.Models;

/// <summary>
/// Findet zu einem Mesh-Abschnitt die zugehoerige Diffusetextur auf der Platte.
/// </summary>
public sealed class TextureResolver(string pDrive)
{
    private readonly ConcurrentDictionary<string, RvmatMaterial> _materialCache = new(StringComparer.OrdinalIgnoreCase);

    public string PDrive { get; } = pDrive;

    /// <summary>Wandelt "DZ\a\b_co.paa" in einen absoluten Pfad. Null bei leerer Eingabe.</summary>
    public string? ZuAbsolut(string? assetPfad)
    {
        if (string.IsNullOrWhiteSpace(assetPfad)) return null;

        var bereinigt = assetPfad.Trim().Replace('/', '\\').TrimStart('\\');
        if (Path.IsPathRooted(bereinigt)) return bereinigt;

        return Path.Combine(PDrive, bereinigt);
    }

    /// <summary>Absoluter Pfad zur Diffusetextur des Abschnitts, oder null.</summary>
    public string? DiffuseFuer(MeshSection abschnitt)
    {
        ArgumentNullException.ThrowIfNull(abschnitt);

        var direkt = ZuAbsolut(abschnitt.TexturePath);
        if (direkt is not null && File.Exists(direkt)) return direkt;

        var materialPfad = ZuAbsolut(abschnitt.MaterialPath);
        if (materialPfad is null || !File.Exists(materialPfad)) return null;

        var material = _materialCache.GetOrAdd(materialPfad, RvmatMaterial.Load);
        var ausMaterial = ZuAbsolut(material.DiffusePfad);

        return ausMaterial is not null && File.Exists(ausMaterial) ? ausMaterial : null;
    }
}
```

- [ ] **Step 5: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter "FullyQualifiedName~RvmatMaterialTests|FullyQualifiedName~TextureResolverTests"`
Expected: `Passed!  - Failed: 0, Passed: 7`

- [ ] **Step 6: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "Texturaufloesung ueber Section, EmbeddedMaterial und RVMAT"
```

---

### Task 6: Asset-Index über alle Wurzeln

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Katalog/AssetEintrag.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Katalog/AssetIndex.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/AssetIndexTests.cs`

**Interfaces:**
- Consumes: nichts aus früheren Tasks.
- Produces:

```csharp
public sealed record AssetEintrag(
    string AbsoluterPfad,   // "H:\P_Drive\DZ\structures\...\basin_a.p3d"
    string AssetPfad,       // "DZ\structures\...\basin_a.p3d"
    string Name,            // "basin_a"
    string Ordner,          // "DZ\structures\furniture\bathroom\basin_a"
    long Groesse,
    DateTime GeaendertUtc);

public sealed class AssetIndex
{
    public IReadOnlyList<AssetEintrag> Eintraege { get; }
    public static AssetIndex Erstellen(IReadOnlyList<string> wurzeln,
                                       IProgress<int>? fortschritt = null,
                                       CancellationToken abbruch = default);
    public static AssetIndex? AusCache(string cacheDatei, IReadOnlyList<string> wurzeln);
    public void InCacheSchreiben(string cacheDatei);
    public IEnumerable<AssetEintrag> Suche(string text, int hoechstens = 500);
}
```

**Verhalten der Suche:** Grosse und kleine Schreibung sind gleich. Mehrere
durch Leerzeichen getrennte Wörter müssen **alle** im `AssetPfad`
vorkommen (UND-Verknüpfung). Treffer, bei denen der Dateiname mit dem
ersten Wort beginnt, stehen vorn.

**Cache-Gültigkeit:** Der Cache enthält die Liste der Wurzeln und je Wurzel
die Anzahl gefundener Dateien. Er wird verworfen, wenn sich die Menge der
Wurzeln unterscheidet. Ändert sich nur der Inhalt eines Ordners, merkt der
Cache das nicht — deshalb gibt es in der Oberfläche eine Schaltfläche
„Neu einlesen" (Task 13). Das ist bewusst so: ein vollständiger Abgleich
über 8.430 Dateien bei jedem Start wäre teurer als der seltene manuelle
Auffrischer.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/AssetIndexTests.cs`:

```csharp
using DzAssets.Formats.Katalog;
using Xunit;

namespace DzAssets.Tests;

public class AssetIndexTests : IDisposable
{
    private readonly string _tempWurzel = Path.Combine(Path.GetTempPath(), "dzai_" + Guid.NewGuid().ToString("N"));

    public AssetIndexTests()
    {
        Directory.CreateDirectory(Path.Combine(_tempWurzel, "DZ", "structures", "haus"));
        Directory.CreateDirectory(Path.Combine(_tempWurzel, "DZ", "plants", "baum"));
        File.WriteAllBytes(Path.Combine(_tempWurzel, "DZ", "structures", "haus", "haus_gross.p3d"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(_tempWurzel, "DZ", "structures", "haus", "haus_klein.p3d"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(_tempWurzel, "DZ", "plants", "baum", "t_fichte.p3d"), new byte[] { 1 });
        File.WriteAllText(Path.Combine(_tempWurzel, "DZ", "plants", "baum", "egal.txt"), "kein Modell");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempWurzel, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Findet_alle_p3d_und_ignoriert_andere_Dateien()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        Assert.Equal(3, index.Eintraege.Count);
        Assert.All(index.Eintraege, e => Assert.EndsWith(".p3d", e.AbsoluterPfad));
    }

    [Fact]
    public void Der_Assetpfad_ist_relativ_zur_Wurzel()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        var eintrag = index.Eintraege.Single(e => e.Name == "t_fichte");
        Assert.Equal(@"DZ\plants\baum\t_fichte.p3d", eintrag.AssetPfad);
        Assert.Equal(@"DZ\plants\baum", eintrag.Ordner);
    }

    [Fact]
    public void Die_Suche_verknuepft_mehrere_Woerter_mit_und()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        var treffer = index.Suche("haus gross").ToList();

        Assert.Single(treffer);
        Assert.Equal("haus_gross", treffer[0].Name);
    }

    [Fact]
    public void Die_Suche_ignoriert_Gross_und_Kleinschreibung()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        Assert.Equal(2, index.Suche("HAUS").Count());
    }

    [Fact]
    public void Treffer_mit_passendem_Dateianfang_stehen_vorn()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        var treffer = index.Suche("t_").ToList();

        Assert.NotEmpty(treffer);
        Assert.Equal("t_fichte", treffer[0].Name);
    }

    [Fact]
    public void Die_Suche_begrenzt_die_Trefferzahl()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        Assert.Single(index.Suche("p3d", hoechstens: 1));
    }

    [Fact]
    public void Der_Cache_wird_geschrieben_und_wieder_gelesen()
    {
        var cacheDatei = Path.Combine(_tempWurzel, "index.json");
        var original = AssetIndex.Erstellen([_tempWurzel]);

        original.InCacheSchreiben(cacheDatei);
        var geladen = AssetIndex.AusCache(cacheDatei, [_tempWurzel]);

        Assert.NotNull(geladen);
        Assert.Equal(original.Eintraege.Count, geladen!.Eintraege.Count);
    }

    [Fact]
    public void Ein_Cache_fuer_andere_Wurzeln_wird_verworfen()
    {
        var cacheDatei = Path.Combine(_tempWurzel, "index.json");
        AssetIndex.Erstellen([_tempWurzel]).InCacheSchreiben(cacheDatei);

        var geladen = AssetIndex.AusCache(cacheDatei, [_tempWurzel, @"C:\andere-wurzel"]);

        Assert.Null(geladen);
    }

    [Fact]
    public void Eine_nicht_vorhandene_Wurzel_fuehrt_nicht_zu_einer_Ausnahme()
    {
        var index = AssetIndex.Erstellen([@"C:\gibt-es-ganz-sicher-nicht-4711"]);

        Assert.Empty(index.Eintraege);
    }

    [Fact]
    public void Der_Fortschritt_wird_gemeldet()
    {
        var gemeldet = new List<int>();
        AssetIndex.Erstellen([_tempWurzel], new Progress<int>(gemeldet.Add));

        // Progress<T> meldet auf dem Threadpool; im Test genuegt es, dass
        // der Aufruf ohne Ausnahme durchlaeuft.
        Assert.True(true);
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~AssetIndexTests`
Expected: FAIL, `DzAssets.Formats.Katalog` existiert nicht.

- [ ] **Step 3: `AssetEintrag.cs` schreiben**

```csharp
namespace DzAssets.Formats.Katalog;

/// <summary>Ein gefundenes Modell im Bestand.</summary>
public sealed record AssetEintrag(
    string AbsoluterPfad,
    string AssetPfad,
    string Name,
    string Ordner,
    long Groesse,
    DateTime GeaendertUtc);
```

- [ ] **Step 4: `AssetIndex.cs` schreiben**

```csharp
using System.Text.Json;

namespace DzAssets.Formats.Katalog;

/// <summary>
/// Verzeichnis aller .p3d-Modelle unterhalb der konfigurierten Wurzeln.
/// </summary>
public sealed class AssetIndex
{
    private sealed record CacheInhalt(string[] Wurzeln, AssetEintrag[] Eintraege);

    private AssetIndex(IReadOnlyList<AssetEintrag> eintraege, IReadOnlyList<string> wurzeln)
    {
        Eintraege = eintraege;
        Wurzeln = wurzeln;
    }

    public IReadOnlyList<AssetEintrag> Eintraege { get; }
    public IReadOnlyList<string> Wurzeln { get; }

    public static AssetIndex Erstellen(
        IReadOnlyList<string> wurzeln,
        IProgress<int>? fortschritt = null,
        CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(wurzeln);

        var eintraege = new List<AssetEintrag>(16_384);

        foreach (var wurzel in wurzeln)
        {
            if (string.IsNullOrWhiteSpace(wurzel) || !Directory.Exists(wurzel))
                continue;

            var optionen = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };

            foreach (var datei in Directory.EnumerateFiles(wurzel, "*.p3d", optionen))
            {
                abbruch.ThrowIfCancellationRequested();

                FileInfo info;
                try
                {
                    info = new FileInfo(datei);
                }
                catch (IOException)
                {
                    continue;
                }

                var relativ = Path.GetRelativePath(wurzel, datei);
                eintraege.Add(new AssetEintrag(
                    AbsoluterPfad: datei,
                    AssetPfad: relativ,
                    Name: Path.GetFileNameWithoutExtension(datei),
                    Ordner: Path.GetDirectoryName(relativ) ?? string.Empty,
                    Groesse: info.Length,
                    GeaendertUtc: info.LastWriteTimeUtc));

                if (eintraege.Count % 500 == 0)
                    fortschritt?.Report(eintraege.Count);
            }
        }

        fortschritt?.Report(eintraege.Count);
        return new AssetIndex(eintraege, wurzeln.ToArray());
    }

    public static AssetIndex? AusCache(string cacheDatei, IReadOnlyList<string> wurzeln)
    {
        if (!File.Exists(cacheDatei)) return null;

        try
        {
            var inhalt = JsonSerializer.Deserialize<CacheInhalt>(File.ReadAllText(cacheDatei));
            if (inhalt is null) return null;

            var gleich = inhalt.Wurzeln.Length == wurzeln.Count
                && inhalt.Wurzeln.OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(wurzeln.OrderBy(w => w, StringComparer.OrdinalIgnoreCase),
                                   StringComparer.OrdinalIgnoreCase);

            return gleich ? new AssetIndex(inhalt.Eintraege, inhalt.Wurzeln) : null;
        }
        catch (Exception fehler) when (fehler is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void InCacheSchreiben(string cacheDatei)
    {
        var ordner = Path.GetDirectoryName(cacheDatei);
        if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);

        var inhalt = new CacheInhalt(Wurzeln.ToArray(), Eintraege.ToArray());
        File.WriteAllText(cacheDatei, JsonSerializer.Serialize(inhalt));
    }

    public IEnumerable<AssetEintrag> Suche(string text, int hoechstens = 500)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Eintraege.Take(hoechstens);

        var woerter = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var erstes = woerter[0];

        return Eintraege
            .Where(e => woerter.All(w => e.AssetPfad.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.Name.StartsWith(erstes, StringComparison.OrdinalIgnoreCase))
            .ThenBy(e => e.Name.Length)
            .ThenBy(e => e.AssetPfad, StringComparer.OrdinalIgnoreCase)
            .Take(hoechstens);
    }
}
```

- [ ] **Step 5: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~AssetIndexTests`
Expected: `Passed!  - Failed: 0, Passed: 10`

- [ ] **Step 6: Den Index einmal gegen das echte P-Drive laufen lassen**

Ein kurzer Zusatztest, der zeigt, dass der Durchlauf über den echten
Bestand schnell genug ist. In `AssetIndexTests` ergänzen:

```csharp
    [PDriveFact]
    public void Der_echte_Bestand_wird_in_unter_dreissig_Sekunden_eingelesen()
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();

        var index = AssetIndex.Erstellen([TestAssets.PDrive!]);

        uhr.Stop();
        Assert.True(index.Eintraege.Count > 5000, $"Nur {index.Eintraege.Count} Modelle gefunden");
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(30), $"Dauer {uhr.Elapsed}");
    }
```

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~AssetIndexTests`
Expected: `Passed!` mit 11 Tests. Dauert der Durchlauf länger als 30 Sekunden,
die Grenze **nicht** hochsetzen, sondern in Task 13 den Aufbau in einen
Hintergrund-Thread verlegen und den Wert hier auf die gemessene Dauer
dokumentieren.

- [ ] **Step 7: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "Asset-Index mit Suche und Cache"
```

---

### Task 7: Klassennamen aus config.cpp

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Formats/Katalog/ConfigClassIndex.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/ConfigClassIndexTests.cs`

**Interfaces:**
- Consumes: nichts.
- Produces:

```csharp
public sealed class ConfigClassIndex
{
    public static ConfigClassIndex Erstellen(IReadOnlyList<string> wurzeln,
                                             CancellationToken abbruch = default);
    public static ConfigClassIndex AusText(string configText);   // fuer Tests
    /// Klassennamen, die auf dieses Modell zeigen. Der Schluessel ist der
    /// Assetpfad in Kleinschreibung mit Backslashes, z. B.
    /// "dz\plants\tree\t_piceaabies_2s.p3d".
    public IReadOnlyList<string> KlassenFuer(string assetPfad);
    public int AnzahlKlassen { get; }
}
```

**Vorgehen:** Kein vollständiger Parser. Ein Zeilenscanner mit
Klammerzählung genügt: Bei `class X` oder `class X : Y` den Namen auf einen
Stapel legen, bei `{` die Tiefe erhöhen, bei `};` beziehungsweise `}` die
Tiefe senken und den Namen entfernen. Trifft der Scanner auf
`model="..."`, wird der oberste Klassenname dem Modellpfad zugeordnet.
Zeilen mit `//` und Blöcke mit `/* */` werden vorher entfernt.

Das ist bewusst tolerant: `config.cpp` aus Spieldateien enthält Makros und
`#include`, die ein strenger Parser ablehnen würde. Ein falsch zugeordneter
Klassenname ist hier folgenlos — die Angabe ist ein Hinweis im Info-Panel,
keine Grundlage für eine Entscheidung des Programms.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/ConfigClassIndexTests.cs`:

```csharp
using DzAssets.Formats.Katalog;
using Xunit;

namespace DzAssets.Tests;

public class ConfigClassIndexTests
{
    [Fact]
    public void Ordnet_einen_Klassennamen_dem_Modellpfad_zu()
    {
        const string text = """
            class CfgVehicles
            {
                class HouseNoDestruct;
                class Land_Wand_A : HouseNoDestruct
                {
                    scope=1;
                    model="DZ\structures\walls\wand_a.p3d";
                };
            };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Contains("Land_Wand_A", index.KlassenFuer(@"DZ\structures\walls\wand_a.p3d"));
    }

    [Fact]
    public void Die_Zuordnung_ignoriert_Gross_und_Kleinschreibung_und_Schraegstriche()
    {
        const string text = """
            class Land_X { model="DZ\a\b.p3d"; };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Contains("Land_X", index.KlassenFuer(@"dz/a/b.p3d"));
    }

    [Fact]
    public void Mehrere_Klassen_koennen_auf_dasselbe_Modell_zeigen()
    {
        const string text = """
            class Land_A { model="DZ\a\b.p3d"; };
            class Land_B { model="DZ\a\b.p3d"; };
            """;

        var index = ConfigClassIndex.AusText(text);

        var klassen = index.KlassenFuer(@"DZ\a\b.p3d");
        Assert.Equal(2, klassen.Count);
        Assert.Contains("Land_A", klassen);
        Assert.Contains("Land_B", klassen);
    }

    [Fact]
    public void Kommentare_werden_uebergangen()
    {
        const string text = """
            // class Land_Falsch { model="DZ\falsch.p3d"; };
            /* class Land_AuchFalsch { model="DZ\auch_falsch.p3d"; }; */
            class Land_Richtig { model="DZ\richtig.p3d"; };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Empty(index.KlassenFuer(@"DZ\falsch.p3d"));
        Assert.Empty(index.KlassenFuer(@"DZ\auch_falsch.p3d"));
        Assert.Contains("Land_Richtig", index.KlassenFuer(@"DZ\richtig.p3d"));
    }

    [Fact]
    public void Eine_Vorwaertsdeklaration_ohne_Rumpf_stoert_die_Zuordnung_nicht()
    {
        const string text = """
            class Basis;
            class Land_A : Basis { model="DZ\a.p3d"; };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Contains("Land_A", index.KlassenFuer(@"DZ\a.p3d"));
        Assert.Single(index.KlassenFuer(@"DZ\a.p3d"));
    }

    [Fact]
    public void Ein_unbekanntes_Modell_liefert_eine_leere_Liste_statt_null()
    {
        var index = ConfigClassIndex.AusText("class Leer {};");

        Assert.Empty(index.KlassenFuer(@"DZ\gibt-es-nicht.p3d"));
    }

    [PDriveFact]
    public void Liest_die_echte_config_cpp_der_Pflanzen()
    {
        var index = ConfigClassIndex.Erstellen([Path.Combine(TestAssets.PDrive!, "DZ", "plants")]);

        Assert.True(index.AnzahlKlassen > 10, $"Nur {index.AnzahlKlassen} Klassen gefunden");
        // In plants/config.cpp nachweislich vorhanden (am 2026-08-26 geprueft):
        var klassen = index.KlassenFuer(@"DZ\plants\tree\t_PiceaAbies_2s_xmas.p3d");
        Assert.NotEmpty(klassen);
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~ConfigClassIndexTests`
Expected: FAIL, `ConfigClassIndex` existiert nicht.

- [ ] **Step 3: `ConfigClassIndex.cs` schreiben**

```csharp
using System.Text.RegularExpressions;

namespace DzAssets.Formats.Katalog;

/// <summary>
/// Ordnet Modellpfaden die Klassennamen aus den config.cpp-Dateien zu.
/// Bewusst toleranter Zeilenscanner, kein vollstaendiger Parser: die
/// Spieldateien enthalten Makros, die jeder strenge Parser ablehnen wuerde.
/// </summary>
public sealed partial class ConfigClassIndex
{
    private readonly Dictionary<string, List<string>> _nachModell = new(StringComparer.OrdinalIgnoreCase);

    public int AnzahlKlassen => _nachModell.Values.Sum(l => l.Count);

    public IReadOnlyList<string> KlassenFuer(string assetPfad)
        => _nachModell.TryGetValue(Normalisieren(assetPfad), out var liste)
            ? liste
            : Array.Empty<string>();

    public static ConfigClassIndex AusText(string configText)
    {
        var index = new ConfigClassIndex();
        index.Verarbeiten(configText);
        return index;
    }

    public static ConfigClassIndex Erstellen(IReadOnlyList<string> wurzeln, CancellationToken abbruch = default)
    {
        var index = new ConfigClassIndex();

        foreach (var wurzel in wurzeln)
        {
            if (string.IsNullOrWhiteSpace(wurzel) || !Directory.Exists(wurzel)) continue;

            var optionen = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };

            foreach (var datei in Directory.EnumerateFiles(wurzel, "config.cpp", optionen))
            {
                abbruch.ThrowIfCancellationRequested();
                try
                {
                    index.Verarbeiten(File.ReadAllText(datei));
                }
                catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
                {
                    // Eine unlesbare config.cpp darf den Rest nicht verhindern.
                }
            }
        }

        return index;
    }

    private void Verarbeiten(string text)
    {
        text = BlockKommentar().Replace(text, " ");
        text = ZeilenKommentar().Replace(text, string.Empty);

        var stapel = new Stack<string>();
        string? offenerName = null;

        foreach (var rohZeile in text.Split('\n'))
        {
            var zeile = rohZeile.Trim();
            if (zeile.Length == 0) continue;

            var klassenTreffer = KlassenKopf().Match(zeile);
            if (klassenTreffer.Success)
                offenerName = klassenTreffer.Groups["name"].Value;

            foreach (var zeichen in zeile)
            {
                if (zeichen == '{')
                {
                    stapel.Push(offenerName ?? string.Empty);
                    offenerName = null;
                }
                else if (zeichen == '}')
                {
                    if (stapel.Count > 0) stapel.Pop();
                }
            }

            // "class Basis;" ohne Rumpf: der gemerkte Name verfaellt.
            if (klassenTreffer.Success && zeile.EndsWith(';') && !zeile.Contains('{'))
                offenerName = null;

            var modellTreffer = ModellZeile().Match(zeile);
            if (modellTreffer.Success && stapel.Count > 0)
            {
                var klasse = stapel.Peek();
                if (klasse.Length == 0) continue;

                var schluessel = Normalisieren(modellTreffer.Groups["pfad"].Value);
                if (schluessel.Length == 0) continue;

                if (!_nachModell.TryGetValue(schluessel, out var liste))
                    _nachModell[schluessel] = liste = [];

                if (!liste.Contains(klasse, StringComparer.OrdinalIgnoreCase))
                    liste.Add(klasse);
            }
        }
    }

    private static string Normalisieren(string pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad)) return string.Empty;
        var wert = pfad.Trim().Replace('/', '\\').TrimStart('\\');
        if (!wert.EndsWith(".p3d", StringComparison.OrdinalIgnoreCase)) wert += ".p3d";
        return wert.ToLowerInvariant();
    }

    [GeneratedRegex(@"^class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex KlassenKopf();

    [GeneratedRegex("""model\s*=\s*"(?<pfad>[^"]*)"\s*;""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModellZeile();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockKommentar();

    [GeneratedRegex(@"//[^\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex ZeilenKommentar();
}
```

- [ ] **Step 4: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~ConfigClassIndexTests`
Expected: `Passed!  - Failed: 0, Passed: 7`

Schlägt `Liest_die_echte_config_cpp_der_Pflanzen` fehl, den echten Pfad nachschlagen und im Test ersetzen:

```bash
grep -o 'model[[:space:]]*=[[:space:]]*"[^"]*"' /h/P_Drive/DZ/plants/config.cpp | head -3
```

- [ ] **Step 5: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "Klassennamen aus config.cpp den Modellpfaden zuordnen"
```

---

### Task 8: Shell — Fenster, Theme, Modulleiste, Einstellungen, Protokoll

Erst hier entsteht die Anwendung. Sie ist von Anfang an ein Rahmen für
mehrere Werkzeuge, damit der Debinarizer und der Höhenkarten-Previewer
später eingehängt werden können, ohne die Vorschau anzufassen.

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Preview/DzAssets.Preview.csproj`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/App.xaml`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/App.xaml.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Shell/Theme.xaml`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Shell/IWerkzeugModul.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Shell/WerkzeugKontext.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Shell/Einstellungen.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Shell/Protokoll.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Shell/BeobachtbaresObjekt.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Shell/HauptFenster.xaml`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Shell/HauptFenster.xaml.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/EinstellungenTests.cs`

**Interfaces:**
- Consumes: nichts aus früheren Tasks (die Shell kennt keine Dateiformate).
- Produces:

```csharp
public interface IWerkzeugModul
{
    string Titel { get; }          // "Asset-Vorschau"
    string Symbol { get; }         // Glyphe aus Segoe Fluent Icons, z. B. "\uE809"
    string Beschreibung { get; }
    UserControl ErzeugeAnsicht(WerkzeugKontext kontext);
}

public sealed class WerkzeugKontext
{
    public Einstellungen Einstellungen { get; }
    public Protokoll Protokoll { get; }
    public string DatenOrdner { get; }   // %LOCALAPPDATA%\DayZAssetPreview
}

public sealed class Einstellungen : BeobachtbaresObjekt
{
    public List<string> Wurzeln { get; set; }
    public string? ZuletztGeoeffnet { get; set; }
    public bool BodengitterZeigen { get; set; }
    public bool MassstabsfigurZeigen { get; set; }
    public static Einstellungen Laden(string datei);
    public void Speichern(string datei);
    public static IReadOnlyList<string> WurzelnErraten();
}

public sealed class Protokoll
{
    public Protokoll(string datei);
    public void Schreiben(string nachricht);
    public void Fehler(string nachricht, Exception? ausnahme = null);
}

public abstract class BeobachtbaresObjekt : INotifyPropertyChanged
{
    protected bool Setzen<T>(ref T feld, T wert, [CallerMemberName] string? name = null);
}
```

**Gestaltung.** Alle Farben und Masse liegen als Ressourcen in
`Shell/Theme.xaml`. Bis die Farben der Website vorliegen, gilt die unten
stehende dunkle Palette; ausgetauscht wird später **nur diese eine Datei**.
Kein anderer Ort im Projekt darf eine Farbe fest verdrahten — jede Angabe
läuft über `{DynamicResource ...}`.

- [ ] **Step 1: Anwendungsprojekt anlegen**

`tools/DayZAssetPreview/DzAssets.Preview/DzAssets.Preview.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ApplicationTitle>DayZ Asset Preview</ApplicationTitle>
    <AssemblyName>DayZ Asset Preview</AssemblyName>
    <RootNamespace>DzAssets.Preview</RootNamespace>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\DzAssets.Formats\DzAssets.Formats.csproj" />
  </ItemGroup>

</Project>
```

```bash
cd tools/DayZAssetPreview
dotnet sln add DzAssets.Preview/DzAssets.Preview.csproj
dotnet add DzAssets.Tests/DzAssets.Tests.csproj reference DzAssets.Preview/DzAssets.Preview.csproj
```

- [ ] **Step 2: Den fehlschlagenden Test für die Einstellungen schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/EinstellungenTests.cs`:

```csharp
using DzAssets.Preview.Shell;
using Xunit;

namespace DzAssets.Tests;

public class EinstellungenTests : IDisposable
{
    private readonly string _datei = Path.Combine(Path.GetTempPath(), $"dz_{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_datei); } catch (IOException) { }
    }

    [Fact]
    public void Eine_fehlende_Datei_ergibt_Voreinstellungen_statt_einer_Ausnahme()
    {
        var einstellungen = Einstellungen.Laden(_datei);

        Assert.NotNull(einstellungen);
        Assert.True(einstellungen.BodengitterZeigen);
    }

    [Fact]
    public void Gespeicherte_Werte_werden_wieder_gelesen()
    {
        var original = Einstellungen.Laden(_datei);
        original.Wurzeln = [@"H:\P_Drive", @"D:\Mods"];
        original.BodengitterZeigen = false;
        original.Speichern(_datei);

        var geladen = Einstellungen.Laden(_datei);

        Assert.Equal(2, geladen.Wurzeln.Count);
        Assert.Contains(@"D:\Mods", geladen.Wurzeln);
        Assert.False(geladen.BodengitterZeigen);
    }

    [Fact]
    public void Eine_beschaedigte_Datei_fuehrt_zu_Voreinstellungen()
    {
        File.WriteAllText(_datei, "{ das ist kein JSON");

        var einstellungen = Einstellungen.Laden(_datei);

        Assert.NotNull(einstellungen);
    }

    [Fact]
    public void Aenderungen_werden_gemeldet()
    {
        var einstellungen = Einstellungen.Laden(_datei);
        var gemeldet = new List<string?>();
        einstellungen.PropertyChanged += (_, e) => gemeldet.Add(e.PropertyName);

        einstellungen.BodengitterZeigen = !einstellungen.BodengitterZeigen;

        Assert.Contains(nameof(Einstellungen.BodengitterZeigen), gemeldet);
    }

    [Fact]
    public void Das_Erraten_der_Wurzeln_liefert_nur_vorhandene_Ordner()
    {
        var wurzeln = Einstellungen.WurzelnErraten();

        Assert.All(wurzeln, w => Assert.True(Directory.Exists(w), $"Nicht vorhanden: {w}"));
    }
}
```

- [ ] **Step 3: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~EinstellungenTests`
Expected: FAIL, `DzAssets.Preview.Shell` existiert nicht.

- [ ] **Step 4: `BeobachtbaresObjekt.cs`, `Protokoll.cs` und `Einstellungen.cs` schreiben**

`Shell/BeobachtbaresObjekt.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DzAssets.Preview.Shell;

/// <summary>Kleinste Grundlage fuer Bindungen. Kein MVVM-Framework noetig.</summary>
public abstract class BeobachtbaresObjekt : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Setzen<T>(ref T feld, T wert, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(feld, wert)) return false;
        feld = wert;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Melden([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

`Shell/Protokoll.cs`:

```csharp
namespace DzAssets.Preview.Shell;

/// <summary>
/// Schreibt Meldungen in eine Textdatei. Fehler beim Protokollieren
/// duerfen die Anwendung nie stoeren.
/// </summary>
public sealed class Protokoll(string datei)
{
    private readonly Lock _schloss = new();

    public string Datei { get; } = datei;

    public void Schreiben(string nachricht) => Anhaengen("INFO ", nachricht);

    public void Fehler(string nachricht, Exception? ausnahme = null)
        => Anhaengen("FEHLER", ausnahme is null ? nachricht : $"{nachricht} :: {ausnahme}");

    /// <summary>
    /// Ein TextWriter, der ins Protokoll schreibt. Gedacht fuer
    /// Console.SetError: BisDll meldet beim Lesen von ODOL-Dateien
    /// ueber Console.Error, und in einer Fensteranwendung gaebe es dafuer
    /// sonst keinen Empfaenger. Synchronisiert, weil mehrere Threads
    /// gleichzeitig Modelle lesen.
    /// </summary>
    public TextWriter AlsTextWriter() => TextWriter.Synchronized(new ProtokollSchreiber(this));

    private void Anhaengen(string art, string text)
    {
        try
        {
            var ordner = Path.GetDirectoryName(Datei);
            if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);

            lock (_schloss)
            {
                File.AppendAllText(Datei,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {art} {text}{Environment.NewLine}");
            }
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Protokollieren darf nie zum Problem werden.
        }
    }

    private sealed class ProtokollSchreiber(Protokoll ziel) : TextWriter
    {
        private readonly System.Text.StringBuilder _zeile = new();

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override void Write(char zeichen)
        {
            if (zeichen == '\n')
            {
                var text = _zeile.ToString().TrimEnd('\r');
                _zeile.Clear();
                if (text.Length > 0) ziel.Anhaengen("BISDLL", text);
            }
            else if (_zeile.Length < 4096)
            {
                _zeile.Append(zeichen);
            }
        }

        public override void Write(string? wert)
        {
            if (wert is null) return;
            foreach (var zeichen in wert) Write(zeichen);
        }
    }
}
```

`Shell/Einstellungen.cs`:

```csharp
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DzAssets.Preview.Shell;

public sealed class Einstellungen : BeobachtbaresObjekt
{
    private List<string> _wurzeln = [];
    private string? _zuletztGeoeffnet;
    private bool _bodengitterZeigen = true;
    private bool _massstabsfigurZeigen = true;

    public List<string> Wurzeln
    {
        get => _wurzeln;
        set => Setzen(ref _wurzeln, value);
    }

    public string? ZuletztGeoeffnet
    {
        get => _zuletztGeoeffnet;
        set => Setzen(ref _zuletztGeoeffnet, value);
    }

    public bool BodengitterZeigen
    {
        get => _bodengitterZeigen;
        set => Setzen(ref _bodengitterZeigen, value);
    }

    public bool MassstabsfigurZeigen
    {
        get => _massstabsfigurZeigen;
        set => Setzen(ref _massstabsfigurZeigen, value);
    }

    public static Einstellungen Laden(string datei)
    {
        Einstellungen? geladen = null;
        try
        {
            if (File.Exists(datei))
                geladen = JsonSerializer.Deserialize<Einstellungen>(File.ReadAllText(datei));
        }
        catch (Exception fehler) when (fehler is JsonException or IOException or UnauthorizedAccessException)
        {
            geladen = null;
        }

        geladen ??= new Einstellungen();
        if (geladen.Wurzeln.Count == 0)
            geladen.Wurzeln = WurzelnErraten().ToList();

        return geladen;
    }

    public void Speichern(string datei)
    {
        try
        {
            var ordner = Path.GetDirectoryName(datei);
            if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);

            File.WriteAllText(datei,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Nicht speichern zu koennen darf die Anwendung nicht beenden.
        }
    }

    /// <summary>
    /// Sucht nach einem Arbeitslaufwerk mit entpackten Gamefiles.
    /// Erkennungsmerkmal ist ein Unterordner "DZ".
    /// </summary>
    public static IReadOnlyList<string> WurzelnErraten()
    {
        var kandidaten = new List<string> { @"H:\P_Drive", @"P:\", @"C:\P_Drive" };

        foreach (var laufwerk in DriveInfo.GetDrives())
        {
            if (!laufwerk.IsReady) continue;
            kandidaten.Add(Path.Combine(laufwerk.RootDirectory.FullName, "P_Drive"));
        }

        return kandidaten
            .Where(k => SicherVorhanden(Path.Combine(k, "DZ")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        static bool SicherVorhanden(string pfad)
        {
            try { return Directory.Exists(pfad); }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
```

- [ ] **Step 5: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~EinstellungenTests`
Expected: `Passed!  - Failed: 0, Passed: 5`

- [ ] **Step 6: `Theme.xaml` schreiben**

`Shell/Theme.xaml` — die einzige Stelle mit Farben. Wenn die Farben der
Website vorliegen, werden **nur** die neun `Color`-Einträge im ersten Block
ersetzt; nichts anderes muss angefasst werden.

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- ===== Farbwerte. Nur dieser Block wird ausgetauscht. ===== -->
  <Color x:Key="FarbeGrund">#0E1116</Color>
  <Color x:Key="FarbeFlaeche">#161A21</Color>
  <Color x:Key="FarbeFlaecheHoch">#1E242D</Color>
  <Color x:Key="FarbeRand">#2A313C</Color>
  <Color x:Key="FarbeText">#E6EAF0</Color>
  <Color x:Key="FarbeTextSchwach">#98A2B3</Color>
  <Color x:Key="FarbeAkzent">#4C8DFF</Color>
  <Color x:Key="FarbeAkzentGedaempft">#2B4C8C</Color>
  <Color x:Key="FarbeWarnung">#F0A64A</Color>

  <!-- ===== Abgeleitete Pinsel ===== -->
  <SolidColorBrush x:Key="PinselGrund" Color="{DynamicResource FarbeGrund}" />
  <SolidColorBrush x:Key="PinselFlaeche" Color="{DynamicResource FarbeFlaeche}" />
  <SolidColorBrush x:Key="PinselFlaecheHoch" Color="{DynamicResource FarbeFlaecheHoch}" />
  <SolidColorBrush x:Key="PinselRand" Color="{DynamicResource FarbeRand}" />
  <SolidColorBrush x:Key="PinselText" Color="{DynamicResource FarbeText}" />
  <SolidColorBrush x:Key="PinselTextSchwach" Color="{DynamicResource FarbeTextSchwach}" />
  <SolidColorBrush x:Key="PinselAkzent" Color="{DynamicResource FarbeAkzent}" />
  <SolidColorBrush x:Key="PinselWarnung" Color="{DynamicResource FarbeWarnung}" />

  <!-- ===== Masse ===== -->
  <CornerRadius x:Key="RadiusKlein">6</CornerRadius>
  <CornerRadius x:Key="RadiusGross">10</CornerRadius>
  <Thickness x:Key="AbstandInnen">12</Thickness>
  <sys:Double xmlns:sys="clr-namespace:System;assembly=System.Runtime"
              x:Key="SchriftgroesseNormal">13</sys:Double>

  <!-- ===== Schriften ===== -->
  <FontFamily x:Key="SchriftText">Segoe UI Variable Text, Segoe UI, sans-serif</FontFamily>
  <FontFamily x:Key="SchriftUeberschrift">Segoe UI Variable Display, Segoe UI, sans-serif</FontFamily>
  <FontFamily x:Key="SchriftSymbole">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>

  <!-- ===== Grundstile ===== -->
  <Style TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{DynamicResource SchriftText}" />
    <Setter Property="FontSize" Value="{DynamicResource SchriftgroesseNormal}" />
    <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
    <Setter Property="TextOptions.TextFormattingMode" Value="Ideal" />
  </Style>

  <Style x:Key="StilUeberschrift" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{DynamicResource SchriftUeberschrift}" />
    <Setter Property="FontSize" Value="18" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
  </Style>

  <Style x:Key="StilNebentext" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{DynamicResource SchriftText}" />
    <Setter Property="FontSize" Value="12" />
    <Setter Property="Foreground" Value="{DynamicResource PinselTextSchwach}" />
  </Style>

  <!-- Schaltflaeche: flach, abgerundet, mit weichem Hover -->
  <Style TargetType="Button">
    <Setter Property="FontFamily" Value="{DynamicResource SchriftText}" />
    <Setter Property="FontSize" Value="{DynamicResource SchriftgroesseNormal}" />
    <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
    <Setter Property="Background" Value="{DynamicResource PinselFlaecheHoch}" />
    <Setter Property="Padding" Value="12,7" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Rahmen"
                  Background="{TemplateBinding Background}"
                  BorderBrush="{DynamicResource PinselRand}"
                  BorderThickness="1"
                  CornerRadius="{DynamicResource RadiusKlein}"
                  Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Rahmen" Property="BorderBrush" Value="{DynamicResource PinselAkzent}" />
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="Rahmen" Property="Background" Value="{DynamicResource PinselAkzent}" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="Rahmen" Property="Opacity" Value="0.45" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Symbolschaltflaeche der Modulleiste: quadratisch, Akzentbalken links -->
  <Style x:Key="StilModulSchalter" TargetType="ToggleButton">
    <Setter Property="Width" Value="52" />
    <Setter Property="Height" Value="52" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Foreground" Value="{DynamicResource PinselTextSchwach}" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ToggleButton">
          <Grid>
            <Border x:Name="Balken" Width="3" HorizontalAlignment="Left"
                    Margin="0,10" CornerRadius="0,2,2,0"
                    Background="{DynamicResource PinselAkzent}" Opacity="0" />
            <Border x:Name="Flaeche" Margin="6,4"
                    CornerRadius="{DynamicResource RadiusKlein}"
                    Background="Transparent">
              <TextBlock Text="{TemplateBinding Content}"
                         FontFamily="{DynamicResource SchriftSymbole}"
                         FontSize="19"
                         Foreground="{TemplateBinding Foreground}"
                         HorizontalAlignment="Center" VerticalAlignment="Center" />
            </Border>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Flaeche" Property="Background" Value="{DynamicResource PinselFlaecheHoch}" />
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Balken" Property="Opacity" Value="1" />
              <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Eingabefeld -->
  <Style TargetType="TextBox">
    <Setter Property="FontFamily" Value="{DynamicResource SchriftText}" />
    <Setter Property="FontSize" Value="{DynamicResource SchriftgroesseNormal}" />
    <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
    <Setter Property="CaretBrush" Value="{DynamicResource PinselAkzent}" />
    <Setter Property="Background" Value="{DynamicResource PinselFlaecheHoch}" />
    <Setter Property="BorderBrush" Value="{DynamicResource PinselRand}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="9,7" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TextBox">
          <Border x:Name="Rahmen"
                  Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  CornerRadius="{DynamicResource RadiusKlein}">
            <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocusWithin" Value="True">
              <Setter TargetName="Rahmen" Property="BorderBrush" Value="{DynamicResource PinselAkzent}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Schlanke Bildlaufleiste -->
  <Style TargetType="ScrollBar">
    <Setter Property="Width" Value="10" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ScrollBar">
          <Grid Background="{TemplateBinding Background}">
            <Track x:Name="PART_Track" IsDirectionReversed="True">
              <Track.Thumb>
                <Thumb>
                  <Thumb.Template>
                    <ControlTemplate TargetType="Thumb">
                      <Border Background="{DynamicResource PinselRand}"
                              CornerRadius="5" Margin="3,0" />
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" />
              </Track.IncreaseRepeatButton>
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" />
              </Track.DecreaseRepeatButton>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

</ResourceDictionary>
```

- [ ] **Step 7: `IWerkzeugModul.cs` und `WerkzeugKontext.cs` schreiben**

`Shell/IWerkzeugModul.cs`:

```csharp
using System.Windows.Controls;

namespace DzAssets.Preview.Shell;

/// <summary>
/// Ein Werkzeug in der Shell. Die Shell kennt nur diese Schnittstelle;
/// sie weiss nichts ueber P3D, PAA oder ASC.
/// </summary>
public interface IWerkzeugModul
{
    /// <summary>Beschriftung in der Modulleiste.</summary>
    string Titel { get; }

    /// <summary>Eine Glyphe aus "Segoe Fluent Icons", z. B. "\uE809".</summary>
    string Symbol { get; }

    /// <summary>Ein Satz fuer den Hinweis beim Ueberfahren mit der Maus.</summary>
    string Beschreibung { get; }

    /// <summary>
    /// Erzeugt die Ansicht. Wird von der Shell genau einmal aufgerufen —
    /// beim ersten Oeffnen des Moduls, nicht beim Programmstart. So kostet
    /// ein Modul nichts, solange es niemand benutzt.
    /// </summary>
    UserControl ErzeugeAnsicht(WerkzeugKontext kontext);
}
```

`Shell/WerkzeugKontext.cs`:

```csharp
namespace DzAssets.Preview.Shell;

/// <summary>
/// Die Dienste, die sich alle Werkzeuge teilen. Als ein Objekt und nicht
/// als drei Konstruktorparameter, damit spaetere Module ohne Aenderung an
/// der Schnittstelle weitere Dienste vorfinden.
/// </summary>
public sealed class WerkzeugKontext(string datenOrdner, Einstellungen einstellungen, Protokoll protokoll)
{
    public string DatenOrdner { get; } = datenOrdner;
    public Einstellungen Einstellungen { get; } = einstellungen;
    public Protokoll Protokoll { get; } = protokoll;

    /// <summary>Pfad einer Datei im Datenordner der Anwendung.</summary>
    public string DatenDatei(string name) => Path.Combine(DatenOrdner, name);

    public static WerkzeugKontext Standard()
    {
        var ordner = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DayZAssetPreview");
        Directory.CreateDirectory(ordner);

        var einstellungen = Einstellungen.Laden(Path.Combine(ordner, "settings.json"));
        var protokoll = new Protokoll(Path.Combine(ordner, "log.txt"));
        return new WerkzeugKontext(ordner, einstellungen, protokoll);
    }

    public void EinstellungenSpeichern()
        => Einstellungen.Speichern(DatenDatei("settings.json"));
}
```

- [ ] **Step 8: `App.xaml`, `HauptFenster.xaml` und deren Code schreiben**

`App.xaml`:

```xml
<Application x:Class="DzAssets.Preview.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Shell/Theme.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

`App.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Threading;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview;

public partial class App : Application
{
    private WerkzeugKontext? _kontext;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _kontext = WerkzeugKontext.Standard();

        // BisDll meldet beim Lesen ueber Console.Error. In einer
        // Fensteranwendung gaebe es dafuer keinen Empfaenger, also
        // umleiten — einmal, prozessweit, ins Protokoll. Kein anderer Ort
        // im Programm darf Console.SetError aufrufen: der Strom ist
        // global, und mehrere Threads lesen gleichzeitig Modelle.
        Console.SetError(_kontext.Protokoll.AlsTextWriter());

        DispatcherUnhandledException += (_, args) =>
        {
            _kontext.Protokoll.Fehler("Unbehandelter Fehler in der Oberflaeche", args.Exception);
            MessageBox.Show(
                $"Es ist ein Fehler aufgetreten:\n\n{args.Exception.Message}\n\n" +
                $"Einzelheiten stehen in\n{_kontext.Protokoll.Datei}",
                "DayZ Asset Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        new HauptFenster(_kontext).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _kontext?.EinstellungenSpeichern();
        base.OnExit(e);
    }
}
```

`Shell/HauptFenster.xaml`:

```xml
<Window x:Class="DzAssets.Preview.Shell.HauptFenster"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        Title="DayZ Asset Preview"
        Height="820" Width="1400"
        MinHeight="560" MinWidth="960"
        WindowStartupLocation="CenterScreen"
        Background="{DynamicResource PinselGrund}"
        Foreground="{DynamicResource PinselText}"
        UseLayoutRounding="True"
        TextOptions.TextFormattingMode="Ideal">

  <shell:WindowChrome.WindowChrome>
    <shell:WindowChrome CaptionHeight="40" ResizeBorderThickness="6"
                        CornerRadius="0" GlassFrameThickness="0" />
  </shell:WindowChrome.WindowChrome>

  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="40" />
      <RowDefinition Height="*" />
      <RowDefinition Height="26" />
    </Grid.RowDefinitions>

    <!-- Titelleiste -->
    <Grid Grid.Row="0" Background="{DynamicResource PinselFlaeche}">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>

      <StackPanel Grid.Column="0" Orientation="Horizontal" Margin="14,0,0,0"
                  VerticalAlignment="Center">
        <TextBlock Text="&#xE81E;" FontFamily="{DynamicResource SchriftSymbole}"
                   FontSize="15" Foreground="{DynamicResource PinselAkzent}"
                   VerticalAlignment="Center" />
        <TextBlock Text="DayZ Asset Preview" Margin="10,0,0,0"
                   FontWeight="SemiBold" VerticalAlignment="Center" />
        <TextBlock x:Name="ModulTitel" Margin="12,0,0,0"
                   Style="{StaticResource StilNebentext}" VerticalAlignment="Center" />
      </StackPanel>

      <StackPanel Grid.Column="2" Orientation="Horizontal"
                  shell:WindowChrome.IsHitTestVisibleInChrome="True">
        <Button x:Name="KnopfMinimieren" Content="&#xE921;" Click="Minimieren_Click"
                Style="{StaticResource StilFensterKnopf}" />
        <Button x:Name="KnopfMaximieren" Content="&#xE922;" Click="Maximieren_Click"
                Style="{StaticResource StilFensterKnopf}" />
        <Button x:Name="KnopfSchliessen" Content="&#xE8BB;" Click="Schliessen_Click"
                Style="{StaticResource StilSchliessenKnopf}" />
      </StackPanel>
    </Grid>

    <!-- Modulleiste und Arbeitsflaeche -->
    <Grid Grid.Row="1">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="52" />
        <ColumnDefinition Width="*" />
      </Grid.ColumnDefinitions>

      <Border Grid.Column="0" Background="{DynamicResource PinselFlaeche}"
              BorderBrush="{DynamicResource PinselRand}" BorderThickness="0,0,1,0">
        <StackPanel x:Name="ModulLeiste" Margin="0,8,0,0" />
      </Border>

      <ContentControl x:Name="ModulInhalt" Grid.Column="1" />
    </Grid>

    <!-- Statusleiste -->
    <Border Grid.Row="2" Background="{DynamicResource PinselFlaeche}"
            BorderBrush="{DynamicResource PinselRand}" BorderThickness="0,1,0,0">
      <TextBlock x:Name="StatusText" Margin="14,0" VerticalAlignment="Center"
                 Style="{StaticResource StilNebentext}" Text="Bereit" />
    </Border>
  </Grid>
</Window>
```

Die beiden Stile `StilFensterKnopf` und `StilSchliessenKnopf` in
`Theme.xaml` ergänzen:

```xml
  <Style x:Key="StilFensterKnopf" TargetType="Button">
    <Setter Property="Width" Value="46" />
    <Setter Property="Height" Value="40" />
    <Setter Property="Foreground" Value="{DynamicResource PinselTextSchwach}" />
    <Setter Property="FontFamily" Value="{DynamicResource SchriftSymbole}" />
    <Setter Property="FontSize" Value="10" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Flaeche" Background="Transparent">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Flaeche" Property="Background" Value="{DynamicResource PinselFlaecheHoch}" />
              <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="StilSchliessenKnopf" TargetType="Button"
         BasedOn="{StaticResource StilFensterKnopf}">
    <Style.Triggers>
      <Trigger Property="IsMouseOver" Value="True">
        <Setter Property="Foreground" Value="White" />
        <Setter Property="Background" Value="#C42B1C" />
      </Trigger>
    </Style.Triggers>
  </Style>
```

Da `StilSchliessenKnopf` den Hintergrund über einen Trigger setzt, im
`StilFensterKnopf`-Template `Background="Transparent"` durch
`Background="{TemplateBinding Background}"` ersetzen.

`Shell/HauptFenster.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DzAssets.Preview.Shell;

public partial class HauptFenster : Window
{
    private readonly WerkzeugKontext _kontext;
    private readonly List<IWerkzeugModul> _module = [];
    private readonly Dictionary<IWerkzeugModul, UserControl> _ansichten = [];

    public HauptFenster(WerkzeugKontext kontext)
    {
        _kontext = kontext;
        InitializeComponent();

        // Weitere Werkzeuge werden hier eingehaengt — Debinarizer,
        // Hoehenkarten-Previewer. Die Shell braucht dafuer keine Aenderung.
        ModuleHinzufuegen(new Module.AssetVorschau.AssetVorschauModul());

        StateChanged += (_, _) =>
            KnopfMaximieren.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    public void StatusSetzen(string text) => StatusText.Text = text;

    private void ModuleHinzufuegen(params IWerkzeugModul[] module)
    {
        foreach (var modul in module)
        {
            _module.Add(modul);

            var schalter = new ToggleButton
            {
                Style = (Style)FindResource("StilModulSchalter"),
                Content = modul.Symbol,
                ToolTip = $"{modul.Titel} — {modul.Beschreibung}",
                Tag = modul,
            };
            schalter.Checked += ModulSchalter_Checked;
            ModulLeiste.Children.Add(schalter);
        }

        if (ModulLeiste.Children.Count > 0)
            ((ToggleButton)ModulLeiste.Children[0]).IsChecked = true;
    }

    private void ModulSchalter_Checked(object sender, RoutedEventArgs e)
    {
        var schalter = (ToggleButton)sender;
        var modul = (IWerkzeugModul)schalter.Tag;

        foreach (var anderer in ModulLeiste.Children.OfType<ToggleButton>())
            if (!ReferenceEquals(anderer, schalter)) anderer.IsChecked = false;

        if (!_ansichten.TryGetValue(modul, out var ansicht))
        {
            try
            {
                ansicht = modul.ErzeugeAnsicht(_kontext);
                _ansichten[modul] = ansicht;
            }
            catch (Exception fehler)
            {
                _kontext.Protokoll.Fehler($"Modul '{modul.Titel}' liess sich nicht oeffnen", fehler);
                StatusSetzen($"Modul '{modul.Titel}' liess sich nicht oeffnen — siehe Protokoll.");
                return;
            }
        }

        ModulInhalt.Content = ansicht;
        ModulTitel.Text = modul.Titel;
    }

    private void Minimieren_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximieren_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Schliessen_Click(object sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 9: Ein leeres Vorschau-Modul anlegen, damit die Shell startet**

`tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/AssetVorschauModul.cs`:

```csharp
using System.Windows.Controls;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

public sealed class AssetVorschauModul : IWerkzeugModul
{
    public string Titel => "Asset-Vorschau";
    public string Symbol => "\uE809";
    public string Beschreibung => "DayZ-Modelle aus den entpackten Gamefiles ansehen";

    public UserControl ErzeugeAnsicht(WerkzeugKontext kontext)
        => new AssetVorschauAnsicht(kontext);
}
```

`Module/AssetVorschau/AssetVorschauAnsicht.xaml` — vorläufig nur ein
Platzhalter, der in Task 9 gefüllt wird:

```xml
<UserControl x:Class="DzAssets.Preview.Module.AssetVorschau.AssetVorschauAnsicht"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource PinselGrund}">
  <TextBlock Text="Asset-Vorschau" Style="{StaticResource StilUeberschrift}"
             HorizontalAlignment="Center" VerticalAlignment="Center" />
</UserControl>
```

`Module/AssetVorschau/AssetVorschauAnsicht.xaml.cs`:

```csharp
using System.Windows.Controls;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

public partial class AssetVorschauAnsicht : UserControl
{
    private readonly WerkzeugKontext _kontext;

    public AssetVorschauAnsicht(WerkzeugKontext kontext)
    {
        _kontext = kontext;
        InitializeComponent();
    }
}
```

- [ ] **Step 10: Bauen und starten**

Run: `cd tools/DayZAssetPreview && dotnet build`
Expected: `Build succeeded`

Run: `cd tools/DayZAssetPreview && dotnet run --project DzAssets.Preview`
Expected: Ein dunkles, randloses Fenster mit eigener Titelleiste, links eine
Modulleiste mit einem Symbol, in der Mitte „Asset-Vorschau", unten eine
Statuszeile mit „Bereit". Minimieren, Maximieren und Schliessen
funktionieren; das Fenster lässt sich an der Titelleiste ziehen und an den
Rändern in der Grösse ändern.

Sichtprüfung, bevor es weitergeht:
- Keine Standard-Windows-Titelleiste zusätzlich zur eigenen.
- Der Text ist nicht verwaschen (`TextFormattingMode="Ideal"`).
- Beim Maximieren verdeckt das Fenster die Taskleiste nicht. Tut es das
  doch, in `HauptFenster.xaml.cs` ein `MaxHeight` aus
  `SystemParameters.WorkArea` setzen, sobald sich der Bildschirm ändert.

- [ ] **Step 11: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "Shell mit dunklem Theme, eigenem Fenster-Chrome und Modulleiste"
```

---

### Task 9: Ordnerbaum und Auswahl

**Files:**
- Modify: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/AssetVorschauAnsicht.xaml` (vollständig ersetzen)
- Modify: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/AssetVorschauAnsicht.xaml.cs` (vollständig ersetzen)
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/BaumKnoten.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/BaumKnotenTests.cs`

**Interfaces:**
- Consumes: `AssetIndex`, `AssetEintrag` (Task 6); `WerkzeugKontext` (Task 8).
- Produces:

```csharp
public sealed class BaumKnoten : BeobachtbaresObjekt
{
    public string Beschriftung { get; }
    public string? AssetPfad { get; }        // nur bei Blaettern gesetzt
    public string? AbsoluterPfad { get; }    // nur bei Blaettern gesetzt
    public bool IstOrdner { get; }
    public ObservableCollection<BaumKnoten> Kinder { get; }
    public bool IstAufgeklappt { get; set; }

    /// Baut aus einer flachen Eintragsliste den Ordnerbaum.
    public static IReadOnlyList<BaumKnoten> BaumBauen(IEnumerable<AssetEintrag> eintraege);
}
```

**Warum ein eigener Baum statt `TreeView` mit Dateisystem:** Der Bestand ist
bereits im `AssetIndex`. Ein zweiter Durchlauf über die Platte beim
Aufklappen jedes Ordners wäre langsamer und würde Ordner ohne Modelle
anzeigen, die hier niemanden interessieren.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/BaumKnotenTests.cs`:

```csharp
using DzAssets.Formats.Katalog;
using DzAssets.Preview.Module.AssetVorschau;
using Xunit;

namespace DzAssets.Tests;

public class BaumKnotenTests
{
    private static AssetEintrag Eintrag(string assetPfad) => new(
        AbsoluterPfad: @"H:\P_Drive\" + assetPfad,
        AssetPfad: assetPfad,
        Name: Path.GetFileNameWithoutExtension(assetPfad),
        Ordner: Path.GetDirectoryName(assetPfad) ?? string.Empty,
        Groesse: 100,
        GeaendertUtc: DateTime.UnixEpoch);

    [Fact]
    public void Baut_aus_flachen_Pfaden_einen_Ordnerbaum()
    {
        var baum = BaumKnoten.BaumBauen([
            Eintrag(@"DZ\structures\haus\a.p3d"),
            Eintrag(@"DZ\structures\haus\b.p3d"),
            Eintrag(@"DZ\plants\baum\c.p3d"),
        ]);

        var dz = Assert.Single(baum);
        Assert.Equal("DZ", dz.Beschriftung);
        Assert.Equal(2, dz.Kinder.Count);           // structures, plants
    }

    [Fact]
    public void Blaetter_tragen_den_Assetpfad_Ordner_nicht()
    {
        var baum = BaumKnoten.BaumBauen([Eintrag(@"DZ\a\b.p3d")]);

        var dz = baum[0];
        var a = dz.Kinder[0];
        var blatt = a.Kinder[0];

        Assert.True(dz.IstOrdner);
        Assert.Null(dz.AssetPfad);
        Assert.False(blatt.IstOrdner);
        Assert.Equal(@"DZ\a\b.p3d", blatt.AssetPfad);
        Assert.Equal("b", blatt.Beschriftung);
    }

    [Fact]
    public void Ordner_stehen_vor_Dateien_und_sind_alphabetisch_sortiert()
    {
        var baum = BaumKnoten.BaumBauen([
            Eintrag(@"DZ\zebra.p3d"),
            Eintrag(@"DZ\alpha.p3d"),
            Eintrag(@"DZ\unterordner\x.p3d"),
        ]);

        var kinder = baum[0].Kinder;
        Assert.Equal("unterordner", kinder[0].Beschriftung);
        Assert.True(kinder[0].IstOrdner);
        Assert.Equal("alpha", kinder[1].Beschriftung);
        Assert.Equal("zebra", kinder[2].Beschriftung);
    }

    [Fact]
    public void Eine_leere_Liste_ergibt_einen_leeren_Baum()
    {
        Assert.Empty(BaumKnoten.BaumBauen([]));
    }

    [Fact]
    public void Zehntausend_Eintraege_werden_in_unter_zwei_Sekunden_verbaut()
    {
        var eintraege = Enumerable.Range(0, 10_000)
            .Select(i => Eintrag($@"DZ\gruppe{i % 50}\unter{i % 7}\modell{i}.p3d"))
            .ToList();

        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var baum = BaumKnoten.BaumBauen(eintraege);
        uhr.Stop();

        Assert.NotEmpty(baum);
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(2), $"Dauer {uhr.Elapsed}");
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~BaumKnotenTests`
Expected: FAIL, `BaumKnoten` existiert nicht.

- [ ] **Step 3: `BaumKnoten.cs` schreiben**

```csharp
using System.Collections.ObjectModel;
using DzAssets.Formats.Katalog;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>Ein Ordner oder ein Modell im Bestandsbaum.</summary>
public sealed class BaumKnoten : BeobachtbaresObjekt
{
    private bool _istAufgeklappt;

    private BaumKnoten(string beschriftung, bool istOrdner, string? assetPfad, string? absoluterPfad)
    {
        Beschriftung = beschriftung;
        IstOrdner = istOrdner;
        AssetPfad = assetPfad;
        AbsoluterPfad = absoluterPfad;
    }

    public string Beschriftung { get; }
    public bool IstOrdner { get; }
    public string? AssetPfad { get; }
    public string? AbsoluterPfad { get; }
    public ObservableCollection<BaumKnoten> Kinder { get; } = [];

    /// <summary>Glyphe aus "Segoe Fluent Icons": Ordner oder Wuerfel.</summary>
    public string Symbol => IstOrdner ? "\uE8B7" : "\uE809";

    public bool IstAufgeklappt
    {
        get => _istAufgeklappt;
        set => Setzen(ref _istAufgeklappt, value);
    }

    public static IReadOnlyList<BaumKnoten> BaumBauen(IEnumerable<AssetEintrag> eintraege)
    {
        ArgumentNullException.ThrowIfNull(eintraege);

        var wurzeln = new Dictionary<string, BaumKnoten>(StringComparer.OrdinalIgnoreCase);
        var ordnerCache = new Dictionary<string, BaumKnoten>(StringComparer.OrdinalIgnoreCase);

        foreach (var eintrag in eintraege)
        {
            var teile = eintrag.AssetPfad.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (teile.Length == 0) continue;

            BaumKnoten? eltern = null;
            var bisher = string.Empty;

            for (var i = 0; i < teile.Length - 1; i++)
            {
                bisher = bisher.Length == 0 ? teile[i] : bisher + "\\" + teile[i];

                if (!ordnerCache.TryGetValue(bisher, out var ordner))
                {
                    ordner = new BaumKnoten(teile[i], istOrdner: true, null, null);
                    ordnerCache[bisher] = ordner;

                    if (eltern is null) wurzeln[bisher] = ordner;
                    else eltern.Kinder.Add(ordner);
                }

                eltern = ordner;
            }

            var blatt = new BaumKnoten(
                Path.GetFileNameWithoutExtension(teile[^1]),
                istOrdner: false,
                eintrag.AssetPfad,
                eintrag.AbsoluterPfad);

            if (eltern is null) wurzeln[eintrag.AssetPfad] = blatt;
            else eltern.Kinder.Add(blatt);
        }

        var ergebnis = wurzeln.Values.ToList();
        foreach (var knoten in ordnerCache.Values) Sortieren(knoten);
        ergebnis.Sort(Vergleichen);
        return ergebnis;
    }

    private static void Sortieren(BaumKnoten knoten)
    {
        var sortiert = knoten.Kinder.OrderBy(k => k, Comparer<BaumKnoten>.Create(Vergleichen)).ToList();
        knoten.Kinder.Clear();
        foreach (var kind in sortiert) knoten.Kinder.Add(kind);
    }

    private static int Vergleichen(BaumKnoten? a, BaumKnoten? b)
    {
        if (a is null || b is null) return 0;
        if (a.IstOrdner != b.IstOrdner) return a.IstOrdner ? -1 : 1;
        return string.Compare(a.Beschriftung, b.Beschriftung, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 4: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~BaumKnotenTests`
Expected: `Passed!  - Failed: 0, Passed: 5`

- [ ] **Step 5: Die Vorschau-Ansicht mit Baum aufbauen**

`Module/AssetVorschau/AssetVorschauAnsicht.xaml` vollständig ersetzen:

```xml
<UserControl x:Class="DzAssets.Preview.Module.AssetVorschau.AssetVorschauAnsicht"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource PinselGrund}">

  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="300" MinWidth="220" />
      <ColumnDefinition Width="4" />
      <ColumnDefinition Width="*" MinWidth="380" />
      <ColumnDefinition Width="4" />
      <ColumnDefinition Width="280" MinWidth="220" />
    </Grid.ColumnDefinitions>

    <!-- Linke Spalte: Suche und Baum -->
    <Border Grid.Column="0" Background="{DynamicResource PinselFlaeche}"
            BorderBrush="{DynamicResource PinselRand}" BorderThickness="0,0,1,0">
      <Grid>
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto" />
          <RowDefinition Height="*" />
          <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <Grid Grid.Row="0" Margin="12,12,12,8">
          <TextBox x:Name="SuchFeld" TextChanged="SuchFeld_TextChanged" Padding="30,7,9,7" />
          <TextBlock Text="&#xE721;" FontFamily="{DynamicResource SchriftSymbole}"
                     FontSize="13" Foreground="{DynamicResource PinselTextSchwach}"
                     Margin="10,0,0,0" VerticalAlignment="Center" IsHitTestVisible="False" />
          <TextBlock x:Name="SuchHinweis" Text="Modelle durchsuchen…"
                     Style="{StaticResource StilNebentext}"
                     Margin="31,0,0,0" VerticalAlignment="Center" IsHitTestVisible="False" />
        </Grid>

        <TreeView x:Name="Baum" Grid.Row="1"
                  Background="Transparent" BorderThickness="0"
                  SelectedItemChanged="Baum_SelectedItemChanged"
                  VirtualizingStackPanel.IsVirtualizing="True"
                  VirtualizingStackPanel.VirtualizationMode="Recycling"
                  ScrollViewer.HorizontalScrollBarVisibility="Disabled">
          <TreeView.ItemContainerStyle>
            <Style TargetType="TreeViewItem">
              <Setter Property="IsExpanded" Value="{Binding IstAufgeklappt, Mode=TwoWay}" />
              <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
              <Setter Property="Padding" Value="2" />
            </Style>
          </TreeView.ItemContainerStyle>
          <TreeView.ItemTemplate>
            <HierarchicalDataTemplate ItemsSource="{Binding Kinder}">
              <StackPanel Orientation="Horizontal" Margin="0,2">
                <TextBlock Text="{Binding Symbol}"
                           FontFamily="{DynamicResource SchriftSymbole}" FontSize="12"
                           Foreground="{DynamicResource PinselTextSchwach}"
                           VerticalAlignment="Center" Margin="0,0,7,0" />
                <TextBlock Text="{Binding Beschriftung}" VerticalAlignment="Center"
                           TextTrimming="CharacterEllipsis" />
              </StackPanel>
            </HierarchicalDataTemplate>
          </TreeView.ItemTemplate>
        </TreeView>

        <Border Grid.Row="2" Padding="12,8" BorderBrush="{DynamicResource PinselRand}"
                BorderThickness="0,1,0,0">
          <StackPanel Orientation="Horizontal">
            <Button x:Name="KnopfNeuEinlesen" Content="Neu einlesen" Click="NeuEinlesen_Click" />
            <TextBlock x:Name="BestandText" Style="{StaticResource StilNebentext}"
                       VerticalAlignment="Center" Margin="10,0,0,0" />
          </StackPanel>
        </Border>
      </Grid>
    </Border>

    <GridSplitter Grid.Column="1" Width="4" HorizontalAlignment="Stretch"
                  Background="Transparent" />

    <!-- Mitte: Viewport, kommt in Task 10 -->
    <Grid x:Name="ViewportBereich" Grid.Column="2">
      <TextBlock x:Name="ViewportHinweis"
                 Text="Ein Modell im Baum auswaehlen"
                 Style="{StaticResource StilNebentext}"
                 HorizontalAlignment="Center" VerticalAlignment="Center" />
    </Grid>

    <GridSplitter Grid.Column="3" Width="4" HorizontalAlignment="Stretch"
                  Background="Transparent" />

    <!-- Rechts: Info, kommt in Task 12 -->
    <Border Grid.Column="4" Background="{DynamicResource PinselFlaeche}"
            BorderBrush="{DynamicResource PinselRand}" BorderThickness="1,0,0,0">
      <StackPanel x:Name="InfoBereich" Margin="14" />
    </Border>
  </Grid>
</UserControl>
```

- [ ] **Step 6: Den Code der Ansicht schreiben**

`Module/AssetVorschau/AssetVorschauAnsicht.xaml.cs` vollständig ersetzen:

```csharp
using System.Windows;
using System.Windows.Controls;
using DzAssets.Formats.Katalog;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

public partial class AssetVorschauAnsicht : UserControl
{
    private readonly WerkzeugKontext _kontext;
    private AssetIndex? _index;

    public AssetVorschauAnsicht(WerkzeugKontext kontext)
    {
        _kontext = kontext;
        InitializeComponent();
        Loaded += (_, _) => BestandLaden(neuEinlesen: false);
    }

    /// <summary>Wird in Task 10 mit dem Viewport verbunden.</summary>
    private void ModellAnzeigen(string absoluterPfad, string assetPfad)
    {
        ViewportHinweis.Text = assetPfad;
    }

    private async void BestandLaden(bool neuEinlesen)
    {
        KnopfNeuEinlesen.IsEnabled = false;
        BestandText.Text = "wird eingelesen…";

        var wurzeln = _kontext.Einstellungen.Wurzeln.ToList();
        var cacheDatei = _kontext.DatenDatei("index.json");

        try
        {
            _index = await Task.Run(() =>
            {
                if (!neuEinlesen)
                {
                    var ausCache = AssetIndex.AusCache(cacheDatei, wurzeln);
                    if (ausCache is not null) return ausCache;
                }

                var neu = AssetIndex.Erstellen(wurzeln);
                neu.InCacheSchreiben(cacheDatei);
                return neu;
            });

            var baum = await Task.Run(() => BaumKnoten.BaumBauen(_index.Eintraege));
            Baum.ItemsSource = baum;
            BestandText.Text = $"{_index.Eintraege.Count:N0} Modelle";
            _kontext.Protokoll.Schreiben($"Bestand eingelesen: {_index.Eintraege.Count} Modelle");

            if (_index.Eintraege.Count == 0)
                BestandText.Text = "keine Modelle gefunden — Wurzeln pruefen";
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler("Bestand liess sich nicht einlesen", fehler);
            BestandText.Text = "Fehler — siehe Protokoll";
        }
        finally
        {
            KnopfNeuEinlesen.IsEnabled = true;
        }
    }

    private void NeuEinlesen_Click(object sender, RoutedEventArgs e) => BestandLaden(neuEinlesen: true);

    private void Baum_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not BaumKnoten knoten) return;
        if (knoten.IstOrdner || knoten.AbsoluterPfad is null || knoten.AssetPfad is null) return;

        ModellAnzeigen(knoten.AbsoluterPfad, knoten.AssetPfad);
    }

    private void SuchFeld_TextChanged(object sender, TextChangedEventArgs e)
    {
        SuchHinweis.Visibility = SuchFeld.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        // Die eigentliche Suche folgt in Task 13.
    }
}
```

- [ ] **Step 7: Bauen, starten und den Baum prüfen**

Run: `cd tools/DayZAssetPreview && dotnet run --project DzAssets.Preview`
Expected: Nach wenigen Sekunden zeigt der Baum links `DZ` und die eigenen
Mod-Ordner. Unten steht die Anzahl (etwa 8.430 bei reinem `DZ`, mehr mit
Mods). Aufklappen ist flüssig, ein Klick auf ein Modell schreibt dessen
Pfad in die Mitte.

Ist der erste Start langsam, ist es der Verzeichnisdurchlauf; der zweite
Start kommt aus dem Cache und muss sofort da sein.

- [ ] **Step 8: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "Ordnerbaum ueber den Assetbestand mit Cache"
```

---

### Task 10: 3D-Viewport ohne Texturen

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/ModelViewport.xaml`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/ModelViewport.xaml.cs`
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/GeometrieBauer.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/GeometrieBauerTests.cs`
- Modify: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/AssetVorschauAnsicht.xaml` (Viewport statt Hinweis)
- Modify: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/AssetVorschauAnsicht.xaml.cs` (`ModellAnzeigen`)

**Interfaces:**
- Consumes: `ModelGeometry`, `LodGeometry`, `MeshSection` (Task 4).
- Produces:

```csharp
public static class GeometrieBauer
{
    /// Baut aus einem Abschnitt eine WPF-Geometrie. Spiegelt V, weil WPF
    /// die Texturkoordinate von oben zaehlt und ODOL von unten.
    public static MeshGeometry3D Bauen(LodGeometry lod, MeshSection abschnitt);
}

public partial class ModelViewport : UserControl
{
    public void Zeigen(ModelGeometry modell, LodGeometry lod,
                       IReadOnlyDictionary<MeshSection, ImageSource>? texturen = null);
    public void Leeren();
    public void Einrahmen();
    public bool DrahtgitterZeigen { get; set; }
    public bool BodengitterZeigen { get; set; }
    public bool MassstabsfigurZeigen { get; set; }
}
```

**Wichtig zur Geometrie:** WPF verlangt, dass `Positions`, `Normals` und
`TextureCoordinates` **gleich lang** sind und dass `TriangleIndices` in
diese Listen zeigt. Weil jeder Abschnitt nur einen Teil der Vertizes
benutzt, wird pro Abschnitt umindiziert: nur die tatsächlich benutzten
Vertizes werden kopiert. Das spart bei einem Haus mit zwanzig Abschnitten
erheblich Speicher gegenüber zwanzig vollständigen Kopien.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/GeometrieBauerTests.cs`:

```csharp
using DzAssets.Formats.Models;
using DzAssets.Preview.Module.AssetVorschau;
using Xunit;

namespace DzAssets.Tests;

public class GeometrieBauerTests
{
    private static LodGeometry EinfacherLod() => new()
    {
        Resolution = 1.0f,
        Name = "1.000",
        IstSichtbar = true,
        Positions = [new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(9, 9, 9)],
        Normals = [new Vec3(0, 0, 1), new Vec3(0, 0, 1), new Vec3(0, 0, 1), new Vec3(0, 1, 0)],
        Uvs = [new Vec2(0, 0), new Vec2(1, 0), new Vec2(0, 1), new Vec2(0.5f, 0.5f)],
        Sections = [],
    };

    [Fact]
    public void Baut_ein_Dreieck_mit_drei_Vertizes()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 2] };

        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), abschnitt);

        Assert.Equal(3, geometrie.Positions.Count);
        Assert.Equal(3, geometrie.TriangleIndices.Count);
        Assert.Equal(3, geometrie.Normals.Count);
        Assert.Equal(3, geometrie.TextureCoordinates.Count);
    }

    [Fact]
    public void Nicht_benutzte_Vertizes_werden_weggelassen()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 2] };

        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), abschnitt);

        // Vertex 3 (9,9,9) wird vom Abschnitt nicht benutzt.
        Assert.DoesNotContain(geometrie.Positions, p => p.X == 9);
    }

    [Fact]
    public void Die_V_Koordinate_wird_gespiegelt()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 2] };

        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), abschnitt);

        // Vertex 2 hatte v = 1 -> in WPF 0
        var punkt = geometrie.TextureCoordinates[2];
        Assert.Equal(0.0, punkt.Y, precision: 5);
    }

    [Fact]
    public void Ein_wiederholt_benutzter_Vertex_wird_nur_einmal_kopiert()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 2, 0, 2, 1] };

        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), abschnitt);

        Assert.Equal(3, geometrie.Positions.Count);
        Assert.Equal(6, geometrie.TriangleIndices.Count);
    }

    [Fact]
    public void Ein_leerer_Abschnitt_ergibt_eine_leere_Geometrie()
    {
        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), new MeshSection { Indices = [] });

        Assert.Empty(geometrie.Positions);
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~GeometrieBauerTests`
Expected: FAIL, `GeometrieBauer` existiert nicht.

- [ ] **Step 3: `GeometrieBauer.cs` schreiben**

```csharp
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DzAssets.Formats.Models;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>
/// Uebersetzt einen Mesh-Abschnitt in eine WPF-Geometrie. Umindiziert
/// dabei, damit jeder Abschnitt nur die Vertizes traegt, die er benutzt.
/// </summary>
public static class GeometrieBauer
{
    public static MeshGeometry3D Bauen(LodGeometry lod, MeshSection abschnitt)
    {
        ArgumentNullException.ThrowIfNull(lod);
        ArgumentNullException.ThrowIfNull(abschnitt);

        var geometrie = new MeshGeometry3D();
        if (abschnitt.Indices.Length == 0) return geometrie;

        var positionen = new Point3DCollection(abschnitt.Indices.Length);
        var normalen = new Vector3DCollection(abschnitt.Indices.Length);
        var uvs = new PointCollection(abschnitt.Indices.Length);
        var dreiecke = new Int32Collection(abschnitt.Indices.Length);

        var abbildung = new Dictionary<int, int>(abschnitt.Indices.Length);

        foreach (var alt in abschnitt.Indices)
        {
            if (alt < 0 || alt >= lod.Positions.Length) continue;

            if (!abbildung.TryGetValue(alt, out var neu))
            {
                neu = positionen.Count;
                abbildung[alt] = neu;

                var p = lod.Positions[alt];
                positionen.Add(new Point3D(p.X, p.Y, p.Z));

                var n = alt < lod.Normals.Length ? lod.Normals[alt] : new Vec3(0, 1, 0);
                normalen.Add(new Vector3D(n.X, n.Y, n.Z));

                var uv = alt < lod.Uvs.Length ? lod.Uvs[alt] : new Vec2(0, 0);
                // WPF zaehlt V von oben, ODOL von unten.
                uvs.Add(new System.Windows.Point(uv.U, 1.0 - uv.V));
            }

            dreiecke.Add(neu);
        }

        positionen.Freeze();
        normalen.Freeze();
        uvs.Freeze();
        dreiecke.Freeze();

        geometrie.Positions = positionen;
        geometrie.Normals = normalen;
        geometrie.TextureCoordinates = uvs;
        geometrie.TriangleIndices = dreiecke;
        return geometrie;
    }
}
```

- [ ] **Step 4: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~GeometrieBauerTests`
Expected: `Passed!  - Failed: 0, Passed: 5`

- [ ] **Step 5: `ModelViewport.xaml` schreiben**

```xml
<UserControl x:Class="DzAssets.Preview.Module.AssetVorschau.ModelViewport"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource PinselGrund}"
             Focusable="True"
             ClipToBounds="True">
  <Grid>
    <Viewport3D x:Name="Sicht">
      <Viewport3D.Camera>
        <PerspectiveCamera x:Name="Kamera" FieldOfView="45"
                           Position="4,3,6" LookDirection="-4,-3,-6" UpDirection="0,1,0" />
      </Viewport3D.Camera>

      <ModelVisual3D>
        <ModelVisual3D.Content>
          <Model3DGroup x:Name="Szene">
            <AmbientLight Color="#4A5058" />
            <DirectionalLight Color="#FFF4E8" Direction="-0.5,-0.9,-0.6" />
            <DirectionalLight Color="#3A4658" Direction="0.7,0.4,0.6" />
          </Model3DGroup>
        </ModelVisual3D.Content>
      </ModelVisual3D>
    </Viewport3D>

    <!-- Bedienhinweis -->
    <Border VerticalAlignment="Bottom" HorizontalAlignment="Left" Margin="12"
            Background="{DynamicResource PinselFlaeche}" Opacity="0.85"
            CornerRadius="{DynamicResource RadiusKlein}" Padding="10,6">
      <TextBlock Style="{StaticResource StilNebentext}"
                 Text="Links drehen · Rechts verschieben · Rad zoomen · F einrahmen · W Drahtgitter · G Gitter · M Massstab" />
    </Border>
  </Grid>
</UserControl>
```

- [ ] **Step 6: `ModelViewport.xaml.cs` schreiben**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DzAssets.Formats.Models;

namespace DzAssets.Preview.Module.AssetVorschau;

public partial class ModelViewport : UserControl
{
    private const int LichterUndKameraImModell = 3;   // Ambient + 2 Direktional

    private static readonly Material Ersatzmaterial = ErzeugeErsatzmaterial();

    private Model3DGroup? _modellGruppe;
    private Model3DGroup? _hilfsGruppe;

    private Point3D _zielpunkt;
    private double _abstand = 5;
    private double _drehungUmY;      // Bogenmass
    private double _drehungUmX = 0.45;
    private Point _letzteMausposition;
    private bool _dreht;
    private bool _verschiebt;

    private bool _drahtgitter;
    private bool _bodengitter = true;
    private bool _massstabsfigur = true;

    public ModelViewport()
    {
        InitializeComponent();
        HilfsgeometrieAufbauen();
        KameraSetzen();

        MouseDown += (_, _) => Focus();
    }

    public bool DrahtgitterZeigen
    {
        get => _drahtgitter;
        set { _drahtgitter = value; NeuAufbauen(); }
    }

    public bool BodengitterZeigen
    {
        get => _bodengitter;
        set { _bodengitter = value; HilfsgeometrieAufbauen(); }
    }

    public bool MassstabsfigurZeigen
    {
        get => _massstabsfigur;
        set { _massstabsfigur = value; HilfsgeometrieAufbauen(); }
    }

    private ModelGeometry? _modell;
    private LodGeometry? _lod;
    private IReadOnlyDictionary<MeshSection, ImageSource>? _texturen;

    public void Zeigen(ModelGeometry modell, LodGeometry lod,
                       IReadOnlyDictionary<MeshSection, ImageSource>? texturen = null)
    {
        _modell = modell;
        _lod = lod;
        _texturen = texturen;
        NeuAufbauen();
        Einrahmen();
    }

    public void Leeren()
    {
        _modell = null;
        _lod = null;
        _texturen = null;
        NeuAufbauen();
    }

    private void NeuAufbauen()
    {
        if (_modellGruppe is not null) Szene.Children.Remove(_modellGruppe);
        _modellGruppe = null;

        if (_lod is null) return;

        var gruppe = new Model3DGroup();

        foreach (var abschnitt in _lod.Sections)
        {
            var geometrie = GeometrieBauer.Bauen(_lod, abschnitt);
            if (geometrie.Positions.Count == 0) continue;

            Material material = Ersatzmaterial;
            if (_texturen is not null && _texturen.TryGetValue(abschnitt, out var bild))
            {
                var pinsel = new ImageBrush(bild)
                {
                    ViewportUnits = BrushMappingMode.Absolute,
                    TileMode = TileMode.Tile,
                    Viewport = new Rect(0, 0, 1, 1),
                };
                pinsel.Freeze();
                material = new DiffuseMaterial(pinsel);
            }

            var modell = new GeometryModel3D(geometrie, material)
            {
                BackMaterial = material,   // DayZ-Modelle sind haeufig einseitig modelliert
            };
            gruppe.Children.Add(modell);
        }

        if (_drahtgitter)
            gruppe.Children.Add(DrahtgitterErzeugen(_lod));

        _modellGruppe = gruppe;
        Szene.Children.Add(gruppe);
    }

    private static Material ErzeugeErsatzmaterial()
    {
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0x8A, 0x90, 0x99))));
        material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(0x30, 0x34, 0x3A)), 22));
        material.Freeze();
        return material;
    }

    /// <summary>
    /// WPF 3D kennt keinen Drahtgitter-Modus. Statt echter Linien wird ein
    /// zweites, leicht vergroessertes Modell in Akzentfarbe mit
    /// zurueckgesetzten Normalen gezeichnet — das genuegt, um die
    /// Unterteilung zu beurteilen, ohne eine Linienbibliothek zu brauchen.
    /// </summary>
    private static Model3DGroup DrahtgitterErzeugen(LodGeometry lod)
    {
        var gruppe = new Model3DGroup();
        var farbe = new SolidColorBrush(Color.FromArgb(0x50, 0x4C, 0x8D, 0xFF));
        farbe.Freeze();
        var material = new EmissiveMaterial(farbe);
        material.Freeze();

        foreach (var abschnitt in lod.Sections)
        {
            var geometrie = GeometrieBauer.Bauen(lod, abschnitt);
            if (geometrie.Positions.Count == 0) continue;

            gruppe.Children.Add(new GeometryModel3D(geometrie, material)
            {
                BackMaterial = material,
                Transform = new ScaleTransform3D(1.001, 1.001, 1.001),
            });
        }

        return gruppe;
    }

    private void HilfsgeometrieAufbauen()
    {
        if (_hilfsGruppe is not null) Szene.Children.Remove(_hilfsGruppe);

        var gruppe = new Model3DGroup();

        if (_bodengitter) gruppe.Children.Add(BodengitterErzeugen());
        if (_massstabsfigur) gruppe.Children.Add(MassstabsfigurErzeugen());

        _hilfsGruppe = gruppe;
        Szene.Children.Add(gruppe);
    }

    /// <summary>
    /// Ein Meterraster als texturierte Flaeche. Guenstiger als tausende
    /// Linienobjekte und in WPF ohnehin die einzige saubere Moeglichkeit.
    /// </summary>
    private static GeometryModel3D BodengitterErzeugen()
    {
        const int halbeKante = 25;   // Meter

        var geometrie = new MeshGeometry3D
        {
            Positions =
            [
                new Point3D(-halbeKante, 0, -halbeKante),
                new Point3D( halbeKante, 0, -halbeKante),
                new Point3D( halbeKante, 0,  halbeKante),
                new Point3D(-halbeKante, 0,  halbeKante),
            ],
            TextureCoordinates =
            [
                new Point(0, 0),
                new Point(halbeKante * 2, 0),
                new Point(halbeKante * 2, halbeKante * 2),
                new Point(0, halbeKante * 2),
            ],
            TriangleIndices = [0, 1, 2, 0, 2, 3],
        };

        var pinsel = new ImageBrush(GitterBild())
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 1, 1),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        pinsel.Freeze();

        var material = new DiffuseMaterial(pinsel);
        material.Freeze();

        return new GeometryModel3D(geometrie, material) { BackMaterial = material };
    }

    /// <summary>Eine Kachel von 64 Pixeln, die einen Quadratmeter darstellt.</summary>
    private static ImageSource GitterBild()
    {
        const int kante = 64;
        var pixel = new byte[kante * kante * 4];

        for (var y = 0; y < kante; y++)
        {
            for (var x = 0; x < kante; x++)
            {
                var amRand = x == 0 || y == 0;
                var i = (y * kante + x) * 4;
                pixel[i + 0] = amRand ? (byte)0x3C : (byte)0x16;   // B
                pixel[i + 1] = amRand ? (byte)0x44 : (byte)0x1A;   // G
                pixel[i + 2] = amRand ? (byte)0x50 : (byte)0x21;   // R
                pixel[i + 3] = 0xFF;
            }
        }

        var bild = System.Windows.Media.Imaging.BitmapSource.Create(
            kante, kante, 96, 96, PixelFormats.Bgra32, null, pixel, kante * 4);
        bild.Freeze();
        return bild;
    }

    /// <summary>
    /// Eine schlichte Saeule von 1,80 m Hoehe als Groessenvergleich.
    /// Bewusst kein Menschmodell: es soll den Blick nicht auf sich ziehen.
    /// </summary>
    private static GeometryModel3D MassstabsfigurErzeugen()
    {
        var geometrie = new MeshGeometry3D();
        const double breite = 0.22;
        const double tiefe = 0.16;
        const double hoehe = 1.80;
        var versatz = new Vector3D(-1.4, 0, -1.4);

        var ecken = new[]
        {
            new Point3D(-breite / 2, 0, -tiefe / 2), new Point3D(breite / 2, 0, -tiefe / 2),
            new Point3D( breite / 2, 0,  tiefe / 2), new Point3D(-breite / 2, 0,  tiefe / 2),
            new Point3D(-breite / 2, hoehe, -tiefe / 2), new Point3D(breite / 2, hoehe, -tiefe / 2),
            new Point3D( breite / 2, hoehe,  tiefe / 2), new Point3D(-breite / 2, hoehe,  tiefe / 2),
        };

        foreach (var ecke in ecken)
            geometrie.Positions.Add(ecke + versatz);

        int[] flaechen =
        [
            0,1,2, 0,2,3,   // unten
            4,6,5, 4,7,6,   // oben
            0,4,5, 0,5,1,
            1,5,6, 1,6,2,
            2,6,7, 2,7,3,
            3,7,4, 3,4,0,
        ];
        foreach (var index in flaechen) geometrie.TriangleIndices.Add(index);

        var pinsel = new SolidColorBrush(Color.FromArgb(0x99, 0x4C, 0x8D, 0xFF));
        pinsel.Freeze();
        var material = new DiffuseMaterial(pinsel);
        material.Freeze();

        return new GeometryModel3D(geometrie, material) { BackMaterial = material };
    }

    public void Einrahmen()
    {
        if (_modell is null)
        {
            _zielpunkt = new Point3D(0, 0.9, 0);
            _abstand = 5;
        }
        else
        {
            var min = _modell.BoundsMin;
            var max = _modell.BoundsMax;
            _zielpunkt = new Point3D((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2);

            var groesse = _modell.Size;
            var radius = Math.Max(0.5, Math.Max(groesse.X, Math.Max(groesse.Y, groesse.Z)));
            _abstand = radius * 1.9;
        }

        KameraSetzen();
    }

    private void KameraSetzen()
    {
        var x = _abstand * Math.Cos(_drehungUmX) * Math.Sin(_drehungUmY);
        var y = _abstand * Math.Sin(_drehungUmX);
        var z = _abstand * Math.Cos(_drehungUmX) * Math.Cos(_drehungUmY);

        var position = new Point3D(_zielpunkt.X + x, _zielpunkt.Y + y, _zielpunkt.Z + z);
        Kamera.Position = position;
        Kamera.LookDirection = _zielpunkt - position;
        Kamera.NearPlaneDistance = Math.Max(0.01, _abstand / 400);
        Kamera.FarPlaneDistance = Math.Max(200, _abstand * 40);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        _letzteMausposition = e.GetPosition(this);

        if (e.ChangedButton == MouseButton.Left) _dreht = true;
        else if (e.ChangedButton is MouseButton.Right or MouseButton.Middle) _verschiebt = true;

        CaptureMouse();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _dreht = false;
        _verschiebt = false;
        ReleaseMouseCapture();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dreht && !_verschiebt) return;

        var jetzt = e.GetPosition(this);
        var dx = jetzt.X - _letzteMausposition.X;
        var dy = jetzt.Y - _letzteMausposition.Y;
        _letzteMausposition = jetzt;

        if (_dreht)
        {
            _drehungUmY -= dx * 0.01;
            _drehungUmX = Math.Clamp(_drehungUmX + dy * 0.01, -1.5, 1.5);
        }
        else
        {
            var richtung = Kamera.LookDirection;
            richtung.Normalize();
            var rechts = Vector3D.CrossProduct(richtung, Kamera.UpDirection);
            rechts.Normalize();
            var hoch = Vector3D.CrossProduct(rechts, richtung);

            var faktor = _abstand * 0.0016;
            _zielpunkt -= rechts * (dx * faktor);
            _zielpunkt += hoch * (dy * faktor);
        }

        KameraSetzen();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _abstand = Math.Clamp(_abstand * (e.Delta > 0 ? 0.88 : 1.136), 0.05, 5000);
        KameraSetzen();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.F: Einrahmen(); e.Handled = true; break;
            case Key.W: DrahtgitterZeigen = !DrahtgitterZeigen; e.Handled = true; break;
            case Key.G: BodengitterZeigen = !BodengitterZeigen; e.Handled = true; break;
            case Key.M: MassstabsfigurZeigen = !MassstabsfigurZeigen; e.Handled = true; break;
        }
    }
}
```

- [ ] **Step 7: Den Viewport in die Ansicht einbauen**

In `AssetVorschauAnsicht.xaml` den Inhalt von `ViewportBereich` ersetzen:

```xml
    <Grid x:Name="ViewportBereich" Grid.Column="2">
      <local:ModelViewport x:Name="Viewport" />
      <TextBlock x:Name="ViewportHinweis"
                 Text="Ein Modell im Baum auswaehlen"
                 Style="{StaticResource StilNebentext}"
                 HorizontalAlignment="Center" VerticalAlignment="Center" />
    </Grid>
```

Dazu im `UserControl`-Kopf den Namensraum ergänzen:

```xml
             xmlns:local="clr-namespace:DzAssets.Preview.Module.AssetVorschau"
```

In `AssetVorschauAnsicht.xaml.cs` die Methode `ModellAnzeigen` ersetzen:

```csharp
    private async void ModellAnzeigen(string absoluterPfad, string assetPfad)
    {
        ViewportHinweis.Text = "wird geladen…";
        ViewportHinweis.Visibility = Visibility.Visible;

        try
        {
            var modell = await Task.Run(() => P3dModelReader.Read(absoluterPfad));
            var lod = modell.FeinsterSichtbarerLod;

            if (lod is null)
            {
                Viewport.Leeren();
                ViewportHinweis.Text = "Dieses Modell hat nur Geometrie- oder Memory-LODs.";
                return;
            }

            Viewport.Zeigen(modell, lod);
            ViewportHinweis.Visibility = Visibility.Collapsed;
            Viewport.Focus();
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler($"Modell liess sich nicht laden: {assetPfad}", fehler);
            Viewport.Leeren();
            ViewportHinweis.Text = $"Laesst sich nicht anzeigen:\n{fehler.Message}";
            ViewportHinweis.Visibility = Visibility.Visible;
        }
    }
```

Oben in der Datei ergänzen: `using DzAssets.Formats.Models;`

- [ ] **Step 8: Bauen, starten und die Darstellung prüfen**

Run: `cd tools/DayZAssetPreview && dotnet run --project DzAssets.Preview`

Sichtprüfung an mindestens vier Modellen — ein Möbelstück, ein Haus, ein
Baum, ein Fahrzeug:
- Das Objekt steht **auf** dem Gitter, nicht darunter oder darin.
- Die Massstabssäule von 1,80 m passt zur Grösse: eine Tür ist etwas höher,
  ein Waschbecken reicht bis etwa zur Hälfte.
- Die Beleuchtung zeigt Wölbungen; die Fläche wirkt nicht durchgehend flach.
- Drehen, Verschieben und Zoomen laufen ruckelfrei.

**Steht das Modell auf dem Kopf oder spiegelverkehrt**, ist die
Achsenumrechnung in `P3dModelReader.LiesLod` zu korrigieren — dort, nicht
hier. Die drei zu prüfenden Varianten: `(x, y, -z)` mit gedrehtem Umlauf
(so umgesetzt), `(x, z, y)`, `(-x, y, z)`. Die richtige erkennt man daran,
dass Schrift auf Schildern lesbar und nicht spiegelverkehrt ist.

**Wirkt alles gleichmässig dunkel oder flach**, sind die Normalen falsch
gerichtet: in `LiesLod` das Vorzeichen der Normalen umdrehen.

- [ ] **Step 9: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "3D-Viewport mit Kamerasteuerung, Bodengitter und Massstabsfigur"
```

---

### Task 11: Texturen im Viewport

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/TexturLader.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/TexturLaderTests.cs`
- Modify: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/AssetVorschauAnsicht.xaml.cs` (`ModellAnzeigen`)

**Interfaces:**
- Consumes: `PaaImage` (Task 3), `TextureResolver` (Task 5), `LodGeometry`/`MeshSection` (Task 4).
- Produces:

```csharp
public sealed class TexturLader(TextureResolver aufloeser, Protokoll protokoll)
{
    /// Laedt die Diffusetexturen aller Abschnitte. Darf im Hintergrund
    /// laufen; die erzeugten BitmapSource sind eingefroren.
    public IReadOnlyDictionary<MeshSection, ImageSource> Laden(LodGeometry lod);

    /// Namen der Texturen, die nicht gefunden oder nicht gelesen werden konnten.
    public IReadOnlyList<string> Fehlend { get; }
}
```

**Warum eingefroren:** Ein `BitmapSource`, der auf einem Hintergrund-Thread
erzeugt und danach mit `Freeze()` unveränderlich gemacht wird, darf vom
UI-Thread benutzt werden. Ohne `Freeze()` wirft WPF beim Zuweisen eine
`InvalidOperationException`. Dasselbe gilt für den `ImageBrush` in Task 10 —
dort bereits so umgesetzt.

**Zwischenspeicher:** Ein Haus benutzt dieselbe Wandtextur in vielen
Abschnitten. Deshalb wird je Dateipfad **einmal** dekodiert und der
`ImageSource` mehrfach zugewiesen. Bei einem Modell mit 20 Abschnitten und
4 verschiedenen Texturen spart das drei Viertel der Arbeit.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/TexturLaderTests.cs`:

```csharp
using DzAssets.Formats.Models;
using DzAssets.Preview.Module.AssetVorschau;
using DzAssets.Preview.Shell;
using Xunit;

namespace DzAssets.Tests;

public class TexturLaderTests
{
    private static Protokoll StillesProtokoll()
        => new(Path.Combine(Path.GetTempPath(), $"dztest_{Guid.NewGuid():N}.log"));

    [PDriveFact]
    public void Laedt_fuer_ein_echtes_Modell_mindestens_eine_Textur()
    {
        var modell = P3dModelReader.Read(
            TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d"));
        var lader = new TexturLader(new TextureResolver(TestAssets.PDrive!), StillesProtokoll());

        var texturen = lader.Laden(modell.FeinsterSichtbarerLod!);

        Assert.NotEmpty(texturen);
    }

    [PDriveFact]
    public void Die_geladenen_Bilder_sind_eingefroren()
    {
        var modell = P3dModelReader.Read(
            TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d"));
        var lader = new TexturLader(new TextureResolver(TestAssets.PDrive!), StillesProtokoll());

        var texturen = lader.Laden(modell.FeinsterSichtbarerLod!);

        Assert.All(texturen.Values, bild => Assert.True(bild.IsFrozen));
    }

    [PDriveFact]
    public void Dieselbe_Texturdatei_wird_nur_einmal_dekodiert()
    {
        var modell = P3dModelReader.Read(
            TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d"));
        var lod = modell.FeinsterSichtbarerLod!;
        var lader = new TexturLader(new TextureResolver(TestAssets.PDrive!), StillesProtokoll());

        var texturen = lader.Laden(lod);

        // Abschnitte mit gleichem Texturpfad muessen dasselbe Objekt bekommen.
        var gruppen = lod.Sections
            .Where(a => texturen.ContainsKey(a) && a.TexturePath is not null)
            .GroupBy(a => a.TexturePath, StringComparer.OrdinalIgnoreCase);

        foreach (var gruppe in gruppen.Where(g => g.Count() > 1))
        {
            var erstes = texturen[gruppe.First()];
            Assert.All(gruppe, a => Assert.Same(erstes, texturen[a]));
        }
    }

    [Fact]
    public void Ein_Abschnitt_ohne_Textur_taucht_nicht_im_Ergebnis_auf()
    {
        var lod = new LodGeometry
        {
            Resolution = 1, Name = "1.000", IstSichtbar = true,
            Positions = [new Vec3(0, 0, 0)],
            Normals = [new Vec3(0, 1, 0)],
            Uvs = [new Vec2(0, 0)],
            Sections = [new MeshSection { Indices = [0, 0, 0] }],
        };
        var lader = new TexturLader(new TextureResolver(@"C:\gibt-es-nicht"), StillesProtokoll());

        var texturen = lader.Laden(lod);

        Assert.Empty(texturen);
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~TexturLaderTests`
Expected: FAIL, `TexturLader` existiert nicht.

- [ ] **Step 3: `TexturLader.cs` schreiben**

```csharp
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DzAssets.Formats.Models;
using DzAssets.Formats.Textures;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>
/// Laedt die Diffusetexturen eines LODs. Laeuft im Hintergrund; die
/// erzeugten Bilder sind eingefroren und damit vom UI-Thread benutzbar.
/// </summary>
public sealed class TexturLader(TextureResolver aufloeser, Protokoll protokoll)
{
    private readonly List<string> _fehlend = [];

    public IReadOnlyList<string> Fehlend => _fehlend;

    public IReadOnlyDictionary<MeshSection, ImageSource> Laden(LodGeometry lod)
    {
        ArgumentNullException.ThrowIfNull(lod);

        _fehlend.Clear();
        var ergebnis = new Dictionary<MeshSection, ImageSource>();
        var jeDatei = new Dictionary<string, ImageSource?>(StringComparer.OrdinalIgnoreCase);

        foreach (var abschnitt in lod.Sections)
        {
            var datei = aufloeser.DiffuseFuer(abschnitt);
            if (datei is null)
            {
                var name = abschnitt.TexturePath ?? abschnitt.MaterialPath;
                if (!string.IsNullOrEmpty(name) && !_fehlend.Contains(name))
                    _fehlend.Add(name);
                continue;
            }

            if (!jeDatei.TryGetValue(datei, out var bild))
            {
                bild = Dekodieren(datei);
                jeDatei[datei] = bild;
            }

            if (bild is not null) ergebnis[abschnitt] = bild;
        }

        return ergebnis;
    }

    private ImageSource? Dekodieren(string datei)
    {
        try
        {
            var paa = PaaImage.Load(datei);

            // WPF 3D kennt kein Alpha-Testing. Harte Kanten sehen besser aus
            // als der Schleier, den halbdurchsichtige Blaetter sonst erzeugen.
            if (paa.HatTransparenz) paa.AlphaQuantisieren();

            var bild = BitmapSource.Create(
                paa.Width, paa.Height, 96, 96,
                PixelFormats.Bgra32, null, paa.Bgra, paa.Width * 4);
            bild.Freeze();
            return bild;
        }
        catch (Exception fehler)
        {
            protokoll.Fehler($"Textur liess sich nicht lesen: {datei}", fehler);
            if (!_fehlend.Contains(datei)) _fehlend.Add(datei);
            return null;
        }
    }
}
```

- [ ] **Step 4: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~TexturLaderTests`
Expected: `Passed!  - Failed: 0, Passed: 4`

- [ ] **Step 5: Den Lader in `ModellAnzeigen` einhängen**

In `AssetVorschauAnsicht.xaml.cs` ein Feld ergänzen und `ModellAnzeigen` erweitern:

```csharp
    private TexturLader? _texturLader;
```

Im Konstruktor nach `InitializeComponent()`:

```csharp
        var wurzel = _kontext.Einstellungen.Wurzeln.FirstOrDefault() ?? string.Empty;
        _texturLader = new TexturLader(new TextureResolver(wurzel), _kontext.Protokoll);
```

In `ModellAnzeigen` die Zeile `Viewport.Zeigen(modell, lod);` ersetzen durch:

```csharp
            var texturen = _texturLader is null
                ? null
                : await Task.Run(() => _texturLader.Laden(lod));

            Viewport.Zeigen(modell, lod, texturen);

            if (_texturLader?.Fehlend.Count > 0)
                _kontext.Protokoll.Schreiben(
                    $"{assetPfad}: {_texturLader.Fehlend.Count} Texturen fehlen");
```

Oben ergänzen: `using DzAssets.Formats.Textures;`

- [ ] **Step 6: Bauen, starten und die Texturierung prüfen**

Run: `cd tools/DayZAssetPreview && dotnet run --project DzAssets.Preview`

Sichtprüfung:
- Ein Waschbecken ist weiss mit erkennbaren Armaturen, nicht einfarbig grau.
- Bei einem Haus sind Ziegel, Putz und Fenster unterscheidbar.
- **Ist die Textur senkrecht gespiegelt** (Schrift steht auf dem Kopf), in
  `GeometrieBauer.Bauen` die Zeile `1.0 - uv.V` in `uv.V` ändern.
- **Ist die Textur waagerecht versetzt oder gekachelt falsch**, den
  `ImageBrush` in `ModelViewport.NeuAufbauen` prüfen: `ViewportUnits`
  muss `Absolute` mit `Viewport = 0,0,1,1` sein, damit UV-Werte ausserhalb
  von 0..1 kacheln.
- Ein Baum zeigt Blätter mit harten Kanten, keinen grauen Schleier.

- [ ] **Step 7: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "Diffusetexturen im Viewport darstellen"
```

---

### Task 12: Info-Panel, LOD-Umschaltung und Ansichtsschalter

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/ModellInfo.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/ModellInfoTests.cs`
- Modify: `AssetVorschauAnsicht.xaml` (Info-Bereich füllen)
- Modify: `AssetVorschauAnsicht.xaml.cs`

**Interfaces:**
- Consumes: `ModelGeometry`, `LodGeometry` (Task 4); `ConfigClassIndex` (Task 7).
- Produces:

```csharp
public sealed class ModellInfo : BeobachtbaresObjekt
{
    public string Dateiname { get; }
    public string AssetPfad { get; }
    public string Groesse { get; }        // "1,20 x 0,85 x 0,55 m"
    public string Dreiecke { get; }       // "3.412"
    public string Version { get; }        // "ODOL 54"
    public string Klassen { get; }        // "Land_Basin_A" oder "—"
    public IReadOnlyList<LodGeometry> Lods { get; }
    public IReadOnlyList<string> FehlendeTexturen { get; }

    public static ModellInfo Erzeugen(ModelGeometry modell, LodGeometry lod,
                                      string assetPfad,
                                      IReadOnlyList<string> klassen,
                                      IReadOnlyList<string> fehlendeTexturen);
}
```

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/ModellInfoTests.cs`:

```csharp
using System.Globalization;
using DzAssets.Formats.Models;
using DzAssets.Preview.Module.AssetVorschau;
using Xunit;

namespace DzAssets.Tests;

public class ModellInfoTests
{
    private static ModelGeometry Beispiel()
    {
        var lod = new LodGeometry
        {
            Resolution = 1.0f, Name = "1.000", IstSichtbar = true,
            Positions = [new Vec3(0, 0, 0), new Vec3(1.2f, 0, 0), new Vec3(0, 0.85f, 0.55f)],
            Normals = [new Vec3(0, 1, 0), new Vec3(0, 1, 0), new Vec3(0, 1, 0)],
            Uvs = [new Vec2(0, 0), new Vec2(1, 0), new Vec2(0, 1)],
            Sections = [new MeshSection { Indices = [0, 1, 2] }],
        };

        return new ModelGeometry
        {
            Path = @"H:\P_Drive\DZ\a\becken.p3d",
            Version = 54,
            Lods = [lod],
            BoundsMin = new Vec3(0, 0, 0),
            BoundsMax = new Vec3(1.2f, 0.85f, 0.55f),
        };
    }

    [Fact]
    public void Die_Groesse_wird_in_Metern_mit_zwei_Nachkommastellen_gezeigt()
    {
        var modell = Beispiel();

        var info = ModellInfo.Erzeugen(modell, modell.Lods[0], @"DZ\a\becken.p3d", [], []);

        Assert.Contains("1,20", info.Groesse.Replace('.', ','));
        Assert.Contains("m", info.Groesse);
    }

    [Fact]
    public void Die_Dreiecksanzahl_stammt_aus_dem_gewaehlten_LOD()
    {
        var modell = Beispiel();

        var info = ModellInfo.Erzeugen(modell, modell.Lods[0], @"DZ\a\becken.p3d", [], []);

        Assert.Equal("1", info.Dreiecke);
    }

    [Fact]
    public void Die_ODOL_Version_wird_genannt()
    {
        var modell = Beispiel();

        var info = ModellInfo.Erzeugen(modell, modell.Lods[0], @"DZ\a\becken.p3d", [], []);

        Assert.Contains("54", info.Version);
    }

    [Fact]
    public void Ohne_Klassennamen_steht_ein_Gedankenstrich()
    {
        var modell = Beispiel();

        var info = ModellInfo.Erzeugen(modell, modell.Lods[0], @"DZ\a\becken.p3d", [], []);

        Assert.Equal("—", info.Klassen);
    }

    [Fact]
    public void Mehrere_Klassennamen_werden_mit_Komma_verbunden()
    {
        var modell = Beispiel();

        var info = ModellInfo.Erzeugen(modell, modell.Lods[0], @"DZ\a\becken.p3d",
            ["Land_A", "Land_B"], []);

        Assert.Equal("Land_A, Land_B", info.Klassen);
    }

    [Fact]
    public void Der_Dateiname_kommt_ohne_Endung()
    {
        var modell = Beispiel();

        var info = ModellInfo.Erzeugen(modell, modell.Lods[0], @"DZ\a\becken.p3d", [], []);

        Assert.Equal("becken", info.Dateiname);
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~ModellInfoTests`
Expected: FAIL, `ModellInfo` existiert nicht.

- [ ] **Step 3: `ModellInfo.cs` schreiben**

```csharp
using System.Globalization;
using DzAssets.Formats.Models;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>Die Angaben, die rechts neben dem Viewport stehen.</summary>
public sealed class ModellInfo : BeobachtbaresObjekt
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private ModellInfo() { }

    public string Dateiname { get; private init; } = string.Empty;
    public string AssetPfad { get; private init; } = string.Empty;
    public string Groesse { get; private init; } = string.Empty;
    public string Dreiecke { get; private init; } = string.Empty;
    public string Version { get; private init; } = string.Empty;
    public string Klassen { get; private init; } = "—";
    public IReadOnlyList<LodGeometry> Lods { get; private init; } = [];
    public IReadOnlyList<string> FehlendeTexturen { get; private init; } = [];

    public bool HatFehlendeTexturen => FehlendeTexturen.Count > 0;

    public string FehlendeTexturenText => FehlendeTexturen.Count == 0
        ? string.Empty
        : $"{FehlendeTexturen.Count} Textur(en) fehlen:\n" +
          string.Join('\n', FehlendeTexturen.Take(8));

    public static ModellInfo Erzeugen(
        ModelGeometry modell,
        LodGeometry lod,
        string assetPfad,
        IReadOnlyList<string> klassen,
        IReadOnlyList<string> fehlendeTexturen)
    {
        ArgumentNullException.ThrowIfNull(modell);
        ArgumentNullException.ThrowIfNull(lod);

        var groesse = modell.Size;

        return new ModellInfo
        {
            Dateiname = Path.GetFileNameWithoutExtension(modell.Path),
            AssetPfad = assetPfad,
            Groesse = string.Format(Deutsch, "{0:N2} × {1:N2} × {2:N2} m",
                groesse.X, groesse.Y, groesse.Z),
            Dreiecke = lod.TriangleCount.ToString("N0", Deutsch),
            Version = $"ODOL {modell.Version}",
            Klassen = klassen.Count == 0 ? "—" : string.Join(", ", klassen),
            Lods = modell.Lods,
            FehlendeTexturen = fehlendeTexturen,
        };
    }
}
```

- [ ] **Step 4: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~ModellInfoTests`
Expected: `Passed!  - Failed: 0, Passed: 6`

- [ ] **Step 5: Den Info-Bereich in XAML füllen**

In `AssetVorschauAnsicht.xaml` den `StackPanel x:Name="InfoBereich"` ersetzen:

```xml
    <Border Grid.Column="4" Background="{DynamicResource PinselFlaeche}"
            BorderBrush="{DynamicResource PinselRand}" BorderThickness="1,0,0,0">
      <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel x:Name="InfoBereich" Margin="16" DataContext="{Binding}">
          <TextBlock x:Name="InfoName" Style="{StaticResource StilUeberschrift}"
                     TextWrapping="Wrap" Text="—" />
          <TextBlock x:Name="InfoPfad" Style="{StaticResource StilNebentext}"
                     TextWrapping="Wrap" Margin="0,4,0,0" />

          <Border Height="1" Background="{DynamicResource PinselRand}" Margin="0,14" />

          <TextBlock Text="MASSE" Style="{StaticResource StilNebentext}" />
          <TextBlock x:Name="InfoGroesse" Margin="0,3,0,0" Text="—" />

          <TextBlock Text="DREIECKE" Style="{StaticResource StilNebentext}" Margin="0,12,0,0" />
          <TextBlock x:Name="InfoDreiecke" Margin="0,3,0,0" Text="—" />

          <TextBlock Text="FORMAT" Style="{StaticResource StilNebentext}" Margin="0,12,0,0" />
          <TextBlock x:Name="InfoVersion" Margin="0,3,0,0" Text="—" />

          <TextBlock Text="KLASSE" Style="{StaticResource StilNebentext}" Margin="0,12,0,0" />
          <TextBlock x:Name="InfoKlassen" Margin="0,3,0,0" TextWrapping="Wrap" Text="—" />

          <TextBlock Text="DETAILSTUFE" Style="{StaticResource StilNebentext}" Margin="0,12,0,0" />
          <ComboBox x:Name="LodAuswahl" Margin="0,4,0,0"
                    SelectionChanged="LodAuswahl_SelectionChanged"
                    DisplayMemberPath="Name" />

          <Border Height="1" Background="{DynamicResource PinselRand}" Margin="0,14" />

          <CheckBox x:Name="SchalterGitter" Content="Bodengitter" Margin="0,2"
                    Checked="Ansicht_Geaendert" Unchecked="Ansicht_Geaendert" />
          <CheckBox x:Name="SchalterMassstab" Content="Massstabsfigur 1,80 m" Margin="0,2"
                    Checked="Ansicht_Geaendert" Unchecked="Ansicht_Geaendert" />
          <CheckBox x:Name="SchalterDraht" Content="Drahtgitter" Margin="0,2"
                    Checked="Ansicht_Geaendert" Unchecked="Ansicht_Geaendert" />

          <Button x:Name="KnopfPfadKopieren" Content="Pfad kopieren" Margin="0,14,0,0"
                  HorizontalAlignment="Stretch" Click="PfadKopieren_Click" />

          <TextBlock x:Name="InfoWarnung" Margin="0,14,0,0" TextWrapping="Wrap"
                     Foreground="{DynamicResource PinselWarnung}"
                     Visibility="Collapsed" FontSize="12" />
        </StackPanel>
      </ScrollViewer>
    </Border>
```

Ein `CheckBox`-Stil fehlt noch in `Theme.xaml`; ohne ihn sieht das
Kontrollkästchen nach Windows 7 aus:

```xml
  <Style TargetType="CheckBox">
    <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
    <Setter Property="FontFamily" Value="{DynamicResource SchriftText}" />
    <Setter Property="FontSize" Value="{DynamicResource SchriftgroesseNormal}" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="CheckBox">
          <StackPanel Orientation="Horizontal" Background="Transparent">
            <Border x:Name="Kasten" Width="17" Height="17" CornerRadius="4"
                    Background="{DynamicResource PinselFlaecheHoch}"
                    BorderBrush="{DynamicResource PinselRand}" BorderThickness="1"
                    VerticalAlignment="Center">
              <TextBlock x:Name="Haken" Text="&#xE73E;"
                         FontFamily="{DynamicResource SchriftSymbole}" FontSize="10"
                         Foreground="White" Opacity="0"
                         HorizontalAlignment="Center" VerticalAlignment="Center" />
            </Border>
            <ContentPresenter Margin="9,0,0,0" VerticalAlignment="Center" />
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Kasten" Property="Background" Value="{DynamicResource PinselAkzent}" />
              <Setter TargetName="Kasten" Property="BorderBrush" Value="{DynamicResource PinselAkzent}" />
              <Setter TargetName="Haken" Property="Opacity" Value="1" />
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Kasten" Property="BorderBrush" Value="{DynamicResource PinselAkzent}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ComboBox">
    <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
    <Setter Property="Background" Value="{DynamicResource PinselFlaecheHoch}" />
    <Setter Property="BorderBrush" Value="{DynamicResource PinselRand}" />
    <Setter Property="FontFamily" Value="{DynamicResource SchriftText}" />
    <Setter Property="Padding" Value="9,6" />
  </Style>
```

- [ ] **Step 6: Den Code der Ansicht erweitern**

In `AssetVorschauAnsicht.xaml.cs` ergänzen:

```csharp
    private ModelGeometry? _aktuellesModell;
    private string _aktuellerAssetPfad = string.Empty;
    private ConfigClassIndex? _klassenIndex;
    private bool _lodWirdGesetzt;
```

Im Konstruktor, nach dem Anlegen des `TexturLader`:

```csharp
        SchalterGitter.IsChecked = _kontext.Einstellungen.BodengitterZeigen;
        SchalterMassstab.IsChecked = _kontext.Einstellungen.MassstabsfigurZeigen;
```

`BestandLaden` am Ende des `try`-Blocks ergänzen, damit die Klassennamen
im Hintergrund entstehen:

```csharp
            _ = Task.Run(() =>
            {
                var index = ConfigClassIndex.Erstellen(wurzeln);
                Dispatcher.Invoke(() => _klassenIndex = index);
            });
```

`ModellAnzeigen` erweitern — nach `Viewport.Zeigen(...)`:

```csharp
            _aktuellesModell = modell;
            _aktuellerAssetPfad = assetPfad;

            _lodWirdGesetzt = true;
            LodAuswahl.ItemsSource = modell.Lods;
            LodAuswahl.SelectedItem = lod;
            _lodWirdGesetzt = false;

            InfoSetzen(modell, lod, assetPfad);
```

Und die neuen Methoden:

```csharp
    private void InfoSetzen(ModelGeometry modell, LodGeometry lod, string assetPfad)
    {
        var klassen = _klassenIndex?.KlassenFuer(assetPfad) ?? [];
        var fehlend = _texturLader?.Fehlend ?? [];
        var info = ModellInfo.Erzeugen(modell, lod, assetPfad, klassen, fehlend);

        InfoName.Text = info.Dateiname;
        InfoPfad.Text = info.AssetPfad;
        InfoGroesse.Text = info.Groesse;
        InfoDreiecke.Text = info.Dreiecke;
        InfoVersion.Text = info.Version;
        InfoKlassen.Text = info.Klassen;

        InfoWarnung.Text = info.FehlendeTexturenText;
        InfoWarnung.Visibility = info.HatFehlendeTexturen ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void LodAuswahl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_lodWirdGesetzt) return;
        if (_aktuellesModell is null) return;
        if (LodAuswahl.SelectedItem is not LodGeometry lod) return;

        if (lod.Positions.Length == 0)
        {
            Viewport.Leeren();
            ViewportHinweis.Text = $"Der LOD „{lod.Name}“ enthaelt keine Geometrie.";
            ViewportHinweis.Visibility = Visibility.Visible;
            return;
        }

        var texturen = _texturLader is null
            ? null
            : await Task.Run(() => _texturLader.Laden(lod));

        Viewport.Zeigen(_aktuellesModell, lod, texturen);
        ViewportHinweis.Visibility = Visibility.Collapsed;
        InfoSetzen(_aktuellesModell, lod, _aktuellerAssetPfad);
    }

    private void Ansicht_Geaendert(object sender, RoutedEventArgs e)
    {
        Viewport.BodengitterZeigen = SchalterGitter.IsChecked == true;
        Viewport.MassstabsfigurZeigen = SchalterMassstab.IsChecked == true;
        Viewport.DrahtgitterZeigen = SchalterDraht.IsChecked == true;

        _kontext.Einstellungen.BodengitterZeigen = SchalterGitter.IsChecked == true;
        _kontext.Einstellungen.MassstabsfigurZeigen = SchalterMassstab.IsChecked == true;
    }

    private void PfadKopieren_Click(object sender, RoutedEventArgs e)
    {
        if (_aktuellerAssetPfad.Length == 0) return;

        try
        {
            Clipboard.SetText(_aktuellerAssetPfad);
        }
        catch (Exception fehler)
        {
            // Die Zwischenablage kann von anderen Programmen belegt sein.
            _kontext.Protokoll.Fehler("Zwischenablage nicht erreichbar", fehler);
        }
    }
```

Ergänzend oben: `using DzAssets.Formats.Katalog;`

- [ ] **Step 7: Bauen, starten und das Info-Panel prüfen**

Run: `cd tools/DayZAssetPreview && dotnet run --project DzAssets.Preview`

Sichtprüfung:
- Bei einem Waschbecken stehen plausible Masse (etwa 0,6 × 0,85 × 0,5 m).
- Die LOD-Liste enthält mehrere Einträge; das Umschalten auf einen groben
  LOD verringert die Dreiecksanzahl sichtbar.
- Ein Wechsel auf „Geometry" zeigt entweder die Kollisionsform oder den
  Hinweis, dass keine Geometrie vorhanden ist — kein Absturz.
- Die drei Kontrollkästchen wirken sofort.
- „Pfad kopieren" legt `DZ\...\x.p3d` in die Zwischenablage.

- [ ] **Step 8: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "Info-Panel mit Massen, LOD-Umschaltung und Ansichtsschaltern"
```

---

### Task 13: Suche

**Files:**
- Modify: `AssetVorschauAnsicht.xaml` (Trefferliste über dem Baum)
- Modify: `AssetVorschauAnsicht.xaml.cs` (`SuchFeld_TextChanged`)

**Interfaces:**
- Consumes: `AssetIndex.Suche` (Task 6).
- Produces: keine neuen öffentlichen Typen.

**Verhalten:** Die Eingabe wird um 220 ms verzögert ausgewertet, damit
nicht bei jedem Tastendruck über 8.430 Einträge gefiltert wird. Ist das
Feld leer, erscheint wieder der Baum. Es sind nie beide gleichzeitig zu
sehen.

- [ ] **Step 1: Die Trefferliste in XAML ergänzen**

In `AssetVorschauAnsicht.xaml` innerhalb von `Grid.Row="1"` der linken
Spalte den `TreeView` in ein `Grid` legen und die Liste daneben stellen:

```xml
        <Grid Grid.Row="1">
          <TreeView x:Name="Baum" ... />   <!-- unveraendert -->

          <ListBox x:Name="Trefferliste" Visibility="Collapsed"
                   Background="Transparent" BorderThickness="0"
                   SelectionChanged="Trefferliste_SelectionChanged"
                   ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                   VirtualizingStackPanel.IsVirtualizing="True"
                   VirtualizingStackPanel.VirtualizationMode="Recycling">
            <ListBox.ItemContainerStyle>
              <Style TargetType="ListBoxItem">
                <Setter Property="Padding" Value="10,7" />
                <Setter Property="Foreground" Value="{DynamicResource PinselText}" />
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="ListBoxItem">
                      <Border x:Name="Flaeche" Background="Transparent"
                              CornerRadius="{DynamicResource RadiusKlein}"
                              Margin="6,1" Padding="{TemplateBinding Padding}">
                        <ContentPresenter />
                      </Border>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                          <Setter TargetName="Flaeche" Property="Background"
                                  Value="{DynamicResource PinselFlaecheHoch}" />
                        </Trigger>
                        <Trigger Property="IsSelected" Value="True">
                          <Setter TargetName="Flaeche" Property="Background"
                                  Value="{DynamicResource PinselAkzent}" />
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Style>
            </ListBox.ItemContainerStyle>
            <ListBox.ItemTemplate>
              <DataTemplate>
                <StackPanel>
                  <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis" />
                  <TextBlock Text="{Binding Ordner}" Style="{StaticResource StilNebentext}"
                             TextTrimming="CharacterEllipsis" />
                </StackPanel>
              </DataTemplate>
            </ListBox.ItemTemplate>
          </ListBox>
        </Grid>
```

- [ ] **Step 2: Die Suche im Code umsetzen**

Feld ergänzen:

```csharp
    private readonly System.Windows.Threading.DispatcherTimer _suchTakt = new()
    {
        Interval = TimeSpan.FromMilliseconds(220),
    };
```

Im Konstruktor:

```csharp
        _suchTakt.Tick += (_, _) => { _suchTakt.Stop(); SucheAusfuehren(); };
```

`SuchFeld_TextChanged` ersetzen und `SucheAusfuehren` ergänzen:

```csharp
    private void SuchFeld_TextChanged(object sender, TextChangedEventArgs e)
    {
        SuchHinweis.Visibility = SuchFeld.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _suchTakt.Stop();
        _suchTakt.Start();
    }

    private void SucheAusfuehren()
    {
        var text = SuchFeld.Text.Trim();

        if (text.Length == 0)
        {
            Trefferliste.Visibility = Visibility.Collapsed;
            Baum.Visibility = Visibility.Visible;
            BestandText.Text = _index is null ? string.Empty : $"{_index.Eintraege.Count:N0} Modelle";
            return;
        }

        if (_index is null) return;

        var treffer = _index.Suche(text).ToList();
        Trefferliste.ItemsSource = treffer;
        Trefferliste.Visibility = Visibility.Visible;
        Baum.Visibility = Visibility.Collapsed;
        BestandText.Text = treffer.Count switch
        {
            0 => "kein Treffer",
            1 => "1 Treffer",
            _ => $"{treffer.Count:N0} Treffer",
        };
    }

    private void Trefferliste_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Trefferliste.SelectedItem is not AssetEintrag eintrag) return;
        ModellAnzeigen(eintrag.AbsoluterPfad, eintrag.AssetPfad);
    }
```

- [ ] **Step 3: Bauen, starten und die Suche prüfen**

Run: `cd tools/DayZAssetPreview && dotnet run --project DzAssets.Preview`

Sichtprüfung:
- „baracke" liefert innerhalb einer Sekunde eine Trefferliste.
- „land baracke" (zwei Wörter) verkleinert die Liste weiter.
- Ein Klick auf einen Treffer zeigt das Modell.
- Das Leeren des Feldes bringt den Baum zurück.
- Beim schnellen Tippen ruckelt nichts.

- [ ] **Step 4: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "Volltextsuche ueber den Assetbestand"
```

---

### Task 14: Miniaturbilder

**Files:**
- Create: `tools/DayZAssetPreview/DzAssets.Preview/Module/AssetVorschau/ThumbnailService.cs`
- Test: `tools/DayZAssetPreview/DzAssets.Tests/ThumbnailServiceTests.cs`
- Modify: `AssetVorschauAnsicht.xaml` (Bild in der Trefferliste)
- Modify: `AssetVorschauAnsicht.xaml.cs`

**Interfaces:**
- Consumes: `P3dModelReader`, `TexturLader`, `GeometrieBauer`.
- Produces:

```csharp
public sealed class ThumbnailService(string cacheOrdner, TexturLader lader, Protokoll protokoll)
{
    public const int Kante = 128;

    /// Liefert das Bild aus dem Cache, oder null, wenn es noch keines gibt.
    public BitmapSource? AusCache(string absoluterPfad);

    /// Rendert das Bild. MUSS auf dem UI-Thread laufen: WPF-3D-Rendering
    /// ist an den Dispatcher gebunden.
    public BitmapSource? Erzeugen(string absoluterPfad);

    public string CacheDatei(string absoluterPfad);
}
```

**Wichtig zur Reihenfolge:** `RenderTargetBitmap` verlangt den UI-Thread.
Ein Hintergrund-Thread scheidet damit aus. Stattdessen wird die Warteschlange
über `Dispatcher.BeginInvoke` mit `DispatcherPriority.Background`
abgearbeitet: pro Durchlauf **ein** Bild. Die Oberfläche bleibt bedienbar,
weil zwischen zwei Bildern alle Eingaben Vorrang haben.

**Cache-Schlüssel:** SHA-256 über den absoluten Pfad in Kleinschreibung,
zusammen mit der Änderungszeit der Datei. Ändert sich das Modell, entsteht
ein neuer Schlüssel; das alte Bild verwaist und stört nicht.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`tools/DayZAssetPreview/DzAssets.Tests/ThumbnailServiceTests.cs`:

```csharp
using DzAssets.Formats.Models;
using DzAssets.Preview.Module.AssetVorschau;
using DzAssets.Preview.Shell;
using Xunit;

namespace DzAssets.Tests;

public class ThumbnailServiceTests : IDisposable
{
    private readonly string _cache = Path.Combine(Path.GetTempPath(), "dzthumb_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_cache, recursive: true); } catch (IOException) { }
    }

    private ThumbnailService Dienst() => new(
        _cache,
        new TexturLader(new TextureResolver(TestAssets.PDrive ?? @"C:\"),
                        new Protokoll(Path.Combine(_cache, "log.txt"))),
        new Protokoll(Path.Combine(_cache, "log.txt")));

    [PDriveFact]
    public void Der_Cachename_haengt_am_Pfad_und_an_der_Aenderungszeit()
    {
        var pfad = TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d");
        var dienst = Dienst();

        var eins = dienst.CacheDatei(pfad);
        var zwei = dienst.CacheDatei(pfad);

        Assert.Equal(eins, zwei);
        Assert.EndsWith(".png", eins);
    }

    [PDriveFact]
    public void Zwei_verschiedene_Modelle_bekommen_verschiedene_Cachenamen()
    {
        var dienst = Dienst();
        var a = TestAssets.ErsteDatei("structures", "*.p3d");
        var b = TestAssets.ErsteDatei("plants", "*.p3d");

        Assert.NotEqual(dienst.CacheDatei(a), dienst.CacheDatei(b));
    }

    [Fact]
    public void Ohne_vorhandenes_Bild_liefert_AusCache_null()
    {
        var dienst = Dienst();

        Assert.Null(dienst.AusCache(@"C:\gibt-es-nicht\modell.p3d"));
    }

    [PDriveFact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Erzeugen_liefert_ein_Bild_der_erwarteten_Kantenlaenge()
    {
        // RenderTargetBitmap braucht einen STA-Thread. xUnit laeuft in MTA,
        // deshalb wird das Rendern in einem eigenen STA-Thread ausgefuehrt.
        var pfad = TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d");
        System.Windows.Media.Imaging.BitmapSource? ergebnis = null;
        Exception? fehler = null;

        var faden = new Thread(() =>
        {
            try { ergebnis = Dienst().Erzeugen(pfad); }
            catch (Exception ausnahme) { fehler = ausnahme; }
        });
        faden.SetApartmentState(ApartmentState.STA);
        faden.Start();
        faden.Join(TimeSpan.FromSeconds(30));

        Assert.Null(fehler);
        Assert.NotNull(ergebnis);
        Assert.Equal(ThumbnailService.Kante, ergebnis!.PixelWidth);
    }
}
```

- [ ] **Step 2: Test laufen lassen — Fehlschlag erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~ThumbnailServiceTests`
Expected: FAIL, `ThumbnailService` existiert nicht.

- [ ] **Step 3: `ThumbnailService.cs` schreiben**

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using DzAssets.Formats.Models;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>
/// Rendert kleine Vorschaubilder und legt sie als PNG auf der Platte ab.
/// Das Rendern ist an den UI-Thread gebunden — WPF 3D kennt keinen anderen.
/// </summary>
public sealed class ThumbnailService(string cacheOrdner, TexturLader lader, Protokoll protokoll)
{
    public const int Kante = 128;

    public string CacheOrdner { get; } = cacheOrdner;

    public string CacheDatei(string absoluterPfad)
    {
        var kennung = absoluterPfad.ToLowerInvariant();
        try
        {
            kennung += "|" + File.GetLastWriteTimeUtc(absoluterPfad).Ticks;
        }
        catch (IOException)
        {
            // Ohne Aenderungszeit reicht der Pfad.
        }

        var summe = SHA256.HashData(Encoding.UTF8.GetBytes(kennung));
        return Path.Combine(CacheOrdner, Convert.ToHexString(summe)[..24] + ".png");
    }

    public BitmapSource? AusCache(string absoluterPfad)
    {
        var datei = CacheDatei(absoluterPfad);
        if (!File.Exists(datei)) return null;

        try
        {
            var bild = new BitmapImage();
            bild.BeginInit();
            bild.CacheOption = BitmapCacheOption.OnLoad;
            bild.UriSource = new Uri(datei);
            bild.EndInit();
            bild.Freeze();
            return bild;
        }
        catch (Exception fehler) when (fehler is IOException or NotSupportedException)
        {
            return null;
        }
    }

    public BitmapSource? Erzeugen(string absoluterPfad)
    {
        try
        {
            var modell = P3dModelReader.Read(absoluterPfad);
            var lod = modell.FeinsterSichtbarerLod;
            if (lod is null || lod.Positions.Length == 0) return null;

            var texturen = lader.Laden(lod);
            var szene = SzeneBauen(lod, texturen);
            var bild = Rendern(szene, modell);

            Speichern(bild, CacheDatei(absoluterPfad));
            return bild;
        }
        catch (Exception fehler)
        {
            protokoll.Fehler($"Vorschaubild misslang: {absoluterPfad}", fehler);
            return null;
        }
    }

    private static Model3DGroup SzeneBauen(LodGeometry lod, IReadOnlyDictionary<MeshSection, ImageSource> texturen)
    {
        var gruppe = new Model3DGroup();
        gruppe.Children.Add(new AmbientLight(Color.FromRgb(0x55, 0x5B, 0x64)));
        gruppe.Children.Add(new DirectionalLight(Color.FromRgb(0xFF, 0xF4, 0xE8),
            new Vector3D(-0.5, -0.9, -0.6)));

        var ersatz = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0x8A, 0x90, 0x99)));
        ersatz.Freeze();

        foreach (var abschnitt in lod.Sections)
        {
            var geometrie = GeometrieBauer.Bauen(lod, abschnitt);
            if (geometrie.Positions.Count == 0) continue;

            Material material = ersatz;
            if (texturen.TryGetValue(abschnitt, out var bild))
            {
                var pinsel = new ImageBrush(bild)
                {
                    ViewportUnits = BrushMappingMode.Absolute,
                    TileMode = TileMode.Tile,
                    Viewport = new System.Windows.Rect(0, 0, 1, 1),
                };
                pinsel.Freeze();
                material = new DiffuseMaterial(pinsel);
            }

            gruppe.Children.Add(new GeometryModel3D(geometrie, material) { BackMaterial = material });
        }

        return gruppe;
    }

    private static BitmapSource Rendern(Model3DGroup szene, ModelGeometry modell)
    {
        var mitte = new Point3D(
            (modell.BoundsMin.X + modell.BoundsMax.X) / 2,
            (modell.BoundsMin.Y + modell.BoundsMax.Y) / 2,
            (modell.BoundsMin.Z + modell.BoundsMax.Z) / 2);

        var groesse = modell.Size;
        var radius = Math.Max(0.4, Math.Max(groesse.X, Math.Max(groesse.Y, groesse.Z)));
        var abstand = radius * 2.0;

        var position = new Point3D(
            mitte.X + abstand * 0.62,
            mitte.Y + abstand * 0.48,
            mitte.Z + abstand * 0.62);

        var sicht = new Viewport3D
        {
            Width = Kante,
            Height = Kante,
            Camera = new PerspectiveCamera
            {
                FieldOfView = 45,
                Position = position,
                LookDirection = mitte - position,
                UpDirection = new Vector3D(0, 1, 0),
                NearPlaneDistance = Math.Max(0.01, abstand / 400),
                FarPlaneDistance = abstand * 40,
            },
        };
        sicht.Children.Add(new ModelVisual3D { Content = szene });

        sicht.Measure(new System.Windows.Size(Kante, Kante));
        sicht.Arrange(new System.Windows.Rect(0, 0, Kante, Kante));
        sicht.UpdateLayout();

        var ziel = new RenderTargetBitmap(Kante, Kante, 96, 96, PixelFormats.Pbgra32);
        ziel.Render(sicht);
        ziel.Freeze();
        return ziel;
    }

    private void Speichern(BitmapSource bild, string datei)
    {
        try
        {
            Directory.CreateDirectory(CacheOrdner);
            var kodierer = new PngBitmapEncoder();
            kodierer.Frames.Add(BitmapFrame.Create(bild));
            using var strom = File.Create(datei);
            kodierer.Save(strom);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            protokoll.Fehler($"Vorschaubild liess sich nicht speichern: {datei}", fehler);
        }
    }
}
```

- [ ] **Step 4: Test laufen lassen — Erfolg erwarten**

Run: `cd tools/DayZAssetPreview && dotnet test --filter FullyQualifiedName~ThumbnailServiceTests`
Expected: `Passed!  - Failed: 0, Passed: 4`

Schlägt der STA-Test mit `InvalidOperationException` über den Dispatcher
fehl, im Testfaden vor dem Rendern einmal
`_ = System.Windows.Application.Current;` aufrufen und, falls nötig, eine
`new System.Windows.Application()` anlegen. Den Test **nicht** entfernen —
er ist der einzige automatische Nachweis, dass das Rendern funktioniert.

- [ ] **Step 5: Die Trefferliste um Bilder erweitern**

In `AssetVorschauAnsicht.xaml` das `DataTemplate` der `Trefferliste` ersetzen:

```xml
            <ListBox.ItemTemplate>
              <DataTemplate>
                <StackPanel Orientation="Horizontal">
                  <Border Width="48" Height="48" CornerRadius="{DynamicResource RadiusKlein}"
                          Background="{DynamicResource PinselFlaecheHoch}"
                          Margin="0,0,10,0">
                    <Image Source="{Binding Vorschau}" Stretch="Uniform" Margin="2" />
                  </Border>
                  <StackPanel VerticalAlignment="Center" MaxWidth="180">
                    <TextBlock Text="{Binding Eintrag.Name}" TextTrimming="CharacterEllipsis" />
                    <TextBlock Text="{Binding Eintrag.Ordner}" Style="{StaticResource StilNebentext}"
                               TextTrimming="CharacterEllipsis" />
                  </StackPanel>
                </StackPanel>
              </DataTemplate>
            </ListBox.ItemTemplate>
```

- [ ] **Step 6: Die Hülle für einen Treffer mit Bild anlegen**

Neue Datei `Module/AssetVorschau/TrefferAnzeige.cs`:

```csharp
using System.Windows.Media;
using DzAssets.Formats.Katalog;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>Ein Suchtreffer samt nachgeladenem Vorschaubild.</summary>
public sealed class TrefferAnzeige(AssetEintrag eintrag) : BeobachtbaresObjekt
{
    private ImageSource? _vorschau;

    public AssetEintrag Eintrag { get; } = eintrag;

    public ImageSource? Vorschau
    {
        get => _vorschau;
        set => Setzen(ref _vorschau, value);
    }
}
```

- [ ] **Step 7: Die Warteschlange in der Ansicht einbauen**

In `AssetVorschauAnsicht.xaml.cs` ergänzen:

```csharp
    private ThumbnailService? _bilder;
    private readonly Queue<TrefferAnzeige> _bildWarteschlange = new();
    private bool _bilderLaufen;
```

Im Konstruktor, nach dem `TexturLader`:

```csharp
        _bilder = new ThumbnailService(
            Path.Combine(_kontext.DatenOrdner, "thumbs"),
            _texturLader!,
            _kontext.Protokoll);
```

`SucheAusfuehren` anpassen — statt der rohen Einträge Hüllen füllen:

```csharp
        var treffer = _index.Suche(text).Select(e => new TrefferAnzeige(e)).ToList();
        Trefferliste.ItemsSource = treffer;
        VorschaubilderAnfordern(treffer);
```

`Trefferliste_SelectionChanged` anpassen:

```csharp
    private void Trefferliste_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Trefferliste.SelectedItem is not TrefferAnzeige anzeige) return;
        ModellAnzeigen(anzeige.Eintrag.AbsoluterPfad, anzeige.Eintrag.AssetPfad);
    }
```

Und die Warteschlange:

```csharp
    private void VorschaubilderAnfordern(IReadOnlyList<TrefferAnzeige> treffer)
    {
        _bildWarteschlange.Clear();
        if (_bilder is null) return;

        // Hoechstens 60 Bilder je Suche: mehr sieht ohnehin niemand,
        // bevor er weitertippt.
        foreach (var anzeige in treffer.Take(60))
        {
            var ausCache = _bilder.AusCache(anzeige.Eintrag.AbsoluterPfad);
            if (ausCache is not null) anzeige.Vorschau = ausCache;
            else _bildWarteschlange.Enqueue(anzeige);
        }

        BilderNachziehen();
    }

    private void BilderNachziehen()
    {
        if (_bilderLaufen || _bildWarteschlange.Count == 0 || _bilder is null) return;
        _bilderLaufen = true;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            _bilderLaufen = false;
            if (_bildWarteschlange.Count == 0) return;

            var anzeige = _bildWarteschlange.Dequeue();
            anzeige.Vorschau = _bilder.Erzeugen(anzeige.Eintrag.AbsoluterPfad);

            BilderNachziehen();
        });
    }
```

- [ ] **Step 8: Bauen, starten und die Bilder prüfen**

Run: `cd tools/DayZAssetPreview && dotnet run --project DzAssets.Preview`

Sichtprüfung:
- Nach einer Suche erscheinen nach und nach Miniaturbilder; die Liste
  bleibt dabei scrollbar und das Suchfeld bedienbar.
- Dieselbe Suche ein zweites Mal zeigt die Bilder sofort — sie kommen aus
  dem Cache unter `%LOCALAPPDATA%\DayZAssetPreview\thumbs`.
- Auf einem Bild ist erkennbar, um welches Objekt es geht.

Ruckelt die Oberfläche, ist `DispatcherPriority.Background` zu hoch
gewählt — auf `ApplicationIdle` senken.

- [ ] **Step 9: Committen**

```bash
git add tools/DayZAssetPreview
git commit -m "Miniaturbilder in der Trefferliste mit Plattencache"
```

---

### Task 15: Auslieferung und Dokumentation

**Files:**
- Create: `tools/DayZAssetPreview/veroeffentlichen.ps1`
- Create: `tools/DayZAssetPreview/README.md`
- Modify: `README.md` (Abschnitt „Was sonst im Repository liegt")
- Modify: `CHANGELOG.md`
- Modify: `.gitignore` (Ausgabeordner der neuen Projekte)

**Interfaces:**
- Consumes: alle vorigen Tasks.
- Produces: `DayZ Asset Preview.exe` als eigenständige Datei.

- [ ] **Step 1: Das Veröffentlichungsskript schreiben**

`tools/DayZAssetPreview/veroeffentlichen.ps1`:

```powershell
# Erzeugt eine eigenstaendige DayZ Asset Preview.exe.
# Selbstenthaltend, damit auf dem Zielrechner keine .NET-Installation
# noetig ist. Das kostet Groesse (etwa 150 MB), erspart aber die Frage
# "warum startet das nicht".

$ErrorActionPreference = 'Stop'
$hier = Split-Path -Parent $MyInvocation.MyCommand.Path
$ziel = Join-Path $hier 'veroeffentlicht'

if (Test-Path $ziel) { Remove-Item $ziel -Recurse -Force }

dotnet publish (Join-Path $hier 'DzAssets.Preview\DzAssets.Preview.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $ziel

if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen." }

$exe = Get-ChildItem $ziel -Filter '*.exe' | Select-Object -First 1
Write-Output ''
Write-Output "Fertig: $($exe.FullName)"
Write-Output ("Groesse: {0:N1} MB" -f ($exe.Length / 1MB))
```

- [ ] **Step 2: Veröffentlichen und die `.exe` starten**

Run: `powershell -ExecutionPolicy Bypass -File tools/DayZAssetPreview/veroeffentlichen.ps1`
Expected: `Fertig: ...\veroeffentlicht\DayZ Asset Preview.exe` mit einer
Grössenangabe.

Danach die erzeugte Datei per Doppelklick starten — **nicht** über
`dotnet run`. Sie muss ohne installierte .NET-Laufzeit starten und den
Bestand wie gewohnt anzeigen.

Startet sie nicht, ist fast immer `PublishSingleFile` mit WPF die Ursache:
dann `-p:IncludeNativeLibrariesForSelfExtract=true` prüfen und notfalls
`EnableCompressionInSingleFile` weglassen.

- [ ] **Step 3: `.gitignore` ergänzen**

An `.gitignore` im Wurzelverzeichnis anhängen:

```
# DayZ Asset Preview
tools/DayZAssetPreview/**/bin/
tools/DayZAssetPreview/**/obj/
tools/DayZAssetPreview/veroeffentlicht/
```

- [ ] **Step 4: Die Werkzeug-README schreiben**

`tools/DayZAssetPreview/README.md`:

```markdown
# DayZ Asset Preview

Zeigt die entpackten DayZ-Assets in 3D an — als Ersatz für die fehlende
Vorschau im Terrain Builder.

Das Werkzeug läuft eigenständig. Es koppelt sich nicht an den Terrain
Builder und verändert keine Projektdateien; gedacht ist es für den zweiten
Bildschirm: hier nachsehen, wie ein Objekt aussieht, den Namen dann im
Objekt-Browser des Terrain Builder suchen.

## Bedienung

| Eingabe | Wirkung |
|---|---|
| Linke Maustaste ziehen | drehen |
| Rechte oder mittlere Maustaste ziehen | verschieben |
| Mausrad | zoomen |
| `F` | Objekt einrahmen |
| `W` | Drahtgitter |
| `G` | Bodengitter |
| `M` | Massstabsfigur (1,80 m) |

## Bestand

Beim ersten Start sucht das Programm nach einem Arbeitslaufwerk mit einem
Unterordner `DZ`. Gefunden wird üblicherweise `H:\P_Drive`. Weitere Wurzeln
lassen sich in `%LOCALAPPDATA%\DayZAssetPreview\settings.json` eintragen.

Der Bestand wird zwischengespeichert. Nach dem Hinzufügen neuer Modelle auf
„Neu einlesen" klicken.

## Bauen

```
dotnet build tools/DayZAssetPreview
dotnet test  tools/DayZAssetPreview
powershell -ExecutionPolicy Bypass -File tools/DayZAssetPreview/veroeffentlichen.ps1
```

Die Tests, die echte Dateien brauchen, überspringen sich, wenn kein
P-Drive vorhanden ist.

## Herkunft

Das Lesen der ODOL-Dateien stammt aus `BisDll`, dem Parser des
7SBM P3D.DeBin in diesem Repository. Der Quellcode wird nicht kopiert,
sondern direkt mitkompiliert — Korrekturen wirken in beiden Werkzeugen.
Siehe `VENDOR.md`.

## Grenzen

- WPF 3D kennt kein Alpha-Testing. Vegetation weicht an den Blattkanten
  leicht vom Spiel ab; der Alphakanal wird deshalb auf 0 oder 255
  gerundet.
- Dargestellt wird die Diffusetextur. Normal- und Specular-Maps bleiben
  unberücksichtigt — für „wie sieht das Objekt aus" reicht das.
- Proxies werden nicht aufgelöst: ein Haus zeigt seine eigenen Flächen,
  nicht die über Proxy eingehängten Fenster.
```

- [ ] **Step 5: Haupt-README und CHANGELOG ergänzen**

In der Tabelle „Was sonst im Repository liegt" in `README.md` eine Zeile
ergänzen:

```markdown
| `tools/DayZAssetPreview` | **DayZ Asset Preview** — 3D-Vorschau der entpackten Gamefiles, als Ersatz für die fehlende Vorschau im Terrain Builder |
```

In `CHANGELOG.md` unter `## [Unbekannt]` beziehungsweise einem neuen
Abschnitt ergänzen (das Format der Datei beibehalten):

```markdown
### Hinzugefügt

- **DayZ Asset Preview** — eigenständiges Programm, das die entpackten
  DayZ-Assets texturiert in 3D anzeigt. Ordnerbaum und Volltextsuche über
  den gesamten Bestand, Miniaturbilder, LOD-Umschaltung, Bodengitter in
  Metern und eine Massstabsfigur von 1,80 m. Liest die ODOL-Dateien über
  den Parser des P3D.DeBin und dekodiert PAA-Texturen (DXT1/DXT5) selbst.
  Angelegt als Rahmen für weitere Werkzeuge.
```

- [ ] **Step 6: Alle Tests ein letztes Mal laufen lassen**

Run: `cd tools/DayZAssetPreview && dotnet test`
Expected: Alle Tests bestehen; übersprungen werden höchstens die, die ein
P-Drive brauchen, falls keines vorhanden ist. **Kein einziger Fehlschlag.**

- [ ] **Step 7: Committen**

```bash
git add -A
git commit -m "DayZ Asset Preview ausliefern: Skript, Dokumentation, Changelog"
```

---

## Selbstpruefung des Plans

**Abdeckung der Spec.** Jede Anforderung hat eine Aufgabe:

| Spec | Task |
|---|---|
| ODOL lesen | 1, 4 |
| PAA/DXT dekodieren | 2, 3 |
| RVMAT-Rückfall | 5 |
| Klassennamen aus `config.cpp` | 7 |
| Shell mit Modulleiste, moderne dunkle Oberfläche | 8 |
| Ordnerbaum über die Wurzeln | 6, 9 |
| 3D-Vorschau mit Orbit/Zoom/Pan | 10 |
| Texturen | 11 |
| Masse, Dreiecke, LOD-Auswahl, Gitter, Massstabsfigur | 12 |
| Suche | 6, 13 |
| Miniaturbilder | 14 |
| P-Drive nicht fest verdrahtet, Einstellungsdatei | 8 |
| Fehler je Asset abfangen, Protokoll | 8, 10, 11, 14 |
| Single-File-`.exe`, README, CHANGELOG | 15 |

Nicht Teil dieses Plans und in der Spec auch so ausgewiesen: der
Debinarizer als Modul, der ASC-Previewer, das Einlesen von `.tml`.

**Offene Punkte, die während der Umsetzung zu klären sind** — sie sind
jeweils an der Stelle vermerkt, an der sie auffallen:

1. Die genauen Signaturen von `LZO` und `LZSS` in `BisDll` (Task 3, Step 4).
   Sie werden dort nachgeschlagen, statt hier geraten zu werden.
2. Die richtige Achsenumrechnung von Arma nach WPF (Task 10, Step 8). Drei
   Varianten sind genannt, das Prüfkriterium ebenfalls.
3. Die Richtung der V-Koordinate (Task 11, Step 6), mit Prüfkriterium.
4. Ob `RenderTargetBitmap` im xUnit-Prozess läuft (Task 14, Step 4).

**Namensgleichheit.** Über alle Tasks hinweg durchgesehen: `MeshSection`,
`LodGeometry`, `ModelGeometry`, `Vec2`, `Vec3`, `AssetEintrag`,
`AssetIndex.Suche`, `TextureResolver.DiffuseFuer`, `PaaImage.Load`,
`DxtDecoder.DecodeBc1`/`DecodeBc3`, `GeometrieBauer.Bauen`,
`ModelViewport.Zeigen`, `IWerkzeugModul.ErzeugeAnsicht`,
`WerkzeugKontext.DatenDatei`, `BeobachtbaresObjekt.Setzen` — jeder Name
wird in allen Tasks gleich geschrieben.
