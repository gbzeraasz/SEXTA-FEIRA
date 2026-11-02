using Microsoft.Win32;

namespace SextaFeira;

internal enum Status { Ok, Alerta, Info }

internal class Checagem
{
    public string Nome;
    public Status Status;
    public string Detalhe;
    public string Botao;            // texto do botão (null = sem botão)
    public Func<string> Acao;       // retorna mensagem opcional pro usuário
    public bool Automatica;         // entra no "Corrigir tudo"
}

/// <summary>Tudo que o app confere no PC. Baseado na pesquisa: só itens com efeito comprovado.</summary>
internal static class Checagens
{
    /// <summary>Nota de 0 a 100: itens OK sobre os que contam (os "Info" são escolha tua e não pesam).</summary>
    public static int Nota(List<Checagem> lista)
    {
        var contam = lista.Where(c => c.Status != Status.Info).ToList();
        if (contam.Count == 0) return 100;
        return (int)Math.Round(contam.Count(c => c.Status == Status.Ok) * 100.0 / contam.Count);
    }

    public static List<Checagem> Rodar(InfoPC pc, List<Jogos.Jogo> jogos)
    {
        var l = new List<Checagem>();

        // ---------- Windows ----------
        int? gm = Sistema.LerRegInt(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled");
        l.Add(new Checagem
        {
            Nome = "Modo de Jogo",
            Status = gm == 0 ? Status.Alerta : Status.Ok,
            Detalhe = gm == 0 ? "Desligado. Ajuda a reduzir travadas quando tem coisa aberta junto." : "Ligado",
            Botao = "Ligar",
            Automatica = true,
            Acao = () => { Sistema.GravarDword(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1); return null; }
        });

        int? dvr = Sistema.LerRegInt(@"HKCU\System\GameConfigStore", "GameDVR_Enabled");
        int? cap = Sistema.LerRegInt(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
        bool dvrLigado = dvr != 0 || cap == 1;
        l.Add(new Checagem
        {
            Nome = "Gravação em segundo plano",
            Status = dvrLigado ? Status.Alerta : Status.Ok,
            Detalhe = dvrLigado ? "Ligada. Fica gravando o jogo escondido e gasta desempenho." : "Desligada",
            Botao = "Desligar",
            Automatica = true,
            Acao = () =>
            {
                Sistema.GravarDword(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0);
                Sistema.GravarDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
                return null;
            }
        });

        if (pc.Build >= 22000)
        {
            bool janela = Otimizador.JogosEmJanelaAtivo();
            l.Add(new Checagem
            {
                Nome = "Otimizações para jogos em janela",
                Status = janela ? Status.Ok : Status.Alerta,
                Detalhe = janela ? "Ligadas" : "Desligadas. Diminui a latência em jogos DX10/DX11 em janela ou sem bordas.",
                Botao = "Ligar",
                Automatica = true,
                Acao = () => { Otimizador.AtivarJogosEmJanela(); return null; }
            });
        }

        string plano = Otimizador.PlanoAtivo();
        string nomePlano = Sistema.Rodar("powercfg", "/getactivescheme");
        int abre = nomePlano.LastIndexOf('('), fecha = nomePlano.LastIndexOf(')');
        nomePlano = abre >= 0 && fecha > abre ? nomePlano[(abre + 1)..fecha] : "?";
        bool altoDes = plano == Otimizador.PlanoAltoDesempenho ||
                       nomePlano.Contains("Alto desempenho", StringComparison.OrdinalIgnoreCase) ||
                       nomePlano.Contains("High performance", StringComparison.OrdinalIgnoreCase) ||
                       nomePlano.Contains("Desempenho Máximo", StringComparison.OrdinalIgnoreCase) ||
                       nomePlano.Contains("Ultimate", StringComparison.OrdinalIgnoreCase);
        l.Add(new Checagem
        {
            Nome = "Plano de energia",
            Status = altoDes ? Status.Ok : Status.Alerta,
            Detalhe = nomePlano,
            Botao = "Alto desempenho",
            Automatica = true,
            Acao = () => { Otimizador.AtivarAltoDesempenho(); return null; }
        });

        int? hags = Sistema.LerRegInt(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode");
        l.Add(new Checagem
        {
            Nome = "Agendamento de GPU por hardware",
            Status = hags == 2 ? Status.Ok : Status.Info,
            Detalhe = hags == 2 ? "Ligado" : "Desligado. Ganho pequeno e varia por jogo: vale testar (precisa reiniciar).",
            Botao = "Ligar",
            Acao = () =>
            {
                Sistema.GravarDword(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
                return "Ligado! Reinicia o PC pra valer.";
            }
        });

        // ---------- Latência e consistência ----------
        bool acel = Ajustes.AceleracaoMouseLigada();
        l.Add(new Checagem
        {
            Nome = "Aceleração do mouse",
            Status = acel ? Status.Alerta : Status.Ok,
            Detalhe = acel ? "Ligada: a mira anda diferente dependendo da velocidade do teu movimento. Pro competitivo, desliga." : "Desligada",
            Botao = "Desligar",
            Automatica = true,
            Acao = () => { Ajustes.DesligarAceleracaoMouse(); return null; }
        });

        int usb = Ajustes.UsbSeletivaAtual();
        l.Add(new Checagem
        {
            Nome = "Suspensão seletiva de USB",
            Status = usb == 0 ? Status.Ok : usb < 0 ? Status.Info : Status.Alerta,
            Detalhe = usb == 0 ? "Desligada" : usb < 0 ? "Não deu pra ler" : "Ligada: o Windows pode 'adormecer' mouse e teclado USB.",
            Botao = "Desligar",
            Automatica = true,
            Acao = () => { Ajustes.DesligarUsbSeletiva(); return null; }
        });

        bool thr = Ajustes.ThrottlingLigado();
        l.Add(new Checagem
        {
            Nome = "Power Throttling",
            Status = thr ? Status.Alerta : Status.Ok,
            Detalhe = thr ? "Ligado: o Windows pode segurar o processador em programas que ele acha que são de fundo." : "Desligado",
            Botao = "Desligar",
            Automatica = true,
            Acao = () => { Ajustes.DesligarThrottling(); return null; }
        });

        int rede = Ajustes.PlacasComEconomia();
        if (rede >= 0)
            l.Add(new Checagem
            {
                Nome = "Economia de energia da placa de rede",
                Status = rede == 0 ? Status.Ok : Status.Alerta,
                Detalhe = rede == 0 ? "Desligada" : "Ligada: o Windows pode desligar a placa de rede pra economizar e causar picos de ping.",
                Botao = "Desligar",
                Automatica = true,
                Acao = () => { Ajustes.DesligarEconomiaRede(); return "Pronto! Vale depois de reiniciar o PC (assim tua internet não cai agora)."; }
            });

        // ---------- Segurança (troca: decisão do usuário) ----------
        var dg = Sistema.Wmi("SELECT VirtualizationBasedSecurityStatus FROM Win32_DeviceGuard", @"root\Microsoft\Windows\DeviceGuard").FirstOrDefault();
        int? hvci = Sistema.LerRegInt(@"HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
        bool vbs = (dg != null && Sistema.WmiTexto(dg, "VirtualizationBasedSecurityStatus") == "2") || hvci == 1;
        l.Add(new Checagem
        {
            Nome = "Integridade de Memória (VBS)",
            Status = vbs ? Status.Info : Status.Ok,
            Detalhe = vbs ? "Ligada. Custa uns 4-6% de FPS, mas protege contra malware. Decisão tua." : "Desligada",
            Botao = vbs ? "Abrir config." : null,
            Acao = () => { Sistema.Abrir("windowsdefender://coreisolation"); return null; }
        });

        // ---------- Hardware ----------
        l.Add(new Checagem
        {
            Nome = "RAM: dual channel",
            Status = pc.Pentes >= 2 ? Status.Ok : Status.Alerta,
            Detalhe = pc.Pentes >= 2 ? $"{pc.Pentes} pentes, {pc.RamGB} GB" : "Só 1 pente. Com 2 pentes iguais o Ryzen rende mais, principalmente nas travadas."
        });

        l.Add(new Checagem
        {
            Nome = "RAM: XMP / DOCP",
            Status = pc.RamMHz >= 2900 ? Status.Ok : Status.Alerta,
            Detalhe = pc.RamMHz >= 2900 ? $"Rodando a {pc.RamMHz} MHz" :
                $"Rodando a {pc.RamMHz} MHz. Liga o XMP/DOCP na BIOS (Ryzen gosta de 3200 MHz). Isso só dá pra fazer na BIOS."
        });

        if (pc.DriverNvidia != null)
        {
            int.TryParse(pc.DriverNvidia.Split('.')[0], out int maior);
            bool serie10 = pc.Gpu.Contains("GTX 10", StringComparison.OrdinalIgnoreCase);
            l.Add(new Checagem
            {
                Nome = "Driver NVIDIA",
                Status = maior >= 580 ? Status.Ok : Status.Alerta,
                Detalhe = maior >= 580 ? $"Versão {pc.DriverNvidia}" :
                    $"Versão {pc.DriverNvidia}." + (serie10 ? " Instala o último da série 580 (é o último pra GTX 10)." : " Tem driver mais novo."),
                Botao = maior >= 580 ? null : "Baixar",
                Acao = () => { Sistema.Abrir("https://www.nvidia.com/pt-br/drivers/"); return null; }
            });
        }

        // ---------- Armazenamento ----------
        try
        {
            var c = new DriveInfo("C");
            double pct = c.AvailableFreeSpace * 100d / c.TotalSize;
            l.Add(new Checagem
            {
                Nome = "Espaço no disco C",
                Status = pct < 15 ? Status.Alerta : Status.Ok,
                Detalhe = $"{Sistema.Formatar(c.AvailableFreeSpace)} livres ({pct:0}%)" + (pct < 15 ? ". SSD cheio fica lento: deixa pelo menos 15% livre." : ""),
                Botao = pct < 15 ? "Liberar espaço" : null,
                Acao = () => { Sistema.Abrir("ms-settings:storagesense"); return null; }
            });
        }
        catch { }

        foreach (var j in jogos)
        {
            l.Add(new Checagem
            {
                Nome = $"{j.Nome}: onde está instalado",
                Status = j.Disco == "HDD" ? Status.Alerta : j.Disco == "SSD" ? Status.Ok : Status.Info,
                Detalhe = j.Disco == "HDD" ? "No HD! Move pro SSD pelo launcher do jogo: carrega mais rápido e trava menos."
                                           : $"No {j.Disco} ({j.Pasta[..2]})"
            });
        }

        // ---------- Inicialização ----------
        int ini = Sistema.Wmi("SELECT Name FROM Win32_StartupCommand").Count;
        l.Add(new Checagem
        {
            Nome = "Programas na inicialização",
            Status = ini > 6 ? Status.Alerta : Status.Ok,
            Detalhe = $"{ini} programa(s) abrindo junto com o Windows",
            Botao = ini > 6 ? "Gerenciar" : null,
            Acao = () => { Sistema.Abrir("ms-settings:startupapps"); return null; }
        });

        return l;
    }
}
