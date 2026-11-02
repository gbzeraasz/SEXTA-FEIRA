using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace SextaFeira;

/// <summary>Linha do ping ao longo do teste. Pontos vermelhos = pacote perdido.</summary>
internal class GraficoPing : Control
{
    private float[] valores = Array.Empty<float>();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float EscalaMax { get; set; } = 0; // 0 = automático

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float[] Valores
    {
        get => valores;
        set { valores = value ?? Array.Empty<float>(); Invalidate(); }
    }

    public GraficoPing()
    {
        DoubleBuffered = true;
        BackColor = Tema.CardClaro;
        Height = 40;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (valores.Length < 2) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var ok = valores.Where(v => v >= 0).ToList();
        float max = EscalaMax > 0 ? EscalaMax : Math.Max(20, ok.Count > 0 ? ok.Max() * 1.15f : 20);
        float min = ok.Count > 0 ? ok.Min() : 0;
        float w = Width - 4, h = Height - 6;
        float X(int i) => 2 + w * i / (valores.Length - 1);
        float Y(float v) => 3 + h - h * Math.Min(v, max) / max;

        // linha de "pico" (melhor + 30 ms)
        using (var tracejado = new Pen(Color.FromArgb(70, Tema.Amarelo)) { DashStyle = DashStyle.Dash })
            if (min + 30 < max) g.DrawLine(tracejado, 2, Y(min + 30), Width - 2, Y(min + 30));

        using var linha = new Pen(Tema.Destaque, 1.6f);
        PointF? anterior = null;
        for (int i = 0; i < valores.Length; i++)
        {
            if (valores[i] < 0)
            {
                using var perdido = new SolidBrush(Tema.Vermelho);
                g.FillEllipse(perdido, X(i) - 2.5f, Height - 7, 5, 5);
                anterior = null;
                continue;
            }
            var p = new PointF(X(i), Y(valores[i]));
            if (anterior != null) g.DrawLine(linha, anterior.Value, p);
            if (valores[i] > min + 30)
            {
                using var pico = new SolidBrush(Tema.Amarelo);
                g.FillEllipse(pico, p.X - 2, p.Y - 2, 4, 4);
            }
            anterior = p;
        }
    }
}
