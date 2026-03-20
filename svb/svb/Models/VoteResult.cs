namespace BeneditaUI.Models;

/// <summary>Mapeado do endpoint GET /vote/results (Dictionary&lt;string,int&gt;).</summary>
public class VoteResult
{
    public string Option { get; set; } = string.Empty;
    public int    Count  { get; set; }

    // Percentagem calculada na VM
    public double Percent { get; set; }
    public string Label   => $"{Option}  —  {Count} voto(s)  ({Percent:0.0}%)";
}
