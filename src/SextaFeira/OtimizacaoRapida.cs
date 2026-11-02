namespace SextaFeira;

/// <summary>Modo do botão da barra: otimiza em silêncio e mostra só o aviso no canto da tela.</summary>
internal static class OtimizacaoRapida
{
    public static void Executar()
    {
        Atualizador.Log("Botão da barra: otimização iniciada");

        Otimizador.Resultado r;
        try { r = Otimizador.Executar(); }
        catch (Exception ex)
        {
            Atualizador.Log("ERRO na otimização rápida: " + ex);
            AvisoPopup.Mostrar("Algo deu errado", ex.Message, erro: true);
            return;
        }
        Atualizador.Log("Botão da barra: " + r.Frase());

        // Enquanto mostra o aviso, já procura/baixa atualização em silêncio.
        // Ela é aplicada sozinha quando este processo fechar.
        var atualizacao = Task.Run(() => Atualizador.AtualizarEmSilencio());

        AvisoPopup.Mostrar("⚡ PC otimizado! Bom jogo", r.Frase());

        try { atualizacao.Wait(TimeSpan.FromSeconds(120)); } catch { }
    }
}
