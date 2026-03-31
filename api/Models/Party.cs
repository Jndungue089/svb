namespace BeneditaApi.Models;

public class Entity
{
    public int Id { get; set; }

    /// <summary>Nome completo da entidade (partido, coligação, etc.).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sigla da entidade (max 8 chars — usada no LCD do ESP32).</summary>
    public string Acronym { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Navegação
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
