namespace Forum.Blazor.Models;

public class AuthResult
{
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public LoginResponse? Data { get; set; }

    public static AuthResult Success(LoginResponse? data = null) =>
        new() { Succeeded = true, Data = data };

    public static AuthResult Failure(string error) =>
        new() { Succeeded = false, ErrorMessage = error };
}
