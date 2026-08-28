// CropTex - recorta uma regiao de um PNG e amplia (inspecao de detalhes)
// uso: CropTex.exe <in.png> <x> <y> <w> <h> <escala> <out.png>
using System;
using System.Drawing;
using System.Drawing.Imaging;

class CropTex
{
    static int Main(string[] a)
    {
        if (a.Length < 7) { Console.WriteLine("uso: CropTex.exe in x y w h escala out"); return 1; }
        int x = int.Parse(a[1]), y = int.Parse(a[2]), w = int.Parse(a[3]), h = int.Parse(a[4]), sc = int.Parse(a[5]);
        using (Bitmap src = new Bitmap(a[0]))
        using (Bitmap dst = new Bitmap(w * sc, h * sc))
        {
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0, 0, w * sc, h * sc), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
            }
            dst.Save(a[6], ImageFormat.Png);
        }
        Console.WriteLine("ok");
        return 0;
    }
}
