using Velopack;
using Velopack.Sources;

namespace SextaFeira;

/// <summary>
/// Atualização automática a partir das Releases do repositório (privado, com chave só de leitura).
/// Procura logo ao abrir, baixa em segundo plano e também atualiza em silêncio pelo botão da barra.
/// </summary>
internal static class Atualizador
{
    public const string Repositorio = "https://github.com/gbzeraasz/SEXTA-FEIRA";

    /// <summary>Última mensagem de erro (null se deu certo).</summary>
    public static string UltimoErro { get; private set; }

    private static UpdateManager Criar()
    {
        string token = string.IsNullOrWhiteSpace(Segredos.TokenAtualizacao) ? null : Segredos.TokenAtualizacao;
        return new UpdateManager(new GithubSource(Repositorio, token, false));
    }

    public static void Log(string texto)
    {
        try
        {
            File.AppendAllText(Path.Combine(Sistema.PastaDados, "log.txt"),
                $"[{DateTime.Now:dd/MM HH:mm:ss}] {texto}\r\n");
        }
        catch { }
    }

    public static bool Instalado
    {
        get { try { return Criar().IsInstalled; } catch { return false; } }
    }

    public static string VersaoAtual
    {
        get
        {
            try
            {
                var m = Criar();
                if (m.IsInstalled && m.CurrentVersion != null) return m.CurrentVersion.ToString();
            }
            catch { }
            return "dev";
        }
    }

    /// <summary>Retorna a nova versão se tiver, ou null. Em caso de erro, preenche UltimoErro.</summary>
    public static async Task<UpdateInfo> Verificar()
    {
        try
        {
            var m = Criar();
            if (!m.IsInstalled)
            {
                UltimoErro = "Essa cópia não foi instalada pelo instalador, então não atualiza sozinha.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(Segredos.TokenAtualizacao))
            {
                UltimoErro = "Essa versão foi gerada sem a chave de atualização. Baixa o instalador novo no GitHub.";
                Log(UltimoErro);
                return null;
            }

            var info = await m.CheckForUpdatesAsync();
            UltimoErro = null;
            Log(info == null ? $"Sem atualização (atual {m.CurrentVersion})" : $"Nova versão encontrada: {info.TargetFullRelease.Version}");
            return info;
        }
        catch (Exception ex)
        {
            UltimoErro = "Não consegui falar com o servidor de atualização: " + ex.Message;
            Log("ERRO ao verificar: " + ex);
            return null;
        }
    }

    public static async Task Baixar(UpdateInfo info, Action<int> progresso)
    {
        var m = Criar();
        await m.DownloadUpdatesAsync(info, progress: p => progresso?.Invoke(p));
        Log($"Versão {info.TargetFullRelease.Version} baixada");
    }

    public static void AplicarEReiniciar(UpdateInfo info)
    {
        Log($"Aplicando {info.TargetFullRelease.Version} e reiniciando");
        Criar().ApplyUpdatesAndRestart(info.TargetFullRelease);
    }

    /// <summary>
    /// Usado pelo botão da barra: se tiver versão nova, baixa e deixa pra aplicar
    /// sozinho assim que o processo fechar. Na próxima vez já está atualizado.
    /// </summary>
    public static async Task AtualizarEmSilencio()
    {
        try
        {
            string jogo = Jogos.JogoAberto();
            if (jogo != null) { Log($"Atualização silenciosa adiada: {jogo} aberto (não gastar internet no jogo)"); return; }
            var info = await Verificar();
            if (info == null) return;
            var m = Criar();
            await m.DownloadUpdatesAsync(info);
            m.WaitExitThenApplyUpdates(info.TargetFullRelease, true, false);
            Log($"Atualização silenciosa agendada: {info.TargetFullRelease.Version}");
        }
        catch (Exception ex)
        {
            Log("ERRO na atualização silenciosa: " + ex);
        }
    }
}
