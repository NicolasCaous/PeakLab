// MakeSovietTex - gera as texturas do Fit_Soviet a partir do atlas do Scoutmaster
// (dumpado pelo PeakRecon em BepInEx\recon\tex\). Remapeia familias de cor
// preservando o sombreado original e desenha a estrela vermelha no peito.
// uso: MakeSovietTex.exe <pasta recon\tex> <pasta assets de saida>
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class MakeSovietTex
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("uso: MakeSovietTex.exe <pasta recon\\tex> <pasta assets>");
            return 1;
        }
        string texDir = args[0];
        string outDir = args[1];
        Directory.CreateDirectory(outDir);

        // atlas 1024x1024 do uniforme (regiao do emblema de montanha vira estrela)
        using (Bitmap atlas = new Bitmap(Path.Combine(texDir, "fits_14_Fit_Scoutmaster_Shorts_mat.png")))
        {
            // apaga a montanha clonando camisa limpa da esquerda (mesma faixa de y)
            CloneStamp(atlas, 635, 370, 810, 525, -190, 0);
            Remap(atlas, 0, 820, 440, 1023);
            DrawStar(atlas, 712f, 450f, 74f);
            atlas.Save(Path.Combine(outDir, "FitSoviet_atlas.png"), ImageFormat.Png);
        }
        // icone 256x256 do passaporte (estrela no peito esquerdo)
        using (Bitmap icon = new Bitmap(Path.Combine(texDir, "fits_14_Fit_Scoutmaster_Shorts_tex.png")))
        {
            Remap(icon, 100, 45, 155, 160);
            DrawStar(icon, 88f, 95f, 18f);
            icon.Save(Path.Combine(outDir, "FitSoviet_icon.png"), ImageFormat.Png);
        }
        // capacete: MedicHelmet ja e verde-oliva; troca o coracao por estrela
        using (Bitmap helm = new Bitmap(Path.Combine(texDir, "hat_7_MedicHelmet_mat.png")))
        {
            CloneStamp(helm, 340, 365, 720, 720, -330, 0);
            DrawStar(helm, 528f, 530f, 118f);
            helm.Save(Path.Combine(outDir, "SovietHelmet_tex.png"), ImageFormat.Png);
        }
        Console.WriteLine("ok: FitSoviet_atlas.png + FitSoviet_icon.png em " + outDir);
        return 0;
    }

    static void CloneStamp(Bitmap bmp, int x0, int y0, int x1, int y1, int dx, int dy)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int sx = x + dx, sy = y + dy;
                if (sx < 0 || sy < 0 || sx >= bmp.Width || sy >= bmp.Height) continue;
                bmp.SetPixel(x, y, bmp.GetPixel(sx, sy));
            }
        }
    }

    // remapeia por familia de cor preservando o sombreado original
    static void Remap(Bitmap bmp, int tx0, int ty0, int tx1, int ty1)
    {
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A < 8) continue;
                int r = c.R, g = c.G, b = c.B;
                int max = Math.Max(r, Math.Max(g, b));
                int min = Math.Min(r, Math.Min(g, b));
                double v = max / 255.0;
                double s = max == 0 ? 0 : (max - min) / (double)max;

                bool amarelo = r > 170 && g > 140 && b < 130 && s > 0.35;
                bool marrom = !amarelo && r > g && g >= b && (r - b) > 25;
                bool verde = !amarelo && g >= r && g > b;
                bool claro = (max - min) < 30 && v > 0.55;

                if (amarelo)
                {
                    // frisos/fivela viram dourado discreto
                    bmp.SetPixel(x, y, FromHsv(0.13, 0.55, v * 0.85, c.A));
                }
                else if (marrom)
                {
                    // camisa marrom -> caqui amarelado (referencia: gimnastyorka)
                    bmp.SetPixel(x, y, FromHsv(0.155, 0.38 + s * 0.20, Math.Min(1.0, v * 1.12), c.A));
                }
                else if (verde && v < 0.35 && x >= tx0 && x <= tx1 && y >= ty0 && y <= ty1)
                {
                    // gravata verde-escura -> vermelho (acento de gola sovietico)
                    bmp.SetPixel(x, y, FromHsv(0.0, 0.68, Math.Min(0.60, v * 1.75), c.A));
                }
                else if (verde)
                {
                    // cinto/calcao verdes -> verde-oliva militar
                    bmp.SetPixel(x, y, FromHsv(0.19, s * 0.65, v * 0.88, c.A));
                }
                else if (claro)
                {
                    // gola/meias claras: mantem, so esfria um pouco
                    bmp.SetPixel(x, y, Color.FromArgb(c.A,
                        Clamp(r - 6), Clamp(g - 3), Clamp(b)));
                }
            }
        }
    }

    static void DrawStar(Bitmap bmp, float cx, float cy, float radius)
    {
        PointF[] pts = new PointF[10];
        double rot = -Math.PI / 2.0; // ponta para cima
        for (int i = 0; i < 10; i++)
        {
            double ang = rot + i * Math.PI / 5.0;
            double rr = (i % 2 == 0) ? radius : radius * 0.42;
            pts[i] = new PointF(cx + (float)(Math.Cos(ang) * rr),
                                cy + (float)(Math.Sin(ang) * rr));
        }
        using (Graphics gr = Graphics.FromImage(bmp))
        {
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            using (Brush fill = new SolidBrush(Color.FromArgb(255, 178, 34, 34)))
            using (Pen edge = new Pen(Color.FromArgb(255, 110, 16, 16), Math.Max(2f, radius * 0.08f)))
            {
                edge.LineJoin = LineJoin.Round;
                gr.FillPolygon(fill, pts);
                gr.DrawPolygon(edge, pts);
            }
        }
    }

    static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }

    // h,s,v em 0..1
    static Color FromHsv(double h, double s, double v, int a)
    {
        double r, g, b;
        int i = (int)Math.Floor(h * 6) % 6;
        double f = h * 6 - Math.Floor(h * 6);
        double p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return Color.FromArgb(a, Clamp((int)(r * 255)), Clamp((int)(g * 255)), Clamp((int)(b * 255)));
    }
}
