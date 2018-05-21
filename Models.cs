using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlogApi
{
  public class Blog
  {
    public string Permalink { get; set; }
    public string Title { get; set; }
    public string Source { get; set; }
    public DateTime Created { get; set; }
    public List<string> Tags { get; set; }
    public string Author { get; set; }
  }

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

  public class UserDTO
  {
    public UserDTO()
    {

    }
    public UserDTO(User user)
    {
      Name = user.Name;
      Email = user.Email;
      IsAdmin = user.IsAdmin;
    }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool IsAdmin { get; set; }
  }

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

  public class TokenAuthorizerContext
  {
    public string Type { get; set; }
    public string AuthorizationToken { get; set; }
    public string MethodArn { get; set; }
  }

  public class AuthPolicy
  {
    public PolicyDocument policyDocument { get; set; }
    public string principalId { get; set; }
  }

  public class PolicyDocument
  {
    public string Version { get; set; }
    public Statement[] Statement { get; set; }
  }

  public class Statement
  {
    public string Action { get; set; }
    public string Effect { get; set; }
    public string Resource { get; set; }
  }

}