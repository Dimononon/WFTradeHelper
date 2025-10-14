using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WFTrader.Data;

namespace WFTradeHelper.Core;
public class ItemDatabase
{
    public List<JsonItem> AllItems { get; private set; } = new List<JsonItem>();
    private List<string> _dictionary = new List<string>();

    public void LoadItems()
    {
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Configuration.ItemsFileName);
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"ERROR: Item dictionary file '{Configuration.ItemsFileName}' not found.");
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(filePath);
            AllItems = JsonSerializer.Deserialize<List<JsonItem>>(jsonString);

            _dictionary = AllItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item => item.Name.Trim().ToUpper())
                .ToList();

            Console.WriteLine($"Dictionary loaded from file: {_dictionary.Count} items.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading item file: {ex.Message}");
        }
    }

    public JsonItem GetItemByName(string name)
    {
        return AllItems.FirstOrDefault(x => x.Name?.Trim().ToUpper() == name.ToUpper());
    }

    public string FindBestMatch(string recognizedText)
    {
        const int maxErrorTolerance = 4;
        if (string.IsNullOrWhiteSpace(recognizedText)) return null;

        string normalizedRecognized = recognizedText.ToUpper().Replace(" ", "").Replace("\n", "").Replace(":", "").Replace(".", "").Replace(",", "");

        int bestDistance = int.MaxValue;
        string bestMatch = null;

        foreach (var item in _dictionary)
        {
            string normalizedItem = item.ToUpper().Replace(" ", "");
            if (Math.Abs(normalizedRecognized.Length - normalizedItem.Length) > maxErrorTolerance + 5) continue;

            int distance = ComputeLevenshteinDistance(normalizedRecognized, normalizedItem);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestMatch = item;
            }
        }

        return bestDistance <= maxErrorTolerance ? bestMatch : null;
    }

    private int ComputeLevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
    private static void ExportItemsJson()
    {
        const string CONNECTION_STRING = "Host=localhost;Port=5432;Database=wftrader;Username=wfuser;Password=secret";
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(CONNECTION_STRING);

        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Configuration.ItemsFileName);


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

            Console.WriteLine($"Successfully saved {jsonItemsData.Count} items to '{Configuration.ItemsFileName}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving JSON file: {ex.Message}");
        }
    }
}
