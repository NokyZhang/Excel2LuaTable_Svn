using System;
using System.IO;
using System.Diagnostics;

public class CommandHelper
{

    public static string ExcuteCommand(string command, string argument, Boolean isOut = false)
    {
        ProcessStartInfo start = new ProcessStartInfo(command, argument);
        start.CreateNoWindow = true;
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.RedirectStandardInput = true;
        start.StandardOutputEncoding = System.Text.UTF8Encoding.UTF8;
        start.StandardErrorEncoding = System.Text.UTF8Encoding.UTF8;
        Process ps = new Process();
        ps.StartInfo = start;
        ps.Start();
        ps.WaitForExit();
        string output = ps.StandardOutput.ReadToEnd() + ps.StandardError.ReadToEnd();
        Console.WriteLine(output);
        ps.Close();
        if (isOut)
        {
            return output;
        }
        else
        {
            return null;
        }
    }

    public static void ExcuteCommandNoLog(string command, string argument)
    {
        Process ps = Process.Start(command, argument);
        ps.WaitForExit();
        ps.Close();
    }
}
