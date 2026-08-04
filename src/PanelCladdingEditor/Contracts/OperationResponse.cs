namespace PanelCladdingEditor.Contracts.Responses;

public class OperationResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static OperationResponse Ok(string message = "") => new() { Success = true, Message = message };
    public static OperationResponse Fail(string message) => new() { Success = false, Message = message };
}

public sealed class OperationResponse<T> : OperationResponse
{
    public T? Data { get; init; }

    public static OperationResponse<T> Ok(T data, string message = "") => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static new OperationResponse<T> Fail(string message) => new()
    {
        Success = false,
        Message = message,
        Data = default
    };
}
