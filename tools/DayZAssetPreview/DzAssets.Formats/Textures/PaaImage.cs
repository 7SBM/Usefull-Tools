using System.IO;
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
    /// <summary>Groesste zugelassene Kantenlaenge. DayZ-Texturen bleiben weit darunter.</summary>
    private const int MaxKante = 8192;

    /// <summary>Obergrenze fuer die entpackten Rohdaten einer Mipmap (128 MiB).</summary>
    private const int MaxRohBytes = 128 * 1024 * 1024;

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
        catch (InvalidDataException fehler)
        {
            throw new InvalidDataException($"PAA konnte nicht gelesen werden: {path} — {fehler.Message}", fehler);
        }
        catch (Exception fehler)
        {
            throw new InvalidDataException($"PAA konnte nicht gelesen werden: {path}", fehler);
        }
    }

    public static PaaImage Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.Length < 8)
            throw new InvalidDataException("PAA-Datei ist zu kurz.");

        using var leser = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        var kennung = leser.ReadUInt16();
        var format = KennungDeuten(kennung, out var hatKennung);
        if (!hatKennung)
            stream.Position -= 2;

        UeberspringeTaggs(leser, stream);

        var palettenGroesse = leser.ReadUInt16();
        if (palettenGroesse > 0)
            stream.Position += palettenGroesse * 3;

        var breiteRoh = leser.ReadUInt16();
        var hoehe = leser.ReadUInt16();
        var lzoKomprimiert = (breiteRoh & 0x8000) != 0;
        var breite = (ushort)(breiteRoh & 0x7FFF);

        if (breite == 0 || hoehe == 0)
            throw new InvalidDataException("PAA enthaelt keine Mipmap.");

        // Plausibilitaetsgrenzen. Ohne sie fuehrt eine fehlgedeutete
        // Kennung zu Muellwerten fuer Breite und Hoehe, und der Entpacker
        // laeuft mit einer erwarteten Groesse im Gigabytebereich los.
        if (breite > MaxKante || hoehe > MaxKante)
            throw new InvalidDataException($"PAA nennt eine unplausible Groesse: {breite}x{hoehe}");

        var laenge = leser.ReadByte() | (leser.ReadByte() << 8) | (leser.ReadByte() << 16);
        if (laenge <= 0 || laenge > stream.Length)
            throw new InvalidDataException($"PAA nennt eine unplausible Datenlaenge: {laenge}");

        var erwartet = ErwarteteGroesse(format, breite, hoehe);
        if (erwartet <= 0 || erwartet > MaxRohBytes)
            throw new InvalidDataException($"PAA erwartet {erwartet} Bytes Bilddaten — unplausibel.");
        var entpackt = Entpacken(stream, format, lzoKomprimiert, laenge, erwartet);
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

    private static PaaFormat KennungDeuten(ushort kennung, out bool hatKennung)
    {
        // Achtung: die Kennung steht als Little-Endian-ushort in der Datei.
        // Die Bytefolge "01 FF" einer DXT1-Textur ergibt gelesen 0xFF01 —
        // nicht 0x01FF. Am 2026-08-26 an 50 echten Texturen geprueft:
        // 15x DXT1, 35x DXT5, keine andere Kennung.
        switch (kennung)
        {
            case 0xFF01: hatKennung = true; return PaaFormat.Dxt1;
            case 0xFF02: hatKennung = true; return PaaFormat.Dxt2;
            case 0xFF03: hatKennung = true; return PaaFormat.Dxt3;
            case 0xFF04: hatKennung = true; return PaaFormat.Dxt4;
            case 0xFF05: hatKennung = true; return PaaFormat.Dxt5;
            case 0x4444: hatKennung = true; return PaaFormat.Argb4444;
            case 0x1555: hatKennung = true; return PaaFormat.Argb1555;
            case 0x8080: hatKennung = true; return PaaFormat.Ai88;
            default: hatKennung = false; return PaaFormat.Argb8888;
        }
    }

    private static void UeberspringeTaggs(BinaryReader leser, Stream stream)
    {
        while (stream.Position + 12 <= stream.Length)
        {
            var merker = stream.Position;
            var kopf = leser.ReadBytes(4);

            if (kopf.Length < 4
                || kopf[0] != (byte)'G' || kopf[1] != (byte)'G'
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

    private static byte[] Entpacken(Stream stream, PaaFormat format, bool lzoKomprimiert,
                                    int laenge, int erwartet)
    {
        var istDxt = format is PaaFormat.Dxt1 or PaaFormat.Dxt2 or PaaFormat.Dxt3
                            or PaaFormat.Dxt4 or PaaFormat.Dxt5;

        if (istDxt)
        {
            if (!lzoKomprimiert)
            {
                var roh = new byte[Math.Min(laenge, erwartet)];
                stream.ReadExactly(roh);
                return roh;
            }

            return LZO.readLZO(stream, (uint)erwartet);
        }

        // Nicht-DXT-Formate sind in PAA immer LZSS-komprimiert und
        // verwenden dort die vorzeichenbehaftete Pruefsumme (inPAA).
        LZSS.readLZSS(stream, out var ziel, (uint)erwartet, useSignedChecksum: true);
        return ziel;
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
