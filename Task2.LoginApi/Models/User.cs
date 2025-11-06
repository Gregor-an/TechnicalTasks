namespace Task2.LoginApi.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = default!;
    public string PasswordHashBase64 { get; set; } = default!;
    public string SaltBase64 { get; set; } = default!;
    public int Iterations { get; set; }
    public string HashAlgo { get; set; } = "SHA256";
}
