using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFTradeHelper.Core;
public class ScreenService
{
    public int ScreenWidth { get; }
    public int ScreenHeight { get; }
    public double ScaleX { get; }
    public double ScaleY { get; }

    public ScreenService()
    {
        ScreenWidth = Screen.AllScreens[0].Bounds.Width;
        ScreenHeight = Screen.AllScreens[0].Bounds.Height;
        ScaleX = ScreenWidth / Configuration.BaseScreenWidth;
        ScaleY = ScreenHeight / Configuration.BaseScreenHeight;
    }

    public Bitmap CaptureScreen()
    {
        var bitmap = new Bitmap(ScreenWidth, ScreenHeight);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
        }
        return bitmap;
    }

    public List<Rectangle> GetScaledItemSlots()
    {
        return Configuration.GetBaseItemSlots().Select(r => new Rectangle(
            (int)(r.X * ScaleX),
            (int)(r.Y * ScaleY),
            (int)(r.Width * ScaleX),
            (int)(r.Height * ScaleY)
        )).ToList();
    }

    public static Bitmap PreprocessImage(Bitmap original)
    {
        Bitmap processed = new Bitmap(original.Width, original.Height);
        const int threshold = 130;

        for (int i = 0; i < original.Width; i++)
        {
            for (int j = 0; j < original.Height; j++)
            {
                Color c = original.GetPixel(i, j);
                int gray = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
                processed.SetPixel(i, j, gray > threshold ? Color.Black : Color.White);
            }
        }
        return processed;
    }

    public static Bitmap CropTop(Bitmap original, int cropHeight)
    {
        if (cropHeight <= 0 || original.Height <= cropHeight)
            return original;

        Rectangle cropArea = new Rectangle(0, cropHeight, original.Width, original.Height - cropHeight);
        return original.Clone(cropArea, original.PixelFormat);
    }
}
