namespace Frontend.Services;

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class ToastMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Message { get; set; } = string.Empty;
    public ToastLevel Level { get; set; }
}

public interface IToastService
{
    event Action<ToastMessage>? OnShow;
    void ShowSuccess(string message);
    void ShowError(string message);
    void ShowWarning(string message);
    void ShowInfo(string message);
}
