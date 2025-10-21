using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WFTradeHelper.Core.Models;

namespace WFTradeHelper.Gui
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        const int BOX_LENGHT = 220;
        public OverlayWindow()
        {
            InitializeComponent();
        }
        public void UpdateOverlay(TradeResult result, double dpiScaleX, double dpiScaleY)
        {
            OverlayCanvas.Children.Clear();

            foreach (var element in result.OverlayElements)
            {
                double left = element.Bounds.Left / dpiScaleX;
                double top = element.Bounds.Top / dpiScaleY;
                double width = element.Bounds.Width / dpiScaleX;
                double height = element.Bounds.Height / dpiScaleY;

                var rect = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Stroke = element.IsSuccess ? Brushes.LimeGreen : Brushes.Red,
                    StrokeThickness = 3
                };
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                OverlayCanvas.Children.Add(rect);

                var textBlock = new TextBlock
                {
                    Text = element.Text,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                    Width = BOX_LENGHT / dpiScaleX,
                    TextWrapping = TextWrapping.WrapWithOverflow,
                    TextAlignment = TextAlignment.Center,

                };
                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, top + height);
                OverlayCanvas.Children.Add(textBlock);
            }

            if (result.OverlayElements.Count > 0)
            {
                var firstElement = result.OverlayElements[0];
                double totalPlatLeft = firstElement.Bounds.Left / dpiScaleX;
                double totalPlatTop = (firstElement.Bounds.Bottom / dpiScaleY) + 40;

                var totalPlatBlock = new TextBlock
                {
                    Text = $"Total Platinum: {result.TotalPlatinum}",
                    Foreground = Brushes.Gold,
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0))
                };
                Canvas.SetLeft(totalPlatBlock, totalPlatLeft);
                Canvas.SetTop(totalPlatBlock, totalPlatTop);
                OverlayCanvas.Children.Add(totalPlatBlock);
            }
        }

        public void ShowTemporary(int milliseconds)
        {
            this.Show();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                this.Close();
            };
            timer.Start();
        }
    }
}
