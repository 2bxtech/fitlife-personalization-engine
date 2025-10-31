using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitLife.Infrastructure.Repositories;

/// <summary>
/// User repository implementation
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(FitLifeDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<IEnumerable<User>> GetBySegmentAsync(string segment)
    {
        return await _dbSet
            .Where(u => u.Segment == segment)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync(int daysSinceLastInteraction = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysSinceLastInteraction);
        
        return await _dbSet
            .Where(u => u.Interactions.Any(i => i.Timestamp >= cutoffDate))
            .ToListAsync();
    }
}
