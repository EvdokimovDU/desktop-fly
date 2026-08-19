using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace DesktopFly.Rendering;

public class StimCardInfo
{
    public string RegionName { get; set; } = "";
    public int NeuronCount { get; set; }
    public string Description { get; set; } = "";
    public float Timer { get; set; } = 0f;
    public float MaxTimer { get; set; } = 4.0f;
}

public class HudRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _texture;

    private readonly Bitmap _bitmap;
    private readonly Graphics _graphics;
    private readonly int _width;
    private readonly int _height;

    private readonly Font _titleFont;
    private readonly Font _bodyFont;
    private readonly Font _smallFont;
    private readonly Font _boldFont;

    private readonly Brush _textWhite = Brushes.White;
    private readonly Brush _textGold = new SolidBrush(Color.FromArgb(255, 230, 80));
    private readonly Brush _textCyan = new SolidBrush(Color.FromArgb(80, 220, 255));
    private readonly Brush _textGray = new SolidBrush(Color.FromArgb(200, 210, 225));
    private readonly Pen _borderPen = new(Color.FromArgb(60, 80, 120), 1.5f);
    private readonly Pen _accentPen = new(Color.FromArgb(0, 210, 255), 1.5f);

    public HudRenderer(GL gl, int width = 360, int height = 800)
    {
        _gl = gl;
        _width = width;
        _height = height;

        _bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        _graphics = Graphics.FromImage(_bitmap);
        _graphics.SmoothingMode = SmoothingMode.AntiAlias;
        _graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        _titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _boldFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _bodyFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        _smallFont = new Font("Segoe UI", 7.8f, FontStyle.Regular);

        // Simple Quad Shaders
        string vs = @"#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoords;
out vec2 TexCoords;
void main() {
    gl_Position = vec4(aPos, 0.0, 1.0);
    TexCoords = aTexCoords;
}";

        string fs = @"#version 330 core
in vec2 TexCoords;
out vec4 FragColor;
uniform sampler2D uTexture;
void main() {
    FragColor = texture(uTexture, TexCoords);
}";

        _shader = new ShaderProgram(_gl, vs, fs);

        // Screen Quad [-1..1]
        float[] quadVertices = {
            -1.0f,  1.0f,  0.0f, 0.0f,
            -1.0f, -1.0f,  0.0f, 1.0f,
             1.0f, -1.0f,  1.0f, 1.0f,

            -1.0f,  1.0f,  0.0f, 0.0f,
             1.0f, -1.0f,  1.0f, 1.0f,
             1.0f,  1.0f,  1.0f, 0.0f
        };

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            fixed (float* v = quadVertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quadVertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
            }
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        }

        _texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    public void Render(StimCardInfo? stimInfo)
    {
        _graphics.Clear(Color.Transparent);

        // ==========================================
        // 1. TOP SHORTCUTS HUD CARD
        // ==========================================
        var topRect = new Rectangle(10, 10, _width - 20, 115);
        using (var topBrush = new SolidBrush(Color.FromArgb(200, 16, 20, 32)))
        {
            FillRoundedRectangle(_graphics, topBrush, topRect, 8);
        }
        DrawRoundedRectangle(_graphics, _borderPen, topRect, 8);

        _graphics.DrawString("⚡ Управление и шоткаты", _titleFont, _textGold, 18, 16);

        string[] shortcuts = new[]
        {
            "[Space / E] Взлет (Escape)", "[W] Ходьба (DNp09)",
            "[G] Умывание (DNg11)",       "[M] Ходьба назад (MDN)",
            "[P] Пауза / Старт",          "[A / R] ± Муха",
            "[Drag] Вращение 3D",         "[Scroll] Масштаб",
            "[Click] Стимуляция нейронов"
        };

        for (int i = 0; i < shortcuts.Length; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = 20 + col * 165;
            float y = 38 + row * 15;
            _graphics.DrawString(shortcuts[i], _smallFont, _textGray, x, y);
        }

        // ==========================================
        // 2. BOTTOM STIMULATION INFO CARD
        // ==========================================
        if (stimInfo != null && stimInfo.Timer > 0f)
        {
            float alpha = Math.Clamp(stimInfo.Timer / 0.4f, 0f, 1f) * Math.Clamp((stimInfo.MaxTimer - stimInfo.Timer) / 0.4f, 0f, 1f);
            int a = (int)(alpha * 230);
            if (a > 5)
            {
                var cardRect = new Rectangle(10, _height - 150, _width - 20, 140);
                using (var cardBrush = new SolidBrush(Color.FromArgb(a, 12, 18, 30)))
                {
                    FillRoundedRectangle(_graphics, cardBrush, cardRect, 10);
                }
                using (var pen = new Pen(Color.FromArgb(a, 0, 210, 255), 1.5f))
                {
                    DrawRoundedRectangle(_graphics, pen, cardRect, 10);
                }

                using var titleBrush = new SolidBrush(Color.FromArgb(a, 255, 230, 80));
                using var badgeBrush = new SolidBrush(Color.FromArgb(a, 80, 220, 255));
                using var textBrush = new SolidBrush(Color.FromArgb(a, 225, 235, 245));

                _graphics.DrawString(stimInfo.RegionName, _titleFont, titleBrush, 20, _height - 142);
                _graphics.DrawString($"Задействовано: {stimInfo.NeuronCount} нейронов · Импульс: 400 мс", _boldFont, badgeBrush, 20, _height - 120);

                var descRect = new RectangleF(20, _height - 100, _width - 40, 85);
                _graphics.DrawString(stimInfo.Description, _bodyFont, textBrush, descRect);
            }
        }

        // Upload Bitmap to OpenGL Texture
        UploadBitmapToTexture();

        // Render Quad
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);

        _shader.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _shader.SetUniform("uTexture", 0);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    private unsafe void UploadBitmapToTexture()
    {
        var bmpData = _bitmap.LockBits(new Rectangle(0, 0, _width, _height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, (uint)_width, (uint)_height, 0, Silk.NET.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, (void*)bmpData.Scan0);
        _bitmap.UnlockBits(bmpData);
    }

    private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle r, int d)
    {
        using var path = CreateRoundedRectPath(r, d);
        g.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle r, int d)
    {
        using var path = CreateRoundedRectPath(r, d);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle r, int d)
    {
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
        path.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
        path.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        _textGold.Dispose();
        _textCyan.Dispose();
        _textGray.Dispose();
        _borderPen.Dispose();
        _accentPen.Dispose();
        _titleFont.Dispose();
        _boldFont.Dispose();
        _bodyFont.Dispose();
        _smallFont.Dispose();
        _graphics.Dispose();
        _bitmap.Dispose();
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteTexture(_texture);
        _shader.Dispose();
    }
}
