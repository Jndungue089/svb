namespace BeneditaApi.Models;

public class Vote
{
    public int Id { get; set; }

    public DateTime CastAt { get; set; } = DateTime.UtcNow;

    // FK → Voter
    public int VoterId { get; set; }
    public Voter Voter { get; set; } = null!;

    // FK → Entity
    public int EntityId { get; set; }
    public Entity Entity { get; set; } = null!;
}
