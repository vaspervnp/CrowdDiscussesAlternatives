using System.ComponentModel.DataAnnotations;
using CDA.Domain.Users;

namespace CDA.Web.Models;

public sealed class RegisterViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(UserProfile.DisplayNameMaxLength, MinimumLength = 2)]
    [Display(Name = "Display name", Description = "Shown on everything you post. Cannot be hidden.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(200, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Stay signed in")]
    public bool RememberMe { get; set; }
}

public sealed class EditProfileViewModel
{
    [StringLength(UserProfile.RealNameMaxLength)]
    [Display(Name = "Real name")]
    public string? RealName { get; set; }

    [EmailAddress]
    [Display(Name = "Contact email")]
    public string? Email { get; set; }

    [StringLength(UserProfile.LocationMaxLength)]
    public string? Location { get; set; }

    [Url]
    [StringLength(UserProfile.WebsiteMaxLength)]
    public string? Website { get; set; }

    [StringLength(UserProfile.BiographyMaxLength)]
    [DataType(DataType.MultilineText)]
    public string? Biography { get; set; }

    public ProfileVisibility RealNameVisibility { get; set; }
    public ProfileVisibility EmailVisibility { get; set; }
    public ProfileVisibility LocationVisibility { get; set; }
    public ProfileVisibility WebsiteVisibility { get; set; }
    public ProfileVisibility BiographyVisibility { get; set; }

    [Display(Name = "Online status")]
    public ProfileVisibility OnlineStatusVisibility { get; set; }
}
