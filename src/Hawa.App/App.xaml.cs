using System.IO;
using System.Windows;
using System.Windows.Threading;
using Hawa.App.Popup;
using Hawa.App.Settings;
using Hawa.App.Tray;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Hawa.App;

public partial class App : Application
{
    private const string MutexName = "Hawa.SingleInstance";
    private Mutex? _mutex;
    private bool _ownsMutex;
    private AppServices? _services;
    private TrayController? _tray;
    private PopupCard? _popup;
    private PopupTrigger? _popupTrigger;
    private SettingsWindow? _settings;
    private Microsoft.Extensions.Logging.ILogger? _log;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Subscribe before any work that can throw: until Serilog is configured the handlers
        // fall back to startup-crash.log, so nothing dies silently.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log(LogLevel.Critical, "Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log(LogLevel.Error, "Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        try
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                SecondInstanceSignal.Raise();
                Shutdown();
                return;
            }

            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hawa", "logs");
            Directory.CreateDirectory(logDir);
            var serilog = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(logDir, "hawa-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();
            var loggers = LoggerFactory.Create(b => b.AddSerilog(serilog, dispose: true));
            _log = loggers.CreateLogger<App>();

            _services = new AppServices(loggers);
            await _services.InitializeAsync();

            _popup = new PopupCard(_services);
            _popupTrigger = new PopupTrigger(_services, _popup);
            _tray = new TrayController(_services, ShowSettings, () => _popup.Toggle(), () => _popup.ShowFor(TimeSpan.FromSeconds(3)));
            SecondInstanceSignal.Listen(() => Dispatcher.Invoke(ShowSettings));

            _log.LogInformation("Hawa started");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Critical, "Startup failed", ex);
            Shutdown(1);
        }
    }

    /// <summary>Logs through Serilog once it exists, and through <see cref="FallbackLog"/> before that.</summary>
    private void Log(LogLevel level, string message, Exception? ex)
    {
        if (_log is not null) _log.Log(level, ex, "{Message}", message);
        else FallbackLog(message, ex);
    }

    /// <summary>Last-resort sink for failures that happen before Serilog is configured.</summary>
    private static void FallbackLog(string message, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hawa", "logs");
            Directory.CreateDirectory(dir);
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}" +
                       (ex is null ? string.Empty : Environment.NewLine + ex) + Environment.NewLine;
            File.AppendAllText(Path.Combine(dir, "startup-crash.log"), line);
        }
        catch
        {
            // Nowhere left to report to; losing the line beats crashing the crash handler.
        }
    }

    private void ShowSettings()
    {
        _settings ??= new SettingsWindow(_services!);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(LogLevel.Error, "Dispatcher exception", e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _popupTrigger?.Dispose();
        _tray?.Dispose();
        _services?.Dispose();
        // A second instance never took ownership, so releasing would throw ApplicationException.
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

/// <summary>Named event used by a second instance to ask the first one to show its settings window.</summary>
internal static class SecondInstanceSignal
{
    private const string EventName = "Hawa.ShowSettings";

    public static void Raise()
    {
        try { using var ev = EventWaitHandle.OpenExisting(EventName); ev.Set(); } catch { }
    }

    public static void Listen(Action onSignal)
    {
        var ev = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        var thread = new Thread(() => { while (true) { ev.WaitOne(); onSignal(); } }) { IsBackground = true, Name = "Hawa.SecondInstance" };
        thread.Start();
    }
}
