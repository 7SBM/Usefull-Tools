namespace DzAssets.Formats.Hoehen;

/// <summary>Farbskala fuer die Einfaerbung nach Hoehe.</summary>
public enum Farbskala
{
    /// <summary>Gruen — Gelb — Braun — Weiss, wie eine Wanderkarte.</summary>
    Gelaende,

    /// <summary>Schwarz nach Weiss. Zeigt kleine Stufen am ehrlichsten.</summary>
    Graustufen,

    /// <summary>Blau — Cyan — Gelb — Rot. Fuer Karten ohne Gelaendebezug, etwa Masken.</summary>
    Verlauf,
}

/// <summary>
/// Rechnet ein Hoehenraster auf Anzeigegroesse herunter und faerbt es ein.
/// Reine Rechnung ohne WPF, damit unabhaengig pruefbar und im Hintergrund
/// ausfuehrbar.
/// </summary>
public static class Reliefbild
{
    /// <summary>Ein verkleinertes Raster samt seiner Wertespanne.</summary>
    public sealed class Verkleinert
    {
        public required int Spalten { get; init; }
        public required int Zeilen { get; init; }
        public required float[] Werte { get; init; }
        public required float Min { get; init; }
        public required float Max { get; init; }

        /// <summary>Wie viele Quellzellen auf eine Zielzelle fielen.</summary>
        public required int Faktor { get; init; }

        public float this[int spalte, int zeile] => Werte[zeile * Spalten + spalte];
    }

    /// <summary>
    /// Verkleinert per Blockmittelwert — nicht per Stichprobe. Eine
    /// Stichprobe liesse schmale Grate und Bahntrassen verschwinden, also
    /// genau die Strukturen, wegen derer man die Karte ansieht.
    /// NoData-Zellen gehen nicht in den Mittelwert ein.
    /// </summary>
    public static Verkleinert Verkleinern(AscRaster raster, int hoechstens)
    {
        ArgumentNullException.ThrowIfNull(raster);
        if (hoechstens < 1) throw new ArgumentOutOfRangeException(nameof(hoechstens));

        var groesste = Math.Max(raster.Spalten, raster.Zeilen);
        var faktor = Math.Max(1, (int)Math.Ceiling(groesste / (double)hoechstens));

        var spalten = Math.Max(1, raster.Spalten / faktor);
        var zeilen = Math.Max(1, raster.Zeilen / faktor);
        var werte = new float[spalten * zeilen];

        var min = float.MaxValue;
        var max = float.MinValue;

        for (var zy = 0; zy < zeilen; zy++)
        {
            for (var zx = 0; zx < spalten; zx++)
            {
                double summe = 0;
                var zahl = 0;

                for (var by = 0; by < faktor; by++)
                {
                    var qy = zy * faktor + by;
                    if (qy >= raster.Zeilen) break;

                    for (var bx = 0; bx < faktor; bx++)
                    {
                        var qx = zx * faktor + bx;
                        if (qx >= raster.Spalten) break;

                        var wert = raster.Werte[qy * raster.Spalten + qx];
                        if (Math.Abs(wert - raster.NoData) < 0.001) continue;

                        summe += wert;
                        zahl++;
                    }
                }

                var ergebnis = zahl == 0 ? float.NaN : (float)(summe / zahl);
                werte[zy * spalten + zx] = ergebnis;

                if (float.IsNaN(ergebnis)) continue;
                if (ergebnis < min) min = ergebnis;
                if (ergebnis > max) max = ergebnis;
            }
        }

        if (min > max) { min = 0; max = 0; }

        return new Verkleinert
        {
            Spalten = spalten,
            Zeilen = zeilen,
            Werte = werte,
            Min = min,
            Max = max,
            Faktor = faktor,
        };
    }

    /// <summary>
    /// Faerbt nach Hoehe ein und legt eine Schummerung darueber.
    /// Ergebnis ist BGRA32, zeilenweise von oben — das Layout von
    /// PixelFormats.Bgra32.
    /// </summary>
    /// <param name="zellgroesse">Kantenlaenge einer Zelle in Metern, fuer die Neigung.</param>
    /// <param name="schummerung">0 = keine, 1 = volle Schummerung.</param>
    public static byte[] Einfaerben(Verkleinert raster, Farbskala skala,
                                    double zellgroesse, double schummerung = 0.65)
    {
        ArgumentNullException.ThrowIfNull(raster);

        var bild = new byte[raster.Spalten * raster.Zeilen * 4];
        var spanne = raster.Max - raster.Min;
        if (spanne <= 0) spanne = 1;

        // Lichteinfall aus Nordwest, 45 Grad ueber dem Horizont — die in
        // Karten uebliche Beleuchtung. Eine andere Richtung laesst Taeler
        // als Ruecken erscheinen.
        const double azimut = 315.0 * Math.PI / 180.0;
        const double hoehenwinkel = 45.0 * Math.PI / 180.0;
        var lichtX = -Math.Cos(hoehenwinkel) * Math.Sin(azimut);
        var lichtY = -Math.Cos(hoehenwinkel) * Math.Cos(azimut);
        var lichtZ = Math.Sin(hoehenwinkel);

        // Die Zellgroesse des verkleinerten Rasters, nicht die der Quelle.
        var schrittMeter = Math.Max(0.001, zellgroesse * raster.Faktor);

        for (var y = 0; y < raster.Zeilen; y++)
        {
            for (var x = 0; x < raster.Spalten; x++)
            {
                var i = (y * raster.Spalten + x) * 4;
                var wert = raster.Werte[y * raster.Spalten + x];

                if (float.IsNaN(wert))
                {
                    // NoData: erkennbar dunkelrot, nicht schwarz — sonst
                    // waere es von tiefem Gelaende nicht zu unterscheiden.
                    bild[i + 0] = 0x30; bild[i + 1] = 0x18; bild[i + 2] = 0x60; bild[i + 3] = 0xFF;
                    continue;
                }

                var anteil = (wert - raster.Min) / spanne;
                var (r, g, b) = Farbe(skala, Math.Clamp(anteil, 0f, 1f));

                if (schummerung > 0)
                {
                    var licht = Beleuchtung(raster, x, y, schrittMeter, lichtX, lichtY, lichtZ);
                    var faktor = 1.0 - schummerung + schummerung * licht;
                    r = (byte)Math.Clamp(r * faktor, 0, 255);
                    g = (byte)Math.Clamp(g * faktor, 0, 255);
                    b = (byte)Math.Clamp(b * faktor, 0, 255);
                }

                bild[i + 0] = b;
                bild[i + 1] = g;
                bild[i + 2] = r;
                bild[i + 3] = 0xFF;
            }
        }

        return bild;
    }

    /// <summary>Neigung aus den Nachbarzellen, daraus der Lichtanteil 0..1.</summary>
    private static double Beleuchtung(Verkleinert raster, int x, int y, double schritt,
                                      double lichtX, double lichtY, double lichtZ)
    {
        var links = Wert(raster, x - 1, y);
        var rechts = Wert(raster, x + 1, y);
        var oben = Wert(raster, x, y - 1);
        var unten = Wert(raster, x, y + 1);

        var dzdx = (rechts - links) / (2 * schritt);
        var dzdy = (unten - oben) / (2 * schritt);

        // Flaechennormale aus den beiden Gefaellen.
        var laenge = Math.Sqrt(dzdx * dzdx + dzdy * dzdy + 1);
        var nx = -dzdx / laenge;
        var ny = -dzdy / laenge;
        var nz = 1.0 / laenge;

        return Math.Clamp(nx * lichtX + ny * lichtY + nz * lichtZ, 0.0, 1.0);
    }

    private static double Wert(Verkleinert raster, int x, int y)
    {
        x = Math.Clamp(x, 0, raster.Spalten - 1);
        y = Math.Clamp(y, 0, raster.Zeilen - 1);

        var wert = raster.Werte[y * raster.Spalten + x];
        return float.IsNaN(wert) ? raster.Min : wert;
    }

    private static (byte R, byte G, byte B) Farbe(Farbskala skala, float t) => skala switch
    {
        Farbskala.Graustufen => Mischen(t, [
            (0.00f, 0x10, 0x12, 0x16),
            (1.00f, 0xF2, 0xF4, 0xF8),
        ]),

        Farbskala.Verlauf => Mischen(t, [
            (0.00f, 0x1B, 0x3A, 0x8C),
            (0.35f, 0x24, 0x9E, 0xC4),
            (0.65f, 0xE8, 0xD0, 0x50),
            (1.00f, 0xC4, 0x3A, 0x2B),
        ]),

        // Wanderkartenartig: Talgruen, Almgruen, Fels, Firn.
        _ => Mischen(t, [
            (0.00f, 0x2E, 0x5E, 0x34),
            (0.22f, 0x5A, 0x8A, 0x3E),
            (0.45f, 0xA8, 0xA5, 0x55),
            (0.68f, 0x9B, 0x7A, 0x52),
            (0.86f, 0x7A, 0x6B, 0x63),
            (1.00f, 0xF4, 0xF6, 0xFA),
        ]),
    };

    private static (byte R, byte G, byte B) Mischen(
        float t, (float Stelle, byte R, byte G, byte B)[] stuetzen)
    {
        if (t <= stuetzen[0].Stelle)
            return (stuetzen[0].R, stuetzen[0].G, stuetzen[0].B);

        for (var i = 1; i < stuetzen.Length; i++)
        {
            if (t > stuetzen[i].Stelle) continue;

            var vorher = stuetzen[i - 1];
            var jetzt = stuetzen[i];
            var breite = jetzt.Stelle - vorher.Stelle;
            var anteil = breite <= 0 ? 0f : (t - vorher.Stelle) / breite;

            return (
                (byte)(vorher.R + (jetzt.R - vorher.R) * anteil),
                (byte)(vorher.G + (jetzt.G - vorher.G) * anteil),
                (byte)(vorher.B + (jetzt.B - vorher.B) * anteil));
        }

        var letzte = stuetzen[^1];
        return (letzte.R, letzte.G, letzte.B);
    }
}
