using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WFTradeHelper.Core;

namespace WFTradeHelper.Gui;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly TradeEvaluator _tradeEvaluator;

    private readonly DispatcherTimer _hotkeyTimer;
    private bool _isF8Pressed = false;

    private readonly DispatcherTimer _autoScanTimer;
    private bool _isAutoScanActive = false;

    private bool _isScanning = false;

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    private OverlayWindow _overlayWindow;
    private readonly DispatcherTimer _overlayCloseTimer;
    public MainWindow()
    {
        InitializeComponent();

        var itemDatabase = new ItemDatabase();
        var screenService = new ScreenService();
        var ocrService = new OcrService();
        _tradeEvaluator = new TradeEvaluator(screenService, ocrService, itemDatabase);

        int loadedItemsCount = itemDatabase.LoadItems();
        StatusTextBlock.Text = $"{loadedItemsCount} items loaded.";

        _hotkeyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _hotkeyTimer.Tick += HotkeyTimer_Tick;
        _hotkeyTimer.Start();

        _autoScanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _autoScanTimer.Tick += (s,e)=>PerformScan();

        _overlayCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5000) };
        _overlayCloseTimer.Tick += (s, e) => CloseOverlay();
    }
    private void AutoScanButton_Click(object sender, RoutedEventArgs e)
    {
        _isAutoScanActive = !_isAutoScanActive;

        if (_isAutoScanActive)
        {
            _autoScanTimer.Start();
            AutoScanButton.Content = "Auto Scan: ON";
            ScanButton.IsEnabled = false;
        }
        else
        {
            _autoScanTimer.Stop();
            AutoScanButton.Content = "Auto Scan: OFF";
            ScanButton.IsEnabled = true;
        }
    }
    private void HotkeyTimer_Tick(object sender, EventArgs e)
    {
        const int VK_F8 = 0x77;
        short f8State = GetAsyncKeyState(VK_F8);

        if ((f8State & 0x8000) != 0 && !_isF8Pressed)
        {
            _isF8Pressed = true;
            PerformScan(isManual: true);
        }
        else if ((f8State & 0x8000) == 0)
        {
            _isF8Pressed = false;
        }
    }
    
    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        PerformScan(isManual: true);
    }
    private void CloseOverlay()
    {
        _overlayCloseTimer.Stop();
        _overlayWindow?.Close();
        _overlayWindow = null;
    }
    private void UpdatePricesFromUI()
    {
        if (int.TryParse(Price15.Text, out int price15)) Configuration.PricePer15Ducats = price15;
        if (int.TryParse(Price25.Text, out int price25)) Configuration.PricePer25Ducats = price25;
        if (int.TryParse(Price45.Text, out int price45)) Configuration.PricePer45Ducats = price45;
        if (int.TryParse(Price65.Text, out int price65)) Configuration.PricePer65Ducats = price65;
        if (int.TryParse(Price100.Text, out int price100)) Configuration.PricePer100Ducats = price100;
    }

    private async void PerformScan(bool isManual = false)
    {
        if (_isScanning) return;
        _isScanning = true;

        try
        {
            UpdatePricesFromUI();
            var tradeResult = await Task.Run(() => _tradeEvaluator.EvaluateTrade());

            ResultsListView.ItemsSource = tradeResult.ScanResults;
            TotalPlatTextBlock.Text = $"Total Platinum: {tradeResult.TotalPlatinum}";
            bool allItemsOk = tradeResult.ScanResults.All(r => r.Status == "OK");
            StatusIndicator.Fill = allItemsOk ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red;

            if (isManual)
            {
                CloseOverlay();
                var source = PresentationSource.FromVisual(this);
                double dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                double dpiScaleY = source.CompositionTarget.TransformToDevice.M22;

                _overlayWindow = new OverlayWindow();
                _overlayWindow.UpdateOverlay(tradeResult, dpiScaleX, dpiScaleY);
                _overlayWindow.Show();

                _overlayCloseTimer.Start();
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error: {ex.Message}";
            StatusIndicator.Fill = System.Windows.Media.Brushes.Red;
        }
        finally
        {
            _isScanning = false;
        }
    }
}