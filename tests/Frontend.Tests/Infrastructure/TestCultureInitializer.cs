using System.Globalization;
using System.Runtime.CompilerServices;

namespace Frontend.Tests.Infrastructure;

/// <summary>
/// Tự động khởi tạo Culture vi-VN với định dạng số và tiền tệ Việt Nam (phân cách hàng nghìn bằng dấu chấm ".")
/// trước khi thực thi bất kỳ bài test nào trong assembly Frontend.Tests.
/// Giúp môi trường CI/CD (Ubuntu/Linux) đồng nhất 100% định dạng với máy chủ Windows và môi trường Production.
/// </summary>
public static class TestCultureInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        var culture = new CultureInfo("vi-VN");
        culture.NumberFormat.NumberGroupSeparator = ".";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.CurrencyGroupSeparator = ".";
        culture.NumberFormat.CurrencyDecimalSeparator = ",";

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
