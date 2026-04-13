using BeneditaUI.Models;
using BeneditaUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BeneditaUI.ViewModels;

public partial class VotingViewModel : ObservableObject
{
    private readonly ApiService _api;

    public VotingViewModel(ApiService api) => _api = api;

    [ObservableProperty]
    private ObservableCollection<VotingPartyOption> _partyOptions = new();

    [ObservableProperty]
    private VotingPartyOption? _selectedParty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _identificationPassed;

    [ObservableProperty]
    private string _identificationMessage = "Clique em Ler impressão digital para identificar o eleitor.";

    [ObservableProperty]
    private Color _identificationMessageColor = Colors.Gray;

    [ObservableProperty]
    private bool _hasCompletedSession;

    [ObservableProperty]
    private string _sessionResultTitle = string.Empty;

    [ObservableProperty]
    private string _sessionResultMessage = string.Empty;

    [ObservableProperty]
    private Color _sessionResultColor = Colors.Gray;

    [ObservableProperty]
    private string _scannedVoterName = string.Empty;

    [ObservableProperty]
    private string _partyInfoMessage = string.Empty;

    private int _scannedFingerId;

    public bool HasSelectedParty => SelectedParty is not null;

    public bool CanSubmitIdentification =>
        !IsLoading &&
        !IsScanning &&
        !IdentificationPassed;

    public bool CanSelectParty =>
        IdentificationPassed &&
        !IsLoading &&
        !IsScanning &&
        !HasCompletedSession;

    public bool CanConfirmOrCancel =>
        IdentificationPassed &&
        SelectedParty is not null &&
        !HasCompletedSession &&
        !IsLoading &&
        !IsScanning &&
        _scannedFingerId > 0;

    partial void OnSelectedPartyChanged(VotingPartyOption? value)
    {
        UpdatePartyVisualState();
        RaiseComputed();
    }

    partial void OnIsLoadingChanged(bool value) => RaiseComputed();
    partial void OnIsScanningChanged(bool value) => RaiseComputed();
    partial void OnIdentificationPassedChanged(bool value) => RaiseComputed();
    partial void OnHasCompletedSessionChanged(bool value) => RaiseComputed();

    private void RaiseComputed()
    {
        OnPropertyChanged(nameof(CanSubmitIdentification));
        OnPropertyChanged(nameof(CanSelectParty));
        OnPropertyChanged(nameof(CanConfirmOrCancel));
        OnPropertyChanged(nameof(HasSelectedParty));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;

        var entities = await _api.GetEntitiesAsync() ?? new List<Entity>();
        var results = await _api.GetResultsAsync() ?? new List<VoteResult>();

        var topThree = entities.Take(3).ToList();

        PartyOptions.Clear();
        foreach (var entity in topThree)
        {
            var count = results.FirstOrDefault(r => r.EntityId == entity.Id)?.Count ?? 0;
            PartyOptions.Add(new VotingPartyOption
            {
                EntityId = entity.Id,
                Name = entity.Name,
                Acronym = entity.Acronym,
                VoteCount = count
            });
        }

        PartyInfoMessage = PartyOptions.Count switch
        {
            0 => "Nenhum partido configurado. Cadastre 3 entidades para votação.",
            < 3 => $"Foram carregados {PartyOptions.Count} partido(s). O ideal são 3.",
            _ => "3 partidos disponíveis para seleção por toque ou por botões."
        };

        ResetSessionState(keepIdentificationMessage: false);
        IsLoading = false;
    }

    [RelayCommand]
    public async Task SubmitIdentificationAsync()
    {
        if (!CanSubmitIdentification)
            return;

        IsScanning = true;
        IdentificationMessage = "Aguardando leitura da impressão digital...";
        IdentificationMessageColor = Colors.Orange;

        var (ok, message, fingerId, voterName) = await _api.IdentifyVoterAsync();

        IsScanning = false;
        if (!ok)
        {
            IdentificationPassed = false;
            IdentificationMessage = message;
            IdentificationMessageColor = Colors.Crimson;
            return;
        }

        _scannedFingerId = fingerId;
        ScannedVoterName = voterName;
        IdentificationPassed = true;
        IdentificationMessage = $"Eleitor identificado: {voterName}. Selecione a entidade.";
        IdentificationMessageColor = Colors.SeaGreen;

        SessionResultTitle = "Eleitor validado";
        SessionResultMessage = $"{voterName} identificado por biometria. Escolha a entidade e confirme o voto.";
        SessionResultColor = Colors.DodgerBlue;
    }

    [RelayCommand]
    public void SelectParty(VotingPartyOption? option)
    {
        if (option is null || !CanSelectParty || option.IsDisabled)
            return;

        SelectedParty = option;
    }

    [RelayCommand]
    public async Task ConfirmVoteAsync()
    {
        if (!CanConfirmOrCancel || SelectedParty is null)
            return;

        IsLoading = true;
        var (ok, message) = await _api.ConfirmVoteAsync(_scannedFingerId, SelectedParty.EntityId);
        IsLoading = false;

        if (!ok)
        {
            SessionResultTitle = "Falha ao confirmar";
            SessionResultMessage = message;
            SessionResultColor = Colors.Crimson;
            return;
        }

        HasCompletedSession = true;
        SessionResultTitle = "Voto confirmado";
        SessionResultMessage = $"{message} Foi adicionado +1 ao partido {SelectedParty.Acronym} para {ScannedVoterName}.";
        SessionResultColor = Colors.SeaGreen;

        await RefreshVoteCountsAsync();
    }

    [RelayCommand]
    public async Task CancelVoteAsync()
    {
        if (!CanConfirmOrCancel)
            return;

        IsLoading = true;
        var (ok, message) = await _api.CancelVoteAsync();
        IsLoading = false;

        HasCompletedSession = true;
        SessionResultTitle = "Voto cancelado";
        SessionResultMessage = ok
            ? "Voto cancelado. Nenhum partido recebeu +1 voto."
            : $"Voto cancelado localmente. Aviso da API: {message}";
        SessionResultColor = Colors.DarkOrange;
    }

    [RelayCommand]
    public void NewSession()
    {
        ResetSessionState(keepIdentificationMessage: false);
    }

    private void ResetSessionState(bool keepIdentificationMessage)
    {
        SelectedParty = null;
        _scannedFingerId = 0;
        ScannedVoterName = string.Empty;
        HasCompletedSession = false;

        SessionResultTitle = string.Empty;
        SessionResultMessage = string.Empty;
        SessionResultColor = Colors.Gray;

        IdentificationPassed = false;

        if (!keepIdentificationMessage)
        {
            IdentificationMessage = "Clique em Ler impressão digital para identificar o eleitor.";
            IdentificationMessageColor = Colors.Gray;
        }

        UpdatePartyVisualState();
    }

    private async Task RefreshVoteCountsAsync()
    {
        var results = await _api.GetResultsAsync();
        if (results is null)
            return;

        foreach (var option in PartyOptions)
        {
            option.VoteCount = results.FirstOrDefault(r => r.EntityId == option.EntityId)?.Count ?? option.VoteCount;
        }
    }

    private void UpdatePartyVisualState()
    {
        foreach (var option in PartyOptions)
        {
            var isSelected = SelectedParty?.EntityId == option.EntityId;
            option.IsSelected = isSelected;

            // Após selecionar, apenas o partido escolhido fica ativo.
            option.IsDisabled = SelectedParty is not null && !isSelected;

            if (isSelected)
            {
                option.CardColor = Color.FromArgb("#F5EAE5");
                option.BorderColor = Color.FromArgb("#6B3A2A");
                option.TitleColor = Color.FromArgb("#6B3A2A");
            }
            else if (option.IsDisabled)
            {
                option.CardColor = Color.FromArgb("#F3F3F3");
                option.BorderColor = Color.FromArgb("#D1D1D1");
                option.TitleColor = Color.FromArgb("#A3A3A3");
            }
            else
            {
                option.CardColor = Color.FromArgb("#FFFFFF");
                option.BorderColor = Color.FromArgb("#E8DDD0");
                option.TitleColor = Color.FromArgb("#2C1810");
            }
        }
    }
}
