using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SextaFeira;

/// <summary>
/// A otimização de 1 clique. Só faz o que tem teste mostrando que funciona.
/// Nunca mexe no processo dos jogos (seguro com Vanguard, VAC, FACEIT e EAC).
/// </summary>
internal static class Otimizador
{
    public class Resultado
    {
        public double DiscoLiberado;
        public int Recentes;
        public double RamLivreAntes;
        public double RamLivreDepois;
        public double RamLiberada;
        public List<string> Fechados = new();
        public List<string> Feito = new();

        /// <summary>Uma frase só, pra notificação do botão da barra.</summary>
        public string Frase()
        {
            double ram = RamLiberada;
            string f = $"Liberei {Sistema.Formatar(DiscoLiberado)} de disco e {Sistema.Formatar(ram)} de RAM";
            if (Fechados.Count > 0) f += $" e fechei {Fechados.Count} programa(s)";
            return f + ".";
        }

        public string Resumo()
        {
            var s = $"Disco: {Sistema.Formatar(DiscoLiberado)} limpos\n" +
                    $"RAM livre: {Sistema.Formatar(RamLivreAntes)} → {Sistema.Formatar(RamLivreDepois)}";
            if (Fechados.Count > 0) s += "\nFechados: " + string.Join(", ", Fechados);
            return s;
        }
    }

    /// <summary>Programas fechados antes de jogar. Steam, Epic, Riot e Discord ficam de fora: os jogos dependem deles.</summary>
    public static readonly string[] ProgramasDeFundo =
    {
        "EADesktop", "Battle.net", "upc", "UbisoftConnect", "GalaxyClient",
        "ms-teams", "Teams", "Overwolf", "Medal"
    };

    public const string PlanoAltoDesempenho = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public static Resultado Executar(Action<int, string> progresso = null)
    {
        var r = new Resultado();
        void P(int pct, string txt) { try { progresso?.Invoke(pct, txt); } catch { } }
        var cfg = Configuracao.Atual;

        r.RamLivreAntes = RamLivre();

        // 1) Limpezas marcadas na aba "Limpeza 1 clique" (as manuais nunca entram aqui)
        var limpezas = Limpeza.Itens.Where(i => !i.EhAcao && !i.SoManual && cfg.Marcado(i.Id)).ToList();
        for (int i = 0; i < limpezas.Count; i++)
        {
            var item = limpezas[i];
            try
            {
                if (!item.Disponivel()) continue;
                P(5 + i * 55 / Math.Max(1, limpezas.Count), item.Nome + "...");
                long liberado = item.Executar();
                r.DiscoLiberado += liberado;
                r.Feito.Add(item.Nome);
                Atualizador.Log($"Limpeza '{item.Id}': {Sistema.Formatar(liberado)}");
            }
            catch (Exception ex) { Atualizador.Log($"ERRO na limpeza '{item.Id}': {ex.Message}"); }
        }

        // 2) Ações de desempenho
        if (cfg.Marcado("fechar"))
        {
            P(62, "Fechando programas de fundo...");
            r.Fechados = FecharProgramas();
        }
        if (cfg.Marcado("ajustesjogo"))
        {
            P(70, "Ajustando o Windows pra jogos...");
            AjustesDeJogo();
            r.Feito.Add("Ajustes de jogo do Windows");
        }
        if (cfg.Marcado("energia"))
        {
            P(78, "Ligando o plano Alto Desempenho...");
            if (AtivarAltoDesempenho()) r.Feito.Add("Plano Alto Desempenho");
        }

        if (cfg.Marcado("mouse")) { try { Ajustes.DesligarAceleracaoMouse(); r.Feito.Add("Aceleração do mouse desligada"); } catch { } }
        if (cfg.Marcado("usb")) { try { P(82, "Ajustando USB..."); Ajustes.DesligarUsbSeletiva(); r.Feito.Add("Suspensão de USB desligada"); } catch { } }
        if (cfg.Marcado("throttling")) { try { Ajustes.DesligarThrottling(); r.Feito.Add("Power Throttling desligado"); } catch { } }

        r.RamLivreDepois = RamLivre();
        if (cfg.Marcado("ram"))
        {
            P(86, "Liberando a RAM em espera...");
            double emEsperaAntes = RamEmEspera();
            double livreAntesDaLimpeza = RamLivre();
            uint status = LiberarRamEmEspera();
            Thread.Sleep(800);
            r.RamLivreDepois = RamLivre();
            double emEsperaDepois = RamEmEspera();
            r.RamLiberada = Math.Max(Math.Max(0, emEsperaAntes - emEsperaDepois), Math.Max(0, r.RamLivreDepois - livreAntesDaLimpeza));
            r.Feito.Add("RAM em espera liberada");
            Atualizador.Log($"RAM: status=0x{status:X8} emEspera {Sistema.Formatar(emEsperaAntes)} -> {Sistema.Formatar(emEsperaDepois)}, " +
                            $"livre {Sistema.Formatar(livreAntesDaLimpeza)} -> {Sistema.Formatar(r.RamLivreDepois)}");
        }

        P(100, "Pronto!");
        Historico.Registrar(r);
        return r;
    }

    // ---------------- Etapas ----------------

    internal static List<string> FecharProgramas()
    {
        var fechados = new List<string>();
        foreach (var nome in Configuracao.Atual.ProgramasParaFechar)
        {
            var procs = Process.GetProcessesByName(nome);
            if (procs.Length == 0) continue;
            foreach (var p in procs)
                try { p.Kill(true); } catch { }
            fechados.Add(nome);
        }
        return fechados;
    }

    public static void AjustesDeJogo()
    {
        // Modo de Jogo ligado
        Sistema.GravarDword(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
        // Gravação em segundo plano desligada
        Sistema.GravarDword(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0);
        Sistema.GravarDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
        // Otimizações para jogos em janela (DX10/DX11 em janela/sem bordas → flip model, menos latência)
        AtivarJogosEmJanela();
    }

    public static bool JogosEmJanelaAtivo()
    {
        string v = Sistema.LerReg(@"HKCU\Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings") as string;
        return v != null && v.Contains("SwapEffectUpgradeEnable=1");
    }

    public static void AtivarJogosEmJanela()
    {
        const string caminho = @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences";
        const string nome = "DirectXUserGlobalSettings";
        string atual = Sistema.LerReg(caminho, nome) as string ?? "";

        // Formato: "Chave1=valor;Chave2=valor;" — mantém o que já existir (ex: Auto HDR)
        var pares = atual.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        pares["SwapEffectUpgradeEnable"] = "1";
        string novo = string.Concat(pares.Select(p => $"{p.Key}={p.Value};"));
        if (novo != atual)
            Sistema.GravarReg(caminho, nome, novo, Microsoft.Win32.RegistryValueKind.String);
    }

    public static string PlanoAtivo()
    {
        var m = Regex.Match(Sistema.Rodar("powercfg", "/getactivescheme"), "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    public static bool AtivarAltoDesempenho()
    {
        string atual = PlanoAtivo();
        if (atual == PlanoAltoDesempenho) return true;
        if (atual != null) Backup.GuardarPlano(atual);

        Sistema.Rodar("powercfg", "/setactive " + PlanoAltoDesempenho);
        if (PlanoAtivo() == PlanoAltoDesempenho) return true;

        // Em alguns PCs o plano vem escondido: cria uma cópia dele
        var m = Regex.Match(Sistema.Rodar("powercfg", "-duplicatescheme " + PlanoAltoDesempenho), "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        if (!m.Success) return false;
        Sistema.Rodar("powercfg", "/setactive " + m.Value);
        return true;
    }

    // ---------------- RAM ----------------

    public static double RamLivre()
    {
        var o = Sistema.Wmi("SELECT FreePhysicalMemory FROM Win32_OperatingSystem").FirstOrDefault();
        return o == null ? 0 : double.Parse(Sistema.WmiTexto(o, "FreePhysicalMemory")) * 1024;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TOKEN_PRIVILEGES { public int Count; public long Luid; public int Attr; }

    [DllImport("ntdll.dll")] private static extern uint NtSetSystemInformation(int cls, ref int info, int len);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr h, uint acc, out IntPtr tok);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool LookupPrivilegeValue(string sys, string name, out long luid);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(IntPtr tok, bool dis, ref TOKEN_PRIVILEGES ns, int len, IntPtr prev, IntPtr ret);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

    /// <summary>RAM "em espera" (cache do Windows), somando as 3 prioridades.</summary>
    public static double RamEmEspera()
    {
        var o = Sistema.Wmi("SELECT StandbyCacheCoreBytes, StandbyCacheNormalPriorityBytes, StandbyCacheReserveBytes FROM Win32_PerfRawData_PerfOS_Memory").FirstOrDefault();
        if (o == null) return 0;
        double total = 0;
        foreach (var campo in new[] { "StandbyCacheCoreBytes", "StandbyCacheNormalPriorityBytes", "StandbyCacheReserveBytes" })
            if (double.TryParse(Sistema.WmiTexto(o, campo), out double v)) total += v;
        return total;
    }

    /// <summary>Esvazia a lista "em espera" da RAM. Retorna o código do Windows (0 = deu certo).</summary>
    internal static uint LiberarRamEmEspera()
    {
        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x28, out IntPtr tok)) return 0xFFFFFFFF;
            var tp = new TOKEN_PRIVILEGES { Count = 1, Attr = 2 };
            LookupPrivilegeValue(null, "SeProfileSingleProcessPrivilege", out tp.Luid);
            AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            CloseHandle(tok);

            int cmd = 3; // grava a lista modificada
            NtSetSystemInformation(80, ref cmd, 4);
            cmd = 4;     // esvazia a lista em espera
            return NtSetSystemInformation(80, ref cmd, 4);
        }
        catch { return 0xFFFFFFFE; }
    }
}
