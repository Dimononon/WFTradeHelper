using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFTradeHelper.Core.Models;
public class ScanResult
{
    public int SlotNumber { get; set; }
    public string ItemName { get; set; }
    public int Ducats { get; set; }
    public int PlatinumValue { get; set; }
    public string Status { get; set; }
}