using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitLife.Infrastructure.Repositories;

/// <summary>
/// Repository for managing user interactions/events
/// </summary>
public class InteractionRepository : Repository<Interaction>, IInteractionRepository
{
    public InteractionRepository(FitLifeDbContext context) : base(context)
    {
    }

    public async Task<List<Interaction>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.Timestamp)
            .ToListAsync();
    }

    public async Task<List<Interaction>> GetRecentByUserIdAsync(string userId, int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        
        return await _dbSet
            .Where(i => i.UserId == userId && i.Timestamp >= cutoffDate)
            .OrderByDescending(i => i.Timestamp)
            .ToListAsync();
    }

    public async Task<List<Interaction>> GetByUserAndTypeAsync(string userId, string eventType, int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        
        return await _dbSet
            .Where(i => i.UserId == userId 
                && i.EventType == eventType 
                && i.Timestamp >= cutoffDate)
            .OrderByDescending(i => i.Timestamp)
            .ToListAsync();
    }
}
