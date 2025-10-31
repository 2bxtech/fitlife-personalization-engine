using FitLife.Core.Models;

namespace FitLife.Core.Interfaces;

/// <summary>
/// Repository interface for Class entity with specific operations
/// </summary>
public interface IClassRepository : IRepository<Class>
{
    /// <summary>
    /// Get upcoming classes (active, with available spots, starting after now)
    /// </summary>
    Task<IEnumerable<Class>> GetUpcomingClassesAsync(int limit = 50);
    
    /// <summary>
    /// Get classes by type
    /// </summary>
    Task<IEnumerable<Class>> GetByTypeAsync(string classType);
    
    /// <summary>
    /// Get classes by instructor
    /// </summary>
    Task<IEnumerable<Class>> GetByInstructorAsync(string instructorId);
    
    /// <summary>
    /// Get popular classes (sorted by weekly bookings)
    /// </summary>
    Task<IEnumerable<Class>> GetPopularClassesAsync(int limit = 20);
}
