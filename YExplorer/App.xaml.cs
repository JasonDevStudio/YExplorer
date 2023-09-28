using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using YExplorer.Models;

namespace YExplorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Debug()
           .Enrich.WithThreadId()
           .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {ThreadId}] {Message:lj}{NewLine}{Exception}")
           .WriteTo.File("logs\\YExplorer.txt", rollingInterval: RollingInterval.Day, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {ThreadId}] {Message:lj}{NewLine}{Exception}") 
           .CreateLogger();

            AppSettingsUtils.LoadJson(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

            Log.Information("The application has started.");
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("The application is shutting down.");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
