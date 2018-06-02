using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Amazon.Lambda.Core;
using BlogApi.Models.Lambda;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]


namespace BlogApi.Functions.Manager
{
  public class AuthManager
  {
    const string ENV_AUTH_ISS = "Issuer";
    const string ENV_AUTH_AUD = "Audience";
    const string ENV_AUTH_SECRET = "Secret";
    const string ENV_EXPIRE_SECONDS = "Expire";

    public AuthPolicy AuthLambda(TokenAuthorizerContext request, ILambdaContext context)
    {
      bool isAuthorized = false;
      ClaimsPrincipal claims = null;
      try
      {
        isAuthorized = AuthManager.ValidateJWT(request.AuthorizationToken, ClaimTypes.Role, "admin", out claims);
      }
      catch (System.Exception ex)
      {

        context.Logger.LogLine("Error on AuthLambda");
        context.Logger.Log(ex.Message);
        context.Logger.LogLine(request.AuthorizationToken);
      }

      return new AuthPolicy()
      {
        principalId = isAuthorized ? claims?.FindFirst(ClaimTypes.Email)?.Value : "user",
        policyDocument = new PolicyDocument
        {
          Version = "2012-10-17",
          Statement = new Statement[]{
            new Statement { Action = "execute-api:Invoke",
                            Effect = isAuthorized ? "Allow" : "Deny",
                            Resource = request.MethodArn  },
          }
        }
      };
    }

    public static bool ValidateJWT(string token, string claim, string claimValue, out ClaimsPrincipal claims)
    {
      claims = null;
      var audience = System.Environment.GetEnvironmentVariable(ENV_AUTH_AUD);
      var issuer = System.Environment.GetEnvironmentVariable(ENV_AUTH_ISS);
      var secret = System.Environment.GetEnvironmentVariable(ENV_AUTH_SECRET);

      var sigining = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));

      var jwtParams = new TokenValidationParameters()
      {
        ValidateIssuerSigningKey = true,
        ValidateAudience = true,
        ValidateIssuer = true,
        ValidateLifetime = true,
        IssuerSigningKey = sigining,
        ValidAudience = audience,
        ValidIssuer = issuer,
        ClockSkew = TimeSpan.Zero,
      };

      var handler = new JwtSecurityTokenHandler();
      claims = handler.ValidateToken(token, jwtParams, out SecurityToken validatedToken);
      // return claims != null && claims.HasClaim(claim, claimValue);
      return claims != null && claims.HasClaim(claim, claimValue);
    }

    public static string CreateJWT(Action<List<Claim>> action)
    {

      var audience = System.Environment.GetEnvironmentVariable(ENV_AUTH_AUD);
      var issuer = System.Environment.GetEnvironmentVariable(ENV_AUTH_ISS);
      var secret = System.Environment.GetEnvironmentVariable(ENV_AUTH_SECRET);
      var expire = Convert.ToInt32(Environment.GetEnvironmentVariable(ENV_EXPIRE_SECONDS ?? "300"));

      DateTime now = DateTime.Now;

      List<Claim> claims = new List<Claim>();
      if (action != null) action(claims);

      // claims.Add(new Claim("name", user.Name));
      // claims.Add(new Claim("sub", user.Email));
      // claims.Add(new Claim("user", user.IsAdmin ? "admin" : "normal"));

      var sign = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
      var sigining = new SigningCredentials(sign, SecurityAlgorithms.HmacSha256);

      var desc = new SecurityTokenDescriptor();
      desc.SigningCredentials = sigining;
      desc.Audience = audience;
      desc.IssuedAt = now;
      desc.Issuer = issuer;
      desc.NotBefore = now;
      desc.Expires = now.AddSeconds(expire);
      desc.Subject = new ClaimsIdentity(claims);

      IdentityModelEventSource.ShowPII = true;

      var handler = new JwtSecurityTokenHandler();
      return handler.CreateEncodedJwt(desc);
    }
  }
}