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
    private Color _cardColor = Color.FromArgb("#0F1A33");

    [ObservableProperty]
    private Color _borderColor = Color.FromArgb("#24406B");

    [ObservableProperty]
    private Color _titleColor = Color.FromArgb("#FFFFFF");
}

