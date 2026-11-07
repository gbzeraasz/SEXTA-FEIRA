using System.Runtime.InteropServices;

namespace SextaFeira;

/// <summary>
/// Taxa de atualização (Hz) do monitor. Muita gente tem monitor de 75/144/165 Hz rodando a 60 Hz no Windows
/// sem saber: o jogo pode dar 200 FPS, mas tu só VÊ 60.
/// </summary>
internal static class Tela
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY;
        public int dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(string deviceName, ref DEVMODE devMode, IntPtr hwnd, int flags, IntPtr lParam);

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int DM_DISPLAYFREQUENCY = 0x400000;
    private const int DM_INTERLACED = 0x2;
    private const int CDS_UPDATEREGISTRY = 0x1, CDS_TEST = 0x2;

    public class Monitor
    {
        public string Dispositivo;
        public int Largura, Altura, HzAtual, HzMaximo;
        public bool Principal;
        public bool PodeMelhorar => HzMaximo > HzAtual + 1;
    }

    private static DEVMODE Novo() => new() { dmSize = (short)Marshal.SizeOf<DEVMODE>() };

    public static List<Monitor> Ler()
    {
        var l = new List<Monitor>();
        foreach (var s in Screen.AllScreens)
        {
            try
            {
                var atual = Novo();
                if (!EnumDisplaySettings(s.DeviceName, ENUM_CURRENT_SETTINGS, ref atual)) continue;
                int max = atual.dmDisplayFrequency;
                var modo = Novo();
                for (int i = 0; EnumDisplaySettings(s.DeviceName, i, ref modo); i++)
                {
                    if (modo.dmPelsWidth == atual.dmPelsWidth && modo.dmPelsHeight == atual.dmPelsHeight &&
                        modo.dmBitsPerPel == atual.dmBitsPerPel && (modo.dmDisplayFlags & DM_INTERLACED) == 0 &&
                        modo.dmDisplayFrequency > max && modo.dmDisplayFrequency < 1000)
                        max = modo.dmDisplayFrequency;
                    modo = Novo();
                }
                l.Add(new Monitor
                {
                    Dispositivo = s.DeviceName, Principal = s.Primary,
                    Largura = atual.dmPelsWidth, Altura = atual.dmPelsHeight,
                    HzAtual = atual.dmDisplayFrequency, HzMaximo = max
                });
            }
            catch { }
        }
        return l;
    }

    /// <summary>Coloca o monitor no maior Hz que ele aceita na resolução atual. A tela pisca 1 segundo.</summary>
    public static bool UsarMaximo(Monitor m)
    {
        var dm = Novo();
        if (!EnumDisplaySettings(m.Dispositivo, ENUM_CURRENT_SETTINGS, ref dm)) return false;
        dm.dmDisplayFrequency = m.HzMaximo;
        dm.dmFields = DM_DISPLAYFREQUENCY;
        if (ChangeDisplaySettingsEx(m.Dispositivo, ref dm, IntPtr.Zero, CDS_TEST, IntPtr.Zero) != 0) return false;
        bool ok = ChangeDisplaySettingsEx(m.Dispositivo, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero) == 0;
        Atualizador.Log($"Monitor {m.Dispositivo}: {m.HzAtual} Hz -> {m.HzMaximo} Hz ({(ok ? "ok" : "falhou")})");
        return ok;
    }
}
