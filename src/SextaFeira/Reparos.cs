using System.Diagnostics;

namespace SextaFeira;

/// <summary>Reparos rápidos usando só comandos oficiais do Windows.</summary>
internal class Reparo
{
    public string Id;
    public string Nome;
    public string Icone;
    public string Descricao;
    public string Duracao;
    public bool PedeReinicio;
    public bool Revisao;            // pergunta antes (demora ou derruba algo por uns segundos)
    public string Aviso;            // o que avisar antes de rodar
    public Func<string> Executar;   // devolve a mensagem pro usuário
}

internal static class Reparos
{
    private static string Windir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static string PS(string comando, int timeout = 60000) =>
        Sistema.Rodar("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{comando}\"", timeout);

    private static void Servico(string nome, bool parar)
    {
        Sistema.Rodar("net", (parar ? "stop " : "start ") + nome + (parar ? " /y" : ""), 60000);
    }

    public static readonly List<Reparo> Lista = new()
    {
        new Reparo
        {
            Id = "internet", Nome = "Reparar internet", Icone = "🌐", Duracao = "~30 s", PedeReinicio = true, Revisao = true,
            Descricao = "Limpa o DNS, reseta o Winsock e a pilha de rede e pede um IP novo. Resolve 'sem internet' e site que não abre.",
            Aviso = "A internet vai cair por alguns segundos e é bom reiniciar o PC depois.",
            Executar = () =>
            {
                Sistema.Rodar("ipconfig", "/flushdns");
                Sistema.Rodar("netsh", "winsock reset");
                Sistema.Rodar("netsh", "int ip reset");
                Sistema.Rodar("ipconfig", "/release", 30000);
                Sistema.Rodar("ipconfig", "/renew", 60000);
                return "Rede resetada. Reinicia o PC pra terminar o reparo.";
            }
        },
        new Reparo
        {
            Id = "placas", Nome = "Reativar Wi-Fi e cabo", Icone = "📶", Duracao = "~15 s", Revisao = true,
            Descricao = "Desliga e liga de novo as placas de rede. Resolve Wi-Fi que some ou cabo que não conecta.",
            Aviso = "A internet vai cair por uns 10 segundos.",
            Executar = () =>
            {
                Sistema.Rodar("net", "start WlanSvc");
                PS("Get-NetAdapter -Physical | Restart-NetAdapter -Confirm:$false", 60000);
                return "Placas de rede reiniciadas.";
            }
        },
        new Reparo
        {
            Id = "audio", Nome = "Reparar áudio", Icone = "🔊", Duracao = "~10 s",
            Descricao = "Reinicia os serviços de som do Windows. Resolve som que sumiu, chiado ou fone que não aparece.",
            Executar = () =>
            {
                PS("Restart-Service -Name AudioEndpointBuilder -Force; Start-Service -Name Audiosrv");
                return "Serviços de áudio reiniciados.";
            }
        },
        new Reparo
        {
            Id = "mic", Nome = "Reparar microfone", Icone = "🎙", Duracao = "~10 s",
            Descricao = "Reinicia o áudio e abre a permissão de microfone pra tu conferir se os apps podem usar.",
            Executar = () =>
            {
                PS("Restart-Service -Name AudioEndpointBuilder -Force; Start-Service -Name Audiosrv");
                Sistema.Abrir("ms-settings:privacy-microphone");
                return "Áudio reiniciado. Confere se o acesso ao microfone está liberado na janela que abriu.";
            }
        },
        new Reparo
        {
            Id = "bluetooth", Nome = "Reparar Bluetooth", Icone = "🔵", Duracao = "~10 s",
            Descricao = "Reinicia o serviço de Bluetooth. Resolve fone ou controle que não conecta.",
            Executar = () =>
            {
                PS("Set-Service -Name bthserv -StartupType Manual; Restart-Service -Name bthserv -Force");
                return "Bluetooth reiniciado.";
            }
        },
        new Reparo
        {
            Id = "camera", Nome = "Reparar câmera", Icone = "📷", Duracao = "~10 s",
            Descricao = "Reinicia o serviço da câmera e abre a permissão de câmera.",
            Executar = () =>
            {
                PS("Restart-Service -Name FrameServer -Force -ErrorAction SilentlyContinue");
                Sistema.Abrir("ms-settings:privacy-webcam");
                return "Câmera reiniciada. Confere a permissão na janela que abriu.";
            }
        },
        new Reparo
        {
            Id = "update", Nome = "Reparar Windows Update", Icone = "🔄", Duracao = "~1 min", PedeReinicio = true,
            Descricao = "Para os serviços, recria as pastas de atualização e liga de novo. Resolve atualização travada ou com erro.",
            Executar = () =>
            {
                foreach (var s in new[] { "wuauserv", "bits", "cryptsvc", "msiserver" }) Servico(s, true);
                string carimbo = DateTime.Now.ToString("yyyyMMddHHmm");
                try { Directory.Move(Path.Combine(Windir, "SoftwareDistribution"), Path.Combine(Windir, "SoftwareDistribution.old" + carimbo)); } catch { }
                try { Directory.Move(Path.Combine(Windir, @"System32\catroot2"), Path.Combine(Windir, @"System32\catroot2.old" + carimbo)); } catch { }
                foreach (var s in new[] { "cryptsvc", "bits", "wuauserv" }) Servico(s, false);
                return "Windows Update reparado. Reinicia o PC e tenta atualizar de novo.";
            }
        },
        new Reparo
        {
            Id = "loja", Nome = "Reparar Loja da Microsoft", Icone = "🛍", Duracao = "~20 s",
            Descricao = "Limpa o cache da Loja (ferramenta oficial wsreset). Resolve download travado e loja que não abre.",
            Executar = () =>
            {
                Sistema.Rodar("wsreset.exe", "", 120000);
                return "Cache da Loja limpo. Ela abre sozinha quando terminar.";
            }
        },
        new Reparo
        {
            Id = "menu", Nome = "Reparar menu Iniciar e barra de tarefas", Icone = "🪟", Duracao = "~10 s",
            Descricao = "Reinicia o Explorer e o menu Iniciar. Resolve barra travada, Iniciar que não abre e ícones sumidos.",
            Executar = () =>
            {
                foreach (var n in new[] { "StartMenuExperienceHost", "ShellExperienceHost", "SearchHost" })
                    foreach (var p in Process.GetProcessesByName(n)) try { p.Kill(); } catch { }
                Sistema.Rodar("taskkill", "/f /im explorer.exe");
                Thread.Sleep(3000);
                // O Windows reabre o Explorer sozinho; se não reabrir, abre aqui
                if (Process.GetProcessesByName("explorer").Length == 0)
                    try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); } catch { }
                return "Explorer e menu Iniciar reiniciados.";
            }
        },
        new Reparo
        {
            Id = "arquivos", Nome = "Reparar arquivos do Windows", Icone = "🧩", Duracao = "15 a 40 min", PedeReinicio = true, Revisao = true,
            Descricao = "Ferramentas oficiais DISM e SFC: acham e consertam arquivos corrompidos do Windows.",
            Aviso = "Demora bastante (15 a 40 minutos). Pode usar o PC enquanto isso, mas evita jogar.",
            Executar = () =>
            {
                Sistema.Rodar("DISM", "/Online /Cleanup-Image /RestoreHealth", 60 * 60 * 1000);
                string sfc = Sistema.Rodar("sfc", "/scannow", 60 * 60 * 1000).Replace("\0", "");
                bool achou = sfc.Contains("reparou", StringComparison.OrdinalIgnoreCase) || sfc.Contains("repaired", StringComparison.OrdinalIgnoreCase);
                return achou ? "Arquivos corrompidos encontrados e reparados! Reinicia o PC."
                             : "Verificação concluída. Se tinha algo, foi reparado. Reinicia o PC.";
            }
        },
        new Reparo
        {
            Id = "disco", Nome = "Verificar erros no disco C", Icone = "💽", Duracao = "2 a 10 min",
            Descricao = "Verifica o disco C com a ferramenta oficial (chkdsk) sem precisar reiniciar.",
            Executar = () =>
            {
                string s = Sistema.Rodar("chkdsk", "C: /scan", 30 * 60 * 1000);
                bool ok = s.Contains("nenhum problema", StringComparison.OrdinalIgnoreCase) || s.Contains("no problems", StringComparison.OrdinalIgnoreCase);
                return ok ? "Disco C sem problemas. ✓" : "Verificação concluída. Se ele pedir, reinicia o PC pra terminar o reparo.";
            }
        },
        new Reparo
        {
            Id = "desligar", Nome = "PC não desliga direito", Icone = "⏻", Duracao = "instantâneo", Revisao = true,
            Descricao = "Desliga a 'Inicialização rápida', que costuma travar o desligamento e causar bug ao ligar. O PC pode ligar 1 a 2 segundos mais devagar.",
            Aviso = "O PC pode ligar 1 a 2 segundos mais devagar. Dá pra desfazer em Ferramentas → Desfazer mudanças.",
            Executar = () =>
            {
                Sistema.GravarDword(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0);
                return "Inicialização rápida desligada. Da próxima vez, o PC desliga de verdade.";
            }
        },
    };
}
