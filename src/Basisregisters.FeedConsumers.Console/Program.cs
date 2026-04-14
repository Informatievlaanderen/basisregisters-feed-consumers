using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Basisregisters.FeedConsumers;
using Basisregisters.FeedConsumers.Console.Common;
using Basisregisters.FeedConsumers.Console.Municipality;
using Be.Vlaanderen.Basisregisters.Aws.DistributedMutex;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Debugging;

AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
    Log.Debug(
        eventArgs.Exception,
        "FirstChanceException event raised in {AppDomain}.",
        AppDomain.CurrentDomain.FriendlyName);

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
    Log.Fatal((Exception)eventArgs.ExceptionObject, "Encountered a fatal exception, exiting program.");

var host = new HostBuilder()
    .ConfigureAppConfiguration((_, builder) =>
    {
        builder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.MachineName.ToLowerInvariant()}.json", optional: true,
                reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    })
    .ConfigureLogging((hostContext, builder) =>
    {
        SelfLog.Enable(Console.WriteLine);

        Log.Logger = new LoggerConfiguration() //NOSONAR logging configuration is safe
            .ReadFrom.Configuration(hostContext.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentUserName()
            .CreateLogger();

        builder.ClearProviders();
        builder.AddSerilog(Log.Logger);
    })
    .ConfigureServices((hostContext, services) =>
    {
        var connectionString = hostContext.Configuration.GetConnectionString("FeedConsumers")
                               ?? throw new ArgumentNullException("hostContext.Configuration.GetConnectionString(\"FeedConsumers\")");

        var baseUrl = hostContext.Configuration["BaseUrl"]
                      ?? throw new ArgumentNullException("BaseUrl", "BaseUrl is required in configuration.");
        var apiKey = hostContext.Configuration["ApiKey"]
                     ?? throw new ArgumentNullException("ApiKey", "ApiKey is required in configuration.");

        var municipalityFeedOptions = new FeedProjectorOptions
        {
            Name = hostContext.Configuration["MunicipalityFeed:Name"] ?? "MunicipalityFeed",
            FeedUrl = hostContext.Configuration["MunicipalityFeed:FeedUrl"] ?? throw new ArgumentNullException("MunicipalityFeed:FeedUrl"),
            PollingIntervalInMinutes = int.Parse(hostContext.Configuration["MunicipalityFeed:PollingIntervalInMinutes"] ?? "1440"),
            IgnoreNoEventHandlers = bool.Parse(hostContext.Configuration["MunicipalityFeed:IgnoreNoEventHandlers"] ?? "false")
        };

        services.AddDbContextFactory<FeedContext>((provider, options) =>
        {
            options.UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
            options.UseNpgsql(connectionString, npgSqlOptions =>
            {
                npgSqlOptions.MigrationsHistoryTable(FeedContext.MigrationsTableName, FeedContext.Schema);
                npgSqlOptions.UseNetTopologySuite();
            });
        });

        services.AddHttpClient(municipalityFeedOptions.Name, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        });

        services.AddHostedService(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            municipalityFeedOptions.FeedClient = httpClientFactory.CreateClient(municipalityFeedOptions.Name);
            return new MunicipalityProjector(
                municipalityFeedOptions,
                provider.GetRequiredService<IDbContextFactory<FeedContext>>(),
                provider.GetRequiredService<ILoggerFactory>());
        });
    })
    .UseConsoleLifetime()
    .Build();

Log.Information("Starting Basisregisters.FeedConsumers.Console");

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

try
{
    await DistributedLock<Program>.RunAsync(
            async () =>
            {
                var context = await host.Services.GetRequiredService<IDbContextFactory<FeedContext>>()
                    .CreateDbContextAsync();

                await context.Database.MigrateAsync(CancellationToken.None);

                await host.RunAsync().ConfigureAwait(false);
            },
            DistributedLockOptions.LoadFromConfiguration(configuration),
            logger)
        .ConfigureAwait(false);
}
catch (AggregateException aggregateException)
{
    foreach (var innerException in aggregateException.InnerExceptions)
    {
        logger.LogCritical(innerException, "Encountered a fatal exception, exiting program.");
    }
}
catch (Exception e)
{
    logger.LogCritical(e, "Encountered a fatal exception, exiting program.");
    await Log.CloseAndFlushAsync();

    // Allow some time for flushing before shutdown.
    await Task.Delay(500, CancellationToken.None);
    throw;
}
finally
{
    logger.LogInformation("Stopping...");
}
