using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Notepad.Abstractions;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Notepad.DefaultPlugins;
using Notepad.Services;

namespace Notepad;

/// <summary>
/// Main application class.
/// </summary>
public partial class App
{
    private Window? _window;
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Notepad");
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "app.log");
    private static StreamWriter? _logWriter;
    private static ILogger? _logger;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Gets the logger factory.
    /// </summary>
    public static ILoggerFactory LoggerFactory { get; private set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    public App()
    {
        // Set up file logging early (before DI) so we can catch startup errors
        InitializeFileLogging();
        
        // Initialize configuration from command-line arguments
        AppConfiguration.InitializeFromCommandLine(Environment.GetCommandLineArgs());
        
        // Set up global exception handlers first
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        WriteToLogFile("App constructor: Starting");
        InitializeComponent();
        WriteToLogFile("App constructor: InitializeComponent completed");
        ConfigureServices();
        WriteToLogFile("App constructor: ConfigureServices completed");

        _logger = LoggerFactory.CreateLogger<App>();
        _logger.LogInformation("Application initialized");
    }

    private static void InitializeFileLogging()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
            _logWriter = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };
            _logWriter.WriteLine($"\n=== Application started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        }
        catch
        {
            // Ignore - logging will be disabled
        }
    }

    private static void WriteToLogFile(string message)
    {
        try
        {
            _logWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }
        catch
        {
            // Ignore logging errors
        }
    }

    private static void ConfigureServices()
    {
        try
        {
            WriteToLogFile("ConfigureServices: Starting");
            var services = new ServiceCollection();
            WriteToLogFile("ConfigureServices: ServiceCollection created");

            // Configure logging with console (for debug output) and our file writer
            WriteToLogFile("ConfigureServices: Adding logging");
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddDebug();
                // Note: Console logging may not work in WinUI apps
                // builder.AddConsole();
                builder.AddProvider(new FileLoggerProvider(_logWriter));
            });
            WriteToLogFile("ConfigureServices: Logging added");

            // Register core services (these will be configured by MainWindow after creation)
            WriteToLogFile("ConfigureServices: Registering core services");
            services.AddSingleton<SessionService>();
            
            services.AddSingleton<DocumentService>();
            services.AddSingleton<IDocumentService>(sp => sp.GetRequiredService<DocumentService>());
            
            services.AddSingleton<EditorService>();
            services.AddSingleton<IEditorService>(sp => sp.GetRequiredService<EditorService>());
            
            services.AddSingleton<MenuService>();
            services.AddSingleton<IMenuService>(sp => sp.GetRequiredService<MenuService>());
            WriteToLogFile("ConfigureServices: Core services registered");

            // Register default plugins (this also calls their ConfigureServices methods)
            WriteToLogFile("ConfigureServices: Registering default plugins");
            services.AddDefaultPlugins();
            WriteToLogFile("ConfigureServices: Default plugins registered");
            
            WriteToLogFile("ConfigureServices: Building ServiceProvider");
            Services = services.BuildServiceProvider();
            WriteToLogFile("ConfigureServices: ServiceProvider built");
            
            LoggerFactory = Services.GetRequiredService<ILoggerFactory>();
            WriteToLogFile("ConfigureServices: Completed successfully");
        }
        catch (Exception ex)
        {
            WriteToLogFile($"ConfigureServices: EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            WriteToLogFile($"ConfigureServices: StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _logger?.LogInformation("OnLaunched: Creating MainWindow");
            _window = new MainWindow();
            _logger?.LogInformation("OnLaunched: MainWindow created, activating");
            _window.Activate();
            _logger?.LogInformation("OnLaunched: MainWindow activated");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OnLaunched failed");
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled UI Exception");
        WriteToLogFile($"UNHANDLED UI EXCEPTION: {e.Exception}");
        e.Handled = true; // Try to keep app running for diagnostics
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.ExceptionObject as Exception, "Domain Unhandled Exception");
        WriteToLogFile($"DOMAIN UNHANDLED EXCEPTION: {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unobserved Task Exception");
        WriteToLogFile($"UNOBSERVED TASK EXCEPTION: {e.Exception}");
        e.SetObserved();
    }

    /// <summary>
    /// Closes the log writer. Should be called when application is closing.
    /// </summary>
    public static void CloseLogWriter()
    {
        try
        {
            _logWriter?.WriteLine($"=== Application closing at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _logWriter?.Dispose();
            _logWriter = null;
        }
        catch
        {
            // Ignore disposal errors
        }
    }

    /// <summary>
    /// Gets a logger for the specified type.
    /// </summary>
    public static ILogger<T> GetLogger<T>() => LoggerFactory.CreateLogger<T>();
    
    /// <summary>
    /// Gets a logger for the specified category name.
    /// </summary>
    public static ILogger GetLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);
}

/// <summary>
/// Simple file logger provider for Microsoft.Extensions.Logging.
/// </summary>
file sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter? _writer;

    public FileLoggerProvider(StreamWriter? writer) => _writer = writer;

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer);

    public void Dispose() { }
}

/// <summary>
/// Simple file logger for Microsoft.Extensions.Logging.
/// </summary>
file sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly StreamWriter? _writer;

    public FileLogger(string categoryName, StreamWriter? writer)
    {
        _categoryName = categoryName;
        _writer = writer;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        
        try
        {
            var message = formatter(state, exception);
            var shortCategory = _categoryName.Contains('.') ? _categoryName[((_categoryName.LastIndexOf('.') + 1))..] : _categoryName;
            _writer?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{shortCategory}] {message}");
            if (exception is not null)
            {
                _writer?.WriteLine($"  Exception: {exception}");
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
