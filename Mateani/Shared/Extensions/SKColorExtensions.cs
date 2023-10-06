using SkiaSharp;

namespace Mateani.Shared.Extensions;

public static class SKColorExtensions
{
    public static uint ToUint(this SKColor color)
    {
        return (uint)((color.Alpha << 24) | (color.Red << 16) | (color.Green << 8) | (color.Blue));
    }
}
