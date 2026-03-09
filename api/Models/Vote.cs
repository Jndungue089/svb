namespace BeneditaApi.Models;

public class Vote
{
    public int Id { get; set; }

    public int FingerId { get; set; }

    /// <summary>Ex: "OPCAO_A", "OPCAO_B"…</summary>
    public string Option { get; set; } = string.Empty;

    public DateTime CastAt { get; set; } = DateTime.UtcNow;

    // FK
    public int VoterId { get; set; }
    public Voter Voter { get; set; } = null!;
}
