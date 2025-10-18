using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFTradeHelper.Core.Models;
public class OverlayInfo
{
    public Rectangle Bounds { get; set; }
    public string Text { get; set; }
    public bool IsSuccess { get; set; }
}