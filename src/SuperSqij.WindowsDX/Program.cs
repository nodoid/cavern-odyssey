using System;
using SuperSqij;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var game = new SuperSqijGame();
        game.Run();
    }
}
