// DecodeUV - le o render unlit do boneco vestido com UVGradient_atlas.png e
// pinta, sobre o atlas original escurecido, qual parte do corpo usa cada regiao.
// Faixas de tela (render 512x512 do dummyCamera com o ajuste do PeakAutoTest):
//   ombros=vermelho, mangas=laranja, peito=verde, cinto=azul, calcao=amarelo,
//   meiao=magenta, sapato/bota=ciano.
// uso: DecodeUV.exe <dummy_rt.png> <atlas_original.png> <saida_map.png>
using System;
using System.Drawing;
using System.Drawing.Imaging;

class DecodeUV
{
    static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("uso: DecodeUV.exe <dummy_rt.png> <atlas_original.png> <saida_map.png>");
            return 1;
        }
        using (Bitmap rt = new Bitmap(args[0]))
        using (Bitmap baseAtlas = new Bitmap(args[1]))
        using (Bitmap outMap = new Bitmap(1024, 1024))
        {
            // fundo: atlas original escurecido para dar contexto
            using (Graphics g = Graphics.FromImage(outMap))
            {
                g.DrawImage(baseAtlas, 0, 0, 1024, 1024);
                using (Brush dim = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                    g.FillRectangle(dim, 0, 0, 1024, 1024);
            }
            int pintados = 0;
            for (int y = 0; y < rt.Height; y++)
            {
                Color band = Classify(y, rt.Height);
                if (band.A == 0) continue;
                for (int x = 0; x < rt.Width; x++)
                {
                    Color c = rt.GetPixel(x, y);
                    // so aceita pixels do gradiente: B~0 e nao-preto
                    if (c.B > 40 || c.R + c.G < 25) continue;
                    if (c.R > 230 && c.G > 230) continue; // branco = pele
                    // mangas vs peito: faixa lateral
                    Color use = band;
                    if (band.G == 255 && band.R == 0 && (x < rt.Width * 34 / 100 || x > rt.Width * 66 / 100))
                        use = Color.Orange;
                    int ax = (int)(c.R / 255.0 * 1023.0);
                    int ay = (int)(c.G / 255.0 * 1023.0);
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int px = ax + dx, py = ay + dy;
                            if (px < 0 || py < 0 || px > 1023 || py > 1023) continue;
                            outMap.SetPixel(px, py, use);
                        }
                    }
                    pintados++;
                }
            }
            outMap.Save(args[2], ImageFormat.Png);
            Console.WriteLine("ok: " + pintados + " pixels decodificados -> " + args[2]);
        }
        return 0;
    }

    // faixas verticais do render 512 (escala se o render tiver outro tamanho)
    static Color Classify(int y, int h)
    {
        double f = y / (double)h;
        if (f >= 0.235 && f < 0.290) return Color.Red;                  // ombros/trapezios
        if (f >= 0.290 && f < 0.420) return Color.FromArgb(0, 255, 0);  // peito (mangas via x)
        if (f >= 0.420 && f < 0.455) return Color.Blue;                 // cinto
        if (f >= 0.455 && f < 0.540) return Color.Yellow;               // calcao
        if (f >= 0.600 && f < 0.665) return Color.Magenta;              // meiao
        if (f >= 0.665 && f < 0.760) return Color.Cyan;                 // sapato/bota
        return Color.FromArgb(0, 0, 0, 0);
    }
}
