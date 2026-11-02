namespace SextaFeira;

/// <summary>
/// Miras dos pros por time. Os códigos de CS2 estão no formato novo ("CS...", desde 30/09/2026)
/// e foram conferidos pelo checksum. Quem não tem código público confiável fica só com o link do ProSettings.
/// Atualizado pelo Claude.
/// </summary>
internal static class Pros
{
    public class Pro
    {
        public string Nome;
        public string Pais;      // código ISO de 2 letras (bandeira)
        public string Mira;      // código pra importar (null = sem código confiável)
        public string Detalhe;   // sens, DPI etc.
        public string Slug => Nome.ToLowerInvariant().Replace(" ", "-");
    }

    public class Time
    {
        public string Nome;
        public Color Cor;
        public string Obs;
        public Pro[] Jogadores;
    }

    public class ListaJogo
    {
        public string Jogo;
        public string ComoImportar;
        public string Fonte;
        public string Conferido;
        public Time[] Times;
    }

    private static Pro P(string nome, string pais, string mira = null, string detalhe = null) =>
        new() { Nome = nome, Pais = pais, Mira = mira, Detalhe = detalhe };

    public static readonly ListaJogo[] Jogos =
    {
        new ListaJogo
        {
            Jogo = "Counter-Strike 2",
            ComoImportar = "No CS2: Configurações → Jogo → Mira → Compartilhar ou importar → cola o código. Só o formato novo (começa com CS, sem traços) funciona desde 30/09/2026.",
            Fonte = "cs2crosshaircode.com e procrosshairs.com (códigos conferidos pelo checksum)",
            Conferido = "nov/2026",
            Times = new[]
            {
                new Time
                {
                    Nome = "FURIA", Cor = Color.FromArgb(230, 230, 230),
                    Jogadores = new[]
                    {
                        P("FalleN", "br", "CSaGZfHaBrr5bU6mQbnXniJFEmwP6KF9yUfeiOwFiBThYD"),
                        P("KSCERATO", "br", "CSXVQtCujeaNWjvQatoVBuqiWcRLPqAUAJ4AFee9yd25dO"),
                        P("yuurih", "br", "CSHMaMsuZ4tkuSMkpx6ajXU9XyuWcjcxMG5nKQKj8STPGH"),
                        P("YEKINDAR", "lv", "CSHwwEuPeaioROD9qTvmN7HT3umotat8EACz2BvTXsPUJe"),
                        P("molodoy", "kz", "CSpxaYsCrCP5Hk4GPu7Kmv2z2QFpzrNGuwX2RTWNb628CU"),
                    }
                },
                new Time
                {
                    Nome = "Falcons", Cor = Color.FromArgb(0, 173, 120),
                    Obs = "Só os jogadores com código confirmado.",
                    Jogadores = new[]
                    {
                        P("NiKo", "ba", "CSfSQt6wxXdaV3OxDRzaS6FoQX2ortyyinwaXvwsNiJAcV"),
                        P("m0NESY", "ru", "CSrcC2jkTYnczGVYqNkDS9ABC3JuTcKcQzDx3ROs8hvmQi"),
                        P("kyousuke", "ru", "CSjjQVLnqmjh7O3G3VADW9tw3o3DTNavZfDsNDd7byXdxJ"),
                    }
                },
                new Time
                {
                    Nome = "Vitality", Cor = Color.FromArgb(255, 214, 0),
                    Jogadores = new[]
                    {
                        P("apEX", "fr", "CSHhdAW9O6Kujk5Wc8i2YOYzJsQiQk8hP2XJbEfHTMtnQi"),
                        P("ZywOo", "fr", "CS4N3cTLGtbqN5JxffWv6vBB5dqiCwEP55KQhZQ4bJ4JQE"),
                        P("ropz", "ee", "CSUxjvBKxbaTKG5sfdufqzDvzeAiaTuveQmT6XFOvWmvAc"),
                        P("flameZ", "il", "CSCTWJSVydL9ecJ292AFNuNd7tj4qfJTDzV34iQqryCjhj"),
                        P("mezii", "gb", "CSvfsMCShd5uL4nTGYcUtuSqy4PJRHmCU6UoT9q5FK6DHE"),
                    }
                },
                new Time
                {
                    Nome = "Team Spirit", Cor = Color.FromArgb(150, 150, 160),
                    Jogadores = new[]
                    {
                        P("donk", "ru", "CS2VkbZz3rjH7UzZTUsLDYH7O25fYRoviPYK8eym3XtVhE"),
                        P("sh1ro", "ru", "CSDcomrJBKpqwRGP3676xoNKTGhHdCOaZ6xY5v5wUQHzyD"),
                        P("magixx", "ru", "CSOO4jyrC6XY2puAqtAdDeVK6Tmc77UpYwh9MkiDHQubnQ"),
                        P("zont1x", "ua", "CSNA85FOmKJfsbrdsdhBuM6hxKnvw5wGDytDWVbNXzvT8m"),
                        P("tN1R", "by"),
                    }
                },
            }
        },
        new ListaJogo
        {
            Jogo = "Valorant",
            ComoImportar = "No Valorant: Configurações → Mira → botão de importar perfil → cola o código.",
            Fonte = "PCGamesN, valorant-spind.de, specs.gg, esportsettings.com e crosshair-pro.com",
            Conferido = "nov/2026",
            Times = new[]
            {
                new Time
                {
                    Nome = "LOUD", Cor = Color.FromArgb(0, 230, 90), Obs = "Elenco campeão do Champions 2026.",
                    Jogadores = new[]
                    {
                        P("erde", "cl", "0;P;c;1;o;1;0t;1;0l;1;0o;1;0a;1;0f;0;1t;0;1l;0;1o;0;1a;0;1m;0;1f;0"),
                        P("lukxo", "br"),
                        P("tkzin", "br", "0;s;1;P;o;1;d;1;f;0;m;1;0l;1;0o;1;0a;1;0f;0;1t;0;1l;0;1o;0;1a;1;1m;0;1f;0"),
                        P("DaviH", "pt"),
                        P("Darker", "co"),
                    }
                },
                new Time
                {
                    Nome = "NRG", Cor = Color.FromArgb(255, 110, 30), Obs = "Elenco do Champions 2026.",
                    Jogadores = new[]
                    {
                        P("Ethan", "us", "0;P;h;0;0l;4;0o;0;0a;1;0f;0;1b;0"),
                        P("brawk", "us", "0;P;c;1;o;1;0l;5;0a;1;0f;0;1b;0"),
                        P("skuba", "us", "0;s;1;P;c;1;u;000000FF;h;0;f;0;m;1;0t;1;0l;3;0v;4;0o;2;0a;1;0f;0;1b;0;S;s;0.75;o;1"),
                        P("mada", "ca", "0;s;1;P;u;000000FF;o;1;d;1;f;0;m;1;0l;2;0v;3;0o;0;0a;1;0f;0;1b;0;S;b;1;c;8;t;00FF66FF;o;1"),
                        P("keiko", "gb", "0;s;1;P;u;000000FF;o;1;d;1;z;1;0b;0;1l;0;1a;0;1m;0;1f;0"),
                    }
                },
                new Time
                {
                    Nome = "Vitality", Cor = Color.FromArgb(255, 214, 0), Obs = "Elenco do Champions 2026.",
                    Jogadores = new[]
                    {
                        P("Derke", "fi", "0;P;o;1;0t;1;0l;4;0o;2;0a;1;0f;0;1t;0;1l;1;1o;0;1a;1;1m;0;1f;0"),
                        P("Jamppi", "fi", "0;s;1;P;h;0;0l;4;0o;0;0a;1;0f;0;1b;0", "800 DPI  •  sens 0.317"),
                        P("Chronicle", "ru", "0;P;h;0;f;0;0t;1;0l;4;0o;0;0a;1;0f;0;1b;0"),
                        P("PROFEK", "pl"),
                        P("Sayonara", "md", "0;s;1;P;o;1;d;1;0b;0;1b;0;S;c;0;s;0.457", "1600 DPI  •  sens 0.1"),
                    }
                },
                new Time
                {
                    Nome = "Paper Rex", Cor = Color.FromArgb(240, 60, 140), Obs = "Elenco do Champions 2026.",
                    Jogadores = new[]
                    {
                        P("f0rsakeN", "id", "0;p;0;c;1;s;1;P;h;0;f;0;s;0;0l;3;0v;3;0g;1;0o;0;0a;1;0f;0;1b;0;A;o;1;d;1;0b;0;1b;0;S;d;0"),
                        P("something", "ru", "0;s;1;P;h;0;0l;2;0o;1;0a;1;0f;0;1b;0;S;c;0;s;0.713;o;1"),
                        P("Jinggg", "sg", "0;s;1;P;c;1;o;1;0t;1;0l;2;0o;2;0a;1;0f;0;1b;0;S;c;5"),
                        P("d4v41", "my", "0;s;1;P;h;0;0l;4;0o;2;0a;1;0f;0;1b;0"),
                        P("invy", "ph", "0;s;1;P;o;1;f;0;0t;1;0l;1;0o;2;0a;0;0f;0;1t;1;1l;1;1v;3;1o;3;1a;0;1m;0;1f;0;S;o;0.8"),
                    }
                },
            }
        },
        new ListaJogo
        {
            Jogo = "Fortnite",
            ComoImportar = "O Fortnite não tem código de mira. Copia a sensibilidade: Configurações → Mouse e teclado → Sensibilidade.",
            Fonte = "gamerhardware.org",
            Conferido = "nov/2026",
            Times = new[]
            {
                new Time
                {
                    Nome = "Pros", Cor = Color.FromArgb(160, 110, 255),
                    Jogadores = new[]
                    {
                        P("Bugha", "us", null, "800 DPI  •  Sens X/Y: 6%  •  Mira: 55%  •  Luneta: 55%"),
                        P("Clix", "us", null, "800 DPI  •  Sens X/Y: 7%  •  Mira: 50%  •  Luneta: 50%"),
                        P("Mongraal", "gb", null, "800 DPI  •  Sens X/Y: 6,5%  •  Mira: 55%  •  Luneta: 50%"),
                        P("Peterbot", "us", null, "Config completa no ProSettings"),
                        P("Cooper", "us", null, "Config completa no ProSettings"),
                        P("Veno", "gb", null, "Config completa no ProSettings"),
                    }
                },
            }
        },
    };
}
