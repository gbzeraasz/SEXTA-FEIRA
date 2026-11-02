using System.Globalization;

namespace SextaFeira;

/// <summary>
/// Sensores da placa de vídeo, lidos pelo nvidia-smi (vem junto com o driver NVIDIA).
/// Sem driver extra: zero risco com anti-cheat.
/// </summary>
internal static class Sensores
{
    public class Gpu
    {
        public float Temp = -1;      // °C
        public float Uso = -1;       // %
        public float Ventoinha = -1; // %
        public float Clock = -1;     // MHz
        public float Watts = -1;
        public bool Disponivel => Temp >= 0;
    }

    private static readonly string nvidiaSmi = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");

    public static bool TemNvidia => File.Exists(nvidiaSmi);

    public static Gpu LerGpu()
    {
        var g = new Gpu();
        if (!TemNvidia) return g;

        string saida = Sistema.Rodar(nvidiaSmi,
            "--query-gpu=temperature.gpu,utilization.gpu,fan.speed,clocks.gr,power.draw --format=csv,noheader,nounits", 4000);
        var p = saida.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Split(',');
        if (p == null || p.Length < 5) return g;

        g.Temp = Num(p[0]);
        g.Uso = Num(p[1]);
        g.Ventoinha = Num(p[2]);
        g.Clock = Num(p[3]);
        g.Watts = Num(p[4]);
        return g;
    }

    private static float Num(string s)
    {
        s = s.Trim();
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : -1;
    }

    // ---------- Explicação das faixas ----------

    public enum Faixa { Desconhecida, Fria, Normal, Quente, Perigo }

    /// <summary>Faixas para placas de vídeo de desktop (a GTX 10 corta clock ~83 °C).</summary>
    public static Faixa Classificar(float temp)
    {
        if (temp < 0) return Faixa.Desconhecida;
        if (temp < 50) return Faixa.Fria;
        if (temp < 76) return Faixa.Normal;
        if (temp < 84) return Faixa.Quente;
        return Faixa.Perigo;
    }

    public static Color Cor(Faixa f) => f switch
    {
        Faixa.Fria => Tema.Destaque,
        Faixa.Normal => Tema.Verde,
        Faixa.Quente => Tema.Amarelo,
        Faixa.Perigo => Tema.Vermelho,
        _ => Tema.Apagado
    };

    public static string Explicar(Gpu g)
    {
        if (!g.Disponivel)
            return "Não consegui ler a temperatura. Ela aparece com placas NVIDIA com o driver instalado.";

        bool emJogo = g.Uso >= 60;
        return Classificar(g.Temp) switch
        {
            Faixa.Fria => "Fria. Normal parada ou em uso leve.",
            Faixa.Normal => emJogo ? "Ótima pra jogo. Até uns 75 °C em jogo é tranquilo."
                                   : "Normal. Parada, o ideal é ficar abaixo de 50 °C.",
            Faixa.Quente => emJogo ? "Quente, mas ainda aceitável em jogo. Acima de 83 °C ela começa a perder desempenho."
                                   : "Quente pra quem não tá jogando. Confere se tem algo pesando ou se a ventoinha tá girando.",
            Faixa.Perigo => "Muito quente! Ela já tá cortando desempenho pra se proteger. Limpa a poeira e confere a ventilação do gabinete.",
            _ => ""
        };
    }
}
