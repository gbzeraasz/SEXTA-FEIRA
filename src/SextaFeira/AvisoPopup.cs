using System.Drawing.Drawing2D;

namespace SextaFeira;

/// <summary>
/// Aviso da SEXTA-FEIRA no canto superior direito, no estilo HUD.
/// - Modo aviso: título + texto, some sozinho.
/// - Modo otimização (botão de 1 clique): aparece NA HORA, mostra ao vivo o que está fazendo e termina com o resumo.
/// Aparece mesmo com o "Não perturbe" ligado e não rouba o foco do jogo.
/// </summary>
internal class AvisoPopup : Form
{
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 16 };
    private int fase;              // 0 = entrando, 1 = parado, 2 = saindo
    private int msParado;
    private int tempoNaTela = 4500;
    private const double OpacidadeMax = 0.96;
    private const int Margem = 16, Deslize = 28;
    private int xFinal;

    private string cabecalho = "SEXTA-FEIRA";
    private string titulo, texto;
    private bool erro, trabalhando, concluido;
    private int progresso = -1;    // -1 = sem barra
    private float giro, pulso;
    private float progressoVisivel;

    private AvisoPopup(string titulo, string texto, bool erro, bool trabalhando)
    {
        this.titulo = titulo; this.texto = texto; this.erro = erro; this.trabalhando = trabalhando;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(7, 12, 22);
        DoubleBuffered = true;
        Opacity = 0;
        Width = 400;
        Ajustar();

        var area = Screen.PrimaryScreen!.WorkingArea;
        xFinal = area.Right - Width - Margem;
        Location = new Point(xFinal + Deslize, area.Top + Margem);

        Click += (_, _) => { if (!trabalhando) fase = 2; };
        anim.Tick += Animar;
        Shown += (_, _) => anim.Start();
    }

    private static Font F(string nome, float t) => new(nome, t);
    private readonly Font fCab = F(Hud.FonteForte, 8f);
    private readonly Font fTitulo = F(Hud.FonteForte, 11.5f);
    private readonly Font fTexto = F(Hud.FonteTexto, 9f);

    private const int XTexto = 84;

    private void Ajustar()
    {
        int larguraTexto = Width - XTexto - 18;
        int hTitulo = TextRenderer.MeasureText(titulo ?? "", fTitulo, new Size(larguraTexto, 0), TextFormatFlags.WordBreak).Height;
        int hTexto = string.IsNullOrEmpty(texto) ? 0 : TextRenderer.MeasureText(texto, fTexto, new Size(larguraTexto, 0), TextFormatFlags.WordBreak).Height;
        int h = 34 + hTitulo + (hTexto > 0 ? 4 + hTexto : 0) + (progresso >= 0 ? 22 : 12);
        h = Math.Max(h, 86);
        if (h != Height)
        {
            Height = h;
            try
            {
                using var p = Hud.Redondo(new RectangleF(0, 0, Width, Height), 16);
                Region = new Region(p);
            }
            catch { }
        }
        Invalidate();
    }

    private void Animar(object sender, EventArgs e)
    {
        giro = (giro + (trabalhando ? 9f : 2.5f)) % 360;
        pulso += 0.12f;
        if (progresso >= 0) progressoVisivel += (progresso - progressoVisivel) * 0.18f;
        switch (fase)
        {
            case 0:
                Opacity = Math.Min(OpacidadeMax, Opacity + 0.08);
                Left = Math.Max(xFinal, Left - 4);
                if (Opacity >= OpacidadeMax && Left == xFinal) fase = 1;
                break;
            case 1:
                if (!trabalhando)
                {
                    msParado += anim.Interval;
                    if (msParado >= tempoNaTela) fase = 2;
                }
                break;
            default:
                Opacity = Math.Max(0, Opacity - 0.06);
                if (Opacity <= 0) { anim.Stop(); Close(); return; }
                break;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var cor = erro ? Tema.Vermelho : concluido ? Color.FromArgb(70, 230, 200) : Hud.Ciano;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

        // fundo de vidro escuro com brilho em cima
        using (var caminho = Hud.Redondo(r, 16))
        {
            using var fundo = new LinearGradientBrush(r, Color.FromArgb(14, 24, 42), Color.FromArgb(6, 10, 19), 90f);
            g.FillPath(fundo, caminho);
            using var borda = new Pen(Hud.Alfa(cor, 120), 1.2f);
            g.DrawPath(borda, caminho);
        }
        using (var reflexo = new LinearGradientBrush(new RectangleF(20, 0, Width - 40, 2), Color.Transparent, Color.Transparent, 0f))
        {
            reflexo.InterpolationColors = new ColorBlend
            {
                Colors = new[] { Color.Transparent, Hud.Alfa(cor, 220), Color.Transparent },
                Positions = new[] { 0f, 0.5f, 1f }
            };
            g.FillRectangle(reflexo, 20, 1, Width - 40, 1.5f);
        }

        // núcleo animado à esquerda
        float cx = 42, cy = Math.Min(Height / 2f, 48), raio = 24;
        using (var aura = new GraphicsPath())
        {
            aura.AddEllipse(cx - raio - 8, cy - raio - 8, (raio + 8) * 2, (raio + 8) * 2);
            using var br = new PathGradientBrush(aura)
            {
                CenterColor = Hud.Alfa(cor, (int)(55 + 25 * Math.Sin(pulso))),
                SurroundColors = new[] { Color.Transparent }
            };
            g.FillPath(br, aura);
        }
        using (var trilho = new Pen(Hud.Alfa(cor, 40), 3)) g.DrawEllipse(trilho, cx - raio, cy - raio, raio * 2, raio * 2);
        using (var arco = new Pen(cor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            if (concluido) g.DrawEllipse(arco, cx - raio, cy - raio, raio * 2, raio * 2);
            else
            {
                g.DrawArc(arco, cx - raio, cy - raio, raio * 2, raio * 2, giro, 100);
                g.DrawArc(arco, cx - raio, cy - raio, raio * 2, raio * 2, giro + 180, 100);
            }
        }
        float r2 = raio - 7;
        using (var interno = new Pen(Hud.Alfa(Hud.CianoClaro, 160), 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(interno, cx - r2, cy - r2, r2 * 2, r2 * 2, -giro * 1.4f, 220);
        // símbolo no meio: raio (trabalhando), ✓ (pronto) ou ! (erro)
        using (var simbolo = new SolidBrush(Color.White))
        using (var linha = new Pen(Color.White, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            if (erro) { g.DrawLine(linha, cx, cy - 8, cx, cy + 2); g.FillEllipse(simbolo, cx - 1.8f, cy + 6, 3.6f, 3.6f); }
            else if (concluido) g.DrawLines(linha, new[] { new PointF(cx - 7, cy), new PointF(cx - 2, cy + 5), new PointF(cx + 8, cy - 6) });
            else g.FillPolygon(simbolo, new[]
            {
                new PointF(cx + 2, cy - 11), new PointF(cx - 6, cy + 1), new PointF(cx - 1, cy + 1),
                new PointF(cx - 3, cy + 11), new PointF(cx + 6, cy - 2), new PointF(cx + 1, cy - 2)
            });
        }

        // textos
        int larguraTexto = Width - XTexto - 18;
        TextRenderer.DrawText(g, cabecalho, fCab, new Point(XTexto, 13), Hud.Alfa(cor, 255), TextFormatFlags.NoPadding);
        var rt = new Rectangle(XTexto, 30, larguraTexto, Height);
        TextRenderer.DrawText(g, titulo, fTitulo, rt, Color.White, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        int hTitulo = TextRenderer.MeasureText(titulo ?? "", fTitulo, new Size(larguraTexto, 0), TextFormatFlags.WordBreak).Height;
        if (!string.IsNullOrEmpty(texto))
            TextRenderer.DrawText(g, texto, fTexto, new Rectangle(XTexto, 34 + hTitulo, larguraTexto, Height),
                Color.FromArgb(160, 180, 205), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

        // barra de progresso
        if (progresso >= 0)
        {
            float y = Height - 14, x0 = XTexto, w = Width - XTexto - 52;
            using (var trilho = Hud.Redondo(new RectangleF(x0, y, w, 4), 2))
            using (var b = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                g.FillPath(b, trilho);
            float wp = Math.Max(4, w * Math.Clamp(progressoVisivel, 0, 100) / 100f);
            using (var cheio = Hud.Redondo(new RectangleF(x0, y, wp, 4), 2))
            using (var b = new LinearGradientBrush(new RectangleF(x0, y, w, 4), Hud.Alfa(cor, 160), cor, 0f))
                g.FillPath(b, cheio);
            using (var brilho = new SolidBrush(Hud.Alfa(cor, 90))) g.FillEllipse(brilho, x0 + wp - 5, y - 3, 10, 10);
            TextRenderer.DrawText(g, $"{(int)Math.Round(progressoVisivel)}%", fCab, new Point(Width - 44, (int)y - 6), Hud.Alfa(cor, 255), TextFormatFlags.NoPadding);
        }
    }

    // Não rouba o foco do jogo
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
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

    /// <summary>
    /// Botão de 1 clique: o aviso aparece na hora, acompanha a otimização ao vivo e termina com o resumo.
    /// Devolve o resultado (ou null se deu erro).
    /// </summary>
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
                    try
                    {
                        aviso.BeginInvoke(new Action(() =>
                        {
                            aviso.progresso = pct;
                            aviso.texto = txt;
                            aviso.Ajustar();
                        }));
                    }
                    catch { }
                }));
                aviso.progresso = 100;
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
