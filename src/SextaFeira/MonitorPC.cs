using System.Diagnostics;

namespace SextaFeira;

/// <summary>Lê CPU, RAM, GPU e os processos que mais pesam. Rodar fora da thread da tela.</summary>
internal static class MonitorPC
{
    public class Leitura
    {
        public float Cpu = -1, Ram = -1, Gpu = -1;
        public List<(string Nome, double Bytes)> Top = new();
    }

    public static Leitura Ler()
    {
        var r = new Leitura();

        // Classes WMI de desempenho não mudam de nome com o idioma do Windows
        var cpu = Sistema.Wmi("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'").FirstOrDefault();
        if (cpu != null && float.TryParse(Sistema.WmiTexto(cpu, "PercentProcessorTime"), out float c)) r.Cpu = c;

        var so = Sistema.Wmi("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem").FirstOrDefault();
        if (so != null &&
            double.TryParse(Sistema.WmiTexto(so, "FreePhysicalMemory"), out double livre) &&
            double.TryParse(Sistema.WmiTexto(so, "TotalVisibleMemorySize"), out double total) && total > 0)
            r.Ram = (float)((1 - livre / total) * 100);

        var gpu = Sistema.Wmi("SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
        if (gpu.Count > 0)
        {
            float soma = 0;
            foreach (var g in gpu)
                if (Sistema.WmiTexto(g, "Name").EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(Sistema.WmiTexto(g, "UtilizationPercentage"), out float u))
                    soma += u;
            r.Gpu = Math.Min(100, soma);
        }

        try
        {
            r.Top = Process.GetProcesses()
                .GroupBy(p => p.ProcessName)
                .Select(g => (g.Key, (double)g.Sum(p => { try { return p.WorkingSet64; } catch { return 0L; } })))
                .OrderByDescending(x => x.Item2)
                .Take(6)
                .ToList();
        }
        catch { }

        return r;
    }
}
