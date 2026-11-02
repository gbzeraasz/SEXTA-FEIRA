using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SextaFeira;

/// <summary>
/// Ajustes de latência/consistência que vieram da análise do iGust Windows Boost
/// (só os que têm efeito real). Todos guardam o valor original pra dar pra desfazer.
/// </summary>
internal static class Ajustes
{
    // ================= Aceleração do mouse =================

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint acao, uint param, int[] valores, uint flags);
    private const uint SPI_SETMOUSE = 0x0004, SPIF_UPDATEINIFILE = 0x01, SPIF_SENDCHANGE = 0x02;

    private const string ChaveMouse = @"HKCU\Control Panel\Mouse";

    public static bool AceleracaoMouseLigada() => (Sistema.LerReg(ChaveMouse, "MouseSpeed") as string ?? "1") != "0";

    /// <summary>"Aumentar precisão do ponteiro" desligado: o cursor anda sempre o mesmo tanto pro mesmo movimento.</summary>
    public static void DesligarAceleracaoMouse()
    {
        if (!AceleracaoMouseLigada()) return;
        Sistema.GravarReg(ChaveMouse, "MouseSpeed", "0", RegistryValueKind.String);
        Sistema.GravarReg(ChaveMouse, "MouseThreshold1", "0", RegistryValueKind.String);
        Sistema.GravarReg(ChaveMouse, "MouseThreshold2", "0", RegistryValueKind.String);
        // vale na hora, sem precisar sair da conta
        try { SystemParametersInfo(SPI_SETMOUSE, 0, new[] { 0, 0, 0 }, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE); } catch { }
    }

    /// <summary>Volta a aceleração do mouse ao que era antes do app (ou ao padrão do Windows).</summary>
    public static void ReativarAceleracaoMouse()
    {
        var originais = Backup.Carregar().Registro.Where(r => r.Caminho == ChaveMouse).ToDictionary(r => r.Nome, r => r.Valor);
        string vel = originais.GetValueOrDefault("MouseSpeed") ?? "1";
        string l1 = originais.GetValueOrDefault("MouseThreshold1") ?? "6";
        string l2 = originais.GetValueOrDefault("MouseThreshold2") ?? "10";
        if (vel == "0") { vel = "1"; l1 = "6"; l2 = "10"; }

        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse", true);
            k.SetValue("MouseSpeed", vel, RegistryValueKind.String);
            k.SetValue("MouseThreshold1", l1, RegistryValueKind.String);
            k.SetValue("MouseThreshold2", l2, RegistryValueKind.String);
        }
        catch { }
        int.TryParse(l1, out int a);
        int.TryParse(l2, out int b);
        int.TryParse(vel, out int c);
        try { SystemParametersInfo(SPI_SETMOUSE, 0, new[] { a, b, c }, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE); } catch { }
    }

    // ================= Suspensão seletiva de USB =================

    private const string SubUsb = "2a737441-1930-4402-8d77-b2bebba308a3";
    private const string UsbSeletiva = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";

    /// <summary>Valor atual na tomada (1 = ligada, 0 = desligada, -1 = não deu pra ler).</summary>
    public static int UsbSeletivaAtual()
    {
        string s = Sistema.Rodar("powercfg", $"/q SCHEME_CURRENT {SubUsb} {UsbSeletiva}");
        var m = Regex.Match(s, "0x([0-9a-fA-F]{8})");
        return m.Success ? Convert.ToInt32(m.Groups[1].Value, 16) : -1;
    }

    public static void DesligarUsbSeletiva()
    {
        int atual = UsbSeletivaAtual();
        if (atual == 0) return;
        if (atual > 0)
            Backup.GuardarComando("usb", "cmd",
                $"/c powercfg /setacvalueindex SCHEME_CURRENT {SubUsb} {UsbSeletiva} {atual} & powercfg /setdcvalueindex SCHEME_CURRENT {SubUsb} {UsbSeletiva} {atual} & powercfg /setactive SCHEME_CURRENT");
        Sistema.Rodar("powercfg", $"/setacvalueindex SCHEME_CURRENT {SubUsb} {UsbSeletiva} 0");
        Sistema.Rodar("powercfg", $"/setdcvalueindex SCHEME_CURRENT {SubUsb} {UsbSeletiva} 0");
        Sistema.Rodar("powercfg", "/setactive SCHEME_CURRENT");
    }

    // ================= Power Throttling =================

    private const string ChaveThrottling = @"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling";

    public static bool ThrottlingLigado() => Sistema.LerRegInt(ChaveThrottling, "PowerThrottlingOff") != 1;

    public static void DesligarThrottling()
    {
        if (ThrottlingLigado()) Sistema.GravarDword(ChaveThrottling, "PowerThrottlingOff", 1);
    }

    // ================= Economia de energia da placa de rede =================

    /// <summary>Quantas placas de rede físicas o Windows pode desligar pra economizar (-1 = não deu pra ler).</summary>
    public static int PlacasComEconomia()
    {
        string s = Sistema.Rodar("powershell",
            "-NoProfile -Command \"@(Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Get-NetAdapterPowerManagement -ErrorAction SilentlyContinue | " +
            "Where-Object { $_.AllowComputerToTurnOffDevice -eq 'Enabled' }).Count\"", 20000);
        return int.TryParse(s.Trim(), out int n) ? n : -1;
    }

    /// <summary>Vale depois de reiniciar o PC (assim a internet não cai agora).</summary>
    public static void DesligarEconomiaRede()
    {
        if (PlacasComEconomia() == 0) return;
        Backup.GuardarComando("rede", "powershell",
            "-NoProfile -Command \"Get-NetAdapter -Physical | Enable-NetAdapterPowerManagement -NoRestart -ErrorAction SilentlyContinue\"");
        Sistema.Rodar("powershell",
            "-NoProfile -Command \"Get-NetAdapter -Physical | Disable-NetAdapterPowerManagement -NoRestart -ErrorAction SilentlyContinue\"", 30000);
    }
}
