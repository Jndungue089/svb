namespace BeneditaApi.Models;

public class Voter
{
    public int Id { get; set; }

    /// <summary>ID retornado pelo sensor biométrico (slot 1-127).</summary>
    public int FingerId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Indica se este eleitor ainda pode votar.</summary>
    public bool CanVote { get; set; } = true;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    // Navegação
    public Vote? Vote { get; set; }
}
