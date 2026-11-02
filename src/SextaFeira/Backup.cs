using System.Text.Json;
using Microsoft.Win32;

namespace SextaFeira;

/// <summary>
/// Guarda o valor ORIGINAL de tudo que a SEXTA-FEIRA muda, pra dar pra desfazer.
/// Só guarda a primeira vez: se otimizar 10 vezes, o "desfazer" volta pro estado de antes da 1ª.
/// </summary>
internal static class Backup
{
    public class ItemReg
    {
        public string Caminho { get; set; }
        public string Nome { get; set; }
        public bool Existia { get; set; }
        public string Tipo { get; set; }
        public string Valor { get; set; }
    }

    public class Dados
    {
        public List<ItemReg> Registro { get; set; } = new();
        public string PlanoEnergia { get; set; }
        public Dictionary<string, string> Arquivos { get; set; } = new();
        public DateTime Criado { get; set; } = DateTime.Now;
    }

    private static readonly object trava = new();
    private static string ArquivoJson => Path.Combine(Sistema.PastaDados, "backup.json");
    private static string PastaArquivos => Path.Combine(Sistema.PastaDados, "arquivos");

    public static Dados Carregar()
    {
        try
        {
            if (File.Exists(ArquivoJson))
                return JsonSerializer.Deserialize<Dados>(File.ReadAllText(ArquivoJson)) ?? new Dados();
        }
        catch { }
        return new Dados();
    }

    private static void Salvar(Dados d) =>
        File.WriteAllText(ArquivoJson, JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));

    public static bool TemBackup()
    {
        var d = Carregar();
        return d.Registro.Count > 0 || d.PlanoEnergia != null || d.Arquivos.Count > 0;
    }

    public static void GuardarReg(string caminho, string nome)
    {
        lock (trava)
        {
            var d = Carregar();
            if (d.Registro.Any(r => r.Caminho == caminho && r.Nome == nome)) return;

            var item = new ItemReg { Caminho = caminho, Nome = nome };
            try
            {
                var raiz = Sistema.Raiz(caminho, out string sub);
                using var k = raiz.OpenSubKey(sub);
                object v = k?.GetValue(nome);
                if (v != null)
                {
                    item.Existia = true;
                    item.Tipo = k.GetValueKind(nome).ToString();
                    item.Valor = v.ToString();
                }
            }
            catch { }
            d.Registro.Add(item);
            Salvar(d);
        }
    }

    public static void GuardarPlano(string guid)
    {
        lock (trava)
        {
            var d = Carregar();
            if (d.PlanoEnergia != null) return;
            d.PlanoEnergia = guid;
            Salvar(d);
        }
    }

    public static void GuardarArquivo(string caminho)
    {
        lock (trava)
        {
            var d = Carregar();
            if (d.Arquivos.ContainsKey(caminho) || !File.Exists(caminho)) return;
            Directory.CreateDirectory(PastaArquivos);
            string destino = Path.Combine(PastaArquivos, Guid.NewGuid().ToString("N") + ".bak");
            File.Copy(caminho, destino, true);
            d.Arquivos[caminho] = destino;
            Salvar(d);
        }
    }

    /// <summary>Volta tudo como era antes. Retorna quantos itens foram restaurados.</summary>
    public static int Restaurar()
    {
        lock (trava)
        {
            var d = Carregar();
            int n = 0;

            foreach (var r in d.Registro)
            {
                try
                {
                    var raiz = Sistema.Raiz(r.Caminho, out string sub);
                    using var k = raiz.CreateSubKey(sub, true);
                    if (!r.Existia)
                    {
                        k.DeleteValue(r.Nome, false);
                    }
                    else
                    {
                        var tipo = Enum.Parse<RegistryValueKind>(r.Tipo);
                        object valor = tipo switch
                        {
                            RegistryValueKind.DWord => int.Parse(r.Valor),
                            RegistryValueKind.QWord => long.Parse(r.Valor),
                            _ => r.Valor
                        };
                        k.SetValue(r.Nome, valor, tipo);
                    }
                    n++;
                }
                catch { }
            }

            if (d.PlanoEnergia != null)
            {
                Sistema.Rodar("powercfg", "/setactive " + d.PlanoEnergia);
                n++;
            }

            foreach (var a in d.Arquivos)
            {
                try { File.Copy(a.Value, a.Key, true); n++; } catch { }
            }

            try { File.Delete(ArquivoJson); } catch { }
            try { if (Directory.Exists(PastaArquivos)) Directory.Delete(PastaArquivos, true); } catch { }
            return n;
        }
    }
}
