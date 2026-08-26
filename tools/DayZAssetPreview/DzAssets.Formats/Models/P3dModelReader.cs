using System.IO;
using BisDll.Model;
using MlodLod = BisDll.Model.MLOD.MLOD_LOD;
using OdolLod = BisDll.Model.ODOL.LOD;

namespace DzAssets.Formats.Models;

/// <summary>
/// Uebersetzt eine P3D-Datei in eine renderfertige Geometrie — sowohl
/// ODOL (binarisiert, wie ausgeliefert) als auch MLOD (debinarisiert).
/// Beide kommen auf einem Arbeitslaufwerk nebeneinander vor.
/// Kennt kein WPF und keine Bildformate.
/// </summary>
public static class P3dModelReader
{
    /// <summary>Modelle oberhalb dieser Groesse werden von der Oberflaeche erst nach Rueckfrage geladen.</summary>
    public const long WarnGroesseBytes = 200L * 1024 * 1024;

    public static ModelGeometry Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Modell nicht gefunden.", path);

        // Achtung: hier NICHT Console.SetError aufrufen. BisDll meldet beim
        // Lesen ueber Console.Error, aber der Strom ist prozessweit; die
        // Anwendung leitet ihn einmal beim Start ins Protokoll um.
        var modell = P3D.GetInstance(path);

        var lods = new List<LodGeometry>();
        foreach (var roh in modell.LODs ?? [])
        {
            var lod = roh switch
            {
                OdolLod odol => LiesOdolLod(odol),
                MlodLod mlod => LiesMlodLod(mlod),
                _ => null,
            };

            if (lod is not null) lods.Add(lod);
        }

        var alle = lods.ToArray();
        var (min, max) = Grenzen(alle);

        return new ModelGeometry
        {
            Path = path,
            Version = modell.Version,
            IstBinarisiert = modell is BisDll.Model.ODOL.ODOL,
            Lods = alle,
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

        if (bezug is null) return (default, default);

        var min = bezug.Positions[0];
        var max = bezug.Positions[0];
        foreach (var punkt in bezug.Positions)
        {
            min = Vec3.Min(min, punkt);
            max = Vec3.Max(max, punkt);
        }
        return (min, max);
    }

    // ---------------------------------------------------------------- ODOL

    private static LodGeometry LiesOdolLod(OdolLod lod)
    {
        var quellPunkte = lod.Vertices ?? [];
        var positionen = new Vec3[quellPunkte.Length];
        for (var i = 0; i < positionen.Length; i++)
        {
            var v = quellPunkte[i];
            positionen[i] = v is null ? default : NachWpf(v.X, v.Y, v.Z);
        }

        var quellNormalen = lod.Normals;
        var normalen = new Vec3[positionen.Length];
        for (var i = 0; i < normalen.Length; i++)
        {
            if (quellNormalen is not null && i < quellNormalen.Length && quellNormalen[i] is not null)
            {
                var n = quellNormalen[i];
                // ODOL speichert Normalen entgegengesetzt zur Flaechenrichtung.
                normalen[i] = new Vec3(-n.X, -n.Y, n.Z);
            }
            else
            {
                normalen[i] = new Vec3(0, 1, 0);
            }
        }

        var uvs = new Vec2[positionen.Length];
        var uvSaetze = lod.UVSets;
        if (uvSaetze is { Length: > 0 } && uvSaetze[0] is not null)
        {
            var roh = uvSaetze[0].UVData;
            for (var i = 0; i < uvs.Length && i * 2 + 1 < roh.Length; i++)
                uvs[i] = new Vec2(roh[i * 2], roh[i * 2 + 1]);
        }

        return new LodGeometry
        {
            Resolution = lod.Resolution,
            Name = LodName(lod),
            IstSichtbar = Resolution.IsVisual(lod.Resolution),
            Positions = positionen,
            Normals = normalen,
            Uvs = uvs,
            Sections = LiesOdolAbschnitte(lod, positionen.Length),
        };
    }

    private static MeshSection[] LiesOdolAbschnitte(OdolLod lod, int vertexAnzahl)
    {
        var flaechen = lod.Faces ?? [];
        var texturen = lod.Textures ?? [];
        var materialien = lod.Materials ?? [];
        var abschnitte = lod.Sections ?? [];

        // Ein Proxy nennt den Abschnitt, in dem sein Platzhalter liegt.
        // Diese Abschnitte enthalten die Pyramide mit Pfeil, nicht das
        // eingehaengte Modell — sie gehoeren nicht zum sichtbaren Objekt.
        var proxyAbschnitte = new HashSet<int>();
        foreach (var proxy in lod.Proxies ?? [])
        {
            if (proxy is not null && proxy.sectionIndex >= 0)
                proxyAbschnitte.Add(proxy.sectionIndex);
        }

        var ergebnis = new List<MeshSection>(abschnitte.Length);
        var nummer = -1;

        foreach (var abschnitt in abschnitte)
        {
            nummer++;
            if (abschnitt is null) continue;

            var indizes = new List<int>();

            foreach (var flaechenIndex in abschnitt.getFaceIndexes(flaechen))
            {
                if (flaechenIndex >= flaechen.Length) continue;

                var ecken = flaechen[flaechenIndex]?.VertexIndices;
                if (ecken is null) continue;

                if (ecken.Length >= 3)
                    FuegeDreieckHinzu(indizes, ecken[0], ecken[1], ecken[2], vertexAnzahl);
                if (ecken.Length == 4)
                    FuegeDreieckHinzu(indizes, ecken[0], ecken[2], ecken[3], vertexAnzahl);
            }

            if (indizes.Count == 0) continue;

            string? texturPfad = null;
            if (abschnitt.textureIndex >= 0 && abschnitt.textureIndex < texturen.Length)
                texturPfad = OhneLeer(texturen[abschnitt.textureIndex]);

            string? materialPfad = null;
            if (abschnitt.materialIndex >= 0 && abschnitt.materialIndex < materialien.Length)
                materialPfad = OhneLeer(materialien[abschnitt.materialIndex]?.materialName);

            ergebnis.Add(new MeshSection
            {
                Indices = indizes.ToArray(),
                TexturePath = texturPfad,
                MaterialPath = materialPfad,
                IstProxy = proxyAbschnitte.Contains(nummer),
            });
        }

        return ergebnis.ToArray();
    }

    // ---------------------------------------------------------------- MLOD

    /// <summary>
    /// MLOD haelt UV-Koordinaten und Normalenverweise pro Flaechenecke, nicht
    /// pro Punkt. Deshalb wird hier umindiziert: jede eindeutige Kombination
    /// aus Punkt, Normale und UV wird ein eigener Vertex. Die Abschnitte
    /// entstehen durch Gruppieren der Flaechen nach Textur und Material.
    /// </summary>
    private static LodGeometry LiesMlodLod(MlodLod lod)
    {
        var punkte = lod.points ?? [];
        var quellNormalen = lod.normals ?? [];
        var flaechen = lod.faces ?? [];

        var positionen = new List<Vec3>(punkte.Length);
        var normalen = new List<Vec3>(punkte.Length);
        var uvs = new List<Vec2>(punkte.Length);
        var bekannt = new Dictionary<(int Punkt, int Normale, float U, float V), int>();

        // In MLOD stehen die Proxy-Platzhalter in Auswahlgruppen, deren
        // Name mit "proxy:" beginnt. Ihre Flaechen bilden die Pyramide mit
        // Pfeil und gehoeren nicht zum sichtbaren Objekt.
        var proxyFlaechen = ProxyFlaechenAusTaggs(lod, flaechen.Length);

        var gruppen = new Dictionary<(string Textur, string Material, bool Proxy), List<int>>();

        // Ausserhalb der Schleife: ein stackalloc je Flaeche waere bei
        // Modellen mit zehntausenden Flaechen ein Stapelueberlauf (CA2014).
        var abgebildet = new int[4];
        var flaechenNummer = -1;

        foreach (var flaeche in flaechen)
        {
            flaechenNummer++;
            if (flaeche?.Vertices is null) continue;

            var ecken = Math.Clamp(flaeche.NumberOfVertices, 0, flaeche.Vertices.Length);
            if (ecken < 3) continue;

            var schluessel = (flaeche.Texture ?? string.Empty,
                              flaeche.Material ?? string.Empty,
                              flaechenNummer < proxyFlaechen.Length && proxyFlaechen[flaechenNummer]);

            if (!gruppen.TryGetValue(schluessel, out var indizes))
                gruppen[schluessel] = indizes = [];

            var gueltig = true;

            for (var i = 0; i < ecken; i++)
            {
                var ecke = flaeche.Vertices[i];
                if (ecke is null) { gueltig = false; break; }

                abgebildet[i] = VertexHolen(ecke.PointIndex, ecke.NormalIndex, ecke.U, ecke.V);
                if (abgebildet[i] < 0) { gueltig = false; break; }
            }

            if (!gueltig) continue;

            // Umlaufsinn drehen, weil die Z-Achse gespiegelt wurde.
            indizes.Add(abgebildet[0]); indizes.Add(abgebildet[2]); indizes.Add(abgebildet[1]);
            if (ecken == 4)
            {
                indizes.Add(abgebildet[0]); indizes.Add(abgebildet[3]); indizes.Add(abgebildet[2]);
            }
        }

        var abschnitte = gruppen
            .Where(g => g.Value.Count > 0)
            .Select(g => new MeshSection
            {
                Indices = g.Value.ToArray(),
                TexturePath = OhneLeer(g.Key.Textur),
                MaterialPath = OhneLeer(g.Key.Material),
                IstProxy = g.Key.Proxy,
            })
            .ToArray();

        return new LodGeometry
        {
            Resolution = lod.Resolution,
            Name = LodName(lod),
            IstSichtbar = Resolution.IsVisual(lod.Resolution),
            Positions = positionen.ToArray(),
            Normals = normalen.ToArray(),
            Uvs = uvs.ToArray(),
            Sections = abschnitte,
        };

        int VertexHolen(int punktIndex, int normalenIndex, float u, float v)
        {
            if (punktIndex < 0 || punktIndex >= punkte.Length) return -1;

            var schluessel = (punktIndex, normalenIndex, u, v);
            if (bekannt.TryGetValue(schluessel, out var vorhanden)) return vorhanden;

            var punkt = punkte[punktIndex];
            if (punkt is null) return -1;

            var neu = positionen.Count;
            positionen.Add(NachWpf(punkt.X, punkt.Y, punkt.Z));

            if (normalenIndex >= 0 && normalenIndex < quellNormalen.Length
                && quellNormalen[normalenIndex] is not null)
            {
                var n = quellNormalen[normalenIndex];
                // MLOD-Normalen zeigen bereits nach aussen; nur die
                // Achsenspiegelung anwenden, keine Umkehrung wie bei ODOL.
                normalen.Add(NachWpf(n.X, n.Y, n.Z));
            }
            else
            {
                normalen.Add(new Vec3(0, 1, 0));
            }

            uvs.Add(new Vec2(u, v));
            bekannt[schluessel] = neu;
            return neu;
        }
    }

    /// <summary>
    /// Markiert die Flaechen, die zu einer Auswahlgruppe mit dem Praefix
    /// "proxy:" gehoeren. Das Byte-Feld je Flaeche ist ungleich null,
    /// wenn die Flaeche in der Gruppe liegt.
    /// </summary>
    private static bool[] ProxyFlaechenAusTaggs(MlodLod lod, int flaechenAnzahl)
    {
        var treffer = new bool[flaechenAnzahl];
        if (lod.taggs is null) return treffer;

        foreach (var tagg in lod.taggs)
        {
            if (tagg is not BisDll.Model.MLOD.NamedSelectionTagg auswahl) continue;
            if (auswahl.faces is null) continue;
            if (auswahl.Name is null) continue;
            if (!auswahl.Name.StartsWith("proxy:", StringComparison.OrdinalIgnoreCase)) continue;

            var anzahl = Math.Min(flaechenAnzahl, auswahl.faces.Length);
            for (var i = 0; i < anzahl; i++)
                if (auswahl.faces[i] != 0) treffer[i] = true;
        }

        return treffer;
    }

    // ------------------------------------------------------------ Gemeinsam

    /// <summary>Arma ist linkshaendig, WPF rechtshaendig: Z spiegeln.</summary>
    private static Vec3 NachWpf(float x, float y, float z) => new(x, y, -z);

    /// <summary>
    /// Lesbarer LOD-Name. BisDll formatiert Aufloesungsstufen mit "#.000",
    /// was bei der Stufe 0 und deutschem Zahlenformat ",000" ergibt —
    /// in einer Auswahlliste unbrauchbar.
    /// </summary>
    private static string LodName(BisDll.Model.P3D_LOD lod)
    {
        if (!Resolution.IsResolution(lod.Resolution))
            return lod.Name;

        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"Stufe {lod.Resolution:0.###}");
    }

    private static string? OhneLeer(string? wert)
        => string.IsNullOrWhiteSpace(wert) ? null : wert;

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
