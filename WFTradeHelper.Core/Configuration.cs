using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFTradeHelper.Core;
public static class Configuration
{
    public const string ItemsFileName = "items.json";

    public const double BaseScreenWidth = 1920.0;
    public const double BaseScreenHeight = 1080.0;

    public const int OcrCropTopPixels = 21;
    public const int UiVerticalOffset = 59;

    public const int PricePer15Ducats = 1;
    public const int PricePer25Ducats = 1;
    public const int PricePer45Ducats = 2;
    public const int PricePer65Ducats = 3;
    public const int PricePer100Ducats = 7;
    public static Rectangle GetBaseTaxArea()
    {
        return new Rectangle(x: 1532, y: 164, width: 15, height: 19);
    }
    public static List<Rectangle> GetBaseItemSlots()
    {
        const int baseWidth = 210;
        const int baseHeight = 46;
        const int cropSideOffset = 5;

        return new List<Rectangle>
            {
                new Rectangle(215 + cropSideOffset, 847, baseWidth, baseHeight),
                new Rectangle(470 + cropSideOffset, 847, baseWidth, baseHeight),
                new Rectangle(725 + cropSideOffset, 847, baseWidth, baseHeight),
                new Rectangle(980 + cropSideOffset, 847, baseWidth, baseHeight),
                new Rectangle(1235 + cropSideOffset, 847, baseWidth, baseHeight),
                new Rectangle(1490 + cropSideOffset, 847, baseWidth, baseHeight)
            };
    }
}
