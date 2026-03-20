using System.Text.Json.Serialization;

namespace BeneditaUI.Models;

public class Voter
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fingerId")]
    public int FingerId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("canVote")]
    public bool CanVote { get; set; }

    [JsonPropertyName("registeredAt")]
    public DateTime RegisteredAt { get; set; }

    [JsonPropertyName("vote")]
    public VoteDto? Vote { get; set; }

    // UI helper
    public string StatusLabel => CanVote ? "Pendente" : "Votou";
    public Color  StatusColor => CanVote ? Colors.DodgerBlue : Colors.SeaGreen;
}

public class VoteDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("option")]
    public string Option { get; set; } = string.Empty;

    [JsonPropertyName("castAt")]
    public DateTime CastAt { get; set; }
}
