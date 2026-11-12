using Microsoft.Xna.Framework;

namespace SuperSqij;

public static class Palette
{
    public static readonly Color[] DoorColors =
    {
        new(255, 92, 92),   // ember
        new(255, 200, 87),  // amber
        new(87, 217, 255),  // azure
        new(176, 124, 255), // violet
        new(155, 255, 87),  // lime
        new(255, 155, 87),  // copper
    };

    public static readonly string[] RuneNames = { "EMBER", "AMBER", "AZURE", "VIOLET", "LIME", "COPPER" };

    public static readonly Color Ink = new(10, 14, 24);
    public static readonly Color RockDark = new(30, 36, 56);
    public static readonly Color Green = new(143, 227, 106);
    public static readonly Color GreenLight = new(206, 255, 170);
    public static readonly Color Teal = new(126, 232, 202);
    public static readonly Color Gold = new(255, 209, 102);
    public static readonly Color Pink = new(255, 111, 174);
    public static readonly Color Purple = new(176, 111, 227);
    public static readonly Color PurpleLight = new(224, 186, 255);
    public static readonly Color Danger = new(255, 96, 96);
    public static readonly Color Paper = new(238, 246, 255);
}
