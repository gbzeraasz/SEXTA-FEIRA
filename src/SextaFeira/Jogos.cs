using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SextaFeira;

/// <summary>Acha os jogos instalados e aplica perfis de desempenho.</summary>
internal static class Jogos
{
    public class Jogo
    {
        public string Nome;
        public string Pasta;
        public string Processo;     // nome do processo sem .exe
        public string Disco;        // SSD / HDD / ?
        public string[] Dicas;      // configs recomendadas pra placa fraca
        public bool TemPerfilAutomatico;
    }

    /// <summary>Processos de jogos online. Enquanto um deles estiver aberto, o app não usa a internet à toa.</summary>
    private static readonly string[] ProcessosOnline =
    {
        "cs2", "VALORANT-Win64-Shipping", "FortniteClient-Win64-Shipping", "League of Legends", "r5apex", "r5apex_dx12",
        "RainbowSix", "RainbowSix_Vulkan", "RocketLeague", "dota2", "cod", "Overwatch", "PUBG-Win64-Shipping", "TslGame",
        "FreeFire", "GTA5", "GTA5_Enhanced", "Marvel-Win64-Shipping", "deadlock"
    };

    /// <summary>Nome do jogo online aberto agora (ou null).</summary>
    public static string JogoAberto()
    {
        foreach (var nome in ProcessosOnline)
        {
            try
            {
                var ps = System.Diagnostics.Process.GetProcessesByName(nome);
                bool achou = ps.Length > 0;
                foreach (var p in ps) p.Dispose();
                if (achou) return nome;
            }
            catch { }
        }
        return null;
    }

    public static List<Jogo> Detectar()
    {
        var lista = new List<Jogo>();

        // ---------- Counter-Strike 2 (Steam) ----------
        foreach (var lib in BibliotecasSteam())
        {
            string cs = Path.Combine(lib, @"steamapps\common\Counter-Strike Global Offensive");
            if (Directory.Exists(cs))
            {
                lista.Add(new Jogo
                {
                    Nome = "Counter-Strike 2",
                    Pasta = cs,
                    Processo = "cs2",
                    Dicas = new[]
                    {
                        "Qualidade geral: tudo no Baixo",
                        "Anti-serrilhado: sem MSAA (use nada ou o mais leve)",
                        "NVIDIA Reflex: Ativado + Boost",
                        "V-Sync: desligado",
                        "Modo de tela: Tela cheia",
                        "Maior ganho com a GTX 1050 Ti: baixar a resolução (ex: 1280x960 esticado)",
                        "NÃO use -high nem -threads nas opções de inicialização: atrapalham"
                    }
                });
                break;
            }
        }

        // ---------- Fortnite (Epic) ----------
        string manifestos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Epic\EpicGamesLauncher\Data\Manifests");
        if (Directory.Exists(manifestos))
        {
            foreach (var arq in Directory.EnumerateFiles(manifestos, "*.item"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(arq));
                    var raiz = doc.RootElement;
                    if (raiz.TryGetProperty("DisplayName", out var n) && n.GetString() == "Fortnite" &&
                        raiz.TryGetProperty("InstallLocation", out var loc) && Directory.Exists(loc.GetString()))
                    {
                        lista.Add(new Jogo
                        {
                            Nome = "Fortnite",
                            Pasta = loc.GetString(),
                            Processo = "FortniteClient-Win64-Shipping",
                            TemPerfilAutomatico = true,
                            Dicas = new[]
                            {
                                "Modo de Renderização: DESEMPENHO (o maior ganho de FPS do jogo)",
                                "Sombras: desligadas",
                                "Efeitos e Pós-processamento: Baixo",
                                "Distância de visão: Longe (melhor pra ver inimigos)",
                                "NVIDIA Reflex: Ativado + Boost",
                                "O botão abaixo já aplica o Modo Desempenho pra ti"
                            }
                        });
                        break;
                    }
                }
                catch { }
            }
        }

        // ---------- Valorant (Riot) ----------
        foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            string v = Path.Combine(d.RootDirectory.FullName, @"Riot Games\VALORANT");
            if (Directory.Exists(v))
            {
                lista.Add(new Jogo
                {
                    Nome = "Valorant",
                    Pasta = v,
                    Processo = "VALORANT-Win64-Shipping",
                    Dicas = new[]
                    {
                        "Qualidade de material, textura e detalhes: Baixo",
                        "Renderização multithread: LIGADA",
                        "Anti-serrilhado: nenhum  •  Bloom e distorção: desligados",
                        "Limitar FPS em segundo plano: 60",
                        "NVIDIA Reflex: Ativado + Boost",
                        "Se ainda faltar FPS: 1600x900 dá um salto grande"
                    }
                });
                break;
            }
        }

        foreach (var j in lista) j.Disco = Sistema.TipoDisco(j.Pasta[0]);
        return lista;
    }

    private static List<string> BibliotecasSteam()
    {
        var libs = new List<string>();
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            string steam = k?.GetValue("SteamPath")?.ToString()?.Replace('/', '\\');
            if (steam == null) return libs;
            libs.Add(steam);
            string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\""))
                    libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            }
        }
        catch { }
        return libs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool EstaAberto(Jogo j) => Process.GetProcessesByName(j.Processo).Length > 0;

    // ---------- Perfil automático do Fortnite ----------

    public static string ArquivoFortnite => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini");

    public static bool FortniteEmModoDesempenho()
    {
        try
        {
            if (!File.Exists(ArquivoFortnite)) return false;
            var ini = File.ReadAllText(ArquivoFortnite);
            return ini.Contains("PreferredFeatureLevel=es31", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Liga o Modo Desempenho e baixa o que mais pesa. Retorna mensagem pro usuário.</summary>
    public static string AplicarPerfilFortnite()
    {
        if (Process.GetProcessesByName("FortniteClient-Win64-Shipping").Length > 0)
            return "Fecha o Fortnite primeiro, senão ele desfaz a mudança quando fechar.";
        if (!File.Exists(ArquivoFortnite))
            return "Não achei as configs do Fortnite. Abre o jogo uma vez, fecha e tenta de novo.";

        Backup.GuardarArquivo(ArquivoFortnite);
        var ini = new Ini(ArquivoFortnite);
        ini.Definir("D3DRHIPreference", "PreferredRHI", "dx11");
        ini.Definir("D3DRHIPreference", "PreferredFeatureLevel", "es31");
        ini.Definir("ScalabilityGroups", "sg.ShadowQuality", "0");
        ini.Definir("ScalabilityGroups", "sg.PostProcessQuality", "0");
        ini.Definir("ScalabilityGroups", "sg.EffectsQuality", "0");
        ini.Definir("ScalabilityGroups", "sg.FoliageQuality", "0");
        ini.Salvar();
        return "Modo Desempenho aplicado! Abre o Fortnite e confere o ganho de FPS.";
    }

    // ---------- Perfil competitivo do CS2 ----------
    // Mesmos valores que o menu de vídeo do jogo grava (cs2_video.txt). Não mexe na resolução: isso é gosto teu.

    private static readonly (string chave, string valor, string oQue)[] PerfilCs2 =
    {
        ("setting.videocfg_shadow_quality", "0", "Sombras: Baixo"),
        ("setting.videocfg_dynamic_shadows", "1", "Sombras dinâmicas: Todas (pra ver a sombra do inimigo)"),
        ("setting.videocfg_texture_detail", "0", "Texturas: Baixo"),
        ("setting.shaderquality", "0", "Shaders: Baixo"),
        ("setting.videocfg_particle_detail", "0", "Partículas: Baixo"),
        ("setting.videocfg_ao_detail", "0", "Oclusão de ambiente: Desligada"),
        ("setting.videocfg_hdr_detail", "3", "HDR: Desempenho"),
        ("setting.msaa_samples", "0", "MSAA: desligado (é o que mais pesa no CS2)"),
        ("setting.r_csgo_cmaa_enable", "1", "Anti-serrilhado leve: CMAA2"),
        ("setting.r_texturefilteringquality", "1", "Filtro de textura: Trilinear"),
        ("setting.videocfg_fsr_detail", "0", "FSR: desligado"),
        ("setting.mat_vsync", "0", "V-Sync: desligado"),
        ("setting.r_low_latency", "1", "NVIDIA Reflex: ligado"),
    };

    public static List<string> ArquivosCs2()
    {
        var l = new List<string>();
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            string steam = k?.GetValue("SteamPath")?.ToString()?.Replace('/', '\\');
            string ud = steam == null ? null : Path.Combine(steam, "userdata");
            if (ud != null && Directory.Exists(ud))
                foreach (var conta in Directory.GetDirectories(ud))
                {
                    string f = Path.Combine(conta, @"730\local\cfg\cs2_video.txt");
                    if (File.Exists(f)) l.Add(f);
                }
        }
        catch { }
        return l;
    }

    public static bool Cs2Otimizado()
    {
        var l = ArquivosCs2();
        return l.Count > 0 && l.All(f =>
        {
            try
            {
                string t = File.ReadAllText(f);
                return PerfilCs2.All(p => Regex.IsMatch(t, $"\"{Regex.Escape(p.chave)}\"\\s+\"{Regex.Escape(p.valor)}\""));
            }
            catch { return false; }
        });
    }

    public static string AplicarPerfilCs2()
    {
        if (Process.GetProcessesByName("cs2").Length > 0)
            return "Fecha o CS2 primeiro, senão ele desfaz a mudança quando fechar.";
        var arquivos = ArquivosCs2();
        if (arquivos.Count == 0)
            return "Não achei as configs do CS2. Abre o jogo uma vez, entra nas opções de vídeo, fecha e tenta de novo.";

        foreach (var f in arquivos)
        {
            Backup.GuardarArquivo(f);
            string t = File.ReadAllText(f);
            foreach (var (chave, valor, _) in PerfilCs2)
            {
                var rx = new Regex($"(\"{Regex.Escape(chave)}\"\\s+\")[^\"]*(\")");
                if (rx.IsMatch(t)) t = rx.Replace(t, m => m.Groups[1].Value + valor + m.Groups[2].Value, 1);
                else
                {
                    int fim = t.LastIndexOf('}');
                    if (fim > 0) t = t[..fim] + $"\t\"{chave}\"\t\t\"{valor}\"\r\n" + t[fim..];
                }
            }
            var attr = File.GetAttributes(f);
            if (attr.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(f, attr & ~FileAttributes.ReadOnly);
            File.WriteAllText(f, t);
        }
        Atualizador.Log($"Perfil CS2 aplicado em {arquivos.Count} conta(s)");
        return "Config competitiva aplicada no CS2!\n\n" + string.Join("\n", PerfilCs2.Select(p => "•  " + p.oQue)) +
               "\n\nA resolução eu não mexi. Se ainda faltar FPS, baixar a resolução é o que mais ajuda na GTX 1050 Ti." +
               "\nPra desfazer: Ferramentas → Desfazer mudanças.";
    }

    // ---------- Perfil competitivo do Valorant ----------
    // GameUserSettings.ini de cada conta. Só valores que o próprio menu do jogo grava (sem mexer em arquivo do jogo).

    private static readonly (string secao, string chave, string valor)[] PerfilValorant =
    {
        ("ScalabilityGroups", "sg.ShadowQuality", "0"),
        ("ScalabilityGroups", "sg.PostProcessQuality", "0"),
        ("ScalabilityGroups", "sg.EffectsQuality", "0"),
        ("ScalabilityGroups", "sg.FoliageQuality", "0"),
        ("ScalabilityGroups", "sg.TextureQuality", "0"),
        ("ScalabilityGroups", "sg.ShadingQuality", "0"),
        ("ScalabilityGroups", "sg.AntiAliasingQuality", "0"),
        ("ScalabilityGroups", "sg.ViewDistanceQuality", "0"),
        ("/Script/ShooterGame.ShooterGameUserSettings", "bUseVSync", "False"),
        ("/Script/ShooterGame.ShooterGameUserSettings", "bUseDynamicResolution", "False"),
    };

    public static List<string> ArquivosValorant()
    {
        var l = new List<string>();
        try
        {
            string raiz = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"VALORANT\Saved\Config");
            if (Directory.Exists(raiz))
                foreach (var conta in Directory.GetDirectories(raiz))
                {
                    string f = Path.Combine(conta, @"Windows\GameUserSettings.ini");
                    if (File.Exists(f)) l.Add(f);
                }
        }
        catch { }
        return l;
    }

    public static bool ValorantOtimizado()
    {
        var l = ArquivosValorant();
        return l.Count > 0 && l.All(f =>
        {
            try { string t = File.ReadAllText(f); return t.Contains("sg.ShadowQuality=0") && t.Contains("sg.EffectsQuality=0") && t.Contains("sg.ShadingQuality=0"); }
            catch { return false; }
        });
    }

    public static string AplicarPerfilValorant()
    {
        if (Process.GetProcessesByName("VALORANT-Win64-Shipping").Length > 0)
            return "Fecha o Valorant primeiro, senão ele desfaz a mudança quando fechar.";
        var arquivos = ArquivosValorant();
        if (arquivos.Count == 0)
            return "Não achei as configs do Valorant. Abre o jogo uma vez, fecha e tenta de novo.";

        foreach (var f in arquivos)
        {
            Backup.GuardarArquivo(f);
            var ini = new Ini(f);
            foreach (var (secao, chave, valor) in PerfilValorant) ini.Definir(secao, chave, valor);
            ini.Salvar();
        }
        Atualizador.Log($"Perfil Valorant aplicado em {arquivos.Count} conta(s)");
        return "Config competitiva aplicada no Valorant!\n\n" +
               "•  Material, textura, detalhes, sombras e efeitos: Baixo\n•  Anti-serrilhado: nenhum\n•  V-Sync e resolução dinâmica: desligados\n\n" +
               "FALTA 1 COISA que o Valorant guarda na nuvem (não dá pra ligar por fora):\n" +
               "Configurações → Vídeo → Qualidade gráfica → Renderização multithread: LIGADA.\n" +
               "No teu processador de 6 núcleos é a que mais dá FPS.\n\nPra desfazer: Ferramentas → Desfazer mudanças.";
    }
}

/// <summary>Editor simples de .ini que preserva o resto do arquivo.</summary>
internal class Ini
{
    private readonly string caminho;
    private readonly List<string> linhas;
    private readonly System.Text.Encoding codificacao;

    public Ini(string caminho)
    {
        this.caminho = caminho;
        // Mantém a mesma codificação do arquivo original (o Unreal às vezes usa UTF-16)
        using var leitor = new StreamReader(caminho, System.Text.Encoding.UTF8, true);
        linhas = leitor.ReadToEnd().Replace("\r\n", "\n").Split('\n').ToList();
        codificacao = leitor.CurrentEncoding;
        if (linhas.Count > 0 && linhas[^1] == "") linhas.RemoveAt(linhas.Count - 1);
    }

    public void Definir(string secao, string chave, string valor)
    {
        int ini = linhas.FindIndex(l => l.Trim().Equals($"[{secao}]", StringComparison.OrdinalIgnoreCase));
        if (ini < 0)
        {
            if (linhas.Count > 0 && linhas[^1].Trim() != "") linhas.Add("");
            linhas.Add($"[{secao}]");
            linhas.Add($"{chave}={valor}");
            return;
        }

        int fim = linhas.FindIndex(ini + 1, l => l.TrimStart().StartsWith("["));
        if (fim < 0) fim = linhas.Count;

        for (int i = ini + 1; i < fim; i++)
        {
            int eq = linhas[i].IndexOf('=');
            if (eq > 0 && linhas[i][..eq].Trim().Equals(chave, StringComparison.OrdinalIgnoreCase))
            {
                linhas[i] = $"{chave}={valor}";
                return;
            }
        }

        // insere antes das linhas em branco do fim da seção
        int pos = fim;
        while (pos > ini + 1 && linhas[pos - 1].Trim() == "") pos--;
        linhas.Insert(pos, $"{chave}={valor}");
    }

    public void Salvar()
    {
        var attr = File.GetAttributes(caminho);
        if (attr.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(caminho, attr & ~FileAttributes.ReadOnly);
        File.WriteAllText(caminho, string.Join("\r\n", linhas) + "\r\n", codificacao);
    }
}
