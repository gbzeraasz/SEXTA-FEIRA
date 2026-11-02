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

    // ---------- checagem ----------
    private readonly Panel listaChecagem = new();
    private readonly Label lblResumoChecagem = Tema.Rotulo("", Tema.Negrito);

    // ---------- jogos ----------
    private readonly Panel listaJogos = new();

    // ---------- ferramentas ----------
    private readonly Label lblAtalho = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private Button btnAtalho;
    private readonly Label lblVersao = Tema.Rotulo("", Tema.Pequena, Tema.Apagado);
    private readonly TextBox txtProgramas = new();

    private readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };

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
        paginas["ferramentas"] = MontarFerramentas();
        foreach (var p in paginas.Values) conteudo.Controls.Add(p);
        Mostrar("inicio");

        timer.Tick += async (_, _) => await AtualizarMonitor();
        Shown += async (_, _) => await Carregar();
        FormClosing += (_, _) => timer.Stop();
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
                 { ("inicio", "⚡  Início"), ("checagem", "🩺  Checagem"), ("jogos", "🎮  Jogos"), ("ferramentas", "🛠  Ferramentas") })
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

    private async Task InstalarAtualizacao()
    {
        if (atualizacao == null) return;
        btnFaixa.Enabled = false;
        try
        {
            await Atualizador.BaixarEInstalar(atualizacao, p => BeginInvoke(new Action(() => lblFaixa.Text = $"Baixando atualização... {p}%")));
        }
        catch (Exception ex)
        {
            lblFaixa.Text = "Não deu pra atualizar agora: " + ex.Message;
            btnFaixa.Enabled = true;
        }
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

        // Card ao vivo
        var vivo = Card(30, 110, 440, 210, "Ao vivo");
        int y = 44;
        foreach (var b in new[] { barCpu, barRam, barGpu })
        {
            b.Location = new Point(18, y);
            b.Width = 404;
            vivo.Controls.Add(b);
            y += 52;
        }
        p.Controls.Add(vivo);

        // Card o que mais pesa
        var top = Card(490, 110, 440, 210, "O que mais está usando RAM");
        lblTop.Location = new Point(18, 44);
        lblTop.AutoSize = false;
        lblTop.Size = new Size(404, 150);
        top.Controls.Add(lblTop);
        p.Controls.Add(top);

        // Card otimizar
        var otim = Card(30, 340, 900, 250);
        btnOtimizar = Tema.Botao("⚡  OTIMIZAR AGORA", Tema.Destaque, 300, 64);
        btnOtimizar.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
        btnOtimizar.Location = new Point(24, 26);
        btnOtimizar.Click += async (_, _) => await Otimizar();
        otim.Controls.Add(btnOtimizar);

        var expl = Tema.Rotulo(
            "Limpa temporários e lixeira, fecha launchers de fundo, liga Modo de Jogo e Alto Desempenho,\n" +
            "desliga a gravação em segundo plano e libera a RAM em espera. Tudo pode ser desfeito.",
            Tema.Pequena, Tema.Apagado);
        expl.Location = new Point(344, 30);
        otim.Controls.Add(expl);

        lblStatus.Location = new Point(344, 74);
        otim.Controls.Add(lblStatus);

        lblResultado.Location = new Point(24, 112);
        lblResultado.MaximumSize = new Size(850, 0);
        otim.Controls.Add(lblResultado);

        lblContador.Location = new Point(24, 216);
        otim.Controls.Add(lblContador);
        p.Controls.Add(otim);

        var dica = Tema.Rotulo(
            "💡 Com a GTX 1050 Ti, o maior salto de FPS vem das configs do jogo. Vê a aba Jogos.",
            Tema.Pequena, Tema.Apagado);
        dica.Location = new Point(33, 606);
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
            var r = await Task.Run(() => Otimizador.Executar((pct, txt) =>
                BeginInvoke(new Action(() => lblStatus.Text = $"{pct}%  {txt}"))));

            lblResultado.ForeColor = Tema.Texto;
            lblResultado.Text = "✅ PC otimizado! Bom jogo.\n\n" + r.Resumo() + "\n• " + string.Join("\n• ", r.Feito);
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
    }

    private void AtualizarContador()
    {
        var h = Historico.Carregar();
        if (h.Count == 0) { lblContador.Text = ""; return; }
        double disco = h.Sum(x => x.Disco);
        lblContador.Text = $"Já otimizou {h.Count}x  •  {Sistema.Formatar(disco)} limpos no total  •  última: {h[^1].Quando:dd/MM HH:mm}";
    }

    private async Task AtualizarMonitor()
    {
        if (lendoMonitor) return;
        lendoMonitor = true;
        try
        {
            var l = await Task.Run(() => MonitorPC.Ler());
            barCpu.Valor = l.Cpu;
            barRam.Valor = l.Ram;
            barGpu.Valor = l.Gpu;
            lblTop.Text = string.Join("\n", l.Top.Select(t =>
            {
                string nome = t.Nome.Length > 26 ? t.Nome[..26] : t.Nome;
                return $"{nome,-28}{Sistema.Formatar(t.Bytes),10}";
            }));
        }
        catch { }
        finally { lendoMonitor = false; }
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
        lblResumoChecagem.Text = alertas == 0 ? "✅ Tudo certo no teu PC!" : $"⚠ {alertas} ponto(s) de atenção";
        lblResumoChecagem.ForeColor = alertas == 0 ? Tema.Verde : Tema.Amarelo;
    }

    // =====================================================================
    //  Página: Jogos
    // =====================================================================

    private Panel MontarJogos()
    {
        var p = NovaPagina("Jogos", "Configs recomendadas pro teu PC. O app nunca mexe no jogo em execução nem injeta nada: seguro com Vanguard, VAC e FACEIT.");
        listaJogos.Location = new Point(30, 110);
        listaJogos.Size = new Size(900, 610);
        listaJogos.AutoScroll = true;
        listaJogos.BackColor = Tema.Fundo;
        p.Controls.Add(listaJogos);
        return p;
    }

    private void PreencherJogos()
    {
        listaJogos.SuspendLayout();
        listaJogos.Controls.Clear();

        if (jogos.Count == 0)
        {
            var l = Tema.Rotulo("Não achei CS2, Valorant nem Fortnite instalados.", Tema.Normal, Tema.Apagado);
            l.Location = new Point(4, 4);
            listaJogos.Controls.Add(l);
            listaJogos.ResumeLayout();
            return;
        }

        int y = 0;
        foreach (var j in jogos)
        {
            int altura = 70 + j.Dicas.Length * 24 + (j.TemPerfilAutomatico ? 56 : 0);
            var c = Card(0, y, 876, altura);

            var nome = Tema.Rotulo(j.Nome, Tema.Subtitulo, Tema.Destaque);
            nome.Location = new Point(18, 14);
            c.Controls.Add(nome);

            var disco = Tema.Rotulo($"{j.Disco}  •  {j.Pasta}", Tema.Pequena, j.Disco == "HDD" ? Tema.Amarelo : Tema.Apagado);
            disco.Location = new Point(20, 44);
            c.Controls.Add(disco);

            int yy = 72;
            foreach (var d in j.Dicas)
            {
                var l = Tema.Rotulo("•  " + d, Tema.Normal);
                l.Location = new Point(20, yy);
                c.Controls.Add(l);
                yy += 24;
            }

            if (j.TemPerfilAutomatico && j.Nome == "Fortnite")
            {
                bool ativo = Jogos.FortniteEmModoDesempenho();
                var b = Tema.Botao(ativo ? "✅ Modo Desempenho ativo" : "Aplicar Modo Desempenho", ativo ? Tema.Verde : Tema.Destaque, 240, 38);
                b.Location = new Point(20, yy + 8);
                b.Click += (_, _) =>
                {
                    string msg = Jogos.AplicarPerfilFortnite();
                    MessageBox.Show(msg, "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    PreencherJogos();
                };
                c.Controls.Add(b);
            }

            listaJogos.Controls.Add(c);
            y += altura + 16;
        }
        listaJogos.ResumeLayout();
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
        _ = AtualizarMonitor();
        AtualizarContador();

        pc = await Task.Run(() => InfoPC.Ler());
        lblSpecs.Text = pc.Resumo();

        jogos = await Task.Run(() => Jogos.Detectar());
        PreencherJogos();
        AtualizarAtalho();

        // Primeira vez: já instala o botão da barra sozinho
        if (!Atalho.Instalado() && !File.Exists(Path.Combine(Sistema.PastaDados, "atalho-oferecido")))
        {
            File.WriteAllText(Path.Combine(Sistema.PastaDados, "atalho-oferecido"), "1");
            if (Atalho.Instalar())
            {
                AtualizarAtalho();
                MessageBox.Show(
                    "Criei o botão \"Otimizar PC (SEXTA-FEIRA)\" no Menu Iniciar.\n\n" +
                    "Pra deixar ele na barra: abre o Iniciar, pesquisa \"Otimizar PC\", clica com o botão direito e escolhe \"Fixar na barra de tarefas\".",
                    "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        await RodarChecagem();
        if (Configuracao.Atual.AtualizarAutomaticamente) await ProcurarAtualizacao(false);
    }

    private async Task ProcurarAtualizacao(bool avisarSeNaoTiver)
    {
        atualizacao = await Atualizador.Verificar();
        if (atualizacao != null)
        {
            lblFaixa.Text = $"🚀 Nova versão {atualizacao.TargetFullRelease.Version} disponível!";
            faixaAtualizacao.Visible = true;
        }
        else if (avisarSeNaoTiver)
        {
            MessageBox.Show(Atualizador.VersaoAtual == "dev"
                ? "Essa cópia não foi instalada pelo instalador, então não atualiza sozinha."
                : "Tu já tá na versão mais nova!", "SEXTA-FEIRA");
        }
    }
}
