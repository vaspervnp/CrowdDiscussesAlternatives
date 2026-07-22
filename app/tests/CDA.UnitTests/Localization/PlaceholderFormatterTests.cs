using System.Globalization;
using CDA.Application.Localization;

namespace CDA.UnitTests.Localization;

/// <summary>
/// The formatter is the whole reason placeholders are named rather than positional, so the
/// reordering case is the one that matters most.
/// </summary>
public class PlaceholderFormatterTests
{
    private static string Apply(string template, params (string, object?)[] data) =>
        PlaceholderFormatter.Apply(template, data);

    [Fact]
    public void A_named_hole_is_filled()
    {
        Assert.Equal("You have 3 new messages", Apply("You have %count% new messages", ("count", 3)));
    }

    [Fact]
    public void The_translation_may_reorder_the_holes()
    {
        // The point of named holes: the same two values, written in whichever order the target
        // language wants, with no change to the calling code.
        var data = new (string, object?)[] { ("name", "Chair"), ("topic", "Traffic") };

        Assert.Equal(
            "Chair started Traffic",
            PlaceholderFormatter.Apply("%name% started %topic%", data));
        Assert.Equal(
            "Traffic was started by Chair",
            PlaceholderFormatter.Apply("%topic% was started by %name%", data));
    }

    [Fact]
    public void The_same_hole_can_appear_more_than_once()
    {
        Assert.Equal("Chair, is that you, Chair?", Apply("%who%, is that you, %who%?", ("who", "Chair")));
    }

    [Fact]
    public void Matching_ignores_case()
    {
        // A translator should not have to remember whether the developer wrote %Count% or %count%.
        Assert.Equal("5 items", Apply("%Count% items", ("count", 5)));
    }

    [Fact]
    public void A_hole_with_no_value_is_left_visible()
    {
        // A loud %missing% in the page is a bug someone will notice; a silent blank is not.
        Assert.Equal("Sent to %missing%", Apply("Sent to %missing%", ("other", "x")));
    }

    [Fact]
    public void A_value_with_no_hole_is_simply_unused()
    {
        Assert.Equal("Nothing to fill", Apply("Nothing to fill", ("count", 3)));
    }

    [Fact]
    public void A_lone_percent_is_literal_text()
    {
        Assert.Equal("100% sure", Apply("100% sure", ("count", 3)));
    }

    [Fact]
    public void An_empty_pair_of_delimiters_is_left_alone()
    {
        Assert.Equal("50%% off", Apply("50%% off", ("count", 3)));
    }

    [Fact]
    public void Text_with_no_data_is_returned_unchanged()
    {
        Assert.Equal("%count% left", PlaceholderFormatter.Apply("%count% left", []));
    }

    [Fact]
    public void A_number_is_written_in_the_current_culture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("el-GR");

        try
        {
            // Greek writes the decimal separator as a comma; the value dropped into the sentence
            // must follow the sentence's language.
            Assert.Equal("1,5 MB το πολύ", Apply("%size% MB το πολύ", ("size", 1.5)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
