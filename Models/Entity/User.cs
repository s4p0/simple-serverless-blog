namespace BlogApi.Models.Entity
{
  public class User
  {
    public string Name { get; set; }

    private string _email;
    public string Email
    {
      get { return _email; }
      set { _email = value.ToLowerInvariant(); }
    }
    public string Password { get; set; }
    public bool IsAdmin { get; set; }
  }
}