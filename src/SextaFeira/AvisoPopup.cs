namespace SextaFeira;

/// <summary>
/// Aviso discreto da SEXTA-FEIRA no canto superior direito.
/// Aparece mesmo com o "Não perturbe" ligado e não rouba o foco do jogo.
/// </summary>
internal class AvisoPopup : Form
{
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 15 };
    private int fase;              // 0 = entrando, 1 = parado, 2 = saindo
    private int msParado;
    private const int TempoNaTela = 4000;
    private const double OpacidadeMax = 0.92;
    private const int Margem = 14;
    private const int Deslize = 24;  // quantos pixels ele desliza ao entrar
    private int xFinal;

    public AvisoPopup(string titulo, string texto, bool erro = false)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(18, 20, 30);
        Opacity = 0;
        Width = 300;

        var cor = erro ? Tema.Vermelho : Tema.Destaque;

        var icone = new PictureBox
        {
            Image = CarregarRaio(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(22, 22),
            Location = new Point(12, 12),
            BackColor = Color.Transparent
        };
        Controls.Add(icone);

        var lblTitulo = Tema.Rotulo(titulo, new Font("Segoe UI Semibold", 9f), Tema.Texto);
        lblTitulo.Location = new Point(42, 9);
        Controls.Add(lblTitulo);

        var lblTexto = Tema.Rotulo(texto, new Font("Segoe UI", 8.5f), Tema.Apagado);
        lblTexto.Location = new Point(42, 28);
        lblTexto.MaximumSize = new Size(Width - 42 - 12, 0);
        Controls.Add(lblTexto);

        int alturaTexto = TextRenderer.MeasureText(texto, lblTexto.Font,
            new Size(lblTexto.MaximumSize.Width, 0), TextFormatFlags.WordBreak).Height;
        Height = Math.Max(48, 28 + alturaTexto + 10);

        // linha fininha embaixo, na cor do aviso
        Controls.Add(new Panel { BackColor = cor, Dock = DockStyle.Bottom, Height = 2 });

        var area = Screen.PrimaryScreen!.WorkingArea;
        xFinal = area.Right - Width - Margem;
        Location = new Point(xFinal + Deslize, area.Top + Margem);

        try { Region = new Region(Tema.Arredondado(new Rectangle(0, 0, Width, Height), 8)); } catch { }

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
            if (File.Exists(raio)) return new Icon(raio, 32, 32).ToBitmap();
        }
        catch { }
        return Tema.Icone.ToBitmap();
    }

    private void Animar(object sender, EventArgs e)
    {
        switch (fase)
        {
            case 0: // desliza da direita e aparece
                Opacity = Math.Min(OpacidadeMax, Opacity + 0.07);
                Left = Math.Max(xFinal, Left - 3);
                if (Opacity >= OpacidadeMax && Left == xFinal) fase = 1;
                break;
            case 1:
                msParado += anim.Interval;
                if (msParado >= TempoNaTela) fase = 2;
                break;
            default: // some devagar
                Opacity = Math.Max(0, Opacity - 0.05);
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
