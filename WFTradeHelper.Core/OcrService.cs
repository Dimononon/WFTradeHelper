using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace WFTradeHelper.Core;
public class OcrService : IDisposable
{
    private readonly TesseractEngine _engine;

    public OcrService()
    {
        _engine = new TesseractEngine(
            $"{AppDomain.CurrentDomain.BaseDirectory}/tessdata",
            "eng",
            EngineMode.Default,
            null,
            new Dictionary<string, object>
            {
                    {"tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz "},
                    {"tessedit_dump_choices", "0" }
            },
            false);
    }

    public string RecognizeText(Bitmap image, PageSegMode mode = PageSegMode.SparseText)
    {
        using (var pix = Pix.LoadFromMemory(GetPngBytes(image)))
        using (var page = _engine.Process(pix, mode))
        {
            return page.GetText().Trim();
        }
    }
    public string RecognizeDigits(Bitmap image)
    {
        _engine.SetVariable("tessedit_char_whitelist", "0123456789");

        string result;
        using (var pix = Pix.LoadFromMemory(GetPngBytes(image)))
        using (var page = _engine.Process(pix, PageSegMode.SingleWord))
        {
            result = page.GetText().Trim();
        }

        _engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ");
        return result;
    }
    private byte[] GetPngBytes(Bitmap bmp)
    {
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }
}
