// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text.Json;
using AngleSharp;
using Microsoft.Extensions.Logging;

namespace ConsoleApp;

public class JobDetailsProvider(IHttpClientFactory clientFactory, ILogger<JobDetailsProvider> logger)
{
    public async Task ProcessAsync(CancellationToken ct)
    {
        var baseInputDir = @"d:\Data\PracujPL\Listing\";
        var baseOutputDir = @"d:\Data\PracujPL\Offers\";

        var waitingPeriod = TimeSpan.FromSeconds(5);

        var httpClient = clientFactory.CreateClient("Pracuj");

        var config = AngleSharp.Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);

        var enumerateDirectories = Directory.EnumerateDirectories(baseInputDir).ToList();
        foreach ((var categoryDir, var index) in enumerateDirectories.Select((categoryDir, index) =>
                     (categoryDir, index)))
        {
            var dirName = Path.GetFileName(categoryDir)!;
            int lastIndexOfDash = dirName.LastIndexOf("-", StringComparison.Ordinal);
            var jobCategory = new JobCategory
            {
                Id = int.Parse(dirName.Substring(lastIndexOfDash + 1).Trim()),
                Name = dirName.Substring(0, lastIndexOfDash)
            };

            logger.LogInformation("\tMain category {index} of {dirsCount} - {categoryName}", index,
                enumerateDirectories.Count, jobCategory.Name);

            var categoryOutputDir = Path.Join(baseOutputDir, $"{jobCategory.Name} - {jobCategory.Id}");
            Directory.CreateDirectory(categoryOutputDir);

            var listingFiles = new DirectoryInfo(categoryDir).GetFiles("*.json").ToList();
            foreach (var (listingFile, pageIndex) in listingFiles.Select((listingFile, pageIndex) =>
                         (listingFile, pageIndex)))
            {
                logger.LogInformation("\t\tCategory page {index} of {filesCount} - {categoryName}", pageIndex,
                    listingFiles.Count, jobCategory.Name);

                await using var fileStream = File.OpenRead(listingFile.FullName);
                ;
                using var listingDoc = await JsonDocument.ParseAsync(fileStream, default, ct);

                JsonElement jobOffersElement = listingDoc.RootElement.GetProperty("props").GetProperty("pageProps")
                    .GetProperty("data").GetProperty("jobOffers");

                var jobOffersElements = jobOffersElement
                    .GetProperty("groupedOffers").EnumerateArray().ToList();

                foreach ((JsonElement mainOfferElement, int offerIndex) in jobOffersElements.Select(
                             (mainOfferElement, offerIndex) => (mainOfferElement, offerIndex)))
                {
                    var subOfferElement = mainOfferElement.GetProperty("offers").EnumerateArray().First();

                    var offerId = subOfferElement.GetProperty("partitionId").GetInt32();
                    var url = subOfferElement.GetProperty("offerAbsoluteUri").GetString()!.Replace("https://www.pracuj.pl", "");

                    try
                    {
                        logger.LogInformation("\t\t\tFetching {offerIndex} of {offersCount} ({offerId}) from {url}",
                            offerIndex, jobOffersElements.Count, offerId, url);

                        var detailsOutputFilePath = Path.Join(categoryOutputDir, $"{offerId}-details.json");
                        if (Path.Exists(detailsOutputFilePath))
                        {
                            logger.LogInformation("{offerId} already downloaded. Skipping", offerId);
                            continue;
                        }

                        HttpResponseMessage response;
                        var maxRetry = 5;
                        var currentRetry = 1;
                        bool hasToBeRetried = false;
                        do
                        {
                            response = await httpClient.GetAsync(url, ct);

                            hasToBeRetried = response.IsSuccessStatusCode is false;
                            if (hasToBeRetried)
                            {
                                httpClient.Dispose();
                                httpClient = clientFactory.CreateClient("Pracuj");
                                currentRetry++;
                                var delayTimeout = waitingPeriod * currentRetry;
                                logger.LogWarning("#{retry} retry. Waiting {delay}s", currentRetry,
                                    delayTimeout.TotalSeconds);
                                await Task.Delay(delayTimeout, ct);
                                logger.LogInformation("Retrying...");
                            }

                        } while (hasToBeRetried && currentRetry <= maxRetry);

                        var html = await response.Content.ReadAsStringAsync(ct);

                        if (response.IsSuccessStatusCode is false)
                        {
                            throw new WebException($"Error code:{response.StatusCode}. Error: {html}");
                        }

                        var document = await context.OpenAsync(req => req.Content(html), cancel: ct);

                        var scriptElement = document.QuerySelector("script#__NEXT_DATA__");

                        if (scriptElement != null)
                        {
                            var detailsDoc = JsonDocument.Parse(scriptElement.TextContent);

                            var jobDetailsElem = detailsDoc.RootElement.GetProperty("props").GetProperty("pageProps")
                                .GetProperty("dehydratedState").GetProperty("queries").EnumerateArray().First().GetProperty("state").GetProperty("data");


                            await File.WriteAllTextAsync(detailsOutputFilePath, jobDetailsElem.JsonPrettify(), ct);
                        }

                        var listingOutputFilePath = Path.Join(categoryOutputDir, $"{offerId}-listing.json");
                        await File.WriteAllTextAsync(listingOutputFilePath, mainOfferElement.JsonPrettify(), ct);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Error during offer {offerId}", offerId);
                    }
                }
            }
        }

        httpClient.Dispose();
    }
}
