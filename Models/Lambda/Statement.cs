namespace BlogApi.Models.Lambda
{
  public class Statement
  {
    public string Action { get; set; }
    public string Effect { get; set; }
    public string Resource { get; set; }
  }
}