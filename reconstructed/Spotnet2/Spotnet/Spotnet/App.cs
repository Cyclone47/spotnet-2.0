using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Threading;
using NLog;
using System.IO;
using Spotnet.Deployment;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;
using Spotnet.Properties;

namespace Spotnet;
public partial class App : Application
{
    private static readonly Logger Log;
    public static List<string> Args { get; set; }

    static App()
    {
        NativeMethods.SetErrorMode(NativeMethods.SetErrorMode(ErrorModes.SystemDefault) | ErrorModes.SemNogpfaulterrorbox | ErrorModes.SemFailcriticalerrors | ErrorModes.SemNoopenfileerrorbox);

        // Let Windows negotiate the best TLS version for every HTTPS call in the app
        // (update checks, Newznab, image and list downloads). Without this the .NET
        // Framework default can still pin these to TLS 1.0, which providers are
        // switching off.
        ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
        ServicePointManager.DefaultConnectionLimit = 32;

        Log = LogManager.GetCurrentClassLogger();
        Tracker tracker = new Tracker();
        DispatcherHelper.Initialize();
        tracker.Debug();
    }

    public App()
    {
        Application.Current.DispatcherUnhandledException += CurrentOnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static void OnProcessExit(object sender, EventArgs e)
    {
        SquirrelStuff.DisposeUpdateManager();
    }

    private void CurrentOnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Exception(e.Exception, !e.Exception.TheMostInnerException().Message.Contains("dwmapi"));
        e.Handled = true;
    }

    private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Exception((Exception)e.ExceptionObject, showToClient: true);
    }

    private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Exception(e.Exception);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                AppHelper.Error("Windows XP and below are not supported");
                Sys.Shutdown();
                return;
            }

            if (!SquirrelStuff.CreateProgramDataAndGetPermissionsToIt())
            {
                Sys.Shutdown();
                return;
            }

            string value = Path.Combine(AppHelper.SettingsFolder, "Logs\\spotnet.log");
            GlobalDiagnosticsContext.Set("logfile", value);
            if (!SquirrelStuff.VerifyAndRestoreSettings())
            {
                Sys.Shutdown();
                return;
            }

            Args = e.Args.ToList();
            if (SquirrelStuff.ProcessStateChangedEvents(Args))
            {
                Sys.Shutdown();
                return;
            }

            Args.RemoveAll((string a) => a.StartsWith("--") && !a.Equals("--exitOnUninstall"));
            Process otherInstance = OtherInstancesCommunicator.GetOtherInstance();
            if (otherInstance != null)
            {
                if (Args != null && Args.Count != 0)
                {
                    OtherInstancesCommunicator.SendParamsToPipe(Args);
                    Sys.Shutdown();
                    return;
                }

                Log.Debug("Spotnet is running already");
                if (OtherInstancesCommunicator.TryToBringSpotnetToTheTop(otherInstance))
                {
                    Sys.Shutdown();
                    return;
                }
            }

            Log.Info("Start Spotnet {0} {1} channel", AppHelper.AppVersion, SquirrelStuff.UpdateChannel);
            Log.Debug("OS version: " + Sys.StatsReporter.OsVersion);

            // Show our custom SplashWindow (replaces the plain WPF SplashScreen).
            // Language is initialized before showing so the step labels are already localized.
            UserLanguageHelper.Initialize(Settings.Default.UserLanguage);
            ThemeHelper.Initialize();
            Views.SplashWindow.ShowSplash();
            Views.SplashWindow.SetProgress(1); // "Loading settings..."

            SquirrelStuff.AfterDeploymentActions();
            Settings.Default.IsNewVersion = false;
            Settings.Default.MaxResults = 250;
            Settings.Default.Save();
            ThreadPool.SetMaxThreads(256, 1000);
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
        }
        finally
        {
            if (!Settings.Default.EnableLogging)
            {
                LogManager.GlobalThreshold = LogLevel.Off;
            }
        }
    }

    /// <summary>Close the custom splash window. Called from MainWindow.OnLoad() after the main window is ready.</summary>
    internal static void CloseSplash()
    {
        Views.SplashWindow.CloseSplash();
    }
}
