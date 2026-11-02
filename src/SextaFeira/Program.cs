using System.Diagnostics;
using Velopack;

namespace SextaFeira;

internal static class Program
{
    public const string ArgOtimizar = "--otimizar";

    [STAThread]
    private static void Main(string[] args)
    {
        // Precisa ser a primeira coisa: trata instalação/atualização do Velopack.
        VelopackApp.Build().Run();

        bool modoRapido = args.Any(a => a.Equals(ArgOtimizar, StringComparison.OrdinalIgnoreCase));

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

        Application.Run(new JanelaPrincipal());
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
