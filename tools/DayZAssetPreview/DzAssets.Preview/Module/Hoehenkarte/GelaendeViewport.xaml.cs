using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DzAssets.Formats.Hoehen;
using DzAssets.Preview.Gemeinsam;

namespace DzAssets.Preview.Module.Hoehenkarte;

public partial class GelaendeViewport : UserControl
{
    private readonly OrbitKamera _kamera;

    private Model3DGroup? _gelaende;
    private Reliefbild.Verkleinert? _raster;
    private double _zellgroesse = 1.0;
    private double _ueberhoehung = 1.0;
    private bool _drahtgitter;

    public GelaendeViewport()
    {
        InitializeComponent();

        _kamera = new OrbitKamera(Kamera)
        {
            // Gelaende ist gross: bis 20 km Kantenlaenge.
            KleinsterAbstand = 1,
            GroessterAbstand = 400_000,
            // Deutlich steiler als bei einem Modell. Eine Karte von
            // 20 km Breite und 3 km Hoehe sieht von flach seitlich nur
            // wie ein Streifen aus.
            DrehungUmX = 0.85,
        };
        _kamera.Anwenden();

        MouseDown += (_, _) => Focus();
    }

    public bool DrahtgitterZeigen
    {
        get => _drahtgitter;
        set { if (_drahtgitter == value) return; _drahtgitter = value; NeuAufbauen(); }
    }

    /// <summary>Zeigt ein Gelaende. Die Textur muss eingefroren sein.</summary>
    public void Zeigen(Reliefbild.Verkleinert raster, ImageSource textur,
                       double zellgroesse, double ueberhoehung)
    {
        _raster = raster;
        _zellgroesse = zellgroesse;
        _ueberhoehung = ueberhoehung;
        _textur = textur;

        NeuAufbauen();
        Einrahmen();
    }

    /// <summary>Nur die Ueberhoehung aendern — das Netz wird neu gebaut.</summary>
    public void UeberhoehungSetzen(double ueberhoehung)
    {
        if (Math.Abs(_ueberhoehung - ueberhoehung) < 0.0001) return;

        _ueberhoehung = ueberhoehung;
        NeuAufbauen();
    }

    public void Leeren()
    {
        _raster = null;
        _textur = null;
        NeuAufbauen();
    }

    private ImageSource? _textur;

    private void NeuAufbauen()
    {
        if (_gelaende is not null) Szene.Children.Remove(_gelaende);
        _gelaende = null;

        if (_raster is null) return;

        var netz = GelaendeNetz.Bauen(_raster, _zellgroesse, _ueberhoehung);

        Material material;
        if (_textur is not null)
        {
            var pinsel = new ImageBrush(_textur)
            {
                Stretch = Stretch.Fill,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0, 0, 1, 1),
                TileMode = TileMode.None,
            };
            pinsel.Freeze();
            material = new DiffuseMaterial(pinsel);
        }
        else
        {
            material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0x7A, 0x82, 0x8E)));
        }

        material.Freeze();

        var gruppe = new Model3DGroup();
        gruppe.Children.Add(new GeometryModel3D(netz, material) { BackMaterial = material });

        if (_drahtgitter)
        {
            var farbe = new SolidColorBrush(Color.FromArgb(0x2A, 0x4C, 0x8D, 0xFF));
            farbe.Freeze();
            var draht = new EmissiveMaterial(farbe);
            draht.Freeze();

            gruppe.Children.Add(new GeometryModel3D(netz, draht)
            {
                BackMaterial = draht,
                Transform = new ScaleTransform3D(1.0006, 1.0006, 1.0006),
            });
        }

        _gelaende = gruppe;
        Szene.Children.Add(gruppe);
    }

    public void Einrahmen()
    {
        if (_raster is null)
        {
            _kamera.Zielpunkt = new Point3D(0, 0, 0);
            _kamera.Abstand = 2000;
            _kamera.Anwenden();
            return;
        }

        var schritt = _zellgroesse * _raster.Faktor;
        var breite = (_raster.Spalten - 1) * schritt;
        var tiefe = (_raster.Zeilen - 1) * schritt;
        var hoehe = GelaendeNetz.NetzHoehe(_raster, _ueberhoehung);

        // 2,5 passt zum Sichtfeld von 45 Grad: die halbe Raumdiagonale
        // geteilt durch tan(22,5 Grad) ergaebe 2,41, etwas Luft dazu.
        _kamera.Einrahmen(
            new Point3D(-breite / 2, 0, -tiefe / 2),
            new Point3D(breite / 2, hoehe, tiefe / 2),
            zugabe: 2.5);
    }

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
        if (e.Key != Key.F) return;

        Einrahmen();
        e.Handled = true;
    }
}
