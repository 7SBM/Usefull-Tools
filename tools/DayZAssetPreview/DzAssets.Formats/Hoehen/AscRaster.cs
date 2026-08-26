using System.Globalization;
using System.IO;

namespace DzAssets.Formats.Hoehen;

/// <summary>
/// Ein eingelesenes Esri-ASCII-Grid (.asc).
///
/// Bewusst <see cref="float"/> und nicht <see cref="double"/>: die Quellen
/// haben hoechstens sechs Nachkommastellen bei vierstelligen Hoehen. Ein
/// 4096er-Raster kostet so 64 MiB statt 128 MiB.
/// </summary>
public sealed class AscRaster
{
    /// <summary>Groesste zugelassene Kantenlaenge — 8192² waeren schon 256 MiB.</summary>
    public const int MaxKante = 8192;

    private AscRaster(int spalten, int zeilen, double xEcke, double yEcke,
                      double zellgroesse, double nodata, float[] werte,
                      float min, float max, int nodataAnzahl)
    {
        Spalten = spalten;
        Zeilen = zeilen;
        XEcke = xEcke;
        YEcke = yEcke;
        Zellgroesse = zellgroesse;
        NoData = nodata;
        Werte = werte;
        Min = min;
        Max = max;
        NoDataAnzahl = nodataAnzahl;
    }

    public int Spalten { get; }
    public int Zeilen { get; }
    public double XEcke { get; }
    public double YEcke { get; }
    public double Zellgroesse { get; }
    public double NoData { get; }

    /// <summary>Zeilenweise von oben nach unten, wie in der Datei.</summary>
    public float[] Werte { get; }

    /// <summary>Kleinster Wert ohne NoData. Gleich <see cref="Max"/>, wenn alles NoData ist.</summary>
    public float Min { get; }
    public float Max { get; }
    public int NoDataAnzahl { get; }

    /// <summary>Breite des Rasters in Metern.</summary>
    public double BreiteMeter => Spalten * Zellgroesse;

    /// <summary>Hoehe des Rasters in Metern.</summary>
    public double HoeheMeter => Zeilen * Zellgroesse;

    public float this[int spalte, int zeile] => Werte[zeile * Spalten + spalte];

    public static AscRaster Laden(string pfad, IProgress<int>? fortschritt = null,
                                  CancellationToken abbruch = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pfad);
        using var strom = File.OpenRead(pfad);
        try
        {
            return Laden(strom, fortschritt, abbruch);
        }
        catch (InvalidDataException fehler)
        {
            throw new InvalidDataException($"{Path.GetFileName(pfad)}: {fehler.Message}", fehler);
        }
    }

    /// <summary>
    /// Liest zeilenweise und schneidet die Zahlen als Spannen aus dem
    /// Zeilenpuffer. So entsteht keine einzige Teilzeichenkette — bei
    /// 16,8 Millionen Werten waere das sonst der teuerste Teil.
    /// </summary>
    public static AscRaster Laden(Stream strom, IProgress<int>? fortschritt = null,
                                  CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(strom);
        using var leser = new StreamReader(strom, System.Text.Encoding.ASCII,
                                           detectEncodingFromByteOrderMarks: true,
                                           bufferSize: 1 << 16, leaveOpen: true);

        var kopf = KopfLesen(leser);

        var gesamt = (long)kopf.Spalten * kopf.Zeilen;
        if (gesamt > (long)MaxKante * MaxKante)
            throw new InvalidDataException(
                $"Raster {kopf.Spalten}x{kopf.Zeilen} ist zu gross (Grenze {MaxKante}x{MaxKante}).");

        var werte = new float[gesamt];
        var min = float.MaxValue;
        var max = float.MinValue;
        var nodataAnzahl = 0;
        var index = 0;

        for (var zeile = 0; zeile < kopf.Zeilen; zeile++)
        {
            abbruch.ThrowIfCancellationRequested();

            // Beim Kopflesen wurde die erste Datenzeile eventuell schon
            // verbraucht; sie kommt hier zuerst zum Zug.
            var text = (zeile == 0 ? kopf.ErsteDatenzeile : null) ?? leser.ReadLine()
                ?? throw new InvalidDataException(
                    $"Datei endet nach {zeile} von {kopf.Zeilen} Rasterzeilen.");

            var spanne = text.AsSpan();
            var inZeile = 0;
            var stelle = 0;

            while (stelle < spanne.Length)
            {
                while (stelle < spanne.Length && char.IsWhiteSpace(spanne[stelle])) stelle++;
                if (stelle >= spanne.Length) break;

                var start = stelle;
                while (stelle < spanne.Length && !char.IsWhiteSpace(spanne[stelle])) stelle++;

                if (inZeile >= kopf.Spalten)
                    throw new InvalidDataException(
                        $"Zeile {zeile + 1} hat mehr als die angekuendigten {kopf.Spalten} Werte.");

                // InvariantCulture ist hier zwingend: auf einem deutschen
                // System ergaebe float.Parse("1358.84") sonst 135884.
                if (!float.TryParse(spanne[start..stelle], NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out var wert))
                {
                    throw new InvalidDataException(
                        $"Zeile {zeile + 1}, Wert {inZeile + 1}: „{spanne[start..stelle]}“ ist keine Zahl.");
                }

                werte[index++] = wert;
                inZeile++;

                // NoData numerisch vergleichen: in den Dateien stehen
                // -9999, -9999.0 und -9999.000000 nebeneinander.
                if (Math.Abs(wert - kopf.NoData) < 0.001)
                {
                    nodataAnzahl++;
                }
                else
                {
                    if (wert < min) min = wert;
                    if (wert > max) max = wert;
                }
            }

            if (inZeile != kopf.Spalten)
                throw new InvalidDataException(
                    $"Zeile {zeile + 1} hat {inZeile} Werte, angekuendigt waren {kopf.Spalten}.");

            if ((zeile & 0xFF) == 0) fortschritt?.Report(zeile);
        }

        fortschritt?.Report(kopf.Zeilen);

        if (min > max) { min = 0; max = 0; }

        return new AscRaster(kopf.Spalten, kopf.Zeilen, kopf.XEcke, kopf.YEcke,
                             kopf.Zellgroesse, kopf.NoData, werte, min, max, nodataAnzahl);
    }

    private readonly record struct Kopf(
        int Spalten, int Zeilen, double XEcke, double YEcke, double Zellgroesse, double NoData,
        string? ErsteDatenzeile);

    /// <summary>
    /// Liest die Kopfzeilen. `NODATA_value` ist im Esri-Standard freiwillig,
    /// deshalb wird gelesen, bis alle Pflichtangaben da sind — nicht
    /// sechsmal blind. Wurde dabei bereits die erste Datenzeile verbraucht,
    /// reicht der Kopf sie zurueck; ueber einen StreamReader laesst sich
    /// nicht verlaesslich zuruecksetzen.
    /// </summary>
    private static Kopf KopfLesen(StreamReader leser)
    {
        int? spalten = null, zeilen = null;
        double? xEcke = null, yEcke = null, zellgroesse = null;
        var nodata = -9999.0;
        string? ersteDatenzeile = null;

        while (true)
        {
            var zeile = leser.ReadLine();
            if (zeile is null) break;
            if (zeile.Length == 0) continue;

            var teile = zeile.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            // Beginnt die Zeile mit einer Zahl, ist der Kopf zu Ende.
            if (teile.Length == 0
                || double.TryParse(teile[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                ersteDatenzeile = zeile;
                break;
            }

            if (teile.Length < 2)
                throw new InvalidDataException($"Kopfzeile ohne Wert: „{zeile.Trim()}“.");

            if (!double.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var wert))
                throw new InvalidDataException($"Kopfzeile „{teile[0]}“ hat keinen Zahlenwert.");

            switch (teile[0].ToLowerInvariant())
            {
                case "ncols": spalten = (int)wert; break;
                case "nrows": zeilen = (int)wert; break;
                case "xllcorner": xEcke = wert; break;
                case "yllcorner": yEcke = wert; break;
                // Esri erlaubt Mittelpunktbezug. In keiner der 80 vorgefundenen
                // Dateien kommt er vor; dieser Zweig ist daher ungeprueft.
                case "xllcenter": xEcke = wert; break;
                case "yllcenter": yEcke = wert; break;
                case "cellsize": zellgroesse = wert; break;
                case "nodata_value": nodata = wert; break;
                default:
                    throw new InvalidDataException($"Unbekannte Kopfzeile: „{teile[0]}“.");
            }
        }

        if (spalten is null or <= 0 || zeilen is null or <= 0)
            throw new InvalidDataException("Kopfzeilen ncols/nrows fehlen oder sind unbrauchbar.");
        if (zellgroesse is null or <= 0)
            throw new InvalidDataException("Kopfzeile cellsize fehlt oder ist unbrauchbar.");
        if (spalten > MaxKante || zeilen > MaxKante)
            throw new InvalidDataException(
                $"Raster {spalten}x{zeilen} ist zu gross (Grenze {MaxKante}).");

        return new Kopf(spalten.Value, zeilen.Value, xEcke ?? 0, yEcke ?? 0,
                        zellgroesse.Value, nodata, ersteDatenzeile);
    }
}
