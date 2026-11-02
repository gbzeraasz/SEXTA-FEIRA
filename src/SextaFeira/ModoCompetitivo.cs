using System.Diagnostics;
using System.Net.NetworkInformation;

namespace SextaFeira;

/// <summary>
/// Modo Competitivo: liga antes da partida.
/// - Dá mais prioridade pro jogo e menos pro que roda no fundo
/// - Pausa o que usa internet escondido (Windows Update, Otimização de Entrega, OneDrive)
/// - Faz a otimização completa
/// Não mexe na memória do jogo nem injeta nada: seguro com anti-cheat.
/// </summary>
internal static class ModoCompetitivo
{
    public static bool Ativo { get; private set; }
    public static DateTime LigadoEm { get; private set; }

    // Rebaixados enquanto o modo está ligado. Discord fica de fora pra não picotar a voz.
    private static readonly string[] Fundo =
    {
        "chrome", "msedge", "firefox", "opera", "opera_gx", "brave",
        "Spotify", "OneDrive", "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper",
        "RiotClientServices", "EADesktop", "Battle.net", "upc", "SearchHost", "PhoneExperienceHost"
    };

    // Serviços que baixam coisa em segundo plano. Voltam sozinhos quando o Windows precisar.
    private static readonly string[] Servicos = { "wuauserv", "BITS", "DoSvc", "UsoSvc" };

    private static readonly Dictionary<int, ProcessPriorityClass> alterados = new();
    private static bool fechouOneDrive;
    public static string JogoAtual { get; private set; }

    // ----- RAM contínua (estilo ISLC) -----
    // Limpa a lista em espera só quando precisa: pouca RAM livre E bastante coisa em espera.
    private const double LimiteLivre = 1024d * 1024 * 1024;      // menos de 1 GB livre
    private const double LimiteEmEspera = 1024d * 1024 * 1024;   // mais de 1 GB em espera
    private static DateTime ultimaLimpeza = DateTime.MinValue;
    public static int LimpezasRam { get; private set; }
    public static double RamLiberadaTotal { get; private set; }

    public static List<string> Ligar()
    {
        var feito = new List<string>();
        if (Ativo) return feito;

        var r = Otimizador.Executar();
        feito.Add("Otimização completa: " + r.Frase());

        int parados = 0;
        foreach (var s in Servicos)
        {
            string saida = Sistema.Rodar("sc", "stop " + s, 10000);
            if (saida.Contains("STOP_PENDING") || saida.Contains("STOPPED")) parados++;
        }
        feito.Add("Downloads do Windows Update pausados durante a partida");

        var od = Process.GetProcessesByName("OneDrive");
        if (od.Length > 0)
        {
            foreach (var p in od) try { p.Kill(); } catch { }
            fechouOneDrive = true;
            feito.Add("Sincronização do OneDrive pausada");
        }

        Ativo = true;
        LigadoEm = DateTime.Now;
        LimpezasRam = 0;
        RamLiberadaTotal = 0;
        Atualizar();
        feito.Add("Prioridade: jogo em primeiro, programas de fundo por último");
        Atualizador.Log("Modo Competitivo ligado");
        return feito;
    }

    /// <summary>Chamado a cada poucos segundos: dá prioridade pro jogo que abrir.</summary>
    public static void Atualizar()
    {
        if (!Ativo) return;
        JogoAtual = null;

        foreach (var nome in ConfigsCompetitivas.ProcessosDeJogos())
            foreach (var p in Process.GetProcessesByName(nome))
            {
                JogoAtual = ConfigsCompetitivas.Todos.First(c => c.Processos.Contains(nome)).Nome;
                Prioridade(p, ProcessPriorityClass.AboveNormal);
            }

        foreach (var nome in Fundo)
            foreach (var p in Process.GetProcessesByName(nome))
                Prioridade(p, ProcessPriorityClass.BelowNormal);

        if (Configuracao.Atual.RamContinua) VerificarRam();
    }

    private static void VerificarRam()
    {
        try
        {
            if ((DateTime.Now - ultimaLimpeza).TotalSeconds < 30) return;
            double livre = Otimizador.RamLivre();
            double emEspera = Otimizador.RamEmEspera();
            if (livre >= LimiteLivre || emEspera < LimiteEmEspera) return;

            Otimizador.LiberarRamEmEspera();
            ultimaLimpeza = DateTime.Now;
            double liberado = Math.Max(0, emEspera - Otimizador.RamEmEspera());
            LimpezasRam++;
            RamLiberadaTotal += liberado;
            Atualizador.Log($"Competitivo: RAM em espera limpa ({Sistema.Formatar(liberado)}), livre estava {Sistema.Formatar(livre)}");
        }
        catch { }
    }

    private static void Prioridade(Process p, ProcessPriorityClass nova)
    {
        try
        {
            if (alterados.ContainsKey(p.Id) || p.PriorityClass == nova) return;
            var antes = p.PriorityClass;
            p.PriorityClass = nova;
            alterados[p.Id] = antes;
        }
        catch { /* anti-cheat pode bloquear: sem problema, só pula */ }
    }

    public static void Desligar()
    {
        if (!Ativo) return;
        foreach (var (id, prioridade) in alterados)
        {
            try { Process.GetProcessById(id).PriorityClass = prioridade; } catch { }
        }
        alterados.Clear();

        if (fechouOneDrive)
        {
            string od = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive\OneDrive.exe");
            if (!File.Exists(od)) od = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft OneDrive\OneDrive.exe");
            if (File.Exists(od))
                try { Process.Start(new ProcessStartInfo(od, "/background") { UseShellExecute = true }); } catch { }
            fechouOneDrive = false;
        }

        Ativo = false;
        JogoAtual = null;
        Atualizador.Log("Modo Competitivo desligado");
    }

    // ---------------- Teste de conexão ----------------

    public class Conexao
    {
        public double Media;
        public double Jitter;    // variação entre um ping e outro: o que causa "teleporte"
        public double Perda;     // %
        public bool Ok;

        public string Nota()
        {
            if (!Ok) return "Sem resposta: confere tua internet";
            if (Perda >= 5) return "Ruim: perdendo pacotes";
            if (Jitter >= 15 || Media >= 120) return "Instável";
            if (Jitter >= 6 || Media >= 60) return "Boa";
            return "Excelente";
        }

        public Color Cor() => !Ok || Perda >= 5 ? Tema.Vermelho
                             : Jitter >= 15 || Media >= 120 ? Tema.Amarelo
                             : Tema.Verde;
    }

    /// <summary>Mede a estabilidade da internet (não do servidor do jogo).</summary>
    public static Conexao TestarConexao(int vezes = 20)
    {
        var tempos = new List<double>();
        int perdidos = 0;
        using var ping = new Ping();
        string[] alvos = { "1.1.1.1", "8.8.8.8" };
        for (int i = 0; i < vezes; i++)
        {
            try
            {
                var resp = ping.Send(alvos[i % 2], 1000);
                if (resp.Status == IPStatus.Success) tempos.Add(resp.RoundtripTime);
                else perdidos++;
            }
            catch { perdidos++; }
            Thread.Sleep(100);
        }

        var c = new Conexao { Perda = perdidos * 100.0 / vezes, Ok = tempos.Count > 0 };
        if (tempos.Count > 0)
        {
            c.Media = tempos.Average();
            c.Jitter = tempos.Count > 1 ? tempos.Zip(tempos.Skip(1), (a, b) => Math.Abs(a - b)).Average() : 0;
        }
        return c;
    }
}
