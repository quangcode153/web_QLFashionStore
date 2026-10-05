using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(int userId, string username, string role, string fullName);
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(int userId, string username, string role, string fullName)
    {
        var secretKey = _configuration["JWT_SECRET_KEY"] 
            ?? _configuration["Jwt:Key"] 
            ?? "FashionStore_Secret_Key_Super_Secure_2026_JWT_Token_AtLeast_32Characters!";
        var issuer = _configuration["JWT_ISSUER"] ?? _configuration["Jwt:Issuer"] ?? "FashionStoreBackend";
        var audience = _configuration["JWT_AUDIENCE"] ?? _configuration["Jwt:Audience"] ?? "FashionStoreClients";
        var expireMinutes = int.TryParse(_configuration["JWT_EXPIRE_MINUTES"], out var m) ? m : 480;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(expireMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new("fullName", fullName),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiresAt);
    }
}
