using Microsoft.EntityFrameworkCore;

namespace FitLife.Infrastructure.Data;

/// <summary>
/// SQL Server application lock held until the current transaction ends. Used to
/// serialize demo seeding and persona resets across requests and replicas.
/// </summary>
internal static class SqlAppLock
{
    public static Task AcquireAsync(DbContext context, string resource, CancellationToken cancellationToken = default) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 50001, 'Timed out waiting for an application lock.', 1;
            """, cancellationToken);
}
