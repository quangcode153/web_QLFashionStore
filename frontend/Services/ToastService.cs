using System;

namespace Frontend.Services;

public class ToastService : IToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message)
    {
        OnShow?.Invoke(new ToastMessage
        {
            Message = message,
            Level = ToastLevel.Success
        });
    }

    public void ShowError(string message)
    {
        OnShow?.Invoke(new ToastMessage
        {
            Message = message,
            Level = ToastLevel.Error
        });
    }

    public void ShowWarning(string message)
    {
        OnShow?.Invoke(new ToastMessage
        {
            Message = message,
            Level = ToastLevel.Warning
        });
    }

    public void ShowInfo(string message)
    {
        OnShow?.Invoke(new ToastMessage
        {
            Message = message,
            Level = ToastLevel.Info
        });
    }
}
