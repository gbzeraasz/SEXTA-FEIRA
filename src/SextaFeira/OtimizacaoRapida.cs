namespace SextaFeira;

/// <summary>Modo do botão da barra: otimiza em silêncio e mostra só a notificação.</summary>
internal static class OtimizacaoRapida
{
    public static void Executar()
    {
        Otimizador.Resultado r;
        try { r = Otimizador.Executar(); }
        catch (Exception ex)
        {
            Mostrar("SEXTA-FEIRA", "Algo deu errado: " + ex.Message, ToolTipIcon.Error);
            return;
        }
        Mostrar("PC otimizado ✅ Bom jogo!", r.Resumo(), ToolTipIcon.Info);
    }

    private static void Mostrar(string titulo, string texto, ToolTipIcon icone)
    {
        using var aviso = new NotifyIcon
        {
            Icon = Tema.Icone,
            Visible = true,
            Text = "SEXTA-FEIRA",
            BalloonTipTitle = titulo,
            BalloonTipText = texto,
            BalloonTipIcon = icone
        };
        aviso.ShowBalloonTip(6000);

        // Mantém o processo vivo um pouco pra notificação aparecer
        var fim = DateTime.Now.AddSeconds(7);
        while (DateTime.Now < fim)
        {
            Application.DoEvents();
            Thread.Sleep(100);
        }
        aviso.Visible = false;
    }
}
