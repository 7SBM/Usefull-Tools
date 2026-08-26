using DzAssets.Formats.Textures;

namespace DzAssets.Tests;

public class DxtDecoderTests
{
    // Ein BC1-Block: color0 = reines Rot (0xF800), color1 = reines Blau (0x001F).
    // color0 > color1, also der undurchsichtige Vier-Farben-Modus.
    // Indextabelle 0x00000000 -> alle 16 Pixel nehmen color0 (Rot).
    private static byte[] BlockRot() =>
    [
        0x00, 0xF8,             // color0 = 0xF800 little endian
        0x1F, 0x00,             // color1 = 0x001F little endian
        0x00, 0x00, 0x00, 0x00, // Indizes: alle 0
    ];

    [Fact]
    public void Bc1_Vierfarbmodus_gibt_color0_als_undurchsichtiges_Rot()
    {
        var pixel = DxtDecoder.DecodeBc1(BlockRot(), 4, 4);

        Assert.Equal(4 * 4 * 4, pixel.Length);
        Assert.Equal(0x00, pixel[0]);   // B
        Assert.Equal(0x00, pixel[1]);   // G
        Assert.Equal(0xFF, pixel[2]);   // R  (5 Bit 0x1F -> 255)
        Assert.Equal(0xFF, pixel[3]);   // A
    }

    [Fact]
    public void Bc1_faerbt_alle_sechzehn_Pixel_des_Blocks()
    {
        var pixel = DxtDecoder.DecodeBc1(BlockRot(), 4, 4);

        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(0xFF, pixel[i * 4 + 2]);   // R
            Assert.Equal(0xFF, pixel[i * 4 + 3]);   // A
        }
    }

    [Fact]
    public void Bc1_Dreifarbmodus_macht_Index_drei_durchsichtig()
    {
        // color0 = 0x001F (Blau), color1 = 0xF800 (Rot) -> color0 < color1
        // Indizes: alle 3 -> durchsichtiges Schwarz
        byte[] block = [0x1F, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF];

        var pixel = DxtDecoder.DecodeBc1(block, 4, 4);

        Assert.Equal(0x00, pixel[3]);
    }

    [Fact]
    public void Bc3_liest_den_Alphakanal_aus_dem_ersten_Halbblock()
    {
        // Alpha-Halbblock: a0 = 255, a1 = 0, alle Indizes 0 -> Alpha 255.
        var block = new byte[16];
        block[0] = 0xFF;
        block[1] = 0x00;
        BlockRot().CopyTo(block, 8);

        var pixel = DxtDecoder.DecodeBc3(block, 4, 4);

        Assert.Equal(4 * 4 * 4, pixel.Length);
        Assert.Equal(0xFF, pixel[3]);   // A
        Assert.Equal(0xFF, pixel[2]);   // R
    }

    [Fact]
    public void Bc3_mit_Alpha_null_liefert_durchsichtige_Pixel()
    {
        var block = new byte[16];
        block[0] = 0x00;
        block[1] = 0x00;
        BlockRot().CopyTo(block, 8);

        var pixel = DxtDecoder.DecodeBc3(block, 4, 4);

        Assert.Equal(0x00, pixel[3]);
    }

    [Fact]
    public void Bc3_nutzt_bei_c0_kleiner_c1_trotzdem_den_Vierfarbmodus()
    {
        // Bei BC3 liefert der Alpha-Halbblock die Transparenz. Der
        // Farb-Halbblock hat deshalb IMMER vier Farben, auch wenn
        // color0 <= color1 ist.
        var block = new byte[16];
        block[0] = 0xFF;                 // a0
        block[1] = 0xFF;                 // a1 -> alles undurchsichtig
        block[8] = 0x1F; block[9] = 0x00;    // color0 = Blau
        block[10] = 0x00; block[11] = 0xF8;  // color1 = Rot
        block[12] = 0xFF; block[13] = 0xFF; block[14] = 0xFF; block[15] = 0xFF; // Index 3

        var pixel = DxtDecoder.DecodeBc3(block, 4, 4);

        // Im Vierfarbmodus ist Index 3 eine Mischfarbe, nicht durchsichtig.
        Assert.Equal(0xFF, pixel[3]);
    }

    [Fact]
    public void Nicht_blockausgerichtete_Groessen_werden_zugeschnitten()
    {
        var pixel = DxtDecoder.DecodeBc1(BlockRot(), 3, 2);

        Assert.Equal(3 * 2 * 4, pixel.Length);
        Assert.Equal(0xFF, pixel[2]);
    }

    [Fact]
    public void Eine_abgeschnittene_Datei_fuehrt_nicht_zu_einer_Ausnahme()
    {
        // Zwei Bloecke angefordert, nur einer geliefert.
        var pixel = DxtDecoder.DecodeBc1(BlockRot(), 8, 4);

        Assert.Equal(8 * 4 * 4, pixel.Length);
    }

    [Fact]
    public void Eine_ungueltige_Groesse_wird_abgewiesen()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DxtDecoder.DecodeBc1(BlockRot(), 0, 4));
    }
}
