using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Basisregisters.FeedConsumers;
using Basisregisters.FeedConsumers.Console.Address;
using Basisregisters.FeedConsumers.Console.Building;
using Basisregisters.FeedConsumers.Console.BuildingUnit;
using Basisregisters.FeedConsumers.Console.Common;
using Basisregisters.FeedConsumers.Console.Municipality;
using Basisregisters.FeedConsumers.Console.PostalInformation;
using Basisregisters.FeedConsumers.Console.StreetName;
using Be.Vlaanderen.Basisregisters.Aws.DistributedMutex;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Debugging;

//TODO: add version id as string
AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
    Log.Debug(
        eventArgs.Exception,
        "FirstChanceException event raised in {AppDomain}.",
        AppDomain.CurrentDomain.FriendlyName);

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    if (eventArgs.ExceptionObject is Exception exception)
        Log.Fatal(exception, "Encountered a fatal exception, exiting program.");
    else
        Log.Fatal("Encountered a fatal exception, exiting program. ExceptionObject: {ExceptionObject}",
            eventArgs.ExceptionObject);
};

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
                               ?? throw new InvalidOperationException(
                                   "Connection string 'FeedConsumers' is required in configuration.");

        var baseUrl = hostContext.Configuration["BaseUrl"]
                      ?? throw new ArgumentNullException("BaseUrl", "BaseUrl is required in configuration.");
        var apiKey = hostContext.Configuration["ApiKey"]
                     ?? throw new ArgumentNullException("ApiKey", "ApiKey is required in configuration.");

        services.AddDbContextFactory<FeedContext>((provider, options) =>
        {
            options.UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
            options.UseNpgsql(connectionString, npgSqlOptions =>
            {
                npgSqlOptions.MigrationsHistoryTable(FeedContext.MigrationsTableName, FeedContext.Schema);
                npgSqlOptions.UseNetTopologySuite();
            });
        });

        var feedRegistrations = new[]
        {
            new FeedRegistration("MunicipalityFeed", "MunicipalityFeed", CreateProjector<MunicipalityProjector>),
            new FeedRegistration("PostalInformationFeed", "PostalInformationFeed", CreateProjector<PostalInformationProjector>),
            new FeedRegistration("StreetNameFeed", "StreetNameFeed", CreateProjector<StreetNameProjector>),
            new FeedRegistration("AddressFeed", "AddressFeed", CreateProjector<AddressProjector>),
            new FeedRegistration("BuildingFeed", "BuildingFeed", CreateProjector<BuildingProjector>),
            new FeedRegistration("BuildingUnitFeed", "BuildingUnitFeed", CreateProjector<BuildingUnitProjector>)
        };

        var feedOptionsBySection = feedRegistrations.ToDictionary(
            r => r.Section,
            r => BuildFeedOptions(hostContext.Configuration, r.Section, r.DefaultName));

        foreach (var options in feedOptionsBySection.Values)
        {
            services.AddHttpClient(options.Name, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            });
        }

        foreach (var registration in feedRegistrations)
        {
            var options = feedOptionsBySection[registration.Section];
            services.AddHostedService(provider => registration.CreateHostedService(provider, options));
        }
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
                await using var context = await host.Services.GetRequiredService<IDbContextFactory<FeedContext>>()
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

    throw;
}
finally
{
    logger.LogInformation("Stopping...");
    await Log.CloseAndFlushAsync();

    // Allow some time for flushing before shutdown.
    await Task.Delay(500, CancellationToken.None);
}

static FeedProjectorOptions BuildFeedOptions(IConfiguration config, string section, string defaultName)
{
    return new FeedProjectorOptions
    {
        Name = config[$"{section}:Name"] ?? defaultName,
        FeedUrl = config[$"{section}:FeedUrl"] ?? throw new ArgumentNullException($"{section}:FeedUrl"),
        PollingIntervalInMinutes = config.GetValue<int>($"{section}:PollingIntervalInMinutes", 1440),
        IgnoreNoEventHandlers = config.GetValue<bool>($"{section}:IgnoreNoEventHandlers", false)
    };
}

static TProjector CreateProjector<TProjector>(IServiceProvider provider, FeedProjectorOptions options)
    where TProjector : class, IHostedService
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(options.Name);
    var feedPageFetcher = new HttpFeedPageFetcher(httpClient, options.FeedUrl);

    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
    var jsonSchemaValidator = new JsonSchemaValidator(loggerFactory.CreateLogger<JsonSchemaValidator>());

    return ActivatorUtilities.CreateInstance<TProjector>(
        provider,
        options,
        provider.GetRequiredService<IDbContextFactory<FeedContext>>(),
        feedPageFetcher,
        jsonSchemaValidator,
        loggerFactory);
}

// Add near top-level statements in Program.cs (below usings is fine)
record FeedRegistration(
    string Section,
    string DefaultName,
    Func<IServiceProvider, FeedProjectorOptions, IHostedService> CreateHostedService);
