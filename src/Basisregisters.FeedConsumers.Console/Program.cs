using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Basisregisters.FeedConsumers;
using Basisregisters.FeedConsumers.Console.Address;
using Basisregisters.FeedConsumers.Console.Building;
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

        var municipalityFeedOptions = new FeedProjectorOptions
        {
            Name = hostContext.Configuration["MunicipalityFeed:Name"] ?? "MunicipalityFeed",
            FeedUrl = hostContext.Configuration["MunicipalityFeed:FeedUrl"] ??
                      throw new ArgumentNullException("MunicipalityFeed:FeedUrl"),
            PollingIntervalInMinutes =
                hostContext.Configuration.GetValue<int>("MunicipalityFeed:PollingIntervalInMinutes", 1440),
            IgnoreNoEventHandlers =
                hostContext.Configuration.GetValue<bool>("MunicipalityFeed:IgnoreNoEventHandlers", false)
        };

        var postalInformationFeedOptions = new FeedProjectorOptions
        {
            Name = hostContext.Configuration["PostalInformationFeed:Name"] ?? "PostalInformationFeed",
            FeedUrl = hostContext.Configuration["PostalInformationFeed:FeedUrl"] ??
                      throw new ArgumentNullException("PostalInformationFeed:FeedUrl"),
            PollingIntervalInMinutes =
                hostContext.Configuration.GetValue<int>("PostalInformationFeed:PollingIntervalInMinutes", 1440),
            IgnoreNoEventHandlers =
                hostContext.Configuration.GetValue<bool>("PostalInformationFeed:IgnoreNoEventHandlers", false)
        };

        var streetNameFeedOptions = new FeedProjectorOptions
        {
            Name = hostContext.Configuration["StreetNameFeed:Name"] ?? "StreetNameFeed",
            FeedUrl = hostContext.Configuration["StreetNameFeed:FeedUrl"] ??
                      throw new ArgumentNullException("StreetNameFeed:FeedUrl"),
            PollingIntervalInMinutes =
                hostContext.Configuration.GetValue<int>("StreetNameFeed:PollingIntervalInMinutes", 1440),
            IgnoreNoEventHandlers =
                hostContext.Configuration.GetValue<bool>("StreetNameFeed:IgnoreNoEventHandlers", false)
        };

        var addressFeedOptions = new FeedProjectorOptions
        {
            Name = hostContext.Configuration["AddressFeed:Name"] ?? "AddressFeed",
            FeedUrl = hostContext.Configuration["AddressFeed:FeedUrl"] ??
                      throw new ArgumentNullException("AddressFeed:FeedUrl"),
            PollingIntervalInMinutes =
                hostContext.Configuration.GetValue<int>("AddressFeed:PollingIntervalInMinutes", 1440),
            IgnoreNoEventHandlers =
                hostContext.Configuration.GetValue<bool>("AddressFeed:IgnoreNoEventHandlers", false)
        };

        var buildingFeedOptions = new FeedProjectorOptions
        {
            Name = hostContext.Configuration["BuildingFeed:Name"] ?? "BuildingFeed",
            FeedUrl = hostContext.Configuration["BuildingFeed:FeedUrl"] ??
                      throw new ArgumentNullException("BuildingFeed:FeedUrl"),
            PollingIntervalInMinutes =
                hostContext.Configuration.GetValue<int>("BuildingFeed:PollingIntervalInMinutes", 1440),
            IgnoreNoEventHandlers =
                hostContext.Configuration.GetValue<bool>("BuildingFeed:IgnoreNoEventHandlers", false)
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

        services.AddHttpClient(postalInformationFeedOptions.Name, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        });

        services.AddHttpClient(streetNameFeedOptions.Name, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        });

        services.AddHttpClient(addressFeedOptions.Name, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        });

        services.AddHttpClient(buildingFeedOptions.Name, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        });

        services.AddHostedService(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(municipalityFeedOptions.Name);
            var feedPageFetcher = new HttpFeedPageFetcher(httpClient, municipalityFeedOptions.FeedUrl);
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var jsonSchemaValidator = new JsonSchemaValidator(loggerFactory.CreateLogger<JsonSchemaValidator>());
            return new MunicipalityProjector(
                municipalityFeedOptions,
                provider.GetRequiredService<IDbContextFactory<FeedContext>>(),
                feedPageFetcher,
                jsonSchemaValidator,
                loggerFactory);
        });

        services.AddHostedService(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(postalInformationFeedOptions.Name);
            var feedPageFetcher = new HttpFeedPageFetcher(httpClient, postalInformationFeedOptions.FeedUrl);
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var jsonSchemaValidator = new JsonSchemaValidator(loggerFactory.CreateLogger<JsonSchemaValidator>());
            return new PostalInformationProjector(
                postalInformationFeedOptions,
                provider.GetRequiredService<IDbContextFactory<FeedContext>>(),
                feedPageFetcher,
                jsonSchemaValidator,
                loggerFactory);
        });

        services.AddHostedService(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(streetNameFeedOptions.Name);
            var feedPageFetcher = new HttpFeedPageFetcher(httpClient, streetNameFeedOptions.FeedUrl);
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var jsonSchemaValidator = new JsonSchemaValidator(loggerFactory.CreateLogger<JsonSchemaValidator>());
            return new StreetNameProjector(
                streetNameFeedOptions,
                provider.GetRequiredService<IDbContextFactory<FeedContext>>(),
                feedPageFetcher,
                jsonSchemaValidator,
                loggerFactory);
        });

        services.AddHostedService(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(addressFeedOptions.Name);
            var feedPageFetcher = new HttpFeedPageFetcher(httpClient, addressFeedOptions.FeedUrl);
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var jsonSchemaValidator = new JsonSchemaValidator(loggerFactory.CreateLogger<JsonSchemaValidator>());
            return new AddressProjector(
                addressFeedOptions,
                provider.GetRequiredService<IDbContextFactory<FeedContext>>(),
                feedPageFetcher,
                jsonSchemaValidator,
                loggerFactory);
        });

        services.AddHostedService(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(buildingFeedOptions.Name);
            var feedPageFetcher = new HttpFeedPageFetcher(httpClient, buildingFeedOptions.FeedUrl);
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var jsonSchemaValidator = new JsonSchemaValidator(loggerFactory.CreateLogger<JsonSchemaValidator>());
            return new BuildingProjector(
                buildingFeedOptions,
                provider.GetRequiredService<IDbContextFactory<FeedContext>>(),
                feedPageFetcher,
                jsonSchemaValidator,
                loggerFactory);
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
