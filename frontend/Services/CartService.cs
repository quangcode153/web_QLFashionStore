using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Shared.DTOs.Order;

namespace Frontend.Services;

public class CartService : ICartService
{
    private readonly IJSRuntime? _jsRuntime;
    private readonly List<CartItemDto> _items = new();
    private string? _currentUsername;
    private bool _isInitialized;
    private bool _isDrawerOpen;

    public CartService(IJSRuntime? jsRuntime = null)
    {
        _jsRuntime = jsRuntime;
    }

    private string CurrentStorageKey => 
        string.IsNullOrEmpty(_currentUsername) ? "fashion_cart_guest" : $"fashion_cart_user_{_currentUsername}";

    public IReadOnlyList<CartItemDto> Items => _items.AsReadOnly();

    public int TotalCount => _items.Sum(i => i.SoLuong);

    public decimal SubTotal => _items.Sum(i => i.ThanhTien);

    public bool IsDrawerOpen
    {
        get => _isDrawerOpen;
        set
        {
            if (_isDrawerOpen != value)
            {
                _isDrawerOpen = value;
                NotifyStateChanged();
            }
        }
    }

    public event Action? OnChange;

    public async Task InitializeAsync(string? username = null)
    {
        if (_jsRuntime == null)
        {
            _isInitialized = true;
            return;
        }

        try
        {
            if (!string.IsNullOrEmpty(username))
            {
                _currentUsername = username;
            }
            else if (string.IsNullOrEmpty(_currentUsername))
            {
                _currentUsername = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "currentUser");
            }

            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", CurrentStorageKey);
            _items.Clear();
            if (!string.IsNullOrWhiteSpace(json))
            {
                var loaded = JsonSerializer.Deserialize<List<CartItemDto>>(json);
                if (loaded != null && loaded.Any())
                {
                    _items.AddRange(loaded);
                }
            }
            _isInitialized = true;
            NotifyStateChanged();
        }
        catch
        {
            // Ignore during prerender or if localStorage is inaccessible
        }
    }

    public async Task MergeGuestCartOnLoginAsync(string username)
    {
        _currentUsername = username;
        if (_jsRuntime == null) return;

        try
        {
            // 1. Đọc giỏ hàng khách vãng lai
            var guestJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "fashion_cart_guest");
            var guestItems = new List<CartItemDto>();
            if (!string.IsNullOrWhiteSpace(guestJson))
            {
                guestItems = JsonSerializer.Deserialize<List<CartItemDto>>(guestJson) ?? new();
            }

            // 2. Đọc giỏ hàng hiện có của user này (nếu có từ phiên trước)
            var userStorageKey = $"fashion_cart_user_{username}";
            var userJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", userStorageKey);
            var userItems = new List<CartItemDto>();
            if (!string.IsNullOrWhiteSpace(userJson))
            {
                userItems = JsonSerializer.Deserialize<List<CartItemDto>>(userJson) ?? new();
            }

            // 3. Hợp nhất (Merge) từ guest vào user
            if (guestItems.Any())
            {
                foreach (var gItem in guestItems)
                {
                    var existing = userItems.FirstOrDefault(u => u.MaBienThe == gItem.MaBienThe);
                    if (existing != null)
                    {
                        var newQty = existing.SoLuong + gItem.SoLuong;
                        if (newQty > existing.SoLuongTon && existing.SoLuongTon > 0)
                        {
                            newQty = existing.SoLuongTon;
                        }
                        existing.SoLuong = newQty;
                    }
                    else
                    {
                        userItems.Add(gItem);
                    }
                }

                // Xóa giỏ khách vãng lai sau khi gộp thành công
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "fashion_cart_guest");
            }

            _items.Clear();
            _items.AddRange(userItems);

            // Lưu lại vào giỏ riêng của tài khoản này
            var updatedJson = JsonSerializer.Serialize(_items);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", userStorageKey, updatedJson);

            _isInitialized = true;
            NotifyStateChanged();
        }
        catch
        {
            // Ignore storage errors
        }
    }

    public async Task SwitchUserOnLogoutAsync()
    {
        _currentUsername = null;
        _items.Clear();

        if (_jsRuntime != null)
        {
            try
            {
                // Nạp lại giỏ khách vãng lai (nếu có)
                var guestJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "fashion_cart_guest");
                if (!string.IsNullOrWhiteSpace(guestJson))
                {
                    var guestItems = JsonSerializer.Deserialize<List<CartItemDto>>(guestJson);
                    if (guestItems != null && guestItems.Any())
                    {
                        _items.AddRange(guestItems);
                    }
                }
            }
            catch { }
        }

        _isInitialized = true;
        NotifyStateChanged();
    }

    private async Task SaveToStorageAsync()
    {
        if (_jsRuntime == null) return;
        try
        {
            var json = JsonSerializer.Serialize(_items);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", CurrentStorageKey, json);
        }
        catch
        {
            // Ignore storage errors
        }
    }

    public async Task AddItemAsync(CartItemDto item)
    {
        if (item == null) return;
        if (!_isInitialized) await InitializeAsync();

        var existing = _items.FirstOrDefault(i => i.MaBienThe == item.MaBienThe);
        if (existing != null)
        {
            var newQty = existing.SoLuong + item.SoLuong;
            if (newQty > existing.SoLuongTon && existing.SoLuongTon > 0)
            {
                newQty = existing.SoLuongTon;
            }
            existing.SoLuong = newQty;
        }
        else
        {
            var initialQty = item.SoLuong < 1 ? 1 : item.SoLuong;
            if (initialQty > item.SoLuongTon && item.SoLuongTon > 0)
            {
                initialQty = item.SoLuongTon;
            }
            item.SoLuong = initialQty;
            _items.Add(item);
        }

        await SaveToStorageAsync();
        NotifyStateChanged();
    }

    public async Task UpdateQuantityAsync(int maBienThe, int quantity)
    {
        if (!_isInitialized) await InitializeAsync();
        var existing = _items.FirstOrDefault(i => i.MaBienThe == maBienThe);
        if (existing != null)
        {
            if (quantity <= 0)
            {
                _items.Remove(existing);
            }
            else
            {
                if (quantity > existing.SoLuongTon && existing.SoLuongTon > 0)
                {
                    existing.SoLuong = existing.SoLuongTon;
                }
                else
                {
                    existing.SoLuong = quantity;
                }
            }
            await SaveToStorageAsync();
            NotifyStateChanged();
        }
    }

    public async Task RemoveItemAsync(int maBienThe)
    {
        if (!_isInitialized) await InitializeAsync();
        var existing = _items.FirstOrDefault(i => i.MaBienThe == maBienThe);
        if (existing != null)
        {
            _items.Remove(existing);
            await SaveToStorageAsync();
            NotifyStateChanged();
        }
    }

    public async Task ClearCartAsync()
    {
        if (!_isInitialized) await InitializeAsync();
        _items.Clear();
        await SaveToStorageAsync();
        NotifyStateChanged();
    }

    public void ToggleDrawer()
    {
        _isDrawerOpen = !_isDrawerOpen;
        NotifyStateChanged();
    }

    public void SetDrawerOpen(bool isOpen)
    {
        _isDrawerOpen = isOpen;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
