namespace BlogApi.Models.Entity
{
  public class Login
  {
    private string _email;
    public string Email
    {
      get { return _email; }
      set { _email = value.ToLowerInvariant(); }
    }
    public string Password { get; set; }
  }
}