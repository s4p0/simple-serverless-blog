namespace BlogApi.Models.Lambda
{
  public class AuthPolicy
  {
    public PolicyDocument policyDocument { get; set; }
    public string principalId { get; set; }
  }
}