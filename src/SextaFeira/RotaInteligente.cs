using System.Diagnostics;
using System.Net.NetworkInformation;

namespace SextaFeira;

/// <summary>
/// Rota inteligente (a ideia do ExitLag/NoPing, usando a rede gratuita da Cloudflare WARP).
/// - Testa 3 caminhos até o servidor de cada jogo em São Paulo: direto pela operadora,
///   Cloudflare (WireGuard) e Cloudflare (MASQUE).
/// - Escolhe pela ESTABILIDADE (perda, picos e variação pesam mais que o ping médio).
/// - Liga a rota escolhida quando o jogo/launcher abre e desliga quando fecha.
///   Nunca troca de rota no meio da partida (trocar derruba a conexão).
/// - Nunca muda a região: é sempre o servidor de SP.
/// Não mexe no jogo: só no caminho da internet, por fora. Sem risco de ban.
/// </summary>
internal static class RotaInteligente
{
    // ===================== Jogos =====================

    public class JogoRota
    {
        public string Id, Nome;
        public string[] Gatilhos;   // processos que abrem a "sessão" (launcher entra pra rota ligar ANTES de conectar no jogo)
        public string[] Jogo;       // processos do jogo em si
        public Func<Task<Rede.Alvo>> Alvo;
    }

    public static readonly List<JogoRota> Jogos = new()
    {
        new JogoRota
        {
            Id = "cs2", Nome = "CS2",
            Gatilhos = new[] { "cs2" }, Jogo = new[] { "cs2" },
            Alvo = async () =>
            {
                string relay = await Rede.RelayValveSaoPaulo();
                return relay == null ? null : new Rede.Alvo { Nome = "CS2", Host = relay };
            }
        },
        new JogoRota
        {
            Id = "valorant", Nome = "Valorant",
            Gatilhos = new[] { "RiotClientServices", "VALORANT-Win64-Shipping" }, Jogo = new[] { "VALORANT-Win64-Shipping" },
            Alvo = () => Task.FromResult(new Rede.Alvo { Nome = "Valorant", Host = "dynamodb.sa-east-1.amazonaws.com", PortaTcp = 443 })
        },
        new JogoRota
        {
            Id = "fortnite", Nome = "Fortnite",
            Gatilhos = new[] { "EpicGamesLauncher", "FortniteClient-Win64-Shipping" }, Jogo = new[] { "FortniteClient-Win64-Shipping" },
            Alvo = () => Task.FromResult(new Rede.Alvo { Nome = "Fortnite", Host = "ec2.sa-east-1.amazonaws.com", PortaTcp = 443 })
        },
    };

    public static readonly Dictionary<string, string> NomeRota = new()
    {
        ["direta"] = "Direta (operadora)",
        ["warp-wireguard"] = "Cloudflare · WireGuard",
        ["warp-masque"] = "Cloudflare · MASQUE",
    };

    // ===================== Cloudflare WARP =====================

    private static string Cli
    {
        get
        {
            foreach (var pasta in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"C:\Program Files" })
            {
                string c = Path.Combine(pasta, "Cloudflare", "Cloudflare WARP", "warp-cli.exe");
                if (File.Exists(c)) return c;
            }
            return null;
        }
    }

    public static bool WarpInstalado => Cli != null;

    private static string Warp(string args, int timeout = 20000) =>
        Cli == null ? "" : Sistema.Rodar(Cli, "--accept-tos " + args, timeout);

    public static bool WarpConectado()
    {
        string s = Warp("status");
        return s.Contains("Connected", StringComparison.OrdinalIgnoreCase) && !s.Contains("Disconnected", StringComparison.OrdinalIgnoreCase)
               && !s.Contains("Connecting", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Instala o WARP oficial da Cloudflare pelo winget (vem no Windows 11).</summary>
    public static bool InstalarWarp()
    {
        Sistema.Rodar("winget", "install --id Cloudflare.Warp -e --silent --accept-package-agreements --accept-source-agreements", 10 * 60 * 1000);
        for (int i = 0; i < 20 && !WarpInstalado; i++) Thread.Sleep(1000);
        if (WarpInstalado) Preparar();
        return WarpInstalado;
    }

    /// <summary>Registra o aparelho (só na 1ª vez) e deixa no modo túnel completo.</summary>
    public static void Preparar()
    {
        if (!WarpInstalado) return;
        string r = Warp("registration show");
        if (r.Contains("Missing", StringComparison.OrdinalIgnoreCase) || r.Contains("not registered", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(r))
        {
            Warp("registration new", 30000);
            Warp("register", 30000); // versões antigas
        }
        Warp("mode warp");
    }

    private static bool Conectar(string protocolo)
    {
        Preparar();
        Warp("tunnel protocol set " + protocolo);
        Warp("connect");
        for (int i = 0; i < 30; i++)
        {
            if (WarpConectado()) return true;
            Thread.Sleep(500);
        }
        return false;
    }

    private static void Desconectar()
    {
        Warp("disconnect");
        for (int i = 0; i < 20 && WarpConectado(); i++) Thread.Sleep(300);
    }

    // ===================== Medição =====================

    public class Medicao
    {
        public string Rota;
        public Rede.Estatistica E;
        /// <summary>Quanto menor, melhor. Estabilidade pesa mais que ping médio.</summary>
        public double Nota => E == null || E.Total == 0 || E.Perdidos == E.Total ? double.MaxValue
            : E.Media + 2.0 * E.Jitter + 0.5 * Math.Max(0, E.P95 - E.Media) + 25 * E.Perda + 3 * E.Picos;
        public string Texto() => E == null || E.Total == 0 || E.Perdidos == E.Total ? "sem resposta"
            : $"{E.Media:0} ms  •  variação {E.Jitter:0.0}  •  picos {E.Picos}  •  perda {E.Perda:0.#}%";
    }

    public class Resultado
    {
        public JogoRota Jogo;
        public List<Medicao> Medicoes = new();
        public string Escolhida = "direta";
        public string Motivo = "";
    }

    private static async Task MedirTodos(List<(JogoRota jogo, Rede.Alvo alvo)> alvos, int segundos)
    {
        foreach (var (_, a) in alvos.Where(x => x.alvo.PortaTcp > 0))
            try { await System.Net.Dns.GetHostAddressesAsync(a.Host); } catch { }
        var fim = DateTime.Now.AddSeconds(segundos);
        await Task.WhenAll(alvos.Select(async x =>
        {
            using var ping = new Ping();
            while (DateTime.Now < fim)
            {
                var inicio = DateTime.Now;
                x.alvo.Add(await Rede.Medir(x.alvo, ping));
                int espera = 300 - (int)(DateTime.Now - inicio).TotalMilliseconds;
                if (espera > 0) await Task.Delay(espera);
            }
        }));
    }

    /// <summary>Testa as rotas pra todos os jogos e guarda a melhor de cada um (~1min30).</summary>
    public static async Task<List<Resultado>> Testar(Action<string, int> progresso)
    {
        const int Segundos = 25;
        var resultados = new List<Resultado>();
        var jogos = new List<(JogoRota jogo, Func<Task<Rede.Alvo>> criar)>();
        foreach (var j in Jogos) { jogos.Add((j, j.Alvo)); resultados.Add(new Resultado { Jogo = j }); }

        bool estavaConectado = WarpInstalado && await Task.Run(() => WarpConectado());
        var rotas = new List<string> { "direta" };
        if (WarpInstalado) { rotas.Add("warp-wireguard"); rotas.Add("warp-masque"); }

        try
        {
            for (int i = 0; i < rotas.Count; i++)
            {
                string rota = rotas[i];
                int pctBase = i * 100 / rotas.Count;
                progresso?.Invoke($"Preparando a rota {NomeRota[rota]}...", pctBase);

                bool ok = await Task.Run(() =>
                {
                    if (rota == "direta") { if (WarpConectado()) Desconectar(); return true; }
                    return Conectar(rota == "warp-wireguard" ? "WireGuard" : "MASQUE");
                });
                if (!ok)
                {
                    foreach (var r in resultados) r.Medicoes.Add(new Medicao { Rota = rota });
                    continue;
                }
                await Task.Delay(2500); // deixa a rota assentar

                var alvos = new List<(JogoRota, Rede.Alvo)>();
                foreach (var (j, criar) in jogos)
                {
                    var a = await criar();
                    if (a != null) alvos.Add((j, a));
                }

                var medir = MedirTodos(alvos, Segundos);
                var t0 = DateTime.Now;
                while (!medir.IsCompleted)
                {
                    int pct = pctBase + (int)(Math.Min(1, (DateTime.Now - t0).TotalSeconds / Segundos) * 100 / rotas.Count);
                    progresso?.Invoke($"Testando {NomeRota[rota]}...", Math.Min(99, pct));
                    await Task.WhenAny(medir, Task.Delay(500));
                }

                foreach (var r in resultados)
                {
                    var a = alvos.FirstOrDefault(x => x.Item1 == r.Jogo).Item2;
                    r.Medicoes.Add(new Medicao { Rota = rota, E = a?.Calcular() });
                }
            }
        }
        finally
        {
            await Task.Run(() =>
            {
                if (WarpInstalado)
                {
                    if (estavaConectado) { if (!WarpConectado()) Conectar("MASQUE"); }
                    else if (WarpConectado()) Desconectar();
                }
            });
        }

        // ----- decide -----
        var cfg = Configuracao.Atual;
        foreach (var r in resultados)
        {
            var direta = r.Medicoes.FirstOrDefault(m => m.Rota == "direta");
            var melhor = r.Medicoes.Where(m => m.Nota < double.MaxValue).OrderBy(m => m.Nota).FirstOrDefault();
            if (melhor == null) { r.Escolhida = "direta"; r.Motivo = "Não deu pra medir: fica a rota direta."; }
            else if (melhor.Rota == "direta" || direta == null || direta.Nota == double.MaxValue)
            {
                r.Escolhida = melhor.Rota == "direta" || direta?.Nota < double.MaxValue ? "direta" : melhor.Rota;
                r.Motivo = r.Escolhida == "direta" ? "A rota da tua operadora já é a mais estável." : "A rota direta não respondeu.";
            }
            else
            {
                // Só troca se a Cloudflare for CLARAMENTE melhor (não troca por 1 ms)
                bool perdaMelhorOuIgual = melhor.E.Perda <= direta.E.Perda;
                bool bemMelhor = melhor.Nota <= direta.Nota * 0.85 || direta.E.Perda >= 1 && melhor.E.Perda < direta.E.Perda;
                if (perdaMelhorOuIgual && bemMelhor)
                {
                    r.Escolhida = melhor.Rota;
                    r.Motivo = $"{NomeRota[melhor.Rota]} foi mais estável que a operadora " +
                               $"(variação {melhor.E.Jitter:0.0} x {direta.E.Jitter:0.0}, picos {melhor.E.Picos} x {direta.E.Picos}, perda {melhor.E.Perda:0.#}% x {direta.E.Perda:0.#}%).";
                }
                else
                {
                    r.Escolhida = "direta";
                    r.Motivo = "A Cloudflare não foi claramente melhor: fica a rota direta.";
                }
            }
            if (cfg.SempreDireta.Contains(r.Jogo.Id)) { r.Escolhida = "direta"; r.Motivo = "Marcado pra usar sempre a rota direta."; }
            cfg.Rotas[r.Jogo.Id] = r.Escolhida;
        }
        cfg.Salvar();
        Atualizador.Log("Rota inteligente: " + string.Join(" | ", resultados.Select(r =>
            $"{r.Jogo.Nome} → {r.Escolhida} ({string.Join("; ", r.Medicoes.Select(m => m.Rota + ": " + m.Texto()))})")));
        return resultados;
    }

    // ===================== Sessão de jogo =====================

    private static string sessaoJogo;       // id do jogo da sessão atual
    private static bool ligamosWarp;
    private static bool protegido;
    private static bool fechamosOneDrive;
    private static bool jogoChegouAbrir;
    private static DateTime sessaoInicio;
    // launchers que ficaram abertos sem o jogo: passam a valer só quando o JOGO abrir
    private static readonly HashSet<string> launcherParado = new();
    public static string Status { get; private set; } = "";

    private static readonly string[] ServicosRede = { "wuauserv", "BITS", "DoSvc", "UsoSvc" };

    private static bool Aberto(string[] nomes)
    {
        foreach (var n in nomes)
        {
            var ps = Process.GetProcessesByName(n);
            bool achou = ps.Length > 0;
            foreach (var p in ps) p.Dispose();
            if (achou) return true;
        }
        return false;
    }

    /// <summary>Chamado a cada 3 segundos (fora da thread da tela). Devolve uma frase quando algo muda.</summary>
    public static string Vigiar()
    {
        try
        {
            var cfg = Configuracao.Atual;
            if (sessaoJogo == null)
            {
                foreach (var id in launcherParado.ToList())
                    if (!Aberto(Jogos.First(x => x.Id == id).Gatilhos)) launcherParado.Remove(id);
                var j = Jogos.FirstOrDefault(x => Aberto(launcherParado.Contains(x.Id) ? x.Jogo : x.Gatilhos));
                if (j == null) return null;
                sessaoJogo = j.Id;
                sessaoInicio = DateTime.Now;
                jogoChegouAbrir = false;
                var frases = new List<string>();

                if (cfg.ProtegerRedeNoJogo)
                {
                    foreach (var s in ServicosRede) Sistema.Rodar("sc", "stop " + s, 10000);
                    var od = Process.GetProcessesByName("OneDrive");
                    if (od.Length > 0) { foreach (var p in od) try { p.Kill(); } catch { } fechamosOneDrive = true; }
                    protegido = true;
                    frases.Add("internet protegida (Update e OneDrive pausados)");
                }

                string rota = cfg.Rotas.GetValueOrDefault(j.Id, "direta");
                if (cfg.RotaAutomatica && rota != "direta" && !cfg.SempreDireta.Contains(j.Id) && WarpInstalado && !WarpConectado())
                {
                    if (Conectar(rota == "warp-wireguard" ? "WireGuard" : "MASQUE"))
                    {
                        ligamosWarp = true;
                        frases.Add("rota " + NomeRota[rota]);
                    }
                    else frases.Add("rota Cloudflare não conectou, ficou a direta");
                }
                else frases.Add("rota direta");

                Status = $"{j.Nome}: " + string.Join(" · ", frases);
                Atualizador.Log("Sessão de jogo: " + Status);
                return $"{j.Nome} detectado: " + string.Join(", ", frases) + ".";
            }
            else
            {
                var j = Jogos.First(x => x.Id == sessaoJogo);
                if (Aberto(j.Jogo)) jogoChegouAbrir = true;
                // launcher aberto há 10 min sem o jogo: desliga e só volta quando o jogo abrir
                if (!jogoChegouAbrir && (DateTime.Now - sessaoInicio).TotalMinutes > 10)
                {
                    launcherParado.Add(j.Id);
                    Encerrar();
                    return null;
                }
                // fecha a sessão quando o jogo fecha (ou quando o launcher fecha sem o jogo ter aberto)
                bool acabou = jogoChegouAbrir ? !Aberto(j.Jogo) : !Aberto(j.Gatilhos);
                if (!acabou) return null;
                Encerrar();
                return $"{j.Nome} fechado: rota e internet de volta ao normal.";
            }
        }
        catch (Exception ex)
        {
            Atualizador.Log("ERRO rota inteligente: " + ex.Message);
            return null;
        }
    }

    /// <summary>Volta tudo ao normal (jogo fechou ou o app está fechando).</summary>
    public static void Encerrar()
    {
        if (ligamosWarp) { try { Desconectar(); } catch { } ligamosWarp = false; }
        if (protegido)
        {
            // Windows Update e BITS voltam sozinhos quando o Windows precisar; o OneDrive a gente reabre
            if (fechamosOneDrive)
            {
                foreach (var od in new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive\OneDrive.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft OneDrive\OneDrive.exe")
                })
                    // abre pelo Explorer pra não rodar como administrador (o OneDrive recusa)
                    if (File.Exists(od)) { try { Process.Start("explorer.exe", "\"" + od + "\""); } catch { } break; }
                fechamosOneDrive = false;
            }
            protegido = false;
        }
        if (sessaoJogo != null) Atualizador.Log("Sessão de jogo encerrada");
        sessaoJogo = null;
        Status = "";
    }

    public static bool EmSessao => sessaoJogo != null;
}
