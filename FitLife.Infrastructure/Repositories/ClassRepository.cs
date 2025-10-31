using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitLife.Infrastructure.Repositories;

/// <summary>
/// Class repository implementation
/// </summary>
public class ClassRepository : Repository<Class>, IClassRepository
{
    public ClassRepository(FitLifeDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Class>> GetUpcomingClassesAsync(int limit = 50)
    {
        return await _dbSet
            .Where(c => c.IsActive 
                && c.StartTime > DateTime.UtcNow 
                && c.CurrentEnrollment < c.Capacity)
            .OrderBy(c => c.StartTime)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<Class>> GetByTypeAsync(string classType)
    {
        return await _dbSet
            .Where(c => c.Type == classType && c.IsActive)
            .OrderBy(c => c.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Class>> GetByInstructorAsync(string instructorId)
    {
        return await _dbSet
            .Where(c => c.InstructorId == instructorId && c.IsActive)
            .OrderBy(c => c.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Class>> GetPopularClassesAsync(int limit = 20)
    {
        return await _dbSet
            .Where(c => c.IsActive && c.StartTime > DateTime.UtcNow)
            .OrderByDescending(c => c.WeeklyBookings)
            .ThenByDescending(c => c.AverageRating)
            .Take(limit)
            .ToListAsync();
    }
}
