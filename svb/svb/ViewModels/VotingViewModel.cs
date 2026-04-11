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
    private string _biInput = string.Empty;

    [ObservableProperty]
    private string _voterCardInput = string.Empty;

    [ObservableProperty]
    private bool _identificationPassed;

    [ObservableProperty]
    private string _identificationMessage = "Informe BI e cartão para iniciar.";

    [ObservableProperty]
    private Color _identificationMessageColor = Colors.Gray;

    [ObservableProperty]
    private bool _isAwaitingConfirmation;

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
        !IdentificationPassed &&
        !string.IsNullOrWhiteSpace(BiInput) &&
        !string.IsNullOrWhiteSpace(VoterCardInput);

    public bool CanSelectParty =>
        IdentificationPassed &&
        !IsLoading &&
        !IsScanning &&
        !IsAwaitingConfirmation &&
        !HasCompletedSession;

    public bool CanStartBiometric => CanSelectParty && SelectedParty is not null;

    public bool CanConfirmOrCancel =>
        IsAwaitingConfirmation &&
        !IsLoading &&
        !IsScanning &&
        _scannedFingerId > 0;

    partial void OnSelectedPartyChanged(VotingPartyOption? value)
    {
        UpdatePartyVisualState();
        RaiseComputed();
    }

    partial void OnBiInputChanged(string value) => RaiseComputed();
    partial void OnVoterCardInputChanged(string value) => RaiseComputed();
    partial void OnIsLoadingChanged(bool value) => RaiseComputed();
    partial void OnIsScanningChanged(bool value) => RaiseComputed();
    partial void OnIdentificationPassedChanged(bool value) => RaiseComputed();
    partial void OnIsAwaitingConfirmationChanged(bool value) => RaiseComputed();
    partial void OnHasCompletedSessionChanged(bool value) => RaiseComputed();

    private void RaiseComputed()
    {
        OnPropertyChanged(nameof(CanSubmitIdentification));
        OnPropertyChanged(nameof(CanSelectParty));
        OnPropertyChanged(nameof(CanStartBiometric));
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

        ResetSessionState(clearInputs: true, keepIdentificationMessage: false);
        IsLoading = false;
    }

    [RelayCommand]
    public async Task SubmitIdentificationAsync()
    {
        if (!CanSubmitIdentification)
            return;

        IsLoading = true;

        var voters = await _api.GetVotersAsync();
        if (voters is null)
        {
            IsLoading = false;
            IdentificationMessage = "Não foi possível validar o BI na API.";
            IdentificationMessageColor = Colors.Crimson;
            return;
        }

        var voter = voters.FirstOrDefault(v =>
            string.Equals(v.BI.Trim(), BiInput.Trim(), StringComparison.OrdinalIgnoreCase));

        if (voter is null)
        {
            IsLoading = false;
            IdentificationMessage = "BI não encontrado no cadastro.";
            IdentificationMessageColor = Colors.Crimson;
            return;
        }

        if (!voter.CanVote)
        {
            IsLoading = false;
            IdentificationMessage = "Este eleitor já votou e não pode votar novamente.";
            IdentificationMessageColor = Colors.Crimson;
            return;
        }

        IdentificationPassed = true;
        IdentificationMessage = "Identificação validada. Agora escolha o partido.";
        IdentificationMessageColor = Colors.SeaGreen;
        IsLoading = false;
    }

    [RelayCommand]
    public void SelectParty(VotingPartyOption? option)
    {
        if (option is null || !CanSelectParty || option.IsDisabled)
            return;

        SelectedParty = option;
    }

    [RelayCommand]
    public async Task StartBiometricAsync()
    {
        if (!CanStartBiometric || SelectedParty is null)
            return;

        IsScanning = true;
        SessionResultTitle = "Leitura biométrica";
        SessionResultMessage = "Aguardando leitura do sensor...";
        SessionResultColor = Colors.Orange;

        var (ok, message, fingerId, voterName) = await _api.ScanVoterAsync(BiInput.Trim(), VoterCardInput.Trim());

        IsScanning = false;
        if (!ok)
        {
            SessionResultTitle = "Falha na biometria";
            SessionResultMessage = message;
            SessionResultColor = Colors.Crimson;
            return;
        }

        _scannedFingerId = fingerId;
        ScannedVoterName = voterName;
        IsAwaitingConfirmation = true;
        SessionResultTitle = "Confirmar voto";
        SessionResultMessage = $"Biometria de {voterName} validada. Clique em Confirmar para registrar +1 em {SelectedParty.Acronym} ou em Cancelar para abortar.";
        SessionResultColor = Colors.DodgerBlue;
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

        IsAwaitingConfirmation = false;
        HasCompletedSession = true;
        SessionResultTitle = "Voto confirmado";
        SessionResultMessage = $"{message} Foi adicionado +1 ao partido {SelectedParty.Acronym}.";
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

        IsAwaitingConfirmation = false;
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
        ResetSessionState(clearInputs: true, keepIdentificationMessage: false);
    }

    private void ResetSessionState(bool clearInputs, bool keepIdentificationMessage)
    {
        SelectedParty = null;
        _scannedFingerId = 0;
        ScannedVoterName = string.Empty;
        IsAwaitingConfirmation = false;
        HasCompletedSession = false;

        SessionResultTitle = string.Empty;
        SessionResultMessage = string.Empty;
        SessionResultColor = Colors.Gray;

        if (clearInputs)
        {
            BiInput = string.Empty;
            VoterCardInput = string.Empty;
        }

        IdentificationPassed = false;

        if (!keepIdentificationMessage)
        {
            IdentificationMessage = "Informe BI e cartão para iniciar.";
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
