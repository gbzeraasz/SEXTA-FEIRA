namespace SextaFeira;

/// <summary>Modo do botão da barra: otimiza e mostra o aviso animado no canto da tela.</summary>
internal static class OtimizacaoRapida
{
    public static void Executar()
    {
        Atualizador.Log("Botão da barra: otimização iniciada");

        // Versão nova já baixada? Instala antes e roda o botão de novo, já atualizado.
        if (Atualizador.AplicarPendenteERodar(new[] { Program.ArgOtimizar })) return;

        // O aviso aparece na hora e acompanha a otimização ao vivo
        var r = AvisoPopup.OtimizarComProgresso();
        if (r != null) Atualizador.Log("Botão da barra: " + r.Frase());

        // Depois, procura/baixa atualização em silêncio (não roda com jogo aberto).
        // Ela é aplicada sozinha quando este processo fechar.
        try { Task.Run(() => Atualizador.AtualizarEmSilencio()).Wait(TimeSpan.FromSeconds(120)); } catch { }
    }
}
