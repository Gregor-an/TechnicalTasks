using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Task2.LoginApi.Services;

public sealed class JwtOptions
{
    public string PublicKeyPath { get; set; } = "Keys/public.key.pem";
    public bool ValidateIssuer { get; set; } = false;
    public bool ValidateAudience { get; set; } = false;
    public bool ValidateLifetime { get; set; } = true;
    public int ClockSkewSeconds { get; set; } = 30;
    public string? ValidIssuer { get; set; }
    public string? ValidAudience { get; set; }
}

public interface IJwtValidator
{
    (bool ok, string? error) Validate(string token);
}

public sealed class JwtValidator(IOptions<JwtOptions> options, ILogger<JwtValidator> logger) : IJwtValidator
{
    private readonly JwtOptions _options = options.Value;
    private readonly ILogger<JwtValidator> _logger = logger;
    private RsaSecurityKey? _key;

    private RsaSecurityKey GetKey()
    {
        if (_key != null) return _key;

        string pem = File.ReadAllText(_options.PublicKeyPath);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        _key = new RsaSecurityKey(rsa.ExportParameters(false));
        return _key;
    }

    public (bool ok, string? error) Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "Missing token");

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GetKey(),
            ValidateIssuer = _options.ValidateIssuer,
            ValidIssuer = _options.ValidIssuer,
            ValidateAudience = _options.ValidateAudience,
            ValidAudience = _options.ValidAudience,
            ValidateLifetime = _options.ValidateLifetime,
            ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
            RequireSignedTokens = true,
            RequireExpirationTime = _options.ValidateLifetime
        };

        try
        {
            handler.ValidateToken(token, parameters, out _);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT validation failed");
            return (false, ex.Message);
        }
    }
}
