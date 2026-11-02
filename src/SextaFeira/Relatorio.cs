using System.Text;

namespace SextaFeira;

/// <summary>Gera um resumo do PC pra colar no chat com o Claude e melhorar o app.</summary>
internal static class Relatorio
{
    public static string Gerar(InfoPC pc, List<Jogos.Jogo> jogos, List<Checagem> checagens)
    {
        var s = new StringBuilder();
        s.AppendLine("=== RELATÓRIO SEXTA-FEIRA ===");
        s.AppendLine($"Versão do app: {Atualizador.VersaoAtual}");
        s.AppendLine($"Data: {DateTime.Now:dd/MM/yyyy HH:mm}");
        s.AppendLine();
        s.AppendLine("[PC]");
        s.AppendLine($"CPU: {pc.Cpu}");
        s.AppendLine($"GPU: {pc.Gpu} (driver {pc.DriverNvidia ?? pc.GpuDriverBruto})");
        s.AppendLine($"RAM: {pc.RamGB} GB, {pc.Pentes} pente(s), {pc.RamMHz} MHz");
        s.AppendLine($"Sistema: {pc.Windows} (build {pc.Build})");
        s.AppendLine();
        s.AppendLine("[Jogos]");
        if (jogos.Count == 0) s.AppendLine("Nenhum encontrado");
        foreach (var j in jogos) s.AppendLine($"{j.Nome}: {j.Disco}");
        s.AppendLine();
        s.AppendLine("[Checagem]");
        foreach (var c in checagens)
        {
            string marca = c.Status switch { Status.Ok => "OK", Status.Alerta => "ATENÇÃO", _ => "INFO" };
            s.AppendLine($"[{marca}] {c.Nome}: {c.Detalhe}");
        }
        s.AppendLine();
        var hist = Historico.Carregar();
        s.AppendLine($"[Uso] Otimizações feitas: {hist.Count}");
        s.AppendLine($"Programas que o app fecha: {string.Join(", ", Configuracao.Atual.ProgramasParaFechar)}");
        s.AppendLine();
        s.AppendLine("Escreve aqui embaixo o que tu sentiu (FPS antes/depois, travadas, o que quer melhorar):");
        return s.ToString();
    }
}
