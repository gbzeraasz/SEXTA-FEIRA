using System.Runtime.InteropServices;
using Velopack;

namespace SextaFeira;

internal class JanelaPrincipal : Form
{
    // ---------- dados ----------
    private InfoPC pc = new();
    private List<Jogos.Jogo> jogos = new();
    private List<Checagem> checagens = new();
    private bool ocupado;
    private bool lendoMonitor;
    private UpdateInfo atualizacao;

    // ---------- estrutura ----------
    private readonly Panel lateral = new();
    private readonly Panel conteudo = new();
    private readonly Panel faixaAtualizacao = new();
    private readonly Dictionary<string, Panel> paginas = new();
    private readonly Dictionary<string, Button> abas = new();
    private const int LarguraUtil = 900;

    // ---------- início ----------
    private readonly BarraUso barCpu = new() { Nome = "CPU" };
    private readonly BarraUso barRam = new() { Nome = "RAM" };
    private readonly BarraUso barGpu = new() { Nome = "GPU" };
    private readonly ListaHud listaTop = new();
    private readonly ConsoleHud consoleIa = new();
    private readonly System.Windows.Forms.Timer timerFala = new() { Interval = 9000 };
    private int falaIndice;
    private float ultTemp = -1, ultRam = -1;
    private string ultTopNome, ultTopTam;
    private readonly ListaHud listaSpecs = new();
    private BotaoHud btnOtimizar;
    private readonly Nucleo nucleo = new();
    private readonly Label lblLegenda = new();
    private readonly Label lblSaudacao = new();
    private readonly Label lblStatus = Tema.Rotulo("", Tema.Negrito, Tema.Destaque);
    private readonly Label lblResultado = Tema.Rotulo("Clica em OTIMIZAR antes de abrir o jogo.", Tema.Normal, Tema.Apagado);
    private readonly Label lblContador = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private readonly Label lblNota = Tema.Rotulo("--%", Hud.Leve(38f), Tema.Destaque);
    private readonly Label lblNotaTxt = Tema.Rotulo("analisando...", Tema.Pequena, Tema.Apagado);
    private readonly Label lblTemp = Tema.Rotulo("-- °C", Hud.Leve(32f), Tema.Apagado);
    private readonly Label lblTempInfo = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private readonly Label lblTempExplica = Tema.Rotulo("Lendo o sensor...", Tema.Pequena, Tema.Texto);
    private readonly Label lblSaudeTitulo = Tema.Rotulo("Analisando...", Tema.Negrito, Tema.Destaque);
    private readonly Label lblSaudeDet = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);

    // ---------- restauração ----------
    private readonly Panel listaPontos = new();
    private readonly TextBox txtPonto = new();
    private readonly Label lblPontoStatus = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private Button btnCriarPonto;

    // ---------- checagem ----------
    private readonly Panel listaChecagem = new();
    private readonly Label lblResumoChecagem = Tema.Rotulo("", Tema.Negrito);

    // ---------- jogos ----------

    // ---------- ferramentas ----------
    private readonly Label lblAtalho = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private Button btnAtalho;
    private readonly Label lblVersao = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private readonly TextBox txtProgramas = new();

    private readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    // Procura atualização de novo a cada 30 minutos enquanto o app estiver aberto
    private readonly System.Windows.Forms.Timer timerSaude = new() { Interval = 5 * 60 * 1000 };
    private readonly System.Windows.Forms.Timer timerAtualizacao = new() { Interval = 5 * 60 * 1000 };
    private BotaoHud btnAtualizacaoLateral;

    public JanelaPrincipal()
    {
        Text = "SEXTA-FEIRA";
        Icon = Tema.Icone;
        BackColor = Tema.Fundo;
        ForeColor = Tema.Texto;
        Font = Tema.Normal;
        ClientSize = new Size(1180, 780);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        MontarLateral();

        conteudo.Dock = DockStyle.Fill;
        conteudo.BackColor = Tema.Fundo;
        Controls.Add(conteudo);
        Controls.Add(lateral);
        conteudo.BringToFront();

        MontarFaixaAtualizacao();
        paginas["inicio"] = MontarInicio();
        paginas["checagem"] = MontarChecagem();
        paginas["jogos"] = MontarJogos();
        paginas["competitivo"] = MontarCompetitivo();
        paginas["limpeza"] = MontarLimpeza();
        paginas["internet"] = MontarInternet();
        paginas["benchmark"] = MontarBenchmark();
        paginas["pros"] = MontarPros();
        paginas["reparos"] = MontarReparos();
        paginas["restauracao"] = MontarRestauracao();
        paginas["ferramentas"] = MontarFerramentas();
        foreach (var p in paginas.Values) conteudo.Controls.Add(p);
        Mostrar("inicio");

        timer.Tick += async (_, _) => await AtualizarMonitor();
        Shown += async (_, _) =>
        {
            MostrarBoot();
            string sd = DateTime.Now.Hour < 12 ? "Bom dia" : DateTime.Now.Hour < 18 ? "Boa tarde" : "Boa noite";
            consoleIa.Dizer($"{sd}. Todos os sistemas online.", true);
            consoleIa.Dizer("Lendo sensores e verificando o PC...");
            timerFala.Tick += (_, _) => { if (consoleIa.Livre) consoleIa.Dizer(ProximaFala()); };
            timerFala.Start();
            await Carregar();
        };
        // ----- Bandeja: fechar a janela não desliga a proteção de rede durante o jogo -----
        var menuBandeja = new ContextMenuStrip();
        menuBandeja.Items.Add("Abrir a SEXTA-FEIRA", null, (_, _) => AbrirDaBandeja());
        menuBandeja.Items.Add("Sair de vez", null, (_, _) => { sairDeVerdade = true; Close(); });
        bandeja = new NotifyIcon { Icon = Tema.Icone, Text = "SEXTA-FEIRA · protegendo tua internet", ContextMenuStrip = menuBandeja };
        bandeja.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) AbrirDaBandeja(); };

        FormClosing += (_, e) =>
        {
            var cfg = Configuracao.Atual;
            bool ficarNaBandeja = !sairDeVerdade && e.CloseReason == CloseReason.UserClosing && (cfg.RotaAutomatica || cfg.ProtegerRedeNoJogo);
            if (ficarNaBandeja)
            {
                e.Cancel = true;
                Hide();
                timer.Stop();
                timerFala.Stop();
                bandeja.Visible = true;
                if (!avisouBandeja)
                {
                    avisouBandeja = true;
                    AvisoPopup.MostrarSemEsperar("Continuo aqui na bandeja",
                        "Quando tu abrir um jogo, ligo a melhor rota e protejo a internet. Pra sair de vez: botão direito no ícone perto do relógio.");
                }
                return;
            }
            bandeja.Visible = false;
            timerVigia.Stop(); timerFala.Stop(); timer.Stop(); timerAtualizacao.Stop(); timerSaude.Stop(); Saude.Salvar(); timerComp.Stop();
            ModoCompetitivo.Desligar();
            RotaInteligente.Encerrar();
        };
    }

    private NotifyIcon bandeja;
    private bool sairDeVerdade, avisouBandeja;

    private void AbrirDaBandeja()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        bandeja.Visible = false;
        timer.Start();
        timerFala.Start();
    }

    private void MostrarBoot()
    {
        var boot = new BootHud(new[]
        {
            "carregando núcleo...",
            "sensores online",
            "lendo hardware e jogos",
            "sistema pronto. bom jogo."
        });
        boot.Terminou += (_, _) =>
        {
            Controls.Remove(boot);
            boot.Dispose();
        };
        Controls.Add(boot);
        boot.BringToFront();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

    /// <summary>Deixa a barra de título escura, combinando com o app (sem a faixa branca).</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int sim = 1;
            DwmSetWindowAttribute(Handle, 20, ref sim, 4);                 // modo escuro
            int fundo = Tema.Lateral.R | (Tema.Lateral.G << 8) | (Tema.Lateral.B << 16);
            DwmSetWindowAttribute(Handle, 35, ref fundo, 4);               // cor da barra (Windows 11)
            int texto = Tema.Texto.R | (Tema.Texto.G << 8) | (Tema.Texto.B << 16);
            DwmSetWindowAttribute(Handle, 36, ref texto, 4);               // cor do título
            int borda = fundo;
            DwmSetWindowAttribute(Handle, 34, ref borda, 4);               // cor da borda
        }
        catch { }
    }

    // =====================================================================
    //  Estrutura
    // =====================================================================

    // Menu: itens soltos e grupos que abrem/fecham
    private record ItemMenu(string Chave, string Texto, (string Chave, string Texto)[] Filhos = null);

    private static readonly ItemMenu[] Menu =
    {
        new("inicio", "⚡  Início"),
        new("limpeza", "🧹  Limpeza 1 clique"),
        new("internet", "🌐  Internet"),
        new("g-checagem", "🩺  Checagem", new[] { ("checagem", "Checagem do PC"), ("benchmark", "Benchmark"), ("reparos", "Reparos"), ("restauracao", "Ponto de restauração") }),
        new("g-biblioteca", "📚  Biblioteca", new[] { ("jogos", "Meus jogos"), ("pros", "Mira dos pros"), ("competitivo", "Competitivo") }),
        new("ferramentas", "🛠  Ferramentas"),
    };

    private readonly Dictionary<string, Button> grupos = new();
    private readonly HashSet<string> gruposAbertos = new();
    private string paginaAtual = "inicio";

    private Button BotaoMenu(string texto, string tag, bool filho)
    {
        var b = new BotaoMenuHud
        {
            Text = texto,
            Font = filho ? new Font(Hud.FonteTexto, 9.5f) : new Font(Hud.FonteForte, 10.5f),
            ForeColor = Tema.Apagado,
            BackColor = Tema.Lateral,
            Size = new Size(filho ? 180 : 196, filho ? 34 : 42),
            Recuo = filho ? 28 : 14,
            Tag = tag
        };
        lateral.Controls.Add(b);
        return b;
    }

    private void MontarLateral()
    {
        lateral.Dock = DockStyle.Left;
        lateral.Width = 220;
        lateral.BackColor = Tema.Lateral;

        lateral.Controls.Add(new LogoNucleo { Location = new Point(16, 30) });
        var logo = Tema.Rotulo("SEXTA-FEIRA", new Font(Hud.FonteForte, 15f), Color.White);
        logo.Location = new Point(54, 26);
        lateral.Controls.Add(logo);
        var sub = Tema.Rotulo("sistema de desempenho", new Font(Hud.FonteTexto, 8.5f), Hud.Alfa(Hud.Ciano, 255));
        sub.Location = new Point(56, 52);
        lateral.Controls.Add(sub);
        // linha de luz na borda do menu
        lateral.Paint += (_, e) =>
        {
            var r = new Rectangle(lateral.Width - 1, 0, 1, lateral.Height);
            using var br = new System.Drawing.Drawing2D.LinearGradientBrush(r, Color.Transparent, Color.Transparent, 90f);
            br.InterpolationColors = new System.Drawing.Drawing2D.ColorBlend
            {
                Colors = new[] { Color.FromArgb(10, 0, 209, 255), Color.FromArgb(90, 0, 209, 255), Color.FromArgb(10, 0, 209, 255) },
                Positions = new[] { 0f, 0.4f, 1f }
            };
            e.Graphics.FillRectangle(br, r);
        };

        foreach (var item in Menu)
        {
            if (item.Filhos == null)
            {
                var b = BotaoMenu(item.Texto, item.Chave, false);
                b.Click += (s, _) => Mostrar((string)((Button)s).Tag);
                abas[item.Chave] = b;
            }
            else
            {
                var g = BotaoMenu(item.Texto + "   ▸", item.Chave, false);
                string chaveGrupo = item.Chave, primeiro = item.Filhos[0].Chave;
                g.Click += (_, _) =>
                {
                    if (gruposAbertos.Contains(chaveGrupo)) gruposAbertos.Remove(chaveGrupo);
                    else { gruposAbertos.Add(chaveGrupo); Mostrar(primeiro); return; }
                    ReorganizarMenu();
                };
                grupos[item.Chave] = g;
                foreach (var (chave, texto) in item.Filhos)
                {
                    var f = BotaoMenu(texto, chave, true);
                    f.Click += (s, _) => Mostrar((string)((Button)s).Tag);
                    abas[chave] = f;
                }
            }
        }

        // selo de "atualização pronta" no menu (aparece só quando tem versão nova)
        btnAtualizacaoLateral = new BotaoHud
        {
            Text = "Atualização pronta",
            Icone = "⬆",
            Cheio = true,
            Cor = Tema.Verde,
            Location = new Point(14, 86),
            Size = new Size(192, 36),
            Visible = false
        };
        btnAtualizacaoLateral.Font = Hud.Titulo(9f, FontStyle.Bold);
        btnAtualizacaoLateral.Click += async (_, _) => await InstalarAtualizacao();
        lateral.Controls.Add(btnAtualizacaoLateral);
        var piscar = new System.Windows.Forms.Timer { Interval = 700 };
        bool aceso = true;
        piscar.Tick += (_, _) =>
        {
            if (!btnAtualizacaoLateral.Visible) return;
            aceso = !aceso;
            btnAtualizacaoLateral.Cor = aceso ? Tema.Verde : ControlPaint.Dark(Tema.Verde, 0.25f);
            btnAtualizacaoLateral.Invalidate();
        };
        piscar.Start();

        lblVersao.Location = new Point(22, 740);
        lblVersao.MaximumSize = new Size(190, 0);
        lateral.Controls.Add(lblVersao);
        ReorganizarMenu();
    }

    private void ReorganizarMenu()
    {
        int y = btnAtualizacaoLateral != null && btnAtualizacaoLateral.Visible ? 132 : 120;
        foreach (var item in Menu)
        {
            if (item.Filhos == null)
            {
                abas[item.Chave].Location = new Point(12, y);
                y += 50;
                continue;
            }
            bool aberto = gruposAbertos.Contains(item.Chave);
            bool ativo = item.Filhos.Any(f => f.Chave == paginaAtual);
            var g = grupos[item.Chave];
            g.Location = new Point(12, y);
            g.Text = item.Texto + (aberto ? "   ▾" : "   ▸");
            g.ForeColor = ativo ? Hud.CianoClaro : Tema.Apagado;
            g.Invalidate();
            y += 48;
            foreach (var (chave, _) in item.Filhos)
            {
                var f = abas[chave];
                f.Visible = aberto;
                if (aberto) { f.Location = new Point(28, y); y += 38; }
            }
            if (aberto) y += 6;
        }
    }

    private void Mostrar(string chave)
    {
        paginaAtual = chave;
        foreach (var item in Menu.Where(m => m.Filhos != null && m.Filhos.Any(f => f.Chave == chave)))
            gruposAbertos.Add(item.Chave);
        foreach (var (k, p) in paginas) p.Visible = k == chave;
        foreach (var (k, b) in abas)
        {
            if (b is BotaoMenuHud m) m.Selecionado = k == chave;
            b.ForeColor = Tema.Apagado;
            b.Invalidate();
        }
        ReorganizarMenu();
    }

    private Panel NovaPagina(string titulo, string subtitulo)
    {
        var p = new PainelHud { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(30, 26, 30, 20) };
        Tema.Escurecer(p);
        // tracinho holográfico ao lado do título
        var marca = new Panel { BackColor = Hud.Ciano, Location = new Point(18, 33), Size = new Size(4, 26) };
        marca.Region = new Region(Hud.Redondo(new RectangleF(0, 0, 4, 26), 2));
        p.Controls.Add(marca);
        var t = Tema.Rotulo(titulo, Tema.Titulo, Color.White);
        t.Location = new Point(28, 24);
        p.Controls.Add(t);
        if (subtitulo != null)
        {
            var s = Tema.Rotulo(subtitulo, Tema.Pequena, Tema.Apagado);
            s.Location = new Point(33, 70);
            s.MaximumSize = new Size(LarguraUtil, 0);
            p.Controls.Add(s);
        }
        return p;
    }

    private static Panel Card(int x, int y, int w, int h, string titulo = null)
    {
        var c = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Tema.Card };
        if (titulo != null)
        {
            var ponto = Tema.Rotulo("●", new Font("Segoe UI", 7f), Hud.Ciano);
            ponto.Location = new Point(16, 17);
            c.Controls.Add(ponto);
            var t = Tema.Rotulo(titulo, new Font(Hud.FonteForte, 9.5f), Hud.CianoClaro);
            t.Location = new Point(30, 13);
            c.Controls.Add(t);
        }
        return c;
    }

    // =====================================================================
    //  Faixa de atualização
    // =====================================================================

    private Label lblFaixa;
    private Button btnFaixa;

    private void MontarFaixaAtualizacao()
    {
        faixaAtualizacao.Dock = DockStyle.Top;
        faixaAtualizacao.Height = 50;
        faixaAtualizacao.BackColor = Color.FromArgb(8, 34, 30);
        faixaAtualizacao.Paint += (_, e) =>
        {
            using var b = new SolidBrush(Hud.Alfa(Tema.Verde, 160));
            e.Graphics.FillRectangle(b, 0, faixaAtualizacao.Height - 1, faixaAtualizacao.Width, 1);
        };
        faixaAtualizacao.Visible = false;

        lblFaixa = Tema.Rotulo("", Tema.Negrito, Color.White);
        lblFaixa.Location = new Point(30, 15);
        faixaAtualizacao.Controls.Add(lblFaixa);

        btnFaixa = Tema.Botao("Atualizar agora", Tema.Verde, 170, 34);
        btnFaixa.Location = new Point(LarguraUtil - 120, 8);
        btnFaixa.Click += async (_, _) => await InstalarAtualizacao();
        faixaAtualizacao.Controls.Add(btnFaixa);

        conteudo.Controls.Add(faixaAtualizacao);
    }

    private bool atualizacaoBaixada;
    private bool procurandoAtualizacao;

    private async Task InstalarAtualizacao()
    {
        if (atualizacao != null && atualizacaoBaixada)
        {
            btnFaixa.Enabled = false;
            lblFaixa.Text = "Reiniciando pra atualizar...";
            try { Atualizador.AplicarEReiniciar(atualizacao); }
            catch (Exception ex)
            {
                lblFaixa.Text = "Não deu pra atualizar agora: " + ex.Message;
                btnFaixa.Enabled = true;
            }
            return;
        }
        await ProcurarAtualizacao(true);
    }

    // =====================================================================
    //  Página: Início
    // =====================================================================

    private static Label RotuloHud(string txt, Font f, Color cor)
    {
        var l = Tema.Rotulo(txt, f, cor);
        l.BackColor = Color.Transparent;
        return l;
    }

    private Panel MontarInicio()
    {
        // Tela inicial no estilo HUD holográfico: núcleo no centro, painéis dos lados
        var p = new PainelHud { Dock = DockStyle.Fill };

        string Saudacao() => DateTime.Now.Hour < 12 ? "Bom dia" : DateTime.Now.Hour < 18 ? "Boa tarde" : "Boa noite";
        lblSaudacao.Text = $"{Saudacao()}  ·  sistema online";
        lblSaudacao.Font = Hud.Titulo(9f, FontStyle.Bold);
        lblSaudacao.ForeColor = Hud.Ciano;
        lblSaudacao.BackColor = Color.Transparent;
        lblSaudacao.AutoSize = true;
        lblSaudacao.Location = new Point(22, 20);
        p.Controls.Add(lblSaudacao);

        var titulo = RotuloHud("SEXTA-FEIRA", Hud.Titulo(24f, FontStyle.Bold), Color.White);
        titulo.Location = new Point(18, 38);
        p.Controls.Add(titulo);

        consoleIa.Location = new Point(340, 42);
        consoleIa.Size = new Size(600, 34);
        p.Controls.Add(consoleIa);

        // ================= coluna esquerda =================
        var temp = new CardHud { Titulo = "Temperatura · GPU", Location = new Point(20, 96), Size = new Size(270, 196) };
        lblTemp.Font = Hud.Leve(32f);
        lblTemp.BackColor = Color.Transparent;
        lblTemp.Location = new Point(14, 44);
        temp.Controls.Add(lblTemp);
        lblTempInfo.Font = Hud.Mono(8.5f);
        lblTempInfo.BackColor = Color.Transparent;
        lblTempInfo.Location = new Point(150, 50);
        temp.Controls.Add(lblTempInfo);
        lblTempExplica.BackColor = Color.Transparent;
        lblTempExplica.Location = new Point(18, 104);
        lblTempExplica.MaximumSize = new Size(236, 0);
        temp.Controls.Add(lblTempExplica);
        var faixas = RotuloHud("50° parada  •  75° jogando  •  83°+ perde FPS", Hud.Mono(7f), Tema.Apagado);
        faixas.Location = new Point(18, 170);
        temp.Controls.Add(faixas);
        p.Controls.Add(temp);

        var saude = new CardHud { Titulo = "Saúde do sistema", Location = new Point(20, 304), Size = new Size(270, 248) };
        lblSaudeTitulo.BackColor = Color.Transparent;
        lblSaudeTitulo.Location = new Point(18, 46);
        lblSaudeTitulo.MaximumSize = new Size(236, 0);
        saude.Controls.Add(lblSaudeTitulo);
        lblSaudeDet.BackColor = Color.Transparent;
        lblSaudeDet.Location = new Point(18, 72);
        lblSaudeDet.MaximumSize = new Size(236, 168);
        saude.Controls.Add(lblSaudeDet);
        p.Controls.Add(saude);

        var sistema = new CardHud { Titulo = "Hardware", Location = new Point(20, 564), Size = new Size(270, 176) };
        listaSpecs.Location = new Point(18, 48);
        listaSpecs.Size = new Size(236, 120);
        listaSpecs.ValorADireita = false;
        listaSpecs.ColunaValor = 44;
        listaSpecs.AlturaLinha = 25;
        listaSpecs.CorValor = Hud.Ciano;
        listaSpecs.Definir(new[] { ("", "Lendo o teu PC...") });
        sistema.Controls.Add(listaSpecs);
        p.Controls.Add(sistema);

        // ================= centro: núcleo =================
        nucleo.Location = new Point(300, 84);
        nucleo.Size = new Size(360, 360);
        nucleo.CliqueCentro += async (_, _) => await Otimizar();
        p.Controls.Add(nucleo);

        lblLegenda.Font = Hud.Mono(8.5f);
        lblLegenda.ForeColor = Hud.Alfa(Hud.Ciano, 220);
        lblLegenda.BackColor = Color.Transparent;
        lblLegenda.AutoSize = false;
        lblLegenda.TextAlign = ContentAlignment.MiddleCenter;
        lblLegenda.Location = new Point(300, 446);
        lblLegenda.Size = new Size(360, 18);
        lblLegenda.Text = "CPU --  •  RAM --  •  GPU --";
        p.Controls.Add(lblLegenda);

        btnOtimizar = new BotaoHud { Text = "Otimizar agora", Icone = "⚡", Cheio = true, Location = new Point(330, 474), Size = new Size(300, 52) };
        btnOtimizar.Font = Hud.Titulo(13f, FontStyle.Bold);
        btnOtimizar.Click += async (_, _) => await Otimizar();
        p.Controls.Add(btnOtimizar);

        lblStatus.BackColor = Color.Transparent;
        lblStatus.Font = Hud.Mono(8.5f);
        lblStatus.AutoSize = false;
        lblStatus.TextAlign = ContentAlignment.MiddleCenter;
        lblStatus.Location = new Point(300, 532);
        lblStatus.Size = new Size(360, 20);
        p.Controls.Add(lblStatus);

        lblResultado.BackColor = Color.Transparent;
        lblResultado.AutoSize = false;
        lblResultado.TextAlign = ContentAlignment.TopCenter;
        lblResultado.Location = new Point(300, 556);
        lblResultado.Size = new Size(360, 130);
        lblResultado.Text = "Clica no núcleo ou no botão antes de abrir o jogo.";
        p.Controls.Add(lblResultado);

        lblContador.BackColor = Color.Transparent;
        lblContador.Font = Hud.Mono(7.5f);
        lblContador.AutoSize = false;
        lblContador.TextAlign = ContentAlignment.MiddleCenter;
        lblContador.Location = new Point(300, 700);
        lblContador.Size = new Size(360, 36);
        p.Controls.Add(lblContador);

        // ================= coluna direita =================
        var status = new CardHud { Titulo = "Status", Location = new Point(670, 96), Size = new Size(270, 150) };
        lblNotaTxt.BackColor = Color.Transparent;
        lblNotaTxt.Location = new Point(18, 46);
        lblNotaTxt.MaximumSize = new Size(236, 0);
        status.Controls.Add(lblNotaTxt);
        var verFalta = new BotaoHud { Text = "Ver o que falta", Location = new Point(18, 104), Size = new Size(170, 32) };
        verFalta.Font = Hud.Titulo(8.5f, FontStyle.Bold);
        verFalta.Click += (_, _) => Mostrar("checagem");
        status.Controls.Add(verFalta);
        p.Controls.Add(status);

        var procs = new CardHud { Titulo = "Processos · RAM", Location = new Point(670, 258), Size = new Size(270, 230) };
        listaTop.Location = new Point(18, 48);
        listaTop.Size = new Size(234, 176);
        listaTop.AlturaLinha = 23;
        procs.Controls.Add(listaTop);
        p.Controls.Add(procs);

        var atalhos = new CardHud { Titulo = "Acesso rápido", Location = new Point(670, 500), Size = new Size(270, 240) };
        int y = 48;
        foreach (var (txt, icone, destino) in new[]
                 {
                     ("Modo competitivo", "🏆", "competitivo"), ("Limpeza 1 clique", "🧹", "limpeza"),
                     ("Internet", "🌐", "internet"), ("Benchmark", "📊", "benchmark")
                 })
        {
            var b = new BotaoHud { Text = txt, Icone = icone, Location = new Point(18, y), Size = new Size(234, 38) };
            b.Font = Hud.Titulo(9f, FontStyle.Bold);
            string d = destino;
            b.Click += (_, _) => Mostrar(d);
            atalhos.Controls.Add(b);
            y += 46;
        }
        p.Controls.Add(atalhos);
        return p;
    }

    private async Task Otimizar()
    {
        if (ocupado) return;
        ocupado = true;
        btnOtimizar.Enabled = false;
        btnOtimizar.Text = "Otimizando...";
        consoleIa.Dizer("Otimizando o sistema. Isso leva só alguns segundos...", true);
        nucleo.Otimizando = true;
        nucleo.Progresso = 0;
        lblResultado.ForeColor = Tema.Apagado;

        try
        {
            // Na 1ª otimização pelo app, cria um ponto de restauração antes de mexer em qualquer coisa
            string marca = Path.Combine(Sistema.PastaDados, "ponto-inicial");
            if (!File.Exists(marca))
            {
                lblStatus.Text = "Criando ponto de restauração (só na 1ª vez, pode demorar)...";
                var (ok, msg) = await Task.Run(() => Restauracao.Criar("SEXTA-FEIRA - antes da 1ª otimização"));
                Atualizador.Log("Ponto inicial: " + msg);
                File.WriteAllText(marca, ok ? "ok" : msg);
            }

            var r = await Task.Run(() => Otimizador.Executar((pct, txt) =>
                BeginInvoke(new Action(() => { lblStatus.Text = $"{pct}%  {txt}"; nucleo.Progresso = pct; }))));

            nucleo.Otimizando = false;
            nucleo.Pulsar();
            lblResultado.ForeColor = Tema.Texto;
            lblResultado.Text = "✅ " + r.Frase() + "\n" + string.Join("  •  ", r.Feito);
            consoleIa.Dizer("Otimização concluída. " + r.Frase(), true);
            AtualizarContador();
        }
        catch (Exception ex)
        {
            lblResultado.ForeColor = Tema.Vermelho;
            lblResultado.Text = "Algo deu errado: " + ex.Message;
        }
        finally
        {
            btnOtimizar.Text = "Otimizar agora";
            btnOtimizar.Enabled = true;
            nucleo.Otimizando = false;
            ocupado = false;
        }
        _ = RodarChecagem();
        _ = CarregarPontos();
    }

    private void AtualizarContador()
    {
        var h = Historico.Carregar();
        if (h.Count == 0) { lblContador.Text = ""; return; }
        double disco = h.Sum(x => x.Disco);
        double ram = h.Sum(x => x.RamLiberada);
        lblContador.Text = $"Já otimizou {h.Count}x  •  {Sistema.Formatar(disco)} de disco e {Sistema.Formatar(ram)} de RAM liberados no total  •  última: {h[^1].Quando:dd/MM HH:mm}";
    }

    private async Task AtualizarMonitor()
    {
        if (lendoMonitor) return;
        lendoMonitor = true;
        try
        {
            var (l, g) = await Task.Run(() =>
            {
                var leitura = MonitorPC.Ler();
                var gpu = Sensores.LerGpu();
                Saude.Registrar(gpu);
                return (leitura, gpu);
            });

            nucleo.Definir(l.Cpu, l.Ram, g.Uso >= 0 ? g.Uso : l.Gpu);
            lblLegenda.Text = $"CPU {Math.Max(0, l.Cpu):0}%  •  RAM {Math.Max(0, l.Ram):0}%  •  GPU {Math.Max(0, g.Uso >= 0 ? g.Uso : l.Gpu):0}%";
            barCpu.Valor = l.Cpu;
            barRam.Valor = l.Ram;
            barGpu.Valor = g.Uso >= 0 ? g.Uso : l.Gpu;
            listaTop.Definir(l.Top.Select(t => (t.Nome, Sistema.Formatar(t.Bytes))));
            ultTemp = g.Disponivel ? g.Temp : -1;
            ultRam = l.Ram;
            var maior = l.Top.FirstOrDefault();
            ultTopNome = maior.Nome;
            ultTopTam = maior.Nome != null ? Sistema.Formatar(maior.Bytes) : null;

            var faixa = Sensores.Classificar(g.Temp);
            lblTemp.Text = g.Disponivel ? $"{g.Temp:0} °C" : "-- °C";
            lblTemp.ForeColor = Sensores.Cor(faixa);
            var info = new List<string>();
            if (g.Ventoinha >= 0) info.Add($"Ventoinha {g.Ventoinha:0}%");
            if (g.Clock >= 0) info.Add($"{g.Clock:0} MHz");
            if (g.Watts >= 0) info.Add($"{g.Watts:0} W");
            lblTempInfo.Text = string.Join("\n", info);
            lblTempExplica.Text = Sensores.Explicar(g);
        }
        catch { }
        finally { lendoMonitor = false; }
    }

    /// <summary>O que o console da SEXTA-FEIRA comenta sozinho, em rodízio, a partir das leituras reais.</summary>
    private string ProximaFala()
    {
        for (int tentativa = 0; tentativa < 5; tentativa++)
        {
            switch (falaIndice++ % 5)
            {
                case 0 when ultTemp >= 0:
                    return ultTemp < 60 ? $"GPU a {ultTemp:0} °C. Temperatura tranquila."
                         : ultTemp < 80 ? $"GPU a {ultTemp:0} °C. Normal pra quem tá jogando."
                         : $"GPU a {ultTemp:0} °C. Tá quente: vale olhar a ventilação do gabinete.";
                case 1 when ultRam >= 0:
                    return ultRam >= 80 ? $"Memória em {ultRam:0}% de uso. Vale otimizar antes de jogar."
                         : $"Memória em {ultRam:0}% de uso. Tem espaço de sobra pro jogo.";
                case 2 when nucleo.Nota >= 0:
                    return nucleo.Nota >= 100 ? "Sistema 100% otimizado. Tudo pronto pra jogar."
                         : $"Sistema {nucleo.Nota}% otimizado. Os ajustes que faltam estão na Checagem.";
                case 3 when ultTopNome != null:
                    return $"{ultTopNome} é o que mais usa memória agora ({ultTopTam}).";
                case 4:
                    return "Clica no núcleo ou em Otimizar agora antes de abrir o jogo.";
            }
        }
        return "Monitorando o sistema.";
    }

    private async Task AtualizarSaude()
    {
        try
        {
            var (analise, discos) = await Task.Run(() => (Saude.Analisar(), Saude.LerDiscos()));
            lblSaudeTitulo.Text = analise.Titulo;
            lblSaudeTitulo.ForeColor = analise.Status switch
            {
                Status.Ok => Tema.Verde,
                Status.Alerta => Tema.Amarelo,
                _ => Tema.Destaque
            };

            var linhas = new List<string>(analise.Detalhes);
            if (discos.Count > 0)
            {
                linhas.Add("");
                foreach (var d in discos)
                    linhas.Add($"{(d.Status == Status.Alerta ? "⚠" : "💾")} {d.Tipo} {d.Nome}: {Saude.Descrever(d)}");
            }
            lblSaudeDet.Text = string.Join("\n", linhas);
            lblSaudeDet.ForeColor = discos.Any(d => d.Status == Status.Alerta) ? Tema.Amarelo : Tema.Apagado;
        }
        catch (Exception ex)
        {
            Atualizador.Log("ERRO na saúde: " + ex);
        }
    }

    // =====================================================================
    //  Página: Limpeza 1 clique
    // =====================================================================

    private class LinhaLimpeza
    {
        public ItemLimpeza Item;
        public CheckBox Caixa;
        public Label Tamanho;
        public Label Status;
        public Button Botao;
        public long Bytes = -1;
    }

    private readonly Panel listaLimpeza = new();
    private readonly List<LinhaLimpeza> linhasLimpeza = new();
    private readonly Label lblLimpResumo = Tema.Rotulo("", Tema.Negrito, Tema.Texto);
    private Button btnLimparSelecionados, btnCalcular;
    private bool limpando;

    private Panel MontarLimpeza()
    {
        var p = NovaPagina("Limpeza 1 clique",
            "Marca o que tu quer que o botão OTIMIZAR e o raio da barra façam. Fica salvo. Cada item também roda sozinho no botão Executar.");

        lblLimpResumo.Location = new Point(33, 108);
        p.Controls.Add(lblLimpResumo);

        btnCalcular = Tema.Botao("Calcular tamanhos", Tema.CardClaro, 170, 36);
        btnCalcular.Location = new Point(530, 100);
        btnCalcular.Click += async (_, _) => await CalcularTamanhos();
        p.Controls.Add(btnCalcular);

        btnLimparSelecionados = Tema.Botao("🧹 Limpar selecionados", Tema.Destaque, 220, 36);
        btnLimparSelecionados.Location = new Point(710, 100);
        btnLimparSelecionados.Click += async (_, _) => await LimparSelecionados();
        p.Controls.Add(btnLimparSelecionados);

        listaLimpeza.Location = new Point(30, 150);
        listaLimpeza.Size = new Size(904, 590);
        listaLimpeza.BackColor = Tema.Card;
        listaLimpeza.AutoScroll = true;
        Tema.Escurecer(listaLimpeza);
        p.Controls.Add(listaLimpeza);
        return p;
    }

    private void PreencherLimpeza()
    {
        listaLimpeza.SuspendLayout();
        listaLimpeza.Controls.Clear();
        linhasLimpeza.Clear();
        var cfg = Configuracao.Atual;
        int y = 10;

        foreach (var grupo in Limpeza.Itens.Where(i => { try { return i.Disponivel(); } catch { return false; } }).GroupBy(i => i.Categoria))
        {
            var cab = Tema.Rotulo($"{grupo.Key}  ·  {grupo.Count()}", new Font(Hud.FonteForte, 9.5f), Hud.CianoClaro);
            cab.Location = new Point(16, y + 4);
            listaLimpeza.Controls.Add(cab);
            y += 30;

            foreach (var item in grupo)
            {
                var linha = new LinhaLimpeza { Item = item };

                var caixa = new CheckBox
                {
                    Location = new Point(18, y + 18),
                    Size = new Size(18, 18),
                    Checked = !item.SoManual && cfg.Marcado(item.Id),
                    Enabled = !item.SoManual,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Tema.Card,
                    Cursor = Cursors.Hand
                };
                caixa.FlatAppearance.BorderColor = Tema.Apagado;
                caixa.FlatAppearance.CheckedBackColor = Tema.Destaque;
                string id = item.Id;
                caixa.CheckedChanged += (_, _) => { Configuracao.Atual.Marcar(id, caixa.Checked); AtualizarResumoLimpeza(); };
                listaLimpeza.Controls.Add(caixa);
                linha.Caixa = caixa;

                var icone = Tema.Rotulo(item.Icone ?? "•", new Font("Segoe UI Emoji", 15f), Tema.Texto);
                icone.Location = new Point(46, y + 10);
                listaLimpeza.Controls.Add(icone);

                var nome = Tema.Rotulo(item.Nome, Tema.Negrito, Tema.Texto);
                nome.Location = new Point(86, y + 6);
                listaLimpeza.Controls.Add(nome);

                string prefixo = item.Risco == Risco.Revisao ? "Pede revisão" : item.SoManual ? "Só manual" : null;
                int xDesc = 88;
                if (prefixo != null)
                {
                    var tag = Tema.Rotulo(prefixo + "  •", Tema.Pequena, Tema.Amarelo);
                    tag.Location = new Point(88, y + 31);
                    listaLimpeza.Controls.Add(tag);
                    xDesc += tag.PreferredWidth + 2;
                }
                var desc = Tema.Rotulo(item.Descricao, Tema.Pequena, Tema.Apagado);
                desc.Location = new Point(xDesc, y + 31);
                desc.AutoSize = false;
                desc.Size = new Size(560 - xDesc, 18);
                desc.AutoEllipsis = true;
                listaLimpeza.Controls.Add(desc);
                new ToolTip().SetToolTip(desc, item.Descricao);

                var status = Tema.Rotulo("", Tema.Pequena, Tema.Verde);
                status.Location = new Point(570, y + 19);
                listaLimpeza.Controls.Add(status);
                linha.Status = status;

                var tam = Tema.Rotulo(item.EhAcao ? "" : "...", Tema.Pequena, Tema.Apagado);
                tam.Location = new Point(690, y + 19);
                listaLimpeza.Controls.Add(tam);
                linha.Tamanho = tam;

                var b = Tema.Botao("▶ Executar", Tema.CardClaro, 110, 32);
                b.Font = Tema.Pequena;
                b.Location = new Point(772, y + 12);
                b.Click += async (_, _) => await ExecutarItem(linha);
                listaLimpeza.Controls.Add(b);
                linha.Botao = b;

                linhasLimpeza.Add(linha);
                listaLimpeza.Controls.Add(new Panel { BackColor = Color.FromArgb(22, 32, 50), Location = new Point(16, y + 54), Size = new Size(860, 1) });
                y += 58;
            }
            y += 8;
        }
        listaLimpeza.ResumeLayout();
        AtualizarResumoLimpeza();
    }

    private void AtualizarResumoLimpeza()
    {
        var marcadas = linhasLimpeza.Where(l => l.Caixa.Checked).ToList();
        long total = marcadas.Where(l => l.Bytes > 0).Sum(l => l.Bytes);
        lblLimpResumo.Text = $"{marcadas.Count} de {linhasLimpeza.Count(l => !l.Item.SoManual)} marcados no 1 clique" +
                             (total > 0 ? $"  •  até {Sistema.Formatar(total)} pra liberar" : "");
    }

    private async Task CalcularTamanhos()
    {
        btnCalcular.Enabled = false;
        foreach (var l in linhasLimpeza.Where(l => !l.Item.EhAcao))
        {
            l.Tamanho.Text = "calculando...";
            var item = l.Item;
            long t = await Task.Run(() => { try { return item.Tamanho(); } catch { return -1L; } });
            l.Bytes = t;
            l.Tamanho.Text = t < 0 ? "varia" : t == 0 ? "vazio" : "até " + Sistema.Formatar(t);
            l.Tamanho.ForeColor = t > 500L * 1024 * 1024 ? Tema.Amarelo : Tema.Apagado;
        }
        AtualizarResumoLimpeza();
        btnCalcular.Enabled = true;
    }

    private async Task ExecutarItem(LinhaLimpeza l)
    {
        if (limpando) return;
        var item = l.Item;

        if (item.Risco == Risco.Revisao &&
            MessageBox.Show($"{item.Nome}\n\n{item.Descricao}\n\nQuer continuar?", "Pede revisão",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        if (Limpeza.EstaAberto(item) && item.Id != "discordsessao" &&
            MessageBox.Show($"O programa ({item.AvisoAberto}) tá aberto, então alguns arquivos estão em uso.\n\n" +
                            "Fecha ele pra limpar tudo, ou continua pra limpar só o que der.\n\nContinuar agora?",
                "SEXTA-FEIRA", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;

        limpando = true;
        l.Botao.Enabled = false;
        l.Botao.Text = "Rodando...";
        l.Status.ForeColor = Tema.Apagado;
        l.Status.Text = "";
        try
        {
            string resultado = await Task.Run(() =>
            {
                switch (item.Id)
                {
                    case "fechar":
                        var f = Otimizador.FecharProgramas();
                        return f.Count == 0 ? "✓ nada aberto" : $"✓ {f.Count} fechado(s)";
                    case "ajustesjogo": Otimizador.AjustesDeJogo(); return "✓ aplicado";
                    case "energia": return Otimizador.AtivarAltoDesempenho() ? "✓ ativo" : "não deu";
                    case "mouse": Ajustes.DesligarAceleracaoMouse(); return "✓ desligada";
                    case "usb": Ajustes.DesligarUsbSeletiva(); return "✓ desligada";
                    case "throttling": Ajustes.DesligarThrottling(); return "✓ desligado";
                    case "ram":
                        double antes = Otimizador.RamEmEspera();
                        Otimizador.LiberarRamEmEspera();
                        Thread.Sleep(600);
                        return $"✓ {Sistema.Formatar(Math.Max(0, antes - Otimizador.RamEmEspera()))} liberados";
                    default:
                        long t = item.Executar();
                        Atualizador.Log($"Limpeza manual '{item.Id}': {Sistema.Formatar(t)}");
                        return $"✓ {Sistema.Formatar(t)} liberados";
                }
            });
            l.Status.ForeColor = Tema.Verde;
            l.Status.Text = resultado;
            l.Botao.Text = "Concluído";
            if (!item.EhAcao)
            {
                long novo = await Task.Run(() => { try { return item.Tamanho(); } catch { return -1L; } });
                l.Bytes = novo;
                l.Tamanho.Text = novo < 0 ? "varia" : novo == 0 ? "vazio" : "até " + Sistema.Formatar(novo);
            }
        }
        catch (Exception ex)
        {
            l.Status.ForeColor = Tema.Vermelho;
            l.Status.Text = "erro";
            l.Botao.Text = "▶ Executar";
            Atualizador.Log($"ERRO limpeza '{item.Id}': {ex}");
        }
        finally
        {
            l.Botao.Enabled = true;
            limpando = false;
            AtualizarResumoLimpeza();
        }
    }

    private async Task LimparSelecionados()
    {
        if (limpando) return;
        limpando = true;
        btnLimparSelecionados.Enabled = false;
        string antes = lblLimpResumo.Text;
        try
        {
            var r = await Task.Run(() => Otimizador.Executar((pct, txt) =>
                BeginInvoke(new Action(() => lblLimpResumo.Text = $"{pct}%  {txt}"))));
            lblLimpResumo.Text = "✅ " + r.Frase();
            foreach (var l in linhasLimpeza.Where(l => l.Caixa.Checked))
            {
                l.Status.ForeColor = Tema.Verde;
                l.Status.Text = "✓ feito";
                l.Botao.Text = "Concluído";
            }
            AtualizarContador();
        }
        catch (Exception ex)
        {
            lblLimpResumo.Text = "Algo deu errado: " + ex.Message;
        }
        finally
        {
            limpando = false;
            btnLimparSelecionados.Enabled = true;
        }
        await CalcularTamanhos();
    }

    // =====================================================================
    //  Página: Checagem
    // =====================================================================

    private Panel MontarChecagem()
    {
        var p = NovaPagina("Checagem do PC", "Confere o que segura teu FPS. Amarelo = vale arrumar. Azul = opcional, é escolha tua.");

        lblResumoChecagem.Location = new Point(33, 108);
        p.Controls.Add(lblResumoChecagem);

        var corrigir = Tema.Botao("Corrigir automático", Tema.Destaque, 180, 38);
        corrigir.Location = new Point(560, 100);
        corrigir.Click += async (_, _) =>
        {
            foreach (var c in checagens.Where(c => c.Automatica && c.Status != Status.Ok))
                try { c.Acao?.Invoke(); } catch { }
            await RodarChecagem();
        };
        p.Controls.Add(corrigir);

        var denovo = Tema.Botao("Verificar de novo", Tema.CardClaro, 170, 38);
        denovo.Location = new Point(752, 100);
        denovo.Click += async (_, _) => await RodarChecagem();
        p.Controls.Add(denovo);

        listaChecagem.Location = new Point(30, 154);
        listaChecagem.Size = new Size(900, 560);
        listaChecagem.BackColor = Tema.Card;
        listaChecagem.AutoScroll = true;
        Tema.Escurecer(listaChecagem);
        p.Controls.Add(listaChecagem);
        return p;
    }

    private async Task RodarChecagem()
    {
        lblResumoChecagem.Text = "Verificando...";
        lblResumoChecagem.ForeColor = Tema.Apagado;
        var info = pc;
        var js = jogos;
        checagens = await Task.Run(() => Checagens.Rodar(info, js));

        listaChecagem.SuspendLayout();
        listaChecagem.Controls.Clear();
        int y = 8;
        foreach (var c in checagens)
        {
            var cor = c.Status switch { Status.Ok => Tema.Verde, Status.Alerta => Tema.Amarelo, _ => Tema.Destaque };

            var ponto = Tema.Rotulo("●", Tema.Negrito, cor);
            ponto.Location = new Point(16, y + 4);
            listaChecagem.Controls.Add(ponto);

            var nome = Tema.Rotulo(c.Nome, Tema.Negrito);
            nome.Location = new Point(40, y + 2);
            listaChecagem.Controls.Add(nome);

            var det = Tema.Rotulo(c.Detalhe, Tema.Pequena, Tema.Apagado);
            det.Location = new Point(40, y + 26);
            det.MaximumSize = new Size(640, 0);
            listaChecagem.Controls.Add(det);

            if (c.Botao != null && c.Status != Status.Ok && c.Acao != null)
            {
                var b = Tema.Botao(c.Botao, c.Status == Status.Alerta ? Tema.Amarelo : Tema.CardClaro, 150, 34);
                b.Font = Tema.Pequena;
                b.Location = new Point(710, y + 8);
                var acao = c.Acao;
                b.Click += async (_, _) =>
                {
                    string msg = null;
                    try { msg = acao(); } catch (Exception ex) { msg = "Erro: " + ex.Message; }
                    if (msg != null) MessageBox.Show(msg, "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await RodarChecagem();
                };
                listaChecagem.Controls.Add(b);
            }
            listaChecagem.Controls.Add(new Panel { BackColor = Color.FromArgb(22, 32, 50), Location = new Point(16, y + 57), Size = new Size(850, 1) });
            y += 62;
        }
        listaChecagem.ResumeLayout();

        int alertas = checagens.Count(c => c.Status == Status.Alerta);
        int nota = Checagens.Nota(checagens);
        lblNota.Text = $"{nota}%";
        nucleo.Nota = nota;
        lblNota.ForeColor = nota >= 90 ? Tema.Verde : nota >= 70 ? Tema.Destaque : Tema.Amarelo;
        lblNotaTxt.Text = nota >= 100 ? "Tudo no ponto pra jogar! 🔥"
            : $"{nota}% otimizado. {alertas} coisa(s) pra arrumar na Checagem" +
              (checagens.Any(c => c.Status == Status.Alerta && c.Acao == null) ? " (algumas só na BIOS ou no hardware)." : ".");
        lblResumoChecagem.Text = alertas == 0 ? "✅ Tudo certo no teu PC!" : $"⚠ {alertas} ponto(s) de atenção";
        lblResumoChecagem.ForeColor = alertas == 0 ? Tema.Verde : Tema.Amarelo;
    }

    // =====================================================================
    //  Página: Biblioteca
    // =====================================================================

    private List<Biblioteca.JogoInstalado> biblioteca = new();
    private readonly ListBox listaBiblioteca = new();
    private readonly Panel detalheJogo = new();
    private readonly Label lblNivel = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private NivelPC nivel = NivelPC.Entrada;

    private Panel MontarJogos()
    {
        var p = NovaPagina("Meus jogos", null);
        lblNivel.Location = new Point(33, 72);
        lblNivel.MaximumSize = new Size(LarguraUtil, 0);
        lblNivel.Text = "Procurando teus jogos...";
        p.Controls.Add(lblNivel);

        listaBiblioteca.Location = new Point(30, 108);
        listaBiblioteca.Size = new Size(250, 620);
        listaBiblioteca.BackColor = Tema.Card;
        listaBiblioteca.ForeColor = Tema.Texto;
        listaBiblioteca.BorderStyle = BorderStyle.None;
        listaBiblioteca.Font = Tema.Normal;
        listaBiblioteca.DrawMode = DrawMode.OwnerDrawFixed;
        listaBiblioteca.ItemHeight = 46;
        listaBiblioteca.DrawItem += DesenharItemBiblioteca;
        Tema.Escurecer(listaBiblioteca);
        listaBiblioteca.SelectedIndexChanged += (_, _) => MostrarJogo();
        p.Controls.Add(listaBiblioteca);

        detalheJogo.Location = new Point(296, 108);
        detalheJogo.Size = new Size(634, 620);
        detalheJogo.BackColor = Tema.Card;
        detalheJogo.AutoScroll = true;
        Tema.Escurecer(detalheJogo);
        p.Controls.Add(detalheJogo);
        return p;
    }

    private void DesenharItemBiblioteca(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= biblioteca.Count) return;
        var j = biblioteca[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var fundo = new SolidBrush(sel ? Tema.CardClaro : Tema.Card))
            e.Graphics.FillRectangle(fundo, e.Bounds);
        if (sel)
            using (var barra = new SolidBrush(Tema.Destaque))
                e.Graphics.FillRectangle(barra, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);

        var corNome = j.Config != null ? Tema.Texto : Tema.Apagado;
        TextRenderer.DrawText(e.Graphics, j.Nome, Tema.Negrito, new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 5, e.Bounds.Width - 20, 20),
            corNome, TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
        string sub = j.Plataforma + (j.Config != null ? "  •  perfil competitivo" : "  •  dicas gerais");
        TextRenderer.DrawText(e.Graphics, sub, Tema.Pequena, new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 25, e.Bounds.Width - 20, 18),
            j.Config != null ? Tema.Destaque : Tema.Apagado, TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
    }

    private void PreencherJogos()
    {
        lblNivel.Text = $"{biblioteca.Count} jogo(s) encontrado(s)  •  Teu PC está no nível {ConfigsCompetitivas.NomeNivel(nivel)} ({pc.Gpu}). " +
                        "As configs são sempre pra competitivo: FPS alto, menos atraso e mais visibilidade.";
        listaBiblioteca.BeginUpdate();
        listaBiblioteca.Items.Clear();
        foreach (var j in biblioteca) listaBiblioteca.Items.Add(j.Nome);
        listaBiblioteca.EndUpdate();
        if (biblioteca.Count > 0) listaBiblioteca.SelectedIndex = 0;
        else MostrarJogo();
    }

    private void MostrarJogo()
    {
        detalheJogo.SuspendLayout();
        detalheJogo.Controls.Clear();
        int i = listaBiblioteca.SelectedIndex;

        if (i < 0 || i >= biblioteca.Count)
        {
            var vazio = Tema.Rotulo(biblioteca.Count == 0 ? "Não achei jogos instalados (Steam, Epic, Riot, Ubisoft, EA, Battle.net)." : "Escolhe um jogo na lista.",
                Tema.Normal, Tema.Apagado);
            vazio.Location = new Point(20, 20);
            detalheJogo.Controls.Add(vazio);
            detalheJogo.ResumeLayout();
            return;
        }

        var j = biblioteca[i];
        var c = j.Config;
        int y = 18;

        Label Add(string texto, Font f, Color cor, int x = 22, int largura = 590)
        {
            var l = Tema.Rotulo(texto, f, cor);
            l.Location = new Point(x, y);
            l.MaximumSize = new Size(largura, 0);
            detalheJogo.Controls.Add(l);
            y += l.PreferredHeight + 6;
            return l;
        }

        Add(j.Nome, Tema.Subtitulo, Tema.Destaque);
        Add($"{j.Plataforma}  •  {j.Disco}  •  {j.Pasta}", Tema.Pequena, j.Disco == "HD" || j.Disco == "HDD" ? Tema.Amarelo : Tema.Apagado);
        if (j.Disco == "HDD")
            Add("⚠ Tá no HD: move pro SSD pelo launcher pra carregar mais rápido e travar menos.", Tema.Pequena, Tema.Amarelo);
        y += 8;

        if (c == null)
        {
            Add("Esse jogo ainda não tem perfil próprio. Dicas competitivas que valem pra quase todo jogo:", Tema.Normal, Tema.Texto);
            y += 4;
            foreach (var d in ConfigsCompetitivas.DicasGerais) Add("•  " + d, Tema.Normal, Tema.Texto, 30);
            y += 10;
            Add("Quer um perfil completo pra ele? Me fala no chat que eu pesquiso e coloco na próxima atualização.", Tema.Pequena, Tema.Apagado);
        }
        else
        {
            if (c.Aviso != null) { Add("⚠ " + c.Aviso, Tema.Pequena, Tema.Amarelo); y += 4; }

            Add($"🎯 PRO TEU PC (nível {ConfigsCompetitivas.NomeNivel(nivel)})", new Font("Segoe UI Semibold", 9f), Tema.Apagado);
            if (c.PorNivel.TryGetValue(nivel, out string doNivel)) Add(doNivel, Tema.Negrito, Tema.Verde, 30);
            y += 10;

            Add("🎮 NO JOGO", new Font("Segoe UI Semibold", 9f), Tema.Apagado);
            foreach (var d in c.Jogo) Add("•  " + d, Tema.Normal, Tema.Texto, 30);
            y += 10;

            Add("💻 NO PC", new Font("Segoe UI Semibold", 9f), Tema.Apagado);
            Add("•  Clica em OTIMIZAR (ou liga o Modo Competitivo) antes de abrir o jogo", Tema.Normal, Tema.Texto, 30);
            foreach (var d in c.Pc) Add("•  " + d, Tema.Normal, Tema.Texto, 30);

            if (c.PerfilAutomatico && c.Nome is "Fortnite" or "Counter-Strike 2" or "Valorant")
            {
                y += 10;
                bool ativo = c.Nome switch
                {
                    "Fortnite" => Jogos.FortniteEmModoDesempenho(),
                    "Counter-Strike 2" => Jogos.Cs2Otimizado(),
                    _ => Jogos.ValorantOtimizado()
                };
                string textoBotao = c.Nome == "Fortnite"
                    ? (ativo ? "✅ Modo Desempenho ativo" : "Aplicar Modo Desempenho")
                    : (ativo ? "✅ Config competitiva aplicada" : "⚡ Aplicar config competitiva");
                var b = Tema.Botao(textoBotao, ativo ? Tema.Verde : Tema.Destaque, 280, 38);
                b.Location = new Point(22, y);
                string nomeJogo = c.Nome;
                b.Click += (_, _) =>
                {
                    string msg = nomeJogo switch
                    {
                        "Fortnite" => Jogos.AplicarPerfilFortnite(),
                        "Counter-Strike 2" => Jogos.AplicarPerfilCs2(),
                        _ => Jogos.AplicarPerfilValorant()
                    };
                    MessageBox.Show(msg, "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    MostrarJogo();
                };
                detalheJogo.Controls.Add(b);
                y += 48;
            }
        }
        detalheJogo.ResumeLayout();
    }

    // =====================================================================
    //  Página: Competitivo
    // =====================================================================

    private Button btnCompetitivo;
    private readonly Label lblCompStatus = Tema.Rotulo("Desligado", Tema.Subtitulo, Tema.Apagado);
    private readonly Label lblCompFeito = Tema.Rotulo("", Tema.Normal, Tema.Texto);
    private readonly Label lblCompJogo = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private readonly Label lblConexao = Tema.Rotulo("Ainda não testada.", Tema.Normal, Tema.Apagado);
    private readonly Label lblConexaoNota = Tema.Rotulo("", new Font(Hud.FonteForte, 20f), Tema.Apagado);
    private readonly System.Windows.Forms.Timer timerComp = new() { Interval = 4000 };
    private ModoCompetitivo.Conexao conexaoAntes;

    private Panel MontarCompetitivo()
    {
        var p = NovaPagina("Modo Competitivo",
            "Liga antes da partida e deixa a SEXTA-FEIRA aberta (pode minimizar). Desliga quando terminar de jogar.");

        var card = Card(30, 104, 900, 300);
        btnCompetitivo = Tema.Botao("🏆  LIGAR MODO COMPETITIVO", Tema.Destaque, 340, 64);
        btnCompetitivo.Font = new Font(Hud.FonteForte, 13f);
        btnCompetitivo.Location = new Point(24, 24);
        btnCompetitivo.Click += async (_, _) => await AlternarCompetitivo();
        card.Controls.Add(btnCompetitivo);

        lblCompStatus.Location = new Point(390, 28);
        card.Controls.Add(lblCompStatus);
        lblCompJogo.Location = new Point(392, 62);
        card.Controls.Add(lblCompJogo);

        var oQueFaz = Tema.Rotulo(
            "O que ele faz:  otimização completa  •  jogo com prioridade acima do normal  •  navegador, launchers e OneDrive com prioridade baixa\n" +
            "•  pausa os downloads do Windows Update e a sincronização do OneDrive (são os que mais causam pico de ping)",
            Tema.Pequena, Tema.Apagado);
        oQueFaz.Location = new Point(24, 104);
        oQueFaz.MaximumSize = new Size(850, 0);
        card.Controls.Add(oQueFaz);

        var chkRam = new CheckBox
        {
            Text = "Limpar a RAM em espera sozinho durante a partida (quando a RAM livre ficar abaixo de 1 GB)",
            Checked = Configuracao.Atual.RamContinua,
            Location = new Point(24, 268),
            AutoSize = true,
            ForeColor = Tema.Texto,
            Font = Tema.Pequena,
            BackColor = Tema.Card,
            Cursor = Cursors.Hand
        };
        chkRam.CheckedChanged += (_, _) => { Configuracao.Atual.RamContinua = chkRam.Checked; Configuracao.Atual.Salvar(); };
        card.Controls.Add(chkRam);

        lblCompFeito.Location = new Point(24, 150);
        lblCompFeito.MaximumSize = new Size(850, 0);
        card.Controls.Add(lblCompFeito);
        p.Controls.Add(card);

        // Conexão
        var net = Card(30, 420, 900, 230, "Estabilidade da tua internet");
        lblConexaoNota.Location = new Point(18, 40);
        net.Controls.Add(lblConexaoNota);
        lblConexao.Location = new Point(20, 84);
        lblConexao.MaximumSize = new Size(560, 0);
        net.Controls.Add(lblConexao);

        var testar = Tema.Botao("Testar conexão", Tema.CardClaro, 170, 36);
        testar.Location = new Point(710, 40);
        testar.Click += async (_, _) =>
        {
            testar.Enabled = false;
            await TestarConexao(null);
            testar.Enabled = true;
        };
        net.Controls.Add(testar);

        var verdade = Tema.Rotulo(
            "Sendo sincero: nenhum programa diminui o ping de verdade, porque ele depende da distância até o servidor e do teu plano de internet. " +
            "O que dá pra fazer é tirar os PICOS (jitter) causados por coisas baixando escondidas. É isso que o Modo Competitivo faz. " +
            "Dica de ouro: jogar no cabo em vez do Wi-Fi corta o jitter na hora.",
            Tema.Pequena, Tema.Apagado);
        verdade.Location = new Point(20, 150);
        verdade.MaximumSize = new Size(860, 0);
        net.Controls.Add(verdade);
        p.Controls.Add(net);

        timerComp.Tick += (_, _) =>
        {
            Task.Run(() => ModoCompetitivo.Atualizar()).ContinueWith(_ => BeginInvoke(new Action(AtualizarTelaCompetitivo)));
        };
        return p;
    }

    private async Task AlternarCompetitivo()
    {
        btnCompetitivo.Enabled = false;
        try
        {
            if (!ModoCompetitivo.Ativo)
            {
                lblCompStatus.Text = "Ligando...";
                lblCompStatus.ForeColor = Tema.Destaque;
                conexaoAntes = await Task.Run(() => ModoCompetitivo.TestarConexao(10));
                var feito = await Task.Run(() => ModoCompetitivo.Ligar());
                lblCompFeito.Text = "✅ " + string.Join("\n✅ ", feito);
                timerComp.Start();
                await TestarConexao(conexaoAntes);
            }
            else
            {
                ModoCompetitivo.Desligar();
                timerComp.Stop();
                lblCompFeito.Text = "Tudo voltou ao normal. Bom jogo! 🎮";
            }
        }
        catch (Exception ex)
        {
            lblCompFeito.Text = "Algo deu errado: " + ex.Message;
            Atualizador.Log("ERRO no Modo Competitivo: " + ex);
        }
        finally
        {
            btnCompetitivo.Enabled = true;
            AtualizarTelaCompetitivo();
        }
    }

    private void AtualizarTelaCompetitivo()
    {
        bool on = ModoCompetitivo.Ativo;
        btnCompetitivo.Text = on ? "⏹  DESLIGAR MODO COMPETITIVO" : "🏆  LIGAR MODO COMPETITIVO";
        btnCompetitivo.BackColor = on ? Tema.Vermelho : Tema.Destaque;
        btnCompetitivo.ForeColor = on ? Color.White : Color.FromArgb(10, 12, 20);
        lblCompStatus.Text = on ? "🟢 Ligado" : "Desligado";
        lblCompStatus.ForeColor = on ? Tema.Verde : Tema.Apagado;
        lblCompJogo.Text = !on ? "" : (ModoCompetitivo.JogoAtual != null
            ? $"Jogo detectado: {ModoCompetitivo.JogoAtual} (com prioridade)  •  ligado desde {ModoCompetitivo.LigadoEm:HH:mm}"
            : $"Esperando tu abrir o jogo...  •  ligado desde {ModoCompetitivo.LigadoEm:HH:mm}") +
            (ModoCompetitivo.LimpezasRam > 0 ? $"  •  RAM limpa {ModoCompetitivo.LimpezasRam}x ({Sistema.Formatar(ModoCompetitivo.RamLiberadaTotal)})" : "");
        abas["competitivo"].Text = on ? "Competitivo  🟢" : "Competitivo";
    }

    private async Task TestarConexao(ModoCompetitivo.Conexao antes)
    {
        lblConexaoNota.Text = "Testando...";
        lblConexaoNota.ForeColor = Tema.Apagado;
        lblConexao.Text = "Mandando 20 pings...";
        var c = await Task.Run(() => ModoCompetitivo.TestarConexao());
        lblConexaoNota.Text = c.Nota();
        lblConexaoNota.ForeColor = c.Cor();
        string txt = c.Ok
            ? $"Ping médio: {c.Media:0} ms  •  Variação (jitter): {c.Jitter:0.0} ms  •  Pacotes perdidos: {c.Perda:0}%"
            : "Não consegui pingar a internet.";
        if (antes != null && antes.Ok && c.Ok)
            txt += $"\nAntes do modo: jitter {antes.Jitter:0.0} ms  →  agora {c.Jitter:0.0} ms";
        lblConexao.Text = txt;
        lblConexao.ForeColor = Tema.Texto;
    }

    // =====================================================================
    //  Página: Benchmark
    // =====================================================================

    private readonly ComboBox cmbJogoAberto = new();
    private readonly ComboBox cmbDuracao = new();
    private Button btnMedir, btnAntesDepois, btnTestePc;
    private readonly Label lblBenchStatus = Tema.Rotulo("", Tema.Negrito, Tema.Destaque);
    private readonly Label lblFpsMedia = Tema.Rotulo("--", Hud.Leve(30f), Tema.Texto);
    private readonly Label lblFpsLow = Tema.Rotulo("--", Hud.Leve(30f), Tema.Texto);
    private readonly Label lblFpsLow01 = Tema.Rotulo("--", Hud.Leve(30f), Tema.Texto);
    private readonly Label lblFpsComparacao = Tema.Rotulo("", Tema.Normal, Tema.Texto);
    private readonly Label lblTestePc = Tema.Rotulo("Mede o processador e o disco C. Repete de vez em quando: se cair muito, o app avisa.", Tema.Pequena, Tema.Apagado);
    private readonly Label lblHistorico = Tema.Rotulo("", Tema.Mono, Tema.Texto);
    private bool medindo;

    private Panel MontarBenchmark()
    {
        var p = NovaPagina("Benchmark", "FPS medido de verdade dentro do jogo, com o mesmo método dos sites de review. Não mexe no jogo: seguro com anti-cheat.");

        // ---------- FPS ----------
        var fps = Card(30, 104, 900, 300, "FPS real no jogo");
        var dica = Tema.Rotulo("1) Abre o jogo e entra numa partida/treino   2) Volta aqui (Alt+Tab)   3) Clica em medir e volta pro jogo", Tema.Pequena, Tema.Apagado);
        dica.Location = new Point(18, 36);
        fps.Controls.Add(dica);

        cmbJogoAberto.Location = new Point(18, 64);
        cmbJogoAberto.Size = new Size(280, 30);
        cmbJogoAberto.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbJogoAberto.FlatStyle = FlatStyle.Flat;
        cmbJogoAberto.BackColor = Tema.CardClaro;
        cmbJogoAberto.ForeColor = Tema.Texto;
        fps.Controls.Add(cmbJogoAberto);

        var atualizar = Tema.Botao("↻", Tema.CardClaro, 40, 30);
        atualizar.Location = new Point(304, 63);
        atualizar.Click += async (_, _) => await ProcurarJogosAbertos();
        fps.Controls.Add(atualizar);

        cmbDuracao.Location = new Point(354, 64);
        cmbDuracao.Size = new Size(110, 30);
        cmbDuracao.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbDuracao.FlatStyle = FlatStyle.Flat;
        cmbDuracao.BackColor = Tema.CardClaro;
        cmbDuracao.ForeColor = Tema.Texto;
        cmbDuracao.Items.AddRange(new object[] { "30 segundos", "60 segundos", "90 segundos" });
        cmbDuracao.SelectedIndex = 0;
        fps.Controls.Add(cmbDuracao);

        btnMedir = Tema.Botao("Medir FPS", Tema.Destaque, 150, 36);
        btnMedir.Location = new Point(480, 60);
        btnMedir.Click += async (_, _) => await MedirFps(false);
        fps.Controls.Add(btnMedir);

        btnAntesDepois = Tema.Botao("⚡ Antes x Depois", Tema.Amarelo, 190, 36);
        btnAntesDepois.Location = new Point(640, 60);
        btnAntesDepois.Click += async (_, _) => await MedirFps(true);
        fps.Controls.Add(btnAntesDepois);

        lblBenchStatus.Location = new Point(18, 104);
        fps.Controls.Add(lblBenchStatus);

        int x = 18;
        foreach (var (rot, lbl, expl) in new[]
                 {
                     ("FPS MÉDIO", lblFpsMedia, "o normal"),
                     ("1% LOW", lblFpsLow, "as quedas (o que tu sente como travada)"),
                     ("0,1% LOW", lblFpsLow01, "as piores travadas"),
                 })
        {
            var r = Tema.Rotulo(rot, new Font("Segoe UI Semibold", 8.5f), Tema.Apagado);
            r.Location = new Point(x + 2, 136);
            fps.Controls.Add(r);
            lbl.Location = new Point(x, 154);
            fps.Controls.Add(lbl);
            var e = Tema.Rotulo(expl, Tema.Pequena, Tema.Apagado);
            e.Location = new Point(x + 2, 206);
            fps.Controls.Add(e);
            x += 290;
        }
        lblFpsComparacao.Location = new Point(18, 240);
        lblFpsComparacao.MaximumSize = new Size(860, 0);
        fps.Controls.Add(lblFpsComparacao);
        p.Controls.Add(fps);

        // ---------- Teste do PC ----------
        var teste = Card(30, 420, 440, 300, "Teste do PC");
        btnTestePc = Tema.Botao("Rodar teste (~15s)", Tema.Destaque, 190, 36);
        btnTestePc.Location = new Point(18, 42);
        btnTestePc.Click += async (_, _) => await RodarTestePc();
        teste.Controls.Add(btnTestePc);
        lblTestePc.Location = new Point(18, 92);
        lblTestePc.MaximumSize = new Size(404, 0);
        teste.Controls.Add(lblTestePc);
        p.Controls.Add(teste);

        // ---------- Histórico ----------
        var hist = Card(490, 420, 440, 300, "Últimas medições de FPS");
        lblHistorico.Location = new Point(18, 42);
        lblHistorico.AutoSize = false;
        lblHistorico.Size = new Size(410, 250);
        hist.Controls.Add(lblHistorico);
        p.Controls.Add(hist);
        return p;
    }

    private List<(string Nome, string Processo)> jogosAbertos = new();

    private async Task ProcurarJogosAbertos()
    {
        var bib = biblioteca;
        jogosAbertos = await Task.Run(() => Benchmark.JogosAbertos(bib));
        cmbJogoAberto.Items.Clear();
        if (jogosAbertos.Count == 0) cmbJogoAberto.Items.Add("Nenhum jogo aberto");
        foreach (var j in jogosAbertos) cmbJogoAberto.Items.Add(j.Nome);
        cmbJogoAberto.SelectedIndex = 0;
    }

    private void MostrarFps(Benchmark.Fps r)
    {
        lblFpsMedia.Text = $"{r.Media:0}";
        lblFpsLow.Text = $"{r.Low1:0}";
        lblFpsLow01.Text = $"{r.Low01:0}";
        lblFpsMedia.ForeColor = Tema.Destaque;
        lblFpsLow.ForeColor = r.Low1 >= r.Media * 0.6 ? Tema.Verde : Tema.Amarelo;
        lblFpsLow01.ForeColor = r.Low01 >= r.Media * 0.4 ? Tema.Verde : Tema.Amarelo;
    }

    private async Task MedirFps(bool antesDepois)
    {
        if (medindo) return;
        await ProcurarJogosAbertos();
        if (jogosAbertos.Count == 0)
        {
            MessageBox.Show("Abre o jogo primeiro (de preferência numa partida ou no treino) e clica em medir de novo.", "SEXTA-FEIRA");
            return;
        }
        if (!Benchmark.TemPresentMon)
        {
            MessageBox.Show("O medidor de FPS ainda não chegou nessa versão. Espera a próxima atualização.", "SEXTA-FEIRA");
            return;
        }

        var (nome, processo) = jogosAbertos[Math.Max(0, cmbJogoAberto.SelectedIndex)];
        int seg = (cmbDuracao.SelectedIndex + 1) * 30;
        medindo = true;
        btnMedir.Enabled = btnAntesDepois.Enabled = false;
        lblFpsComparacao.Text = "";

        try
        {
            if (!antesDepois)
            {
                var (r, erro) = await ContarRegressiva(nome, processo, seg, "Medição");
                if (r != null)
                {
                    MostrarFps(r);
                    var anterior = Benchmark.HistoricoFps().Where(h => h.Jogo == nome && h.Data < r.Data).OrderByDescending(h => h.Data).Skip(0).FirstOrDefault();
                    lblFpsComparacao.Text = anterior == null ? $"Primeira medição de {nome} salva." :
                        $"Comparado com a medição anterior de {nome} ({anterior.Data:dd/MM HH:mm}): {Diferenca(r.Media, anterior.Media)} no FPS médio  •  {Diferenca(r.Low1, anterior.Low1)} no 1% low";
                }
                else lblBenchStatus.Text = "⚠ " + erro;
            }
            else
            {
                var (antes, erro1) = await ContarRegressiva(nome, processo, seg, "Antes");
                if (antes == null) { lblBenchStatus.Text = "⚠ " + erro1; return; }
                MostrarFps(antes);

                lblBenchStatus.Text = "Otimizando o PC...";
                await Task.Run(() => Otimizador.Executar());
                lblBenchStatus.Text = "Esperando o PC estabilizar...";
                await Task.Delay(5000);

                var (depois, erro2) = await ContarRegressiva(nome, processo, seg, "Depois");
                if (depois == null) { lblBenchStatus.Text = "⚠ " + erro2; return; }
                MostrarFps(depois);
                lblFpsComparacao.Text =
                    $"ANTES:  {antes.Media:0} FPS  •  1% low {antes.Low1:0}\n" +
                    $"DEPOIS: {depois.Media:0} FPS  •  1% low {depois.Low1:0}\n" +
                    $"Resultado: {Diferenca(depois.Media, antes.Media)} no FPS médio e {Diferenca(depois.Low1, antes.Low1)} nas quedas (1% low)." +
                    (Math.Abs(depois.Media - antes.Media) / antes.Media < 0.03
                        ? "\nDiferença pequena é normal se o PC já estava limpo: aí o ganho vem mais das configs do jogo (aba Biblioteca)."
                        : "");
                lblBenchStatus.Text = "✅ Comparação pronta!";
            }
        }
        finally
        {
            medindo = false;
            btnMedir.Enabled = btnAntesDepois.Enabled = true;
            AtualizarHistoricoFps();
        }
    }

    private async Task<(Benchmark.Fps, string)> ContarRegressiva(string nome, string processo, int seg, string etiqueta)
    {
        var tarefa = Task.Run(() => Benchmark.MedirFps(nome, processo, seg, etiqueta));
        int restante = seg;
        while (!tarefa.IsCompleted)
        {
            lblBenchStatus.Text = restante > 0
                ? $"Medindo {etiqueta.ToLowerInvariant()}: {restante}s  (volta pro jogo e joga normal!)"
                : "Calculando...";
            await Task.WhenAny(tarefa, Task.Delay(1000));
            restante--;
        }
        var (r, erro) = await tarefa;
        if (r != null) lblBenchStatus.Text = $"✅ {r.Quadros:N0} quadros medidos em {nome}" + (r.TempGpu > 0 ? $"  •  GPU chegou a {r.TempGpu:0} °C" : "");
        return (r, erro);
    }

    private static string Diferenca(double agora, double antes)
    {
        if (antes <= 0) return "--";
        double pct = (agora - antes) / antes * 100;
        return (pct >= 0 ? "▲ +" : "▼ ") + $"{pct:0.0}%";
    }

    private void AtualizarHistoricoFps()
    {
        var h = Benchmark.HistoricoFps().OrderByDescending(x => x.Data).Take(12).ToList();
        if (h.Count == 0) { lblHistorico.Text = "Nenhuma medição ainda."; return; }
        lblHistorico.Text = string.Join("\n", h.Select(x =>
        {
            string jogo = x.Jogo.Length > 14 ? x.Jogo[..14] : x.Jogo;
            return $"{x.Data:dd/MM HH:mm} {jogo,-14} {x.Media,4:0} fps  1%:{x.Low1,4:0}  {(x.Etiqueta == "Medição" ? "" : x.Etiqueta)}";
        }));
    }

    private async Task RodarTestePc()
    {
        btnTestePc.Enabled = false;
        lblTestePc.ForeColor = Tema.Apagado;
        try
        {
            var t = await Task.Run(() => Benchmark.RodarTeste((pct, txt) =>
                BeginInvoke(new Action(() => lblTestePc.Text = $"{pct}%  {txt}"))));
            string comp = await Task.Run(() => Benchmark.Comparar(t));
            lblTestePc.ForeColor = Tema.Texto;
            lblTestePc.Text =
                $"Processador (1 núcleo): {t.CpuUmNucleo:N0} pontos\n" +
                $"Processador (todos): {t.CpuTodos:N0} pontos\n" +
                $"Disco C: leitura {t.DiscoLeitura:N0} MB/s  •  escrita {t.DiscoEscrita:N0} MB/s\n\n" + comp;
        }
        catch (Exception ex)
        {
            lblTestePc.Text = "Erro no teste: " + ex.Message;
        }
        finally { btnTestePc.Enabled = true; }
    }

    // =====================================================================
    //  Página: Mira dos pros
    // =====================================================================

    private readonly PainelHud listaPros = new();
    private readonly Dictionary<string, Button> abasPros = new();
    private readonly Label lblComoImportar = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);

    private Panel MontarPros()
    {
        var p = NovaPagina("Mira dos pros", "Vê a mira desenhada, copia o código e importa no jogo. O botão ProSettings abre a config completa do jogador.");
        int x = 30;
        foreach (var lj in Pros.Jogos)
        {
            var b = Tema.Botao(lj.Jogo, Tema.CardClaro, 190, 36);
            b.Location = new Point(x, 100);
            string jogo = lj.Jogo;
            b.Click += (_, _) => MostrarPros(jogo);
            p.Controls.Add(b);
            abasPros[lj.Jogo] = b;
            x += 200;
        }
        lblComoImportar.Location = new Point(33, 146);
        lblComoImportar.MaximumSize = new Size(LarguraUtil, 0);
        p.Controls.Add(lblComoImportar);

        listaPros.Location = new Point(26, 192);
        listaPros.Size = new Size(912, 548);
        listaPros.AutoScroll = true;
        Tema.Escurecer(listaPros);
        p.Controls.Add(listaPros);
        MostrarPros(Pros.Jogos[0].Jogo);
        return p;
    }

    private void MostrarPros(string jogo)
    {
        var lj = Pros.Jogos.First(j => j.Jogo == jogo);
        foreach (var (k, b) in abasPros)
        {
            b.BackColor = k == jogo ? Tema.Destaque : Tema.CardClaro;
            b.ForeColor = k == jogo ? Color.FromArgb(10, 12, 20) : Color.White;
        }
        lblComoImportar.Text = $"{lj.ComoImportar}\nFonte: {lj.Fonte}, conferido em {lj.Conferido}. Os pros trocam de mira com frequência.";

        listaPros.SuspendLayout();
        foreach (Control c in listaPros.Controls.Cast<Control>().ToList()) c.Dispose();
        listaPros.Controls.Clear();
        listaPros.AutoScrollPosition = new Point(0, 0);

        int y = 0;
        foreach (var time in lj.Times)
        {
            // cabeçalho do time
            var barra = new Panel { BackColor = time.Cor, Location = new Point(4, y + 8), Size = new Size(4, 22) };
            listaPros.Controls.Add(barra);
            var nomeTime = Tema.Rotulo(time.Nome.ToUpperInvariant(), new Font(Hud.FonteForte, 12f), time.Cor);
            nomeTime.Location = new Point(16, y + 6);
            listaPros.Controls.Add(nomeTime);
            if (time.Obs != null)
            {
                var obs = Tema.Rotulo(time.Obs, Tema.Pequena, Tema.Apagado);
                obs.Location = new Point(nomeTime.PreferredWidth + 28, y + 11);
                listaPros.Controls.Add(obs);
            }
            y += 36;

            for (int i = 0; i < time.Jogadores.Length; i++)
            {
                var pro = time.Jogadores[i];
                int col = i % 2;
                var card = CartaoPro(pro, time, lj.Jogo);
                card.Location = new Point(4 + col * 446, y + 4);
                listaPros.Controls.Add(card);
                if (col == 1 || i == time.Jogadores.Length - 1) y += card.Height + 12;
            }
            y += 14;
        }
        listaPros.ResumeLayout();
    }

    private Panel CartaoPro(Pros.Pro pro, Pros.Time time, string jogo)
    {
        var c = new Panel { Size = new Size(434, 124), BackColor = Tema.Card };

        // prévia da mira
        var previa = new PreviaMira { Location = new Point(10, 10), Size = new Size(104, 104) };
        if (pro.Mira != null)
            previa.Desenho = pro.Mira.StartsWith("CS") ? Mira.LerCs2(pro.Mira) : Mira.LerValorant(pro.Mira);
        if (jogo == "Fortnite") previa.Visible = false;
        c.Controls.Add(previa);
        int x0 = jogo == "Fortnite" ? 16 : 128;

        // bandeira (baixa sozinha)
        var bandeira = new PictureBox { Location = new Point(x0, 14), Size = new Size(30, 20), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Tema.CardClaro };
        c.Controls.Add(bandeira);
        _ = Task.Run(async () =>
        {
            var img = await Bandeiras.Carregar(pro.Pais);
            if (img != null && !bandeira.IsDisposed)
                bandeira.BeginInvoke(new Action(() => { if (!bandeira.IsDisposed) { bandeira.Image = img; bandeira.BackColor = Color.Transparent; } }));
        });

        var nome = Tema.Rotulo(pro.Nome, new Font("Segoe UI Semibold", 12f), Tema.Texto);
        nome.Location = new Point(x0 + 38, 10);
        c.Controls.Add(nome);

        var timeLbl = Tema.Rotulo(time.Nome == "Pros" ? (pro.Pais ?? "").ToUpperInvariant() : time.Nome, Tema.Pequena, time.Cor);
        timeLbl.Location = new Point(x0 + 2, 40);
        c.Controls.Add(timeLbl);

        string info = pro.Detalhe ?? (pro.Mira == null ? "Sem código público confiável: vê no ProSettings" : null);
        if (info != null)
        {
            var det = Tema.Rotulo(info, Tema.Pequena, Tema.Apagado);
            det.Location = new Point(x0 + 2, 60);
            det.MaximumSize = new Size(434 - x0 - 12, 0);
            c.Controls.Add(det);
        }

        int bx = x0;
        if (pro.Mira != null)
        {
            var copiar = Tema.Botao("Copiar mira", Tema.Destaque, 130, 30);
            copiar.Font = Tema.Pequena;
            copiar.Location = new Point(bx, 84);
            string codigo = pro.Mira;
            copiar.Click += (s, _) =>
            {
                try { Clipboard.SetText(codigo); } catch { }
                var bt = (Button)s;
                bt.Text = "✅ Copiado!";
                bt.BackColor = Tema.Verde;
            };
            c.Controls.Add(copiar);
            bx += 140;
        }

        var ps = Tema.Botao("ProSettings ↗", Tema.CardClaro, 140, 30);
        ps.Font = Tema.Pequena;
        ps.Location = new Point(bx, 84);
        string url = $"https://prosettings.net/players/{pro.Slug}/";
        ps.Click += (_, _) => Sistema.Abrir(url);
        c.Controls.Add(ps);
        return c;
    }

    // =====================================================================
    //  Página: Reparos
    // =====================================================================

    private bool reparando;

    private Panel MontarReparos()
    {
        var p = NovaPagina("Reparos", "Consertos rápidos com as ferramentas oficiais do Windows. Clica no problema que tu tá tendo.");
        var lista = new PainelHud { Location = new Point(26, 96), Size = new Size(912, 648), AutoScroll = true };
        Tema.Escurecer(lista);
        p.Controls.Add(lista);

        int i = 0;
        foreach (var r in Reparos.Lista)
        {
            int col = i % 2, lin = i / 2;
            var c = new Panel { Location = new Point(4 + col * 446, 4 + lin * 118), Size = new Size(434, 106), BackColor = Tema.Card };

            var ic = Tema.Rotulo(r.Icone, new Font("Segoe UI Emoji", 16f), Tema.Texto);
            ic.Location = new Point(14, 12);
            c.Controls.Add(ic);

            var nome = Tema.Rotulo(r.Nome, Tema.Negrito, Tema.Texto);
            nome.Location = new Point(56, 12);
            c.Controls.Add(nome);

            var desc = Tema.Rotulo(r.Descricao, Tema.Pequena, Tema.Apagado);
            desc.Location = new Point(56, 36);
            desc.AutoSize = false;
            desc.Size = new Size(366, 34);
            desc.AutoEllipsis = true;
            new ToolTip().SetToolTip(desc, r.Descricao);
            c.Controls.Add(desc);

            string tags = "⏱ " + r.Duracao + (r.PedeReinicio ? "  •  pede reiniciar" : "");
            var tg = Tema.Rotulo(tags, Tema.Pequena, r.Revisao ? Tema.Amarelo : Tema.Apagado);
            tg.Location = new Point(56, 76);
            c.Controls.Add(tg);

            var b = Tema.Botao("Reparar", Tema.CardClaro, 100, 30);
            b.Font = Tema.Pequena;
            b.Location = new Point(320, 70);
            var reparo = r;
            b.Click += async (_, _) => await RodarReparo(reparo, b);
            c.Controls.Add(b);

            lista.Controls.Add(c);
            i++;
        }
        return p;
    }

    private async Task RodarReparo(Reparo r, Button b)
    {
        if (reparando)
        {
            MessageBox.Show("Espera o reparo atual terminar.", "SEXTA-FEIRA");
            return;
        }
        if (r.Revisao && MessageBox.Show($"{r.Nome}\n\n{r.Aviso ?? r.Descricao}\n\nContinuar?", "SEXTA-FEIRA",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        reparando = true;
        b.Enabled = false;
        b.Text = "Reparando...";
        try
        {
            string msg = await Task.Run(() => r.Executar());
            Atualizador.Log($"Reparo '{r.Id}': {msg}");
            b.Text = "✓ Feito";
            b.BackColor = Tema.Verde;
            b.ForeColor = Color.FromArgb(10, 12, 20);
            if (r.PedeReinicio)
            {
                if (MessageBox.Show(msg + "\n\nReiniciar o PC agora?", "SEXTA-FEIRA", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Sistema.Rodar("shutdown", "/r /t 5");
            }
            else MessageBox.Show(msg, "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            b.Text = "Reparar";
            Atualizador.Log($"ERRO no reparo '{r.Id}': {ex}");
            MessageBox.Show("Não deu certo: " + ex.Message, "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            b.Enabled = true;
            reparando = false;
        }
    }

    // =====================================================================
    //  Página: Internet
    // =====================================================================

    private Button btnDiagnostico, btnBufferbloat, btnAjustesRede, btnTestarRotas, btnInstalarWarp;
    private readonly Label lblRotaInfo = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private readonly Label lblRotaProgresso = Tema.Rotulo("", Tema.Pequena, Hud.CianoClaro);
    private readonly List<(Label nome, Label det)> linhasRota = new();
    private readonly System.Windows.Forms.Timer timerVigia = new() { Interval = 3000 };
    private bool vigiando;

    private static CheckBox CaixaHud(string texto, bool marcado) => new()
    {
        Text = texto, Checked = marcado, AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.Texto,
        BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
    };

    /// <summary>Mostra o estado do WARP e a rota salva de cada jogo.</summary>
    private void MostrarRotas(List<RotaInteligente.Resultado> resultados = null)
    {
        bool warp = RotaInteligente.WarpInstalado;
        btnInstalarWarp.Visible = !warp;
        lblRotaInfo.Text = (warp
            ? "✅ Cloudflare WARP instalado. A SEXTA-FEIRA compara a rota da tua operadora com 2 rotas da Cloudflare até os servidores de São Paulo e escolhe a mais ESTÁVEL de cada jogo."
            : "Sem o WARP, o teste mede só a rota da tua operadora. Instala o Cloudflare WARP (oficial e grátis) pra ter 2 rotas alternativas, igual o ExitLag faz com os servidores dele.")
            + " A rota liga quando o jogo (ou o launcher) abre e nunca troca no meio da partida. Sem risco de ban: não mexe no jogo e não troca de região.";
        var cfg = Configuracao.Atual;
        for (int i = 0; i < RotaInteligente.Jogos.Count && i < linhasRota.Count; i++)
        {
            var j = RotaInteligente.Jogos[i];
            var (nome, det) = linhasRota[i];
            var r = resultados?.FirstOrDefault(x => x.Jogo.Id == j.Id);
            string rota = cfg.Rotas.GetValueOrDefault(j.Id);
            nome.Text = rota == null ? $"{j.Nome}  ·  ainda não testado" : $"{j.Nome}  →  {RotaInteligente.NomeRota[rota]}";
            nome.ForeColor = rota == null ? Tema.Apagado : rota == "direta" ? Tema.Texto : Tema.Verde;
            det.Text = r == null ? (rota == null ? "Clica em Testar rotas." : "Rota salva do último teste.")
                : string.Join("   |   ", r.Medicoes.Select(m => $"{RotaInteligente.NomeRota[m.Rota]}: {m.Texto()}")) + "\n" + r.Motivo;
        }
    }

    private async Task InstalarWarp()
    {
        if (MessageBox.Show("Vou instalar o Cloudflare WARP oficial (grátis) pelo instalador do próprio Windows (winget).\n\n" +
                            "Leva 1 a 3 minutos. Depois disso a SEXTA-FEIRA usa ele como rota alternativa só quando for melhor.\n\nInstalar?",
                "SEXTA-FEIRA", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        btnInstalarWarp.Enabled = false;
        btnInstalarWarp.Text = "Instalando...";
        bool ok = await Task.Run(() => RotaInteligente.InstalarWarp());
        btnInstalarWarp.Text = "Instalar Cloudflare WARP (grátis)";
        btnInstalarWarp.Enabled = true;
        if (!ok)
        {
            MessageBox.Show("Não consegui instalar sozinho. Vou abrir o site oficial: baixa, instala e depois volta aqui e clica em Testar rotas.", "SEXTA-FEIRA");
            Sistema.Abrir("https://one.one.one.one/");
        }
        MostrarRotas();
    }

    private async Task TestarRotas()
    {
        if (testandoRede) return;
        if (RotaInteligente.EmSessao || Jogos.JogoAberto() != null)
        {
            MessageBox.Show("Fecha o jogo antes de testar: o teste troca de rota algumas vezes e isso derrubaria a tua partida.", "SEXTA-FEIRA");
            return;
        }
        testandoRede = true;
        btnTestarRotas.Enabled = btnDiagnostico.Enabled = btnBufferbloat.Enabled = false;
        consoleIa.Dizer("Testando as rotas até os servidores de São Paulo...", true);
        try
        {
            var res = await RotaInteligente.Testar((txt, pct) =>
                BeginInvoke(new Action(() => lblRotaProgresso.Text = $"{pct}%  {txt}")));
            lblRotaProgresso.Text = "✅ Teste concluído. A melhor rota de cada jogo foi salva.";
            MostrarRotas(res);
            consoleIa.Dizer("Rotas testadas. " + string.Join(", ", res.Select(r => $"{r.Jogo.Nome}: {RotaInteligente.NomeRota[r.Escolhida]}")) + ".", true);
        }
        catch (Exception ex)
        {
            lblRotaProgresso.Text = "Erro no teste: " + ex.Message;
            Atualizador.Log("ERRO teste de rotas: " + ex);
        }
        finally
        {
            testandoRede = false;
            btnTestarRotas.Enabled = btnDiagnostico.Enabled = btnBufferbloat.Enabled = true;
        }
    }

    /// <summary>A cada 3 s: vê se algum jogo abriu/fechou e liga/desliga a rota e a proteção.</summary>
    private async Task Vigiar()
    {
        // outra abertura do app pediu pra janela aparecer
        try
        {
            if (File.Exists(Program.ArquivoAbrir)) { File.Delete(Program.ArquivoAbrir); AbrirDaBandeja(); }
        }
        catch { }
        if (vigiando || testandoRede) return;
        vigiando = true;
        try
        {
            string frase = await Task.Run(() => RotaInteligente.Vigiar());
            if (frase != null)
            {
                consoleIa.Dizer(frase, true);
                if (RotaInteligente.EmSessao) AvisoPopup.MostrarSemEsperar("SEXTA-FEIRA · partida", frase);
            }
        }
        finally { vigiando = false; }
    }
    private readonly Label lblDiagStatus = Tema.Rotulo("Ainda não testado.", Tema.Pequena, Tema.Apagado);
    private readonly Panel areaAlvos = new();
    private readonly Label lblVeredito = Tema.Rotulo("", Tema.Negrito, Tema.Texto);
    private readonly Label lblVereditoDet = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private readonly Label lblBbNota = Tema.Rotulo("--", Hud.Leve(30f), Tema.Apagado);
    private readonly Label lblBbTxt = Tema.Rotulo("Mede quanto o ping sobe quando a internet está sendo usada (o famoso bufferbloat).", Tema.Pequena, Tema.Apagado);
    private readonly Label lblRedeInfo = Tema.Rotulo("Lendo a placa de rede...", Tema.Pequena, Tema.Apagado);
    private readonly Label lblRedeAjustes = Tema.Rotulo("", Tema.Pequena, Tema.Texto);
    private bool testandoRede;

    private Panel MontarInternet()
    {
        var p = NovaPagina("Internet", "Rota inteligente pros teus jogos, proteção da internet durante a partida e diagnóstico de picos.");

        // ---------- Rota inteligente ----------
        var rota = Card(30, 100, 900, 300, "Rota inteligente  ·  estilo ExitLag, grátis");
        lblRotaInfo.Location = new Point(18, 44);
        lblRotaInfo.MaximumSize = new Size(860, 0);
        rota.Controls.Add(lblRotaInfo);
        btnTestarRotas = Tema.Botao("▶ Testar rotas (~1:30)", Tema.Destaque, 210, 36);
        btnTestarRotas.Location = new Point(18, 90);
        btnTestarRotas.Click += async (_, _) => await TestarRotas();
        rota.Controls.Add(btnTestarRotas);
        btnInstalarWarp = Tema.Botao("Instalar Cloudflare WARP (grátis)", Tema.CardClaro, 260, 36);
        btnInstalarWarp.Location = new Point(240, 90);
        btnInstalarWarp.Click += async (_, _) => await InstalarWarp();
        rota.Controls.Add(btnInstalarWarp);
        lblRotaProgresso.Location = new Point(514, 100);
        lblRotaProgresso.MaximumSize = new Size(370, 0);
        rota.Controls.Add(lblRotaProgresso);
        for (int i = 0; i < RotaInteligente.Jogos.Count; i++)
        {
            var nome = Tema.Rotulo("", Tema.Negrito, Tema.Texto);
            nome.Location = new Point(18, 140 + i * 38);
            rota.Controls.Add(nome);
            var det = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
            det.Location = new Point(18, 160 + i * 38);
            det.MaximumSize = new Size(860, 0);
            rota.Controls.Add(det);
            linhasRota.Add((nome, det));
        }
        var chkAuto = CaixaHud("Ligar a melhor rota sozinho quando o jogo abrir", Configuracao.Atual.RotaAutomatica);
        chkAuto.Location = new Point(18, 260);
        chkAuto.CheckedChanged += (_, _) => { Configuracao.Atual.RotaAutomatica = chkAuto.Checked; Configuracao.Atual.Salvar(); };
        rota.Controls.Add(chkAuto);
        var chkProteger = CaixaHud("Proteger a internet durante o jogo (pausa Update e OneDrive)", Configuracao.Atual.ProtegerRedeNoJogo);
        chkProteger.Location = new Point(440, 260);
        chkProteger.CheckedChanged += (_, _) => { Configuracao.Atual.ProtegerRedeNoJogo = chkProteger.Checked; Configuracao.Atual.Salvar(); };
        rota.Controls.Add(chkProteger);
        p.Controls.Add(rota);

        // ---------- Diagnóstico ----------
        var diag = Card(30, 416, 900, 360, "Diagnóstico de picos (60 segundos)");
        btnDiagnostico = Tema.Botao("▶ Diagnosticar", Tema.Destaque, 170, 36);
        btnDiagnostico.Location = new Point(18, 38);
        btnDiagnostico.Click += async (_, _) => await RodarDiagnostico();
        diag.Controls.Add(btnDiagnostico);
        lblDiagStatus.Location = new Point(200, 48);
        lblDiagStatus.MaximumSize = new Size(680, 0);
        diag.Controls.Add(lblDiagStatus);

        areaAlvos.Location = new Point(18, 84);
        areaAlvos.Size = new Size(864, 200);
        areaAlvos.BackColor = Tema.Card;
        diag.Controls.Add(areaAlvos);

        lblVeredito.Location = new Point(18, 290);
        diag.Controls.Add(lblVeredito);
        lblVereditoDet.Location = new Point(18, 314);
        lblVereditoDet.MaximumSize = new Size(860, 0);
        diag.Controls.Add(lblVereditoDet);
        p.Controls.Add(diag);

        // ---------- Bufferbloat ----------
        var bb = Card(30, 790, 440, 266, "Bufferbloat (~40 segundos)");
        btnBufferbloat = Tema.Botao("▶ Testar", Tema.CardClaro, 120, 34);
        btnBufferbloat.Location = new Point(18, 38);
        btnBufferbloat.Click += async (_, _) => await RodarBufferbloat();
        bb.Controls.Add(btnBufferbloat);
        lblBbNota.Location = new Point(320, 30);
        bb.Controls.Add(lblBbNota);
        lblBbTxt.Location = new Point(18, 86);
        lblBbTxt.MaximumSize = new Size(404, 0);
        bb.Controls.Add(lblBbTxt);
        p.Controls.Add(bb);

        // ---------- Ajustes ----------
        var aj = Card(490, 790, 440, 266, "Ajustes de rede");
        lblRedeInfo.Location = new Point(18, 38);
        lblRedeInfo.MaximumSize = new Size(404, 0);
        aj.Controls.Add(lblRedeInfo);
        lblRedeAjustes.Location = new Point(18, 96);
        lblRedeAjustes.MaximumSize = new Size(404, 0);
        aj.Controls.Add(lblRedeAjustes);
        btnAjustesRede = Tema.Botao("Aplicar ajustes de rede", Tema.Destaque, 210, 34);
        btnAjustesRede.Location = new Point(18, 216);
        btnAjustesRede.Click += async (_, _) => await AplicarAjustesRede();
        aj.Controls.Add(btnAjustesRede);
        var desfazerRede = Tema.Botao("Desfazer", Tema.CardClaro, 110, 34);
        desfazerRede.Location = new Point(240, 216);
        desfazerRede.Click += async (_, _) =>
        {
            if (MessageBox.Show("Volta a placa de rede e o Windows Update exatamente como estavam antes da SEXTA-FEIRA mexer.\n\n" +
                                "O cabo pode desconectar por 2-3 segundos. Continuar?", "SEXTA-FEIRA",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            desfazerRede.Enabled = false;
            int n = await Task.Run(() => Backup.RestaurarRede());
            Atualizador.Log($"Ajustes de rede desfeitos ({n})");
            MessageBox.Show(n > 0 ? "Pronto, voltou tudo como era. Reinicia o PC e testa o jogo de novo pra comparar."
                                  : "Não tinha nenhum ajuste de rede feito pela SEXTA-FEIRA pra desfazer.", "SEXTA-FEIRA");
            desfazerRede.Enabled = true;
            await AtualizarInfoRede();
        };
        aj.Controls.Add(desfazerRede);
        p.Controls.Add(aj);
        return p;
    }

    private async Task RodarDiagnostico()
    {
        if (testandoRede) return;
        testandoRede = true;
        btnDiagnostico.Enabled = btnBufferbloat.Enabled = false;
        lblVeredito.Text = "";
        lblVereditoDet.Text = "";
        lblDiagStatus.Text = "Preparando... joga ou deixa o PC como tu usa normalmente: o teste pega os picos na hora.";
        try
        {
            var alvos = await Rede.MontarAlvos();
            areaAlvos.Controls.Clear();
            var graficos = new List<(Rede.Alvo alvo, GraficoPing graf, Label stats)>();
            int y = 0;
            foreach (var a in alvos)
            {
                var nome = Tema.Rotulo(a.Nome, Tema.Negrito, Tema.Texto);
                nome.Location = new Point(0, y);
                areaAlvos.Controls.Add(nome);
                var exp = Tema.Rotulo(a.Explicacao, Tema.Pequena, Tema.Apagado);
                exp.Location = new Point(0, y + 22);
                areaAlvos.Controls.Add(exp);
                var graf = new GraficoPing { Location = new Point(250, y + 2), Size = new Size(260, 38) };
                areaAlvos.Controls.Add(graf);
                var st = Tema.Rotulo("...", Tema.Pequena, Tema.Apagado);
                st.Location = new Point(522, y + 4);
                st.MaximumSize = new Size(340, 0);
                areaAlvos.Controls.Add(st);
                graficos.Add((a, graf, st));
                y += 50;
            }

            using var cts = new CancellationTokenSource();
            var tarefa = Rede.Diagnosticar(alvos, 60, pct =>
            {
                BeginInvoke(new Action(() =>
                {
                    lblDiagStatus.Text = $"{pct}%  medindo...  (os pontos amarelos são picos, os vermelhos são pacotes perdidos)";
                    foreach (var (alvo, graf, stats) in graficos)
                    {
                        graf.Valores = alvo.Copia();
                        stats.Text = alvo.Calcular().Texto();
                    }
                }));
            }, cts.Token);
            await tarefa;

            foreach (var (alvo, graf, stats) in graficos)
            {
                graf.Valores = alvo.Copia();
                var e = alvo.Calcular();
                stats.Text = e.Texto();
                stats.ForeColor = e.Perdidos > 0 || e.Picos >= 3 ? Tema.Amarelo : Tema.Apagado;
            }

            var (status, titulo, detalhe) = Rede.Veredito(alvos);
            lblVeredito.Text = titulo;
            lblVeredito.ForeColor = status == Status.Ok ? Tema.Verde : status == Status.Alerta ? Tema.Amarelo : Tema.Destaque;
            lblVereditoDet.Text = detalhe;
            lblDiagStatus.Text = "✅ Diagnóstico concluído.";
            Atualizador.Log("Diagnóstico de rede: " + titulo + " | " + string.Join(" | ", alvos.Select(a => a.Nome + ": " + a.Calcular().Texto())));
        }
        catch (Exception ex)
        {
            lblDiagStatus.Text = "Erro no diagnóstico: " + ex.Message;
            Atualizador.Log("ERRO diagnóstico rede: " + ex);
        }
        finally
        {
            testandoRede = false;
            btnDiagnostico.Enabled = btnBufferbloat.Enabled = true;
        }
    }

    private async Task RodarBufferbloat()
    {
        if (testandoRede) return;
        string jogoAberto = Jogos.JogoAberto();
        if (jogoAberto != null &&
            MessageBox.Show("Esse teste usa a internet no máximo por uns 40 segundos, e o jogo vai ficar com lag enquanto isso.\n\n" +
                            "O ideal é rodar com o jogo fechado. Rodar mesmo assim?", "SEXTA-FEIRA",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        testandoRede = true;
        btnDiagnostico.Enabled = btnBufferbloat.Enabled = false;
        lblBbNota.Text = "...";
        lblBbNota.ForeColor = Tema.Apagado;
        try
        {
            var b = await Task.Run(() => Rede.TestarBufferbloat(txt => BeginInvoke(new Action(() => lblBbTxt.Text = txt))));
            string nota = b.NotaGeral;
            lblBbNota.Text = nota;
            lblBbNota.ForeColor = nota is "A+" or "A" ? Tema.Verde : nota == "B" ? Tema.Amarelo : Tema.Vermelho;
            string dica = nota is "A+" or "A"
                ? "Ótimo: usar a internet não atrapalha o teu ping. Os picos não vêm daqui."
                : b.AumentoUp > b.AumentoDown
                    ? "Enviar dados (upload) faz teu ping disparar. Fecha OBS/live, backup na nuvem e sincronização (OneDrive, Google Drive) quando for jogar. Se o roteador tiver QoS ou 'SQM', liga."
                    : "Baixar coisas faz teu ping disparar. Pausa downloads da Steam, Epic e Windows Update quando for jogar (o Modo Competitivo já pausa o Update). Se o roteador tiver QoS ou 'SQM', liga.";
            lblBbTxt.ForeColor = Tema.Texto;
            lblBbTxt.Text =
                $"Parado: {b.Parado:0} ms\n" +
                $"Baixando: {b.Baixando:0} ms (+{b.AumentoDown:0})  •  {b.MbpsDown:0} Mbps  •  nota {Rede.Bufferbloat.Nota(b.AumentoDown)}\n" +
                $"Enviando: {b.Enviando:0} ms (+{b.AumentoUp:0})  •  {b.MbpsUp:0} Mbps  •  nota {Rede.Bufferbloat.Nota(b.AumentoUp)}\n\n" + dica;
            Atualizador.Log($"Bufferbloat: {nota} parado {b.Parado:0} down {b.Baixando:0} up {b.Enviando:0} ({b.MbpsDown:0}/{b.MbpsUp:0} Mbps)");
        }
        catch (Exception ex)
        {
            lblBbTxt.Text = "Erro no teste: " + ex.Message;
        }
        finally
        {
            testandoRede = false;
            btnDiagnostico.Enabled = btnBufferbloat.Enabled = true;
        }
    }

    private async Task AtualizarInfoRede()
    {
        var (placas, eee, economia, p2p) = await Task.Run(() =>
            (Rede.PlacasAtivas(), Rede.EeeLigado(), Ajustes.PlacasComEconomia(), Rede.AtualizacaoP2PLigada()));

        if (placas.Count == 0) lblRedeInfo.Text = "Nenhuma placa de rede conectada.";
        else
        {
            lblRedeInfo.Text = string.Join("\n", placas.Select(pl =>
                $"{(Rede.EhCabo(pl) ? "🔌 Cabo" : "📶 Wi-Fi")}: {pl.Velocidade}  •  {pl.Descricao}"));
            var cabo = placas.FirstOrDefault(Rede.EhCabo);
            if (cabo != null && cabo.Velocidade.StartsWith("100 M"))
            {
                lblRedeInfo.Text += "\n⚠ O cabo está conectado a só 100 Mbps: normalmente é cabo velho/danificado ou porta ruim. O certo é 1 Gbps.";
                lblRedeInfo.ForeColor = Tema.Amarelo;
            }
            else lblRedeInfo.ForeColor = Tema.Apagado;
        }

        string Linha(bool ok, string txt) => (ok ? "✅ " : "⬜ ") + txt;
        lblRedeAjustes.Text = string.Join("\n", new[]
        {
            Linha(eee == 0, "Ethernet verde (EEE) desligada" + (eee < 0 ? " (não deu pra ler)" : "")),
            Linha(economia == 0, "Economia de energia da placa desligada"),
            Linha(!p2p, "Windows Update sem enviar pra outros PCs"),
        });
        bool tudoOk = eee <= 0 && economia <= 0 && !p2p;
        btnAjustesRede.Text = tudoOk ? "✅ Tudo aplicado" : "Aplicar ajustes de rede";
        btnAjustesRede.BackColor = tudoOk ? Tema.CardClaro : Tema.Destaque;
    }

    private async Task AplicarAjustesRede()
    {
        if (MessageBox.Show("Vou desligar a Ethernet verde (EEE), a economia de energia da placa e o envio de atualizações pra outros PCs.\n\n" +
                            "O cabo pode desconectar por 2-3 segundos. Tudo pode ser desfeito em Ferramentas → Desfazer mudanças.\n\nContinuar?",
                "SEXTA-FEIRA", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        btnAjustesRede.Enabled = false;
        btnAjustesRede.Text = "Aplicando...";
        try
        {
            await Task.Run(() =>
            {
                Rede.DesligarEee();
                Ajustes.DesligarEconomiaRede();
                Rede.DesligarAtualizacaoP2P();
            });
            Atualizador.Log("Ajustes de rede aplicados");
            MessageBox.Show("Pronto! Reinicia o PC pra tudo valer e depois roda o diagnóstico de novo pra comparar.", "SEXTA-FEIRA");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não deu: " + ex.Message, "SEXTA-FEIRA");
        }
        finally
        {
            btnAjustesRede.Enabled = true;
            await AtualizarInfoRede();
        }
    }

    // =====================================================================
    //  Página: Restauração
    // =====================================================================

    private Panel MontarRestauracao()
    {
        var p = NovaPagina("Ponto de restauração",
            "Um ponto de restauração é uma \"foto\" das configurações do Windows. Se algo der errado depois de instalar um driver ou mudar algo, " +
            "tu volta pra ela. Teus arquivos (fotos, documentos, jogos) NÃO são apagados.");

        var criar = Card(30, 118, 900, 120, "Criar um ponto agora");
        txtPonto.Location = new Point(18, 46);
        txtPonto.Size = new Size(560, 30);
        txtPonto.BackColor = Tema.CardClaro;
        txtPonto.ForeColor = Tema.Texto;
        txtPonto.BorderStyle = BorderStyle.FixedSingle;
        txtPonto.Font = Tema.Normal;
        txtPonto.Text = "SEXTA-FEIRA - " + DateTime.Now.ToString("dd/MM/yyyy");
        criar.Controls.Add(txtPonto);

        btnCriarPonto = Tema.Botao("Criar ponto", Tema.Destaque, 180, 34);
        btnCriarPonto.Location = new Point(598, 44);
        btnCriarPonto.Click += async (_, _) =>
        {
            string nome = string.IsNullOrWhiteSpace(txtPonto.Text) ? "SEXTA-FEIRA" : txtPonto.Text.Trim();
            btnCriarPonto.Enabled = false;
            lblPontoStatus.ForeColor = Tema.Apagado;
            lblPontoStatus.Text = "Criando... pode levar até 1 minuto.";
            var (ok, msg) = await Task.Run(() => Restauracao.Criar(nome));
            lblPontoStatus.ForeColor = ok ? Tema.Verde : Tema.Amarelo;
            lblPontoStatus.Text = msg;
            btnCriarPonto.Enabled = true;
            await CarregarPontos();
        };
        criar.Controls.Add(btnCriarPonto);

        lblPontoStatus.Location = new Point(20, 88);
        criar.Controls.Add(lblPontoStatus);
        p.Controls.Add(criar);

        var titulo = Tema.Rotulo("Pontos que já existem no teu PC", Tema.Negrito);
        titulo.Location = new Point(33, 254);
        p.Controls.Add(titulo);

        listaPontos.Location = new Point(30, 282);
        listaPontos.Size = new Size(900, 430);
        listaPontos.BackColor = Tema.Card;
        listaPontos.AutoScroll = true;
        Tema.Escurecer(listaPontos);
        p.Controls.Add(listaPontos);
        return p;
    }

    private async Task CarregarPontos()
    {
        var pontos = await Task.Run(() => Restauracao.Listar());
        listaPontos.SuspendLayout();
        listaPontos.Controls.Clear();

        if (pontos.Count == 0)
        {
            var vazio = Tema.Rotulo("Nenhum ponto de restauração ainda. Cria o primeiro aí em cima!", Tema.Normal, Tema.Apagado);
            vazio.Location = new Point(18, 18);
            listaPontos.Controls.Add(vazio);
        }

        int y = 8;
        foreach (var pt in pontos)
        {
            var nome = Tema.Rotulo(pt.Descricao, Tema.Negrito);
            nome.Location = new Point(18, y + 4);
            nome.MaximumSize = new Size(640, 0);
            listaPontos.Controls.Add(nome);

            string quando = pt.Data == default ? "" : pt.Data.ToString("dd/MM/yyyy 'às' HH:mm") + "  •  ";
            var det = Tema.Rotulo(quando + pt.Tipo, Tema.Pequena, Tema.Apagado);
            det.Location = new Point(18, y + 28);
            listaPontos.Controls.Add(det);

            var b = Tema.Botao("Restaurar", Tema.CardClaro, 140, 34);
            b.Font = Tema.Pequena;
            b.Location = new Point(710, y + 8);
            var ponto = pt;
            b.Click += (_, _) => RestaurarPonto(ponto);
            listaPontos.Controls.Add(b);
            y += 60;
        }
        listaPontos.ResumeLayout();
    }

    private void RestaurarPonto(Restauracao.Ponto pt)
    {
        var resp = MessageBox.Show(
            $"Voltar o Windows para \"{pt.Descricao}\" ({pt.Data:dd/MM/yyyy HH:mm})?\n\n" +
            "• Programas e drivers instalados depois dessa data podem sumir.\n" +
            "• Teus arquivos pessoais e jogos NÃO são apagados.\n" +
            "• O PC vai precisar reiniciar e demora alguns minutos.\n\n" +
            "Salva o que estiver aberto antes de continuar.",
            "Restaurar o Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (resp != DialogResult.Yes) return;

        var (ok, msg) = Restauracao.Restaurar(pt.Numero);
        if (!ok)
        {
            MessageBox.Show(msg, "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show("Tudo pronto! Reiniciar agora pra restaurar?", "SEXTA-FEIRA",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            Sistema.Rodar("shutdown", "/r /t 5");
    }

    // =====================================================================
    //  Página: Ferramentas
    // =====================================================================

    private Panel MontarFerramentas()
    {
        var p = NovaPagina("Ferramentas", null);

        // Botão da barra
        var c1 = Card(30, 90, 440, 170, "Botão na barra de tarefas");
        var t1 = Tema.Rotulo("Otimiza com 1 clique, sem abrir o app\ne sem pedir permissão toda vez.", Tema.Pequena, Tema.Texto);
        t1.Location = new Point(18, 40);
        c1.Controls.Add(t1);
        lblAtalho.Location = new Point(18, 82);
        c1.Controls.Add(lblAtalho);
        btnAtalho = Tema.Botao("Instalar botão", Tema.Destaque, 180, 36);
        btnAtalho.Location = new Point(18, 116);
        btnAtalho.Click += (_, _) =>
        {
            if (Atalho.Instalado()) Atalho.Remover();
            else if (Atalho.Instalar())
                MessageBox.Show("Pronto! Agora abre o Iniciar, pesquisa \"Otimizar PC\", clica com o botão direito e escolhe \"Fixar na barra de tarefas\".",
                    "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Não consegui criar o botão. Tenta de novo.", "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            AtualizarAtalho();
        };
        c1.Controls.Add(btnAtalho);
        var btnTestar = Tema.Botao("Testar", Tema.CardClaro, 110, 36);
        btnTestar.Location = new Point(210, 116);
        btnTestar.Click += (_, _) =>
        {
            if (!Atalho.Instalado())
            {
                MessageBox.Show("Instala o botão primeiro.", "SEXTA-FEIRA");
                return;
            }
            // Roda exatamente o que o raio da barra roda
            Sistema.Rodar("schtasks", $"/run /tn \"{Atalho.NomeTarefa}\"");
            MessageBox.Show("Rodando! Em alguns segundos o aviso \"PC otimizado\" aparece no canto de cima da tela.\n\nSe não aparecer, me manda o arquivo log.txt (Ferramentas → Abrir pasta de dados).",
                "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        c1.Controls.Add(btnTestar);
        p.Controls.Add(c1);

        // Desfazer
        var c2 = Card(490, 90, 440, 170, "Desfazer tudo");
        var t2 = Tema.Rotulo("Volta registro, plano de energia e configs\nde jogo pro jeito que eram antes do app.", Tema.Pequena, Tema.Texto);
        t2.Location = new Point(18, 40);
        c2.Controls.Add(t2);
        var bDesfazer = Tema.Botao("Desfazer mudanças", Tema.Vermelho, 180, 36);
        bDesfazer.Location = new Point(18, 116);
        bDesfazer.Click += (_, _) =>
        {
            if (!Backup.TemBackup())
            {
                MessageBox.Show("Não tem nada pra desfazer ainda.", "SEXTA-FEIRA");
                return;
            }
            if (MessageBox.Show("Voltar tudo pro jeito de antes?", "SEXTA-FEIRA", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int n = Backup.Restaurar();
            MessageBox.Show($"Pronto! {n} item(ns) restaurado(s). Reinicia o PC pra tudo voltar 100%.", "SEXTA-FEIRA");
            _ = RodarChecagem();
        };
        c2.Controls.Add(bDesfazer);
        p.Controls.Add(c2);

        // Relatório
        var c3 = Card(30, 280, 440, 170, "Relatório pra IA");
        var t3 = Tema.Rotulo("Copia um resumo do teu PC. Cola no chat\ncom o Claude pra gente melhorar o app.", Tema.Pequena, Tema.Texto);
        t3.Location = new Point(18, 40);
        c3.Controls.Add(t3);
        var bRel = Tema.Botao("Copiar relatório", Tema.Destaque, 180, 36);
        bRel.Location = new Point(18, 116);
        bRel.Click += (_, _) =>
        {
            Clipboard.SetText(Relatorio.Gerar(pc, jogos, checagens));
            MessageBox.Show("Relatório copiado! É só colar (Ctrl+V) no chat.", "SEXTA-FEIRA");
        };
        c3.Controls.Add(bRel);
        p.Controls.Add(c3);

        // Atualizações
        var c4 = Card(490, 280, 440, 170, "Atualizações");
        var t4 = Tema.Rotulo("O app procura versão nova sozinho ao abrir.", Tema.Pequena, Tema.Texto);
        t4.Location = new Point(18, 40);
        c4.Controls.Add(t4);
        var bAtual = Tema.Botao("Procurar agora", Tema.CardClaro, 180, 36);
        bAtual.Location = new Point(18, 116);
        bAtual.Click += async (_, _) =>
        {
            bAtual.Enabled = false;
            await ProcurarAtualizacao(true);
            bAtual.Enabled = true;
        };
        c4.Controls.Add(bAtual);
        var bPasta = Tema.Botao("Abrir pasta de dados", Tema.CardClaro, 190, 36);
        bPasta.Location = new Point(210, 116);
        bPasta.Click += (_, _) => Sistema.Abrir(Sistema.PastaDados);
        c4.Controls.Add(bPasta);
        p.Controls.Add(c4);

        // Programas fechados
        var c5 = Card(30, 470, 900, 150, "Programas fechados ao otimizar (nome do processo, separado por vírgula)");
        txtProgramas.Location = new Point(18, 42);
        txtProgramas.Size = new Size(860, 30);
        txtProgramas.BackColor = Tema.CardClaro;
        txtProgramas.ForeColor = Tema.Texto;
        txtProgramas.BorderStyle = BorderStyle.FixedSingle;
        txtProgramas.Font = Tema.Normal;
        txtProgramas.Text = string.Join(", ", Configuracao.Atual.ProgramasParaFechar);
        c5.Controls.Add(txtProgramas);
        var bSalvar = Tema.Botao("Salvar", Tema.CardClaro, 120, 34);
        bSalvar.Location = new Point(18, 92);
        bSalvar.Click += (_, _) =>
        {
            Configuracao.Atual.ProgramasParaFechar = txtProgramas.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? x[..^4] : x)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Configuracao.Atual.Salvar();
            MessageBox.Show("Salvo!", "SEXTA-FEIRA");
        };
        c5.Controls.Add(bSalvar);
        var aviso = Tema.Rotulo("Não coloca Steam, Epic, Riot nem Discord: os jogos precisam deles.", Tema.Pequena, Tema.Apagado);
        aviso.Location = new Point(156, 100);
        c5.Controls.Add(aviso);
        p.Controls.Add(c5);

        return p;
    }

    private void AtualizarAtalho()
    {
        bool ok = Atalho.Instalado();
        lblAtalho.Text = ok ? "✅ Instalado" : "Não instalado";
        lblAtalho.ForeColor = ok ? Tema.Verde : Tema.Apagado;
        btnAtalho.Text = ok ? "Remover botão" : "Instalar botão";
        btnAtalho.BackColor = ok ? Tema.CardClaro : Tema.Destaque;
        btnAtalho.ForeColor = ok ? Color.White : Color.FromArgb(10, 12, 20);
    }

    // =====================================================================
    //  Carregamento
    // =====================================================================

    private async Task Carregar()
    {
        lblVersao.Text = "v" + Atualizador.VersaoAtual;
        timer.Start();

        // Correção v1.0.15: o mouse deixou de ser marcado por padrão e volta ao normal uma vez
        var cfg = Configuracao.Atual;
        if (cfg.VersaoIconeRaio < 3)
        {
            cfg.VersaoIconeRaio = 3;
            cfg.Salvar();
            _ = Task.Run(() => Atalho.AtualizarIcone());
        }
        if (!cfg.TelaCorrigida)
        {
            cfg.TelaCorrigida = true;
            cfg.Salvar();
            _ = Task.Run(() => { Otimizador.ManterTelaLigada(); Atualizador.Log("Tela: desligamento automático na tomada desativado (v1.0.27)"); });
        }
        if (!cfg.MouseCorrigido)
        {
            cfg.MouseCorrigido = true;
            if (cfg.UmClique != null) cfg.UmClique.Remove("mouse");
            cfg.Salvar();
            if (!Ajustes.AceleracaoMouseLigada())
            {
                Ajustes.ReativarAceleracaoMouse();
                Atualizador.Log("Mouse: aceleração reativada pela correção da v1.0.15");
            }
        }

        // Atualização vem PRIMEIRO e em paralelo com o resto (antes ficava pro final)
        Task tarefaAtualizacao = Configuracao.Atual.AtualizarAutomaticamente ? ProcurarAtualizacao(false) : Task.CompletedTask;
        timerAtualizacao.Tick += async (_, _) => await ProcurarAtualizacao(false);
        timerAtualizacao.Start();
        _ = AtualizarMonitor();
        AtualizarContador();

        _ = AtualizarSaude();
        _ = CarregarPontos();
        PreencherLimpeza();
        _ = CalcularTamanhos();
        _ = AtualizarInfoRede();
        MostrarRotas();
        timerVigia.Tick += async (_, _) => await Vigiar();
        timerVigia.Start();
        timerSaude.Tick += async (_, _) => await AtualizarSaude();
        timerSaude.Start();

        pc = await Task.Run(() => InfoPC.Ler());
        listaSpecs.Definir(new[]
        {
            ("CPU", pc.Cpu), ("GPU", pc.Gpu),
            ("RAM", $"{pc.RamGB} GB · {pc.Pentes}x · {pc.RamMHz} MHz"), ("OS", pc.Windows)
        });

        jogos = await Task.Run(() => Jogos.Detectar());
        nivel = ConfigsCompetitivas.Nivel(pc.Gpu);
        biblioteca = await Task.Run(() => Biblioteca.Listar());
        PreencherJogos();
        AtualizarHistoricoFps();
        _ = ProcurarJogosAbertos();
        AtualizarAtalho();

        // Atualiza o botão já existente (ícone e caminho novos depois de uma atualização)
        if (Atalho.Instalado()) await Task.Run(() => Atalho.Instalar());

        // Primeira vez: já instala o botão da barra sozinho
        if (!Atalho.Instalado() && !File.Exists(Path.Combine(Sistema.PastaDados, "atalho-oferecido")))
        {
            File.WriteAllText(Path.Combine(Sistema.PastaDados, "atalho-oferecido"), "1");
            if (Atalho.Instalar())
            {
                AtualizarAtalho();
                MessageBox.Show(
                    "Criei o botão \"Otimizar PC (SEXTA-FEIRA)\" no Menu Iniciar, com um ícone de raio amarelo ⚡.\n\n" +
                    "Pra deixar ele na barra: abre o Iniciar, pesquisa \"Otimizar PC\", clica com o botão direito no do RAIO e escolhe \"Fixar na barra de tarefas\".\n\nNa barra é 1 clique só: ele otimiza escondido e só mostra a notificação.",
                    "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        await RodarChecagem();
        await tarefaAtualizacao;
    }

    private async Task ProcurarAtualizacao(bool avisarSeNaoTiver)
    {
        if (procurandoAtualizacao) return;
        // Não baixa nada enquanto tu joga: download no meio da partida faz o ping disparar
        if (!avisarSeNaoTiver && !atualizacaoBaixada)
        {
            string jogo = Jogos.JogoAberto();
            if (jogo != null || ModoCompetitivo.Ativo)
            {
                Atualizador.Log($"Busca de atualização adiada: {(jogo ?? "Modo Competitivo")} ativo");
                return;
            }
        }
        procurandoAtualizacao = true;
        string versao = "v" + Atualizador.VersaoAtual;
        try
        {
            if (atualizacaoBaixada)
            {
                if (avisarSeNaoTiver) MessageBox.Show("A versão nova já tá baixada! Clica em \"Reiniciar e atualizar\" lá em cima.", "SEXTA-FEIRA");
                return;
            }

            lblVersao.Text = versao + "  •  procurando atualização...";
            atualizacao = await Atualizador.Verificar();

            if (atualizacao == null)
            {
                bool erro = Atualizador.UltimoErro != null;
                lblVersao.Text = versao + (erro ? "  •  ⚠ sem atualização" : "  •  atualizado ✅");
                if (avisarSeNaoTiver)
                    MessageBox.Show(Atualizador.UltimoErro ?? "Tu já tá na versão mais nova!", "SEXTA-FEIRA",
                        MessageBoxButtons.OK, erro ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                return;
            }

            // Achou: já baixa em segundo plano, sem esperar clique
            string nova = atualizacao.TargetFullRelease.Version.ToString();
            lblVersao.Text = versao + $"  •  baixando v{nova}";
            faixaAtualizacao.Visible = true;
            btnFaixa.Visible = false;
            lblFaixa.Text = $"🚀 Baixando a versão {nova}... 0%";

            await Atualizador.Baixar(atualizacao, p =>
                BeginInvoke(new Action(() => lblFaixa.Text = $"🚀 Baixando a versão {nova}... {p}%")));

            atualizacaoBaixada = true;
            lblFaixa.Text = $"✅ Versão {nova} pronta pra instalar!";
            btnAtualizacaoLateral.Text = $"Atualizar p/ {nova}";
            btnAtualizacaoLateral.Visible = true;
            btnAtualizacaoLateral.BringToFront();
            ReorganizarMenu();
            consoleIa.Dizer($"Versão {nova} baixada. Clica em Atualizar no menu quando quiser.", true);
            AvisoPopup.MostrarSemEsperar("Atualização pronta", $"A versão {nova} da SEXTA-FEIRA já foi baixada. Clica em \"Atualizar\" no menu quando quiser.");
            btnFaixa.Text = "Reiniciar e atualizar";
            btnFaixa.Visible = true;
            btnFaixa.Enabled = true;
            lblVersao.Text = versao + $"  •  v{nova} pronta";
        }
        catch (Exception ex)
        {
            Atualizador.Log("ERRO no download: " + ex);
            faixaAtualizacao.Visible = true;
            lblFaixa.Text = "⚠ Não consegui baixar a atualização agora.";
            btnFaixa.Text = "Tentar de novo";
            btnFaixa.Visible = true;
            btnFaixa.Enabled = true;
            lblVersao.Text = versao;
        }
        finally
        {
            procurandoAtualizacao = false;
        }
    }
}
