// See https://aka.ms/new-console-template for more information


using ConsoleApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

//To set value via EnvVar
//Environment.SetEnvironmentVariable("EXAMPLE__ConfigName", "MyExecOverwrittenValue");

using var host = CreateHostBuilder(args).Build();
await host.StartAsync();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("App arguments: '{args}'", string.Join(Environment.NewLine, args));

if(args.Any(arg => arg.Equals("listing", StringComparison.OrdinalIgnoreCase)))
    await host.Services.GetRequiredService<IJobsListingProvider>().ProcessAsync();

if(args.Any(arg => arg.Equals("details", StringComparison.OrdinalIgnoreCase)))
    await host.Services.GetRequiredService<JobDetailsProvider>().ProcessAsync(CancellationToken.None);

Console.WriteLine("Press any key to continue");
Console.ReadKey();

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder
                .AddJsonFile("appSettings.json", optional: false)
                .AddJsonFile($"appSettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables("EXAMPLE:"); //https://learn.microsoft.com/en-us/dotnet/core/compatibility/extensions/7.0/environment-variable-prefixn
        })
        .UseConsoleLifetime()
        .ConfigureLogging((_, builder) => builder
            .AddConsole()
            .AddFilter("Microsoft.Hosting", LogLevel.Warning)
            .AddFilter("System.Net.Http", LogLevel.Warning)
            .SetMinimumLevel(LogLevel.Information))
        .ConfigureServices((context, services) =>
        {
            services.AddHttpClient<HttpClient>("Pracuj",httpClient =>
            {
                httpClient.BaseAddress = new Uri("https://www.pracuj.pl/");
                httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36");
            });
            services.AddSingleton<IJobsListingProvider, JobListingProvider>();
            services.AddSingleton<JobDetailsProvider>();
            services.Configure<Configuration>(context.Configuration);
        });
