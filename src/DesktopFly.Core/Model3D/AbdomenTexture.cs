namespace DesktopFly.Core.Model3D;

public static class AbdomenTexture
{
    public static (byte[] Rgba, int Width, int Height) Generate()
    {
        const int width = 64;
        const int height = 128;

        byte baseR = (byte)(0.72f * 255);
        byte baseG = (byte)(0.55f * 255);
        byte baseB = (byte)(0.32f * 255);

        byte darkR = (byte)(0.22f * 255);
        byte darkG = (byte)(0.15f * 255);
        byte darkB = (byte)(0.09f * 255);

        var bytes = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            bool isDark = (y >= 0 && y < 26) ||
                          (y >= 38 && y < 48) ||
                          (y >= 60 && y < 70) ||
                          (y >= 82 && y < 91);

            byte r = isDark ? darkR : baseR;
            byte g = isDark ? darkG : baseG;
            byte b = isDark ? darkB : baseB;

            for (int x = 0; x < width; x++)
            {
                int idx = (y * width + x) * 4;
                bytes[idx + 0] = r;
                bytes[idx + 1] = g;
                bytes[idx + 2] = b;
                bytes[idx + 3] = 255;
            }
        }

        return (bytes, width, height);
    }
}
