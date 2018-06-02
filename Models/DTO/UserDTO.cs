using BlogApi.Models.Entity;

namespace BlogApi.Models.DTO
{
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
}