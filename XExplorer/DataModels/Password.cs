using System.ComponentModel.DataAnnotations.Schema;

namespace XExplorer.DataModels;

[Table("Passwords")]
public class Passwords : ModelBase
{
	public string? Password { get; set; }
}