namespace DzAssets.Formats.Textures;

/// <summary>
/// Dekodiert BC1- (DXT1) und BC3-Bloecke (DXT5) nach BGRA32.
/// Reine Rechnung ohne Dateizugriff, damit unabhaengig pruefbar.
/// </summary>
public static class DxtDecoder
{
    public static byte[] DecodeBc1(byte[] data, int width, int height)
        => Decode(data, width, height, blockSize: 8, hatAlphaBlock: false);

    public static byte[] DecodeBc3(byte[] data, int width, int height)
        => Decode(data, width, height, blockSize: 16, hatAlphaBlock: true);

    private static byte[] Decode(byte[] data, int width, int height, int blockSize, bool hatAlphaBlock)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Breite muss positiv sein.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Hoehe muss positiv sein.");

        var ziel = new byte[width * height * 4];
        var bloeckeX = (width + 3) / 4;
        var bloeckeY = (height + 3) / 4;

        var farben = new byte[4 * 4];   // 4 Farben, je BGRA
        var alpha = new byte[8];

        for (var by = 0; by < bloeckeY; by++)
        {
            for (var bx = 0; bx < bloeckeX; bx++)
            {
                var offset = (by * bloeckeX + bx) * blockSize;
                if (offset + blockSize > data.Length)
                    return ziel;   // abgeschnittene Datei: Rest bleibt schwarz-durchsichtig

                var farbOffset = offset;
                ulong alphaIndizes = 0;

                if (hatAlphaBlock)
                {
                    LeseAlphaPalette(data, offset, alpha);
                    alphaIndizes = LeseAlphaIndizes(data, offset);
                    farbOffset = offset + 8;
                }

                LeseFarbPalette(data, farbOffset, farben, alphaAusFarbblock: !hatAlphaBlock);
                var indizes = BitConverter.ToUInt32(data, farbOffset + 4);

                for (var py = 0; py < 4; py++)
                {
                    var y = by * 4 + py;
                    if (y >= height) break;

                    for (var px = 0; px < 4; px++)
                    {
                        var x = bx * 4 + px;
                        if (x >= width) continue;

                        var i = py * 4 + px;
                        var farbIndex = (int)((indizes >> (i * 2)) & 0x3);
                        var z = (y * width + x) * 4;

                        ziel[z + 0] = farben[farbIndex * 4 + 0];
                        ziel[z + 1] = farben[farbIndex * 4 + 1];
                        ziel[z + 2] = farben[farbIndex * 4 + 2];
                        ziel[z + 3] = hatAlphaBlock
                            ? alpha[(int)((alphaIndizes >> (i * 3)) & 0x7)]
                            : farben[farbIndex * 4 + 3];
                    }
                }
            }
        }

        return ziel;
    }

    private static void LeseFarbPalette(byte[] data, int offset, byte[] farben, bool alphaAusFarbblock)
    {
        var c0 = (ushort)(data[offset] | (data[offset + 1] << 8));
        var c1 = (ushort)(data[offset + 2] | (data[offset + 3] << 8));

        Entpacke565(c0, farben, 0);
        Entpacke565(c1, farben, 1);

        // Der Dreifarbmodus (color0 <= color1) gilt nur fuer BC1, wo der
        // Alphakanal aus dem Farbblock kommt. Bei BC3 liefert der
        // Alpha-Halbblock die Transparenz, dort sind es immer vier Farben.
        var vierFarben = !alphaAusFarbblock || c0 > c1;

        if (vierFarben)
        {
            for (var k = 0; k < 3; k++)
            {
                farben[2 * 4 + k] = (byte)((2 * farben[0 * 4 + k] + farben[1 * 4 + k]) / 3);
                farben[3 * 4 + k] = (byte)((farben[0 * 4 + k] + 2 * farben[1 * 4 + k]) / 3);
            }
            farben[2 * 4 + 3] = 0xFF;
            farben[3 * 4 + 3] = 0xFF;
        }
        else
        {
            for (var k = 0; k < 3; k++)
            {
                farben[2 * 4 + k] = (byte)((farben[0 * 4 + k] + farben[1 * 4 + k]) / 2);
                farben[3 * 4 + k] = 0;
            }
            farben[2 * 4 + 3] = 0xFF;
            farben[3 * 4 + 3] = 0x00;   // durchsichtig
        }
    }

    private static void Entpacke565(ushort wert, byte[] farben, int slot)
    {
        var r5 = (wert >> 11) & 0x1F;
        var g6 = (wert >> 5) & 0x3F;
        var b5 = wert & 0x1F;

        farben[slot * 4 + 0] = (byte)((b5 << 3) | (b5 >> 2));   // B
        farben[slot * 4 + 1] = (byte)((g6 << 2) | (g6 >> 4));   // G
        farben[slot * 4 + 2] = (byte)((r5 << 3) | (r5 >> 2));   // R
        farben[slot * 4 + 3] = 0xFF;                            // A
    }

    private static void LeseAlphaPalette(byte[] data, int offset, byte[] alpha)
    {
        alpha[0] = data[offset];
        alpha[1] = data[offset + 1];

        if (alpha[0] > alpha[1])
        {
            for (var i = 1; i <= 6; i++)
                alpha[i + 1] = (byte)(((7 - i) * alpha[0] + i * alpha[1]) / 7);
        }
        else
        {
            for (var i = 1; i <= 4; i++)
                alpha[i + 1] = (byte)(((5 - i) * alpha[0] + i * alpha[1]) / 5);
            alpha[6] = 0x00;
            alpha[7] = 0xFF;
        }
    }

    private static ulong LeseAlphaIndizes(byte[] data, int offset)
    {
        ulong wert = 0;
        for (var i = 0; i < 6; i++)
            wert |= (ulong)data[offset + 2 + i] << (i * 8);
        return wert;
    }
}
