using System.ComponentModel.DataAnnotations;
using CDA.Domain.Users;

namespace CDA.Web.Models;

// Every validation message is written out in full rather than left to the framework's
// "The {0} field is required." templates. That keeps each message a self-contained English
// sentence, which is exactly what the translation store keys on — so a rejected form speaks the
// reader's language too, with no property-name interpolation to translate around.
public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "An email address is required.")]
    [EmailAddress(ErrorMessage = "That is not a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A display name is required.")]
    [StringLength(UserProfile.DisplayNameMaxLength, MinimumLength = 2,
        ErrorMessage = "A display name is between 2 and 60 characters.")]
    [Display(Name = "Display name", Description = "Shown on everything you post. Cannot be hidden.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "A password is required.")]
    [DataType(DataType.Password)]
    [StringLength(200, MinimumLength = 12, ErrorMessage = "Use at least 12 characters.")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "An email address is required.")]
    [EmailAddress(ErrorMessage = "That is not a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A password is required.")]
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
