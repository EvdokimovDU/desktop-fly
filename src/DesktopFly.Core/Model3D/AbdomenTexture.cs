using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DesktopFly.Core.Model3D;

public static class AbdomenTexture
{
    public static (byte[] Rgba, int Width, int Height) Generate()
    {
        const int width = 64;
        const int height = 128;

        var baseColor = new Rgba32((byte)(0.72f * 255), (byte)(0.55f * 255), (byte)(0.32f * 255), 255);
        var darkColor = new Rgba32((byte)(0.22f * 255), (byte)(0.15f * 255), (byte)(0.09f * 255), 255);

        using var image = new Image<Rgba32>(width, height);
        for (int y = 0; y < height; y++)
        {
            bool isDark = (y >= 0 && y < 26) ||
                          (y >= 38 && y < 48) ||
                          (y >= 60 && y < 70) ||
                          (y >= 82 && y < 91);
            var c = isDark ? darkColor : baseColor;
            for (int x = 0; x < width; x++)
            {
                image[x, y] = c;
            }
        }

        var bytes = new byte[width * height * 4];
        image.CopyPixelDataTo(bytes);
        return (bytes, width, height);
    }
}
