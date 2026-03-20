using BeneditaUI.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeneditaUI.Services;

/// <summary>
/// Cliente HTTP para a API C# (BeneditaApi).
/// Base URL configurada em Configurações → salva em Preferences.
/// </summary>
public class ApiService
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiService(HttpClient http) => _http = http;

    // ── VOTERS ────────────────────────────────────────────────

    public async Task<List<Voter>?> GetVotersAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<Voter>>("voters", _json);
        }
        catch { return null; }
    }

    public async Task<(bool Ok, string Message)> RegisterVoterAsync(int fingerId, string name)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("voters", new { fingerId, name });
            if (res.IsSuccessStatusCode) return (true, "Eleitor cadastrado!");
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"Erro {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Ok, string Message)> DeleteVoterAsync(int fingerId)
    {
        try
        {
            var res = await _http.DeleteAsync($"voters/{fingerId}");
            return res.IsSuccessStatusCode
                ? (true, "Eleitor removido.")
                : (false, $"Erro {(int)res.StatusCode}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── AUTH (teste manual) ────────────────────────────────────

    public async Task<(bool Authorized, string Reason)> AuthAsync(int fingerId)
    {
        try
        {
            var res  = await _http.PostAsJsonAsync("auth", new { fingerId });
            var body = await res.Content.ReadFromJsonAsync<AuthResponse>(_json);
            return (body?.Autorizado ?? false, body?.Motivo ?? "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── VOTE RESULTS ──────────────────────────────────────────

    public async Task<List<VoteResult>?> GetResultsAsync()
    {
        try
        {
            var raw = await _http.GetFromJsonAsync<Dictionary<string, int>>("vote/results", _json);
            if (raw is null) return null;

            int total = raw.Values.Sum();
            return raw.Select(kv => new VoteResult
            {
                Option  = kv.Key,
                Count   = kv.Value,
                Percent = total == 0 ? 0 : kv.Value * 100.0 / total
            }).OrderByDescending(r => r.Count).ToList();
        }
        catch { return null; }
    }

    // ── PING ──────────────────────────────────────────────────

    public async Task<bool> PingAsync()
    {
        try
        {
            var res = await _http.GetAsync("vote/results");
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Troca de base URL em runtime ──────────────────────────

    public void SetBaseUrl(string url)
    {
        if (!url.EndsWith('/')) url += '/';
        _http.BaseAddress = new Uri(url);
        Preferences.Set("ApiBaseUrl", url);
    }

    public string CurrentBaseUrl => _http.BaseAddress?.ToString() ?? "";
}

file record AuthResponse(bool Autorizado, string Motivo);
