// Program.cs
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WFTradeHelper.Core;

namespace WFTradeHelper
{
    class Program
    {
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        private static bool isF8Pressed = false;

        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Console.OutputEncoding = Encoding.UTF8;

            var itemDatabase = new ItemDatabase();
            itemDatabase.LoadItems();

            var screenService = new ScreenService();
            using (var ocrService = new OcrService())
            {
                var tradeEvaluator = new TradeEvaluator(screenService, ocrService, itemDatabase);

                Console.WriteLine("Screen capture is adapted for your current resolution.");
                Console.WriteLine("Press F8 to scan items manually.");
                Console.WriteLine("Press F7 to toggle automatic scan mode.");

                bool autoMode = false;
                bool tradeHasItems = false;

                while (true)
                {
                    if (GetAsyncKeyState((int)Keys.F8) != 0 && !isF8Pressed)
                    {
                        isF8Pressed = true;
                        tradeEvaluator.EvaluateTrade();
                    }
                    else if (GetAsyncKeyState((int)Keys.F8) == 0)
                    {
                        isF8Pressed = false;
                    }

                    if (GetAsyncKeyState((int)Keys.F7) != 0)
                    {
                        autoMode = !autoMode;
                        Console.WriteLine($"Automatic scan mode: {(autoMode ? "ON" : "OFF")}");
                        Thread.Sleep(500);
                    }

                    if (autoMode)
                    {
                        bool currentState = tradeEvaluator.HasTradeStateChanged();
                        if (currentState && !tradeHasItems)
                        {
                            Console.WriteLine("Change detected! Performing full scan...");
                            tradeEvaluator.EvaluateTrade();
                        }
                        tradeHasItems = currentState;
                    }

                    Thread.Sleep(250);
                }
            }
        }
    }
}