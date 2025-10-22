using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFTradeHelper.Core.Models;
public class TradeResult
{
    public List<ScanResult> ScanResults { get; set; } = new List<ScanResult>();
    public List<OverlayInfo> OverlayElements { get; set; } = new List<OverlayInfo>();
    public int TotalPlatinum { get; set; }
    public bool IsVerticalOffset { get; set; } = false;
}