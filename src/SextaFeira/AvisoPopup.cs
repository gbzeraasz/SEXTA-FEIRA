using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace SextaFeira;

/// <summary>
/// Aviso holográfico da SEXTA-FEIRA no canto superior direito.
/// - Transparência real nas bordas (brilho), painel firme por trás do texto pra ler fácil.
/// - Acompanha a escala do Windows (100%, 125%, 150%...): nada corta.
/// - Texto quebra linha em vez de cortar.
/// - "Se projeta" a partir de uma linha de luz e se recolhe do mesmo jeito.
/// Não rouba o foco do jogo.
/// </summary>
internal class AvisoPopup : Form
{
    // ---------- Win32: janela com transparência por pixel ----------
    [StructLayout(LayoutKind.Sequential)] private struct PONTO { public int x, y; public PONTO(int x, int y) { this.x = x; this.y = y; } }
    [StructLayout(LayoutKind.Sequential)] private struct TAMANHO { public int cx, cy; public TAMANHO(int cx, int cy) { this.cx = cx; this.cy = cy; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BLEND { public byte Op, Flags, Alpha, Format; }
    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref PONTO pptDst, ref TAMANHO psize, IntPtr hdcSrc, ref PONTO pprSrc, int crKey, ref BLEND pblend, int dwFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    // ---------- medidas em "pixels de 100%" (multiplicadas pela escala da tela) ----------
    private const float Larg = 380, Margem = 16, Brilho = 10;   // Brilho: borda transparente pro glow
    private const float LogoTam = 54, XTexto = Brilho + 16 + LogoTam + 16;
    private float escala = 1;
    private float altLogica = 96;

    // ---------- estado ----------
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 16 };
    private readonly Random rnd = new();
    private int fase;
    private float t;
    private int msParado, tempoNaTela = 5000;
    private string cabecalho = "SEXTA-FEIRA";
    private string titulo, texto;
    private bool erro, trabalhando, concluido;
    private int progresso = -1;
    private float progressoVisivel, giro, pulso;
    private Point posTela;

    // fontes em pixel (a escala é aplicada no desenho)
    private readonly Font fCab = new(Hud.FonteForte, 10.5f, GraphicsUnit.Pixel);
    private readonly Font fTitulo = new(Hud.FonteForte, 16f, GraphicsUnit.Pixel);
    private readonly Font fTexto = new(Hud.FonteTexto, 13f, GraphicsUnit.Pixel);

    private AvisoPopup(string titulo, string texto, bool erro, bool trabalhando)
    {
        this.titulo = titulo; this.texto = texto; this.erro = erro; this.trabalhando = trabalhando;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        try { using var g = Graphics.FromHwnd(IntPtr.Zero); escala = Math.Max(1f, g.DpiX / 96f); } catch { }
        Ajustar();
        MouseClick += (_, _) => { if (!this.trabalhando && fase == 1) fase = 2; };
        anim.Tick += Animar;
        Shown += (_, _) => { Desenhar(); anim.Start(); };
    }

    private float LargTexto => Larg - XTexto - Brilho - 16;

    private static float Medir(string s, Font f, float largura)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        using var bmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        return (float)Math.Ceiling(g.MeasureString(s, f, (int)largura).Height);
    }

    private void Ajustar()
    {
        float h = Brilho + 14 + 14 + Medir(titulo, fTitulo, LargTexto) + 3 + Medir(texto, fTexto, LargTexto)
                  + (progresso >= 0 ? 22 : 14) + Brilho;
        altLogica = Math.Max(h, Brilho * 2 + LogoTam + 28);
        var area = Screen.PrimaryScreen!.WorkingArea;
        int w = (int)Math.Ceiling(Larg * escala), hh = (int)Math.Ceiling(altLogica * escala);
        posTela = new Point(area.Right - w - (int)(Margem * escala) + (int)(Brilho * escala), area.Top + (int)((Margem - Brilho) * escala));
        Bounds = new Rectangle(posTela, new Size(w, hh));
    }

    private static float Suave(float x) => 1 - (float)Math.Pow(1 - Math.Clamp(x, 0, 1), 3);

    private void Animar(object sender, EventArgs e)
    {
        giro = (giro + (trabalhando ? 6f : 1.5f)) % 360;
        pulso += 0.09f;
        if (progresso >= 0) progressoVisivel += (progresso - progressoVisivel) * 0.15f;
        switch (fase)
        {
            case 0: t = Math.Min(1, t + 0.05f); if (t >= 1) fase = 1; break;
            case 1: if (!trabalhando && (msParado += anim.Interval) >= tempoNaTela) fase = 2; break;
            default:
                t = Math.Max(0, t - 0.06f);
                if (t <= 0) { anim.Stop(); Close(); return; }
                break;
        }
        Desenhar();
    }

    private void Desenhar()
    {
        if (!IsHandleCreated) return;
        int w = (int)Math.Ceiling(Larg * escala), h = (int)Math.Ceiling(altLogica * escala);
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.ScaleTransform(escala, escala);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // projeção: abre na vertical a partir do meio
            float abre = Suave(fase == 2 ? t : t * 1.15f);
            float hVis = Math.Max(2, altLogica * abre), y0 = (altLogica - hVis) / 2f;
            g.SetClip(new RectangleF(0, y0, Larg, hVis));
            Conteudo(g);
            g.ResetClip();
            if (abre < 0.98f)
            {
                float lw = (Larg - Brilho * 2) * Math.Min(1, abre * 3 + 0.2f), lx = (Larg - lw) / 2;
                using var luz = new LinearGradientBrush(new RectangleF(lx, 0, lw, 2), Color.Transparent, Color.Transparent, 0f)
                {
                    InterpolationColors = new ColorBlend { Colors = new[] { Color.Transparent, Color.FromArgb(240, 190, 248, 255), Color.Transparent }, Positions = new[] { 0f, 0.5f, 1f } }
                };
                g.FillRectangle(luz, lx, y0 - 1, lw, 2);
                g.FillRectangle(luz, lx, y0 + hVis - 1, lw, 2);
            }
        }
        byte alfa = (byte)Math.Clamp((fase == 0 && t < 0.5f ? rnd.Next(75, 100) / 100f : 1f) * Math.Min(1f, t * 1.6f) * 255, 0, 255);
        Aplicar(bmp, alfa);
    }

    private void Conteudo(Graphics g)
    {
        var cor = erro ? Color.FromArgb(255, 90, 90) : concluido ? Color.FromArgb(70, 235, 200) : Color.FromArgb(0, 210, 255);
        var r = new RectangleF(Brilho, Brilho, Larg - Brilho * 2, altLogica - Brilho * 2);

        // brilho externo (o "holograma" vazando pra fora do painel)
        for (int i = 4; i >= 1; i--)
            using (var p = Hud.Redondo(new RectangleF(r.X - i * 2, r.Y - i * 2, r.Width + i * 4, r.Height + i * 4), 14 + i * 2))
            using (var b = new SolidBrush(Hud.Alfa(cor, 9)))
                g.FillPath(b, p);

        using (var caminho = Hud.Redondo(r, 14))
        {
            // painel firme (pra ler fácil) com degradê
            using (var fundo = new LinearGradientBrush(r, Color.FromArgb(242, 9, 20, 36), Color.FromArgb(232, 5, 11, 22), 90f))
                g.FillPath(fundo, caminho);
            // brilho do lado do logo
            using (var lado = new LinearGradientBrush(r, Hud.Alfa(cor, 38), Color.Transparent, 0f))
            {
                lado.SetBlendTriangularShape(0f, 1f);
                g.FillPath(lado, caminho);
            }
            // linhas de varredura bem fracas
            g.SetClip(caminho, CombineMode.Intersect);
            using (var scan = new Pen(Color.FromArgb(10, cor.R, cor.G, cor.B)))
                for (float ys = r.Y + 2; ys < r.Bottom; ys += 3) g.DrawLine(scan, r.X, ys, r.Right, ys);
            g.ResetClip();
            using (var borda = new Pen(Hud.Alfa(cor, 130), 1.2f)) g.DrawPath(borda, caminho);
        }
        // reflexo no topo + cantoneiras
        using (var reflexo = new LinearGradientBrush(new RectangleF(r.X + 20, r.Y, r.Width - 40, 1), Color.Transparent, Color.Transparent, 0f)
        { InterpolationColors = new ColorBlend { Colors = new[] { Color.Transparent, Hud.Alfa(cor, 230), Color.Transparent }, Positions = new[] { 0f, 0.35f, 1f } } })
            g.FillRectangle(reflexo, r.X + 20, r.Y, r.Width - 40, 1.2f);
        using (var canto = new Pen(Hud.Alfa(cor, 235), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            float c = 14;
            g.DrawLines(canto, new[] { new PointF(r.X + 0.5f, r.Y + c), new PointF(r.X + 0.5f, r.Y + 4), new PointF(r.X + 4, r.Y + 0.5f), new PointF(r.X + c, r.Y + 0.5f) });
            g.DrawLines(canto, new[] { new PointF(r.Right - 0.5f, r.Bottom - c), new PointF(r.Right - 0.5f, r.Bottom - 4), new PointF(r.Right - 4, r.Bottom - 0.5f), new PointF(r.Right - c, r.Bottom - 0.5f) });
        }

        // logo com aura e arcos girando
        float cx = Brilho + 16 + LogoTam / 2, cy = r.Y + Math.Min(r.Height / 2, 44), raio = LogoTam / 2;
        using (var aura = new GraphicsPath())
        {
            aura.AddEllipse(cx - raio - 10, cy - raio - 10, (raio + 10) * 2, (raio + 10) * 2);
            using var br = new PathGradientBrush(aura) { CenterColor = Hud.Alfa(cor, (int)(80 + 35 * Math.Sin(pulso))), SurroundColors = new[] { Color.Transparent } };
            g.FillPath(br, aura);
        }
        float lado2 = LogoTam - 8;
        g.DrawImage(Tema.Logo, cx - lado2 / 2, cy - lado2 / 2, lado2, lado2);
        using (var arco = new Pen(Hud.Alfa(cor, 245), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            if (concluido || erro) g.DrawEllipse(arco, cx - raio, cy - raio, raio * 2, raio * 2);
            else for (int k = 0; k < 3; k++) g.DrawArc(arco, cx - raio, cy - raio, raio * 2, raio * 2, giro + k * 120, 55);
        }
        if (concluido || erro)
        {
            float sx = cx + raio * 0.72f, sy = cy + raio * 0.72f, sr = 9;
            using (var b = new SolidBrush(cor)) g.FillEllipse(b, sx - sr, sy - sr, sr * 2, sr * 2);
            using var traco = new Pen(Color.FromArgb(6, 14, 24), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            if (erro) { g.DrawLine(traco, sx, sy - 4.5f, sx, sy + 1); g.DrawLine(traco, sx, sy + 4, sx, sy + 4.3f); }
            else g.DrawLines(traco, new[] { new PointF(sx - 4.5f, sy), new PointF(sx - 1.2f, sy + 3.3f), new PointF(sx + 4.5f, sy - 3.3f) });
        }

        // textos (quebram linha, não cortam)
        float y = r.Y + 12;
        using (var b = new SolidBrush(Hud.Alfa(cor, 255))) g.DrawString(cabecalho, fCab, b, XTexto, y);
        y += 15;
        float hT = Medir(titulo, fTitulo, LargTexto);
        using (var brilhoTxt = new SolidBrush(Hud.Alfa(cor, 60)))
            g.DrawString(titulo, fTitulo, brilhoTxt, new RectangleF(XTexto + 0.7f, y + 0.7f, LargTexto, hT + 4));
        using (var branco = new SolidBrush(Color.FromArgb(255, 244, 252, 255)))
            g.DrawString(titulo, fTitulo, branco, new RectangleF(XTexto, y, LargTexto, hT + 4));
        y += hT + 3;
        if (!string.IsNullOrEmpty(texto))
        {
            float hX = Medir(texto, fTexto, LargTexto);
            using var cinza = new SolidBrush(Color.FromArgb(255, 176, 210, 230));
            g.DrawString(texto, fTexto, cinza, new RectangleF(XTexto, y, LargTexto, hX + 4));
        }

        // progresso
        if (progresso >= 0)
        {
            float py = r.Bottom - 12, x0 = XTexto, wl = LargTexto - 40;
            using (var trilho = new Pen(Color.FromArgb(45, cor.R, cor.G, cor.B), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(trilho, x0, py, x0 + wl, py);
            float wp = Math.Max(2, wl * Math.Clamp(progressoVisivel, 0, 100) / 100f);
            using (var cheio = new Pen(Hud.Alfa(cor, 245), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(cheio, x0, py, x0 + wp, py);
            using (var cabeca = new SolidBrush(Hud.Alfa(cor, 110))) g.FillEllipse(cabeca, x0 + wp - 5, py - 5, 10, 10);
            using (var b = new SolidBrush(Hud.Alfa(cor, 255))) g.DrawString($"{(int)Math.Round(progressoVisivel)}%", fCab, b, x0 + wl + 8, py - 7);
        }
    }

    private void Aplicar(Bitmap bmp, byte alfa)
    {
        IntPtr tela = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(tela), hbmp = IntPtr.Zero, antigo = IntPtr.Zero;
        try
        {
            hbmp = bmp.GetHbitmap(Color.FromArgb(0));
            antigo = SelectObject(mem, hbmp);
            var tam = new TAMANHO(bmp.Width, bmp.Height);
            var origem = new PONTO(0, 0);
            var destino = new PONTO(posTela.X, posTela.Y);
            var blend = new BLEND { Op = 0, Flags = 0, Alpha = alfa, Format = 1 };
            UpdateLayeredWindow(Handle, tela, ref destino, ref tam, mem, ref origem, 0, ref blend, 2);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, tela);
            if (hbmp != IntPtr.Zero) { SelectObject(mem, antigo); DeleteObject(hbmp); }
            DeleteDC(mem);
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8, WS_EX_LAYERED = 0x80000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_LAYERED;
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { anim.Dispose(); fCab.Dispose(); fTitulo.Dispose(); fTexto.Dispose(); }
        base.Dispose(disposing);
    }

    // ===================== Uso =====================

    public static void MostrarSemEsperar(string titulo, string texto)
    {
        var aviso = new AvisoPopup(titulo, texto, false, false);
        aviso.FormClosed += (_, _) => aviso.Dispose();
        aviso.Show();
    }

    public static void Mostrar(string titulo, string texto, bool erro = false)
    {
        using var aviso = new AvisoPopup(titulo, texto, erro, false);
        Application.Run(aviso);
    }

    /// <summary>Botão de 1 clique: aparece na hora, acompanha a otimização ao vivo e termina com o resumo.</summary>
    public static Otimizador.Resultado OtimizarComProgresso()
    {
        Otimizador.Resultado resultado = null;
        using var aviso = new AvisoPopup(Falas.Inicio(), "Varrendo o sistema...", false, true) { progresso = 0 };
        aviso.cabecalho = "SEXTA-FEIRA  ·  PROTOCOLO DE JOGO";
        aviso.Ajustar();
        aviso.Shown += async (_, _) =>
        {
            try
            {
                resultado = await Task.Run(() => Otimizador.Executar((pct, txt) =>
                {
                    try { aviso.BeginInvoke(new Action(() => { aviso.progresso = pct; aviso.texto = txt; })); } catch { }
                }));
                aviso.progresso = -1;
                aviso.trabalhando = false;
                aviso.concluido = true;
                aviso.titulo = Falas.Fim(resultado);
                aviso.texto = resultado.Frase();
                aviso.tempoNaTela = 6500;
                aviso.Ajustar();
            }
            catch (Exception ex)
            {
                Atualizador.Log("ERRO na otimização rápida: " + ex);
                aviso.erro = true;
                aviso.trabalhando = false;
                aviso.progresso = -1;
                aviso.titulo = "Deu um problema aqui, chefe.";
                aviso.texto = ex.Message;
                aviso.Ajustar();
            }
        };
        Application.Run(aviso);
        return resultado;
    }
}

/// <summary>As falas da SEXTA-FEIRA no botão de 1 clique. Sorteadas pra não enjoar.</summary>
internal static class Falas
{
    private static readonly Random rnd = new();
    private static string Uma(params string[] l) => l[rnd.Next(l.Length)];

    public static string Inicio()
    {
        int h = DateTime.Now.Hour;
        if (h >= 0 && h < 5)
            return Uma("Madrugada de ranqueada, chefe? Preparando o sistema.",
                       "Entendido. Deixando tudo no ponto pra madrugada.");
        return Uma("Entendido, chefe. Iniciando protocolo de jogo.",
                   "Preparando o sistema pra batalha.",
                   "Iniciando varredura completa.",
                   "Deixa comigo. Ajustando tudo pra partida.",
                   "Protocolo de desempenho ativado.");
    }

    public static string Fim(Otimizador.Resultado r)
    {
        double gb = r.RamLiberada / (1024d * 1024 * 1024);
        if (gb >= 3)
            return Uma($"Tava pesado aqui, chefe. Agora tá leve.",
                       "Sistema desafogado. Pode entrar na partida.");
        if (r.Fechados.Count > 0)
            return Uma("Tirei o que tava atrapalhando. Pronto pra jogo.",
                       "Fechei os intrusos. Sistema pronto, chefe.");
        return Uma("Sistema pronto pra combate, chefe.",
                   "Varredura concluída. Desempenho no máximo.",
                   "Tudo nos trinques. Boa partida.",
                   "Protocolo concluído. Pode ir pro jogo.",
                   "Pronto, chefe. Sem nada no caminho.");
    }
}
