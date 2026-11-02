namespace SextaFeira;

/// <summary>
/// Aviso próprio da SEXTA-FEIRA no canto da tela, em cima da barra de tarefas.
/// Diferente da notificação do Windows, aparece mesmo com o "Não perturbe" ligado
/// e não rouba o foco do jogo.
/// </summary>
internal class AvisoPopup : Form
{
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 15 };
    private int fase;            // 0 = aparecendo, 1 = parado, 2 = sumindo
    private int ticksParado;
    private const int TempoNaTela = 5500; // ms

    public AvisoPopup(string titulo, string texto, bool erro = false)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Tema.Card;
        Opacity = 0;
        Width = 400;

        var cor = erro ? Tema.Vermelho : Tema.Amarelo;

        // faixa colorida na esquerda
        Controls.Add(new Panel { BackColor = cor, Dock = DockStyle.Left, Width = 5 });

        var icone = new PictureBox
        {
            Image = CarregarRaio(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(40, 40),
            Location = new Point(20, 18),
            BackColor = Color.Transparent
        };
        Controls.Add(icone);

        var lblTitulo = Tema.Rotulo(titulo, Tema.Negrito, cor);
        lblTitulo.Location = new Point(74, 14);
        Controls.Add(lblTitulo);

        var lblTexto = Tema.Rotulo(texto, Tema.Normal, Tema.Texto);
        lblTexto.Location = new Point(74, 40);
        lblTexto.MaximumSize = new Size(Width - 74 - 18, 0);
        Controls.Add(lblTexto);

        var lblMarca = Tema.Rotulo("SEXTA-FEIRA", new Font("Segoe UI Semibold", 7.5f), Tema.Apagado);
        Controls.Add(lblMarca);

        // altura de acordo com o texto
        int alturaTexto = TextRenderer.MeasureText(texto, Tema.Normal, new Size(lblTexto.MaximumSize.Width, 0), TextFormatFlags.WordBreak).Height;
        Height = Math.Max(84, 40 + alturaTexto + 30);
        lblMarca.Location = new Point(74, Height - 22);

        var area = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);

        try { Region = new Region(Tema.Arredondado(new Rectangle(0, 0, Width, Height), 10)); } catch { }

        // clicar fecha
        Click += (_, _) => fase = 2;
        foreach (Control c in Controls) c.Click += (_, _) => fase = 2;

        anim.Tick += Animar;
        Shown += (_, _) => anim.Start();
    }

    private static Image CarregarRaio()
    {
        try
        {
            string raio = Path.Combine(AppContext.BaseDirectory, "raio.ico");
            if (File.Exists(raio)) return new Icon(raio, 48, 48).ToBitmap();
        }
        catch { }
        return Tema.Icone.ToBitmap();
    }

    private void Animar(object sender, EventArgs e)
    {
        switch (fase)
        {
            case 0:
                Opacity = Math.Min(1, Opacity + 0.08);
                if (Opacity >= 1) fase = 1;
                break;
            case 1:
                ticksParado += anim.Interval;
                if (ticksParado >= TempoNaTela) fase = 2;
                break;
            default:
                Opacity = Math.Max(0, Opacity - 0.06);
                if (Opacity <= 0) { anim.Stop(); Close(); }
                break;
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

    /// <summary>Mostra o aviso e espera ele sumir.</summary>
    public static void Mostrar(string titulo, string texto, bool erro = false)
    {
        using var aviso = new AvisoPopup(titulo, texto, erro);
        Application.Run(aviso);
    }
}
