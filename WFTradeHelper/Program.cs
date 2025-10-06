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
    private static List<string> LoadItemDictionary(TextWriter originalOut)
    {
        var dictionary = new List<string>();
        const string CONNECTION_STRING = "Host=localhost;Port=5432;Database=wftrader;Username=wfuser;Password=secret";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(CONNECTION_STRING);

        try
        {
            using (var context = new AppDbContext(optionsBuilder.Options))
            {
                dictionary.Add("PLATINUM");
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
                    if (item.I18N.First().Name != null)
                    {
                        dictionary.Add(item.I18N.First().Name.Trim().ToUpper());
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
            Console.WriteLine(ex.Message);
            Console.SetOut(new StreamWriter(Stream.Null));
        }
        return dictionary;
    }
    public static void RecognizeItemsFromTrade(List<string> validItemDictionary, TextWriter originalOut)
    {
        Console.SetOut(new StreamWriter(Stream.Null));

        try
        {
            var primaryScreen = Screen.PrimaryScreen;

            var itemSlots = new List<Rectangle>
            {
                new Rectangle(215, 846, 220, 55),
                new Rectangle(470, 846, 220, 55),
                new Rectangle(725, 846, 220, 55),
                new Rectangle(980, 846, 220, 55),
                new Rectangle(1235, 846, 220, 55),
                new Rectangle(1490, 846, 220, 55)
            };

            using (var screenBitmap = new Bitmap(CAPTURE_WIDTH, CAPTURE_HEIGHT))
            {
                using (var g = Graphics.FromImage(screenBitmap))
                {
                    g.CopyFromScreen(0, 0, 0, 0, screenBitmap.Size);
                }

                Console.SetOut(originalOut);

                // --- ЗБЕРЕЖЕННЯ ПОВНОГО СКРІНШОТА ---
                string fullScreenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "full_screen_capture.png");
                screenBitmap.Save(fullScreenPath, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine($"Full screenshot saved: {Path.GetFileName(fullScreenPath)}");

                Console.WriteLine("\n--- Start scan (F8) ---");
                Console.SetOut(new StreamWriter(Stream.Null)); // Знову заглушуємо Tesseract

                using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default,
                    new string[] { },
                    new Dictionary<string, object>
                    {
                        {"tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz " }
                    }, true))
                {
                    Console.SetOut(originalOut);
                    int platCost = 0;
                    int slotIndex = 1;
                    foreach (var slot in itemSlots) // Використовуємо фіксовані itemSlots
                    {
                        // Тут ми повинні ігнорувати перевірку на вихід за межі, оскільки ми примусово захопили 1920x1080
                        // Але обов'язково перевіряємо, чи слот в межах нашого Bitmap
                        if (slot.Right > CAPTURE_WIDTH || slot.Bottom > CAPTURE_HEIGHT)
                        {
                            Console.WriteLine($"Error: Slot {slotIndex} not in screen range");
                            slotIndex++;
                            continue;
                        }

                        using (var itemBitmap = screenBitmap.Clone(slot, screenBitmap.PixelFormat))
                        using (var preprocessedBitmap = PreprocessImage(itemBitmap))
                        using (var ms = new MemoryStream())
                        {
                            // Код для збереження обробленого зображення
                            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"slot_{slotIndex}.png");
                            preprocessedBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                            preprocessedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            var imgBytes = ms.ToArray();

                            using (var pix = Pix.LoadFromMemory(imgBytes))
                            using (var page = engine.Process(pix, PageSegMode.SparseText))
                            {
                                string recognizedText = page.GetText().Trim();

                                // 3. ПОСТ-ОБРОБКА ТЕКСТУ ЗІ СЛОВНИКОМ
                                if (validItemDictionary.Count > 0)
                                {
                                    string correctedName = FindBestMatch(recognizedText, validItemDictionary);
                                    if ((correctedName != null) && (correctedName != "PLATINUM"))
                                    {
                                        int ducatValue = (int)AllItems.Find(x => x.I18N.First().Name.ToString().Trim().ToUpper() == correctedName).Ducats;
                                        if (ducatValue == 15) platCost += PRICEPER15;
                                        else if (ducatValue == 25) platCost += PRICEPER25;
                                        else if (ducatValue == 45) platCost += PRICEPER45;
                                        else if (ducatValue == 65) platCost += PRICEPER65;
                                        else if (ducatValue == 100) platCost += PRICEPER100;
                                        Console.WriteLine($"Slot {slotIndex}: {correctedName,-50} (From DB) {ducatValue, -3} ducats");
                                    }
                                    else if(correctedName == "PLATINUM")
                                    {
                                        Console.WriteLine($"Slot {slotIndex}: {correctedName,-50}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Slot {slotIndex}: {recognizedText.Replace("\n", " ").Replace("|", "I"),-50} (Not DB)");
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(recognizedText))
                                {
                                    Console.WriteLine($"Слот {slotIndex}: {recognizedText.Replace("\n", " ").Replace("|", "I"),-50}");
                                }
                                else
                                {
                                    Console.WriteLine($"Слот {slotIndex}: Text not recognized {Path.GetFileName(filePath)}");
                                }
                            }
                        }
                        slotIndex++;
                    }
                    Console.WriteLine($"--- Stop scan ---\n Platinum: {platCost}");
                }
            }
        }
        catch (Exception e)
        {
            Console.SetOut(originalOut);
            Console.WriteLine($"Критична помилка: {e.Message}");
            Console.WriteLine($"Перевірте, чи є папка 'tessdata' з 'eng.traineddata' поруч з .exe");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    /// Кольоровий фільтр для ЖОВТО-ЗОЛОТОГО тексту на темному фоні.
    /// </summary>
    public static Bitmap PreprocessImage(Bitmap original)
    {
        Bitmap processed = new Bitmap(original.Width, original.Height);
        const int threshold = 130; // Знижено поріг для кращого захоплення світлого тексту

        for (int i = 0; i < original.Width; i++)
        {
            for (int j = 0; j < original.Height; j++)
            {
                Color c = original.GetPixel(i, j);
                int gray = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);

                if (gray > threshold)
                {
                    processed.SetPixel(i, j, Color.Black); // Текст (світле) стає ЧОРНИМ
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
    private static string FindBestMatch(string recognizedText, List<string> dictionary)
    {
        // Дозволяємо максимум 3 помилки (вставки, видалення, заміни)
        const int MaxErrorTolerance = 4;

        if (string.IsNullOrWhiteSpace(recognizedText)) return null;

        // Нормалізація
        string normalizedRecognized = recognizedText.ToUpper().Replace(" ", "").Replace("\n", "");

        int bestDistance = int.MaxValue;
        string bestMatch = null;

        foreach (var item in dictionary)
        {
            string normalizedItem = item.ToUpper().Replace(" ", "").Replace("\n", "");

            // Оптимізація: пропускаємо, якщо різниця в довжині занадто велика.
            if (Math.Abs(normalizedRecognized.Length - normalizedItem.Length) > MaxErrorTolerance + 2) continue;


            // ----------------------------------------------------
            // --- ОБЧИСЛЕННЯ КІЛЬКОХ ВАРІАНТІВ ВІДСТАНІ ЛЕВЕНШТЕЙНА ---
            // ----------------------------------------------------

            // 1. Повна відповідність
            int distanceFull = ComputeLevenshteinDistance(normalizedRecognized, normalizedItem);

            // 2. Відповідність з ігноруванням 1-го символу (для компенсації шуму на початку)
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

            // Знаходимо мінімальну відстань з усіх варіантів
            int minDistance = Math.Min(distanceFull, Math.Min(distanceNoPrefix, distanceNo2Prefix));


            // ----------------------------------------------------

            if (minDistance < bestDistance)
            {
                bestDistance = minDistance;
                bestMatch = item;
            }
        }

        // Повертаємо знайдену відповідність, лише якщо кількість помилок не перевищує ліміт.
        // Якщо Akarius і Sikarus мають однакову низьку відстань,
        // повернеться той, який трапився першим у словнику (це краще, ніж нічого).
        if (bestDistance <= MaxErrorTolerance)
        {
            return bestMatch;
        }

        return null;
    }
    //private static string FindBestMatch(string recognizedText, List<string> dictionary)
    //{
    //    const int MaxErrorTolerance = 4;

    //    if (string.IsNullOrWhiteSpace(recognizedText)) return null;

    //    // Нормалізація: робимо текст великими літерами та видаляємо пробіли/переноси рядків
    //    string normalizedRecognized = recognizedText.ToUpper().Replace(" ", "").Replace("\n", "");

    //    int bestDistance = int.MaxValue;
    //    string bestMatch = null;

    //    foreach (var item in dictionary)
    //    {
    //        // Нормалізація елемента словника
    //        string normalizedItem = item.ToUpper().Replace(" ", "").Replace("\n", "");

    //        // Оптимізація: якщо різниця в довжині більша за допустиму помилку, пропускаємо
    //        if (Math.Abs(normalizedRecognized.Length - normalizedItem.Length) > MaxErrorTolerance) continue;

    //        // Обчислюємо відстань Левенштейна
    //        int distance = ComputeLevenshteinDistance(normalizedRecognized, normalizedItem);

    //        if (distance < bestDistance)
    //        {
    //            bestDistance = distance;
    //            bestMatch = item;
    //        }
    //    }

    //    // Повертаємо знайдену відповідність, лише якщо кількість помилок не перевищує ліміт
    //    if (bestDistance <= MaxErrorTolerance)
    //    {
    //        return bestMatch;
    //    }

    //    return null;
    //}
}
