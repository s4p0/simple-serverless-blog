using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using BlogApi.Models.DTO;
using BlogApi.Models.Entity;
using Newtonsoft.Json;

namespace BlogApi.Functions.Manager
{
  public class UserManager
  {
    const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "UserTable";
    const string ID_QUERY_STRING_NAME = "Email";
    const string SALT_ENVIRONMENT_VARIABLE = "Salt";

    public DynamoDBContext DDBContext { get; }

    public UserManager()
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

      return Models.Lambda.Response.CreateResponse(result);
    }

    public async Task<APIGatewayProxyResponse> RemoveUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      string userId = null;
      if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
        userId = request.PathParameters[ID_QUERY_STRING_NAME];
      else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
        userId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

      if (string.IsNullOrEmpty(userId))
        return Models.Lambda.Response.CreateErrorResponse($"Missing required parameter {ID_QUERY_STRING_NAME}");

      context.Logger.LogLine($"Deleting blog with id {userId}");
      await this.DDBContext.DeleteAsync<User>(userId);

      return Models.Lambda.Response.CreateResponse();
    }

    public async Task<APIGatewayProxyResponse> GetUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      string userId = null;
      if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
        userId = request.PathParameters[ID_QUERY_STRING_NAME];
      else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
        userId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

      if (string.IsNullOrEmpty(userId))
        return Models.Lambda.Response.CreateErrorResponse($"Missing required parameter {ID_QUERY_STRING_NAME}");


      // context.Logger.LogLine($"Getting user {userId}");
      var user = await DDBContext.LoadAsync<User>(userId);
      // context.Logger.LogLine($"Found user: {user != null}");

      if (user == null)
        return Models.Lambda.Response.CreateResponse(status: HttpStatusCode.NotFound);

      return Models.Lambda.Response.CreateResponse(new UserDTO(user));
    }

    public async Task<APIGatewayProxyResponse> GetUsersAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      // context.Logger.LogLine("Getting users");
      var search = this.DDBContext.ScanAsync<User>(null);
      var page = await search.GetNextSetAsync();
      // context.Logger.LogLine($"Found {page.Count} users");

      var users = page.Select(user => new UserDTO(user));

      return Models.Lambda.Response.CreateResponse(users);
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
          if (AuthManager.ValidateJWT(token, "user", "admin", out ClaimsPrincipal claims))
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
        return Models.Lambda.Response.CreateResponse(status: HttpStatusCode.NotFound);

      var pass = PasswordCrypt(login.Email, login.Password);
      if (string.Compare(pass, user.Password) != 0)
        return Models.Lambda.Response.CreateResponse("Password mismatches.", HttpStatusCode.NotFound);

      string token = "";

      try
      {
        token = AuthManager.CreateJWT((claims) =>
        {
          var role = user.IsAdmin ? "admin" : "normal";
          claims.Add(new Claim(ClaimTypes.Role, role));
          claims.Add(new Claim(ClaimTypes.Name, user.Name));
          claims.Add(new Claim(ClaimTypes.Email, user.Email));
        });

      }
      catch (System.Exception ex)
      {
        return Models.Lambda.Response.CreateErrorResponse(ex.Message);
      }

      return Models.Lambda.Response.CreateResponse(
        new { token = token, name = user.Name, email = user.Email }
      );

    }
  }
}