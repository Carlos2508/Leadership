using System.Globalization;
using System.Text;
using QRCoder;
using ZXing;
using ZXing.OneD;

namespace SchoolPlatform.Host.Documents;

/// <summary>Vector barcodes for printed documents: QR (camera readers) and Code 128 (USB/handheld 1D readers).</summary>
public static class Barcodes
{
    public static string QrSvg(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(4, "#000000", "#ffffff", true, SvgQRCode.SizingMode.ViewBoxAttribute);
    }

    /// <summary>Code 128 as SVG rectangles (one unit per module, quiet zones included).</summary>
    public static string Code128Svg(string content, int height = 40)
    {
        var matrix = new Code128Writer().encode(content, BarcodeFormat.CODE_128, 0, height);
        var width = matrix.Width;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" preserveAspectRatio=\"none\">");
        svg.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{height}\" fill=\"#fff\"/>");
        var x = 0;
        while (x < width)
        {
            if (!matrix[x, 0]) { x++; continue; }
            var start = x;
            while (x < width && matrix[x, 0]) x++;
            svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{start}\" width=\"{x - start}\" height=\"{height}\" fill=\"#000\"/>");
        }
        return svg.Append("</svg>").ToString();
    }

    /// <summary>Modules of a Code 128 symbol, for tests that decode what is printed.</summary>
    public static bool[] Code128Modules(string content)
    {
        var matrix = new Code128Writer().encode(content, BarcodeFormat.CODE_128, 0, 1);
        return Enumerable.Range(0, matrix.Width).Select(x => matrix[x, 0]).ToArray();
    }
}
