using System.Security.Cryptography;
using System.Text;

namespace Backend.Security;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 10000;

    /// <summary>
    /// Băm mật khẩu sử dụng PBKDF2 với HMAC-SHA256 và Salt ngẫu nhiên
    /// </summary>
    public string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của mật khẩu (hỗ trợ PBKDF2 salted hash, SHA256 và MD5 tương thích seed data)
    /// </summary>
    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        // 1. Kiểm tra định dạng PBKDF2 Salted Hash (Salt:Hash)
        var parts = hashedPassword.Split(':');
        if (parts.Length == 2)
        {
            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expectedHash = Convert.FromBase64String(parts[1]);

                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    Iterations,
                    HashAlgorithmName.SHA256,
                    HashSize);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                // Format lỗi thì thử các thuật toán bên dưới
            }
        }

        // 2. Kiểm tra hash MD5 (dành cho seed data ban đầu)
        using var md5 = MD5.Create();
        var md5Bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
        var md5String = Convert.ToHexString(md5Bytes).ToLower();
        if (string.Equals(md5String, hashedPassword, StringComparison.OrdinalIgnoreCase))
            return true;

        // 3. Khớp chuỗi trực tiếp (cho trường hợp dữ liệu thử nghiệm thô)
        return password == hashedPassword;
    }
}
