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
    private readonly Label lblSpecs = Tema.Rotulo("Lendo o teu PC...", Tema.Pequena, Tema.Apagado);
    private readonly BarraUso barCpu = new() { Nome = "CPU" };
    private readonly BarraUso barRam = new() { Nome = "RAM" };
    private readonly BarraUso barGpu = new() { Nome = "GPU" };
    private readonly Label lblTop = Tema.Rotulo("...", Tema.Mono, Tema.Texto);
    private Button btnOtimizar;
    private readonly Label lblStatus = Tema.Rotulo("", Tema.Negrito, Tema.Destaque);
    private readonly Label lblResultado = Tema.Rotulo("Clica em OTIMIZAR antes de abrir o jogo.", Tema.Normal, Tema.Apagado);
    private readonly Label lblContador = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private readonly Label lblNota = Tema.Rotulo("--%", new Font("Segoe UI", 38f, FontStyle.Bold), Tema.Destaque);
    private readonly Label lblNotaTxt = Tema.Rotulo("analisando...", Tema.Pequena, Tema.Apagado);
    private readonly Label lblTemp = Tema.Rotulo("-- °C", new Font("Segoe UI", 32f, FontStyle.Bold), Tema.Apagado);
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
    private readonly System.Windows.Forms.Timer timerAtualizacao = new() { Interval = 30 * 60 * 1000 };

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
        paginas["restauracao"] = MontarRestauracao();
        paginas["ferramentas"] = MontarFerramentas();
        foreach (var p in paginas.Values) conteudo.Controls.Add(p);
        Mostrar("inicio");

        timer.Tick += async (_, _) => await AtualizarMonitor();
        Shown += async (_, _) => await Carregar();
        FormClosing += (_, _) => { timer.Stop(); timerAtualizacao.Stop(); timerSaude.Stop(); Saude.Salvar(); timerComp.Stop(); ModoCompetitivo.Desligar(); };
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

    private void MontarLateral()
    {
        lateral.Dock = DockStyle.Left;
        lateral.Width = 220;
        lateral.BackColor = Tema.Lateral;

        var logo = Tema.Rotulo("SEXTA-FEIRA", new Font("Segoe UI", 17f, FontStyle.Bold), Tema.Destaque);
        logo.Location = new Point(22, 26);
        lateral.Controls.Add(logo);
        var sub = Tema.Rotulo("otimizador de jogos", Tema.Pequena, Tema.Apagado);
        sub.Location = new Point(25, 62);
        lateral.Controls.Add(sub);

        int y = 120;
        foreach (var (chave, texto) in new[]
                 { ("inicio", "⚡  Início"), ("checagem", "🩺  Checagem"), ("jogos", "📚  Biblioteca"), ("competitivo", "🏆  Competitivo"), ("restauracao", "🛟  Restauração"), ("ferramentas", "🛠  Ferramentas") })
        {
            var b = new Button
            {
                Text = texto,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Tema.Negrito,
                ForeColor = Tema.Apagado,
                BackColor = Tema.Lateral,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(196, 44),
                Location = new Point(12, y),
                Padding = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = chave
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Tema.Card;
            b.Click += (s, _) => Mostrar((string)((Button)s).Tag);
            lateral.Controls.Add(b);
            abas[chave] = b;
            y += 50;
        }

        lblVersao.Location = new Point(22, 740);
        lblVersao.MaximumSize = new Size(190, 0);
        lateral.Controls.Add(lblVersao);
    }

    private void Mostrar(string chave)
    {
        foreach (var (k, p) in paginas) p.Visible = k == chave;
        foreach (var (k, b) in abas)
        {
            b.BackColor = k == chave ? Tema.Card : Tema.Lateral;
            b.ForeColor = k == chave ? Tema.Destaque : Tema.Apagado;
        }
    }

    private Panel NovaPagina(string titulo, string subtitulo)
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fundo, AutoScroll = true, Padding = new Padding(30, 26, 30, 20) };
        var t = Tema.Rotulo(titulo, Tema.Titulo);
        t.Location = new Point(30, 24);
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
            var t = Tema.Rotulo(titulo.ToUpperInvariant(), Tema.Pequena, Tema.Apagado);
            t.Font = new Font("Segoe UI Semibold", 8.5f);
            t.Location = new Point(18, 14);
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
        faixaAtualizacao.BackColor = Tema.DestaqueEscuro;
        faixaAtualizacao.Visible = false;

        lblFaixa = Tema.Rotulo("", Tema.Negrito, Color.White);
        lblFaixa.Location = new Point(30, 15);
        faixaAtualizacao.Controls.Add(lblFaixa);

        btnFaixa = Tema.Botao("Atualizar agora", Color.White, 150, 34);
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

    private Panel MontarInicio()
    {
        var p = NovaPagina("Pronto pra jogar?", null);
        lblSpecs.Location = new Point(33, 72);
        lblSpecs.MaximumSize = new Size(LarguraUtil, 0);
        p.Controls.Add(lblSpecs);

        // ---------- Nota ----------
        var nota = Card(30, 104, 270, 220, "Teu PC está");
        lblNota.Location = new Point(14, 36);
        nota.Controls.Add(lblNota);
        lblNotaTxt.Location = new Point(20, 104);
        lblNotaTxt.MaximumSize = new Size(236, 0);
        nota.Controls.Add(lblNotaTxt);
        var verFalta = Tema.Botao("Ver o que falta", Tema.CardClaro, 150, 32);
        verFalta.Font = Tema.Pequena;
        verFalta.Location = new Point(18, 172);
        verFalta.Click += (_, _) => Mostrar("checagem");
        nota.Controls.Add(verFalta);
        p.Controls.Add(nota);

        // ---------- Ao vivo ----------
        var vivo = Card(315, 104, 300, 220, "Ao vivo");
        int y = 44;
        foreach (var b in new[] { barCpu, barRam, barGpu })
        {
            b.Location = new Point(18, y);
            b.Width = 264;
            vivo.Controls.Add(b);
            y += 54;
        }
        p.Controls.Add(vivo);

        // ---------- Temperatura ----------
        var temp = Card(630, 104, 300, 220, "Temperatura da placa de vídeo");
        lblTemp.Location = new Point(14, 36);
        temp.Controls.Add(lblTemp);
        lblTempInfo.Location = new Point(150, 50);
        temp.Controls.Add(lblTempInfo);
        lblTempExplica.Location = new Point(20, 104);
        lblTempExplica.MaximumSize = new Size(264, 0);
        temp.Controls.Add(lblTempExplica);
        var faixas = Tema.Rotulo("até 50° parada  •  até 75° jogando  •  83°+ perde FPS", new Font("Segoe UI", 7.5f), Tema.Apagado);
        faixas.Location = new Point(20, 192);
        temp.Controls.Add(faixas);
        p.Controls.Add(temp);

        // ---------- Otimizar ----------
        var otim = Card(30, 340, 900, 220);
        btnOtimizar = Tema.Botao("⚡  OTIMIZAR AGORA", Tema.Destaque, 300, 64);
        btnOtimizar.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
        btnOtimizar.Location = new Point(24, 24);
        btnOtimizar.Click += async (_, _) => await Otimizar();
        otim.Controls.Add(btnOtimizar);

        var expl = Tema.Rotulo(
            "Limpa temporários e lixeira, fecha launchers de fundo, liga Modo de Jogo e Alto Desempenho,\n" +
            "desliga a gravação em segundo plano e libera a RAM em espera. Tudo pode ser desfeito.",
            Tema.Pequena, Tema.Apagado);
        expl.Location = new Point(344, 28);
        otim.Controls.Add(expl);

        lblStatus.Location = new Point(344, 70);
        otim.Controls.Add(lblStatus);

        lblResultado.Location = new Point(24, 104);
        lblResultado.MaximumSize = new Size(850, 0);
        otim.Controls.Add(lblResultado);

        lblContador.Location = new Point(24, 190);
        otim.Controls.Add(lblContador);
        p.Controls.Add(otim);

        // ---------- Saúde ----------
        var saude = Card(30, 575, 440, 250, "Saúde do PC");
        lblSaudeTitulo.Location = new Point(18, 40);
        saude.Controls.Add(lblSaudeTitulo);
        lblSaudeDet.Location = new Point(18, 66);
        lblSaudeDet.MaximumSize = new Size(404, 0);
        saude.Controls.Add(lblSaudeDet);
        p.Controls.Add(saude);

        // ---------- O que mais pesa ----------
        var top = Card(490, 575, 440, 250, "O que mais está usando RAM");
        lblTop.Location = new Point(18, 44);
        lblTop.AutoSize = false;
        lblTop.Size = new Size(404, 190);
        top.Controls.Add(lblTop);
        p.Controls.Add(top);

        var dica = Tema.Rotulo(
            "💡 O maior salto de FPS vem das configs do jogo. Vê a aba Biblioteca.",
            Tema.Pequena, Tema.Apagado);
        dica.Location = new Point(33, 840);
        p.Controls.Add(dica);

        return p;
    }

    private async Task Otimizar()
    {
        if (ocupado) return;
        ocupado = true;
        btnOtimizar.Enabled = false;
        btnOtimizar.Text = "Otimizando...";
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
                BeginInvoke(new Action(() => lblStatus.Text = $"{pct}%  {txt}"))));

            lblResultado.ForeColor = Tema.Texto;
            lblResultado.Text = "✅ " + r.Frase() + "\n" + string.Join("  •  ", r.Feito);
            AtualizarContador();
        }
        catch (Exception ex)
        {
            lblResultado.ForeColor = Tema.Vermelho;
            lblResultado.Text = "Algo deu errado: " + ex.Message;
        }
        finally
        {
            btnOtimizar.Text = "⚡  OTIMIZAR AGORA";
            btnOtimizar.Enabled = true;
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

            barCpu.Valor = l.Cpu;
            barRam.Valor = l.Ram;
            barGpu.Valor = g.Uso >= 0 ? g.Uso : l.Gpu;
            lblTop.Text = string.Join("\n", l.Top.Select(t =>
            {
                string nome = t.Nome.Length > 26 ? t.Nome[..26] : t.Nome;
                return $"{nome,-28}{Sistema.Formatar(t.Bytes),10}";
            }));

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
            y += 62;
        }
        listaChecagem.ResumeLayout();

        int alertas = checagens.Count(c => c.Status == Status.Alerta);
        int nota = Checagens.Nota(checagens);
        lblNota.Text = $"{nota}%";
        lblNota.ForeColor = nota >= 90 ? Tema.Verde : nota >= 70 ? Tema.Destaque : Tema.Amarelo;
        lblNotaTxt.Text = nota >= 100 ? "otimizado. Tudo no ponto pra jogar! 🔥"
            : $"otimizado. {alertas} coisa(s) pra arrumar na aba Checagem" +
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
        var p = NovaPagina("Biblioteca", null);
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
        listaBiblioteca.SelectedIndexChanged += (_, _) => MostrarJogo();
        p.Controls.Add(listaBiblioteca);

        detalheJogo.Location = new Point(296, 108);
        detalheJogo.Size = new Size(634, 620);
        detalheJogo.BackColor = Tema.Card;
        detalheJogo.AutoScroll = true;
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

            if (c.PerfilAutomatico && c.Nome == "Fortnite")
            {
                y += 10;
                bool ativo = Jogos.FortniteEmModoDesempenho();
                var b = Tema.Botao(ativo ? "✅ Modo Desempenho ativo" : "Aplicar Modo Desempenho", ativo ? Tema.Verde : Tema.Destaque, 250, 38);
                b.Location = new Point(22, y);
                b.Click += (_, _) =>
                {
                    MessageBox.Show(Jogos.AplicarPerfilFortnite(), "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
    private readonly Label lblConexaoNota = Tema.Rotulo("", new Font("Segoe UI", 20f, FontStyle.Bold), Tema.Apagado);
    private readonly System.Windows.Forms.Timer timerComp = new() { Interval = 4000 };
    private ModoCompetitivo.Conexao conexaoAntes;

    private Panel MontarCompetitivo()
    {
        var p = NovaPagina("Modo Competitivo",
            "Liga antes da partida e deixa a SEXTA-FEIRA aberta (pode minimizar). Desliga quando terminar de jogar.");

        var card = Card(30, 104, 900, 270);
        btnCompetitivo = Tema.Botao("🏆  LIGAR MODO COMPETITIVO", Tema.Destaque, 340, 64);
        btnCompetitivo.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
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

        lblCompFeito.Location = new Point(24, 150);
        lblCompFeito.MaximumSize = new Size(850, 0);
        card.Controls.Add(lblCompFeito);
        p.Controls.Add(card);

        // Conexão
        var net = Card(30, 390, 900, 230, "Estabilidade da tua internet");
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
        lblCompJogo.Text = !on ? "" : ModoCompetitivo.JogoAtual != null
            ? $"Jogo detectado: {ModoCompetitivo.JogoAtual} (com prioridade)  •  ligado desde {ModoCompetitivo.LigadoEm:HH:mm}"
            : $"Esperando tu abrir o jogo...  •  ligado desde {ModoCompetitivo.LigadoEm:HH:mm}";
        abas["competitivo"].Text = on ? "🏆  Competitivo  🟢" : "🏆  Competitivo";
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
    //  Página: Restauração
    // =====================================================================

    private Panel MontarRestauracao()
    {
        var p = NovaPagina("Restauração",
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

        // Atualização vem PRIMEIRO e em paralelo com o resto (antes ficava pro final)
        Task tarefaAtualizacao = Configuracao.Atual.AtualizarAutomaticamente ? ProcurarAtualizacao(false) : Task.CompletedTask;
        timerAtualizacao.Tick += async (_, _) => await ProcurarAtualizacao(false);
        timerAtualizacao.Start();
        _ = AtualizarMonitor();
        AtualizarContador();

        _ = AtualizarSaude();
        _ = CarregarPontos();
        timerSaude.Tick += async (_, _) => await AtualizarSaude();
        timerSaude.Start();

        pc = await Task.Run(() => InfoPC.Ler());
        lblSpecs.Text = pc.Resumo();

        jogos = await Task.Run(() => Jogos.Detectar());
        nivel = ConfigsCompetitivas.Nivel(pc.Gpu);
        biblioteca = await Task.Run(() => Biblioteca.Listar());
        PreencherJogos();
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
