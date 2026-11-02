using System.Text.Json;

namespace SextaFeira;

/// <summary>
/// Saúde do PC ao longo do tempo: guarda a temperatura da GPU por dia (parada e em carga)
/// e compara a última semana com o mês anterior pra achar sinais de problema.
/// Também lê a saúde dos discos (o que o próprio SSD/HD informa).
/// </summary>
internal static class Saude
{
    public class Dia
    {
        public string Data { get; set; }
        public double SomaCarga { get; set; }
        public int NCarga { get; set; }
        public double SomaRepouso { get; set; }
        public int NRepouso { get; set; }
        public double Max { get; set; }

        public double? MediaCarga => NCarga >= 20 ? SomaCarga / NCarga : null;
        public double? MediaRepouso => NRepouso >= 20 ? SomaRepouso / NRepouso : null;
    }

    public class Resultado
    {
        public Status Status;
        public string Titulo;
        public List<string> Detalhes = new();
    }

    private static readonly object trava = new();
    private static string Arquivo => Path.Combine(Sistema.PastaDados, "saude.json");
    private static List<Dia> dias;
    private static DateTime ultimoSalvo = DateTime.MinValue;

    private static List<Dia> Dias
    {
        get
        {
            if (dias != null) return dias;
            try
            {
                if (File.Exists(Arquivo))
                    dias = JsonSerializer.Deserialize<List<Dia>>(File.ReadAllText(Arquivo));
            }
            catch { }
            return dias ??= new List<Dia>();
        }
    }

    /// <summary>Registra uma leitura. Chamado a cada poucos segundos enquanto o app está aberto.</summary>
    public static void Registrar(Sensores.Gpu g)
    {
        if (!g.Disponivel || g.Uso < 0) return;
        lock (trava)
        {
            string hoje = DateTime.Now.ToString("yyyy-MM-dd");
            var d = Dias.FirstOrDefault(x => x.Data == hoje);
            if (d == null)
            {
                d = new Dia { Data = hoje };
                Dias.Add(d);
                // guarda só os últimos 90 dias
                if (Dias.Count > 90) Dias.RemoveRange(0, Dias.Count - 90);
            }

            if (g.Uso >= 70) { d.SomaCarga += g.Temp; d.NCarga++; }
            else if (g.Uso <= 15) { d.SomaRepouso += g.Temp; d.NRepouso++; }
            d.Max = Math.Max(d.Max, g.Temp);

            if ((DateTime.Now - ultimoSalvo).TotalSeconds >= 60) Salvar();
        }
    }

    public static void Salvar()
    {
        lock (trava)
        {
            try
            {
                File.WriteAllText(Arquivo, JsonSerializer.Serialize(Dias));
                ultimoSalvo = DateTime.Now;
            }
            catch { }
        }
    }

    /// <summary>Compara a última semana com os 30 dias antes dela.</summary>
    public static Resultado Analisar()
    {
        lock (trava)
        {
            var r = new Resultado();
            var hoje = DateTime.Today;
            DateTime Data(Dia d) => DateTime.TryParse(d.Data, out var x) ? x : DateTime.MinValue;

            var semana = Dias.Where(d => Data(d) > hoje.AddDays(-7)).ToList();
            var base30 = Dias.Where(d => Data(d) <= hoje.AddDays(-7) && Data(d) > hoje.AddDays(-37)).ToList();

            int diasComDado = Dias.Count(d => d.MediaCarga != null || d.MediaRepouso != null);
            double maxSemana = semana.Count > 0 ? semana.Max(d => d.Max) : 0;

            if (maxSemana >= 84)
            {
                r.Status = Status.Alerta;
                r.Detalhes.Add($"A GPU chegou a {maxSemana:0} °C nesta semana. Acima de 83 °C ela perde desempenho pra se proteger.");
            }

            double? Media(List<Dia> l, Func<Dia, double?> f)
            {
                var v = l.Select(f).Where(x => x != null).Select(x => x.Value).ToList();
                return v.Count >= 2 ? v.Average() : null;
            }

            var cargaAgora = Media(semana, d => d.MediaCarga);
            var cargaAntes = Media(base30, d => d.MediaCarga);
            var repAgora = Media(semana, d => d.MediaRepouso);
            var repAntes = Media(base30, d => d.MediaRepouso);

            bool comparou = false;
            void Comparar(double? agora, double? antes, string quando)
            {
                if (agora == null || antes == null) return;
                comparou = true;
                double dif = agora.Value - antes.Value;
                if (dif >= 8)
                {
                    r.Status = Status.Alerta;
                    r.Detalhes.Add($"{quando}, a GPU está {dif:0} °C mais quente que no mês passado ({antes:0} → {agora:0} °C). " +
                                   "Sinal clássico de poeira, ventoinha falhando ou pasta térmica seca.");
                }
                else if (dif >= 5)
                {
                    if (r.Status != Status.Alerta) r.Status = Status.Info;
                    r.Detalhes.Add($"{quando}, a GPU está {dif:0} °C mais quente que no mês passado. Fica de olho; pode ser só o dia mais quente.");
                }
                else
                {
                    r.Detalhes.Add($"{quando}: média de {agora:0} °C, igual ao mês passado ({antes:0} °C). ✓");
                }
            }
            Comparar(cargaAgora, cargaAntes, "Jogando");
            Comparar(repAgora, repAntes, "Parada");

            if (r.Status == Status.Alerta) r.Titulo = "⚠ Atenção com a placa de vídeo";
            else if (r.Status == Status.Info) r.Titulo = "Temperatura subindo um pouco";
            else if (comparou) { r.Status = Status.Ok; r.Titulo = "✅ Placa de vídeo estável"; }
            else
            {
                r.Status = Status.Info;
                r.Titulo = "Coletando dados";
                r.Detalhes.Insert(0, $"{diasComDado} dia(s) de histórico. A comparação fica pronta depois de umas 5 semanas de uso, " +
                                     "mas os alertas de temperatura alta já funcionam. Ele aprende com o app aberto.");
            }
            return r;
        }
    }

    // ---------- Discos ----------

    public class Disco
    {
        public string Nome;
        public string Tipo;
        public string Saude;     // Saudável / Atenção / Com defeito / ?
        public Status Status;
        public int? Temperatura;
        public int? Desgaste;    // % usado da vida útil (SSD)
        public long? Erros;
        public long? HorasLigado;
    }

    public static List<Disco> LerDiscos()
    {
        const string esc = @"root\Microsoft\Windows\Storage";
        var lista = new List<Disco>();
        var contadores = Sistema.Wmi("SELECT DeviceId, Temperature, Wear, ReadErrorsUncorrected, PowerOnHours FROM MSFT_StorageReliabilityCounter", esc);

        foreach (var d in Sistema.Wmi("SELECT DeviceId, FriendlyName, MediaType, HealthStatus FROM MSFT_PhysicalDisk", esc))
        {
            var disco = new Disco
            {
                Nome = Sistema.WmiTexto(d, "FriendlyName"),
                Tipo = Sistema.WmiTexto(d, "MediaType") switch { "3" => "HD", "4" => "SSD", _ => "Disco" }
            };
            switch (Sistema.WmiTexto(d, "HealthStatus"))
            {
                case "0": disco.Saude = "Saudável"; disco.Status = Status.Ok; break;
                case "1": disco.Saude = "Atenção"; disco.Status = Status.Alerta; break;
                case "2": disco.Saude = "Com defeito"; disco.Status = Status.Alerta; break;
                default: disco.Saude = "?"; disco.Status = Status.Info; break;
            }

            string id = Sistema.WmiTexto(d, "DeviceId");
            var c = contadores.FirstOrDefault(x => Sistema.WmiTexto(x, "DeviceId") == id);
            if (c != null)
            {
                disco.Temperatura = Int(Sistema.WmiTexto(c, "Temperature"));
                disco.Desgaste = Int(Sistema.WmiTexto(c, "Wear"));
                disco.Erros = Long(Sistema.WmiTexto(c, "ReadErrorsUncorrected"));
                disco.HorasLigado = Long(Sistema.WmiTexto(c, "PowerOnHours"));
                if (disco.Temperatura == 0) disco.Temperatura = null;
                if (disco.Desgaste >= 90 || disco.Erros > 0) disco.Status = Status.Alerta;
            }
            lista.Add(disco);
        }
        return lista;
    }

    public static string Descrever(Disco d)
    {
        var partes = new List<string> { d.Saude };
        if (d.Temperatura != null) partes.Add($"{d.Temperatura} °C");
        if (d.Desgaste != null && d.Tipo == "SSD") partes.Add($"{d.Desgaste}% da vida útil usada");
        if (d.HorasLigado != null && d.HorasLigado > 0) partes.Add($"{d.HorasLigado:N0} h ligado");
        if (d.Erros > 0) partes.Add($"{d.Erros} erro(s) de leitura: faz backup!");
        return string.Join("  •  ", partes);
    }

    private static int? Int(string s) => int.TryParse(s, out int v) ? v : null;
    private static long? Long(string s) => long.TryParse(s, out long v) ? v : null;
}
