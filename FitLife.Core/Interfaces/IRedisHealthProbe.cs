namespace FitLife.Core.Interfaces;

public interface IRedisHealthProbe
{
    Task<TimeSpan> PingAsync();
}
