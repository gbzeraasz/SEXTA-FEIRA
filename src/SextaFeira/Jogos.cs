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
