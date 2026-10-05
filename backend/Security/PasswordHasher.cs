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
    private readonly string _pepper;

    public PasswordHasher(IConfiguration configuration)
    {
        _pepper = configuration["PASSWORD_PEPPER"] 
            ?? configuration["JWT_SECRET_KEY"] 
            ?? "FashionStore_Pepper_Secret_Key_Default_2026!";
    }

    /// <summary>
    /// Băm mật khẩu sử dụng cơ chế bảo mật cao:
    /// Mật khẩu + Khóa bí mật (Secret Key / Pepper từ cấu hình Server) + Salt ngẫu nhiên qua PBKDF2 (HMAC-SHA256)
    /// </summary>
    public string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        string passwordWithSecret = $"{password}:{_pepper}";

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            passwordWithSecret,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Xác thực mật khẩu:
    /// 1. Kiểm tra với Khóa bí mật Secret Key (Pepper) + Salt + PBKDF2
    /// 2. Hỗ trợ tương thích ngược: PBKDF2 không Secret Key, MD5 seed data, Plaintext
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

                // 1.1. Thử khớp với Secret Key (Pepper)
                string passwordWithSecret = $"{password}:{_pepper}";
                byte[] actualHashWithSecret = Rfc2898DeriveBytes.Pbkdf2(
                    passwordWithSecret,
                    salt,
                    Iterations,
                    HashAlgorithmName.SHA256,
                    HashSize);

                if (CryptographicOperations.FixedTimeEquals(actualHashWithSecret, expectedHash))
                    return true;

                // 1.2. Thử khớp không có Secret Key (tương thích các hash cũ)
                byte[] actualHashWithoutSecret = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    Iterations,
                    HashAlgorithmName.SHA256,
                    HashSize);

                if (CryptographicOperations.FixedTimeEquals(actualHashWithoutSecret, expectedHash))
                    return true;
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
