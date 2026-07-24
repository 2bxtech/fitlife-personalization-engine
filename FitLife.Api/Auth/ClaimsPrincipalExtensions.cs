using System.Security.Claims;

namespace FitLife.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string? GetSubjectId(this ClaimsPrincipal principal)
    {
        var subjectId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? principal.FindFirstValue("sub");

        return string.IsNullOrWhiteSpace(subjectId) ? null : subjectId;
    }
}
