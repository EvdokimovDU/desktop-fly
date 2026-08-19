using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using DesktopFly.Core;
using DesktopFly.Core.Behavior;
using DesktopFly.Core.Data;
using DesktopFly.Core.Model3D;
using DesktopFly.Core.Sim;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace DesktopFly.Rendering;

public static class OffscreenRenderer
{
    public static void RenderSnapshot(string outputPath, int width = 720, int height = 720)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(width, height);
        options.Title = "Offscreen Snapshot";
        options.IsVisible = false;

        using var window = Window.Create(options);
        window.Initialize();

        var gl = window.CreateOpenGL();
        gl.Viewport(0, 0, (uint)width, (uint)height);
        gl.ClearColor(0.94f, 0.94f, 0.94f, 1.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        using var renderer = new SceneRenderer(gl);
        renderer.LightDir = Vector3.Normalize(new Vector3(-0.9f, 0.5f, 0f));
        renderer.LightColor = new Vector3(1.1f, 1.1f, 1.1f);
        renderer.AmbientColor = new Vector3(0.5f, 0.5f, 0.5f);

        var fly = new Fly(Vector2.Zero)
        {
            Heading = MathF.PI / 2f
        };
        var legAngles = new[] { 0.25f, -0.2f, -0.22f, 0.28f, 0.2f, -0.25f };
        var legLifts = new[] { 0.35f, 0f, 0f, 0.3f, 0f, 0.35f };
        for (int i = 0; i < fly.Model.Legs.Length; i++)
        {
            fly.Model.Legs[i].Angle = legAngles[i];
            fly.Model.Legs[i].Lift = legLifts[i];
            fly.Model.Legs[i].Apply();
        }
        fly.SyncNode();

        var cameraPos = new Vector3(30f, -58f, 42f);
        var viewMatrix = Matrix4x4.CreateLookAt(cameraPos, fly.Node.Position, Vector3.UnitZ);
        var projMatrix = Matrix4x4.CreatePerspectiveFieldOfView(42f * MathF.PI / 180f, (float)width / height, 1f, 600f);

        renderer.Render(fly.Node, viewMatrix, projMatrix, cameraPos);

        SaveGlFramebufferToPng(gl, width, height, outputPath);
        Console.WriteLine($"snapshot written to {outputPath}");
    }

    public static void RenderBrainshot(string outputPath, int width = 720, int height = 560)
    {
        var data = DataLoader.LoadBrainData();
        if (data == null)
        {
            Console.Error.WriteLine("no data/ — run etl.py first");
            Environment.Exit(1);
        }

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(width, height);
        options.Title = "Offscreen Brainshot";
        options.IsVisible = false;

        using var window = Window.Create(options);
        window.Initialize();

        var gl = window.CreateOpenGL();
        gl.Viewport(0, 0, (uint)width, (uint)height);
        gl.ClearColor(0.03f, 0.035f, 0.06f, 1.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        var sim = new LIFSim(data.Value.Circuit, null);
        using var brainRenderer = new BrainSceneRenderer(gl, data.Value.Points, sim);
        brainRenderer.RotationY = 0.5f;

        var rand = new Random();
        for (int i = 0; i < 40; i++)
        {
            brainRenderer.Flash(rand.Next(sim.N), false);
        }
        if (sim.GF.Count > 0)
        {
            brainRenderer.Flash(sim.GF[0], true);
        }

        brainRenderer.Render(width, height);

        SaveGlFramebufferToPng(gl, width, height, outputPath);
        Console.WriteLine($"brainshot written to {outputPath}");
    }

    private static unsafe void SaveGlFramebufferToPng(GL gl, int width, int height, string path)
    {
        var pixels = new byte[width * height * 4];

        fixed (byte* p = pixels)
        {
            gl.ReadPixels(0, 0, (uint)width, (uint)height, Silk.NET.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, p);
        }

        using var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        int stride = bmpData.Stride;
        byte* scan0 = (byte*)bmpData.Scan0;

        for (int y = 0; y < height; y++)
        {
            int glY = height - 1 - y; // OpenGL Y inversion
            int srcOffset = glY * width * 4;
            int dstOffset = y * stride;

            Marshal.Copy(pixels, srcOffset, (IntPtr)(scan0 + dstOffset), width * 4);
        }

        bmp.UnlockBits(bmpData);
        bmp.Save(path, ImageFormat.Png);
    }
}
