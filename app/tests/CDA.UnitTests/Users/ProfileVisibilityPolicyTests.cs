using CDA.Application.Users;
using CDA.Domain.Users;

namespace CDA.UnitTests.Users;

/// <summary>
/// These rules decide what one participant may learn about another, and they are applied in
/// exactly one place, so this is where they get exercised properly.
/// </summary>
public class ProfileVisibilityPolicyTests
{
    private static readonly DateTime Now = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StrangerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static UserProfile Owner()
    {
        var profile = new UserProfile(OwnerId, "Alexandra", Now.AddDays(-30));
        profile.EditDetails(
            realName: "Alexandra Papadopoulou",
            email: "alexandra@example.com",
            location: "Thessaloniki",
            website: "https://example.com",
            biography: "Interested in transport policy.");
        return profile;
    }

    public static TheoryData<ProfileField> AllFields() =>
        [.. Enum.GetValues<ProfileField>()];

    [Theory]
    [MemberData(nameof(AllFields))]
    public void A_new_profile_hides_every_configurable_field(ProfileField field)
    {
        // Least exposure by default: registering must not publish anything the user has not
        // chosen to publish.
        var profile = new UserProfile(OwnerId, "Alexandra", Now);

        Assert.Equal(ProfileVisibility.Private, profile.VisibilityOf(field));
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    public void Owners_see_their_own_fields_however_they_are_configured(ProfileField field)
    {
        var profile = Owner();
        profile.SetVisibility(field, ProfileVisibility.Private);

        Assert.True(ProfileVisibilityPolicy.CanSee(profile, field, new ProfileViewer(OwnerId)));
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    public void Administrators_see_private_fields(ProfileField field)
    {
        var profile = Owner();
        profile.SetVisibility(field, ProfileVisibility.Private);

        var administrator = new ProfileViewer(StrangerId, IsAdministrator: true);

        Assert.True(ProfileVisibilityPolicy.CanSee(profile, field, administrator));
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    public void Private_fields_are_hidden_from_other_members(ProfileField field)
    {
        var profile = Owner();
        profile.SetVisibility(field, ProfileVisibility.Private);

        Assert.False(ProfileVisibilityPolicy.CanSee(profile, field, new ProfileViewer(StrangerId)));
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    public void Members_only_fields_are_hidden_from_anonymous_visitors(ProfileField field)
    {
        var profile = Owner();
        profile.SetVisibility(field, ProfileVisibility.Members);

        Assert.True(ProfileVisibilityPolicy.CanSee(profile, field, new ProfileViewer(StrangerId)));
        Assert.False(ProfileVisibilityPolicy.CanSee(profile, field, ProfileViewer.Anonymous));
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    public void Public_fields_are_visible_to_everyone(ProfileField field)
    {
        var profile = Owner();
        profile.SetVisibility(field, ProfileVisibility.Public);

        Assert.True(ProfileVisibilityPolicy.CanSee(profile, field, ProfileViewer.Anonymous));
    }

    [Fact]
    public void Projection_strips_every_field_the_viewer_may_not_see()
    {
        var profile = Owner();

        var view = ProfileVisibilityPolicy.Project(profile, ProfileViewer.Anonymous, Now);

        Assert.Null(view.RealName);
        Assert.Null(view.Email);
        Assert.Null(view.Location);
        Assert.Null(view.Website);
        Assert.Null(view.Biography);
        Assert.Null(view.IsOnline);
    }

    [Fact]
    public void Projection_always_carries_the_display_name()
    {
        // It appears on everything the user posts, so hiding it here would be theatre.
        var profile = Owner();

        var view = ProfileVisibilityPolicy.Project(profile, ProfileViewer.Anonymous, Now);

        Assert.Equal("Alexandra", view.DisplayName);
        Assert.False(view.IsSelf);
    }

    [Fact]
    public void Projection_exposes_a_field_only_at_the_level_its_owner_chose()
    {
        var profile = Owner();
        profile.SetVisibility(ProfileField.Location, ProfileVisibility.Members);
        profile.SetVisibility(ProfileField.Biography, ProfileVisibility.Public);

        var anonymous = ProfileVisibilityPolicy.Project(profile, ProfileViewer.Anonymous, Now);
        var member = ProfileVisibilityPolicy.Project(profile, new ProfileViewer(StrangerId), Now);

        Assert.Null(anonymous.Location);
        Assert.Equal("Interested in transport policy.", anonymous.Biography);

        Assert.Equal("Thessaloniki", member.Location);
        Assert.Equal("Interested in transport policy.", member.Biography);
    }

    [Fact]
    public void A_hidden_field_is_indistinguishable_from_an_empty_one()
    {
        // Reporting the difference would disclose that hidden data exists, which is exactly
        // what the owner asked not to share.
        var filledIn = Owner();
        var neverFilledIn = new UserProfile(OwnerId, "Alexandra", Now);
        neverFilledIn.SetVisibility(ProfileField.Location, ProfileVisibility.Public);

        var hidden = ProfileVisibilityPolicy.Project(filledIn, ProfileViewer.Anonymous, Now);
        var empty = ProfileVisibilityPolicy.Project(neverFilledIn, ProfileViewer.Anonymous, Now);

        Assert.Equal(empty.Location, hidden.Location);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-4, true)]
    [InlineData(-30, false)]
    public void Online_status_follows_the_last_seen_window(int minutesAgo, bool expectedOnline)
    {
        // Separate profiles per case: Seen only ever moves forward, so one instance cannot
        // be walked backwards through these times.
        var profile = Owner();
        profile.SetVisibility(ProfileField.OnlineStatus, ProfileVisibility.Public);
        profile.Seen(Now.AddMinutes(minutesAgo));

        var view = ProfileVisibilityPolicy.Project(profile, ProfileViewer.Anonymous, Now);

        Assert.Equal(expectedOnline, view.IsOnline);
    }

    [Fact]
    public void A_user_who_has_never_signed_in_is_not_online()
    {
        var profile = Owner();
        profile.SetVisibility(ProfileField.OnlineStatus, ProfileVisibility.Public);

        Assert.False(ProfileVisibilityPolicy.Project(profile, ProfileViewer.Anonymous, Now).IsOnline);
    }

    [Fact]
    public void Hiding_online_status_reports_null_rather_than_offline()
    {
        // "Offline" would be a claim about the user; null is the absence of one.
        var profile = Owner();
        profile.Seen(Now);

        Assert.Null(ProfileVisibilityPolicy.Project(profile, ProfileViewer.Anonymous, Now).IsOnline);
    }

    [Fact]
    public void Seen_ignores_a_clock_that_moves_backwards()
    {
        var profile = Owner();
        profile.Seen(Now);
        profile.Seen(Now.AddMinutes(-10));

        profile.SetVisibility(ProfileField.OnlineStatus, ProfileVisibility.Public);

        Assert.True(profile.IsOnlineAt(Now.AddMinutes(4)));
    }
}
