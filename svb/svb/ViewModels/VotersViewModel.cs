using BeneditaUI.Models;
using BeneditaUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BeneditaUI.ViewModels;

public partial class VotersViewModel : ObservableObject
{
    private readonly ApiService _api;

    public VotersViewModel(ApiService api) => _api = api;

    [ObservableProperty]
    private ObservableCollection<Voter> _voters = new();

    [ObservableProperty]
    private Voter? _selectedVoter;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _feedbackMessage = string.Empty;

    // ── Novo eleitor ──────────────────────────────────────────
    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newFingerId = string.Empty;

    // ── Load ──────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        var voters = await _api.GetVotersAsync();
        Voters.Clear();
        if (voters is not null)
            foreach (var v in voters)
                Voters.Add(v);
        IsLoading = false;
        FeedbackMessage = $"{Voters.Count} eleitor(es) carregado(s).";
    }

    // ── Register ──────────────────────────────────────────────

    [RelayCommand]
    public async Task RegisterAsync()
    {
        if (!int.TryParse(NewFingerId, out int fid))
        {
            FeedbackMessage = "⚠ ID biométrico inválido.";
            return;
        }
        if (string.IsNullOrWhiteSpace(NewName))
        {
            FeedbackMessage = "⚠ Nome obrigatório.";
            return;
        }

        IsLoading = true;
        var (ok, msg) = await _api.RegisterVoterAsync(fid, NewName.Trim());
        FeedbackMessage = ok ? $"✅ {msg}" : $"❌ {msg}";

        if (ok)
        {
            NewName     = string.Empty;
            NewFingerId = string.Empty;
            await LoadAsync();
        }
        IsLoading = false;
    }

    // ── Delete ────────────────────────────────────────────────

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedVoter is null)
        {
            FeedbackMessage = "⚠ Selecione um eleitor primeiro.";
            return;
        }

        bool confirm = await Shell.Current.DisplayAlert(
            "Confirmar",
            $"Remover eleitor '{SelectedVoter.Name}' (finger {SelectedVoter.FingerId})?",
            "Sim", "Não");

        if (!confirm) return;

        IsLoading = true;
        var (ok, msg) = await _api.DeleteVoterAsync(SelectedVoter.FingerId);
        FeedbackMessage = ok ? $"✅ {msg}" : $"❌ {msg}";

        if (ok) await LoadAsync();
        IsLoading = false;
    }
}
