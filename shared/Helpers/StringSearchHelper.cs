using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Shared.Helpers;

/// <summary>
/// Helper tìm kiếm thông minh: Hỗ trợ tìm kiếm gần đúng (Fuzzy/Smart Search),
/// loại bỏ dấu tiếng Việt (Diacritics), hỗ trợ đa từ khóa không phân biệt thứ tự (Tokenized Match),
/// và so khớp chuỗi con linh hoạt cho danh mục biến thể sản phẩm.
/// </summary>
public static class StringSearchHelper
{
    /// <summary>
    /// Loại bỏ dấu tiếng Việt và chuyển về chữ thường, xử lý triệt để ký tự đ/Đ.
    /// </summary>
    public static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var result = stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("đ", "d", StringComparison.OrdinalIgnoreCase)
            .Replace("Đ", "D", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        return result;
    }

    /// <summary>
    /// Kiểm tra source có khớp với query tìm kiếm hay không:
    /// - Không phân biệt hoa/thường, không phân biệt có dấu/không dấu.
    /// - Cho phép tìm kiếm đa từ khóa (Multi-token): Tất cả các từ khóa trong query đều phải xuất hiện trong source (bất kể thứ tự).
    /// - Hỗ trợ so khớp chuỗi con liên tiếp (Subsequence match) khi từ khóa viết tắt.
    /// </summary>
    public static bool IsFuzzyMatch(string? source, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        if (string.IsNullOrWhiteSpace(source))
            return false;

        var cleanSource = RemoveDiacritics(source);
        var cleanQuery = RemoveDiacritics(query);

        // Đa từ khóa: Tách các từ cách nhau bởi khoảng trắng
        var tokens = cleanQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return true;

        foreach (var token in tokens)
        {
            // 1. Kiểm tra chứa trực tiếp token
            if (cleanSource.Contains(token, StringComparison.OrdinalIgnoreCase))
                continue;

            // 2. Nếu không chứa trực tiếp, kiểm tra chuỗi con viết tắt (Acronym / Subsequence) nếu token có từ 2 ký tự
            if (token.Length >= 2 && IsSubsequence(token, cleanSource))
                continue;

            return false;
        }

        return true;
    }

    private static bool IsSubsequence(string pattern, string text)
    {
        int patternIndex = 0;
        int textIndex = 0;

        while (patternIndex < pattern.Length && textIndex < text.Length)
        {
            if (char.ToLowerInvariant(pattern[patternIndex]) == char.ToLowerInvariant(text[textIndex]))
            {
                patternIndex++;
            }
            textIndex++;
        }

        return patternIndex == pattern.Length;
    }
}

