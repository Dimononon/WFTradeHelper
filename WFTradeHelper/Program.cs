using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Tesseract;
using System.Linq; // Додано для LINQ
using WFTrader.Data;
using WFTrader.Data.Models;

class Program
{
    // Імпорт API залишається тільки для читання клавіш
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    private static bool isF8Pressed = false;

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

    // ----------------------------------------------------------------------------------------------------------------------------------
    // --- ЗМІНИ ТУТ: LoadItemDictionary ---
    // ----------------------------------------------------------------------------------------------------------------------------------

    private static List<string> LoadItemDictionary(TextWriter originalOut)
    {
        var dictionary = new List<string>();
        // Приклад: оновіть рядок підключення на свій
        const string CONNECTION_STRING = "Host=localhost;Port=5432;Database=wftrader;Username=wfuser;Password=secret";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(CONNECTION_STRING);

        try
        {
            using (var context = new AppDbContext(optionsBuilder.Options))
            {
                dictionary.Add("PLATINUM");
                // Використовуємо .AsEnumerable() перед First() для коректної роботи LINQ to Objects
                var items = context.Items
                    .Include(i => i.I18N)
                    .Include(i => i.ItemTags)
                        .ThenInclude(it => it.Tag)
                    .Where(item =>
                        item.ItemTags.Any(itemTag =>
                            itemTag.Tag.Name == "prime"
                        )
                    )
                    .ToList();

                foreach (var item in items)
                {
                    // Перевірка на null та наявність I18N
                    var i18nName = item.I18N.FirstOrDefault()?.Name;
                    if (!string.IsNullOrWhiteSpace(i18nName))
                    {
                        dictionary.Add(i18nName.Trim().ToUpper());
                        AllItems.Add(item);
                    }
                }
            }

            Console.SetOut(originalOut);
            Console.WriteLine($"Dictionary loaded: {dictionary.Count} items.");
            Console.SetOut(new StreamWriter(Stream.Null));
        }
        catch (Exception ex)
        {
            Console.SetOut(originalOut);
            Console.WriteLine($"Error loading dictionary: {ex.Message}");
            Console.SetOut(new StreamWriter(Stream.Null));
        }
        return dictionary;
    }

    // ----------------------------------------------------------------------------------------------------------------------------------
    // --- ЗМІНИ ТУТ: RecognizeItemsFromTrade ---
    // ----------------------------------------------------------------------------------------------------------------------------------

    public static void RecognizeItemsFromTrade(List<string> validItemDictionary, TextWriter originalOut)
    {
        Console.SetOut(new StreamWriter(Stream.Null));

        try
        {
            // --- 1. ОНОВЛЕНІ КООРДИНАТИ З ОБРІЗАННЯМ БОКОВИХ КРАЇВ (5px) ---
            const int BASE_WIDTH = 220;
            const int BASE_HEIGHT = 55;
            const int NEW_WIDTH = BASE_WIDTH - 10; // 210
            const int CROP_SIDE_OFFSET = 5; // 5px зліва + 5px справа

            var itemSlots = new List<Rectangle>
            {
                new Rectangle(215 + CROP_SIDE_OFFSET, 846, NEW_WIDTH, BASE_HEIGHT), // Slot 1
                new Rectangle(470 + CROP_SIDE_OFFSET, 846, NEW_WIDTH, BASE_HEIGHT), // Slot 2
                new Rectangle(725 + CROP_SIDE_OFFSET, 846, NEW_WIDTH, BASE_HEIGHT), // Slot 3
                new Rectangle(980 + CROP_SIDE_OFFSET, 846, NEW_WIDTH, BASE_HEIGHT), // Slot 4
                new Rectangle(1235 + CROP_SIDE_OFFSET, 846, NEW_WIDTH, BASE_HEIGHT), // Slot 5
                new Rectangle(1490 + CROP_SIDE_OFFSET, 846, NEW_WIDTH, BASE_HEIGHT)  // Slot 6
            };

            using (var screenBitmap = new Bitmap(CAPTURE_WIDTH, CAPTURE_HEIGHT))
            {
                using (var g = Graphics.FromImage(screenBitmap))
                {
                    g.CopyFromScreen(0, 0, 0, 0, screenBitmap.Size);
                }

                Console.SetOut(originalOut);

                // Збереження повного скріншота для налагодження
                string fullScreenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "full_screen_capture.png");
                screenBitmap.Save(fullScreenPath, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine($"Full screenshot saved: {Path.GetFileName(fullScreenPath)}");

                Console.WriteLine("\n--- Start scan (F8) ---");
                Console.SetOut(new StreamWriter(Stream.Null)); // Знову заглушуємо Tesseract

                // --- ОНОВЛЕНА КОНФІГУРАЦІЯ TESSERACT З ПОВНИМ WHITELIST ---
                using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default,
                    new string[] { }, // Порожній масив конфігураційних файлів
                    new Dictionary<string, object>
                    {
                        // Додано цифри, апострофи та дефіси для більшої надійності
                        {"tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'- " },
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
                            // Збереження обробленого зображення для налагодження
                            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"slot_{slotIndex}.png");
                            preprocessedBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                            // --- ДВОПРОХІДНА ЛОГІКА OCR ---
                            string recognizedText = "";
                            string finalName = null;
                            const int CROP_TOP_PIXELS = 15; // Пікселі для обрізання верхнього шуму

                            // 1. ПЕРШИЙ ПРОХІД: Повний блок (для дворядкових назв)
                            using (var pix = Pix.LoadFromMemory(GetPngBytes(preprocessedBitmap)))
                            using (var page = engine.Process(pix, PageSegMode.SparseText))
                            {
                                recognizedText = page.GetText().Trim();
                                finalName = FindBestMatch(recognizedText, validItemDictionary);
                            }

                            // 2. ДРУГИЙ ПРОХІД: Обрізаний блок (для однорядкових з шумом)
                            // Якщо не знайшли з першого разу АБО розпізнаний текст містить багато шуму, але ми хочемо перевірити обрізаний варіант
                            if (finalName == null && preprocessedBitmap.Height > CROP_TOP_PIXELS)
                            {
                                using (var croppedBitmap = CropTop(preprocessedBitmap, CROP_TOP_PIXELS))
                                using (var pixCropped = Pix.LoadFromMemory(GetPngBytes(croppedBitmap)))
                                // Використовуємо SingleLine, оскільки ми вже відкинули верхню частину
                                using (var pageCropped = engine.Process(pixCropped, PageSegMode.SingleLine))
                                {
                                    string croppedText = pageCropped.GetText().Trim();
                                    string croppedName = FindBestMatch(croppedText, validItemDictionary);

                                    if (croppedName != null)
                                    {
                                        // Використовуємо кращий (обрізаний) результат
                                        recognizedText = croppedText;
                                        finalName = croppedName;
                                    }
                                }
                            }

                            // 3. ФІНАЛЬНА ОБРОБКА
                            if (finalName != null)
                            {
                                if (finalName != "PLATINUM")
                                {
                                    // Обробка AllItems.Find може бути небезпечною, краще використовувати LINQ:
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
                                // Виведення нерозпізнаного тексту для налагодження
                                Console.WriteLine($"Slot {slotIndex}: {recognizedText.Replace("\n", " ").Replace("|", "I"),-50} (Not Found)");
                            }
                        }
                        slotIndex++;
                    }
                    Console.WriteLine($"--- Stop scan ---\n Total Platinum: {platCost}");
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

    // ----------------------------------------------------------------------------------------------------------------------------------
    // --- ДОПОМІЖНІ ФУНКЦІЇ ---
    // ----------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Кольоровий фільтр (бінаризація) для світлого тексту на темному фоні.
    /// </summary>
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
                    processed.SetPixel(i, j, Color.Black); // Текст (світле) стає ЧОРНИМ
                }
                else
                {
                    processed.SetPixel(i, j, Color.White); // Фон (темне) стає БІЛИМ
                }
            }
        }
        return processed;
    }

    /// <summary>
    /// Обрізає верхню частину бітмапу, щоб видалити верхній шум інтерфейсу.
    /// </summary>
    private static Bitmap CropTop(Bitmap original, int cropHeight)
    {
        // Обрізаємо верхні 'cropHeight' пікселів
        Rectangle cropArea = new Rectangle(0, cropHeight, original.Width, original.Height - cropHeight);

        if (cropArea.Height <= 0 || cropArea.Width <= 0)
            return original;

        return original.Clone(cropArea, original.PixelFormat);
    }

    /// <summary>
    /// Перетворює Bitmap на масив байтів PNG для Tesseract.
    /// </summary>
    private static byte[] GetPngBytes(Bitmap bmp)
    {
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
    }


    /// <summary>
    /// Обчислює відстань Левенштейна між двома рядками.
    /// </summary>
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

        // Ініціалізація
        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }
        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        // Обчислення
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

    /// <summary>
    /// Знаходить найкращу відповідність, використовуючи мінімальну відстань Левенштейна та компенсацію префікса.
    /// </summary>
    private static string FindBestMatch(string recognizedText, List<string> dictionary)
    {
        const int MaxErrorTolerance = 4; // Допуск помилок
        if (string.IsNullOrWhiteSpace(recognizedText)) return null;

        string normalizedRecognized = recognizedText.ToUpper().Replace(" ", "").Replace("\n", "").Replace(":", "").Replace(".", "").Replace(",", "");

        int bestDistance = int.MaxValue;
        string bestMatch = null;

        foreach (var item in dictionary)
        {
            string normalizedItem = item.ToUpper().Replace(" ", "").Replace("\n", "").Replace(":", "").Replace(".", "").Replace(",", "");

            if (Math.Abs(normalizedRecognized.Length - normalizedItem.Length) > MaxErrorTolerance + 5) continue;

            // --- ОБЧИСЛЕННЯ КІЛЬКОХ ВАРІАНТІВ ВІДСТАНІ ---

            // 1. Повна відповідність
            int distanceFull = ComputeLevenshteinDistance(normalizedRecognized, normalizedItem);

            // 2. Відповідність з ігноруванням 1-го символу (компенсація шуму/помилки на початку)
            int distanceNoPrefix = int.MaxValue;
            if (normalizedRecognized.Length > 1)
            {
                distanceNoPrefix = ComputeLevenshteinDistance(normalizedRecognized.Substring(1), normalizedItem);
            }

            // 3. Відповідність з ігноруванням 2-х символів
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