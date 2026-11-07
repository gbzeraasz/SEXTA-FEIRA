using System.Diagnostics;
using Velopack;

namespace SextaFeira;

internal static class Program
{
    public const string ArgOtimizar = "--otimizar";
    public const string ArgBandeja = "--bandeja";

    [STAThread]
    private static void Main(string[] args)
    {
        // Precisa ser a primeira coisa: trata instalação/atualização do Velopack.
        VelopackApp.Build().Run();

        bool modoRapido = args.Any(a => a.Equals(ArgOtimizar, StringComparison.OrdinalIgnoreCase));

        // Já tem uma SEXTA-FEIRA aberta (ou na bandeja)? Só pede pra ela aparecer.
        if (!modoRapido && JanelaJaAberta())
        {
            try { File.WriteAllText(ArquivoAbrir, DateTime.Now.ToString("o")); } catch { }
            return;
        }

        // Quase tudo que o app faz precisa de administrador.
        if (!Sistema.EhAdmin())
        {
            if (ReabrirComoAdmin(args)) return;
            MessageBox.Show(
                "A SEXTA-FEIRA precisa de permissão de administrador pra otimizar o PC.\n\nAbre de novo e clica em \"Sim\".",
                "SEXTA-FEIRA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ApplicationConfiguration.Initialize();

        if (modoRapido)
        {
            OtimizacaoRapida.Executar();
            return;
        }

        using var mutex = new Mutex(true, NomeMutex, out bool primeira);
        if (!primeira)
        {
            try { File.WriteAllText(ArquivoAbrir, DateTime.Now.ToString("o")); } catch { }
            return;
        }
        bool naBandeja = args.Any(a => a.Equals(ArgBandeja, StringComparison.OrdinalIgnoreCase));
        Application.Run(new JanelaPrincipal(naBandeja));
        GC.KeepAlive(mutex);
    }

    private const string NomeMutex = @"Local\SEXTA-FEIRA-janela";

    /// <summary>Arquivo que uma 2ª abertura cria pra avisar a janela que já está rodando.</summary>
    public static string ArquivoAbrir => Path.Combine(Sistema.PastaDados, "abrir.sinal");

    private static bool JanelaJaAberta()
    {
        try
        {
            if (Mutex.TryOpenExisting(NomeMutex, out var m)) { m.Dispose(); return true; }
            return false;
        }
        catch (UnauthorizedAccessException) { return true; } // existe, mas foi criado como administrador
        catch { return false; }
    }

    private static bool ReabrirComoAdmin(string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                Arguments = string.Join(" ", args.Select(a => "\"" + a + "\"")),
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false; // usuário clicou em "Não"
        }
    }
}
