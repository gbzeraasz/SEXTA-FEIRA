using System.Security;
using System.Text;

namespace SextaFeira;

/// <summary>
/// Cria o botão "Otimizar" pra fixar na barra de tarefas.
/// Usa uma tarefa agendada com privilégio máximo, assim o clique não pede permissão toda vez.
/// </summary>
internal static class Atalho
{
    public const string NomeTarefa = "SEXTA-FEIRA Otimizar";

    private static string CaminhoAtalho => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Otimizar PC (SEXTA-FEIRA).lnk");

    public static bool Instalado()
    {
        string saida = Sistema.Rodar("schtasks", $"/query /tn \"{NomeTarefa}\"");
        return saida.Contains(NomeTarefa) && File.Exists(CaminhoAtalho);
    }

    public static bool Instalar()
    {
        // Caminho estável: o Velopack mantém o exe em ...\SEXTA-FEIRA\current\
        string exe = Environment.ProcessPath!;
        string usuario = Environment.UserDomainName + "\\" + Environment.UserName;

        string xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo><Description>Otimiza o PC pela SEXTA-FEIRA</Description></RegistrationInfo>
  <Principals>
    <Principal id=""Author"">
      <UserId>{SecurityElement.Escape(usuario)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT5M</ExecutionTimeLimit>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{SecurityElement.Escape(exe)}</Command>
      <Arguments>{Program.ArgOtimizar}</Arguments>
    </Exec>
  </Actions>
</Task>";

        string arqXml = Path.Combine(Path.GetTempPath(), "sexta-feira-tarefa.xml");
        File.WriteAllText(arqXml, xml, Encoding.Unicode);
        Sistema.Rodar("schtasks", $"/create /tn \"{NomeTarefa}\" /xml \"{arqXml}\" /f");
        try { File.Delete(arqXml); } catch { }

        // Atalho no Menu Iniciar que só dispara a tarefa (sem janela preta)
        try
        {
            string conhost = Path.Combine(Environment.SystemDirectory, "conhost.exe");
            Type tipo = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(tipo)!;
            dynamic lnk = shell.CreateShortcut(CaminhoAtalho);
            lnk.TargetPath = conhost;
            lnk.Arguments = $"--headless schtasks.exe /run /tn \"{NomeTarefa}\"";
            string raio = Path.Combine(AppContext.BaseDirectory, "raio.ico");
            lnk.IconLocation = File.Exists(raio) ? raio + ",0" : exe + ",0";
            lnk.Description = "Otimiza o PC antes de jogar";
            lnk.Save();
        }
        catch { return false; }

        return Instalado();
    }

    public static void Remover()
    {
        Sistema.Rodar("schtasks", $"/delete /tn \"{NomeTarefa}\" /f");
        try { File.Delete(CaminhoAtalho); } catch { }
    }
}
