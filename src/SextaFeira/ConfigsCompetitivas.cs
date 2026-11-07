namespace SextaFeira;

/// <summary>Nível da placa de vídeo, pra ajustar resolução e o que dá pra subir.</summary>
internal enum NivelPC { Entrada, Medio, Alto }

/// <summary>
/// Configs competitivas por jogo: sempre FPS alto, baixa latência e visibilidade.
/// Sem upscaler e sem geração de quadros (aumentam a latência). Atualizado pelo Claude.
/// </summary>
internal class ConfigJogo
{
    public string Nome;
    public string[] SteamIds = Array.Empty<string>();
    public string[] Palavras = Array.Empty<string>();   // pra achar pelo nome em outros launchers
    public string[] Processos = Array.Empty<string>();
    public string[] Jogo = Array.Empty<string>();
    public string[] Pc = Array.Empty<string>();
    public Dictionary<NivelPC, string> PorNivel = new();
    public string Aviso;
    public bool PerfilAutomatico;   // o app aplica sozinho (CS2, Valorant e Fortnite)
}

internal static class ConfigsCompetitivas
{
    public static NivelPC Nivel(string gpu)
    {
        string g = (gpu ?? "").ToUpperInvariant();
        int Num(string prefixo)
        {
            int i = g.IndexOf(prefixo, StringComparison.Ordinal);
            if (i < 0) return -1;
            var digitos = new string(g[(i + prefixo.Length)..].SkipWhile(c => c == ' ').TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digitos, out int n) ? n : -1;
        }

        int rtx = Num("RTX");
        if (rtx > 0)
        {
            int serie = rtx / 1000 * 1000, modelo = rtx % 100;
            if (rtx >= 5000) return modelo >= 70 ? NivelPC.Alto : NivelPC.Medio;
            if (serie >= 3000 && modelo >= 70) return NivelPC.Alto;
            return NivelPC.Medio;
        }
        int gtx = Num("GTX");
        if (gtx > 0) return gtx is 1070 or 1080 ? NivelPC.Medio : NivelPC.Entrada;

        int rx = Num("RX");
        if (rx > 0)
        {
            if (rx >= 9000) return rx % 100 >= 70 ? NivelPC.Alto : NivelPC.Medio;
            if (rx >= 7700 || (rx >= 6800 && rx < 7000)) return NivelPC.Alto;
            if (rx >= 5600) return NivelPC.Medio;
            return NivelPC.Entrada;
        }
        int arc = Num("ARC A");
        if (arc > 0) return arc >= 750 ? NivelPC.Medio : NivelPC.Entrada;
        if (g.Contains("ARC B")) return NivelPC.Medio;

        // Integrada (Intel UHD/Iris, Radeon Graphics/Vega) ou desconhecida
        return NivelPC.Entrada;
    }

    public static string NomeNivel(NivelPC n) => n switch
    {
        NivelPC.Entrada => "Entrada",
        NivelPC.Medio => "Intermediário",
        _ => "Alto"
    };

    public static readonly string[] DicasGerais =
    {
        "Modo de tela: Tela cheia (menos atraso que janela sem bordas)",
        "V-Sync: desligado  •  NVIDIA Reflex ou AMD Anti-Lag: ligado, se o jogo tiver",
        "Upscaler (DLSS/FSR/XeSS) e geração de quadros: DESLIGADOS (aumentam o atraso)",
        "Sombras, efeitos, pós-processamento, oclusão de ambiente: Baixo/Desligado",
        "Desfoque de movimento, profundidade de campo, aberração cromática, granulação: Desligados",
        "Texturas: Média se tiver 4 GB de memória de vídeo; Alta se tiver 8 GB ou mais",
        "Limite de FPS: um pouco acima da taxa do monitor, pra estabilizar o tempo de quadro",
    };

    public static readonly ConfigJogo[] Todos =
    {
        new ConfigJogo
        {
            Nome = "Counter-Strike 2",
            PerfilAutomatico = true,
            SteamIds = new[] { "730" },
            Palavras = new[] { "counter-strike" },
            Processos = new[] { "cs2" },
            Jogo = new[]
            {
                "Modo de exibição: Tela cheia",
                "Qualidade das sombras globais: Baixa (Alta só se sobrar FPS: ajuda a ver sombras de inimigos)",
                "Detalhe de modelo e textura: Baixo  •  Filtragem de textura: Bilinear",
                "Detalhe de shader: Baixo  •  Detalhe de partículas: Baixo",
                "Oclusão de ambiente: Desligada  •  HDR: Desempenho",
                "FidelityFX Super Resolution: Desligado",
                "NVIDIA Reflex: Ativado + Boost  •  Esperar pela sincronização vertical: Desligado",
                "FPS máximo no jogo: 0 (sem limite) ou um pouco acima do teu monitor",
            },
            Pc = new[]
            {
                "Opções de inicialização: NÃO use -high nem -threads (atrapalham)",
                "Shader cache: deixa as primeiras partidas depois de atualizar driver rodarem (travam um pouco e depois normaliza)",
            },
            PorNivel =
            {
                [NivelPC.Entrada] = "Resolução: 1280x960 esticado (4:3) ou 1024x768  •  Anti-serrilhado: Nenhum",
                [NivelPC.Medio] = "Resolução: 1280x960 esticado ou 1920x1080  •  Anti-serrilhado: CMAA2 ou MSAA 2x",
                [NivelPC.Alto] = "Resolução: nativa ou 1280x960 esticado (gosto pessoal)  •  Anti-serrilhado: MSAA 4x",
            }
        },
        new ConfigJogo
        {
            Nome = "Valorant",
            PerfilAutomatico = true,
            Palavras = new[] { "valorant" },
            Processos = new[] { "VALORANT-Win64-Shipping" },
            Jogo = new[]
            {
                "Modo de exibição: Tela cheia",
                "Renderização multithread: LIGADA",
                "Qualidade de material, textura, detalhes e interface: Baixa",
                "Vinheta: Desligada  •  Sincronização vertical: Desligada",
                "Filtragem anisotrópica: 4x (quase não pesa)",
                "Bloom, distorção e sombras projetadas: Desligados",
                "NVIDIA Reflex: Ativado + Boost",
                "Limitar FPS em segundo plano e nos menus: 60",
            },
            Pc = new[]
            {
                "Não use 'boosters' que mexem nos arquivos do jogo: o Vanguard pune",
            },
            PorNivel =
            {
                [NivelPC.Entrada] = "Resolução: 1600x900 se faltar FPS (1920x1080 se segurar)  •  Anti-serrilhado: Nenhum  •  Melhorar clareza: Desligado",
                [NivelPC.Medio] = "Resolução: 1920x1080  •  Anti-serrilhado: MSAA 2x  •  Melhorar clareza: Desligado",
                [NivelPC.Alto] = "Resolução: nativa  •  Anti-serrilhado: MSAA 4x  •  Melhorar clareza: Ligado (se gostar)",
            }
        },
        new ConfigJogo
        {
            Nome = "Fortnite",
            Palavras = new[] { "fortnite" },
            Processos = new[] { "FortniteClient-Win64-Shipping" },
            PerfilAutomatico = true,
            Jogo = new[]
            {
                "Modo de renderização: DESEMPENHO (o maior ganho de FPS do jogo; a maioria dos pros usa)",
                "Distância de visão: Épica (ver inimigos de longe)",
                "Malhas: Baixo  •  Efeitos: Baixo  •  Pós-processamento: Baixo",
                "Sombras: Desligadas  •  Anti-serrilhado: Desligado",
                "NVIDIA Reflex: Ativado + Boost  •  V-Sync: Desligado",
                "Limite de FPS: o do teu monitor (ou um pouco acima)",
                "Mostrar FPS: Ligado",
            },
            Pc = new[] { "A SEXTA-FEIRA aplica o Modo Desempenho sozinha (botão abaixo, com o jogo fechado)" },
            PorNivel =
            {
                [NivelPC.Entrada] = "Resolução 3D: 85-100% (abaixa só se faltar FPS)  •  Texturas: Baixo",
                [NivelPC.Medio] = "Resolução 3D: 100%  •  Texturas: Médio",
                [NivelPC.Alto] = "Resolução 3D: 100%  •  Texturas: Alto",
            }
        },
        new ConfigJogo
        {
            Nome = "Apex Legends",
            SteamIds = new[] { "1172470" },
            Palavras = new[] { "apex legends" },
            Processos = new[] { "r5apex", "r5apex_dx12" },
            Jogo = new[]
            {
                "Modo de tela: Tela cheia  •  V-Sync: Desligado",
                "Campo de visão: 104 a 110",
                "Resolução adaptativa: Desligada (alvo de FPS = 0)",
                "Filtragem de textura: Bilinear  •  Oclusão de ambiente: Desligada",
                "Sombras do sol: Baixo  •  Sombras pontuais: Desligadas  •  Luz volumétrica: Desligada",
                "Detalhe de modelo: Baixo  •  Detalhe de efeitos: Baixo",
                "Marcas de impacto e Ragdolls: Baixo/Desligado",
                "NVIDIA Reflex: Ativado (teste o + Boost)",
            },
            PorNivel =
            {
                [NivelPC.Entrada] = "Resolução: 1920x1080 (ou 1600x900 se faltar)  •  Orçamento de texturas: Baixo  •  Anti-serrilhado: Nenhum",
                [NivelPC.Medio] = "Resolução: 1920x1080  •  Orçamento de texturas: Médio  •  Anti-serrilhado: Nenhum",
                [NivelPC.Alto] = "Resolução: nativa  •  Orçamento de texturas: Médio  •  Anti-serrilhado: Nenhum ou TSAA",
            }
        },
        new ConfigJogo
        {
            Nome = "Rainbow Six Siege X",
            SteamIds = new[] { "359550" },
            Palavras = new[] { "rainbow six" },
            Processos = new[] { "RainbowSix", "RainbowSix_Vulkan", "RainbowSix_DX11" },
            Jogo = new[]
            {
                "Modo de exibição: Tela cheia  •  V-Sync: Desligado  •  NVIDIA Reflex: Ativado + Boost",
                "Campo de visão: 85 a 95",
                "Oclusão de ambiente: Desligada  •  Efeitos de lente: Desligados",
                "Profundidade de campo ao mirar: Desligada  •  Tremor de tela: Desligado (Acessibilidade)",
                "Anti-serrilhado: Desligado ou TAA",
                "Teste Vulkan x DirectX: em muitas placas NVIDIA o Vulkan dá mais FPS",
            },
            PorNivel =
            {
                [NivelPC.Entrada] = "Escala de renderização: 85-100%  •  Texturas: Baixo  •  Sombras: Média (mostram inimigos; Baixa se faltar FPS)",
                [NivelPC.Medio] = "Escala de renderização: 100%  •  Texturas: Médio  •  Sombras: Alta (vantagem: aparecem as sombras dos inimigos)",
                [NivelPC.Alto] = "Escala de renderização: 100%  •  Texturas: Médio/Alto  •  Sombras: Alta",
            }
        },
        new ConfigJogo
        {
            Nome = "Overwatch 2",
            SteamIds = new[] { "2357570" },
            Palavras = new[] { "overwatch" },
            Processos = new[] { "Overwatch" },
            Jogo = new[]
            {
                "Modo de exibição: Tela cheia  •  Escala de renderização dinâmica: Desligada",
                "Taxa de quadros: personalizada, no máximo do teu monitor (ou acima)",
                "V-Sync e Buffer triplo: Desligados  •  Reduzir buffer: Ligado",
                "NVIDIA Reflex: Ativado + Boost",
                "Sombras, efeitos, névoa local, iluminação e refração: Baixo",
                "Reflexos dinâmicos: Desligados  •  Oclusão de ambiente: Desligada",
                "Campo de visão: 103 (o máximo)",
            },
            PorNivel =
            {
                [NivelPC.Entrada] = "Escala de renderização: 75-100%  •  Texturas: Média  •  Detalhe de modelo: Baixo  •  Anti-serrilhado: Baixo",
                [NivelPC.Medio] = "Escala de renderização: 100%  •  Texturas: Média  •  Detalhe de modelo: Médio",
                [NivelPC.Alto] = "Escala de renderização: 100%  •  Texturas: Média  •  Detalhe de modelo: Ultra (ajuda a ver contornos)",
            }
        },
        new ConfigJogo
        {
            Nome = "Marvel Rivals",
            SteamIds = new[] { "2767030" },
            Palavras = new[] { "marvel rivals" },
            Processos = new[] { "Marvel-Win64-Shipping" },
            Jogo = new[]
            {
                "Modo de exibição: Tela cheia  •  V-Sync: Desligado  •  NVIDIA Reflex: Ativado + Boost",
                "Geração de quadros: DESLIGADA (aumenta muito o atraso)",
                "Iluminação global, reflexos, pós-processamento, sombras e folhagem: Baixo",
                "Detalhe de modelo: Baixo  •  Efeitos: Baixo",
                "Desfoque de movimento: Desligado",
            },
            Aviso = "É um dos jogos mais pesados da lista: em placas de entrada, conta com FPS menor que nos outros.",
            PorNivel =
            {
                [NivelPC.Entrada] = "Resolução nativa sem upscaler, tudo no Baixo, texturas Baixo. Se ficar abaixo de 60 FPS, use FSR em Qualidade como último recurso",
                [NivelPC.Medio] = "Tudo Baixo, texturas Médio, sem upscaler",
                [NivelPC.Alto] = "Tudo Baixo, texturas Alto, sem upscaler",
            }
        },
        new ConfigJogo
        {
            Nome = "Rocket League",
            SteamIds = new[] { "252950" },
            Palavras = new[] { "rocket league" },
            Processos = new[] { "RocketLeague" },
            Jogo = new[]
            {
                "Modo de janela: Tela cheia  •  V-Sync: Desligado",
                "Limite de FPS: o do teu monitor (máximo 250)",
                "Qualidade de renderização: Alto desempenho  •  Detalhe de renderização: Desempenho",
                "Detalhe do mundo e de partículas: Desempenho  •  Intensidade de efeitos: Baixa",
                "Desliga: oclusão de ambiente, profundidade de campo, bloom, raios de luz, reflexos de lente, desfoque de movimento, sombras dinâmicas, efeitos de clima",
                "Traves transparentes: Ligado",
            },
            PorNivel =
            {
                [NivelPC.Entrada] = "Detalhe de textura: Desempenho",
                [NivelPC.Medio] = "Detalhe de textura: Alta qualidade",
                [NivelPC.Alto] = "Detalhe de textura: Alta qualidade",
            }
        },
        new ConfigJogo
        {
            Nome = "League of Legends",
            Palavras = new[] { "league of legends" },
            Processos = new[] { "League of Legends" },
            Jogo = new[]
            {
                "Modo de janela: Tela cheia",
                "Qualidade de personagens, ambiente e efeitos: Baixa",
                "Qualidade de sombras: Desligada",
                "Limite de FPS: Sem limite ou acima do monitor  •  Sincronização vertical: Desligada",
                "Anti-serrilhamento: Desligado",
            },
            PorNivel =
            {
                [NivelPC.Entrada] = "Resolução nativa  •  tudo Baixo",
                [NivelPC.Medio] = "Resolução nativa  •  efeitos Médio se quiser",
                [NivelPC.Alto] = "Resolução nativa",
            }
        },
        new ConfigJogo
        {
            Nome = "PUBG: Battlegrounds",
            SteamIds = new[] { "578080" },
            Palavras = new[] { "pubg" },
            Processos = new[] { "TslGame" },
            Jogo = new[]
            {
                "Modo de exibição: Tela cheia  •  V-Sync: Desligado",
                "Escala de tela: 100",
                "Pós-processamento, sombras, efeitos, folhagem: Muito baixo",
                "Distância de visão: Média  •  Desfoque de movimento: Desligado",
            },
            PorNivel =
            {
                [NivelPC.Entrada] = "Anti-serrilhado: Médio  •  Texturas: Baixo",
                [NivelPC.Medio] = "Anti-serrilhado: Ultra (deixa os inimigos mais nítidos)  •  Texturas: Médio",
                [NivelPC.Alto] = "Anti-serrilhado: Ultra  •  Texturas: Médio/Alto",
            }
        },
        new ConfigJogo
        {
            Nome = "Call of Duty",
            SteamIds = new[] { "1938090" },
            Palavras = new[] { "call of duty" },
            Processos = new[] { "cod" },
            Jogo = new[]
            {
                "Modo de exibição: Tela cheia exclusiva  •  V-Sync: Desligado  •  NVIDIA Reflex: Ativado + Boost",
                "Escala de resolução: 100%  •  Upscaling: Desligado (só nitidez FidelityFX CAS se quiser)",
                "Streaming de texturas sob demanda: Desligado",
                "Sombras, oclusão de ambiente, reflexos, partículas: Baixo/Desligado",
                "Desfoque de movimento, profundidade de campo, granulação: Desligados",
                "Campo de visão: 100 a 120",
            },
            Aviso = "Muito pesado: placas com 4 GB de vídeo sofrem nos últimos CoDs.",
            PorNivel =
            {
                [NivelPC.Entrada] = "Texturas: Muito baixo  •  conta com FPS limitado",
                [NivelPC.Medio] = "Texturas: Normal",
                [NivelPC.Alto] = "Texturas: Normal/Alto",
            }
        },
        new ConfigJogo
        {
            Nome = "Dota 2",
            SteamIds = new[] { "570" },
            Processos = new[] { "dota2" },
            Jogo = new[]
            {
                "Modo de exibição: Tela cheia  •  V-Sync: Desligado",
                "Qualidade: personalizada, tudo baixo/desligado exceto 'Texturas' (Médio)",
                "Limite de FPS: acima do monitor",
                "API: teste Vulkan e DirectX 11 (fica com a que der mais FPS)",
            },
            PorNivel =
            {
                [NivelPC.Entrada] = "Qualidade de renderização: 80-100%",
                [NivelPC.Medio] = "Qualidade de renderização: 100%",
                [NivelPC.Alto] = "Qualidade de renderização: 100%",
            }
        },
    };

    public static ConfigJogo Procurar(string nome, string steamId)
    {
        if (steamId != null)
        {
            var porId = Todos.FirstOrDefault(c => c.SteamIds.Contains(steamId));
            if (porId != null) return porId;
        }
        string n = (nome ?? "").ToLowerInvariant();
        return Todos.FirstOrDefault(c => c.Palavras.Any(p => n.Contains(p)));
    }

    public static IEnumerable<string> ProcessosDeJogos() => Todos.SelectMany(c => c.Processos);
}
