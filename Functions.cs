using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Microsoft.IdentityModel.Logging;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BlogApi
{
  public class UsersFunctions
  {
    const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "UserTable";
    const string ID_QUERY_STRING_NAME = "Email";
    const string SALT_ENVIRONMENT_VARIABLE = "Salt";

    public DynamoDBContext DDBContext { get; }

    public UsersFunctions()
    {
      // Check to see if a table name was passed in through environment variables and if so 
      // add the table mapping.
      var tableName = System.Environment.GetEnvironmentVariable(TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP);
      if (!string.IsNullOrEmpty(tableName))
      {
        AWSConfigsDynamoDB.Context.TypeMappings[typeof(User)] = new Amazon.Util.TypeMapping(typeof(User), tableName);
      }

      var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
      DDBContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);

      JsonConvert.DefaultSettings = () => new JsonSerializerSettings
      {
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
      };
    }

    public string PasswordCrypt(string email, string password)
    {
      var salt = System.Environment.GetEnvironmentVariable(SALT_ENVIRONMENT_VARIABLE);
      // TODO: use a properly crypt
      return email + password + salt;
    }

    public async Task<APIGatewayProxyResponse> AddUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      var user = JsonConvert.DeserializeObject<User>(request?.Body);
      user.Email = user.Email.ToLowerInvariant();
      user.Password = PasswordCrypt(user.Email, user.Password);

      context.Logger.LogLine($"Saving blog with id {user.Email}");
      await DDBContext.SaveAsync<User>(user);

      var result = new UserDTO
      {
        Name = user.Name,
        Email = user.Email,
        IsAdmin = user.IsAdmin,
      };

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonConvert.SerializeObject(result),
        Headers = new Dictionary<string, string>
        {
          { "Content-Type", "application/json" },
          { "Access-Control-Allow-Origin", "*" }
        }
      };
    }

    public async Task<APIGatewayProxyResponse> RemoveUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      string userId = null;
      if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
        userId = request.PathParameters[ID_QUERY_STRING_NAME];
      else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
        userId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

      if (string.IsNullOrEmpty(userId))
      {

        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.BadRequest,
          Body = JsonConvert.SerializeObject(new
          {
            msg = $"Missing required parameter {ID_QUERY_STRING_NAME}"
          }),
          Headers = new Dictionary<string, string>
          {
            { "Content-Type", "application/json" },
            { "Access-Control-Allow-Origin", "*" }
          }
        };
      }

      context.Logger.LogLine($"Deleting blog with id {userId}");
      await this.DDBContext.DeleteAsync<User>(userId);

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Headers = new Dictionary<string, string>
        {
          { "Access-Control-Allow-Origin", "*" }
        }
      };
    }

    public async Task<APIGatewayProxyResponse> GetUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      string userId = null;
      if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
        userId = request.PathParameters[ID_QUERY_STRING_NAME];
      else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
        userId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

      if (string.IsNullOrEmpty(userId))
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.BadRequest,
          Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
        };
      }

      context.Logger.LogLine($"Getting user {userId}");
      var user = await DDBContext.LoadAsync<User>(userId);
      context.Logger.LogLine($"Found user: {user != null}");

      if (user == null)
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.NotFound,
        };
      }

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonConvert.SerializeObject(new UserDTO(user)),
        Headers = new Dictionary<string, string>
        {
          { "Content-Type", "application/json" },
          { "Access-Control-Allow-Origin", "*" }
        }
      };
    }

    public async Task<APIGatewayProxyResponse> GetUsersAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      context.Logger.LogLine("Getting users");
      var search = this.DDBContext.ScanAsync<User>(null);
      var page = await search.GetNextSetAsync();
      context.Logger.LogLine($"Found {page.Count} users");

      var users = page.Select(user => new UserDTO(user));

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonConvert.SerializeObject(users),
        Headers = new Dictionary<string, string>
        {
          { "Content-Type", "application/json" },
          { "Access-Control-Allow-Origin", "*" }
        }
      };
    }

    public APIGatewayProxyResponse AuthUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {

      APIGatewayProxyResponse response = new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.Unauthorized
      };

      try
      {
        if (request.Headers.ContainsKey("Authorization") && request.Headers["Authorization"].Contains("Bearer "))
        {
          var authorization = request.Headers["Authorization"];
          var token = authorization.Substring("Bearer ".Length);
          if (AuthFunctions.ValidateJWT(token, "user", "admin", out ClaimsPrincipal claims))
            response.StatusCode = (int)HttpStatusCode.OK;
        }
      }
      catch (System.Exception ex)
      {
        response.StatusCode = (int)HttpStatusCode.InternalServerError;
        response.Body = JsonConvert.SerializeObject(new
        {
          message = ex.Message,
          stack = ex.StackTrace
        });
      }

      return response;
    }

    public async Task<APIGatewayProxyResponse> UserLoginAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {

      var login = JsonConvert.DeserializeObject<Login>(request?.Body);
      var user = await DDBContext.LoadAsync<User>(login.Email);
      if (user == null)
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.NotFound,
        };
      }

      var pass = PasswordCrypt(login.Email, login.Password);
      if (string.Compare(pass, user.Password) != 0)
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.NotFound,
          Body = JsonConvert.SerializeObject(new
          {
            message = "Password mismatches."
          }),
          Headers = new Dictionary<string, string>
            {
              { "Content-Type", "application/json" },
              { "Access-Control-Allow-Origin", "*" }
            }
        };
      }

      string token = "";

      try
      {
        token = AuthFunctions.CreateJWT((claims) =>
        {
          var role = user.IsAdmin ? "admin" : "normal";
          claims.Add(new Claim(ClaimTypes.Role, role));
          claims.Add(new Claim(ClaimTypes.Name, user.Name));
          claims.Add(new Claim(ClaimTypes.Email, user.Email));
        });

      }
      catch (System.Exception ex)
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.InternalServerError,
          Body = JsonConvert.SerializeObject(new
          {
            message = ex.Message,
          }),
          Headers = new Dictionary<string, string>
          {
            { "Content-Type", "application/json" },
            { "Access-Control-Allow-Origin", "*" }
          }
        };
      }

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonConvert.SerializeObject(
          new { token = token, name = user.Name, email = user.Email }),
        Headers = new Dictionary<string, string>
        {
          { "Content-Type", "application/json" },
          { "Access-Control-Allow-Origin", "*" }
        }
      };

    }
  }

  public class AuthFunctions
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
        isAuthorized = AuthFunctions.ValidateJWT(request.AuthorizationToken, ClaimTypes.Role, "admin", out claims);
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

  public class Functions
  {

    // This const is the name of the environment variable that the serverless.template will use to set
    // the name of the DynamoDB table used to store blog posts.
    const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "BlogTable";

    public const string ID_QUERY_STRING_NAME = "Permalink";
    IDynamoDBContext DDBContext { get; set; }

    /// <summary>
    /// Default constructor that Lambda will invoke.
    /// </summary>
    public Functions()
    {
      // Check to see if a table name was passed in through environment variables and if so 
      // add the table mapping.
      var tableName = System.Environment.GetEnvironmentVariable(TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP);
      if (!string.IsNullOrEmpty(tableName))
      {
        AWSConfigsDynamoDB.Context.TypeMappings[typeof(Blog)] = new Amazon.Util.TypeMapping(typeof(Blog), tableName);
      }

      var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
      this.DDBContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);

      JsonConvert.DefaultSettings = () => new JsonSerializerSettings
      {
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
      };
    }

    /// <summary>
    /// Constructor used for testing passing in a preconfigured DynamoDB client.
    /// </summary>
    /// <param name="ddbClient"></param>
    /// <param name="tableName"></param>
    public Functions(IAmazonDynamoDB ddbClient, string tableName)
    {
      if (!string.IsNullOrEmpty(tableName))
      {
        AWSConfigsDynamoDB.Context.TypeMappings[typeof(Blog)] = new Amazon.Util.TypeMapping(typeof(Blog), tableName);
      }

      var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
      this.DDBContext = new DynamoDBContext(ddbClient, config);

      JsonConvert.DefaultSettings = () => new JsonSerializerSettings
      {
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
      };
    }

    /// <summary>
    /// A Lambda function that returns back a page worth of blog posts.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>The list of blogs</returns>
    public async Task<APIGatewayProxyResponse> GetBlogsAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      context.Logger.LogLine("Getting blogs");
      var search = this.DDBContext.ScanAsync<Blog>(null);
      var page = await search.GetNextSetAsync();
      context.Logger.LogLine($"Found {page.Count} blogs");

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonConvert.SerializeObject(page),
        Headers = new Dictionary<string, string>
        {
          { "Content-Type", "application/json" },
          { "Access-Control-Allow-Origin", "*" }
        }
      };
    }

    /// <summary>
    /// A Lambda function that returns the blog identified by blogId
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<APIGatewayProxyResponse> GetBlogAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      string blogId = null;
      if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
        blogId = request.PathParameters[ID_QUERY_STRING_NAME];
      else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
        blogId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

      if (string.IsNullOrEmpty(blogId))
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.BadRequest,
          Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
        };
      }

      context.Logger.LogLine($"Getting blog {blogId}");
      var blog = await DDBContext.LoadAsync<Blog>(blogId);
      context.Logger.LogLine($"Found blog: {blog != null}");

      if (blog == null)
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.NotFound,
        };
      }

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonConvert.SerializeObject(blog),
        Headers = new Dictionary<string, string>
        {
          { "Content-Type", "application/json" },
          { "Access-Control-Allow-Origin", "*" }
        }
      };
    }

    /// <summary>
    /// A Lambda function that adds a blog post.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<APIGatewayProxyResponse> AddBlogAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      var blog = JsonConvert.DeserializeObject<Blog>(request?.Body);
      // blog.Permalink = Guid.NewGuid().ToString();
      blog.Created = DateTime.Now;

      context.Logger.LogLine($"Saving blog with id {blog.Permalink}");
      await DDBContext.SaveAsync<Blog>(blog);

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonConvert.SerializeObject(blog),
        Headers = new Dictionary<string, string>
        {
          { "Content-Type", "application/json" },
          { "Access-Control-Allow-Origin", "*" }
        }
      };
    }

    /// <summary>
    /// A Lambda function that removes a blog post from the DynamoDB table.
    /// </summary>
    /// <param name="request"></param>
    public async Task<APIGatewayProxyResponse> RemoveBlogAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      string blogId = null;
      if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
        blogId = request.PathParameters[ID_QUERY_STRING_NAME];
      else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
        blogId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

      if (string.IsNullOrEmpty(blogId))
      {

        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.BadRequest,
          Body = JsonConvert.SerializeObject(new
          {
            msg = $"Missing required parameter {ID_QUERY_STRING_NAME}"
          }),
          Headers = new Dictionary<string, string>
          {
            { "Content-Type", "application/json" },
            { "Access-Control-Allow-Origin", "*" }
          }
        };
      }

      context.Logger.LogLine($"Deleting blog with id {blogId}");
      await this.DDBContext.DeleteAsync<Blog>(blogId);

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Headers = new Dictionary<string, string>
        {
          { "Access-Control-Allow-Origin", "*" }
        }
      };
    }

  }
}
