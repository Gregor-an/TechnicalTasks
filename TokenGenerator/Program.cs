using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

string privateKeyPem = File.ReadAllText("Keys/private.key.pem");

using var rsa = RSA.Create();
rsa.ImportFromPem(privateKeyPem);

var creds = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

var token = new JwtSecurityToken(
    issuer: "test-issuer",
    audience: "test-audience",
    claims: new[] { new Claim("sub", "user1") },
    expires: DateTime.UtcNow.AddHours(1),
    signingCredentials: creds
);

string jwt = new JwtSecurityTokenHandler().WriteToken(token);
Console.WriteLine(jwt);
