using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

namespace SextaFeira;

/// <summary>Visual da SEXTA-FEIRA: escuro com azul holográfico.</summary>
internal static class Tema
{
    public static readonly Color Fundo = Color.FromArgb(10, 12, 20);
    public static readonly Color Lateral = Color.FromArgb(14, 17, 28);
    public static readonly Color Card = Color.FromArgb(20, 24, 38);
    public static readonly Color CardClaro = Color.FromArgb(30, 35, 54);
    public static readonly Color Texto = Color.FromArgb(232, 236, 245);
    public static readonly Color Apagado = Color.FromArgb(132, 140, 165);
    public static readonly Color Destaque = Color.FromArgb(0, 209, 255);
    public static readonly Color DestaqueEscuro = Color.FromArgb(0, 120, 160);
    public static readonly Color Verde = Color.FromArgb(46, 213, 115);
    public static readonly Color Amarelo = Color.FromArgb(255, 193, 7);
    public static readonly Color Vermelho = Color.FromArgb(255, 82, 82);

    public static readonly Font Normal = new("Segoe UI", 10f);
    public static readonly Font Pequena = new("Segoe UI", 9f);
    public static readonly Font Negrito = new("Segoe UI Semibold", 10.5f);
    public static readonly Font Subtitulo = new("Segoe UI Semibold", 13f);
    public static readonly Font Titulo = new("Segoe UI", 22f, FontStyle.Bold);
    public static readonly Font Mono = new("Consolas", 9.5f);

    private static Icon icone;
    public static Icon Icone
    {
        get
        {
            if (icone != null) return icone;
            try
            {
                using var s = typeof(Tema).Assembly.GetManifestResourceStream("SextaFeira.icone.ico");
                icone = s != null ? new Icon(s) : SystemIcons.Application;
            }
            catch { icone = SystemIcons.Application; }
            return icone;
        }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string app, string id);

    /// <summary>Deixa as barras de rolagem do controle escuras (Windows 10 1809+ e 11).</summary>
    public static void Escurecer(Control c)
    {
        void Aplicar() { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }
        if (c.IsHandleCreated) Aplicar();
        else c.HandleCreated += (_, _) => Aplicar();
    }

    public static Label Rotulo(string texto, Font fonte = null, Color? cor = null)
    {
        return new Label
        {
            Text = texto,
            Font = fonte ?? Normal,
            ForeColor = cor ?? Texto,
            BackColor = Color.Transparent,
            AutoSize = true
        };
    }

    public static Button Botao(string texto, Color? cor = null, int largura = 160, int altura = 40)
    {
        var c = cor ?? Destaque;
        var b = new Button
        {
            Text = texto,
            Font = Negrito,
            Size = new Size(largura, altura),
            FlatStyle = FlatStyle.Flat,
            BackColor = c,
            ForeColor = c.GetBrightness() > 0.55f ? Color.FromArgb(10, 12, 20) : Color.White,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(c, 0.15f);
        b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(c, 0.1f);
        return b;
    }

    public static Panel CardPainel()
    {
        return new Panel { BackColor = Card, Padding = new Padding(18) };
    }

    public static GraphicsPath Arredondado(Rectangle r, int raio)
    {
        var p = new GraphicsPath();
        int d = raio * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// <summary>Barra de uso (CPU/RAM/GPU) desenhada à mão.</summary>
internal class BarraUso : Control
{
    private float valor = -1;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Nome { get; set; } = "";

    public BarraUso()
    {
        DoubleBuffered = true;
        Height = 46;
        BackColor = Tema.Card;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Valor
    {
        get => valor;
        set { valor = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        TextRenderer.DrawText(g, Nome, Tema.Negrito, new Point(0, 0), Tema.Texto);
        string txt = valor < 0 ? "n/d" : $"{valor:0}%";
        var tam = TextRenderer.MeasureText(txt, Tema.Negrito);
        TextRenderer.DrawText(g, txt, Tema.Negrito, new Point(Width - tam.Width, 0), CorPara(valor));

        var trilho = new Rectangle(0, 26, Width - 1, 12);
        using (var p = Tema.Arredondado(trilho, 6))
        using (var b = new SolidBrush(Tema.CardClaro))
            g.FillPath(b, p);

        if (valor > 0)
        {
            int w = Math.Max(12, (int)(trilho.Width * Math.Min(100, valor) / 100f));
            var cheio = new Rectangle(trilho.X, trilho.Y, w, trilho.Height);
            using var p = Tema.Arredondado(cheio, 6);
            using var b = new LinearGradientBrush(cheio, ControlPaint.Dark(CorPara(valor), 0.2f), CorPara(valor), 0f);
            g.FillPath(b, p);
        }
    }

    private static Color CorPara(float v) =>
        v < 0 ? Tema.Apagado : v >= 90 ? Tema.Vermelho : v >= 70 ? Tema.Amarelo : Tema.Destaque;
}
