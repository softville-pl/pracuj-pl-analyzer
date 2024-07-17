// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text.Json;
using AngleSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConsoleApp;

internal class JobListingProvider(
    ILogger<JobListingProvider> logger,
    IHttpClientFactory clientFactory,
    IOptions<Configuration> config) : IJobsListingProvider
{
    public async Task ProcessAsync()
    {
        using (var client = clientFactory.CreateClient("Pracuj"))
        {
            var baseOutputDir = @"d:\Data\PracujPL\Listing\";

            foreach (JobCategory jobCategory in JobCategoryParser.ParseCategories().OrderBy(c => c.Id))
            {
                var categoryOutputDir = Path.Join(baseOutputDir, $"{jobCategory.Name} - {jobCategory.Id}");

                Directory.CreateDirectory(categoryOutputDir);

                logger.LogInformation($"'{jobCategory.Name}' ({jobCategory.Id}) fetching...");

                var page = 1;
                var processedJobsCount = 0;
                var offersTotalCount = 0;
                do
                {
                    var htmlPageOutputPath = Path.Join(categoryOutputDir, $"{jobCategory.Id}-{page}.html");
                    var jsonPageOutputPath = Path.Join(categoryOutputDir, $"{jobCategory.Id}-{page}.json");

                    try
                    {
                        logger.LogInformation("Getting page: {page} of {jobCategoryName} ({jobCategoryId})", page,
                            jobCategory.Name, jobCategory.Id);


                        // var t = "https://www.pracuj.pl/praca/hotelarstwo%20gastronomia%20turystyka;cc,5010?%3Fpn=1'

                        var categotyUrlPath = jobCategory.Name.Replace(" & ", "%20").Replace(" - ", "%20-%20").Replace(" ", "%20").ToLower();

                        var url = $"/praca/{categotyUrlPath};cc,{jobCategory.Id}?pn={page}";
                        var response = await client.GetAsync(url);

                        string html = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode is false)
                        {
                            throw new WebException(
                                $"Code: {response.StatusCode}. Response: {html}");
                        }

                        await File.WriteAllTextAsync(htmlPageOutputPath, html);

                        var config = AngleSharp.Configuration.Default.WithDefaultLoader();
                        var context = BrowsingContext.New(config);

                        var document = await context.OpenAsync(req => req.Content(html));

                        var scriptElement = document.QuerySelector("script#__NEXT_DATA__");

                        if (scriptElement != null)
                        {
                            using JsonDocument jDoc = JsonDocument.Parse(scriptElement.TextContent);

                            JsonElement jobOffersElement = jDoc.RootElement.GetProperty("props").GetProperty("pageProps")
                                .GetProperty("data").GetProperty("jobOffers");

                            offersTotalCount =
                                jobOffersElement.GetProperty("groupedOffersTotalCount").GetInt32();

                            var jobOffersElements = jobOffersElement
                                .GetProperty("groupedOffers").EnumerateArray().ToList();

                            await File.WriteAllTextAsync(jsonPageOutputPath, jDoc.JsonPrettify());

                            processedJobsCount += jobOffersElements.Count;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Error fetching {page} of {jobCategoryName} ({jobCategoryId})", page,
                            jobCategory.Name, jobCategory.Id);
                    }
                    page++;

                } while (processedJobsCount < offersTotalCount);
            }
        }


        logger.LogInformation("Configuration {configKey}: {configValue}", nameof(Configuration.ConfigName),
            config.Value.ConfigName);
    }
}
