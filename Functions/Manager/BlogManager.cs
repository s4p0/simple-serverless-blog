using System;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using BlogApi.Models.Entity;


namespace BlogApi.Functions.Manager
{
  public class BlogManager
  {

    // This const is the name of the environment variable that the serverless.template will use to set
    // the name of the DynamoDB table used to store blog posts.
    const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "BlogTable";

    public const string ID_QUERY_STRING_NAME = "Permalink";
    IDynamoDBContext DDBContext { get; set; }

    /// <summary>
    /// Default constructor that Lambda will invoke.
    /// </summary>
    public BlogManager()
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
    public BlogManager(IAmazonDynamoDB ddbClient, string tableName)
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
      // context.Logger.LogLine("Getting blogs");
      var search = this.DDBContext.ScanAsync<Blog>(null);
      var pages = await search.GetNextSetAsync();
      // context.Logger.LogLine($"Found {page.Count} blogs");

      if (pages.Any())
      {
        pages = pages.OrderByDescending(p => p.Created).ToList();
      }

      return Models.Lambda.Response.CreateResponse(pages);
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
        return Models.Lambda.Response.CreateResponse(
          $"Missing required parameter {ID_QUERY_STRING_NAME}"
          , HttpStatusCode.BadRequest);
      }

      // context.Logger.LogLine($"Getting blog {blogId}");
      var blog = await DDBContext.LoadAsync<Blog>(blogId);
      // context.Logger.LogLine($"Found blog: {blog != null}");

      if (blog == null)
      {
        return Models.Lambda.Response.CreateResponse(status: HttpStatusCode.NotFound);
      }

      return Models.Lambda.Response.CreateResponse(blog);
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

      // context.Logger.LogLine($"Saving blog with id {blog.Permalink}");
      await DDBContext.SaveAsync<Blog>(blog);

      return Models.Lambda.Response.CreateResponse(blog);
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
        return Models.Lambda.Response.CreateResponse(
          $"Missing required parameter {ID_QUERY_STRING_NAME}"
          , HttpStatusCode.BadRequest
        );
      }

      // context.Logger.LogLine($"Deleting blog with id {blogId}");
      await this.DDBContext.DeleteAsync<Blog>(blogId);

      return Models.Lambda.Response.CreateResponse(status: HttpStatusCode.Accepted);
    }

  }
}