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

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => _log.LogCritical(args.ExceptionObject as Exception, "Unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, args) => { _log.LogError(args.Exception, "Unobserved task exception"); args.SetObserved(); };

        _services = new AppServices(loggers);
        await _services.InitializeAsync();

        _popup = new PopupCard(_services);
        _popupTrigger = new PopupTrigger(_services, _popup);
        _tray = new TrayController(_services, ShowSettings, () => _popup.Toggle(), () => _popup.ShowFor(TimeSpan.FromSeconds(3)));
        SecondInstanceSignal.Listen(() => Dispatcher.Invoke(ShowSettings));

        _log.LogInformation("Hawa started");
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
        _log?.LogError(e.Exception, "Dispatcher exception");
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
