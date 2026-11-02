namespace SextaFeira;

/// <summary>
/// Configs e miras de pros. Os pros trocam de mira com frequência e as fontes às vezes discordam,
/// por isso cada lista mostra de onde veio e quando foi conferida. Atualizado pelo Claude.
/// </summary>
internal static class Pros
{
    public class Pro
    {
        public string Nome;
        public string Time;
        public string Pais;      // emoji da bandeira
        public string Mira;      // código pra importar (null = jogo sem código de mira)
        public string Detalhe;   // sens, DPI etc.
    }

    public class ListaJogo
    {
        public string Jogo;
        public string ComoImportar;
        public string Fonte;
        public string Conferido;
        public Pro[] Lista;
    }

    public static readonly ListaJogo[] Jogos =
    {
        new ListaJogo
        {
            Jogo = "Counter-Strike 2",
            ComoImportar = "No CS2: Configurações → Jogo → Mira → Compartilhar ou importar → cola o código.",
            Fonte = "tradeit.gg e PCGamesN",
            Conferido = "nov/2026",
            Lista = new[]
            {
                new Pro { Nome = "donk", Time = "Team Spirit", Pais = "🇷🇺", Mira = "CSGO-2uZLz-jXPTZ-SUkJx-dmJyv-UxQRO" },
                new Pro { Nome = "ZywOo", Time = "Vitality", Pais = "🇫🇷", Mira = "CSGO-KDOkV-jUpoE-GdhAh-YzbZL-MYXTB" },
                new Pro { Nome = "m0NESY", Time = "Falcons", Pais = "🇷🇺", Mira = "CSGO-JV6Rw-9sfQB-UzokG-SHqBx-sbK5O" },
                new Pro { Nome = "NiKo", Time = "Falcons", Pais = "🇧🇦", Mira = "CSGO-m89VO-6zPDr-53TJ9-OySAJ-V8Y2Q" },
                new Pro { Nome = "sh1ro", Time = "Team Spirit", Pais = "🇷🇺", Mira = "CSGO-bk5KR-52jw7-tKRaw-7kzdw-hOxoO" },
                new Pro { Nome = "ropz", Time = "Vitality", Pais = "🇪🇪", Mira = "CSGO-RLHnF-xbYw5-ZBiB5-MEOKJ-edK5O" },
                new Pro { Nome = "KSCERATO", Time = "FURIA", Pais = "🇧🇷", Mira = "CSGO-uSeWF-F4v3i-yzDJ7-OEThH-CFCEB" },
                new Pro { Nome = "molodoy", Time = "FURIA", Pais = "🇰🇿", Mira = "CSGO-Un9sK-tQVUX-avrUV-WXfxK-pBtdA" },
                new Pro { Nome = "FalleN", Time = "lenda do CS brasileiro", Pais = "🇧🇷", Mira = "CSGO-ssxCh-Y9LFi-B9amu-NWkZo-ObaAB" },
                new Pro { Nome = "Twistzz", Time = "FaZe", Pais = "🇨🇦", Mira = "CSGO-Q3Syz-WOaGL-9WTqW-9N3iJ-e64RG" },
                new Pro { Nome = "b1t", Time = "NAVI", Pais = "🇺🇦", Mira = "CSGO-u2H9q-R3KDb-ijHuY-Bfizr-J9T8N" },
                new Pro { Nome = "torzsi", Time = "MOUZ", Pais = "🇭🇺", Mira = "CSGO-hP4eK-PqkTQ-jo4T7-cmpxc-ZHZML" },
            }
        },
        new ListaJogo
        {
            Jogo = "Valorant",
            ComoImportar = "No Valorant: Configurações → Mira → botão de importar perfil (ícone de seta) → cola o código.",
            Fonte = "PCGamesN",
            Conferido = "nov/2026",
            Lista = new[]
            {
                new Pro { Nome = "Less", Time = "KRÜ", Pais = "🇧🇷", Mira = "0;s;1;P;c;8;u;000000FF;h;0;b;1;f;0;0l;4;0o;0;0a;1;0f;0;1b;0" },
                new Pro { Nome = "mwzera", Time = "KRÜ", Pais = "🇧🇷", Mira = "0;P;c;4;h;0;f;0;0l;2;0o;2;0a;1;0f;0;1b;0" },
                new Pro { Nome = "saadhak", Time = "KRÜ", Pais = "🇦🇷", Mira = "0;P;o;1;d;1;f;0;0b;0;1b;0" },
                new Pro { Nome = "kiNgg", Time = "Leviatán", Pais = "🇨🇱", Mira = "0;P;c;8;u;000000FF;h;0;b;1;0l;4;0o;0;0a;1;0f;0;1b;0" },
                new Pro { Nome = "f0rsakeN", Time = "Paper Rex", Pais = "🇮🇩", Mira = "0;s;1;P;u;000000FF;o;1;s;0;0t;3;0l;1;0v;0;0g;1;0o;0;0a;1;0f;0;1t;1;1l;4;1g;1;1o;0;1a;1;1m;0;1f;0;S;c;0;s;0.9;o;1" },
                new Pro { Nome = "Jinggg", Time = "Paper Rex", Pais = "🇸🇬", Mira = "0;s;1;P;c;1;o;1;0t;1;0l;2;0o;2;0a;1;0f;0;1b;0;S;c;5" },
                new Pro { Nome = "something", Time = "Paper Rex", Pais = "🇷🇺", Mira = "0;P;o;0.619;d;1;f;0;s;0;0t;1;0l;0;0o;2;0a;1;0f;0;1b;0" },
                new Pro { Nome = "Boaster", Time = "Fnatic", Pais = "🇬🇧", Mira = "0;s;1;P;c;1;o;1;d;1;0l;0;0o;2;0a;1;0f;0;1t;0;1l;0;1o;0;1a;0;S;c;1;o;1" },
                new Pro { Nome = "crashies", Time = "Fnatic", Pais = "🇺🇸", Mira = "0;P;h;0;0l;3;0v;3;0g;1;0o;2;0a;1;1b;0" },
                new Pro { Nome = "benjyfishy", Time = "Team Heretics", Pais = "🇬🇧", Mira = "0;P;c;7;h;0;0l;4;0o;0;0a;1;0f;0;1b;0" },
                new Pro { Nome = "stax", Time = "T1", Pais = "🇰🇷", Mira = "0;p;0;P;h;0;f;0;0l;3;0o;2;0a;1;0f;0;1b;0" },
                new Pro { Nome = "shroud", Time = "criador de conteúdo", Pais = "🇨🇦", Mira = "0;P;o;0.506;d;1;z;1;0t;1;0l;4;0o;2;0a;1;0f;0;1b;0" },
            }
        },
        new ListaJogo
        {
            Jogo = "Fortnite",
            ComoImportar = "O Fortnite não tem código de mira. Copia a sensibilidade: Configurações → Mouse e teclado → Sensibilidade.",
            Fonte = "gamerhardware.org",
            Conferido = "nov/2026",
            Lista = new[]
            {
                new Pro { Nome = "Bugha", Pais = "🇺🇸", Time = "", Detalhe = "800 DPI  •  Sens X/Y: 6%  •  Mira: 55%  •  Luneta: 55%" },
                new Pro { Nome = "Clix", Pais = "🇺🇸", Time = "", Detalhe = "800 DPI  •  Sens X/Y: 7%  •  Mira: 50%  •  Luneta: 50%" },
                new Pro { Nome = "Mongraal", Pais = "🇬🇧", Time = "", Detalhe = "800 DPI  •  Sens X/Y: 6,5%  •  Mira: 55%  •  Luneta: 50%" },
            }
        },
    };
}
