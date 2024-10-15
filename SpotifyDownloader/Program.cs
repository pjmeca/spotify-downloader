using EasyCronJob.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SpotifyAPI.Web;
using SpotifyDownloader.Data;
using SpotifyDownloader.Helpers;
using SpotifyDownloader.Services;
using SpotifyDownloader.Utils;

var banner = await BannerRetriever.GetBanner();
Console.Write(banner);

var Configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var configuration = Fluents.Fluent.Try(() => new GlobalConfiguration(Configuration))
    .Catch(x => Environment.FailFast($"[ERROR] Missing required configuration: {x.Message}"))
    .Execute<GlobalConfiguration>();
string CRON_SCHEDULE = configuration.CRON_SCHEDULE;
string SPOTIFY_CLIENT_ID = configuration.SPOTIFY_CLIENT_ID;
string SPOTIFY_CLIENT_SECRET = configuration.SPOTIFY_CLIENT_SECRET;

var app = Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    Directory.CreateDirectory(Directory.GetParent(GlobalConfiguration.DB_PATH)!.FullName);
    using var scope = app.Services.CreateScope();
    using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
    if (pendingMigrations.Any())
    {
        logger.LogInformation("Applying {num} pending migrations...", pendingMigrations.Count());
        pendingMigrations.ToList().ForEach(x => logger.LogDebug("Applying migration \"{name}\".", x));
        await dbContext.Database.MigrateAsync();
    }

    logger.LogInformation("Cron job configured with: \"{cron}\"", CRON_SCHEDULE);
    
    logger.LogInformation("Ready!");

    // The cron job will take it from here
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "Stopped program because of exception");
    Environment.FailFast("Stopped program because of exception", ex);
}

IHost Build()
{
    var builder = Host.CreateDefaultBuilder(args);

    builder.ConfigureAppConfiguration((context, config) =>
    {
        var env = context.HostingEnvironment;
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
              .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();
    });

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

        x.AddSingleton<GlobalConfiguration>();

        x.AddDbContext<ApplicationDbContext>();

        x.AddSingleton<IFileManagmentService, FileManagmentService>();
        x.AddSingleton<ITrackingService, TrackingService>();
        x.AddSingleton<IDownloadingService, DownloadingService>();
        x.AddScoped<IArtistsService, ArtistsService>();

        var config = SpotifyClientConfig
            .CreateDefault()
            .WithAuthenticator(new ClientCredentialsAuthenticator(
                SPOTIFY_CLIENT_ID,
                SPOTIFY_CLIENT_SECRET));
        x.AddSingleton(new SpotifyClient(config));

        x.ApplyResulation<CronJob>(options =>
        {
            options.CronExpression = CRON_SCHEDULE;
            options.TimeZoneInfo = TimeZoneInfo.Local;
            options.CronFormat = Cronos.CronFormat.Standard;
        });
    });

    var app = builder.Build();
    return app;
}