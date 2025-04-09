using DefectDetection.Helpers;
using SixLabors.Fonts;
using System.Runtime.InteropServices.WindowsRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Windows.Graphics.Imaging;

public class ImageProcessor
{
    // 颜色预定义（根据类别数量扩展）
    private static readonly Color[] Palette = new[]
    {
        Color.Red,
        Color.Green,
        Color.Blue,
        Color.Yellow,
        Color.Purple,
        Color.Cyan
    };

    public static async Task<SoftwareBitmap> DrawDetectionsAsync(
        SoftwareBitmap originBitmap,
        IEnumerable<Detection> detections,
        float fontScale = 1.0f)
    {
        // 转换为ImageSharp图像格式
        using var image = await ConvertToImageSharp(originBitmap);

        // 绘制所有检测框
        foreach (var det in detections)
        {
            DrawDetection(image, det, fontScale);
        }

        // 转换回SoftwareBitmap
        return await ConvertToSoftwareBitmap(image);
    }

    private static async Task<Image<Rgba32>> ConvertToImageSharp(SoftwareBitmap softwareBitmap)
    {
        // 确保格式为BGRA8
        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
        {
            softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8);
        }

        var bitmapBuffer = new byte[softwareBitmap.PixelWidth * softwareBitmap.PixelHeight * 4];
        softwareBitmap.CopyToBuffer(bitmapBuffer.AsBuffer());

        return await Task.Run(() =>
            Image.LoadPixelData<Bgra32>(bitmapBuffer, softwareBitmap.PixelWidth, softwareBitmap.PixelHeight)
                .CloneAs<Rgba32>());
    }

    private static async Task<SoftwareBitmap> ConvertToSoftwareBitmap(Image<Rgba32> image)
    {
        var softwareBitmap = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            image.Width,
            image.Height,
            BitmapAlphaMode.Premultiplied);

        await Task.Run(() =>
        {
            image.ProcessPixelRows(accessor =>
            {
                var buffer = new byte[accessor.Height * accessor.Width * 4];

                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        var idx = (y * accessor.Width + x) * 4;
                        buffer[idx] = row[x].B;     // B
                        buffer[idx + 1] = row[x].G; // G
                        buffer[idx + 2] = row[x].R; // R
                        buffer[idx + 3] = row[x].A;   // A
                    }
                }

                softwareBitmap.CopyFromBuffer(buffer.AsBuffer());
            });
        });

        return softwareBitmap;
    }

    private static void DrawDetection(Image<Rgba32> image, Detection det, float fontScale)
    {
        // 转换归一化坐标到实际像素坐标
        var rect = new RectangleF(
            det.BBox[0] - det.BBox[2] / 2,
            det.BBox[1] - det.BBox[2] / 2,
            det.BBox[2],
            det.BBox[3]);

        var options = new DrawingOptions
        {
            GraphicsOptions = new GraphicsOptions
            {
                Antialias = true,
                ColorBlendingMode = PixelColorBlendingMode.Normal
            }
        };

        // 绘制边框
        image.Mutate(ctx => ctx.Draw(
            options,
            Palette[det.ClassId % Palette.Length],
            6f, // 线宽
            rect));

        // 绘制文本标签
        var text = $"{det.Label} {det.Confidence:0.00}";
        var font = SystemFonts.CreateFont("Arial", 12 * fontScale, FontStyle.Bold);
        var x = (int)Math.Max(det.BBox[0] - det.BBox[2] / 2, 0);
        var y = (int)Math.Max(det.BBox[1] - det.BBox[2] / 2, 0);
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        image.Mutate(ctx => ctx.DrawText(text, font, 
            Palette[det.ClassId % Palette.Length],
            new Point(x, (int)(y - size.Height - 1))));
    }
}