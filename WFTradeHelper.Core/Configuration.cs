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

    public static int PricePer15Ducats = 1;
    public static int PricePer25Ducats = 1;
    public static int PricePer45Ducats = 2;
    public static int PricePer65Ducats = 3;
    public static int PricePer100Ducats = 7;
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
    public static List<Rectangle> GetBaseOverlaySlots()
    {
        const int baseWidth = 220;
        const int baseHeight = 230;
        const int cropSideOffset = 5;

        return new List<Rectangle>
    {
        new Rectangle(215 + cropSideOffset, 675 , baseWidth, baseHeight),
        new Rectangle(470 + cropSideOffset, 675, baseWidth, baseHeight),
        new Rectangle(725 + cropSideOffset, 675, baseWidth, baseHeight),
        new Rectangle(980 + cropSideOffset, 675, baseWidth, baseHeight),
        new Rectangle(1235 + cropSideOffset, 675, baseWidth, baseHeight),
        new Rectangle(1490 + cropSideOffset , 675, baseWidth, baseHeight)
    };
    }
}
