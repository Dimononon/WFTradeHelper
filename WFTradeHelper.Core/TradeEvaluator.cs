using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace WFTradeHelper.Core;
public class TradeEvaluator
{
    private readonly ScreenService _screenService;
    private readonly OcrService _ocrService;
    private readonly ItemDatabase _itemDatabase;

    public TradeEvaluator(ScreenService screenService, OcrService ocrService, ItemDatabase itemDatabase)
    {
        _screenService = screenService;
        _ocrService = ocrService;
        _itemDatabase = itemDatabase;
    }

    public void EvaluateTrade()
    {
        Console.WriteLine("\n--- Start scan (F8) ---");

        using (var screenBitmap = _screenService.CaptureScreen())
        {
            string fullScreenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "full_screen_capture.png");
            screenBitmap.Save(fullScreenPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"Full screenshot saved: {Path.GetFileName(fullScreenPath)}");

            var itemSlots = _screenService.GetScaledItemSlots();
            int platCost = 0;
            int slotIndex = 1;

            foreach (var slot in itemSlots)
            {
                if (slot.Right > _screenService.ScreenWidth || slot.Bottom > _screenService.ScreenHeight)
                {
                    Console.WriteLine($"Error: Slot {slotIndex} not in screen range");
                    slotIndex++;
                    continue;
                }

                string finalName = null;
                string recognizedText = "(not recognized)";

                //1.1 try
                using (var itemBitmap = screenBitmap.Clone(slot, screenBitmap.PixelFormat))
                using (var preprocessedBitmap = ScreenService.PreprocessImage(itemBitmap))
                {
                    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"slot_{slotIndex}.png");
                    preprocessedBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                    recognizedText = _ocrService.RecognizeText(preprocessedBitmap);
                    finalName = _itemDatabase.FindBestMatch(recognizedText);

                    //1.2 try
                    int scaledCropTopPixels = (int)(Configuration.OcrCropTopPixels * _screenService.ScaleY);
                    if (finalName == null && preprocessedBitmap.Height > scaledCropTopPixels)
                    {
                        using (var croppedBitmap = ScreenService.CropTop(preprocessedBitmap, scaledCropTopPixels))
                        {
                            string croppedText = _ocrService.RecognizeText(croppedBitmap, PageSegMode.SingleLine);
                            string croppedName = _itemDatabase.FindBestMatch(croppedText);
                            if (croppedName != null)
                            {
                                finalName = croppedName;
                            }
                        }
                    }
                }
                //2 try
                if (finalName == null)
                {
                    int scaledYOffset = (int)(Configuration.UiVerticalOffset * _screenService.ScaleY);
                    var shiftedSlot = new Rectangle(slot.X, slot.Y + scaledYOffset, slot.Width, slot.Height);

                    if (shiftedSlot.Bottom <= _screenService.ScreenHeight)
                    {
                        using (var shiftedItemBitmap = screenBitmap.Clone(shiftedSlot, screenBitmap.PixelFormat))
                        using (var shiftedPreprocessedBitmap = ScreenService.PreprocessImage(shiftedItemBitmap))
                        {
                            //2.1 try
                            string shiftedRecognizedText = _ocrService.RecognizeText(shiftedPreprocessedBitmap);
                            string shiftedFinalName = _itemDatabase.FindBestMatch(shiftedRecognizedText);

                            //2.2 try
                            int scaledCropTopPixels = (int)(Configuration.OcrCropTopPixels * _screenService.ScaleY);
                            if (shiftedFinalName == null && shiftedPreprocessedBitmap.Height > scaledCropTopPixels)
                            {
                                using (var croppedBitmap = ScreenService.CropTop(shiftedPreprocessedBitmap, scaledCropTopPixels))
                                {
                                    string croppedText = _ocrService.RecognizeText(croppedBitmap, PageSegMode.SingleLine);
                                    shiftedFinalName = _itemDatabase.FindBestMatch(croppedText);
                                    if (shiftedFinalName != null)
                                    {
                                        recognizedText = croppedText;
                                    }
                                }
                            }

                            if (shiftedFinalName != null)
                            {
                                finalName = shiftedFinalName;
                                if (recognizedText == "(not recognized)") recognizedText = shiftedRecognizedText;
                            }
                        }
                    }
                }
                ProcessRecognizedItem(slotIndex, finalName, recognizedText, ref platCost);
                slotIndex++;
            }
            Console.WriteLine($"--- Stop scan ---\n Total Platinum: {platCost}");
        }
    }

    private void ProcessRecognizedItem(int slotIndex, string finalName, string originalText, ref int platCost)
    {
        if (finalName != null)
        {
            var matchedItem = _itemDatabase.GetItemByName(finalName);
            if (matchedItem != null && matchedItem.Ducats.HasValue)
            {
                int ducatValue = matchedItem.Ducats.Value;
                platCost += CalculatePlatCost(ducatValue);
                Console.WriteLine($"Slot {slotIndex}: {finalName,-50} ({ducatValue,-3} ducats)");
            }
            else
            {
                Console.WriteLine($"Slot {slotIndex}: {finalName,-50} (Item data error)");
            }
        }
        else
        {
            Console.WriteLine($"Slot {slotIndex}: {originalText.Replace("\n", " ").Replace("|", "I"),-50} (Not Found)");
        }
    }
    public bool HasTradeStateChanged()
    {
        var baseTaxArea = Configuration.GetBaseTaxArea();
        var scaledTaxArea = new Rectangle(
            (int)(baseTaxArea.X * _screenService.ScaleX),
            (int)(baseTaxArea.Y * _screenService.ScaleY),
            (int)(baseTaxArea.Width * _screenService.ScaleX),
            (int)(baseTaxArea.Height * _screenService.ScaleY)
        );

        using (var taxBitmap = new Bitmap(scaledTaxArea.Width, scaledTaxArea.Height))
        {
            using (var g = Graphics.FromImage(taxBitmap))
            {
                g.CopyFromScreen(scaledTaxArea.Location, Point.Empty, scaledTaxArea.Size);
            }

            using (var preprocessedBitmap = ScreenService.PreprocessImage(taxBitmap))
            {
                string text = _ocrService.RecognizeDigits(preprocessedBitmap);
                return text != "0";
            }
        }
    }
    private int CalculatePlatCost(int ducats) => ducats switch
    {
        15 => Configuration.PricePer15Ducats,
        25 => Configuration.PricePer25Ducats,
        45 => Configuration.PricePer45Ducats,
        65 => Configuration.PricePer65Ducats,
        100 => Configuration.PricePer100Ducats,
        _ => 0
    };
}
