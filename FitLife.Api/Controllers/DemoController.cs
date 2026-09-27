using FitLife.Api.Configuration;
using FitLife.Core.Auth;
using FitLife.Core.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Infrastructure.Data;
using FitLife.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitLife.Api.Controllers;

/// <summary>
/// One-click entry for synthetic demo personas. Every route returns 404 unless
/// <c>Demo:Enabled</c> is true, and only allowlisted personas can be signed in as.
/// A session always issues a Member token; it never grants operator access.
/// </summary>
[ApiController]
[Route("api/demo")]
[AllowAnonymous]
public class DemoController : ControllerBase
{
    private readonly DemoPersonaService _personas;
    private readonly IRecommendationService _recommendations;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoController> _logger;

    public DemoController(
        DemoPersonaService personas,
        IRecommendationService recommendations,
        IJwtService jwtService,
        IConfiguration configuration,
        ILogger<DemoController> logger)
    {
        _personas = personas;
        _recommendations = recommendations;
        _jwtService = jwtService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Lists the personas a visitor can explore.</summary>
    [HttpGet("personas")]
    public ActionResult<ApiResponse<List<DemoPersonaDto>>> GetPersonas()
    {
        if (!DemoMode.IsEnabled(_configuration))
            return NotFound();

        var personas = DemoCatalog.Personas.Select(persona =>
        {
            var profile = DemoCatalog.Profile(persona.UserId);
            return new DemoPersonaDto
            {
                Id = persona.Id,
                FirstName = profile.FirstName,
                FitnessLevel = profile.FitnessLevel,
                PreferredClassTypes = profile.PreferredTypes.ToList(),
                Headline = persona.Headline,
                Summary = persona.Summary
            };
        }).ToList();

        return Ok(new ApiResponse<List<DemoPersonaDto>> { Success = true, Data = personas });
    }

    /// <summary>
    /// Resets the persona to its canonical state and signs the visitor in as that member.
    /// </summary>
    [HttpPost("personas/{personaId}/session")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> StartSession(
        string personaId, CancellationToken cancellationToken)
    {
        if (!DemoMode.IsEnabled(_configuration))
            return NotFound();

        var persona = DemoPersonaService.Find(personaId);
        if (persona == null)
            return NotFound(new ApiResponse<AuthResponseDto> { Success = false, Message = "Unknown demo persona" });

        var user = await _personas.ResetAsync(persona, cancellationToken);
        // Best-effort: the cache client swallows Redis errors, and reset removed the
        // persisted rows, so the next read regenerates from SQL either way.
        await _recommendations.InvalidateCacheAsync(user.Id);

        _logger.LogInformation("Demo session started for persona {PersonaId}", persona.Id);
        return Ok(new ApiResponse<AuthResponseDto>
        {
            Success = true,
            Message = "Demo session started",
            Data = new AuthResponseDto
            {
                Token = _jwtService.GenerateToken(user.Id, user.Email, user.Segment, FitLifeRoles.Member),
                User = DtoMappers.MapToUserDto(user)
            }
        });
    }
}
