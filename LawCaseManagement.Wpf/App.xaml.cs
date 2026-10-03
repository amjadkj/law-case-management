using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using LawCaseManagement.Core;
using LawCaseManagement.Wpf.ViewModels;
using LawCaseManagement.Wpf.Views;

namespace LawCaseManagement.Wpf
{
    public partial class App : Application
    {
        private static IHost? _host;
        public static IServiceProvider Services => _host!.Services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LawCaseManagement");
            Directory.CreateDirectory(appDataDir);
            string logDir = Path.Combine(appDataDir, "logs");
            Directory.CreateDirectory(logDir);

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(logDir, "app_log-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Set unhandled exception logging to AppData (safe from permissions errors)
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                string msg = args.ExceptionObject?.ToString() ?? "Unknown exception";
                string crashPath = Path.Combine(appDataDir, "crash_log.txt");
                File.WriteAllText(crashPath, msg);
                Log.Fatal("Unhandled Domain Exception: {Exception}", msg);
                MessageBox.Show(msg, "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                string msg = args.Exception.ToString();
                string crashPath = Path.Combine(appDataDir, "crash_log.txt");
                File.WriteAllText(crashPath, msg);
                Log.Fatal(args.Exception, "Unhandled Dispatcher Exception");
                MessageBox.Show(args.Exception.Message, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            try
            {
                var builder = Host.CreateDefaultBuilder(e.Args)
                    .UseSerilog()
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                              .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // 1. Bind Configuration
                        var appConfig = new AppConfig();
                        var dbSection = context.Configuration.GetSection("Database");
                        if (dbSection.Exists())
                        {
                            appConfig.Database.Provider = dbSection["Provider"] ?? "Sqlite";
                            appConfig.Database.ConnectionString = dbSection["ConnectionString"] ?? "Data Source=law_case_management.db";
                        }
                        var docSection = context.Configuration.GetSection("Documents");
                        if (docSection.Exists())
                        {
                            appConfig.Documents.SharedFolder = docSection["SharedFolder"] ?? string.Empty;
                        }
                        services.AddSingleton(appConfig);

                        // 2. Register DbContextFactory
                        services.AddDbContextFactory<CaseDbContext>(options =>
                        {
                            if (appConfig.Database.Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                            {
                                options.UseSqlServer(appConfig.Database.ConnectionString);
                            }
                            else
                            {
                                options.UseSqlite(appConfig.Database.ConnectionString);
                            }
                        });

                        // 3. Register Core Services
                        services.AddSingleton<DatabaseInitializer>();
                        services.AddSingleton<IAuditService, AuditService>();
                        services.AddSingleton<IAuthService, AuthService>();
                        services.AddTransient<ICaseService, CaseService>();
                        services.AddTransient<IClientService, ClientService>();
                        services.AddTransient<ITaskService, TaskService>();
                        services.AddTransient<IDocumentService, DocumentService>();
                        services.AddTransient<IAdminService, AdminService>();

                        // 4. Register ViewModels
                        services.AddTransient<LoginViewModel>();
                        services.AddTransient<DashboardViewModel>();
                        services.AddTransient<CasesViewModel>();
                        services.AddTransient<ClientsViewModel>();
                        services.AddTransient<TasksViewModel>();
                        services.AddTransient<AdminViewModel>();
                        services.AddTransient<MainViewModel>();

                        // 5. Register Views
                        services.AddTransient<LoginWindow>();
                        services.AddTransient<MainWindow>();
                    });

                _host = builder.Build();

                // Run Database Initializer & Seed once at startup
                using (var scope = _host.Services.CreateScope())
                {
                    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
                    initializer.Initialize();
                }

                // Show Login Window
                var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                string msg = $"Startup Error: {ex.Message}\n\n{ex.StackTrace}";
                string errPath = Path.Combine(appDataDir, "startup_error.txt");
                File.WriteAllText(errPath, msg);
                Log.Fatal(ex, "Startup Failure");
                MessageBox.Show(msg, "Startup Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
