using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitLife.Infrastructure.Repositories;

/// <summary>
/// Repository for managing recommendations
/// </summary>
public class RecommendationRepository : Repository<Recommendation>, IRecommendationRepository
{
    public RecommendationRepository(FitLifeDbContext context) : base(context)
    {
    }

    public async Task<List<Recommendation>> GetByUserIdAsync(string userId, int limit = 10)
    {
        return await _dbSet
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Rank)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Recommendation>> GetRecentByUserIdAsync(string userId, int withinMinutes = 10, int limit = 10)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-withinMinutes);
        
        return await _dbSet
            .Where(r => r.UserId == userId && r.GeneratedAt >= cutoffTime)
            .OrderBy(r => r.Rank)
            .Take(limit)
            .ToListAsync();
    }

    public async Task DeleteByUserIdAsync(string userId)
    {
        var recommendations = await _dbSet
            .Where(r => r.UserId == userId)
            .ToListAsync();

        _dbSet.RemoveRange(recommendations);
        await _context.SaveChangesAsync();
    }

    public async Task SaveRecommendationsAsync(string userId, List<Recommendation> recommendations)
    {
        // Delete existing recommendations for the user
        var existing = await _dbSet.Where(r => r.UserId == userId).ToListAsync();
        _dbSet.RemoveRange(existing);

        // Add new recommendations
        await _dbSet.AddRangeAsync(recommendations);
        
        // SaveChangesAsync is already transactional and will rollback on exception
        // No need for explicit transaction when retry strategy is configured
        await _context.SaveChangesAsync();
    }
}
