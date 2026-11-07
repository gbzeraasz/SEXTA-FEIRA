using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace SextaFeira;

/// <summary>
/// Aviso holográfico da SEXTA-FEIRA no canto superior direito.
/// Janela com transparência real (dá pra ver a tela por trás), linhas finas brilhando,
/// "se projeta" abrindo a partir de uma linha de luz e se recolhe do mesmo jeito.
/// - Modo aviso: título + texto, some sozinho.
/// - Modo otimização (botão de 1 clique): aparece na hora e acompanha ao vivo.
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

    // ---------- estado ----------
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 16 };
    private readonly Random rnd = new();
    private int fase;                 // 0 = projetando, 1 = parado, 2 = recolhendo
    private float t;                  // 0..1 da entrada/saída
    private int msParado, tempoNaTela = 4500;
    private string cabecalho = "SEXTA-FEIRA";
    private string titulo, texto;
    private bool erro, trabalhando, concluido;
    private int progresso = -1;
    private float progressoVisivel, giro, pulso;
    private int largura = 296, altura = 62;
    private Point posFinal;

    private readonly Font fCab = new(Hud.FonteForte, 6.75f);
    private readonly Font fTitulo = new(Hud.FonteForte, 9.25f);
    private readonly Font fTexto = new(Hud.FonteTexto, 7.75f);
    private const int XTexto = 54;

    private AvisoPopup(string titulo, string texto, bool erro, bool trabalhando)
    {
        this.titulo = titulo; this.texto = texto; this.erro = erro; this.trabalhando = trabalhando;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Ajustar();
        MouseClick += (_, _) => { if (!this.trabalhando && fase == 1) fase = 2; };
        anim.Tick += Animar;
        Shown += (_, _) => { Desenhar(); anim.Start(); };
    }

    private void Ajustar()
    {
        int w = largura - XTexto - 16;
        int hTexto = string.IsNullOrEmpty(texto) ? 0 :
            Math.Min(28, TextRenderer.MeasureText(texto, fTexto, new Size(w, 0), TextFormatFlags.WordBreak).Height);
        altura = Math.Max(58, 38 + hTexto + (progresso >= 0 ? 12 : 6));
        var area = Screen.PrimaryScreen!.WorkingArea;
        posFinal = new Point(area.Right - largura - 12, area.Top + 12);
        Bounds = new Rectangle(posFinal, new Size(largura, altura));
    }

    private static float Suave(float x) => 1 - (float)Math.Pow(1 - Math.Clamp(x, 0, 1), 3);

    private void Animar(object sender, EventArgs e)
    {
        giro = (giro + (trabalhando ? 7f : 1.8f)) % 360;
        pulso += 0.09f;
        if (progresso >= 0) progressoVisivel += (progresso - progressoVisivel) * 0.15f;
        switch (fase)
        {
            case 0:
                t = Math.Min(1, t + 0.05f);
                if (t >= 1) fase = 1;
                break;
            case 1:
                if (!trabalhando && (msParado += anim.Interval) >= tempoNaTela) fase = 2;
                break;
            default:
                t = Math.Max(0, t - 0.06f);
                if (t <= 0) { anim.Stop(); Close(); return; }
                break;
        }
        Desenhar();
    }

    // ---------- desenho ----------
    private void Desenhar()
    {
        if (!IsHandleCreated) return;
        using var bmp = new Bitmap(largura, altura, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            // "projeção": abre na vertical a partir do meio
            float abre = Suave(fase == 2 ? t : t * 1.15f);
            float hVis = Math.Max(2, altura * abre);
            float y0 = (altura - hVis) / 2f;
            g.SetClip(new RectangleF(0, y0, largura, hVis));
            DesenharConteudo(g);
            g.ResetClip();
            if (abre < 0.98f)
            {
                // linha de luz que "projeta" o painel
                float lw = largura * Math.Min(1, abre * 3 + 0.2f);
                float lx = (largura - lw) / 2;
                using var luz = new LinearGradientBrush(new RectangleF(lx, 0, lw, 2), Color.Transparent, Color.Transparent, 0f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { Color.Transparent, Color.FromArgb(230, 180, 245, 255), Color.Transparent },
                        Positions = new[] { 0f, 0.5f, 1f }
                    }
                };
                g.FillRectangle(luz, lx, y0 - 1, lw, 2);
                g.FillRectangle(luz, lx, y0 + hVis - 1, lw, 2);
            }
        }

        // tremidinha de holograma só durante a projeção
        byte alfa = (byte)Math.Clamp((fase == 0 && t < 0.6f ? (rnd.Next(70, 100) / 100f) : 1f) * Math.Min(1f, t * 1.6f) * 255, 0, 255);
        Aplicar(bmp, alfa);
    }

    private void DesenharConteudo(Graphics g)
    {
        var cor = erro ? Color.FromArgb(255, 95, 95) : concluido ? Color.FromArgb(80, 240, 210) : Color.FromArgb(0, 214, 255);
        var r = new RectangleF(0.5f, 0.5f, largura - 1.5f, altura - 1.5f);

        // vidro quase transparente, mais forte na esquerda
        using (var caminho = Hud.Redondo(r, 10))
        {
            using (var fundo = new LinearGradientBrush(r, Color.FromArgb(150, 4, 18, 30), Color.FromArgb(70, 4, 18, 30), 0f))
                g.FillPath(fundo, caminho);
            using (var borda = new Pen(Hud.Alfa(cor, 70), 1f)) g.DrawPath(borda, caminho);
        }
        // linhas de varredura bem fracas
        using (var scan = new Pen(Color.FromArgb(12, cor.R, cor.G, cor.B)))
            for (int y = 3; y < altura; y += 3) g.DrawLine(scan, 6, y, largura - 6, y);
        // cantoneiras
        using (var canto = new Pen(Hud.Alfa(cor, 220), 1.6f))
        {
            float c = 10, wc = largura - 1.5f, h = altura - 1.5f;
            g.DrawLine(canto, 1, c + 4, 1, 4); g.DrawLine(canto, 4, 1, c + 4, 1);
            g.DrawLine(canto, wc, h - c - 4, wc, h - 4); g.DrawLine(canto, wc - 4, h, wc - c - 4, h);
        }
        // reflexo no topo
        using (var reflexo = new LinearGradientBrush(new RectangleF(20, 0, largura - 40, 1), Color.Transparent, Color.Transparent, 0f)
        {
            InterpolationColors = new ColorBlend { Colors = new[] { Color.Transparent, Hud.Alfa(cor, 200), Color.Transparent }, Positions = new[] { 0f, 0.4f, 1f } }
        })
            g.FillRectangle(reflexo, 20, 0.5f, largura - 40, 1f);

        // núcleo
        float cx = 28, cy = Math.Min(altura / 2f, 31), raio = 14;
        using (var aura = new GraphicsPath())
        {
            aura.AddEllipse(cx - raio - 7, cy - raio - 7, (raio + 7) * 2, (raio + 7) * 2);
            using var br = new PathGradientBrush(aura) { CenterColor = Hud.Alfa(cor, (int)(70 + 30 * Math.Sin(pulso))), SurroundColors = new[] { Color.Transparent } };
            g.FillPath(br, aura);
        }
        using (var trilho = new Pen(Hud.Alfa(cor, 45), 1.2f)) g.DrawEllipse(trilho, cx - raio, cy - raio, raio * 2, raio * 2);
        using (var arco = new Pen(Hud.Alfa(cor, 240), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            if (concluido) g.DrawEllipse(arco, cx - raio, cy - raio, raio * 2, raio * 2);
            else for (int k = 0; k < 3; k++) g.DrawArc(arco, cx - raio, cy - raio, raio * 2, raio * 2, giro + k * 120, 70);
        }
        float r2 = raio - 4.5f;
        using (var interno = new Pen(Hud.Alfa(Color.FromArgb(160, 240, 255), 150), 1f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(interno, cx - r2, cy - r2, r2 * 2, r2 * 2, -giro * 1.5f, 230);
        using (var traco = new Pen(Color.FromArgb(240, 250, 255), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        using (var simbolo = new SolidBrush(Color.FromArgb(245, 252, 255)))
        {
            if (erro) { g.DrawLine(traco, cx, cy - 6, cx, cy + 1); g.FillEllipse(simbolo, cx - 1.4f, cy + 4, 2.8f, 2.8f); }
            else if (concluido) g.DrawLines(traco, new[] { new PointF(cx - 5, cy), new PointF(cx - 1.5f, cy + 3.5f), new PointF(cx + 6, cy - 4.5f) });
            else g.FillPolygon(simbolo, new[]
            {
                new PointF(cx + 1.2f, cy - 6.5f), new PointF(cx - 3.6f, cy + 0.8f), new PointF(cx - 0.6f, cy + 0.8f),
                new PointF(cx - 1.8f, cy + 6.5f), new PointF(cx + 3.6f, cy - 1.2f), new PointF(cx + 0.6f, cy - 1.2f)
            });
        }

        // textos (com um brilho leve atrás do título)
        using (var b = new SolidBrush(Hud.Alfa(cor, 210))) g.DrawString(cabecalho, fCab, b, XTexto - 1, 7);
        int w = largura - XTexto - 14;
        using (var brilho = new SolidBrush(Hud.Alfa(cor, 45)))
        using (var branco = new SolidBrush(Color.FromArgb(240, 235, 250, 255)))
        using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        {
            var rt = new RectangleF(XTexto - 1, 18, w, 18);
            g.DrawString(titulo, fTitulo, brilho, new RectangleF(rt.X + 0.8f, rt.Y + 0.8f, rt.Width, rt.Height), fmt);
            g.DrawString(titulo, fTitulo, branco, rt, fmt);
        }
        if (!string.IsNullOrEmpty(texto))
            using (var cinza = new SolidBrush(Color.FromArgb(200, 165, 205, 225)))
            using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                g.DrawString(texto, fTexto, cinza, new RectangleF(XTexto - 1, 35, w, 28), fmt);

        // progresso: linha fina com cabeça brilhante
        if (progresso >= 0)
        {
            float y = altura - 7, x0 = XTexto, wl = largura - XTexto - 42;
            using (var trilho = new Pen(Color.FromArgb(35, cor.R, cor.G, cor.B), 1.5f)) g.DrawLine(trilho, x0, y, x0 + wl, y);
            float wp = wl * Math.Clamp(progressoVisivel, 0, 100) / 100f;
            using (var cheio = new Pen(Hud.Alfa(cor, 230), 1.6f)) g.DrawLine(cheio, x0, y, x0 + wp, y);
            using (var cabeca = new SolidBrush(Hud.Alfa(cor, 120))) g.FillEllipse(cabeca, x0 + wp - 4, y - 4, 8, 8);
            using (var b = new SolidBrush(Hud.Alfa(cor, 230))) g.DrawString($"{(int)Math.Round(progressoVisivel)}%", fCab, b, largura - 36, y - 6);
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
            var destino = new PONTO(posFinal.X, posFinal.Y);
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

    /// <summary>Mostra o aviso sem travar (pra usar com o app aberto).</summary>
    public static void MostrarSemEsperar(string titulo, string texto)
    {
        var aviso = new AvisoPopup(titulo, texto, false, false);
        aviso.FormClosed += (_, _) => aviso.Dispose();
        aviso.Show();
    }

    /// <summary>Mostra o aviso e espera ele sumir.</summary>
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
                aviso.tempoNaTela = 6000;
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
