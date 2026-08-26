using System.Windows.Media;
using System.Windows.Media.Imaging;
using DzAssets.Formats.Models;
using DzAssets.Formats.Textures;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>
/// Laedt die Diffusetexturen eines LODs. Darf im Hintergrund laufen: die
/// erzeugten Bilder werden eingefroren und sind damit vom UI-Thread
/// benutzbar. Ohne Freeze wirft WPF beim Zuweisen.
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

        // Ein Haus benutzt dieselbe Wandtextur in vielen Abschnitten.
        // Deshalb je Datei einmal dekodieren und mehrfach zuweisen.
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

            // WPF 3D kennt kein Alpha-Testing. Harte Kanten sehen besser
            // aus als der Schleier, den halbdurchsichtige Blaetter sonst
            // erzeugen.
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
