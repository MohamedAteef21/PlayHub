using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PlayHub.Application.Auth;
using PlayHub.Domain.Enums;

namespace PlayHub.Infrastructure.Auth;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(
        Guid userId,
        Guid tenantId,
        string email,
        string firstName,
        string lastName,
        bool isMaster,
        UserRole role,
        IEnumerable<string> permissions,
        IEnumerable<Guid> branchIds,
        Guid? activeBranchId)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["AccessTokenMinutes"] ?? "15"));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("given_name", firstName),
            new("family_name", lastName),
            new("tenant_id", tenantId.ToString()),
            new("is_master", isMaster.ToString().ToLowerInvariant()),
            new("role", ((int)role).ToString()),
        };

        if (activeBranchId.HasValue)
            claims.Add(new Claim("branch_id", activeBranchId.Value.ToString()));

        foreach (var branchId in branchIds)
            claims.Add(new Claim("branch", branchId.ToString()));

        foreach (var permission in permissions)
            claims.Add(new Claim("permission", permission));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public string HashToken(string token) => BCrypt.Net.BCrypt.HashPassword(token);
}
