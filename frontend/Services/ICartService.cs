using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Order;

namespace Frontend.Services;

public interface ICartService
{
    IReadOnlyList<CartItemDto> Items { get; }
    int TotalCount { get; }
    decimal SubTotal { get; }
    bool IsDrawerOpen { get; set; }

    event Action? OnChange;

    Task InitializeAsync(string? username = null);
    Task MergeGuestCartOnLoginAsync(string username);
    Task SwitchUserOnLogoutAsync();
    Task AddItemAsync(CartItemDto item);
    Task UpdateQuantityAsync(int maBienThe, int quantity);
    Task RemoveItemAsync(int maBienThe);
    Task ClearCartAsync();
    void ToggleDrawer();
    void SetDrawerOpen(bool isOpen);
}
