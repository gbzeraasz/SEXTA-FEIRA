using System.Text.Json;

namespace SextaFeira;

/// <summary>Configurações do usuário, salvas fora da pasta do app (sobrevivem às atualizações).</summary>
internal class Configuracao
{
    public List<string> ProgramasParaFechar { get; set; } = Otimizador.ProgramasDeFundo.ToList();
    public bool AtualizarAutomaticamente { get; set; } = true;

    /// <summary>Modo Competitivo: limpa a RAM em espera sozinho durante a partida quando a RAM livre fica baixa.</summary>
    public bool RamContinua { get; set; } = true;

    /// <summary>O que o botão OTIMIZAR / raio da barra faz (ids da aba Limpeza 1 clique). null = padrão.</summary>
    public List<string> UmClique { get; set; }

    public bool Marcado(string id) =>
        UmClique?.Contains(id) ?? (Limpeza.Pegar(id)?.Padrao ?? false);

    public void Marcar(string id, bool marcado)
    {
        UmClique ??= Limpeza.Itens.Where(i => i.Padrao).Select(i => i.Id).ToList();
        UmClique.Remove(id);
        if (marcado) UmClique.Add(id);
        Salvar();
    }

    private static string Arquivo => Path.Combine(Sistema.PastaDados, "config.json");
    private static Configuracao atual;

    public static Configuracao Atual => atual ??= Carregar();

    private static Configuracao Carregar()
    {
        try
        {
            if (File.Exists(Arquivo))
                return JsonSerializer.Deserialize<Configuracao>(File.ReadAllText(Arquivo)) ?? new Configuracao();
        }
        catch { }
        return new Configuracao();
    }

    public void Salvar()
    {
        try { File.WriteAllText(Arquivo, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }
}

/// <summary>Guarda as últimas otimizações pra mostrar no painel.</summary>
internal static class Historico
{
    public class Entrada
    {
        public DateTime Quando { get; set; }
        public double Disco { get; set; }
        public double RamLiberada { get; set; }
    }

    private static string Arquivo => Path.Combine(Sistema.PastaDados, "historico.json");

    public static List<Entrada> Carregar()
    {
        try
        {
            if (File.Exists(Arquivo))
                return JsonSerializer.Deserialize<List<Entrada>>(File.ReadAllText(Arquivo)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Registrar(Otimizador.Resultado r)
    {
        try
        {
            var lista = Carregar();
            lista.Add(new Entrada
            {
                Quando = DateTime.Now,
                Disco = r.DiscoLiberado,
                RamLiberada = r.RamLiberada
            });
            if (lista.Count > 50) lista = lista.Skip(lista.Count - 50).ToList();
            File.WriteAllText(Arquivo, JsonSerializer.Serialize(lista));
        }
        catch { }
    }
}
