using FitLife.Core.Interfaces;
using FitLife.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitLife.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation with EF Core
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly FitLifeDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(FitLifeDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(string id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public virtual async Task<bool> ExistsAsync(string id)
    {
        var entity = await GetByIdAsync(id);
        return entity != null;
    }

    public virtual async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
