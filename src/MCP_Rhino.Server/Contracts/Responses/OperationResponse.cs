namespace MCP_Rhino.Server.Contracts.Responses;

public class OperationResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static OperationResponse Ok(string message = "")
    {
        return new OperationResponse
        {
            Success = true,
            Message = message
        };
    }

    public static OperationResponse Fail(string message)
    {
        return new OperationResponse
        {
            Success = false,
            Message = message
        };
    }
}

public class OperationResponse<T> : OperationResponse
{
    public T? Data { get; init; }

    public static OperationResponse<T> Ok(T data, string message = "")
    {
        return new OperationResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static new OperationResponse<T> Fail(string message)
    {
        return new OperationResponse<T>
        {
            Success = false,
            Message = message,
            Data = default
        };
    }
}