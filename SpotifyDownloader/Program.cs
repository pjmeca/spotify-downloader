using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SpotifyAPI.Web;
using SpotifyDownloader.Services;

var Configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var app = Build(args, Configuration);

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Initializing...");

try
{
    var trackingService = app.Services.GetRequiredService<ITrackingService>();
    var downloadingService = app.Services.GetRequiredService<IDownloadingService>();

    var trackingInformation = trackingService.ReadTrackingInformation();
    var result = await downloadingService.Download(trackingInformation);
    logger.LogInformation("Downloaded {albums} new albums and {playlists} new playlists.", result.AlbumsDownloaded, result.PlaylistsDownloaded);

    logger.LogInformation("Ready!");
}
catch (Exception ex)
{
    logger.LogError(ex, "Stopped program because of exception");
    Environment.FailFast("Stopped program because of exception", ex);
}

static IHost Build(string[] args, IConfigurationRoot Configuration)
{
    var builder = Host.CreateDefaultBuilder(args);

    builder.ConfigureServices(x =>
    {
        x.AddSerilog(config =>
        {
            config
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console()
                .WriteTo.File("/app/logs/debug-.log", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug, rollingInterval: RollingInterval.Day, fileSizeLimitBytes: null, retainedFileCountLimit: 5)
                .WriteTo.File("/app/logs/info-.log", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information, rollingInterval: RollingInterval.Day, fileSizeLimitBytes: null, retainedFileCountLimit: 31)
                .WriteTo.File("/app/logs/error-.log", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error, rollingInterval: RollingInterval.Day, fileSizeLimitBytes: null, retainedFileCountLimit: 31);
        });

        x.AddSingleton<ITrackingService, TrackingService>();
        x.AddSingleton<IDownloadingService, DownloadingService>();

        var config = SpotifyClientConfig
            .CreateDefault()
            .WithAuthenticator(new ClientCredentialsAuthenticator(
                Configuration.GetSection("CLIENT").GetValue<string>("ID")!,
                Configuration.GetSection("CLIENT").GetValue<string>("SECRET")!));
        x.AddSingleton(new SpotifyClient(config));
    });

    var app = builder.Build();
    return app;
}