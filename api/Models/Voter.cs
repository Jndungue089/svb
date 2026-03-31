namespace BeneditaApi.Models;

public class Voter
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Número do Bilhete de Identidade.</summary>
    public string BI { get; set; } = string.Empty;

    /// <summary>ID do slot no sensor biométrico (1-127). Nulo até ao registo da impressão digital.</summary>
    public int? FingerId { get; set; }

    /// <summary>Indica se este eleitor ainda pode votar.</summary>
    public bool CanVote { get; set; } = true;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    // Navegação
    public Vote? Vote { get; set; }
}
