// UseWPF nimmt System.IO aus den impliziten Usings, weil Path sonst mit
// System.Windows.Shapes.Path kollidieren wuerde.
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using DzAssets.Formats.Models;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>
/// Rendert kleine Vorschaubilder und legt sie als PNG auf der Platte ab.
///
/// Das Rendern ist an den UI-Thread gebunden — RenderTargetBitmap
/// verlangt ihn. Die Warteschlange wird deshalb ueber den Dispatcher mit
/// niedriger Vorrangstufe abgearbeitet, ein Bild je Durchlauf: so bleibt
/// die Oberflaeche bedienbar, weil Eingaben dazwischen Vorrang haben.
/// </summary>
public sealed class ThumbnailService(string cacheOrdner, TexturLader lader, Protokoll protokoll)
{
    public const int Kante = 128;

    public string CacheOrdner { get; } = cacheOrdner;

    /// <summary>
    /// Cache-Name aus Pfad und Aenderungszeit. Aendert sich das Modell,
    /// entsteht ein neuer Name; das alte Bild verwaist und stoert nicht.
    /// </summary>
    public string CacheDatei(string absoluterPfad)
    {
        var kennung = absoluterPfad.ToLowerInvariant();
        try
        {
            kennung += "|" + File.GetLastWriteTimeUtc(absoluterPfad).Ticks;
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
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
        catch (Exception fehler)
            when (fehler is IOException or NotSupportedException or UriFormatException)
        {
            return null;
        }
    }

    /// <summary>Rendert das Bild. MUSS auf dem UI-Thread laufen.</summary>
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

    private static Model3DGroup SzeneBauen(
        LodGeometry lod, IReadOnlyDictionary<MeshSection, ImageSource> texturen)
    {
        // Deutlich heller als im grossen Viewport: ein 128er Bild wird nur
        // ueberflogen, und Vegetation ist ueberwiegend durchsichtig — bei
        // gedaempftem Licht bliebe von einem Baum kaum etwas uebrig.
        var gruppe = new Model3DGroup();
        gruppe.Children.Add(new AmbientLight(Color.FromRgb(0x8C, 0x92, 0x9C)));
        gruppe.Children.Add(new DirectionalLight(
            Color.FromRgb(0xFF, 0xF4, 0xE8), new Vector3D(-0.5, -0.9, -0.6)));
        gruppe.Children.Add(new DirectionalLight(
            Color.FromRgb(0x50, 0x5A, 0x68), new Vector3D(0.7, 0.4, 0.6)));

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
                    Viewport = new Rect(0, 0, 1, 1),
                };
                pinsel.Freeze();
                material = new DiffuseMaterial(pinsel);
                material.Freeze();
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
        var abstand = radius * 1.9;

        var position = new Point3D(
            mitte.X + abstand * 0.60,
            mitte.Y + abstand * 0.45,
            mitte.Z + abstand * 0.60);

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

        sicht.Measure(new Size(Kante, Kante));
        sicht.Arrange(new Rect(0, 0, Kante, Kante));
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
