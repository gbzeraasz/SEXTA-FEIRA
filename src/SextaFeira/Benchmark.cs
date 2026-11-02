using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace SextaFeira;

/// <summary>
/// Benchmark: FPS real dentro do jogo (PresentMon, da Intel, via ETW: não mexe no jogo,
/// seguro com anti-cheat), teste de processador e de disco, e histórico pra comparar.
/// </summary>
internal static class Benchmark
{
    // ===================== FPS real =====================

    public class Fps
    {
        public DateTime Data { get; set; }
        public string Jogo { get; set; }
        public string Etiqueta { get; set; }   // "Medição", "Antes", "Depois"
        public double Media { get; set; }
        public double Low1 { get; set; }       // média do 1% mais lento
        public double Low01 { get; set; }      // média do 0,1% mais lento
        public double FrameTime { get; set; }  // ms
        public int Quadros { get; set; }
        public bool Competitivo { get; set; }
        public float TempGpu { get; set; } = -1;
    }

    public static string PresentMon => Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon.exe");
    public static bool TemPresentMon => File.Exists(PresentMon);

    /// <summary>Jogos abertos agora (nome bonito, nome do processo).</summary>
    public static List<(string Nome, string Processo)> JogosAbertos(List<Biblioteca.JogoInstalado> biblioteca)
    {
        var achados = new List<(string, string)>();

        // 1) jogos com perfil competitivo (pelo nome do processo)
        foreach (var c in ConfigsCompetitivas.Todos)
            foreach (var proc in c.Processos)
                if (Process.GetProcessesByName(proc).Length > 0 && !achados.Any(a => a.Item2 == proc))
                    achados.Add((c.Nome, proc));

        // 2) qualquer outro processo rodando de dentro da pasta de um jogo da biblioteca
        var pastas = biblioteca.Where(b => !string.IsNullOrEmpty(b.Pasta) && b.Config == null).ToList();
        if (pastas.Count > 0)
        {
            foreach (var p in Process.GetProcesses())
            {
                string caminho = null;
                try { caminho = p.MainModule?.FileName; } catch { }
                if (caminho == null) continue;
                var jogo = pastas.FirstOrDefault(b => caminho.StartsWith(b.Pasta, StringComparison.OrdinalIgnoreCase));
                if (jogo != null && !achados.Any(a => a.Item2 == p.ProcessName) && p.MainWindowHandle != IntPtr.Zero)
                    achados.Add((jogo.Nome, p.ProcessName));
            }
        }
        return achados;
    }

    public static (Fps resultado, string erro) MedirFps(string nomeJogo, string processo, int segundos, string etiqueta)
    {
        if (!TemPresentMon) return (null, "O medidor de FPS não veio nessa versão. Atualiza o app.");
        if (Process.GetProcessesByName(processo).Length == 0) return (null, "O jogo não está aberto.");

        string csv = Path.Combine(Path.GetTempPath(), $"sexta-fps-{Guid.NewGuid():N}.csv");
        try
        {
            var psi = new ProcessStartInfo(PresentMon,
                $"--process_name \"{processo}.exe\" --output_file \"{csv}\" --timed {segundos} --terminate_after_timed " +
                "--no_console_stats --v1_metrics --session_name SextaFeira --stop_existing_session")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            float tempMax = -1;
            using (var p = Process.Start(psi)!)
            {
                p.OutputDataReceived += (_, _) => { };
                p.ErrorDataReceived += (_, _) => { };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                // enquanto mede, anota a temperatura máxima da GPU
                var fim = DateTime.Now.AddSeconds(segundos + 20);
                while (!p.HasExited && DateTime.Now < fim)
                {
                    var g = Sensores.LerGpu();
                    if (g.Disponivel) tempMax = Math.Max(tempMax, g.Temp);
                    p.WaitForExit(2000);
                }
                if (!p.HasExited) try { p.Kill(); } catch { }
            }

            if (!File.Exists(csv)) return (null, "O medidor não gerou dados. O jogo precisa estar rodando (não no menu minimizado).");

            var tempos = LerTempos(csv);
            if (tempos.Count < 30) return (null, "Poucos quadros medidos. Deixa o jogo em primeiro plano durante a medição.");

            var ordenado = tempos.OrderByDescending(t => t).ToList(); // mais lentos primeiro
            double MediaPior(double fracao)
            {
                int n = Math.Max(1, (int)Math.Ceiling(ordenado.Count * fracao));
                return 1000.0 / ordenado.Take(n).Average();
            }

            var r = new Fps
            {
                Data = DateTime.Now,
                Jogo = nomeJogo,
                Etiqueta = etiqueta,
                FrameTime = tempos.Average(),
                Media = 1000.0 / tempos.Average(),
                Low1 = MediaPior(0.01),
                Low01 = MediaPior(0.001),
                Quadros = tempos.Count,
                Competitivo = ModoCompetitivo.Ativo,
                TempGpu = tempMax
            };
            Salvar(r);
            return (r, null);
        }
        catch (Exception ex)
        {
            Atualizador.Log("ERRO ao medir FPS: " + ex);
            return (null, "Erro ao medir: " + ex.Message);
        }
        finally
        {
            try { File.Delete(csv); } catch { }
        }
    }

    private static List<double> LerTempos(string csv)
    {
        var tempos = new List<double>();
        using var leitor = new StreamReader(csv);
        string cab = leitor.ReadLine();
        if (cab == null) return tempos;
        var colunas = cab.Split(',');
        int idx = Array.FindIndex(colunas, c => c.Trim() == "MsBetweenPresents");
        if (idx < 0) idx = Array.FindIndex(colunas, c => c.Trim() is "FrameTime" or "MsBetweenAppStart");
        if (idx < 0) return tempos;

        string linha;
        while ((linha = leitor.ReadLine()) != null)
        {
            var p = linha.Split(',');
            if (p.Length > idx && double.TryParse(p[idx], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) && ms > 0 && ms < 1000)
                tempos.Add(ms);
        }
        // descarta o primeiro segundo (aquecimento)
        double soma = 0; int pular = 0;
        while (pular < tempos.Count && soma < 1000) soma += tempos[pular++];
        return tempos.Skip(pular).ToList();
    }

    // ---------- histórico de FPS ----------
    private static string ArqFps => Path.Combine(Sistema.PastaDados, "fps.json");

    public static List<Fps> HistoricoFps()
    {
        try { if (File.Exists(ArqFps)) return JsonSerializer.Deserialize<List<Fps>>(File.ReadAllText(ArqFps)) ?? new(); } catch { }
        return new();
    }

    private static void Salvar(Fps r)
    {
        var l = HistoricoFps();
        l.Add(r);
        if (l.Count > 200) l = l.Skip(l.Count - 200).ToList();
        try { File.WriteAllText(ArqFps, JsonSerializer.Serialize(l)); } catch { }
    }

    // ===================== Teste do PC =====================

    public class TestePC
    {
        public DateTime Data { get; set; }
        public double CpuUmNucleo { get; set; }
        public double CpuTodos { get; set; }
        public double DiscoLeitura { get; set; }  // MB/s
        public double DiscoEscrita { get; set; }  // MB/s
        public float TempGpu { get; set; } = -1;
    }

    private static string ArqTeste => Path.Combine(Sistema.PastaDados, "teste-pc.json");

    public static List<TestePC> HistoricoTestes()
    {
        try { if (File.Exists(ArqTeste)) return JsonSerializer.Deserialize<List<TestePC>>(File.ReadAllText(ArqTeste)) ?? new(); } catch { }
        return new();
    }

    public static TestePC RodarTeste(Action<int, string> progresso)
    {
        var t = new TestePC { Data = DateTime.Now };

        progresso?.Invoke(5, "Processador: 1 núcleo...");
        t.CpuUmNucleo = PontosCpu(1, 4000);

        progresso?.Invoke(35, "Processador: todos os núcleos...");
        t.CpuTodos = PontosCpu(Environment.ProcessorCount, 4000);

        progresso?.Invoke(65, "Disco C: escrevendo e lendo...");
        (t.DiscoEscrita, t.DiscoLeitura) = TesteDisco(Path.GetPathRoot(Environment.SystemDirectory));

        var g = Sensores.LerGpu();
        t.TempGpu = g.Temp;

        var l = HistoricoTestes();
        l.Add(t);
        if (l.Count > 100) l = l.Skip(l.Count - 100).ToList();
        try { File.WriteAllText(ArqTeste, JsonSerializer.Serialize(l)); } catch { }

        progresso?.Invoke(100, "Pronto!");
        return t;
    }

    /// <summary>Conta operações matemáticas por um tempo fixo. Mais pontos = mais rápido.</summary>
    private static double PontosCpu(int threads, int ms)
    {
        long total = 0;
        var fim = Stopwatch.StartNew();
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
        {
            long n = 0;
            double x = 1.0001;
            uint h = 2166136261;
            while (fim.ElapsedMilliseconds < ms)
            {
                for (int i = 0; i < 20000; i++)
                {
                    x = x * 1.0000001 + Math.Sqrt(i + 1.0);
                    h = (h ^ (uint)i) * 16777619;
                }
                if (x > 1e300) x = 1.0001;
                n += 20000;
            }
            if (h == 42) Console.Write(""); // impede o compilador de jogar o laço fora
            Interlocked.Add(ref total, n);
        });
        return Math.Round(total / 1_000_000.0 / (ms / 1000.0), 1); // milhões de operações por segundo
    }

    private static (double escrita, double leitura) TesteDisco(string raiz)
    {
        string arq = Path.Combine(raiz, "ProgramData", "SEXTA-FEIRA-teste-disco.tmp");
        const int mb = 256;
        var buffer = new byte[1024 * 1024];
        new Random(7).NextBytes(buffer);
        try
        {
            var sw = Stopwatch.StartNew();
            using (var fs = new FileStream(arq, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.WriteThrough))
            {
                for (int i = 0; i < mb; i++) fs.Write(buffer, 0, buffer.Length);
                fs.Flush(true);
            }
            double escrita = mb / sw.Elapsed.TotalSeconds;

            // tira o arquivo do cache da RAM pra ler do disco de verdade
            Otimizador.LiberarRamEmEspera();

            sw.Restart();
            using (var fs = new FileStream(arq, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
                while (fs.Read(buffer, 0, buffer.Length) > 0) { }
            double leitura = mb / sw.Elapsed.TotalSeconds;
            return (Math.Round(escrita), Math.Round(leitura));
        }
        catch { return (0, 0); }
        finally { try { File.Delete(arq); } catch { } }
    }

    /// <summary>Compara o teste novo com a média dos anteriores. Queda grande pode indicar defeito/superaquecimento.</summary>
    public static string Comparar(TestePC novo)
    {
        var anteriores = HistoricoTestes().Where(t => t.Data < novo.Data.AddMinutes(-1)).ToList();
        if (anteriores.Count == 0) return "Primeiro teste salvo! Os próximos vão ser comparados com ele.";

        string Dif(double agora, double antes, string nome)
        {
            if (antes <= 0 || agora <= 0) return null;
            double pct = (agora - antes) / antes * 100;
            string seta = pct >= 0 ? "▲" : "▼";
            string alerta = pct <= -15 ? "  ⚠ caiu bastante" : "";
            return $"{nome}: {seta} {Math.Abs(pct):0}%{alerta}";
        }

        var media = new TestePC
        {
            CpuUmNucleo = anteriores.Average(a => a.CpuUmNucleo),
            CpuTodos = anteriores.Average(a => a.CpuTodos),
            DiscoLeitura = anteriores.Average(a => a.DiscoLeitura),
            DiscoEscrita = anteriores.Average(a => a.DiscoEscrita)
        };
        var partes = new[]
        {
            Dif(novo.CpuUmNucleo, media.CpuUmNucleo, "CPU 1 núcleo"),
            Dif(novo.CpuTodos, media.CpuTodos, "CPU todos"),
            Dif(novo.DiscoLeitura, media.DiscoLeitura, "Leitura"),
            Dif(novo.DiscoEscrita, media.DiscoEscrita, "Escrita"),
        }.Where(x => x != null);

        string txt = $"Comparado com a média dos {anteriores.Count} teste(s) anteriores:  " + string.Join("  •  ", partes);
        if (novo.CpuTodos > 0 && media.CpuTodos > 0 && novo.CpuTodos < media.CpuTodos * 0.85)
            txt += "\n⚠ O processador rendeu bem menos que o normal. Fecha os programas e testa de novo; " +
                   "se continuar, pode ser superaquecimento (cooler sujo ou pasta térmica seca).";
        return txt;
    }
}
