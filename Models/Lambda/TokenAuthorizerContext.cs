namespace BlogApi.Models.Lambda
{
  public class TokenAuthorizerContext
  {
    public string Type { get; set; }
    public string AuthorizationToken { get; set; }
    public string MethodArn { get; set; }
  }
}