using System.Runtime.InteropServices;

namespace RedFox.AppHost.Sample;

/// <summary>
/// Reports how the launcher started the sample and exercises the splash screen API.
/// </summary>
internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string report = $"""
            Hosted: {AppHostSplash.IsHosted}
            Apartment: {Thread.CurrentThread.GetApartmentState()}
            Runtime: {RuntimeInformation.FrameworkDescription} ({typeof(object).Assembly.Location})
            Base directory: {AppContext.BaseDirectory}
            Process path: {Environment.ProcessPath}
            Arguments: {string.Join(" | ", args)}
            """;

        int exitCode = Array.IndexOf(args, "--exit-code");

        if (exitCode >= 0)
        {
            Console.WriteLine(report);
            return int.Parse(args[exitCode + 1]);
        }

        AppHostSplash.SetStatus("Loading sample data...");
        Thread.Sleep(1500);
        AppHostSplash.SetStatus("Creating window...");
        Thread.Sleep(500);
        AppHostSplash.Close();

        MessageBoxW(0, report, "RedFox AppHost Sample", 0);
        return 0;
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint owner, string text, string caption, uint type);
}
