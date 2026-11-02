using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using Microsoft.Win32;

namespace SextaFeira;

/// <summary>Funções de baixo nível: admin, registro, comandos, WMI, discos.</summary>
internal static class Sistema
{
    public static bool EhAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static string PastaDados
    {
        get
        {
            // Fora da pasta do app, porque o Velopack apaga a pasta "current" a cada atualização.
            string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SEXTA-FEIRA-dados");
            Directory.CreateDirectory(p);
            return p;
        }
    }

    // ---------------- Registro ----------------

    public static RegistryKey Raiz(string caminho, out string sub)
    {
        int i = caminho.IndexOf('\\');
        string raiz = caminho[..i].ToUpperInvariant();
        sub = caminho[(i + 1)..];
        return raiz switch
        {
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            _ => throw new ArgumentException("Raiz de registro desconhecida: " + raiz)
        };
    }

    public static object LerReg(string caminho, string nome)
    {
        try
        {
            var raiz = Raiz(caminho, out string sub);
            using var k = raiz.OpenSubKey(sub);
            return k?.GetValue(nome);
        }
        catch { return null; }
    }

    public static int? LerRegInt(string caminho, string nome)
    {
        object v = LerReg(caminho, nome);
        if (v is int i) return i;
        if (v != null && int.TryParse(v.ToString(), out int j)) return j;
        return null;
    }

    /// <summary>Grava no registro guardando o valor original no backup antes.</summary>
    public static void GravarReg(string caminho, string nome, object valor, RegistryValueKind tipo)
    {
        Backup.GuardarReg(caminho, nome);
        var raiz = Raiz(caminho, out string sub);
        using var k = raiz.CreateSubKey(sub, true);
        k.SetValue(nome, valor, tipo);
    }

    public static void GravarDword(string caminho, string nome, int valor) =>
        GravarReg(caminho, nome, valor, RegistryValueKind.DWord);

    // ---------------- Comandos ----------------

    public static string Rodar(string exe, string argumentos, int timeoutMs = 30000)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, argumentos)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            string saida = p.StandardOutput.ReadToEnd();
            p.WaitForExit(timeoutMs);
            return saida;
        }
        catch { return ""; }
    }

    public static void Abrir(string alvo)
    {
        try { Process.Start(new ProcessStartInfo(alvo) { UseShellExecute = true }); } catch { }
    }

    // ---------------- WMI ----------------

    public static List<ManagementBaseObject> Wmi(string consulta, string escopo = @"root\cimv2")
    {
        var lista = new List<ManagementBaseObject>();
        try
        {
            using var s = new ManagementObjectSearcher(escopo, consulta);
            foreach (var o in s.Get()) lista.Add(o);
        }
        catch { }
        return lista;
    }

    public static string WmiTexto(ManagementBaseObject o, string prop)
    {
        try { return o[prop]?.ToString()?.Trim() ?? ""; } catch { return ""; }
    }

    // ---------------- Discos ----------------

    /// <summary>"SSD", "HDD" ou "?" para a unidade da letra informada.</summary>
    public static string TipoDisco(char letra)
    {
        try
        {
            const string esc = @"root\Microsoft\Windows\Storage";
            var part = Wmi($"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{char.ToUpperInvariant(letra)}'", esc).FirstOrDefault();
            if (part == null) return "?";
            string num = WmiTexto(part, "DiskNumber");
            var disco = Wmi($"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId = '{num}'", esc).FirstOrDefault();
            if (disco == null) return "?";
            return WmiTexto(disco, "MediaType") switch { "3" => "HDD", "4" => "SSD", _ => "?" };
        }
        catch { return "?"; }
    }

    public static string Formatar(double bytes)
    {
        if (bytes >= 1024d * 1024 * 1024) return $"{bytes / (1024d * 1024 * 1024):N2} GB";
        if (bytes >= 1024d * 1024) return $"{bytes / (1024d * 1024):N0} MB";
        return $"{bytes / 1024d:N0} KB";
    }
}
