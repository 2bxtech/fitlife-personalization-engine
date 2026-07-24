using FitLife.Core.DTOs;
using FitLife.Core.Auth;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FitLife.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        IUserRepository userRepository,
        IJwtService jwtService,
        ILogger<AuthController> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register([FromBody] RegisterUserDto dto)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Email and password are required"
                });
            }

            // Check if user already exists
            var existingUser = await _userRepository.GetByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                return Conflict(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "User with this email already exists"
                });
            }

            // Create new user
            var user = new User
            {
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                FitnessLevel = dto.FitnessLevel,
                Goals = JsonSerializer.Serialize(dto.Goals ?? new List<string>()),
                PreferredClassTypes = JsonSerializer.Serialize(dto.PreferredClassTypes ?? new List<string>()),
                Segment = "Beginner" // Default segment for new users
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();

            // Generate JWT token
            var token = _jwtService.GenerateToken(
                user.Id,
                user.Email,
                user.Segment,
                FitLifeRoles.Member);

            _logger.LogInformation("User registered successfully: {Email}", dto.Email);

            return Ok(new ApiResponse<AuthResponseDto>
            {
                Success = true,
                Data = new AuthResponseDto
                {
                    Token = token,
                    User = DtoMappers.MapToUserDto(user)
                },
                Message = "Registration successful"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return StatusCode(500, new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Registration failed"
            });
        }
    }

    /// <summary>
    /// Login user
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginDto dto)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Email and password are required"
                });
            }

            // Find user
            var user = await _userRepository.GetByEmailAsync(dto.Email);
            if (user == null)
            {
                return Unauthorized(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Invalid credentials"
                });
            }

            // Verify password
            var isValidPassword = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isValidPassword)
            {
                return Unauthorized(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Invalid credentials"
                });
            }

            // Generate JWT token
            var token = _jwtService.GenerateToken(
                user.Id,
                user.Email,
                user.Segment,
                ResolveLoginRole(user.Email));

            _logger.LogInformation("User logged in successfully: {Email}", dto.Email);

            return Ok(new ApiResponse<AuthResponseDto>
            {
                Success = true,
                Data = new AuthResponseDto
                {
                    Token = token,
                    User = DtoMappers.MapToUserDto(user)
                },
                Message = "Login successful"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login");
            return StatusCode(500, new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Login failed"
            });
        }
    }

    private string ResolveLoginRole(string email)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            return FitLifeRoles.Member;
        }

        var operatorEmails = _configuration
            .GetSection("DemoAuthorization:OperatorEmails")
            .Get<string[]>() ?? Array.Empty<string>();

        return operatorEmails.Contains(email, StringComparer.OrdinalIgnoreCase)
            ? FitLifeRoles.Operator
            : FitLifeRoles.Member;
    }
}
