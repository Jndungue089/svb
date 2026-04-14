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

    [ObservableProperty]
    private Color _feedbackColor = Colors.Gray;

    [ObservableProperty]
    private bool _hasFeedback;

    // ── Formulário Passo 1 ────────────────────────────────────
    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newBI = string.Empty;

    // ── Aviso de duplicado em tempo real ─────────────────────
    [ObservableProperty]
    private string _duplicateWarning = string.Empty;

    [ObservableProperty]
    private bool _hasDuplicateWarning;

    // ── Enrolamento Passo 2 ───────────────────────────────────
    [ObservableProperty]
    private bool _isEnrolling;

    [ObservableProperty]
    private string _enrollMessage = string.Empty;

    public bool CanEnrollSelected =>
        SelectedVoter is not null && !SelectedVoter.HasFingerprint && !IsEnrolling;

    partial void OnSelectedVoterChanged(Voter? value) =>
        OnPropertyChanged(nameof(CanEnrollSelected));

    partial void OnIsEnrollingChanged(bool value) =>
        OnPropertyChanged(nameof(CanEnrollSelected));

    // Verificação em tempo real conforme o utilizador escreve
    partial void OnNewBIChanged(string value) => CheckDuplicates();
    partial void OnNewNameChanged(string value) => CheckDuplicates();

    private void CheckDuplicates()
    {
        if (!string.IsNullOrWhiteSpace(NewBI) &&
            Voters.Any(v => v.BI.Trim().ToLower() == NewBI.Trim().ToLower()))
        {
            DuplicateWarning   = $"Já existe um eleitor com o BI «{NewBI.Trim()}».";
            HasDuplicateWarning = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(NewName) &&
            Voters.Any(v => v.Name.Trim().ToLower() == NewName.Trim().ToLower()))
        {
            DuplicateWarning   = $"Já existe um eleitor com o nome «{NewName.Trim()}».";
            HasDuplicateWarning = true;
            return;
        }

        DuplicateWarning    = string.Empty;
        HasDuplicateWarning = false;
    }

    private void SetFeedback(string msg, bool isError)
    {
        FeedbackMessage = msg;
        FeedbackColor   = isError
            ? Color.FromArgb("#1D4ED8")
            : Color.FromArgb("#1E90FF");
        HasFeedback = !string.IsNullOrEmpty(msg);
    }

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
        SetFeedback($"{Voters.Count} eleitor(es) carregado(s).", false);
        CheckDuplicates();
    }

    // ── Passo 1: Cadastrar (Nome + BI, SEM impressão digital) ─

    [RelayCommand]
    public async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            SetFeedback("Nome completo é obrigatório.", true);
            return;
        }
        if (string.IsNullOrWhiteSpace(NewBI))
        {
            SetFeedback("Número do BI é obrigatório.", true);
            return;
        }

        // Verificação local anti-duplicado
        if (Voters.Any(v => v.BI.Trim().ToLower() == NewBI.Trim().ToLower()))
        {
            SetFeedback($"Já existe um eleitor registado com o BI «{NewBI.Trim()}».", true);
            return;
        }
        if (Voters.Any(v => v.Name.Trim().ToLower() == NewName.Trim().ToLower()))
        {
            SetFeedback($"Já existe um eleitor com o nome «{NewName.Trim()}».", true);
            return;
        }

        IsLoading = true;
        var (ok, msg, voter) = await _api.RegisterVoterAsync(NewName.Trim(), NewBI.Trim());
        SetFeedback(ok ? $"Eleitor «{NewName.Trim()}» cadastrado com sucesso." : msg, !ok);

        if (ok)
        {
            NewName = string.Empty;
            NewBI   = string.Empty;
            await LoadAsync();
        }
        IsLoading = false;
    }

    // ── Passo 2: Enrolar impressão digital ────────────────────

    [RelayCommand]
    public async Task EnrollFingerprintAsync()
    {
        if (SelectedVoter is null) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Registar Impressão Digital",
            $"Peça ao eleitor «{SelectedVoter.Name}» para colocar o dedo no sensor quando solicitado pelo ecrã.\n\nDeseja continuar?",
            "Sim, iniciar", "Cancelar");

        if (!confirm) return;

        IsEnrolling   = true;
        EnrollMessage = "A aguardar — coloque o dedo no sensor...";
        SetFeedback(string.Empty, false);
        HasFeedback = false;

        var (ok, msg, updatedVoter) = await _api.EnrollFingerAsync(SelectedVoter.Id);

        if (ok && updatedVoter is not null)
        {
            var idx = Voters.IndexOf(SelectedVoter);
            if (idx >= 0) Voters[idx] = updatedVoter;
            SelectedVoter = updatedVoter;
            SetFeedback($"Impressão digital de «{updatedVoter.Name}» registada no slot {updatedVoter.FingerId}.", false);
        }
        else
        {
            SetFeedback(msg, true);
        }

        EnrollMessage = string.Empty;
        IsEnrolling   = false;
    }

    // ── Delete ────────────────────────────────────────────────

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedVoter is null)
        {
            SetFeedback("Selecione um eleitor na lista primeiro.", true);
            return;
        }

        bool confirm = await Shell.Current.DisplayAlert(
            "Remover Eleitor",
            $"Tem a certeza que deseja remover «{SelectedVoter.Name}» (BI: {SelectedVoter.BI})?\n\nEsta ação não pode ser desfeita.",
            "Sim, remover", "Cancelar");

        if (!confirm) return;

        IsLoading = true;
        var (ok, msg) = await _api.DeleteVoterAsync(SelectedVoter.Id);
        SetFeedback(ok ? $"Eleitor «{SelectedVoter.Name}» removido com sucesso." : msg, !ok);

        if (ok) await LoadAsync();
        IsLoading = false;
    }
}

