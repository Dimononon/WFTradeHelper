using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using Tesseract;
using WFTradeHelper;
using WFTrader.Data;
using WFTrader.Data.Models;
public class ConsoleRedirector : IDisposable
{
    private readonly TextWriter _originalConsoleOut;
    private readonly TextWriter _originalConsoleError;
    private readonly StringWriter _stringWriter;

    public ConsoleRedirector()
    {
        // Зберігаємо оригінальні потоки
        _originalConsoleOut = Console.Out;
        _originalConsoleError = Console.Error;

        // Створюємо порожній потік для захоплення небажаного виводу
        _stringWriter = new StringWriter();

        // Перенаправляємо консольні потоки
        Console.SetOut(_stringWriter);
        Console.SetError(_stringWriter);
    }

    // Метод для повернення захопленого виводу (якщо потрібен)
    public string GetOutput() => _stringWriter.ToString();

    // Метод Dispose для відновлення оригінальних потоків
    public void Dispose()
    {
        Console.SetOut(_originalConsoleOut);
        Console.SetError(_originalConsoleError);
        _stringWriter.Dispose();
    }
}
class Program
{
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    private static bool isF8Pressed = false;

    const string FILENAME = "items.json";

    const int CAPTURE_WIDTH = 1920;
    const int CAPTURE_HEIGHT = 1080;
    private static List<Item> AllItems { get; set; } = new List<Item>();

    const int PRICEPER15 = 1;
    const int PRICEPER25 = 1;
    const int PRICEPER45 = 2;
    const int PRICEPER65 = 3;
    const int PRICEPER100 = 7;

    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        TextWriter originalOut = Console.Out;
        Console.WriteLine($"Capture in {CAPTURE_WIDTH}x{CAPTURE_HEIGHT}.");
        Console.WriteLine("Press F8 to scan items");
        //ExportItemsJson();
        try
        {
            var validItemDictionary = LoadItemDictionary(originalOut);

            while (true)
            {

                short f8State = GetAsyncKeyState((int)Keys.F8);

                if (f8State != 0 && !isF8Pressed)
                {
                    isF8Pressed = true;
                    RecognizeItemsFromTrade(validItemDictionary, originalOut);
                }
                else if (f8State == 0)
                {
                    isF8Pressed = false;
                }
                Thread.Sleep(100);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
    private static void ExportItemsJson()
    {
        const string CONNECTION_STRING = "Host=localhost;Port=5432;Database=wftrader;Username=wfuser;Password=secret";
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(CONNECTION_STRING);

        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FILENAME);


        var jsonItemsData = new List<JsonItem>();
        try
        {
            using (var context = new AppDbContext(optionsBuilder.Options))
            {
                var items = context.Items
                    .Include(i => i.I18N)
                    .Include(i => i.ItemTags)
                        .ThenInclude(it => it.Tag)
                    .Where(item =>
                        item.ItemTags.Any(itemTag =>
                            itemTag.Tag.Name == "prime"
                        )
                    )
                    .Where(item =>
                        item.ItemTags.All(itemTag =>
                            itemTag.Tag.Name != "set"
                        )
                    )
                    .ToList();

                foreach (var item in items)
                {
                    var i18nName = item.I18N.FirstOrDefault()?.Name;
                    if (!string.IsNullOrWhiteSpace(i18nName))
                    {
                        jsonItemsData.Add(new JsonItem { Name = i18nName.ToUpper(), Ducats = item.Ducats });
                    }
                }
            }

            Console.WriteLine($"Loaded data: {jsonItemsData.Count} items.");
            Console.SetOut(new StreamWriter(Stream.Null));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading data: {ex.Message}");
            Console.SetOut(new StreamWriter(Stream.Null));
        }
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(jsonItemsData, options);

            File.WriteAllText(filePath, jsonString);

            Console.WriteLine($"Successfully saved {jsonItemsData.Count} items to '{FILENAME}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving JSON file: {ex.Message}");
        }
    }
    private static List<string> LoadItemDictionary(TextWriter originalOut)
    {
        var dictionary = new List<string>();
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FILENAME);

        var jsonItemsData = new List<JsonItem>();

        try
        {
            if (File.Exists(filePath))
            {
                string jsonString = File.ReadAllText(filePath);

                jsonItemsData = JsonSerializer.Deserialize<List<JsonItem>>(jsonString);

                foreach (var item in jsonItemsData)
                {
                    if (!string.IsNullOrWhiteSpace(item.Name))
                    {
                        dictionary.Add(item.Name.Trim().ToUpper());
                    }
                }
                AllItems.Clear();
                foreach (var jItem in jsonItemsData)
                {
                    AllItems.Add(new Item
                    {
                        Ducats = jItem.Ducats,
                        I18N = new List<ItemI18N> { new ItemI18N { Name = jItem.Name } }
                    });
                }


                Console.SetOut(originalOut);
                Console.WriteLine($"Dictionary loaded from file: {dictionary.Count} items.");
                Console.SetOut(new StreamWriter(Stream.Null));
            }
            else
            {
                Console.SetOut(originalOut);
                Console.WriteLine($"ERROR: Item dictionary file '{FILENAME}' not found. Check directory.");
                Console.SetOut(new StreamWriter(Stream.Null));
            }
        }
        catch (Exception ex)
        {
            Console.SetOut(originalOut);
            Console.WriteLine($"Error reading item file: {ex.Message}");
            Console.SetOut(new StreamWriter(Stream.Null));
        }
        return dictionary;
    }
    public static void RecognizeItemsFromTrade(List<string> validItemDictionary, TextWriter originalOut)
    {
        Console.SetOut(new StreamWriter(Stream.Null));

        try
        {
            const int BASE_WIDTH = 210;
            const int BASE_HEIGHT = 46;
            const int CROP_SIDE_OFFSET = 5;

            const int CROP_TOP_PIXELS = 21;
            const int SET_OFFSET = 59;
            var itemSlots = new List<Rectangle>
            {
                new Rectangle(215 + CROP_SIDE_OFFSET, 847, BASE_WIDTH, BASE_HEIGHT),
                new Rectangle(470 + CROP_SIDE_OFFSET, 847, BASE_WIDTH, BASE_HEIGHT),
                new Rectangle(725 + CROP_SIDE_OFFSET, 847, BASE_WIDTH, BASE_HEIGHT),
                new Rectangle(980 + CROP_SIDE_OFFSET, 847, BASE_WIDTH, BASE_HEIGHT),
                new Rectangle(1235 + CROP_SIDE_OFFSET, 847, BASE_WIDTH, BASE_HEIGHT),
                new Rectangle(1490 + CROP_SIDE_OFFSET, 847, BASE_WIDTH, BASE_HEIGHT)
            };
            using (var redirector = new ConsoleRedirector())
            {
                using (var screenBitmap = new Bitmap(CAPTURE_WIDTH, CAPTURE_HEIGHT))
                {
                    using (var g = Graphics.FromImage(screenBitmap))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, screenBitmap.Size);
                    }

                    Console.SetOut(originalOut);

                    string fullScreenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "full_screen_capture.png");
                    screenBitmap.Save(fullScreenPath, System.Drawing.Imaging.ImageFormat.Png);
                    Console.WriteLine($"Full screenshot saved: {Path.GetFileName(fullScreenPath)}");

                    Console.WriteLine("\n--- Start scan (F8) ---");
                    Console.SetOut(new StreamWriter(Stream.Null));

                    using (var engine = new TesseractEngine($"{AppDomain.CurrentDomain.BaseDirectory}/tessdata", "eng", EngineMode.Default,
                    new string[] { }, 
                    new Dictionary<string, object>
                    {
                        {"tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz "},
                        {"tessedit_dump_choices", "0" }
                    }, false))
                    {
                        Console.SetOut(originalOut);
                        int platCost = 0;
                        int slotIndex = 1;

                        foreach (var slot in itemSlots)
                        {
                            if (slot.Right > CAPTURE_WIDTH || slot.Bottom > CAPTURE_HEIGHT)
                            {
                                Console.WriteLine($"Error: Slot {slotIndex} not in screen range");
                                slotIndex++;
                                continue;
                            }

                            using (var itemBitmap = screenBitmap.Clone(slot, screenBitmap.PixelFormat))
                            using (var preprocessedBitmap = PreprocessImage(itemBitmap))
                            {
                                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"slot_{slotIndex}.png");
                                preprocessedBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                                string recognizedText = "";
                                string finalName = null;

                                using (var pix = Pix.LoadFromMemory(GetPngBytes(preprocessedBitmap)))
                                using (var page = engine.Process(pix, PageSegMode.SparseText))
                                {
                                    recognizedText = page.GetText().Trim();
                                    finalName = FindBestMatch(recognizedText, validItemDictionary);
                                }

                                if (finalName == null && preprocessedBitmap.Height > CROP_TOP_PIXELS)
                                {
                                    using (var croppedBitmap = CropTop(preprocessedBitmap, CROP_TOP_PIXELS))
                                    using (var pixCropped = Pix.LoadFromMemory(GetPngBytes(croppedBitmap)))
                                    using (var pageCropped = engine.Process(pixCropped, PageSegMode.SingleLine))
                                    {
                                        string croppedText = pageCropped.GetText().Trim();
                                        string croppedName = FindBestMatch(croppedText, validItemDictionary);

                                        if (croppedName != null)
                                        {
                                            recognizedText = croppedText;
                                            finalName = croppedName;
                                        }
                                    }
                                }

                                if (finalName != null)
                                {
                                    if (finalName != "PLATINUM")
                                    {
                                        var matchedItem = AllItems.FirstOrDefault(x => x.I18N.FirstOrDefault()?.Name?.Trim().ToUpper() == finalName);

                                        if (matchedItem != null && matchedItem.Ducats.HasValue)
                                        {
                                            int ducatValue = (int)matchedItem.Ducats.Value;
                                            if (ducatValue == 15) platCost += PRICEPER15;
                                            else if (ducatValue == 25) platCost += PRICEPER25;
                                            else if (ducatValue == 45) platCost += PRICEPER45;
                                            else if (ducatValue == 65) platCost += PRICEPER65;
                                            else if (ducatValue == 100) platCost += PRICEPER100;
                                            Console.WriteLine($"Slot {slotIndex}: {finalName,-50} ({ducatValue,-3} ducats)");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Slot {slotIndex}: {finalName,-50} (Item data error)");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Slot {slotIndex}: {finalName,-50}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Slot {slotIndex}: {recognizedText.Replace("\n", " ").Replace("|", "I"),-50} (Not Found)");
                                }
                            }
                            slotIndex++;
                        }
                        Console.WriteLine($"--- Stop scan ---\n Total Platinum: {platCost}");
                    }

                }
            }
        }
        catch (Exception e)
        {
            Console.SetOut(originalOut);
            Console.WriteLine($"Critical Error: {e.Message}");
            Console.WriteLine($"Check 'tessdata' folder and paths.");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
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

                if (gray > threshold)
                {
                    processed.SetPixel(i, j, Color.Black);
                }
                else
                {
                    processed.SetPixel(i, j, Color.White);
                }
            }
        }
        return processed;
    }
    private static Bitmap CropTop(Bitmap original, int cropHeight)
    {
        Rectangle cropArea = new Rectangle(0, cropHeight, original.Width, original.Height - cropHeight);

        if (cropArea.Height <= 0 || cropArea.Width <= 0)
            return original;

        return original.Clone(cropArea, original.PixelFormat);
    }

    private static byte[] GetPngBytes(Bitmap bmp)
    {
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.IsNullOrEmpty(t) ? 0 : t.Length;
        }

        if (string.IsNullOrEmpty(t))
        {
            return s.Length;
        }

        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }
        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static string FindBestMatch(string recognizedText, List<string> dictionary)
    {
        const int MaxErrorTolerance = 4;
        if (string.IsNullOrWhiteSpace(recognizedText)) return null;

        string normalizedRecognized = recognizedText.ToUpper().Replace(" ", "").Replace("\n", "").Replace(":", "").Replace(".", "").Replace(",", "");

        int bestDistance = int.MaxValue;
        string bestMatch = null;

        foreach (var item in dictionary)
        {
            string normalizedItem = item.ToUpper().Replace(" ", "").Replace("\n", "").Replace(":", "").Replace(".", "").Replace(",", "");

            if (Math.Abs(normalizedRecognized.Length - normalizedItem.Length) > MaxErrorTolerance + 5) continue;


            int distanceFull = ComputeLevenshteinDistance(normalizedRecognized, normalizedItem);

            int distanceNoPrefix = int.MaxValue;
            if (normalizedRecognized.Length > 1)
            {
                distanceNoPrefix = ComputeLevenshteinDistance(normalizedRecognized.Substring(1), normalizedItem);
            }


            int distanceNo2Prefix = int.MaxValue;
            if (normalizedRecognized.Length > 2)
            {
                distanceNo2Prefix = ComputeLevenshteinDistance(normalizedRecognized.Substring(2), normalizedItem);
            }

            int minDistance = Math.Min(distanceFull, Math.Min(distanceNoPrefix, distanceNo2Prefix));

            if (minDistance < bestDistance)
            {
                bestDistance = minDistance;
                bestMatch = item;
            }
        }

        if (bestDistance <= MaxErrorTolerance)
        {
            return bestMatch;
        }
        return null;
    }
}