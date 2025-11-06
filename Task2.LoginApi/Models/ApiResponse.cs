namespace Task2.LoginApi.Models;

public sealed class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }

    public static ApiResponse Ok(string msg, object? data = null)
        => new() { Success = true, Message = msg, Data = data };

    public static ApiResponse Fail(string msg)
        => new() { Success = false, Message = msg };
}