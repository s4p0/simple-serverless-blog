using System;
using System.Collections.Generic;

namespace BlogApi.Models.Entity
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
}