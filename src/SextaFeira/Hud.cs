using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace SextaFeira;

/// <summary>Cores e fontes do visual holográfico (HUD) da SEXTA-FEIRA. Design original.</summary>
internal static class Hud
{
    public static readonly Color Ciano = Color.FromArgb(0, 209, 255);
    public static readonly Color CianoClaro = Color.FromArgb(140, 235, 255);
    public static readonly Color Fundo = Color.FromArgb(7, 10, 18);
    public static readonly Color Painel = Color.FromArgb(205, 11, 17, 30);
    public static readonly Color Linha = Color.FromArgb(70, 0, 209, 255);

    // Fontes: família Segoe UI (a do próprio Windows), mais limpa e arredondada.
    private static string Fam(string nome, string reserva = "Segoe UI") => Existe(nome) ? nome : reserva;
    public static readonly string FonteForte = Fam("Segoe UI Semibold");
    public static readonly string FonteLeve = Fam("Segoe UI Light");
    public static readonly string FonteTexto = "Segoe UI";

    /// <summary>Títulos e botões: Bold vira Semibold (fica mais elegante que o negrito cheio).</summary>
    public static Font Titulo(float tam, FontStyle s = FontStyle.Regular) =>
        s.HasFlag(FontStyle.Bold) ? new Font(FonteForte, tam, s & ~FontStyle.Bold) : new Font(FonteTexto, tam, s);
    /// <summary>Números grandes (nota, temperatura): fino e grande, estilo holograma.</summary>
    public static Font Leve(float tam) => new(FonteLeve, tam);
    /// <summary>Texto pequeno de apoio (antes era monoespaçado).</summary>
    public static Font Mono(float tam) => new(FonteTexto, tam + 0.5f);

    private static bool Existe(string nome)
    {
        try { using var f = new FontFamily(nome); return true; } catch { return false; }
    }

    public static Color Alfa(Color c, int a) => Color.FromArgb(Math.Clamp(a, 0, 255), c);

    public static Color CorNivel(float v) => v >= 90 ? Tema.Vermelho : v >= 70 ? Tema.Amarelo : Ciano;

    /// <summary>Retângulo com cantos arredondados (float).</summary>
    public static GraphicsPath Redondo(RectangleF r, float raio)
    {
        var p = new GraphicsPath();
        float d = Math.Min(raio * 2, Math.Min(r.Width, r.Height));
        if (d <= 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Retângulo com os cantos cortados (estilo HUD).</summary>
    public static GraphicsPath Chanfrado(RectangleF r, float corte)
    {
        var p = new GraphicsPath();
        p.AddLines(new[]
        {
            new PointF(r.Left + corte, r.Top), new PointF(r.Right, r.Top),
            new PointF(r.Right, r.Bottom - corte), new PointF(r.Right - corte, r.Bottom),
            new PointF(r.Left, r.Bottom), new PointF(r.Left, r.Top + corte)
        });
        p.CloseFigure();
        return p;
    }

    /// <summary>Colchetes nos 4 cantos.</summary>
    public static void Colchetes(Graphics g, RectangleF r, Color cor, float tam = 12, float esp = 2)
    {
        using var p = new Pen(cor, esp);
        g.DrawLines(p, new[] { new PointF(r.Left, r.Top + tam), new PointF(r.Left, r.Top), new PointF(r.Left + tam, r.Top) });
        g.DrawLines(p, new[] { new PointF(r.Right - tam, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Top + tam) });
        g.DrawLines(p, new[] { new PointF(r.Right, r.Bottom - tam), new PointF(r.Right, r.Bottom), new PointF(r.Right - tam, r.Bottom) });
        g.DrawLines(p, new[] { new PointF(r.Left + tam, r.Bottom), new PointF(r.Left, r.Bottom), new PointF(r.Left, r.Bottom - tam) });
    }

    // ---------- fundo com grade hexagonal (desenhado uma vez e reaproveitado) ----------
    private static Bitmap fundoCache;
    private static Size fundoTam;

    public static Bitmap FundoGrade(Size tam)
    {
        if (fundoCache != null && fundoTam == tam) return fundoCache;
        fundoCache?.Dispose();
        fundoTam = tam;
        var bmp = new Bitmap(Math.Max(1, tam.Width), Math.Max(1, tam.Height));
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Fundo);

        // brilho suave no centro
        using (var caminho = new GraphicsPath())
        {
            caminho.AddEllipse(-tam.Width * 0.2f, -tam.Height * 0.3f, tam.Width * 1.4f, tam.Height * 1.4f);
            using var br = new PathGradientBrush(caminho) { CenterColor = Color.FromArgb(18, 30, 52), SurroundColors = new[] { Fundo } };
            g.FillRectangle(br, 0, 0, tam.Width, tam.Height);
        }

        // hexágonos
        float raio = 22, w = raio * 1.732f, h = raio * 1.5f;
        using var pen = new Pen(Color.FromArgb(10, 0, 209, 255), 1);
        for (int lin = -1; lin * h < tam.Height + raio; lin++)
            for (int col = -1; col * w < tam.Width + w; col++)
            {
                float cx = col * w + (lin % 2 == 0 ? 0 : w / 2), cy = lin * h;
                var pts = Enumerable.Range(0, 6).Select(i =>
                {
                    double a = Math.PI / 180 * (60 * i - 30);
                    return new PointF(cx + raio * (float)Math.Cos(a), cy + raio * (float)Math.Sin(a));
                }).ToArray();
                g.DrawPolygon(pen, pts);
            }

        // vinheta nas bordas
        using (var caminho = new GraphicsPath())
        {
            caminho.AddRectangle(new Rectangle(0, 0, tam.Width, tam.Height));
            using var vin = new PathGradientBrush(caminho) { CenterColor = Color.Transparent, SurroundColors = new[] { Color.FromArgb(150, 3, 5, 10) }, FocusScales = new PointF(0.75f, 0.7f) };
            g.FillRectangle(vin, 0, 0, tam.Width, tam.Height);
        }
        fundoCache = bmp;
        return bmp;
    }
}

/// <summary>
/// Página com fundo holográfico. Filhos transparentes mostram a grade por trás.
/// Se estiver dentro de outra PainelHud, continua a grade da página (fica tudo emendado).
/// Cards (painéis com cor Tema.Card) ganham cantos arredondados e moldura de vidro automaticamente.
/// </summary>
internal class PainelHud : Panel
{
    public PainelHud()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Hud.Fundo;
    }

    private PainelHud Raiz()
    {
        PainelHud r = this;
        for (var c = Parent; c != null; c = c.Parent)
            if (c is PainelHud ph) r = ph;
        return r;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;
        var raiz = Raiz();
        if (raiz == this) e.Graphics.DrawImageUnscaled(Hud.FundoGrade(Size), 0, 0);
        else
        {
            var deslocamento = raiz.PointToClient(PointToScreen(Point.Empty));
            e.Graphics.DrawImageUnscaled(Hud.FundoGrade(raiz.Size), -deslocamento.X, -deslocamento.Y);
        }

        // moldura de vidro em volta dos cards
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (Control c in Controls)
        {
            if (!c.Visible || !EhCard(c)) continue;
            var r = new RectangleF(c.Left - 1.5f, c.Top - 1.5f, c.Width + 2f, c.Height + 2f);
            using (var aura = Hud.Redondo(new RectangleF(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6), 16))
            using (var b = new SolidBrush(Color.FromArgb(14, 0, 209, 255)))
                g.FillPath(b, aura);
            using var borda = Hud.Redondo(r, 13);
            using var pen = new Pen(Color.FromArgb(48, 0, 209, 255), 1.2f);
            g.DrawPath(pen, borda);
        }
    }

    public static bool EhCard(Control c) => c.BackColor == Tema.Card && c is not Button && c is not Label;

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        var c = e.Control;
        void Arredondar()
        {
            if (!EhCard(c) || c.Width <= 0 || c.Height <= 0) return;
            using var caminho = Hud.Redondo(new RectangleF(0, 0, c.Width, c.Height), 12);
            c.Region = new Region(caminho);
        }
        Arredondar();
        c.SizeChanged += (_, _) => { Arredondar(); Invalidate(); };
        c.BackColorChanged += (_, _) => { Arredondar(); Invalidate(); };
        c.LocationChanged += (_, _) => Invalidate();
        c.VisibleChanged += (_, _) => Invalidate();
        if (c is Panel && EhCard(c))
            c.Paint += (_, pe) =>
            {
                // reflexo fino no topo do card
                using var br = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(2, c.Width), 2), Color.Transparent, Color.Transparent, 0f);
                br.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { Color.Transparent, Color.FromArgb(120, 0, 209, 255), Color.Transparent },
                    Positions = new[] { 0f, 0.5f, 1f }
                };
                pe.Graphics.FillRectangle(br, 12, 0, c.Width - 24, 1);
            };
    }

    protected override void OnControlRemoved(ControlEventArgs e) { base.OnControlRemoved(e); Invalidate(); }

    // A grade fica parada e o conteúdo rola: redesenha tudo pra não "borrar".
    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(true); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); if (VerticalScroll.Visible) Invalidate(true); }
}

/// <summary>Card translúcido com cantos em colchete e título em fonte técnica.</summary>
internal class CardHud : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Titulo { get; set; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CorDestaque { get; set; } = Hud.Ciano;

    public CardHud()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(1, 1, Width - 3, Height - 3);
        using (var caminho = Hud.Redondo(r, 14))
        {
            // vidro: um pouco mais claro em cima, escurecendo pra baixo
            using (var fundo = new LinearGradientBrush(r, Color.FromArgb(215, 16, 24, 40), Color.FromArgb(200, 9, 13, 24), 90f))
                g.FillPath(fundo, caminho);
            using var borda = new Pen(Hud.Alfa(CorDestaque, 38), 1);
            g.DrawPath(borda, caminho);
        }
        // reflexo fino no topo, que some nas pontas
        using (var reflexo = new LinearGradientBrush(new RectangleF(14, 1, Width - 28, 2), Color.Transparent, Color.Transparent, 0f))
        {
            reflexo.InterpolationColors = new ColorBlend
            {
                Colors = new[] { Color.Transparent, Hud.Alfa(CorDestaque, 150), Color.Transparent },
                Positions = new[] { 0f, 0.5f, 1f }
            };
            g.FillRectangle(reflexo, 14, 1, Width - 28, 1.5f);
        }

        if (!string.IsNullOrEmpty(Titulo))
        {
            using var f = Hud.Titulo(9.5f, FontStyle.Bold);
            using (var ponto = new SolidBrush(CorDestaque)) g.FillEllipse(ponto, 18, 19, 6, 6);
            using (var aura = new SolidBrush(Hud.Alfa(CorDestaque, 50))) g.FillEllipse(aura, 15, 16, 12, 12);
            TextRenderer.DrawText(g, Titulo, f, new Point(30, 12), Hud.Alfa(Hud.CianoClaro, 235), TextFormatFlags.NoPadding);
            using var lin = new Pen(Hud.Alfa(CorDestaque, 22), 1);
            g.DrawLine(lin, 18, 38, Width - 18, 38);
        }
    }
}

/// <summary>Botão arredondado com brilho no hover e "acender" ao clicar.</summary>
internal class BotaoHud : Control
{
    private bool hover, apertado;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Cor { get; set; } = Hud.Ciano;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Cheio { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Icone { get; set; }

    public BotaoHud()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Hud.Titulo(10.5f, FontStyle.Bold);
        ForeColor = Tema.Texto;
        Size = new Size(200, 44);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; apertado = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { apertado = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { apertado = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var cor = Enabled ? Cor : Tema.Apagado;
        var r = new RectangleF(1, 1, Width - 3, Height - 3);
        var rr = hover && Enabled ? r : new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        using var caminho = Hud.Redondo(rr, Math.Min(12, Height / 2f));

        if (Cheio)
        {
            int a = apertado ? 255 : hover ? 250 : 225;
            using var fundo = new LinearGradientBrush(rr, Hud.Alfa(ControlPaint.Light(cor, 0.25f), a), Hud.Alfa(ControlPaint.Dark(cor, 0.12f), a), 90f);
            g.FillPath(fundo, caminho);
        }
        else
        {
            using var fundo = new SolidBrush(Hud.Alfa(cor, apertado ? 70 : hover ? 40 : 16));
            g.FillPath(fundo, caminho);
        }
        if (hover && Enabled)
            using (var brilho = new Pen(Hud.Alfa(cor, 45), 5)) g.DrawPath(brilho, caminho);
        if (!Cheio)
            using (var borda = new Pen(Hud.Alfa(cor, hover ? 200 : 90), 1.2f)) g.DrawPath(borda, caminho);

        var corTexto = Cheio ? Color.FromArgb(4, 16, 28) : (Enabled ? (hover ? Color.White : ForeColor) : Tema.Apagado);
        string txt = (Icone != null ? Icone + "  " : "") + Text;
        TextRenderer.DrawText(g, txt, Font, Rectangle.Round(r), corTexto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>
/// O núcleo holográfico: anéis girando que mostram CPU (fora), RAM (meio) e GPU (dentro).
/// Centro = nota de otimização. Passar o mouse num anel mostra o valor; clicar no centro otimiza.
/// </summary>
internal class Nucleo : Control
{
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 33 };
    private float cpu, ram, gpu, cpuV, ramV, gpuV; // alvo e valor exibido (suavizado)
    private float a1, a2, a3, aTicks, varredura;
    private int anelHover = -1;
    private bool centroHover;
    private readonly List<(float raio, float alfa)> pulsos = new();
    private readonly Random rnd = new();
    private readonly List<(float ang, float dist, float vel, float vida)> particulas = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Nota { get; set; } = -1;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Otimizando { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Progresso { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TextoTopo { get; set; } = "OTIMIZADO";

    public event EventHandler CliqueCentro;

    public Nucleo()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Size = new Size(360, 360);
        anim.Tick += (_, _) => Passo();
        VisibleChanged += (_, _) => { if (Visible) anim.Start(); else anim.Stop(); };
        anim.Start();
    }

    public void Definir(float cpu, float ram, float gpu)
    {
        this.cpu = Math.Max(0, cpu); this.ram = Math.Max(0, ram); this.gpu = Math.Max(0, gpu);
    }

    /// <summary>Onda de pulso (quando termina de otimizar).</summary>
    public void Pulsar()
    {
        pulsos.Add((Width * 0.22f, 1f));
        pulsos.Add((Width * 0.16f, 1f));
        for (int i = 0; i < 40; i++)
            particulas.Add(((float)(rnd.NextDouble() * 360), Width * 0.2f, 2f + (float)rnd.NextDouble() * 3, 1f));
    }

    private void Passo()
    {
        var form = FindForm();
        // economiza: devagar quando o app não está em foco, parado quando minimizado
        if (form == null || form.WindowState == FormWindowState.Minimized || !Visible) return;
        anim.Interval = Form.ActiveForm == form ? 33 : 200;

        float vel = Otimizando ? 5f : 1f;
        a1 = (a1 + 0.35f * vel) % 360;
        a2 = (a2 - 0.55f * vel + 360) % 360;
        a3 = (a3 + 0.8f * vel) % 360;
        aTicks = (aTicks + 0.12f * vel) % 360;
        varredura = (varredura + 1.6f) % (Height * 1.4f);
        cpuV += (cpu - cpuV) * 0.08f;
        ramV += (ram - ramV) * 0.08f;
        gpuV += (gpu - gpuV) * 0.08f;

        for (int i = pulsos.Count - 1; i >= 0; i--)
        {
            var (r, a) = pulsos[i];
            r += 4.5f; a -= 0.025f;
            if (a <= 0) pulsos.RemoveAt(i); else pulsos[i] = (r, a);
        }
        for (int i = particulas.Count - 1; i >= 0; i--)
        {
            var (ang, d, v, vida) = particulas[i];
            d += v; vida -= 0.02f;
            if (vida <= 0) particulas.RemoveAt(i); else particulas[i] = (ang, d, v, vida);
        }
        // partícula ambiente de vez em quando
        if (rnd.NextDouble() < 0.08)
            particulas.Add(((float)(rnd.NextDouble() * 360), Width * 0.12f, 0.4f + (float)rnd.NextDouble() * 0.6f, 0.9f));
        Invalidate();
    }

    private PointF Centro => new(Width / 2f, Height / 2f);
    private float R(float frac) => Math.Min(Width, Height) * frac;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        float dx = e.X - Centro.X, dy = e.Y - Centro.Y;
        float d = (float)Math.Sqrt(dx * dx + dy * dy);
        int anel = -1;
        if (Math.Abs(d - R(0.43f)) < 12) anel = 0;
        else if (Math.Abs(d - R(0.36f)) < 12) anel = 1;
        else if (Math.Abs(d - R(0.29f)) < 12) anel = 2;
        bool centro = d < R(0.24f);
        if (anel != anelHover || centro != centroHover)
        {
            anelHover = anel; centroHover = centro;
            Cursor = centro ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { anelHover = -1; centroHover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (centroHover && !Otimizando) CliqueCentro?.Invoke(this, EventArgs.Empty);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var c = Centro;

        // ---------- halo de fundo ----------
        using (var cam = new GraphicsPath())
        {
            float rh = R(0.5f);
            cam.AddEllipse(c.X - rh, c.Y - rh, rh * 2, rh * 2);
            using var br = new PathGradientBrush(cam) { CenterColor = Color.FromArgb(Otimizando ? 70 : 40, 0, 160, 220), SurroundColors = new[] { Color.Transparent } };
            g.FillPath(br, cam);
        }

        // ---------- marcações girando (anel externo) ----------
        float rt = R(0.485f);
        using (var p1 = new Pen(Hud.Alfa(Hud.Ciano, 90), 1))
        using (var p2 = new Pen(Hud.Alfa(Hud.Ciano, 170), 1.6f))
            for (int i = 0; i < 72; i++)
            {
                double a = (aTicks + i * 5) * Math.PI / 180;
                bool longo = i % 6 == 0;
                float r0 = rt - (longo ? 9 : 4);
                g.DrawLine(longo ? p2 : p1,
                    c.X + r0 * (float)Math.Cos(a), c.Y + r0 * (float)Math.Sin(a),
                    c.X + rt * (float)Math.Cos(a), c.Y + rt * (float)Math.Sin(a));
            }

        // ---------- os 3 anéis de dados ----------
        DesenharAnel(g, R(0.43f), cpuV, a1, 0, "CPU");
        DesenharAnel(g, R(0.36f), ramV, a2, 1, "RAM");
        DesenharAnel(g, R(0.29f), gpuV, a3, 2, "GPU");

        // ---------- disco central ----------
        float rc = R(0.235f);
        using (var cam = new GraphicsPath())
        {
            cam.AddEllipse(c.X - rc, c.Y - rc, rc * 2, rc * 2);
            using var br = new PathGradientBrush(cam)
            {
                CenterColor = Color.FromArgb(centroHover ? 120 : 80, 0, 110, 160),
                SurroundColors = new[] { Color.FromArgb(200, 6, 12, 22) }
            };
            g.FillPath(br, cam);

            // linha de varredura dentro do disco
            var reg = g.Clip;
            g.SetClip(cam);
            float yv = c.Y - rc + (varredura % (rc * 2.6f)) - rc * 0.3f;
            using (var sc = new LinearGradientBrush(new RectangleF(c.X - rc, yv - 10, rc * 2, 20), Color.Transparent, Hud.Alfa(Hud.Ciano, 70), 90f))
            {
                sc.SetBlendTriangularShape(0.5f);
                g.FillRectangle(sc, c.X - rc, yv - 10, rc * 2, 20);
            }
            g.Clip = reg;
        }
        using (var borda = new Pen(Hud.Alfa(Hud.Ciano, centroHover ? 255 : 150), centroHover ? 2.2f : 1.4f))
            g.DrawEllipse(borda, c.X - rc, c.Y - rc, rc * 2, rc * 2);
        using (var tracejado = new Pen(Hud.Alfa(Hud.Ciano, 90), 1) { DashPattern = new[] { 2f, 4f } })
            g.DrawEllipse(tracejado, c.X - rc + 7, c.Y - rc + 7, rc * 2 - 14, rc * 2 - 14);

        // ---------- texto do centro ----------
        string grande, pequeno;
        Color corGrande = Hud.CianoClaro;
        if (Otimizando) { grande = $"{Progresso}%"; pequeno = "OTIMIZANDO"; }
        else if (anelHover >= 0)
        {
            float v = anelHover == 0 ? cpuV : anelHover == 1 ? ramV : gpuV;
            grande = $"{v:0}%";
            pequeno = anelHover == 0 ? "PROCESSADOR" : anelHover == 1 ? "MEMÓRIA RAM" : "PLACA DE VÍDEO";
            corGrande = Hud.CorNivel(v);
        }
        else if (centroHover) { grande = "⚡"; pequeno = "CLIQUE P/ OTIMIZAR"; }
        else { grande = Nota >= 0 ? $"{Nota}%" : "--"; pequeno = TextoTopo; corGrande = Nota >= 90 ? Tema.Verde : Nota >= 70 ? Hud.CianoClaro : Tema.Amarelo; }

        using (var fg = Hud.Leve(rc * 0.44f))
        using (var fp = Hud.Titulo(Math.Max(7f, rc * 0.095f), FontStyle.Bold))
        {
            var tg = g.MeasureString(grande, fg);
            using var bg = new SolidBrush(corGrande);
            g.DrawString(grande, fg, bg, c.X - tg.Width / 2, c.Y - tg.Height / 2 - rc * 0.08f);
            var tp = g.MeasureString(pequeno, fp);
            using var bp = new SolidBrush(Hud.Alfa(Hud.Ciano, 200));
            g.DrawString(pequeno, fp, bp, c.X - tp.Width / 2, c.Y + rc * 0.36f);
        }

        // ---------- pulsos e partículas ----------
        foreach (var (r, a) in pulsos)
            using (var p = new Pen(Hud.Alfa(Hud.CianoClaro, (int)(a * 200)), 2))
                g.DrawEllipse(p, c.X - r, c.Y - r, r * 2, r * 2);
        foreach (var (ang, d, _, vida) in particulas)
        {
            double a = ang * Math.PI / 180;
            using var b = new SolidBrush(Hud.Alfa(Hud.CianoClaro, (int)(vida * 200)));
            g.FillEllipse(b, c.X + d * (float)Math.Cos(a) - 1.5f, c.Y + d * (float)Math.Sin(a) - 1.5f, 3, 3);
        }
    }

    private void DesenharAnel(Graphics g, float r, float valor, float rot, int indice, string nome)
    {
        var c = Centro;
        var ret = new RectangleF(c.X - r, c.Y - r, r * 2, r * 2);
        bool destaque = anelHover == indice;
        var cor = Hud.CorNivel(valor);

        using (var trilho = new Pen(Hud.Alfa(Hud.Ciano, destaque ? 50 : 26), 7)) g.DrawEllipse(trilho, ret);

        float varrer = Math.Max(2, 360 * Math.Clamp(valor, 0, 100) / 100f);
        using (var brilho = new Pen(Hud.Alfa(cor, destaque ? 90 : 45), destaque ? 15 : 12) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(brilho, ret, rot, varrer);
        using (var arco = new Pen(Hud.Alfa(cor, 235), destaque ? 7 : 5) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(arco, ret, rot, varrer);

        // segmentos decorativos girando ao contrário
        using (var seg = new Pen(Hud.Alfa(Hud.Ciano, 70), 1.2f) { DashPattern = new[] { 1f, 6f } })
            g.DrawArc(seg, new RectangleF(ret.X - 9, ret.Y - 9, ret.Width + 18, ret.Height + 18), -rot * 0.7f, 120);

        // nome do anel no começo do arco
        double a = rot * Math.PI / 180;
        using var f = Hud.Titulo(7f, FontStyle.Bold);
        using var b = new SolidBrush(Hud.Alfa(cor, destaque ? 255 : 170));
        g.DrawString(nome, f, b, c.X + (r + 10) * (float)Math.Cos(a) - 9, c.Y + (r + 10) * (float)Math.Sin(a) - 6);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) anim.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Sequência de inicialização (~2,5 s). Um clique pula.</summary>
internal class BootHud : Control
{
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 30 };
    private readonly DateTime inicio = DateTime.Now;
    private readonly string[] linhas;
    private const double Duracao = 2.6;
    public event EventHandler Terminou;
    private bool acabou;

    public BootHud(string[] linhas)
    {
        this.linhas = linhas;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        Dock = DockStyle.Fill;
        Cursor = Cursors.Hand;
        anim.Tick += (_, _) =>
        {
            Invalidate();
            if ((DateTime.Now - inicio).TotalSeconds > Duracao + 0.5) Fim();
        };
        Click += (_, _) => Fim();
        anim.Start();
    }

    private void Fim()
    {
        if (acabou) return;
        acabou = true;
        anim.Stop();
        Terminou?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.DrawImageUnscaled(Hud.FundoGrade(Size), 0, 0);

        double t = (DateTime.Now - inicio).TotalSeconds;
        float p = (float)Math.Clamp(t / Duracao, 0, 1);
        float fade = t > Duracao ? (float)Math.Clamp(1 - (t - Duracao) / 0.5, 0, 1) : 1;
        var c = new PointF(Width / 2f, Height / 2f - 40);

        // anéis se montando
        for (int i = 0; i < 4; i++)
        {
            float r = 70 + i * 26;
            float prog = Math.Clamp(p * 1.6f - i * 0.15f, 0, 1);
            float rot = (float)(t * (i % 2 == 0 ? 90 : -70) + i * 40);
            using var pen = new Pen(Hud.Alfa(Hud.Ciano, (int)(220 * fade)), i == 0 ? 4 : 2);
            g.DrawArc(pen, c.X - r, c.Y - r, r * 2, r * 2, rot, 360 * prog);
        }
        using (var nucleo = new SolidBrush(Hud.Alfa(Hud.Ciano, (int)(60 * p * fade))))
            g.FillEllipse(nucleo, c.X - 55, c.Y - 55, 110, 110);

        // nome
        using (var f = Hud.Titulo(26, FontStyle.Bold))
        {
            string nome = "SEXTA-FEIRA";
            int letras = (int)Math.Ceiling(nome.Length * Math.Clamp(p * 2.2f, 0, 1));
            var txt = nome[..letras];
            var tam = g.MeasureString(nome, f);
            using var b = new SolidBrush(Hud.Alfa(Hud.CianoClaro, (int)(255 * fade)));
            g.DrawString(txt, f, b, c.X - tam.Width / 2, c.Y + 130);
        }

        // linhas de status digitando
        using (var f = Hud.Mono(10))
        {
            float y = c.Y + 180;
            for (int i = 0; i < linhas.Length; i++)
            {
                float ini = 0.25f + i * 0.17f;
                float prog = Math.Clamp((p - ini) / 0.15f, 0, 1);
                if (prog <= 0) continue;
                string s = "> " + linhas[i];
                s = s[..(int)Math.Ceiling(s.Length * prog)];
                var tam = g.MeasureString("> " + linhas[i], f);
                using var b = new SolidBrush(Hud.Alfa(i == linhas.Length - 1 ? Tema.Verde : Hud.Ciano, (int)(220 * fade)));
                g.DrawString(s, f, b, c.X - tam.Width / 2, y);
                y += 20;
            }
        }

        using (var f = Hud.Titulo(8))
        using (var b = new SolidBrush(Hud.Alfa(Tema.Apagado, (int)(180 * fade))))
            g.DrawString("clique para pular", f, b, Width - 120, Height - 26);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) anim.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Lista "rótulo · valor" alinhada sem precisar de fonte monoespaçada.</summary>
internal class ListaHud : Control
{
    private List<(string rotulo, string valor)> linhas = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ValorADireita { get; set; } = true;   // true: valor colado na direita; false: valor numa coluna fixa
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ColunaValor { get; set; } = 46;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int AlturaLinha { get; set; } = 24;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CorRotulo { get; set; } = Tema.Texto;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CorValor { get; set; } = Tema.Apagado;

    public ListaHud()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Font = new Font(Hud.FonteTexto, 9f);
    }

    public void Definir(IEnumerable<(string rotulo, string valor)> l)
    {
        linhas = l.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using var forte = new Font(Hud.FonteForte, Font.Size);
        int y = 0;
        for (int i = 0; i < linhas.Count; i++)
        {
            var (rot, val) = linhas[i];
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
            if (ValorADireita)
            {
                var tv = TextRenderer.MeasureText(val, Font, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, rot, Font, new Rectangle(0, y, Width - tv.Width - 10, AlturaLinha), CorRotulo, flags);
                TextRenderer.DrawText(g, val, forte, new Rectangle(Width - tv.Width - 4, y, tv.Width + 4, AlturaLinha), CorValor, flags | TextFormatFlags.Right);
            }
            else
            {
                TextRenderer.DrawText(g, rot, forte, new Rectangle(0, y, ColunaValor, AlturaLinha), CorValor, flags);
                TextRenderer.DrawText(g, val, Font, new Rectangle(ColunaValor, y, Width - ColunaValor, AlturaLinha), CorRotulo, flags);
            }
            if (i < linhas.Count - 1)
                using (var p = new Pen(Color.FromArgb(14, 255, 255, 255))) g.DrawLine(p, 0, y + AlturaLinha + 1, Width, y + AlturaLinha + 1);
            y += AlturaLinha + 3;
        }
    }
}

/// <summary>Item do menu lateral: fundo arredondado e um tracinho de destaque quando selecionado.</summary>
internal class BotaoMenuHud : Button
{
    private bool hover;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selecionado { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Recuo { get; set; } = 14;

    public BotaoMenuHud()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
    }

    protected override bool ShowFocusCues => false;
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Tema.Lateral);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(2, 2, Width - 5, Height - 5);
        if (Selecionado || hover)
        {
            using var caminho = Hud.Redondo(r, 10);
            using var fundo = Selecionado
                ? (Brush)new LinearGradientBrush(r, Hud.Alfa(Hud.Ciano, 40), Hud.Alfa(Hud.Ciano, 8), 0f)
                : new SolidBrush(Color.FromArgb(10, 255, 255, 255));
            g.FillPath(fundo, caminho);
        }
        if (Selecionado)
            using (var traco = Hud.Redondo(new RectangleF(2, Height / 2f - 9, 3.5f, 18), 2))
            using (var b = new SolidBrush(Hud.Ciano))
                g.FillPath(b, traco);

        var cor = Selecionado ? Color.White : hover ? Tema.Texto : ForeColor;
        TextRenderer.DrawText(g, Text, Font, new Rectangle(Recuo, 0, Width - Recuo - 4, Height), cor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

/// <summary>Símbolo da SEXTA-FEIRA no menu: um núcleo pequeno com dois arcos girando devagar.</summary>
internal class LogoNucleo : Control
{
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 50 };
    private float ang;

    public LogoNucleo()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Size = new Size(34, 34);
        anim.Tick += (_, _) =>
        {
            var f = FindForm();
            if (f == null || f.WindowState == FormWindowState.Minimized) return;
            ang = (ang + 4) % 360;
            Invalidate();
        };
        anim.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float cx = Width / 2f, cy = Height / 2f, r = Width / 2f - 3;

        using (var aura = new GraphicsPath())
        {
            aura.AddEllipse(cx - r, cy - r, r * 2, r * 2);
            using var br = new PathGradientBrush(aura) { CenterColor = Hud.Alfa(Hud.Ciano, 90), SurroundColors = new[] { Color.Transparent } };
            g.FillPath(br, aura);
        }
        using (var p = new Pen(Hud.Alfa(Hud.Ciano, 230), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawArc(p, cx - r, cy - r, r * 2, r * 2, ang, 110);
            g.DrawArc(p, cx - r, cy - r, r * 2, r * 2, ang + 180, 110);
        }
        float r2 = r - 5;
        using (var p = new Pen(Hud.Alfa(Hud.CianoClaro, 170), 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(p, cx - r2, cy - r2, r2 * 2, r2 * 2, -ang * 1.5f, 200);
        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - 2.5f, cy - 2.5f, 5, 5);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) anim.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Console da SEXTA-FEIRA: uma cápsula que vai "digitando" o que ela está fazendo ou percebeu no PC.
/// </summary>
internal class ConsoleHud : Control
{
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 28 };
    private readonly Queue<string> fila = new();
    private string atual = "", alvo = "";
    private int tick;
    private int pausa; // ticks parado mostrando a frase inteira

    public ConsoleHud()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Font = new Font(Hud.FonteTexto, 9.5f);
        Size = new Size(600, 34);
        anim.Tick += (_, _) => Passo();
        anim.Start();
    }

    /// <summary>Fala algo agora (passa na frente).</summary>
    public void Dizer(string frase, bool urgente = false)
    {
        if (string.IsNullOrWhiteSpace(frase)) return;
        if (urgente) { fila.Clear(); alvo = frase; atual = ""; pausa = 0; return; }
        if (fila.Count < 4 && frase != alvo && !fila.Contains(frase)) fila.Enqueue(frase);
    }

    public bool Livre => fila.Count == 0 && atual == alvo && pausa > 150;

    private void Passo()
    {
        var f = FindForm();
        if (f == null || f.WindowState == FormWindowState.Minimized || !Visible) return;
        tick++;
        if (atual.Length < alvo.Length)
        {
            atual = alvo[..Math.Min(alvo.Length, atual.Length + 2)];
            Invalidate();
            return;
        }
        pausa++;
        if (pausa > 110 && fila.Count > 0) { alvo = fila.Dequeue(); atual = ""; pausa = 0; }
        if (tick % 18 == 0) Invalidate(); // cursor piscando
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(1, 1, Width - 3, Height - 3);
        using (var caminho = Hud.Redondo(r, r.Height / 2))
        {
            using var fundo = new SolidBrush(Color.FromArgb(170, 10, 18, 32));
            g.FillPath(fundo, caminho);
            using var borda = new Pen(Hud.Alfa(Hud.Ciano, 60), 1);
            g.DrawPath(borda, caminho);
        }
        // pontinho "vivo" pulsando
        float pulso = (float)(0.5 + 0.5 * Math.Sin(tick / 8.0));
        float cy = Height / 2f;
        using (var aura = new SolidBrush(Hud.Alfa(Hud.Ciano, (int)(40 + 50 * pulso)))) g.FillEllipse(aura, 12, cy - 7, 14, 14);
        using (var ponto = new SolidBrush(Hud.Ciano)) g.FillEllipse(ponto, 16, cy - 3, 6, 6);

        using var forte = new Font(Hud.FonteForte, Font.Size);
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        const string nome = "SEXTA-FEIRA  ›";
        var tn = TextRenderer.MeasureText(nome, forte, Size.Empty, flags);
        TextRenderer.DrawText(g, nome, forte, new Rectangle(34, 0, tn.Width + 2, Height), Hud.Ciano, flags);

        int x = 34 + tn.Width + 8;
        var area = new Rectangle(x, 0, Width - x - 18, Height);
        TextRenderer.DrawText(g, atual, Font, area, Tema.Texto, flags | TextFormatFlags.EndEllipsis);
        bool cursorAceso = atual.Length < alvo.Length || (tick / 18) % 2 == 0;
        if (cursorAceso)
        {
            var tt = TextRenderer.MeasureText(g, atual, Font, area.Size, flags);
            float cx = Math.Min(area.Right - 6, x + tt.Width + 3);
            using var b = new SolidBrush(Hud.Alfa(Hud.CianoClaro, 220));
            g.FillRectangle(b, cx, cy - 7, 7, 14);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) anim.Dispose();
        base.Dispose(disposing);
    }
}
