using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsoleApp;

public class JobCategory
{

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("id")]
    public required int Id { get; set; }
}

internal class JobCategoryParser
{
    public static List<JobCategory> ParseCategories()
    {
        return JsonSerializer.Deserialize<List<JobCategory>>(File.ReadAllText(".//jobs-categories.json"))!;
    }
}

