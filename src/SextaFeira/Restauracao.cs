using System.Management;

namespace SextaFeira;

/// <summary>Pontos de restauração do Windows: listar, criar e restaurar.</summary>
internal static class Restauracao
{
    public class Ponto
    {
        public uint Numero;
        public string Descricao;
        public DateTime Data;
        public string Tipo;
    }

    private const string ChaveSR = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";

    public static List<Ponto> Listar()
    {
        var lista = new List<Ponto>();
        foreach (var o in Sistema.Wmi("SELECT SequenceNumber, Description, CreationTime, RestorePointType FROM SystemRestore", @"root\default"))
        {
            var p = new Ponto
            {
                Descricao = Sistema.WmiTexto(o, "Description"),
                Tipo = Sistema.WmiTexto(o, "RestorePointType") switch
                {
                    "0" => "Instalação de programa",
                    "1" => "Remoção de programa",
                    "10" => "Instalação de driver",
                    "12" => "Mudança de configuração",
                    "13" => "Cancelado",
                    _ => "Ponto de restauração"
                }
            };
            uint.TryParse(Sistema.WmiTexto(o, "SequenceNumber"), out p.Numero);
            try { p.Data = ManagementDateTimeConverter.ToDateTime(Sistema.WmiTexto(o, "CreationTime")); } catch { }
            lista.Add(p);
        }
        return lista.OrderByDescending(p => p.Data).ToList();
    }

    /// <summary>Cria um ponto. Liga a proteção do disco C se precisar. Pode demorar um pouco.</summary>
    public static (bool ok, string msg) Criar(string descricao)
    {
        try
        {
            using var classe = new ManagementClass(@"root\default", "SystemRestore", null);

            // Garante a proteção do sistema ligada no C:
            try { classe.InvokeMethod("Enable", new object[] { Path.GetPathRoot(Environment.SystemDirectory) }); } catch { }

            // O Windows só deixa criar 1 ponto a cada 24h. Libera só durante a criação e volta como estava.
            int? freqAntes = Sistema.LerRegInt(ChaveSR, "SystemRestorePointCreationFrequency");
            SetarFreq(0);
            object ret;
            try
            {
                // 12 = mudança de configuração, 100 = início de mudança no sistema
                ret = classe.InvokeMethod("CreateRestorePoint", new object[] { descricao, 12u, 100u });
            }
            finally
            {
                if (freqAntes == null) ApagarFreq(); else SetarFreq(freqAntes.Value);
            }

            uint codigo = Convert.ToUInt32(ret);
            if (codigo == 0) return (true, "Ponto de restauração criado! ✅");
            if (codigo == 1058) return (false, "A proteção do sistema está desligada e não consegui ligar.");
            return (false, $"O Windows recusou criar o ponto (código {codigo}).");
        }
        catch (Exception ex)
        {
            return (false, "Não consegui criar: " + ex.Message);
        }
    }

    /// <summary>Agenda a restauração. O PC precisa reiniciar pra aplicar.</summary>
    public static (bool ok, string msg) Restaurar(uint numero)
    {
        try
        {
            using var classe = new ManagementClass(@"root\default", "SystemRestore", null);
            uint codigo = Convert.ToUInt32(classe.InvokeMethod("Restore", new object[] { numero }));
            return codigo == 0
                ? (true, "Restauração agendada.")
                : (false, $"O Windows recusou restaurar (código {codigo}).");
        }
        catch (Exception ex)
        {
            return (false, "Não consegui restaurar: " + ex.Message);
        }
    }

    // Grava direto, sem passar pelo backup do app (é temporário).
    private static void SetarFreq(int v)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", true);
            k.SetValue("SystemRestorePointCreationFrequency", v, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch { }
    }

    private static void ApagarFreq()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", true);
            k?.DeleteValue("SystemRestorePointCreationFrequency", false);
        }
        catch { }
    }
}
