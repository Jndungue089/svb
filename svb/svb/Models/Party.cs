using System.Text.Json.Serialization;

namespace BeneditaUI.Models;

public class Entity
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("acronym")]
    public string Acronym { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("votes")]
    public List<object> Votes { get; set; } = new();

    public int VoteCount => Votes.Count;
}
