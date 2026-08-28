// app.ico 생성 도구 - 네온 스피커 아이콘 (16~256px 멀티사이즈)
// 빌드 시 한 번만 실행: csc MakeIcon.cs && MakeIcon.exe
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

class MakeIcon
{
    // 강조색 하나만 사용 (앱 UI와 동일)
    static readonly Color Cyan = Color.FromArgb(82, 139, 235);
    static readonly Color Magenta = Color.FromArgb(82, 139, 235);

    static GraphicsPath Rounded(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, rad, rad, 180, 90);
        p.AddArc(r.Right - rad, r.Y, rad, rad, 270, 90);
        p.AddArc(r.Right - rad, r.Bottom - rad, rad, rad, 0, 90);
        p.AddArc(r.X, r.Bottom - rad, rad, rad, 90, 90);
        p.CloseFigure();
        return p;
    }

    static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float s = size / 256f;
            g.ScaleTransform(s, s);
            // 작은 크기에서는 선을 굵게 보정해야 흐려지지 않음
            float k = size <= 16 ? 2.4f : (size <= 32 ? 1.7f : (size <= 48 ? 1.3f : 1f));

            // 다크 라운드 배경 + 시안 네온 테두리
            using (var path = Rounded(new RectangleF(12, 12, 232, 232), 56))
            {
                using (var bg = new LinearGradientBrush(new Point(0, 12), new Point(0, 244),
                       Color.FromArgb(40, 44, 56), Color.FromArgb(18, 20, 26)))
                    g.FillPath(bg, path);
                using (var glow = new Pen(Color.FromArgb(60, Cyan), 16 * k))
                    g.DrawPath(glow, path);
                using (var p = new Pen(Cyan, 6.5f * k))
                    g.DrawPath(p, path);
            }

            // 스피커 본체 (시안, 글로우 포함)
            PointF[] spk = {
                new PointF(46, 102), new PointF(84, 102), new PointF(128, 58),
                new PointF(128, 198), new PointF(84, 154), new PointF(46, 154) };
            using (var glow = new Pen(Color.FromArgb(70, Cyan), 14 * k))
            {
                glow.LineJoin = LineJoin.Round;
                g.DrawPolygon(glow, spk);
            }
            using (var b = new SolidBrush(Cyan)) g.FillPolygon(b, spk);

            // 음파 2줄 시안 + 바깥 1줄 마젠타 (투톤 포인트)
            DrawWave(g, new RectangleF(140, 102, 28, 52), Cyan, 10 * k, k);
            DrawWave(g, new RectangleF(154, 80, 44, 96), Cyan, 10 * k, k);
            DrawWave(g, new RectangleF(168, 56, 58, 144), Magenta, 10 * k, k);
        }
        return bmp;
    }

    static void DrawWave(Graphics g, RectangleF r, Color c, float w, float k)
    {
        using (var glow = new Pen(Color.FromArgb(70, c), w + 10 * k))
        {
            glow.StartCap = LineCap.Round; glow.EndCap = LineCap.Round;
            g.DrawArc(glow, r.X, r.Y, r.Width, r.Height, -58, 116);
        }
        using (var p = new Pen(c, w))
        {
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            g.DrawArc(p, r.X, r.Y, r.Width, r.Height, -58, 116);
        }
    }

    // 32bpp BGRA 상하반전 + AND 마스크 (ICO 내장 BMP 포맷)
    static byte[] BmpEntry(Bitmap b)
    {
        int w = b.Width, h = b.Height;
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(40); bw.Write(w); bw.Write(h * 2);
        bw.Write((short)1); bw.Write((short)32);
        bw.Write(0); bw.Write(w * h * 4);
        bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
        var bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var row = new byte[w * 4];
        for (int y = h - 1; y >= 0; y--)
        {
            Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), row, 0, w * 4);
            bw.Write(row);
        }
        b.UnlockBits(bd);
        int maskStride = ((w + 31) / 32) * 4;
        bw.Write(new byte[maskStride * h]);
        return ms.ToArray();
    }

    static byte[] PngEntry(Bitmap b)
    {
        var ms = new MemoryStream();
        b.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    static void Main()
    {
        int[] bmpSizes = { 16, 24, 32, 48, 64, 128 };
        var sizes = new List<int>(bmpSizes);
        sizes.Add(256);
        var blobs = new List<byte[]>();
        foreach (int sz in bmpSizes)
            using (var b = Render(sz)) blobs.Add(BmpEntry(b));
        using (var b256 = Render(256))
        {
            blobs.Add(PngEntry(b256));
            b256.Save("icon_preview.png", ImageFormat.Png); // 미리보기용
        }

        using (var fs = new FileStream("app.ico", FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Count);
            int offset = 6 + 16 * sizes.Count;
            for (int i = 0; i < sizes.Count; i++)
            {
                int sz = sizes[i];
                w.Write((byte)(sz == 256 ? 0 : sz));
                w.Write((byte)(sz == 256 ? 0 : sz));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(blobs[i].Length);
                w.Write(offset);
                offset += blobs[i].Length;
            }
            foreach (var blob in blobs) w.Write(blob);
        }
        Console.WriteLine("app.ico 생성 완료");
    }
}
