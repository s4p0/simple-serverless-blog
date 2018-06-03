namespace BlogApi.Models.Lambda
{
  public class PolicyDocument
  {
    public string Version { get; set; }
    public Statement[] Statement { get; set; }
  }
}