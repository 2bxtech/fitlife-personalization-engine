namespace FitLife.Api.Configuration;

/// <summary>
/// Demo mode enables one-click persona sessions and startup seeding of synthetic
/// data. It is off unless <c>Demo:Enabled</c> is explicitly true.
/// </summary>
public static class DemoMode
{
    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue("Demo:Enabled", false);
}
