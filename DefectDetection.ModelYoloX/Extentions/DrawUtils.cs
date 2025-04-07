using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

using Yolov8Net;

namespace YoloX.Net.Extentions;
public class DrawUtils
{
    private readonly Font font;
    public DrawUtils()
    {
        var fontCollection = new FontCollection();
        var fontFamily = fontCollection.Add("ms-apps:///Assets/Fonts/CONSOLA.TTF");
        font = fontFamily.CreateFont(11, FontStyle.Bold);
    }

    public Image DrawBoxes(int modelInputHeight, int modelInputWidth, Image image, Prediction[] predictions)
    {
        foreach (var pred in predictions)
        {
            var originalImageHeight = image.Height;
            var originalImageWidth = image.Width;

            var x = (int)Math.Max(pred.Rectangle.X, 0);
            var y = (int)Math.Max(pred.Rectangle.Y, 0);
            var width = (int)Math.Min(originalImageWidth - x, pred.Rectangle.Width);
            var height = (int)Math.Min(originalImageHeight - y, pred.Rectangle.Height);

            //Note that the output is already scaled to the original image height and width.

            // Bounding Box Text
            string text = $"{pred.Label.Name} [{pred.Score}]";
            var size = TextMeasurer.MeasureSize(text, new TextOptions(font));

            image.Mutate(d => d.Draw(Pens.Solid(Color.Yellow, 2),
                    new Rectangle(x, y, width, height)));

            image.Mutate(d => d.DrawText(text, font, Color.Yellow, new Point(x, (int)(y - size.Height - 1))));

        }
        return image;
    }
}
