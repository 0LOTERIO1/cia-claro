using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cia.Api.Configuration;
using Cia.Api.DTOs;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cia.Api.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly JwtOptions _jwt;

    public AuthService(IUserRepository users, IOptions<JwtOptions> jwt)
    {
        _users = users;
        _jwt = jwt.Value;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationAppException("E-mail e senha são obrigatórios.");
        }

        var user = await _users.GetByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null || !PasswordProtector.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAppException("E-mail ou senha inválidos.");
        }

        return new LoginResponse
        {
            Token = CreateToken(user.Id, user.Email, user.Name, user.Role.ToString(), user.CustomerId),
            User = user.ToDto()
        };
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado.");

        return user.ToDto();
    }

    private string CreateToken(Guid userId, string email, string name, string role, string? customerId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            claims.Add(new Claim("customerId", customerId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwt.ExpiresHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
