using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

namespace SextaFeira;

/// <summary>Visual da SEXTA-FEIRA: escuro com azul holográfico.</summary>
internal static class Tema
{
    public static readonly Color Fundo = Color.FromArgb(7, 10, 18);
    public static readonly Color Lateral = Color.FromArgb(9, 13, 23);
    public static readonly Color Card = Color.FromArgb(13, 19, 33);
    public static readonly Color CardClaro = Color.FromArgb(22, 31, 50);
    public static readonly Color Texto = Color.FromArgb(232, 236, 245);
    public static readonly Color Apagado = Color.FromArgb(128, 146, 172);
    public static readonly Color Destaque = Color.FromArgb(0, 209, 255);
    public static readonly Color DestaqueEscuro = Color.FromArgb(0, 120, 160);
    public static readonly Color Verde = Color.FromArgb(46, 213, 115);
    public static readonly Color Amarelo = Color.FromArgb(255, 193, 7);
    public static readonly Color Vermelho = Color.FromArgb(255, 82, 82);

    public static readonly Font Normal = new("Segoe UI", 10f);
    public static readonly Font Pequena = new("Segoe UI", 9f);
    public static readonly Font Negrito = new("Segoe UI Semibold", 10.5f);
    public static readonly Font Subtitulo = new("Segoe UI Semibold", 13f);
    public static readonly Font Titulo = new("Segoe UI Semibold", 22f);
    public static readonly Font Mono = new("Segoe UI", 9.5f);

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
        return new BotaoArredondado
        {
            Text = texto,
            Font = Negrito,
            Size = new Size(largura, altura),
            BackColor = cor ?? Destaque
        };
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

/// <summary>
/// Botão arredondado no estilo HUD. A cor vem do BackColor (igual um Button normal):
/// cor forte = botão cheio com degradê; CardClaro = botão de vidro com borda azul.
/// </summary>
internal class BotaoArredondado : Button
{
    private bool hover, apertado;
    private Color cor = Tema.Destaque;

    public BotaoArredondado()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        cor = BackColor;
        Invalidate();
        base.OnBackColorChanged(e);
    }

    protected override bool ShowFocusCues => false;
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; apertado = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { apertado = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { apertado = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Hud.PintarFundoDoPai(this, g);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool vidro = cor == Tema.CardClaro || cor == Tema.Card;
        var c = Enabled ? cor : Tema.CardClaro;
        var r = new RectangleF(1, 1, Width - 3, Height - 3);
        using var caminho = Hud.Redondo(r, Math.Min(10, Height / 2f));

        if (vidro || !Enabled)
        {
            using var fundo = new SolidBrush(Hud.Alfa(Tema.Destaque, apertado ? 60 : hover ? 36 : 14));
            g.FillPath(fundo, caminho);
            using var borda = new Pen(Hud.Alfa(Tema.Destaque, !Enabled ? 40 : hover ? 200 : 85), 1.2f);
            g.DrawPath(borda, caminho);
        }
        else
        {
            var cima = hover ? ControlPaint.Light(c, 0.35f) : ControlPaint.Light(c, 0.15f);
            var baixo = apertado ? ControlPaint.Dark(c, 0.25f) : ControlPaint.Dark(c, 0.08f);
            using var fundo = new LinearGradientBrush(r, cima, baixo, 90f);
            g.FillPath(fundo, caminho);
            if (hover)
                using (var brilho = new Pen(Hud.Alfa(c, 70), 4)) g.DrawPath(brilho, caminho);
        }

        Color corTexto = !Enabled ? Tema.Apagado
            : vidro ? (hover ? Color.White : Tema.Texto)
            : (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 140 ? Color.FromArgb(4, 16, 28) : Color.White;
        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), corTexto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}
