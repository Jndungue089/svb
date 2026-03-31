using System.Text.Json.Serialization;

namespace BeneditaUI.Models;

public class Voter
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bi")]
    public string BI { get; set; } = string.Empty;

    [JsonPropertyName("fingerId")]
    public int? FingerId { get; set; }

    [JsonPropertyName("canVote")]
    public bool CanVote { get; set; }

    [JsonPropertyName("registeredAt")]
    public DateTime RegisteredAt { get; set; }

    [JsonPropertyName("vote")]
    public VoteDto? Vote { get; set; }

    public bool HasFingerprint => FingerId.HasValue;

    public string StatusLabel => !HasFingerprint
        ? "Sem Digital"
        : CanVote ? "Pendente" : "Votou";

    public Color StatusColor => !HasFingerprint
        ? Colors.DarkOrange
        : CanVote ? Colors.DodgerBlue : Colors.SeaGreen;
}

public class VoteDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("entityId")]
    public int EntityId { get; set; }

    [JsonPropertyName("castAt")]
    public DateTime CastAt { get; set; }

    [JsonPropertyName("entity")]
    public EntityDto? Entity { get; set; }
}

public class EntityDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("acronym")]
    public string Acronym { get; set; } = string.Empty;
}
