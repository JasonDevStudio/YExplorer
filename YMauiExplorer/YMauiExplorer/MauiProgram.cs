using Microsoft.Extensions.Logging;
using Serilog;

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
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

        Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Debug()
           .Enrich.WithThreadId()
           .WriteTo.File("logs\\YExplorer.{Date}.txt", rollingInterval: RollingInterval.Day, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {ThreadId}] {Message:lj}{NewLine}{Exception}")
           .CreateLogger();

        Log.Information("The application has started.");
        return builder.Build();
	}
}
