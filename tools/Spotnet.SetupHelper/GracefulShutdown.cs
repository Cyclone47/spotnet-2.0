using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;

namespace Spotnet.Setup;

public static class GracefulShutdown
{
    public static void CloseSpotnet()
    {
        int session = Process.GetCurrentProcess().SessionId;
        var processes = Process.GetProcessesByName("Spotnet");
        try
        {
            if (processes.Length == 0) return;
            if (processes.Any(p => !p.HasExited && p.SessionId != session))
                throw new IOException("Spotnet is running in another Windows session. Ask that user to exit Spotnet, then retry Setup.");

            bool signalled = false;
            try
            {
                // Spotnet's existing tray-safe exit protocol; does not invoke a legacy executable.
                using (var pipe = new NamedPipeClientStream(".", @"Pipe\Spotnet", PipeDirection.Out))
                {
                    pipe.Connect(1500);
                    using (var writer = new BinaryWriter(pipe)) writer.Write("--exitOnUninstall");
                    signalled = true;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is UnauthorizedAccessException) { }

            if (!signalled)
                foreach (var process in processes)
                    if (!process.HasExited) process.CloseMainWindow();

            WaitUntilClosed(AnyRunning, TimeSpan.FromSeconds(30));
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private static bool AnyRunning()
    {
        bool running = false;
        foreach (var process in Process.GetProcessesByName("Spotnet"))
            using (process) running |= !process.HasExited;
        return running;
    }

    public static void WaitUntilClosed(Func<bool> running, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (running())
        {
            if (watch.Elapsed >= timeout)
                throw new IOException("Spotnet did not exit within 30 seconds. Finish or pause downloads and exit it from the tray, then retry Setup. Setup will not force-kill it.");
            Thread.Sleep(100);
        }
    }
}
