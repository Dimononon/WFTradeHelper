using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;
using WFTradeHelper.Core.Models;

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

    public TradeResult EvaluateTrade()
    {
        var tradeResult = new TradeResult();
        bool isVerticalOffset = false;

        var itemSlots = _screenService.GetScaledItemSlots();
        var overlaySlots = Configuration.GetBaseOverlaySlots()
        .Select(r => new Rectangle(
            (int)(r.X * _screenService.ScaleX),
            (int)(r.Y * _screenService.ScaleY),
            (int)(r.Width * _screenService.ScaleX),
            (int)(r.Height * _screenService.ScaleY)))
        .ToList();
        int slotIndex = 1;

#if DEBUG
        using (var fullScreenBitmapForDebug = _screenService.CaptureScreen())
        {
            string fullScreenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "full_screen_capture.png");
            fullScreenBitmapForDebug.Save(fullScreenPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"[DEBUG] Full screenshot saved: {Path.GetFileName(fullScreenPath)}");
        }
#endif

        foreach (var slot in itemSlots)
        {
            if (slot.Right > _screenService.ScreenWidth || slot.Bottom > _screenService.ScreenHeight || slot.Y < 0)
            {
                slotIndex++;
                continue;
            }

            string finalName = null;
            string recognizedText = "(not recognized)";
            //try 1.1
            using (var itemBitmap = _screenService.CaptureRegion(slot))
            using (var preprocessedBitmap = ScreenService.PreprocessImage(itemBitmap))
            {
#if DEBUG
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"slot_{slotIndex}.png");
                preprocessedBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
#endif
                recognizedText = _ocrService.RecognizeText(preprocessedBitmap);
                finalName = _itemDatabase.FindBestMatch(recognizedText);

                int scaledCropTopPixels = (int)(Configuration.OcrCropTopPixels * _screenService.ScaleY);
                //try 1.2
                if (finalName == null && preprocessedBitmap.Height > scaledCropTopPixels)
                {
                    using (var croppedBitmap = ScreenService.CropTop(preprocessedBitmap, scaledCropTopPixels))
                    {
                        string croppedText = _ocrService.RecognizeText(croppedBitmap, PageSegMode.SingleLine);
                        string croppedName = _itemDatabase.FindBestMatch(croppedText);
                        if (croppedName != null)
                        {
                            finalName = croppedName;
                            recognizedText = croppedText;
                        }
                    }
                }
            }

            //try 2
            if (finalName == null)
            {
                isVerticalOffset = true;
                int scaledYOffset = (int)(Configuration.UiVerticalOffset * _screenService.ScaleY);
                var shiftedSlot = new Rectangle(slot.X, slot.Y + scaledYOffset, slot.Width, slot.Height);

                if (shiftedSlot.Bottom <= _screenService.ScreenHeight)
                {
                    using (var shiftedItemBitmap = _screenService.CaptureRegion(shiftedSlot))
                    using (var shiftedPreprocessedBitmap = ScreenService.PreprocessImage(shiftedItemBitmap))
                    {
                        string shiftedRecognizedText = _ocrService.RecognizeText(shiftedPreprocessedBitmap);
                        string shiftedFinalName = _itemDatabase.FindBestMatch(shiftedRecognizedText);

                        if (shiftedFinalName != null)
                        {
                            finalName = shiftedFinalName;
                            recognizedText = shiftedRecognizedText;
                        }
                    }
                }
            }

            var scanResult = ProcessSlot(slotIndex, finalName, recognizedText);
            tradeResult.ScanResults.Add(scanResult);

            var overlayInfo = new OverlayInfo
            {
                Bounds = overlaySlots[slotIndex - 1],
                Text = scanResult.Status == "OK" ? scanResult.ItemName : "Not Found",
                IsSuccess = scanResult.Status == "OK"
            };
            tradeResult.OverlayElements.Add(overlayInfo);
            slotIndex++;
        }
        tradeResult.TotalPlatinum = tradeResult.ScanResults.Sum(r => r.PlatinumValue);
        tradeResult.IsVerticalOffset = isVerticalOffset;
        return tradeResult;
    }

    private ScanResult ProcessSlot(int slotIndex, string finalName, string originalText)
    {
        var result = new ScanResult
        {
            SlotNumber = slotIndex,
            ItemName = originalText.Replace("\n", " ").Replace("|", "I"),
            Status = "Not Found"
        };

        if (finalName != null)
        {
            var matchedItem = _itemDatabase.GetItemByName(finalName);
            if (matchedItem != null && matchedItem.Ducats.HasValue)
            {
                result.ItemName = finalName;
                result.Ducats = matchedItem.Ducats.Value;
                result.PlatinumValue = CalculatePlatCost(result.Ducats);
                result.Status = "OK";
            }
            else
            {
                result.ItemName = finalName;
                result.Status = "Item data error";
            }
        }
        return result;
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
