using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using DzAssets.Formats.Models;
using DzAssets.Preview.Gemeinsam;

namespace DzAssets.Preview.Module.AssetVorschau;

public partial class ModelViewport : UserControl
{
    private static readonly Material Ersatzmaterial = ErzeugeErsatzmaterial();
    private static readonly ImageSource Gitterkachel = ErzeugeGitterkachel();

    private Model3DGroup? _modellGruppe;
    private Model3DGroup? _hilfsGruppe;

    private ModelGeometry? _modell;
    private LodGeometry? _lod;
    private IReadOnlyDictionary<MeshSection, ImageSource>? _texturen;

    private readonly OrbitKamera _kamera;

    private bool _drahtgitter;
    private bool _bodengitter = true;
    private bool _massstabsfigur = true;

    public ModelViewport()
    {
        InitializeComponent();

        _kamera = new OrbitKamera(Kamera) { DrehungUmX = 0.45, Abstand = 6 };
        _kamera.Anwenden();

        HilfsgeometrieAufbauen();

        MouseDown += (_, _) => Focus();
    }

    public bool DrahtgitterZeigen
    {
        get => _drahtgitter;
        set { if (_drahtgitter == value) return; _drahtgitter = value; NeuAufbauen(); }
    }

    public bool BodengitterZeigen
    {
        get => _bodengitter;
        set { if (_bodengitter == value) return; _bodengitter = value; HilfsgeometrieAufbauen(); }
    }

    public bool MassstabsfigurZeigen
    {
        get => _massstabsfigur;
        set { if (_massstabsfigur == value) return; _massstabsfigur = value; HilfsgeometrieAufbauen(); }
    }

    public void Zeigen(ModelGeometry modell, LodGeometry lod,
                       IReadOnlyDictionary<MeshSection, ImageSource>? texturen = null)
    {
        _modell = modell;
        _lod = lod;
        _texturen = texturen;
        NeuAufbauen();
        // Die Massstabsfigur richtet sich nach der Groesse des Objekts,
        // muss also nach jedem Modellwechsel neu gesetzt werden.
        HilfsgeometrieAufbauen();
        Einrahmen();
    }

    public void Leeren()
    {
        _modell = null;
        _lod = null;
        _texturen = null;
        NeuAufbauen();
        HilfsgeometrieAufbauen();
    }

    // ------------------------------------------------------------- Aufbau

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

            var material = MaterialFuer(abschnitt);

            // DayZ-Modelle sind haeufig einseitig modelliert; ohne
            // Rueckseitenmaterial fehlten ganze Waende.
            gruppe.Children.Add(new GeometryModel3D(geometrie, material)
            {
                BackMaterial = material,
            });
        }

        if (_drahtgitter) gruppe.Children.Add(DrahtgitterErzeugen(_lod));

        _modellGruppe = gruppe;
        Szene.Children.Add(gruppe);
    }

    private Material MaterialFuer(MeshSection abschnitt)
    {
        if (_texturen is null || !_texturen.TryGetValue(abschnitt, out var bild))
            return Ersatzmaterial;

        var pinsel = new ImageBrush(bild)
        {
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 1, 1),
        };
        pinsel.Freeze();

        var material = new DiffuseMaterial(pinsel);
        material.Freeze();
        return material;
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
    /// zweites, leicht vergroessertes Modell in Akzentfarbe darueber
    /// gezeichnet — das genuegt, um die Unterteilung zu beurteilen, ohne
    /// eine Linienbibliothek zu brauchen.
    /// </summary>
    private static Model3DGroup DrahtgitterErzeugen(LodGeometry lod)
    {
        var gruppe = new Model3DGroup();

        var farbe = new SolidColorBrush(Color.FromArgb(0x38, 0x4C, 0x8D, 0xFF));
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
                Transform = new ScaleTransform3D(1.002, 1.002, 1.002),
            });
        }

        return gruppe;
    }

    private void HilfsgeometrieAufbauen()
    {
        if (_hilfsGruppe is not null) Szene.Children.Remove(_hilfsGruppe);

        var gruppe = new Model3DGroup();
        if (_bodengitter) gruppe.Children.Add(BodengitterErzeugen());
        if (_massstabsfigur) gruppe.Children.Add(MassstabsfigurErzeugen(_modell));

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
        geometrie.Freeze();

        var pinsel = new ImageBrush(Gitterkachel)
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
    private static ImageSource ErzeugeGitterkachel()
    {
        const int kante = 64;
        var pixel = new byte[kante * kante * 4];

        for (var y = 0; y < kante; y++)
        {
            for (var x = 0; x < kante; x++)
            {
                // Linie #3C4450, Flaeche #161A21 — als BGRA abgelegt.
                var amRand = x == 0 || y == 0;
                var i = (y * kante + x) * 4;
                pixel[i + 0] = amRand ? (byte)0x50 : (byte)0x21;   // B
                pixel[i + 1] = amRand ? (byte)0x44 : (byte)0x1A;   // G
                pixel[i + 2] = amRand ? (byte)0x3C : (byte)0x16;   // R
                pixel[i + 3] = 0xFF;
            }
        }

        var bild = BitmapSource.Create(kante, kante, 96, 96,
            PixelFormats.Bgra32, null, pixel, kante * 4);
        bild.Freeze();
        return bild;
    }

    /// <summary>
    /// Eine schlichte Saeule von 1,80 m Hoehe als Groessenvergleich.
    /// Bewusst kein Menschmodell: sie soll den Blick nicht auf sich ziehen.
    /// </summary>
    private static GeometryModel3D MassstabsfigurErzeugen(ModelGeometry? modell)
    {
        const double breite = 0.24;
        const double tiefe = 0.18;
        const double hoehe = 1.80;

        // Neben das Objekt stellen, nicht an einen festen Punkt: bei einem
        // Waschbecken staende die Figur sonst ausserhalb des Bildes, bei
        // einer Halle mitten darin.
        var versatz = new Vector3D(-1.5, 0, -1.5);
        if (modell is not null)
        {
            var min = modell.BoundsMin;
            var max = modell.BoundsMax;
            var abstand = Math.Max(0.6, (max.X - min.X) * 0.15);
            versatz = new Vector3D(
                min.X - abstand - breite,
                0,
                (min.Z + max.Z) / 2);
        }

        var ecken = new[]
        {
            new Point3D(-breite / 2, 0, -tiefe / 2), new Point3D(breite / 2, 0, -tiefe / 2),
            new Point3D( breite / 2, 0,  tiefe / 2), new Point3D(-breite / 2, 0,  tiefe / 2),
            new Point3D(-breite / 2, hoehe, -tiefe / 2), new Point3D(breite / 2, hoehe, -tiefe / 2),
            new Point3D( breite / 2, hoehe,  tiefe / 2), new Point3D(-breite / 2, hoehe,  tiefe / 2),
        };

        var geometrie = new MeshGeometry3D();
        foreach (var ecke in ecken)
            geometrie.Positions.Add(ecke + versatz);

        int[] flaechen =
        [
            0, 1, 2, 0, 2, 3,   // unten
            4, 6, 5, 4, 7, 6,   // oben
            0, 4, 5, 0, 5, 1,
            1, 5, 6, 1, 6, 2,
            2, 6, 7, 2, 7, 3,
            3, 7, 4, 3, 4, 0,
        ];
        foreach (var index in flaechen) geometrie.TriangleIndices.Add(index);
        geometrie.Freeze();

        var pinsel = new SolidColorBrush(Color.FromArgb(0xAA, 0x4C, 0x8D, 0xFF));
        pinsel.Freeze();
        var material = new DiffuseMaterial(pinsel);
        material.Freeze();

        return new GeometryModel3D(geometrie, material) { BackMaterial = material };
    }

    // ------------------------------------------------------------- Kamera

    public void Einrahmen()
    {
        if (_modell is null)
        {
            _kamera.Zielpunkt = new Point3D(0, 0.9, 0);
            _kamera.Abstand = 6;
            _kamera.Anwenden();
            return;
        }

        var min = _modell.BoundsMin;
        var max = _modell.BoundsMax;

        // 2,2 statt der frueheren 1,9: die Kamera rechnet jetzt mit der
        // halben Raumdiagonale statt mit der laengsten Kante.
        _kamera.Einrahmen(
            new Point3D(min.X, min.Y, min.Z),
            new Point3D(max.X, max.Y, max.Z),
            zugabe: 2.2);
    }

    // ------------------------------------------------------------ Eingabe

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        _kamera.MausRunter(e, this);
        CaptureMouse();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _kamera.MausHoch();
        ReleaseMouseCapture();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _kamera.MausBewegt(e, this);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _kamera.Rad(e.Delta);
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
