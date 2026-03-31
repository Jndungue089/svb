using BeneditaApi.Data;
using BeneditaApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BeneditaApi.Services;

public class VoteService
{
    private readonly AppDbContext _db;
    private readonly ILogger<VoteService> _logger;

    public VoteService(AppDbContext db, ILogger<VoteService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────
    //  AUTH
    // ──────────────────────────────────────────────────────────

    public async Task<(bool Authorized, string Reason, string VoterName)> AuthorizeAsync(int fingerId)
    {
        var voter = await _db.Voters
            .Include(v => v.Vote)
            .FirstOrDefaultAsync(v => v.FingerId == fingerId);

        if (voter is null)
            return (false, "Eleitor nao cadastrado", "");

        if (!voter.CanVote)
            return (false, "Voto ja realizado", voter.Name);

        if (voter.Vote is not null)
            return (false, "Voto ja registado", voter.Name);

        return (true, "OK", voter.Name);
    }

    // ──────────────────────────────────────────────────────────
    //  VOTE
    // ──────────────────────────────────────────────────────────

    public async Task<(bool Success, string Message)> CastVoteAsync(int fingerId, int entityId)
    {
        var voter = await _db.Voters
            .Include(v => v.Vote)
            .FirstOrDefaultAsync(v => v.FingerId == fingerId);

        if (voter is null)
            return (false, "Eleitor nao encontrado");

        if (voter.Vote is not null || !voter.CanVote)
            return (false, "Eleitor ja votou");

        var entity = await _db.Entities.FindAsync(entityId);
        if (entity is null)
            return (false, "Entidade invalida");

        var vote = new Vote
        {
            VoterId  = voter.Id,
            EntityId = entityId,
            CastAt   = DateTime.UtcNow
        };

        voter.CanVote = false;
        _db.Votes.Add(vote);

        await _db.SaveChangesAsync();
        _logger.LogInformation("Voto registado: finger={FingerId} entidade={EntityId}", fingerId, entityId);
        return (true, "OK");
    }

    // ──────────────────────────────────────────────────────────
    //  VOTERS (CRUD)
    // ──────────────────────────────────────────────────────────

    public async Task<List<Voter>> GetAllVotersAsync() =>
        await _db.Voters.Include(v => v.Vote).ThenInclude(v => v!.Entity).ToListAsync();

    public async Task<Voter?> GetVoterByIdAsync(int id) =>
        await _db.Voters.Include(v => v.Vote).ThenInclude(v => v!.Entity).FirstOrDefaultAsync(v => v.Id == id);

    public async Task<Voter?> GetVoterByFingerAsync(int fingerId) =>
        await _db.Voters.Include(v => v.Vote).FirstOrDefaultAsync(v => v.FingerId == fingerId);

    public async Task<Voter> RegisterVoterAsync(string name, string bi)
    {
        if (await _db.Voters.AnyAsync(v => v.BI == bi))
            throw new InvalidOperationException($"O BI '{bi}' já está registado.");

        if (await _db.Voters.AnyAsync(v => v.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"O eleitor '{name}' já está cadastrado.");

        var voter = new Voter { Name = name, BI = bi };
        _db.Voters.Add(voter);
        await _db.SaveChangesAsync();
        return voter;
    }

    public async Task<Voter> AssignFingerAsync(int voterId, int fingerSlot)
    {
        var voter = await _db.Voters.FindAsync(voterId)
            ?? throw new InvalidOperationException("Eleitor não encontrado.");

        if (await _db.Voters.AnyAsync(v => v.FingerId == fingerSlot && v.Id != voterId))
            throw new InvalidOperationException($"O slot {fingerSlot} já está em uso.");

        voter.FingerId = fingerSlot;
        await _db.SaveChangesAsync();
        return voter;
    }

    public async Task<int> NextFreeSlotAsync()
    {
        var used = await _db.Voters
            .Where(v => v.FingerId != null)
            .Select(v => v.FingerId!.Value)
            .ToListAsync();

        for (int i = 1; i <= 127; i++)
            if (!used.Contains(i)) return i;

        throw new InvalidOperationException("Todos os 127 slots biométricos estão ocupados.");
    }

    public async Task<bool> DeleteVoterAsync(int id)
    {
        var voter = await _db.Voters.FindAsync(id);
        if (voter is null) return false;
        _db.Voters.Remove(voter);
        await _db.SaveChangesAsync();
        return true;
    }

    // ──────────────────────────────────────────────────────────
    //  ENTITIES (CRUD)
    // ──────────────────────────────────────────────────────────

    public async Task<List<Entity>> GetAllEntitiesAsync() =>
        await _db.Entities.Include(e => e.Votes).ToListAsync();

    public async Task<Entity> AddEntityAsync(string name, string acronym, string? description)
    {
        if (await _db.Entities.AnyAsync(e => e.Acronym.ToUpper() == acronym.ToUpper()))
            throw new InvalidOperationException($"A sigla '{acronym}' já existe.");

        var entity = new Entity { Name = name, Acronym = acronym.ToUpper(), Description = description };
        _db.Entities.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteEntityAsync(int id)
    {
        var entity = await _db.Entities.FindAsync(id);
        if (entity is null) return false;
        _db.Entities.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    // ──────────────────────────────────────────────────────────
    //  RESULTS
    // ──────────────────────────────────────────────────────────

    public async Task<List<object>> GetResultsAsync()
    {
        var entities = await _db.Entities.ToListAsync();
        var votes    = await _db.Votes.ToListAsync();
        int total    = votes.Count;

        return entities.Select(e =>
        {
            int count  = votes.Count(v => v.EntityId == e.Id);
            double pct = total == 0 ? 0 : count * 100.0 / total;
            return (object)new { entityId = e.Id, name = e.Name, acronym = e.Acronym, count, percent = Math.Round(pct, 1) };
        }).ToList();
    }
}
