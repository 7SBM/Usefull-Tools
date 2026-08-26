using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace DzAssets.Preview.Gemeinsam;

/// <summary>
/// Kamera, die um einen Zielpunkt kreist. Von der Modellvorschau und der
/// Gelaendeansicht gemeinsam genutzt — die Bedienung soll in beiden
/// gleich sein, und zweimal dieselbe Mathematik zu pflegen waere eine
/// Quelle stiller Unterschiede.
/// </summary>
public sealed class OrbitKamera(PerspectiveCamera kamera)
{
    private Point _letzteMausposition;
    private bool _dreht;
    private bool _verschiebt;

    public PerspectiveCamera Kamera { get; } = kamera;

    public Point3D Zielpunkt { get; set; } = new(0, 0, 0);
    public double Abstand { get; set; } = 5;

    /// <summary>Drehung um die senkrechte Achse, im Bogenmass.</summary>
    public double DrehungUmY { get; set; }

    /// <summary>Neigung, im Bogenmass. Wird auf knapp unter senkrecht begrenzt.</summary>
    public double DrehungUmX { get; set; } = 0.45;

    public double KleinsterAbstand { get; set; } = 0.05;
    public double GroessterAbstand { get; set; } = 200_000;

    public bool ZiehtGerade => _dreht || _verschiebt;

    /// <summary>Rahmt einen Quader ein.</summary>
    public void Einrahmen(Point3D min, Point3D max, double zugabe = 1.9)
    {
        Zielpunkt = new Point3D(
            (min.X + max.X) / 2,
            (min.Y + max.Y) / 2,
            (min.Z + max.Z) / 2);

        // Die Raumdiagonale, nicht die laengste Kante: bei einem breiten,
        // flachen Objekt ragt sonst je nach Blickrichtung eine Ecke aus
        // dem Bild.
        var dx = max.X - min.X;
        var dy = max.Y - min.Y;
        var dz = max.Z - min.Z;
        var radius = Math.Max(0.5, Math.Sqrt(dx * dx + dy * dy + dz * dz) / 2);

        Abstand = Math.Clamp(radius * zugabe, KleinsterAbstand, GroessterAbstand);
        Anwenden();
    }

    public void Anwenden()
    {
        DrehungUmX = Math.Clamp(DrehungUmX, -1.5, 1.5);
        Abstand = Math.Clamp(Abstand, KleinsterAbstand, GroessterAbstand);

        var x = Abstand * Math.Cos(DrehungUmX) * Math.Sin(DrehungUmY);
        var y = Abstand * Math.Sin(DrehungUmX);
        var z = Abstand * Math.Cos(DrehungUmX) * Math.Cos(DrehungUmY);

        var position = new Point3D(Zielpunkt.X + x, Zielpunkt.Y + y, Zielpunkt.Z + z);
        Kamera.Position = position;
        Kamera.LookDirection = Zielpunkt - position;
        Kamera.NearPlaneDistance = Math.Max(0.01, Abstand / 500);
        Kamera.FarPlaneDistance = Math.Max(200, Abstand * 60);
    }

    public void MausRunter(MouseButtonEventArgs e, IInputElement bezug)
    {
        _letzteMausposition = e.GetPosition(bezug);

        if (e.ChangedButton == MouseButton.Left) _dreht = true;
        else if (e.ChangedButton is MouseButton.Right or MouseButton.Middle) _verschiebt = true;
    }

    public void MausHoch()
    {
        _dreht = false;
        _verschiebt = false;
    }

    public bool MausBewegt(MouseEventArgs e, IInputElement bezug)
    {
        if (!_dreht && !_verschiebt) return false;

        var jetzt = e.GetPosition(bezug);
        var dx = jetzt.X - _letzteMausposition.X;
        var dy = jetzt.Y - _letzteMausposition.Y;
        _letzteMausposition = jetzt;

        if (_dreht)
        {
            DrehungUmY -= dx * 0.01;
            DrehungUmX = Math.Clamp(DrehungUmX + dy * 0.01, -1.5, 1.5);
        }
        else
        {
            var richtung = Kamera.LookDirection;
            richtung.Normalize();
            var rechts = Vector3D.CrossProduct(richtung, Kamera.UpDirection);
            rechts.Normalize();
            var hoch = Vector3D.CrossProduct(rechts, richtung);

            var faktor = Abstand * 0.0016;
            Zielpunkt -= rechts * (dx * faktor);
            Zielpunkt += hoch * (dy * faktor);
        }

        Anwenden();
        return true;
    }

    public void Rad(int delta)
    {
        Abstand *= delta > 0 ? 0.88 : 1.136;
        Anwenden();
    }
}
