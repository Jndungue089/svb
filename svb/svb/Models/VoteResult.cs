using System.Text.Json.Serialization;

namespace BeneditaUI.Models;

public class VoteResult
{
    [JsonPropertyName("entityId")]
    public int EntityId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("acronym")]
    public string Acronym { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("percent")]
    public double Percent { get; set; }

    public string Label => $"{Acronym}  —  {Count} voto(s)  ({Percent:0.0}%)";
}
