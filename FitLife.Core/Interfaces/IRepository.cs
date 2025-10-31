namespace FitLife.Core.Interfaces;

/// <summary>
/// Generic repository interface for common data access operations
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Get entity by ID
    /// </summary>
    Task<T?> GetByIdAsync(string id);
    
    /// <summary>
    /// Get all entities
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync();
    
    /// <summary>
    /// Add new entity
    /// </summary>
    Task<T> AddAsync(T entity);
    
    /// <summary>
    /// Update existing entity
    /// </summary>
    Task UpdateAsync(T entity);
    
    /// <summary>
    /// Delete entity
    /// </summary>
    Task DeleteAsync(T entity);
    
    /// <summary>
    /// Check if entity exists
    /// </summary>
    Task<bool> ExistsAsync(string id);
    
    /// <summary>
    /// Save all pending changes
    /// </summary>
    Task<int> SaveChangesAsync();
}
