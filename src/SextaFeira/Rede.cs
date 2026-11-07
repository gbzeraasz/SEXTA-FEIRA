using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace SextaFeira;

/// <summary>
/// Diagnóstico de internet: descobre ONDE o pico de ping nasce (casa, operadora ou rota até o jogo),
/// testa bufferbloat e aplica os ajustes de placa de rede que têm efeito real.
/// </summary>
internal static class Rede
{
    // ===================== Alvos =====================

    public class Alvo
    {
        public string Nome;
        public string Explicacao;
        public string Host;      // IP ou nome
        public int PortaTcp;     // 0 = ICMP (ping normal); >0 = mede o tempo de conexão TCP
        public readonly List<float> Amostras = new(); // ms; -1 = perdido
        private readonly object trava = new();

        public void Add(float v) { lock (trava) Amostras.Add(v); }
        public float[] Copia() { lock (trava) return Amostras.ToArray(); }

        public Estatistica Calcular() => Estatistica.De(Copia());
    }

    public class Estatistica
    {
        public int Total, Perdidos, Picos;
        public double Media, Minimo, Maximo, P95, Jitter;
        public double Perda => Total == 0 ? 0 : Perdidos * 100.0 / Total;

        public static Estatistica De(float[] amostras)
        {
            var e = new Estatistica { Total = amostras.Length };
            var ok = amostras.Where(a => a >= 0).Select(a => (double)a).ToList();
            e.Perdidos = amostras.Count(a => a < 0);
            if (ok.Count == 0) return e;
            e.Media = ok.Average();
            e.Minimo = ok.Min();
            e.Maximo = ok.Max();
            var ord = ok.OrderBy(x => x).ToList();
            e.P95 = ord[Math.Min(ord.Count - 1, (int)(ord.Count * 0.95))];
            e.Jitter = ok.Count > 1 ? ok.Zip(ok.Skip(1), (a, b) => Math.Abs(a - b)).Average() : 0;
            // pico = 30 ms acima do melhor tempo (o que tu sente como "subiu do nada")
            e.Picos = ok.Count(x => x > e.Minimo + 30);
            return e;
        }

        public string Texto() => Total == 0 ? "sem dados" :
            Perdidos == Total ? "sem resposta" :
            $"média {Media:0} ms  •  melhor {Minimo:0}  •  pior {Maximo:0}  •  jitter {Jitter:0.0}  •  picos {Picos}  •  perda {Perda:0}%";
    }

    public static IPAddress Gateway()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                         .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1))
            {
                var gw = nic.GetIPProperties().GatewayAddresses
                    .Select(g => g.Address)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
                if (gw != null) return gw;
            }
        }
        catch { }
        return null;
    }

    /// <summary>IP de um relay da Valve em São Paulo (onde os servidores do CS2 da América do Sul ficam).</summary>
    public static async Task<string> RelayValveSaoPaulo()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            string json = await http.GetStringAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=730");
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("pops", out var pops) && pops.TryGetProperty("gru", out var gru) &&
                gru.TryGetProperty("relays", out var relays))
            {
                foreach (var r in relays.EnumerateArray())
                    if (r.TryGetProperty("ipv4", out var ip)) return ip.GetString();
            }
        }
        catch { }
        return null;
    }

    public static async Task<List<Alvo>> MontarAlvos()
    {
        var l = new List<Alvo>();
        var gw = Gateway();
        if (gw != null)
            l.Add(new Alvo { Nome = "Teu roteador", Host = gw.ToString(), Explicacao = "dentro de casa: cabo, porta, roteador" });
        l.Add(new Alvo { Nome = "Internet", Host = "1.1.1.1", Explicacao = "até a operadora e a internet" });
        string relay = await RelayValveSaoPaulo();
        if (relay != null)
            l.Add(new Alvo { Nome = "CS2 (São Paulo)", Host = relay, Explicacao = "servidor da Valve em SP" });
        l.Add(new Alvo { Nome = "Valorant/Fortnite (SP)", Host = "dynamodb.sa-east-1.amazonaws.com", PortaTcp = 443, Explicacao = "Amazon em SP, onde esses jogos rodam" });
        return l;
    }

    internal static async Task<float> Medir(Alvo a, Ping ping)
    {
        try
        {
            if (a.PortaTcp == 0)
            {
                var r = await ping.SendPingAsync(a.Host, 1000);
                return r.Status == IPStatus.Success ? r.RoundtripTime : -1;
            }
            var sw = Stopwatch.StartNew();
            using var tcp = new TcpClient();
            var conectar = tcp.ConnectAsync(a.Host, a.PortaTcp);
            if (await Task.WhenAny(conectar, Task.Delay(1500)) != conectar || !tcp.Connected) return -1;
            return (float)sw.Elapsed.TotalMilliseconds;
        }
        catch { return -1; }
    }

    /// <summary>Pinga todos os alvos ao mesmo tempo, a cada meio segundo, pelo tempo pedido.</summary>
    public static async Task Diagnosticar(List<Alvo> alvos, int segundos, Action<int> progresso, CancellationToken ct)
    {
        // resolve o nome antes, pra não somar o DNS no tempo
        foreach (var a in alvos.Where(a => a.PortaTcp > 0))
            try { await Dns.GetHostAddressesAsync(a.Host, ct); } catch { }

        var fim = DateTime.Now.AddSeconds(segundos);
        var tarefas = alvos.Select(async a =>
        {
            using var ping = new Ping();
            while (DateTime.Now < fim && !ct.IsCancellationRequested)
            {
                var inicio = DateTime.Now;
                a.Add(await Medir(a, ping));
                var espera = 500 - (int)(DateTime.Now - inicio).TotalMilliseconds;
                if (espera > 0) await Task.Delay(espera, ct).ContinueWith(_ => { });
            }
        }).ToList();

        while (tarefas.Any(t => !t.IsCompleted))
        {
            int pct = (int)Math.Clamp(100 - (fim - DateTime.Now).TotalSeconds * 100 / segundos, 0, 100);
            progresso?.Invoke(pct);
            await Task.WhenAny(Task.WhenAll(tarefas), Task.Delay(500));
        }
    }

    /// <summary>Diz onde o problema nasce, em português de gente.</summary>
    public static (Status status, string titulo, string detalhe) Veredito(List<Alvo> alvos)
    {
        var casa = alvos.FirstOrDefault(a => a.Nome == "Teu roteador")?.Calcular();
        var net = alvos.First(a => a.Nome == "Internet").Calcular();
        var servidores = alvos.Where(a => a.Nome != "Teu roteador" && a.Nome != "Internet").Select(a => (a.Nome, e: a.Calcular())).ToList();

        if (casa != null && (casa.Perdidos > 0 || casa.Maximo > 15 || casa.Picos > 0))
            return (Status.Alerta, "⚠ O problema começa DENTRO de casa",
                $"Até o teu roteador o ping devia ficar em 1-2 ms sem variar, mas chegou a {casa.Maximo:0} ms" +
                (casa.Perdidos > 0 ? $" e perdeu {casa.Perda:0}% dos pacotes" : "") + ". " +
                "No cabo, isso costuma ser: cabo de rede velho ou dobrado, mau contato na porta do roteador ou da placa, ou o roteador travando. " +
                "Testa outro cabo e outra porta do roteador, e reinicia o roteador.");

        if (net.Perdidos > 0 && net.Perda >= 2 || net.Picos >= 3 || net.Jitter >= 8)
            return (Status.Alerta, "⚠ O problema está na internet (depois do teu roteador)",
                $"Dentro de casa está limpo, mas até a internet teve {net.Picos} pico(s), jitter de {net.Jitter:0.0} ms e {net.Perda:0}% de perda. " +
                "As causas mais comuns: alguém ou algo usando a internet ao mesmo tempo (roda o teste de bufferbloat ao lado) ou instabilidade da operadora. " +
                "Se acontecer sempre nos mesmos horários, vale abrir chamado na operadora com esse resultado.");

        var pior = servidores.Where(s => s.e.Total > 0).OrderByDescending(s => s.e.Picos + s.e.Perdidos).FirstOrDefault();
        if (pior.e != null && (pior.e.Picos >= 3 || pior.e.Perda >= 2))
            return (Status.Info, "A rota até o servidor do jogo está oscilando",
                $"Tua casa e a internet estão limpas, mas o caminho até {pior.Nome} teve {pior.e.Picos} pico(s). " +
                "Isso é rota da operadora até o servidor: não dá pra resolver no PC. Se for sempre assim, a operadora pode ajustar a rota.");

        double melhorJogo = servidores.Where(s => s.e.Total > s.e.Perdidos).Select(s => s.e.Minimo).DefaultIfEmpty(-1).Min();
        return (Status.Ok, "✅ Conexão limpa durante o teste",
            "Nenhum pico dentro de casa, na internet ou até os servidores." +
            (melhorJogo > 0 ? $" Teu melhor ping até São Paulo foi {melhorJogo:0} ms." : "") +
            " Se o ping sobe só às vezes, quase sempre é algo usando a internet na hora: roda o teste de bufferbloat ao lado.");
    }

    // ===================== Bufferbloat =====================

    public class Bufferbloat
    {
        public double Parado, Baixando, Enviando;      // ping médio (ms)
        public double MbpsDown, MbpsUp;
        public double AumentoDown => Math.Max(0, Baixando - Parado);
        public double AumentoUp => Math.Max(0, Enviando - Parado);

        public static string Nota(double aumento) =>
            aumento < 5 ? "A+" : aumento < 30 ? "A" : aumento < 60 ? "B" : aumento < 200 ? "C" : aumento < 400 ? "D" : "F";

        public string NotaGeral => Nota(Math.Max(AumentoDown, AumentoUp));
    }

    private const string Cf = "https://speed.cloudflare.com";

    private static async Task<double> PingMedio(int segundos, CancellationToken ct)
    {
        var l = new List<double>();
        using var p = new Ping();
        var fim = DateTime.Now.AddSeconds(segundos);
        while (DateTime.Now < fim && !ct.IsCancellationRequested)
        {
            try
            {
                var r = await p.SendPingAsync("1.1.1.1", 1000);
                if (r.Status == IPStatus.Success) l.Add(r.RoundtripTime);
            }
            catch { }
            await Task.Delay(250, ct).ContinueWith(_ => { });
        }
        return l.Count > 0 ? l.Average() : -1;
    }

    public static async Task<Bufferbloat> TestarBufferbloat(Action<string> progresso)
    {
        var b = new Bufferbloat();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        progresso?.Invoke("Medindo o ping com a internet parada...");
        b.Parado = await PingMedio(8, CancellationToken.None);

        // ---------- download ----------
        progresso?.Invoke("Medindo o ping enquanto baixa...");
        long baixado = 0;
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
        {
            var sw = Stopwatch.StartNew();
            var carga = Enumerable.Range(0, 4).Select(async _ =>
            {
                var buf = new byte[81920];
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        using var resp = await http.GetAsync($"{Cf}/__down?bytes=25000000", HttpCompletionOption.ResponseHeadersRead, cts.Token);
                        using var s = await resp.Content.ReadAsStreamAsync(cts.Token);
                        int n;
                        while ((n = await s.ReadAsync(buf, cts.Token)) > 0) Interlocked.Add(ref baixado, n);
                    }
                    catch { if (cts.IsCancellationRequested) break; await Task.Delay(200); }
                }
            }).ToList();
            await Task.Delay(2000); // deixa a carga subir antes de medir
            b.Baixando = await PingMedio(9, cts.Token);
            await Task.WhenAll(carga);
            b.MbpsDown = baixado * 8 / 1_000_000.0 / sw.Elapsed.TotalSeconds;
        }

        // ---------- upload ----------
        progresso?.Invoke("Medindo o ping enquanto envia...");
        long enviado = 0;
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
        {
            var sw = Stopwatch.StartNew();
            var dados = new byte[5_000_000];
            var carga = Enumerable.Range(0, 3).Select(async _ =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        using var conteudo = new ByteArrayContent(dados);
                        using var resp = await http.PostAsync($"{Cf}/__up", conteudo, cts.Token);
                        Interlocked.Add(ref enviado, dados.Length);
                    }
                    catch { if (cts.IsCancellationRequested) break; await Task.Delay(200); }
                }
            }).ToList();
            await Task.Delay(2000);
            b.Enviando = await PingMedio(9, cts.Token);
            await Task.WhenAll(carga);
            b.MbpsUp = enviado * 8 / 1_000_000.0 / sw.Elapsed.TotalSeconds;
        }
        return b;
    }

    // ===================== Placa de rede =====================

    public class Placa
    {
        public string Nome, Descricao, Velocidade, Tipo;
    }

    public static List<Placa> PlacasAtivas()
    {
        var l = new List<Placa>();
        string s = Sistema.Rodar("powershell",
            "-NoProfile -Command \"Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | ForEach-Object { $_.Name + '|' + $_.InterfaceDescription + '|' + $_.LinkSpeed + '|' + $_.MediaType }\"", 20000);
        foreach (var linha in s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = linha.Split('|');
            if (p.Length >= 4) l.Add(new Placa { Nome = p[0], Descricao = p[1], Velocidade = p[2], Tipo = p[3] });
        }
        return l;
    }

    public static bool EhCabo(Placa p) => p.Tipo.Contains("802.3", StringComparison.OrdinalIgnoreCase);

    // ----- Ethernet verde / EEE (economia de energia do cabo, conhecida por causar atraso em placas Realtek) -----

    private const string FiltroEee =
        "$_.RegistryKeyword -match 'EEE|GreenEthernet|PowerSavingMode|GigaLite|EnablePME$' -or $_.DisplayName -match 'Energy|Green|Verde|Eficiente|Economia de energia'";

    /// <summary>Quantas opções de economia de energia do cabo estão ligadas.</summary>
    public static int EeeLigado()
    {
        string s = Sistema.Rodar("powershell",
            "-NoProfile -Command \"@(Get-NetAdapter -Physical | Where-Object MediaType -match '802.3' | Get-NetAdapterAdvancedProperty -ErrorAction SilentlyContinue | " +
            $"Where-Object {{ ({FiltroEee}) -and $_.RegistryKeyword -notmatch 'PME' -and $_.RegistryValue -ne '0' -and $_.ValidRegistryValues -contains '0' }}).Count\"", 20000);
        return int.TryParse(s.Trim(), out int n) ? n : -1;
    }

    /// <summary>Desliga e guarda o comando de desfazer. Pode derrubar o cabo por 2-3 segundos.</summary>
    public static int DesligarEee()
    {
        string listar = Sistema.Rodar("powershell",
            "-NoProfile -Command \"Get-NetAdapter -Physical | Where-Object MediaType -match '802.3' | Get-NetAdapterAdvancedProperty -ErrorAction SilentlyContinue | " +
            $"Where-Object {{ ({FiltroEee}) -and $_.RegistryKeyword -notmatch 'PME' -and $_.RegistryValue -ne '0' -and $_.ValidRegistryValues -contains '0' }} | " +
            "ForEach-Object { $_.Name + '|' + $_.RegistryKeyword + '|' + ($_.RegistryValue -join ',') }\"", 20000);
        int n = 0;
        foreach (var linha in listar.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = linha.Split('|');
            if (p.Length < 3) continue;
            string nome = p[0].Replace("'", "''"), chave = p[1].Replace("'", "''"), valor = p[2];
            Backup.GuardarComando($"eee:{p[0]}:{p[1]}", "powershell",
                $"-NoProfile -Command \"Set-NetAdapterAdvancedProperty -Name '{nome}' -RegistryKeyword '{chave}' -RegistryValue '{valor}'\"");
            Sistema.Rodar("powershell",
                $"-NoProfile -Command \"Set-NetAdapterAdvancedProperty -Name '{nome}' -RegistryKeyword '{chave}' -RegistryValue '0'\"", 30000);
            n++;
        }
        return n;
    }

    // ----- Windows Update enviando pra outros PCs (gasta teu upload) -----

    private const string ChaveDO = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";

    public static bool AtualizacaoP2PLigada() => Sistema.LerRegInt(ChaveDO, "DODownloadMode") != 0;

    public static void DesligarAtualizacaoP2P() => Sistema.GravarDword(ChaveDO, "DODownloadMode", 0);
}
