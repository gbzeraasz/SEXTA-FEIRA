using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SextaFeira;

/// <summary>Acha os jogos instalados em Steam, Epic, Riot, Ubisoft, EA e Battle.net.</summary>
internal static class Biblioteca
{
    public class JogoInstalado
    {
        public string Nome;
        public string Plataforma;
        public string Pasta;
        public string SteamId;
        public string Disco = "?";
        public ConfigJogo Config;
    }

    // Coisas da Steam que não são jogo
    private static readonly string[] Ignorar =
    {
        "redistributable", "steamworks", "proton", "steam linux runtime", "steamvr",
        "soundtrack", "dedicated server", "sdk", "benchmark", "wallpaper engine"
    };

    public static List<JogoInstalado> Listar()
    {
        var lista = new List<JogoInstalado>();
        try { lista.AddRange(Steam()); } catch { }
        try { lista.AddRange(Epic()); } catch { }
        try { lista.AddRange(Riot()); } catch { }
        try { lista.AddRange(Ubisoft()); } catch { }
        try { lista.AddRange(EA()); } catch { }
        try { lista.AddRange(BattleNet()); } catch { }

        // Remove repetidos (mesmo jogo em dois lugares) e liga com o perfil competitivo
        var final = new List<JogoInstalado>();
        foreach (var j in lista.Where(j => !Ignorar.Any(i => j.Nome.Contains(i, StringComparison.OrdinalIgnoreCase))))
        {
            j.Config = ConfigsCompetitivas.Procurar(j.Nome, j.SteamId);
            string chave = j.Config?.Nome ?? j.Nome;
            if (final.Any(f => (f.Config?.Nome ?? f.Nome).Equals(chave, StringComparison.OrdinalIgnoreCase))) continue;
            if (j.Config != null) j.Nome = j.Config.Nome;
            final.Add(j);
        }

        foreach (var j in final)
            if (!string.IsNullOrEmpty(j.Pasta) && j.Pasta.Length > 1) j.Disco = Sistema.TipoDisco(j.Pasta[0]);

        // Com perfil primeiro, depois em ordem alfabética
        return final.OrderBy(j => j.Config == null).ThenBy(j => j.Nome).ToList();
    }

    // ---------------- Steam ----------------
    private static IEnumerable<JogoInstalado> Steam()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        string steam = k?.GetValue("SteamPath")?.ToString()?.Replace('/', '\\');
        if (steam == null) yield break;

        var libs = new List<string> { steam };
        string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
        if (File.Exists(vdf))
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\""))
                libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));

        foreach (var lib in libs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var acf in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                string txt;
                try { txt = File.ReadAllText(acf); } catch { continue; }
                string Campo(string nome) => Regex.Match(txt, $"\"{nome}\"\\s+\"(.*?)\"").Groups[1].Value;
                string nomeJogo = Campo("name");
                if (string.IsNullOrWhiteSpace(nomeJogo)) continue;
                yield return new JogoInstalado
                {
                    Nome = nomeJogo,
                    Plataforma = "Steam",
                    SteamId = Campo("appid"),
                    Pasta = Path.Combine(apps, "common", Campo("installdir"))
                };
            }
        }
    }

    // ---------------- Epic ----------------
    private static IEnumerable<JogoInstalado> Epic()
    {
        string pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Epic\EpicGamesLauncher\Data\Manifests");
        if (!Directory.Exists(pasta)) yield break;
        foreach (var arq in Directory.EnumerateFiles(pasta, "*.item"))
        {
            JogoInstalado j = null;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(arq));
                var r = doc.RootElement;
                string nome = r.TryGetProperty("DisplayName", out var n) ? n.GetString() : null;
                string loc = r.TryGetProperty("InstallLocation", out var l) ? l.GetString() : null;
                bool ehJogo = !r.TryGetProperty("AppCategories", out var cats) ||
                              cats.EnumerateArray().Any(c => c.GetString() == "games");
                if (nome != null && ehJogo && Directory.Exists(loc))
                    j = new JogoInstalado { Nome = nome, Plataforma = "Epic", Pasta = loc };
            }
            catch { }
            if (j != null) yield return j;
        }
    }

    // ---------------- Riot ----------------
    private static IEnumerable<JogoInstalado> Riot()
    {
        foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            string raiz = Path.Combine(d.RootDirectory.FullName, "Riot Games");
            if (!Directory.Exists(raiz)) continue;
            if (Directory.Exists(Path.Combine(raiz, "VALORANT")))
                yield return new JogoInstalado { Nome = "Valorant", Plataforma = "Riot", Pasta = Path.Combine(raiz, "VALORANT") };
            if (Directory.Exists(Path.Combine(raiz, "League of Legends")))
                yield return new JogoInstalado { Nome = "League of Legends", Plataforma = "Riot", Pasta = Path.Combine(raiz, "League of Legends") };
        }
    }

    // ---------------- Ubisoft ----------------
    private static IEnumerable<JogoInstalado> Ubisoft()
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs");
        if (k == null) yield break;
        foreach (var id in k.GetSubKeyNames())
        {
            using var sub = k.OpenSubKey(id);
            string pasta = sub?.GetValue("InstallDir")?.ToString()?.Replace('/', '\\').TrimEnd('\\');
            if (string.IsNullOrEmpty(pasta) || !Directory.Exists(pasta)) continue;
            yield return new JogoInstalado { Nome = Path.GetFileName(pasta), Plataforma = "Ubisoft", Pasta = pasta };
        }
    }

    // ---------------- EA ----------------
    private static IEnumerable<JogoInstalado> EA()
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Electronic Arts")
                   ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Electronic Arts");
        if (k == null) yield break;
        foreach (var nome in k.GetSubKeyNames())
        {
            if (nome.StartsWith("EA ", StringComparison.OrdinalIgnoreCase)) continue; // EA Desktop/EA Core
            using var sub = k.OpenSubKey(nome);
            string pasta = sub?.GetValue("Install Dir")?.ToString()?.TrimEnd('\\');
            if (string.IsNullOrEmpty(pasta) || !Directory.Exists(pasta)) continue;
            yield return new JogoInstalado { Nome = nome, Plataforma = "EA", Pasta = pasta };
        }
    }

    // ---------------- Battle.net ----------------
    private static IEnumerable<JogoInstalado> BattleNet()
    {
        string[] conhecidos = { "Overwatch", "Call of Duty", "Diablo IV", "Hearthstone", "World of Warcraft" };
        var raizes = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        foreach (var raiz in raizes.Where(Directory.Exists).Distinct())
            foreach (var nome in conhecidos)
            {
                string pasta = Path.Combine(raiz, nome);
                if (Directory.Exists(pasta))
                    yield return new JogoInstalado { Nome = nome == "Overwatch" ? "Overwatch 2" : nome, Plataforma = "Battle.net", Pasta = pasta };
            }
    }
}
