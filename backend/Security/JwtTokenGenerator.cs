using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(int userId, string username, string role, string fullName);
    (string Token, DateTime ExpiresAt) GenerateToken(int userId, string username, string role, string fullName, int? branchId, bool isOnlineWarehouse);
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(int userId, string username, string role, string fullName)
        => GenerateToken(userId, username, role, fullName, null, false);

    public (string Token, DateTime ExpiresAt) GenerateToken(int userId, string username, string role, string fullName, int? branchId, bool isOnlineWarehouse)
    {
        var secretKey = _configuration["JWT_SECRET_KEY"] 
            ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
            ?? _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
        {
            throw new InvalidOperationException("LỖI BẢO MẬT NGHIÊM TRỌNG (Fail-Fast): Khóa bí mật JWT chưa được cấu hình hoặc quá ngắn (< 32 ký tự).");
        }

        var issuer = _configuration["JWT_ISSUER"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _configuration["Jwt:Issuer"] ?? "FashionStoreBackend";
        var audience = _configuration["JWT_AUDIENCE"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _configuration["Jwt:Audience"] ?? "FashionStoreClients";
        var expireMinutes = int.TryParse(_configuration["JWT_EXPIRE_MINUTES"] ?? Environment.GetEnvironmentVariable("JWT_EXPIRE_MINUTES"), out var m) ? m : 480;

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

        if (branchId.HasValue)
        {
            claims.Add(new Claim("branchId", branchId.Value.ToString()));
            claims.Add(new Claim("isOnlineWarehouse", isOnlineWarehouse ? "true" : "false"));
        }

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
