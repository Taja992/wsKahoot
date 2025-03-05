using System.ComponentModel.DataAnnotations;

namespace Api.Utilities;

public sealed class AppOptions
{
    [Required] public string DbConnectionString { get; set; } = null!;
    [Required] public string AdminPassword { get; set; } = null!;

}