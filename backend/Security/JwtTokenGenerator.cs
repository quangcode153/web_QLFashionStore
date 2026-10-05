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
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
            ?? _configuration["JWT_SECRET_KEY"] 
            ?? _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
        {
            throw new InvalidOperationException("LỖI BẢO MẬT NGHIÊM TRỌNG (Fail-Fast): Khóa bí mật JWT chưa được cấu hình hoặc quá ngắn (< 32 ký tự).");
        }

        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _configuration["JWT_ISSUER"] ?? _configuration["Jwt:Issuer"] ?? "FashionStoreBackend";
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _configuration["JWT_AUDIENCE"] ?? _configuration["Jwt:Audience"] ?? "FashionStoreClients";
        var expireMinutes = int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIRE_MINUTES") ?? _configuration["JWT_EXPIRE_MINUTES"], out var m) ? m : 480;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(expireMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
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
