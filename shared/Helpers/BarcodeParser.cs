using System;
using System.Text.RegularExpressions;

namespace Shared.Helpers;

public static class BarcodeParser
{
    private static readonly Regex SkuJsonRegex = new(@"\""sku\""\s*:\s*\""([^\""]+)\""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SkuUrlRegex = new(@"[?&]sku=([^&#]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Trích xuất mã SKU chuẩn từ chuỗi quét được của máy quét mã vạch hoặc camera QR.
    /// Hỗ trợ: SKU thuần, tiền tố QF/SKU:, URL chứa tham số sku, hoặc JSON.
    /// </summary>
    public static string ExtractSku(string? rawScanData)
    {
        if (string.IsNullOrWhiteSpace(rawScanData))
        {
            return string.Empty;
        }

        var text = rawScanData.Trim();

        // 1. Kiểm tra nếu là chuỗi JSON: {"sku":"POLO-DEN-L", ...}
        if (text.StartsWith("{") && text.EndsWith("}"))
        {
            var matchJson = SkuJsonRegex.Match(text);
            if (matchJson.Success && matchJson.Groups.Count > 1)
            {
                return matchJson.Groups[1].Value.Trim().ToUpperInvariant();
            }
        }

        // 2. Kiểm tra nếu là URL web: https://domain/product/1?sku=POLO-DEN-L
        if (text.Contains("sku=", StringComparison.OrdinalIgnoreCase))
        {
            var matchUrl = SkuUrlRegex.Match(text);
            if (matchUrl.Success && matchUrl.Groups.Count > 1)
            {
                var unescaped = Uri.UnescapeDataString(matchUrl.Groups[1].Value);
                return unescaped.Trim().ToUpperInvariant();
            }
        }

        // 3. Kiểm tra tiền tố SKU:
        if (text.StartsWith("SKU:", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(4).Trim();
        }

        // 4. Kiểm tra tiền tố thương hiệu QF: hoặc QF-
        if (text.StartsWith("QF:", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(3).Trim();
        }
        else if (text.StartsWith("QF-", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(3).Trim();
        }

        return text.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Sinh URL hình ảnh mã QR chuẩn cho một SKU biến thể sản phẩm.
    /// </summary>
    public static string GenerateQrCodeUrl(string sku, int size = 180)
    {
        if (string.IsNullOrWhiteSpace(sku)) return string.Empty;
        var encodedSku = Uri.EscapeDataString(sku.Trim().ToUpperInvariant());
        return $"https://api.qrserver.com/v1/create-qr-code/?size={size}x{size}&data={encodedSku}";
    }
}
