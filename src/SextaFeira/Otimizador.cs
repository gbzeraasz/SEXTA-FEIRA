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
        public List<string> Fechados = new();
        public List<string> Feito = new();

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

        r.RamLivreAntes = RamLivre();

        P(5, "Limpando arquivos temporários...");
        r.DiscoLiberado += LimparTemporarios();
        r.Feito.Add("Temporários limpos");

        P(25, "Esvaziando a lixeira...");
        r.DiscoLiberado += EsvaziarLixeira();
        r.Feito.Add("Lixeira esvaziada");

        P(35, "Limpando lista de recentes...");
        r.Recentes = LimparRecentes();
        r.Feito.Add("Recentes limpos");

        P(45, "Fechando programas de fundo...");
        r.Fechados = FecharProgramas();

        P(55, "Ajustando o Windows pra jogos...");
        AjustesDeJogo();
        r.Feito.Add("Modo de Jogo ligado, gravação em segundo plano desligada");
        r.Feito.Add("Otimizações para jogos em janela ligadas");

        P(70, "Ligando o plano Alto Desempenho...");
        if (AtivarAltoDesempenho()) r.Feito.Add("Plano Alto Desempenho ativo");

        P(85, "Liberando a RAM em espera...");
        LiberarRamEmEspera();
        Thread.Sleep(600);
        r.RamLivreDepois = RamLivre();
        r.Feito.Add("RAM em espera liberada");

        P(100, "Pronto!");
        Historico.Registrar(r);
        return r;
    }

    // ---------------- Etapas ----------------

    private static double TamanhoPasta(string pasta)
    {
        double total = 0;
        try
        {
            foreach (var f in new DirectoryInfo(pasta).EnumerateFiles("*", new EnumerationOptions
                     { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }))
            {
                try { total += f.Length; } catch { }
            }
        }
        catch { }
        return total;
    }

    private static double LimparTemporarios()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // Cache de shader (D3DSCache, NVIDIA DXCache) NÃO entra: apagar ele causa travadas.
        string[] pastas =
        {
            Path.GetTempPath(),
            Path.Combine(windir, "Temp"),
            Path.Combine(local, @"Microsoft\Windows\INetCache"),
            Path.Combine(local, "CrashDumps"),
            Path.Combine(progData, @"Microsoft\Windows\WER\ReportQueue"),
            Path.Combine(progData, @"Microsoft\Windows\WER\ReportArchive")
        };

        double liberado = 0;
        foreach (var pasta in pastas)
        {
            if (!Directory.Exists(pasta)) continue;
            double antes = TamanhoPasta(pasta);
            var dir = new DirectoryInfo(pasta);
            foreach (var f in SafeEnum(() => dir.EnumerateFiles()))
                try { f.Attributes = FileAttributes.Normal; f.Delete(); } catch { }
            foreach (var d in SafeEnum(() => dir.EnumerateDirectories()))
                try { d.Delete(true); } catch { }
            liberado += Math.Max(0, antes - TamanhoPasta(pasta));
        }
        return liberado;
    }

    private static IEnumerable<T> SafeEnum<T>(Func<IEnumerable<T>> f)
    {
        try { return f().ToList(); } catch { return Enumerable.Empty<T>(); }
    }

    // Em 64 bits o Windows usa o alinhamento padrão (24 bytes), por isso sem Pack aqui.
    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint flags);

    private static double EsvaziarLixeira()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        SHQueryRecycleBin(null, ref info);
        // 1 = sem confirmação, 2 = sem barra de progresso, 4 = sem som
        SHEmptyRecycleBin(IntPtr.Zero, null, 1 | 2 | 4);
        return info.i64Size;
    }

    private static int LimparRecentes()
    {
        // Só os atalhos da pasta Recent. Os fixados do Acesso Rápido ficam intactos.
        string pasta = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        int n = 0;
        foreach (var f in SafeEnum(() => Directory.EnumerateFiles(pasta)))
            try { File.Delete(f); n++; } catch { }
        return n;
    }

    private static List<string> FecharProgramas()
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

    /// <summary>Esvazia a lista "em espera" da RAM (o que o Windows guardou em cache).</summary>
    private static void LiberarRamEmEspera()
    {
        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x28, out IntPtr tok)) return;
            var tp = new TOKEN_PRIVILEGES { Count = 1, Attr = 2 };
            LookupPrivilegeValue(null, "SeProfileSingleProcessPrivilege", out tp.Luid);
            AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            CloseHandle(tok);

            int cmd = 3; // grava a lista modificada
            NtSetSystemInformation(80, ref cmd, 4);
            cmd = 4;     // esvazia a lista em espera
            NtSetSystemInformation(80, ref cmd, 4);
        }
        catch { }
    }
}
