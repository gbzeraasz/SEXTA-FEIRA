using Velopack;
using Velopack.Sources;

namespace SextaFeira;

/// <summary>Atualização automática a partir das Releases do repositório (privado, com chave só de leitura).</summary>
internal static class Atualizador
{
    public const string Repositorio = "https://github.com/gbzeraasz/SEXTA-FEIRA";

    private static UpdateManager Criar()
    {
        string token = string.IsNullOrWhiteSpace(Segredos.TokenAtualizacao) ? null : Segredos.TokenAtualizacao;
        return new UpdateManager(new GithubSource(Repositorio, token, false));
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

    /// <summary>Retorna a nova versão se tiver, ou null.</summary>
    public static async Task<UpdateInfo> Verificar()
    {
        try
        {
            var m = Criar();
            if (!m.IsInstalled) return null;
            return await m.CheckForUpdatesAsync();
        }
        catch { return null; }
    }

    public static async Task BaixarEInstalar(UpdateInfo info, Action<int> progresso)
    {
        var m = Criar();
        await m.DownloadUpdatesAsync(info, progress: p => progresso?.Invoke(p));
        m.ApplyUpdatesAndRestart(info.TargetFullRelease);
    }
}
