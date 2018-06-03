using System.Collections.Generic;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;

namespace BlogApi.Models.Lambda
{
  public static class Response
  {
    public static APIGatewayProxyResponse CreateErrorResponse(string message)
    {
      return CreateResponse(message, HttpStatusCode.BadRequest);
    }

    public static APIGatewayProxyResponse CreateResponse(string message, HttpStatusCode status = HttpStatusCode.OK)
    {
      return CreateResponse(new { message = message }, status);
    }
    public static APIGatewayProxyResponse CreateResponse(object bodyObj = null, HttpStatusCode status = HttpStatusCode.OK)
    {
      if (bodyObj == null)
        bodyObj = new object();

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)status,
        Body = JsonConvert.SerializeObject(bodyObj),
        Headers = new Dictionary<string, string>
        {
          { "Content-Type", "application/json" },
          { "Access-Control-Allow-Origin", "*" }
        }
      };
    }
  }
}