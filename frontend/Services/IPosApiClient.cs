using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Catalog;
using Shared.DTOs.Customer;
using Shared.DTOs.Order;
using Shared.DTOs.Promotion;

using global::Shared.DTOs.Common;

namespace Frontend.Services;

public interface IPosApiClient
{
    Task<List<ProductListItemDto>?> GetFastSaleProductsAsync();
    Task<ProductVariantDto?> SearchSkuAsync(string sku);
    Task<CustomerProfileDto?> LookupCustomerByPhoneAsync(string phone);
    Task<ValidateVoucherResponseDto?> ValidateVoucherAsync(string code, decimal total);
    Task<OrderSummaryDto?> CreatePosOrderAsync(CreateOrderRequestDto request);
    Task<ReceiptPrintDto?> GetReceiptPrintAsync(int orderId);
    Task<OrderReturnLookupDto?> GetOrderForReturnLookupAsync(int orderId);
    Task<ApiResponse<int>?> ProcessReturnAsync(int orderId, ReturnOrderRequestDto request);
    Task<List<ProductVariantDto>?> GetProductVariantsAsync(int productId);
}
