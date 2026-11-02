using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SextaFeira;

internal enum Risco { Seguro, Revisao }

/// <summary>Um item da aba "Limpeza 1 clique".</summary>
internal class ItemLimpeza
{
    public string Id;
    public string Nome;
    public string Descricao;
    public string Categoria;
    public string Icone;
    public Risco Risco = Risco.Seguro;
    public bool Padrao;            // vem marcado de fábrica
    public bool SoManual;          // nunca entra no 1 clique (demora ou desloga), só no botão Executar
    public bool EhAcao;            // não limpa arquivo (ex: plano de energia)
    public Func<bool> Disponivel = () => true;
    public Func<long> Tamanho = () => -1;          // -1 = não dá pra calcular
    public Func<long> Executar;                     // devolve bytes liberados
    public string AvisoAberto;                      // processo que trava os arquivos se estiver aberto
}

/// <summary>Catálogo de tudo que dá pra limpar/otimizar. Atualizado pelo Claude.</summary>
internal static class Limpeza
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string ProgData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    private static string Windir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    // ---------------- ajudantes ----------------

    public static long TamanhoPastas(IEnumerable<string> pastas)
    {
        long total = 0;
        foreach (var p in pastas.Where(Directory.Exists))
        {
            try
            {
                foreach (var f in new DirectoryInfo(p).EnumerateFiles("*", new EnumerationOptions
                         { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                    try { total += f.Length; } catch { }
            }
            catch { }
        }
        return total;
    }

    /// <summary>Apaga o CONTEÚDO das pastas (a pasta continua existindo). Arquivo em uso é pulado.</summary>
    public static long LimparPastas(IEnumerable<string> pastas)
    {
        var lista = pastas.Where(Directory.Exists).ToList();
        long antes = TamanhoPastas(lista);
        foreach (var p in lista)
        {
            var dir = new DirectoryInfo(p);
            try
            {
                foreach (var f in dir.EnumerateFiles())
                    try { f.Attributes = FileAttributes.Normal; f.Delete(); } catch { }
                foreach (var d in dir.EnumerateDirectories())
                    try { if (!d.Attributes.HasFlag(FileAttributes.ReparsePoint)) d.Delete(true); } catch { }
            }
            catch { }
        }
        return Math.Max(0, antes - TamanhoPastas(lista));
    }

    private static long LimparArquivos(string pasta, string padrao)
    {
        long total = 0;
        if (!Directory.Exists(pasta)) return 0;
        foreach (var f in SafeFiles(pasta, padrao))
            try { long t = f.Length; f.Delete(); total += t; } catch { }
        return total;
    }

    private static long TamanhoArquivos(string pasta, string padrao) =>
        Directory.Exists(pasta) ? SafeFiles(pasta, padrao).Sum(f => { try { return f.Length; } catch { return 0L; } }) : 0;

    private static List<FileInfo> SafeFiles(string pasta, string padrao)
    {
        try { return new DirectoryInfo(pasta).EnumerateFiles(padrao).ToList(); } catch { return new(); }
    }

    // Navegadores baseados no Chromium: cada perfil tem suas pastas de cache
    private static readonly string[] PastasCacheChromium = { "Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache" };

    private static List<string> CachesChromium(string userData)
    {
        var l = new List<string>();
        if (!Directory.Exists(userData)) return l;
        try
        {
            foreach (var perfil in Directory.EnumerateDirectories(userData))
            {
                string nome = Path.GetFileName(perfil);
                if (nome != "Default" && !nome.StartsWith("Profile ") && !nome.StartsWith("Guest")) continue;
                l.AddRange(PastasCacheChromium.Select(c => Path.Combine(perfil, c)));
            }
        }
        catch { }
        l.Add(Path.Combine(userData, "ShaderCache"));
        l.Add(Path.Combine(userData, "GrShaderCache"));
        return l;
    }

    private static List<string> CachesFirefox()
    {
        var l = new List<string>();
        string perfis = Path.Combine(Local, @"Mozilla\Firefox\Profiles");
        if (!Directory.Exists(perfis)) return l;
        try
        {
            foreach (var p in Directory.EnumerateDirectories(perfis))
            {
                l.Add(Path.Combine(p, "cache2"));
                l.Add(Path.Combine(p, "startupCache"));
                l.Add(Path.Combine(p, "thumbnails"));
            }
        }
        catch { }
        return l;
    }

    private static ItemLimpeza Navegador(string id, string nome, string processo, Func<List<string>> pastas, Func<bool> instalado) => new()
    {
        Id = id,
        Nome = $"Cache do {nome}",
        Descricao = "Imagens e arquivos temporários dos sites. Logins, senhas, favoritos e histórico NÃO são apagados.",
        Categoria = "Navegadores",
        Icone = "🌐",
        Padrao = true,
        Disponivel = instalado,
        AvisoAberto = processo,
        Tamanho = () => TamanhoPastas(pastas()),
        Executar = () => LimparPastas(pastas())
    };

    // ---------------- Lixeira (API do Windows) ----------------

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHQueryRecycleBin(string root, ref SHQUERYRBINFO info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);

    private static long TamanhoLixeira()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        SHQueryRecycleBin(null, ref info);
        return info.i64Size;
    }

    // ---------------- Catálogo ----------------

    public static readonly List<ItemLimpeza> Itens = Montar();

    private static List<ItemLimpeza> Montar()
    {
        string chromeUD = Path.Combine(Local, @"Google\Chrome\User Data");
        string edgeUD = Path.Combine(Local, @"Microsoft\Edge\User Data");
        string braveUD = Path.Combine(Local, @"BraveSoftware\Brave-Browser\User Data");
        string operaGX = Path.Combine(Local, @"Opera Software\Opera GX Stable");
        string opera = Path.Combine(Local, @"Opera Software\Opera Stable");
        string discord = Path.Combine(Roaming, "discord");
        string[] DiscordCache() => new[] { "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache" }
            .Select(c => Path.Combine(discord, c)).ToArray();

        var l = new List<ItemLimpeza>
        {
            // ================= SISTEMA =================
            new()
            {
                Id = "temp", Nome = "Arquivos temporários", Icone = "🗂", Categoria = "Windows", Padrao = true,
                Descricao = "Sobras de instalações e programas (pasta Temp do usuário e do Windows).",
                Tamanho = () => TamanhoPastas(new[] { Path.GetTempPath(), Path.Combine(Windir, "Temp") }),
                Executar = () => LimparPastas(new[] { Path.GetTempPath(), Path.Combine(Windir, "Temp") })
            },
            new()
            {
                Id = "lixeira", Nome = "Lixeira", Icone = "🗑", Categoria = "Windows", Padrao = true,
                Descricao = "Esvazia a lixeira de todos os discos.",
                Tamanho = TamanhoLixeira,
                Executar = () => { long t = TamanhoLixeira(); SHEmptyRecycleBin(IntPtr.Zero, null, 1 | 2 | 4); return t; }
            },
            new()
            {
                Id = "erros", Nome = "Relatórios de erro", Icone = "🧾", Categoria = "Windows", Padrao = true,
                Descricao = "Relatórios de travamento que o Windows guarda e quase ninguém usa.",
                Tamanho = () => TamanhoPastas(PastasErro()),
                Executar = () => LimparPastas(PastasErro())
            },
            new()
            {
                Id = "inetcache", Nome = "Cache de internet do Windows", Icone = "🧭", Categoria = "Windows", Padrao = true,
                Descricao = "Cache usado pelo Windows e por apps antigos.",
                Tamanho = () => TamanhoPastas(new[] { Path.Combine(Local, @"Microsoft\Windows\INetCache") }),
                Executar = () => LimparPastas(new[] { Path.Combine(Local, @"Microsoft\Windows\INetCache") })
            },
            new()
            {
                Id = "recentes", Nome = "Lista de arquivos recentes", Icone = "🕘", Categoria = "Windows", Padrao = true,
                Descricao = "Privacidade: limpa a lista de 'recentes'. Os fixados do Acesso Rápido ficam.",
                Tamanho = () => TamanhoArquivos(Environment.GetFolderPath(Environment.SpecialFolder.Recent), "*"),
                Executar = () => LimparArquivos(Environment.GetFolderPath(Environment.SpecialFolder.Recent), "*")
            },
            new()
            {
                Id = "miniaturas", Nome = "Cache de miniaturas", Icone = "🖼", Categoria = "Windows",
                Descricao = "Miniaturas de fotos e vídeos. O Windows recria quando abrir as pastas (primeira vez fica mais lento).",
                Tamanho = () => TamanhoArquivos(Path.Combine(Local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db"),
                Executar = () => LimparArquivos(Path.Combine(Local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db")
            },
            new()
            {
                Id = "winupdate", Nome = "Downloads velhos do Windows Update", Icone = "🔄", Categoria = "Windows",
                Descricao = "Arquivos de atualizações que já foram instaladas. Costuma passar de 1 GB.",
                Tamanho = () => TamanhoPastas(new[] { Path.Combine(Windir, @"SoftwareDistribution\Download") }),
                Executar = () =>
                {
                    Sistema.Rodar("net", "stop wuauserv /y", 30000);
                    Sistema.Rodar("net", "stop bits /y", 30000);
                    long t = LimparPastas(new[] { Path.Combine(Windir, @"SoftwareDistribution\Download") });
                    Sistema.Rodar("net", "start bits", 30000);
                    Sistema.Rodar("net", "start wuauserv", 30000);
                    return t;
                }
            },
            new()
            {
                Id = "otimizacaoentrega", Nome = "Cache da Otimização de Entrega", Icone = "📦", Categoria = "Windows",
                Descricao = "Pedaços de atualizações que o Windows guarda pra compartilhar com outros PCs.",
                Tamanho = () => TamanhoPastas(new[] { Path.Combine(Windir, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache") }),
                Executar = () =>
                {
                    string p = Path.Combine(Windir, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache");
                    long antes = TamanhoPastas(new[] { p });
                    Sistema.Rodar("powershell", "-NoProfile -Command \"Delete-DeliveryOptimizationCache -Force\"", 60000);
                    return Math.Max(0, antes - TamanhoPastas(new[] { p }));
                }
            },
            new()
            {
                Id = "componentes", Nome = "Limpeza profunda do Windows (componentes)", Icone = "🧹", Categoria = "Windows",
                Risco = Risco.Revisao, SoManual = true,
                Descricao = "Remove versões antigas de componentes do Windows (ferramenta oficial DISM). Pode levar de 5 a 20 minutos.",
                Executar = () =>
                {
                    var c = new DriveInfo("C");
                    long antes = c.AvailableFreeSpace;
                    Sistema.Rodar("dism", "/Online /Cleanup-Image /StartComponentCleanup /Quiet", 40 * 60 * 1000);
                    c = new DriveInfo("C");
                    return Math.Max(0, c.AvailableFreeSpace - antes);
                }
            },
            new()
            {
                Id = "shaders", Nome = "Cache de shaders da placa de vídeo", Icone = "🎨", Categoria = "Windows",
                Risco = Risco.Revisao, SoManual = true,
                Descricao = "Só use se algum jogo estiver com bug gráfico. Os jogos vão travar um pouco nas primeiras partidas enquanto recriam o cache.",
                Tamanho = () => TamanhoPastas(PastasShader()),
                Executar = () => LimparPastas(PastasShader())
            },
            new()
            {
                Id = "nvidiasobras", Nome = "Sobras de instalação da NVIDIA", Icone = "🟩", Categoria = "Windows", Padrao = true,
                Descricao = "Arquivos de drivers antigos que o instalador da NVIDIA deixa pra trás.",
                Disponivel = () => Directory.Exists(Path.Combine(ProgData, @"NVIDIA Corporation\Downloader")) || Directory.Exists(@"C:\NVIDIA"),
                Tamanho = () => TamanhoPastas(new[] { Path.Combine(ProgData, @"NVIDIA Corporation\Downloader"), @"C:\NVIDIA" }),
                Executar = () => LimparPastas(new[] { Path.Combine(ProgData, @"NVIDIA Corporation\Downloader"), @"C:\NVIDIA" })
            },

            // ================= NAVEGADORES =================
            Navegador("chrome", "Google Chrome", "chrome", () => CachesChromium(chromeUD), () => Directory.Exists(chromeUD)),
            Navegador("edge", "Microsoft Edge", "msedge", () => CachesChromium(edgeUD), () => Directory.Exists(edgeUD)),
            Navegador("brave", "Brave", "brave", () => CachesChromium(braveUD), () => Directory.Exists(braveUD)),
            Navegador("operagx", "Opera GX", "opera", () => new() { Path.Combine(operaGX, "Cache"), Path.Combine(operaGX, "Code Cache"), Path.Combine(operaGX, "GPUCache") }, () => Directory.Exists(operaGX)),
            Navegador("opera", "Opera", "opera", () => new() { Path.Combine(opera, "Cache"), Path.Combine(opera, "Code Cache"), Path.Combine(opera, "GPUCache") }, () => Directory.Exists(opera)),
            Navegador("firefox", "Firefox", "firefox", CachesFirefox, () => Directory.Exists(Path.Combine(Local, @"Mozilla\Firefox\Profiles"))),

            // ================= APPS =================
            new()
            {
                Id = "discordcache", Nome = "Limpador de cache do Discord", Icone = "💬", Categoria = "Apps e launchers", Padrao = true,
                Descricao = "Libera espaço e corrige lentidão e imagens bugadas no Discord. Tu continua logado.",
                Disponivel = () => Directory.Exists(discord), AvisoAberto = "Discord",
                Tamanho = () => TamanhoPastas(DiscordCache()),
                Executar = () => LimparPastas(DiscordCache())
            },
            new()
            {
                Id = "discordsessao", Nome = "Sessão do Discord", Icone = "🚪", Categoria = "Apps e launchers",
                Risco = Risco.Revisao, SoManual = true,
                Descricao = "Apaga os dados locais de sessão pra destravar o Discord. Vai te DESLOGAR (precisa entrar de novo).",
                Disponivel = () => Directory.Exists(discord), AvisoAberto = "Discord",
                Tamanho = () => TamanhoPastas(new[] { Path.Combine(discord, "Local Storage"), Path.Combine(discord, "Session Storage") }),
                Executar = () =>
                {
                    foreach (var p in Process.GetProcessesByName("Discord")) try { p.Kill(); } catch { }
                    Thread.Sleep(1500);
                    return LimparPastas(new[] { Path.Combine(discord, "Local Storage"), Path.Combine(discord, "Session Storage") });
                }
            },
            new()
            {
                Id = "steam", Nome = "Cache da loja da Steam", Icone = "🎮", Categoria = "Apps e launchers", Padrao = true,
                Descricao = "Cache das páginas da loja e da comunidade. Deixa a Steam mais leve. Não mexe nos jogos.",
                Disponivel = () => Directory.Exists(Path.Combine(Local, @"Steam\htmlcache")), AvisoAberto = "steamwebhelper",
                Tamanho = () => TamanhoPastas(new[] { Path.Combine(Local, @"Steam\htmlcache") }),
                Executar = () => LimparPastas(new[] { Path.Combine(Local, @"Steam\htmlcache") })
            },
            new()
            {
                Id = "epic", Nome = "Cache do Epic Games Launcher", Icone = "🛡", Categoria = "Apps e launchers", Padrao = true,
                Descricao = "Cache das páginas do launcher. Não mexe nos jogos.",
                Disponivel = () => Directory.Exists(Path.Combine(Local, @"EpicGamesLauncher\Saved")), AvisoAberto = "EpicGamesLauncher",
                Tamanho = () => TamanhoPastas(PastasEpic()),
                Executar = () => LimparPastas(PastasEpic())
            },
            new()
            {
                Id = "battlenet", Nome = "Cache do Battle.net", Icone = "🔷", Categoria = "Apps e launchers", Padrao = true,
                Descricao = "Cache do launcher da Blizzard. Não mexe nos jogos.",
                Disponivel = () => Directory.Exists(Path.Combine(Local, "Battle.net")), AvisoAberto = "Battle.net",
                Tamanho = () => TamanhoPastas(new[] { Path.Combine(Local, @"Battle.net\Cache"), Path.Combine(Local, @"Battle.net\BrowserCaches") }),
                Executar = () => LimparPastas(new[] { Path.Combine(Local, @"Battle.net\Cache"), Path.Combine(Local, @"Battle.net\BrowserCaches") })
            },
            new()
            {
                Id = "spotify", Nome = "Cache do Spotify", Icone = "🎵", Categoria = "Apps e launchers",
                Descricao = "Músicas guardadas pra tocar mais rápido. Ele baixa de novo quando tu ouvir (não apaga as baixadas offline).",
                Disponivel = () => Directory.Exists(Path.Combine(Local, @"Spotify\Data")), AvisoAberto = "Spotify",
                Tamanho = () => TamanhoPastas(new[] { Path.Combine(Local, @"Spotify\Data") }),
                Executar = () => LimparPastas(new[] { Path.Combine(Local, @"Spotify\Data") })
            },

            // ================= AÇÕES (não apagam arquivo) =================
            new()
            {
                Id = "fechar", Nome = "Fechar programas de fundo", Icone = "⛔", Categoria = "Desempenho", Padrao = true, EhAcao = true,
                Descricao = "Fecha launchers e apps que ficam abertos à toa (lista editável em Ferramentas).",
                Executar = () => 0
            },
            new()
            {
                Id = "ajustesjogo", Nome = "Ajustes de jogo do Windows", Icone = "🎯", Categoria = "Desempenho", Padrao = true, EhAcao = true,
                Descricao = "Modo de Jogo ligado, gravação em segundo plano desligada e otimizações para jogos em janela.",
                Executar = () => 0
            },
            new()
            {
                Id = "energia", Nome = "Plano Alto Desempenho", Icone = "⚡", Categoria = "Desempenho", Padrao = true, EhAcao = true,
                Descricao = "Deixa o processador pronto pra entregar o máximo.",
                Executar = () => 0
            },
            new()
            {
                Id = "mouse", Nome = "Desligar aceleração do mouse", Icone = "🖱", Categoria = "Desempenho", Padrao = true, EhAcao = true,
                Descricao = "'Aumentar precisão do ponteiro' desligado: o mesmo movimento sempre move a mira o mesmo tanto.",
                Executar = () => 0
            },
            new()
            {
                Id = "usb", Nome = "Suspensão seletiva de USB desligada", Icone = "🔌", Categoria = "Desempenho", Padrao = true, EhAcao = true,
                Descricao = "Impede o Windows de 'adormecer' mouse, teclado e headset USB pra economizar energia.",
                Executar = () => 0
            },
            new()
            {
                Id = "throttling", Nome = "Power Throttling desligado", Icone = "🚀", Categoria = "Desempenho", Padrao = true, EhAcao = true,
                Descricao = "Impede o Windows de segurar o processador em programas que ele acha que são 'de fundo'.",
                Executar = () => 0
            },
            new()
            {
                Id = "ram", Nome = "Liberar RAM em espera", Icone = "🧠", Categoria = "Desempenho", Padrao = true, EhAcao = true,
                Descricao = "Esvazia o cache de memória do Windows. Ajuda em PCs com 16 GB ou menos.",
                Executar = () => 0
            },
        };
        return l;
    }

    private static string[] PastasErro() => new[]
    {
        Path.Combine(Local, "CrashDumps"),
        Path.Combine(ProgData, @"Microsoft\Windows\WER\ReportQueue"),
        Path.Combine(ProgData, @"Microsoft\Windows\WER\ReportArchive")
    };

    private static string[] PastasShader() => new[]
    {
        Path.Combine(Local, "D3DSCache"),
        Path.Combine(Local, @"NVIDIA\DXCache"),
        Path.Combine(Local, @"NVIDIA\GLCache"),
        Path.Combine(Local, @"AMD\DxCache"),
        Path.Combine(Local, @"AMD\DxcCache"),
    };

    private static string[] PastasEpic()
    {
        string saved = Path.Combine(Local, @"EpicGamesLauncher\Saved");
        var l = new List<string>();
        try { if (Directory.Exists(saved)) l.AddRange(Directory.EnumerateDirectories(saved, "webcache*")); } catch { }
        return l.ToArray();
    }

    public static ItemLimpeza Pegar(string id) => Itens.FirstOrDefault(i => i.Id == id);

    public static bool EstaAberto(ItemLimpeza item) =>
        item.AvisoAberto != null && Process.GetProcessesByName(item.AvisoAberto).Length > 0;
}
