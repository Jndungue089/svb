using CommunityToolkit.Mvvm.ComponentModel;

namespace BeneditaUI.Models;

public partial class VotingPartyOption : ObservableObject
{
    public int EntityId { get; init; }
    public string Acronym { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    [ObservableProperty]
    private int _voteCount;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isDisabled;

    [ObservableProperty]
    private Color _cardColor = Color.FromArgb("#FFFFFF");

    [ObservableProperty]
    private Color _borderColor = Color.FromArgb("#E8DDD0");

    [ObservableProperty]
    private Color _titleColor = Color.FromArgb("#2C1810");
}
