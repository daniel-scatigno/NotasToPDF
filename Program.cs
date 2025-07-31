using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

class Program
{
    const int dpi = 300;
    const double inchToPoint = 72.0;
    const double inchToPixel = dpi;

    // A4 landscape at 300 DPI
    static readonly double PageWidthInches = 11.69;
    static readonly double PageHeightInches = 8.27;
    static readonly double PageWidthPt = PageWidthInches * inchToPoint;
    static readonly double PageHeightPt = PageHeightInches * inchToPoint;
    static readonly int PageWidthPx = (int)(PageWidthInches * inchToPixel);
    static readonly int PageHeightPx = (int)(PageHeightInches * inchToPixel);

    static void Main(string[] args)
    {
        string folder = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string[] extensions = new[] { ".png", ".jpg", ".jpeg" };

        var imagePaths = Directory.GetFiles(folder)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        if (imagePaths.Count == 0)
        {
            Console.WriteLine("No image files found.");
            return;
        }

        const int maxImagesPerPage = 10;
        var document = new PdfDocument();

        var imageGroups = imagePaths
            .Select(Image.Load<Rgba32>)
            .Select((img, idx) => new { Index = idx, Image = img })
            .GroupBy(x => x.Index / maxImagesPerPage);

        foreach (var group in imageGroups)
        {
            var images = group.Select(x => x.Image).ToList();

            // Resize all to fit height
            var resized = images.Select(img =>
            {
                double scale = (double)PageHeightPx / img.Height;
                int targetWidth = (int)(img.Width * scale);
                int targetHeight = PageHeightPx;
                return new
                {
                    img,
                    WidthPx = targetWidth,
                    HeightPx = targetHeight
                };
            }).ToList();

            // Sum of all widths
            int totalWidthPx = resized.Sum(r => r.WidthPx);

            // Global scale if needed to fit width
            double globalScale = totalWidthPx > PageWidthPx ? (double)PageWidthPx / totalWidthPx : 1.0;

            var page = document.AddPage();
            page.Orientation = PdfSharpCore.PageOrientation.Landscape;
            page.Width = PageWidthPt;
            page.Height = PageHeightPt;

            using var gfx = XGraphics.FromPdfPage(page);

            double currentXPt = 0;
            foreach (var imgData in resized)
            {
                double finalWidthPx = imgData.WidthPx * globalScale;
                double finalHeightPx = imgData.HeightPx * globalScale;

                double finalWidthPt = finalWidthPx * inchToPoint / dpi;
                double finalHeightPt = finalHeightPx * inchToPoint / dpi;

                double y = (PageHeightPt - finalHeightPt) / 2;

                using var ms = new MemoryStream();
                imgData.img.SaveAsPng(ms);
                ms.Seek(0, SeekOrigin.Begin);

                var xImage = XImage.FromStream(() => ms);
                gfx.DrawImage(xImage, currentXPt, y, finalWidthPt, finalHeightPt);
                currentXPt += finalWidthPt;
            }

            // Dispose group images
            foreach (var img in images)
                img.Dispose();
        }

        string outputPath = Path.Combine(folder, "Output_RowLayout_Paged.pdf");
        document.Save(outputPath);
        Console.WriteLine($"✅ PDF saved to: {outputPath}");
    }
}
