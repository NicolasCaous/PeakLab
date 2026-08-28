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

        // atlas 1024x1024 do uniforme. Regioes confirmadas pelo mapa UV
        // (tools\DecodeUV.cs + render unlit do boneco):
        //   x718-908 y40-262  = ombros/gola direita  -> patente vermelha + estrela dourada
        //   x435-705 y0-58    = topo do peito/gola    -> faixa vermelha
        //   x62-348  y282-622 = meiao ("janela")      -> calca caqui + cano de coturno preto
        //   x0-437   y840+    = gravata               -> vermelha
        //   montanha (712,450) = emblema das COSTAS   -> estrela vermelha
        using (Bitmap atlas = new Bitmap(Path.Combine(texDir, "fits_14_Fit_Scoutmaster_Shorts_mat.png")))
        {
            // apaga a montanha clonando camisa limpa da esquerda (mesma faixa de y)
            CloneStamp(atlas, 635, 370, 810, 525, -190, 0);
            Remap(atlas, 0, 820, 440, 1023);
            // gola: faixa vermelha no topo do peito
            PaintRegion(atlas, 435, 0, 705, 58, 0.0, 0.70, 0.55, 0.18, true);
            // patente (pogony): vermelho dentro da moldura escura do "quadro"
            PaintRegion(atlas, 718, 40, 908, 262, 0.0, 0.70, 0.55, 0.18, true);
            // meiao/canela do PERSONAGEM REAL: faixa diagonal ate o canto (1023,1023)
            // (mapeado por selfie unlit com atlas-gradiente; o boneco do passaporte
            // usa OUTRO mesh com UV proprio - regiao coberta por inteiro para os dois)
            PaintRegion(atlas, 62, 282, 348, 455, 0.15, 0.45, 0.60, 0.12, false);
            PaintRegion(atlas, 62, 455, 348, 625, 0.08, 0.15, 0.12, 0.05, false);
            PaintRegion(atlas, 448, 560, 1023, 1023, 0.15, 0.45, 0.60, 0.12, false);
            // ombreira: a costura do topo da manga sampleia ~(800,360) - banda vermelha
            PaintRegion(atlas, 748, 328, 852, 398, 0.0, 0.70, 0.55, 0.18, false);
            // estrela vermelha nas costas (antigo emblema de montanha)
            DrawStar(atlas, 712f, 450f, 74f,
                Color.FromArgb(255, 178, 34, 34), Color.FromArgb(255, 110, 16, 16));
            // estrela dourada na patente do ombro
            DrawStar(atlas, 818f, 152f, 52f,
                Color.FromArgb(255, 214, 176, 56), Color.FromArgb(255, 120, 90, 20));
            atlas.Save(Path.Combine(outDir, "FitSoviet_atlas.png"), ImageFormat.Png);
        }
        // icone 256x256 do passaporte (estrela no peito + ombreiras vermelhas)
        using (Bitmap icon = new Bitmap(Path.Combine(texDir, "fits_14_Fit_Scoutmaster_Shorts_tex.png")))
        {
            Remap(icon, 100, 45, 155, 160);
            PaintRegion(icon, 52, 38, 96, 56, 0.0, 0.70, 0.55, 0.18, true);
            PaintRegion(icon, 158, 38, 202, 56, 0.0, 0.70, 0.55, 0.18, true);
            DrawStar(icon, 88f, 95f, 18f,
                Color.FromArgb(255, 178, 34, 34), Color.FromArgb(255, 110, 16, 16));
            icon.Save(Path.Combine(outDir, "FitSoviet_icon.png"), ImageFormat.Png);
        }
        // capacete: MedicHelmet ja e verde-oliva; troca o coracao por estrela
        using (Bitmap helm = new Bitmap(Path.Combine(texDir, "hat_7_MedicHelmet_mat.png")))
        {
            CloneStamp(helm, 340, 365, 720, 720, -330, 0);
            DrawStar(helm, 528f, 530f, 118f);
            helm.Save(Path.Combine(outDir, "SovietHelmet_tex.png"), ImageFormat.Png);
        }
        // grade de debug UV: 8x8 celulas rotuladas A1..H8 para descobrir que regiao
        // do atlas cai em cada parte do corpo (vestir via autopilot e fotografar)
        using (Bitmap grid = new Bitmap(1024, 1024))
        {
            Color[] cols = new Color[]
            {
                Color.Red, Color.Lime, Color.Blue, Color.Yellow,
                Color.Magenta, Color.Cyan, Color.Orange, Color.White
            };
            using (Graphics gr = Graphics.FromImage(grid))
            using (Font f = new Font("Arial", 22, FontStyle.Bold))
            {
                for (int cy = 0; cy < 8; cy++)
                {
                    for (int cx = 0; cx < 8; cx++)
                    {
                        Color cc = cols[(cx + cy) % 8];
                        using (Brush b = new SolidBrush(cc))
                            gr.FillRectangle(b, cx * 128, cy * 128, 128, 128);
                        string label = "" + (char)('A' + cy) + (cx + 1);
                        gr.DrawString(label, f, Brushes.Black, cx * 128 + 6, cy * 128 + 6);
                        gr.DrawString(label, f, Brushes.Black, cx * 128 + 60, cy * 128 + 88);
                    }
                }
            }
            grid.Save(Path.Combine(outDir, "UVGrid_atlas.png"), ImageFormat.Png);
        }
        // gradiente UV: R=coluna, G=linha, B=0 (marca d'agua). Um pixel do render
        // unlit decodifica direto para a posicao no atlas (ver tools\DecodeUV.cs)
        using (Bitmap grad = new Bitmap(1024, 1024))
        {
            for (int y = 0; y < 1024; y++)
            {
                for (int x = 0; x < 1024; x++)
                {
                    grad.SetPixel(x, y, Color.FromArgb(255, (int)(x * 255.0 / 1023.0),
                                                       (int)(y * 255.0 / 1023.0), 0));
                }
            }
            grad.Save(Path.Combine(outDir, "UVGradient_atlas.png"), ImageFormat.Png);
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
                    // camisa marrom -> caqui esverdeado CLARO (levanta os tons escuros
                    // para nao confundir com o marrom original sob luz quente)
                    bmp.SetPixel(x, y, FromHsv(0.175, 0.40 + s * 0.15, Math.Min(1.0, 0.22 + v * 0.85), c.A));
                }
                else if (verde && v < 0.35 && x >= tx0 && x <= tx1 && y >= ty0 && y <= ty1)
                {
                    // gravata verde-escura -> vermelho (acento de gola sovietico)
                    bmp.SetPixel(x, y, FromHsv(0.0, 0.68, Math.Min(0.60, v * 1.75), c.A));
                }
                else if (verde)
                {
                    // cinto/calcao verdes -> caqui um tom abaixo da camisa (referencia)
                    bmp.SetPixel(x, y, FromHsv(0.155, s * 0.55, Math.Min(1.0, 0.10 + v * 0.85), c.A));
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
        DrawStar(bmp, cx, cy, radius,
            Color.FromArgb(255, 178, 34, 34), Color.FromArgb(255, 110, 16, 16));
    }

    static void DrawStar(Bitmap bmp, float cx, float cy, float radius, Color fillC, Color edgeC)
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
            using (Brush fill = new SolidBrush(fillC))
            using (Pen edge = new Pen(edgeC, Math.Max(2f, radius * 0.08f)))
            {
                edge.LineJoin = LineJoin.Round;
                gr.FillPolygon(fill, pts);
                gr.DrawPolygon(edge, pts);
            }
        }
    }

    // recolore um retangulo do atlas preservando o sombreado (v do pixel);
    // skipDark preserva contornos escuros (ex.: moldura da patente)
    static void PaintRegion(Bitmap bmp, int x0, int y0, int x1, int y1,
                            double h, double s, double vmul, double vadd, bool skipDark)
    {
        for (int y = y0; y <= y1 && y < bmp.Height; y++)
        {
            for (int x = x0; x <= x1 && x < bmp.Width; x++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A < 8) continue;
                int max = Math.Max(c.R, Math.Max(c.G, c.B));
                double v = max / 255.0;
                if (skipDark && v < 0.30) continue;
                bmp.SetPixel(x, y, FromHsv(h, s, Math.Min(1.0, v * vmul + vadd), c.A));
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
