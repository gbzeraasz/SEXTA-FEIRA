using System.Diagnostics;
using System.Text.Json.Nodes;

namespace SextaFeira;

/// <summary>
/// Caçador de "ladrões de FPS": programas que gravam a tela ou usam a placa de vídeo junto com o jogo.
/// Numa GTX 1050 Ti (o jogo já usa a placa no máximo), tudo que disputa a GPU tira FPS e causa travadinha.
/// </summary>
internal static class LadroesFps
{
    /// <summary>Gravadores de clipe que ficam gravando o tempo todo em segundo plano.</summary>
    public static readonly (string processo, string nome)[] Gravadores =
    {
        ("Medal", "Medal (grava clipes o tempo todo)"),
        ("Overwolf", "Overwolf"),
        ("Outplayed", "Outplayed (grava clipes o tempo todo)"),
        ("obs64", "OBS Studio"),
        ("Streamlabs OBS", "Streamlabs"),
        ("wallpaper64", "Wallpaper Engine"),
        ("wallpaper32", "Wallpaper Engine"),
        ("Lively", "Lively Wallpaper"),
    };

    private static bool Rodando(string p)
    {
        var ps = Process.GetProcessesByName(p);
        bool r = ps.Length > 0;
        foreach (var x in ps) x.Dispose();
        return r;
    }

    public static List<(string processo, string nome)> GravadoresAbertos() =>
        Gravadores.Where(g => Rodando(g.processo)).GroupBy(g => g.nome).Select(g => g.First()).ToList();

    /// <summary>Fecha agora e coloca na lista do OTIMIZAR (fecha sozinho das próximas vezes).</summary>
    public static void FecharEIncluir(List<(string processo, string nome)> lista)
    {
        var cfg = Configuracao.Atual;
        foreach (var (processo, _) in lista)
        {
            foreach (var p in Process.GetProcessesByName(processo)) try { p.Kill(true); } catch { }
            if (!processo.StartsWith("wallpaper") && processo != "Lively" && !cfg.ProgramasParaFechar.Contains(processo))
                cfg.ProgramasParaFechar.Add(processo);
        }
        cfg.Salvar();
    }

    // ---------- Overlay / gravação da NVIDIA ----------
    public static bool OverlayNvidiaAberto() => Rodando("NVIDIA Overlay") || Rodando("NVIDIA Share");

    // ---------- Discord: aceleração de hardware ----------
    private static string ArquivoDiscord => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"discord\settings.json");

    public static bool TemDiscord => File.Exists(ArquivoDiscord);

    /// <summary>Sem a chave no arquivo, o Discord deixa ligada (padrão).</summary>
    public static bool DiscordAceleracaoLigada()
    {
        try
        {
            var j = JsonNode.Parse(File.ReadAllText(ArquivoDiscord));
            var v = j?["enableHardwareAcceleration"];
            return v == null || v.GetValue<bool>();
        }
        catch { return false; }
    }

    public static string DesligarAceleracaoDiscord()
    {
        // Não derruba o Discord (tu pode estar em call): pede pra fechar antes
        if (Rodando("Discord"))
            return "Fecha o Discord de vez primeiro (ícone perto do relógio → Sair do Discord) e clica em Desligar de novo. " +
                   "Se tu tá em call, deixa pra depois.";
        Backup.GuardarArquivo(ArquivoDiscord);
        var j = JsonNode.Parse(File.ReadAllText(ArquivoDiscord))?.AsObject() ?? new JsonObject();
        j["enableHardwareAcceleration"] = false;
        File.WriteAllText(ArquivoDiscord, j.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Atualizador.Log("Discord: aceleração de hardware desligada");
        return "Aceleração de hardware do Discord desligada: ele para de usar a placa de vídeo junto com o jogo. Pode abrir o Discord de novo.";
    }

    // ---------- Painel NVIDIA ----------
    public static void AbrirPainelNvidia()
    {
        string[] caminhos =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\Control Panel Client\nvcplui.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\nvcplui.exe"),
        };
        foreach (var c in caminhos)
            if (File.Exists(c)) { try { Process.Start("explorer.exe", "\"" + c + "\""); return; } catch { } }
        Sistema.Abrir("shell:AppsFolder\\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel");
    }
}
