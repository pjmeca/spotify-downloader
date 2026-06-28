using EasyCronJob.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SpotifyAPI.Web;
using SpotCrate.Data;
using SpotCrate.Helpers;
using SpotCrate.Services;
using SpotCrate.Utils;

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
    var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
    if (pendingMigrations.Count != 0)
    {
        logger.LogInformation("Applying {num} pending migrations...", pendingMigrations.Count);
        pendingMigrations.ForEach(x => logger.LogDebug("Applying migration \"{name}\".", x));
        await dbContext.Database.MigrateAsync();
    }

    var appVersionMigrationService = scope.ServiceProvider.GetRequiredService<IAppVersionMigrationService>();
    await appVersionMigrationService.MigrateToCurrentVersion();

    logger.LogInformation("Cron job configured with: \"{cron}\"", CRON_SCHEDULE);
    
    logger.LogInformation("Ready!");

    var hostEnvironment = app.Services.GetRequiredService<IHostEnvironment>();
    var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? hostEnvironment.EnvironmentName;
    if (string.Equals(envName, Environments.Development, StringComparison.OrdinalIgnoreCase))
    {
        logger.LogInformation("Development environment detected. Running job immediately.");
        using var devScope = app.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<CronJob>(devScope.ServiceProvider);
        await job.DoWork(CancellationToken.None);
    }

    // The cron job will take it from here
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "Stopped program because of exception");
    Environment.FailFast("Stopped program because of exception", ex);
}

WebApplication Build()
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.ConfigureAppConfiguration((context, config) =>
    {
        var env = context.HostingEnvironment;
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
              .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();
    });

    builder.Services.AddSerilog(config =>
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

    builder.Services.Configure<RouteOptions>(options =>
    {
        options.LowercaseUrls = true;
    });
    builder.Services.AddRazorPages();
    builder.Services.AddHealthChecks();
    Directory.CreateDirectory(GlobalConfiguration.DATA_PROTECTION_KEYS_DIRECTORY);
    builder.Services.AddDataProtection()
        .SetApplicationName("spotcrate")
        .PersistKeysToFileSystem(new DirectoryInfo(GlobalConfiguration.DATA_PROTECTION_KEYS_DIRECTORY));

    builder.Services.AddSingleton<GlobalConfiguration>();

    builder.Services.AddDbContext<ApplicationDbContext>();

    var config = SpotifyClientConfig
        .CreateDefault()
        .WithAuthenticator(new ClientCredentialsAuthenticator(
            SPOTIFY_CLIENT_ID,
            SPOTIFY_CLIENT_SECRET));
    builder.Services.AddSingleton(new SpotifyClient(config));
    builder.Services.AddScoped<ISpotifyClientWrapper, SpotifyClientWrapper>();

    builder.Services.AddSingleton<IFileManagementService, FileManagementService>();
    builder.Services.AddSingleton<IFileOperationCoordinator, FileOperationCoordinator>();
    builder.Services.AddSingleton<ITrackingService, TrackingService>();
    builder.Services.AddSingleton<ITrackingEditorService, TrackingEditorService>();
    builder.Services.AddScoped<IAppVersionMigrationService, AppVersionMigrationService>();
    builder.Services.AddScoped<IDownloadingService, DownloadingService>();
    builder.Services.AddSingleton<IYtDlpService, YtDlpService>();
    builder.Services.AddScoped<IArtistsService, ArtistsService>();
    builder.Services.AddScoped<PlaylistsService>();

    builder.Services.ApplyResulation<CronJob>(options =>
    {
        options.CronExpression = CRON_SCHEDULE;
        options.TimeZoneInfo = TimeZoneInfo.Local;
        options.CronFormat = Cronos.CronFormat.Standard;
    });

    var app = builder.Build();
    app.UseStaticFiles();
    app.MapRazorPages();
    app.MapHealthChecks("/health");
    return app;
}
