using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DzAssets.Formats.Hoehen;

namespace DzAssets.Preview.Module.Hoehenkarte;

/// <summary>
/// Baut aus einem verkleinerten Hoehenraster ein WPF-Netz.
///
/// Die Feinheit des Netzes und die Aufloesung der Textur sind bewusst
/// getrennt: ein Netz von 512x512 hat schon eine halbe Million Dreiecke,
/// waehrend eine Textur von 2048x2048 nichts kostet. Das Gelaende sieht
/// dadurch viel feiner aus, als es vernetzt ist.
/// </summary>
public static class GelaendeNetz
{
    /// <summary>
    /// Erzeugt das Netz. Die Karte liegt in der XZ-Ebene, die Hoehe auf Y —
    /// wie die Modelle, damit dieselbe Kamera passt.
    /// </summary>
    /// <param name="raster">bereits verkleinertes Raster</param>
    /// <param name="zellgroesse">Kantenlaenge einer Quellzelle in Metern</param>
    /// <param name="ueberhoehung">1 = massstabsgetreu</param>
    public static MeshGeometry3D Bauen(Reliefbild.Verkleinert raster,
                                       double zellgroesse,
                                       double ueberhoehung = 1.0)
    {
        ArgumentNullException.ThrowIfNull(raster);

        var schritt = zellgroesse * raster.Faktor;
        var breite = (raster.Spalten - 1) * schritt;
        var tiefe = (raster.Zeilen - 1) * schritt;

        // Um den Ursprung zentrieren, damit die Kamera einfach zu fuehren ist.
        var halbBreite = breite / 2;
        var halbTiefe = tiefe / 2;

        var anzahl = raster.Spalten * raster.Zeilen;
        var positionen = new Point3DCollection(anzahl);
        var uvs = new PointCollection(anzahl);

        // Luecken bekommen die Mindesthoehe statt NaN — ein NaN im Netz
        // laesst WPF die ganze Geometrie verwerfen.
        var ersatz = raster.Min;

        for (var z = 0; z < raster.Zeilen; z++)
        {
            for (var x = 0; x < raster.Spalten; x++)
            {
                var hoehe = raster.Werte[z * raster.Spalten + x];
                if (float.IsNaN(hoehe)) hoehe = ersatz;

                positionen.Add(new Point3D(
                    x * schritt - halbBreite,
                    (hoehe - raster.Min) * ueberhoehung,
                    z * schritt - halbTiefe));

                uvs.Add(new Point(
                    raster.Spalten == 1 ? 0 : x / (double)(raster.Spalten - 1),
                    raster.Zeilen == 1 ? 0 : z / (double)(raster.Zeilen - 1)));
            }
        }

        var dreiecke = new Int32Collection((raster.Spalten - 1) * (raster.Zeilen - 1) * 6);
        for (var z = 0; z < raster.Zeilen - 1; z++)
        {
            for (var x = 0; x < raster.Spalten - 1; x++)
            {
                var linksOben = z * raster.Spalten + x;
                var rechtsOben = linksOben + 1;
                var linksUnten = linksOben + raster.Spalten;
                var rechtsUnten = linksUnten + 1;

                dreiecke.Add(linksOben); dreiecke.Add(linksUnten); dreiecke.Add(rechtsOben);
                dreiecke.Add(rechtsOben); dreiecke.Add(linksUnten); dreiecke.Add(rechtsUnten);
            }
        }

        positionen.Freeze();
        uvs.Freeze();
        dreiecke.Freeze();

        var netz = new MeshGeometry3D
        {
            Positions = positionen,
            TextureCoordinates = uvs,
            TriangleIndices = dreiecke,
        };

        // Normalen berechnet WPF selbst, wenn keine gesetzt sind. Bei einem
        // halbmillionen-Dreieck-Netz ist das spuerbar schneller, als sie
        // hier in verwaltetem Code aufzusummieren.
        netz.Freeze();
        return netz;
    }

    /// <summary>Wie hoch das Netz nach der Ueberhoehung ist, in Weltmass.</summary>
    public static double NetzHoehe(Reliefbild.Verkleinert raster, double ueberhoehung)
        => (raster.Max - raster.Min) * ueberhoehung;
}
