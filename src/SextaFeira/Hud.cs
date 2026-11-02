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

    private static FontFamily familiaTitulo;
    public static FontFamily FamiliaTitulo => familiaTitulo ??= Existe("Bahnschrift") ? new FontFamily("Bahnschrift") : new FontFamily("Segoe UI");
    public static string FonteMono => Existe("Cascadia Mono") ? "Cascadia Mono" : "Consolas";

    public static Font Titulo(float tam, FontStyle s = FontStyle.Regular) => new(FamiliaTitulo, tam, s);
    public static Font Mono(float tam) => new(FonteMono, tam);

    private static bool Existe(string nome)
    {
        try { using var f = new FontFamily(nome); return true; } catch { return false; }
    }

    public static Color Alfa(Color c, int a) => Color.FromArgb(Math.Clamp(a, 0, 255), c);

    public static Color CorNivel(float v) => v >= 90 ? Tema.Vermelho : v >= 70 ? Tema.Amarelo : Ciano;

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
        using var pen = new Pen(Color.FromArgb(16, 0, 209, 255), 1);
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

/// <summary>Página com fundo holográfico. Filhos transparentes mostram a grade por trás.</summary>
internal class PainelHud : Panel
{
    public PainelHud()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Hud.Fundo;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;
        e.Graphics.DrawImageUnscaled(Hud.FundoGrade(Size), 0, 0);
    }
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
        using (var caminho = Hud.Chanfrado(r, 10))
        {
            using var fundo = new SolidBrush(Hud.Painel);
            g.FillPath(fundo, caminho);
            using var borda = new Pen(Hud.Alfa(CorDestaque, 55), 1);
            g.DrawPath(borda, caminho);
        }
        Hud.Colchetes(g, new RectangleF(4, 4, Width - 9, Height - 9), Hud.Alfa(CorDestaque, 200), 10, 1.6f);

        if (!string.IsNullOrEmpty(Titulo))
        {
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using var f = Hud.Titulo(8.5f, FontStyle.Bold);
            using var b = new SolidBrush(Hud.Alfa(CorDestaque, 220));
            g.FillRectangle(b, 16, 17, 3, 11);
            g.DrawString(Titulo.ToUpperInvariant(), f, b, 23, 14);
            using var lin = new Pen(Hud.Alfa(CorDestaque, 40), 1);
            g.DrawLine(lin, 16, 36, Width - 16, 36);
        }
    }
}

/// <summary>Botão com cantos cortados, brilho no hover e "acender" ao clicar.</summary>
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
        using var caminho = Hud.Chanfrado(r, Math.Min(12, Height / 3f));

        int alfaFundo = Cheio ? (apertado ? 255 : hover ? 235 : 210) : (apertado ? 90 : hover ? 60 : 28);
        using (var fundo = new SolidBrush(Hud.Alfa(cor, alfaFundo))) g.FillPath(fundo, caminho);
        if (hover && Enabled)
            using (var brilho = new Pen(Hud.Alfa(cor, 60), 6)) g.DrawPath(brilho, caminho);
        using (var borda = new Pen(Hud.Alfa(cor, hover ? 255 : 170), 1.4f)) g.DrawPath(borda, caminho);

        var corTexto = Cheio ? Color.FromArgb(6, 12, 20) : (Enabled ? (hover ? Hud.CianoClaro : ForeColor) : Tema.Apagado);
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

        using (var fg = Hud.Titulo(rc * 0.42f, FontStyle.Bold))
        using (var fp = Hud.Titulo(Math.Max(7f, rc * 0.1f), FontStyle.Bold))
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
