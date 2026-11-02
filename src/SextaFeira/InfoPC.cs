using Microsoft.Win32;

namespace SextaFeira;

/// <summary>Hardware e sistema do PC (lido uma vez ao abrir).</summary>
internal class InfoPC
{
    public string Cpu = "?";
    public string Gpu = "?";
    public string GpuDriverBruto = "";
    public string DriverNvidia;      // ex: "581.80" (null se não for NVIDIA)
    public int RamGB;
    public int Pentes;
    public int RamMHz;
    public string Windows = "?";
    public int Build;

    public static InfoPC Ler()
    {
        var i = new InfoPC();

        var cpu = Sistema.Wmi("SELECT Name FROM Win32_Processor").FirstOrDefault();
        if (cpu != null) i.Cpu = Sistema.WmiTexto(cpu, "Name");

        // Prefere a placa dedicada (NVIDIA/AMD) se tiver mais de uma
        var gpus = Sistema.Wmi("SELECT Name, DriverVersion FROM Win32_VideoController");
        var gpu = gpus.FirstOrDefault(g => Sistema.WmiTexto(g, "Name").Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
               ?? gpus.FirstOrDefault(g => Sistema.WmiTexto(g, "Name").Contains("Radeon", StringComparison.OrdinalIgnoreCase))
               ?? gpus.FirstOrDefault();
        if (gpu != null)
        {
            i.Gpu = Sistema.WmiTexto(gpu, "Name");
            i.GpuDriverBruto = Sistema.WmiTexto(gpu, "DriverVersion");
            if (i.Gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                i.DriverNvidia = ConverterDriverNvidia(i.GpuDriverBruto);
        }

        var pentes = Sistema.Wmi("SELECT Capacity, ConfiguredClockSpeed FROM Win32_PhysicalMemory");
        i.Pentes = pentes.Count;
        double total = 0;
        foreach (var p in pentes)
        {
            if (double.TryParse(Sistema.WmiTexto(p, "Capacity"), out double c)) total += c;
            if (i.RamMHz == 0 && int.TryParse(Sistema.WmiTexto(p, "ConfiguredClockSpeed"), out int mhz)) i.RamMHz = mhz;
        }
        i.RamGB = (int)Math.Round(total / (1024d * 1024 * 1024));

        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string build = k?.GetValue("CurrentBuildNumber")?.ToString() ?? "0";
            int.TryParse(build, out i.Build);
            string versao = k?.GetValue("DisplayVersion")?.ToString() ?? "";
            i.Windows = (i.Build >= 22000 ? "Windows 11 " : "Windows 10 ") + versao;
        }
        catch { }

        return i;
    }

    /// <summary>32.0.15.8180 → 581.80</summary>
    public static string ConverterDriverNvidia(string bruto)
    {
        try
        {
            var p = bruto.Split('.');
            string d = p[2] + p[3];
            d = d[^5..];
            return d[..3] + "." + d[3..];
        }
        catch { return null; }
    }

    public string Resumo() =>
        $"{Cpu}  •  {Gpu}  •  {RamGB} GB RAM ({Pentes}x {RamMHz} MHz)  •  {Windows}";
}
