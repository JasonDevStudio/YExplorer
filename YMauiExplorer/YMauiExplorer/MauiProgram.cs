using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using UraniumUI.Dialogs;
using YMauiExplorer.Models;

namespace YMauiExplorer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("JETBRAINSMONO-THIN.TTF", "JetBrainsMonoThin");
            });

        Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Debug()
           .Enrich.WithThreadId()
           .WriteTo.File("logs\\YExplorer.txt", rollingInterval: RollingInterval.Day, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {ThreadId}] {Message:lj}{NewLine}{Exception}")
           .CreateLogger();

        AppSettingsUtils.LoadJson(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Log.Information("The application has started.");

        builder.UseMauiCommunityToolkit()
            .Services.AddOptions<UraniumUI.Dialogs.DialogOptions>();

        var app = builder.Build();

        return app;
    }
}
