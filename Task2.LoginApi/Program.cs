using Microsoft.EntityFrameworkCore;
using Task2.LoginApi.Data;
using Task2.LoginApi.Models;
using Task2.LoginApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<IJwtValidator, JwtValidator>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (!await db.Users.AnyAsync(u => u.Username == "user1"))
    {
        var (hash, salt, it) = PasswordHasher.Hash("Password123");
        db.Users.Add(new User
        {
            Username = "user1",
            PasswordHashBase64 = hash,
            SaltBase64 = salt,
            Iterations = it,
            HashAlgo = "SHA256"
        });
        await db.SaveChangesAsync();
    }
}

app.MapPost("/api/login",
    async Task<IResult> (LoginRequest req, HttpContext http, AppDbContext db, IJwtValidator jwtValidator) =>
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        {
            return TypedResults.Json(
                ApiResponse.Fail("username and password are required"),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        if (!http.Request.Headers.TryGetValue("x-auth-token", out var tokenVals))
        {
            return TypedResults.Json(
                ApiResponse.Fail("Missing x-auth-token header"),
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var token = tokenVals.ToString();
        var (ok, error) = jwtValidator.Validate(token);
        if (!ok)
        {
            return TypedResults.Json(
                ApiResponse.Fail($"Invalid token: {error}"),
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
        if (user is null || !PasswordHasher.Verify(user, req.Password))
        {
            return TypedResults.Json(
                ApiResponse.Fail("Invalid credentials"),
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        return TypedResults.Json(
            ApiResponse.Ok("Login ok", new { username = user.Username }),
            statusCode: StatusCodes.Status200OK
        );
    });

app.Run();
