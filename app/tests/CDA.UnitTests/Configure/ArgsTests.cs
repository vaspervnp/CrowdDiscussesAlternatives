using CDA.Configure;

namespace CDA.UnitTests.Configure;

public class ArgsTests
{
    [Fact]
    public void Value_options_are_read_in_either_form()
    {
        var spaced = Args.Parse(["--host", "db.example.com", "--port", "3307"]);
        Assert.Equal("db.example.com", spaced.Host);
        Assert.Equal("3307", spaced.Port);

        var inline = Args.Parse(["--host=db.example.com", "--port=3307"]);
        Assert.Equal("db.example.com", inline.Host);
        Assert.Equal("3307", inline.Port);
    }

    [Fact]
    public void Switches_are_recognised()
    {
        Assert.True(Args.Parse(["--help"]).Help);
        Assert.True(Args.Parse(["-h"]).Help);
        Assert.True(Args.Parse(["--show"]).Show);
        Assert.True(Args.Parse(["--path"]).ShowPath);
        Assert.True(Args.Parse(["--no-test"]).NoTest);
    }

    [Fact]
    public void Nothing_to_build_from_is_reported_as_such()
    {
        Assert.False(Args.Parse([]).HasBuildInputs);
        Assert.False(Args.Parse(["--show"]).HasBuildInputs);
        Assert.True(Args.Parse(["--host", "x"]).HasBuildInputs);
        Assert.True(Args.Parse(["--connection", "Server=x"]).HasBuildInputs);
    }

    [Fact]
    public void An_unknown_option_is_an_error_rather_than_ignored()
    {
        // A silently ignored typo would leave a setting unchanged and look like it worked.
        Assert.Throws<BadInputException>(() => Args.Parse(["--hsot", "x"]));
    }

    [Fact]
    public void A_value_option_with_no_value_is_an_error()
    {
        Assert.Throws<BadInputException>(() => Args.Parse(["--host"]));
    }

    [Fact]
    public void A_bare_word_is_an_error()
    {
        Assert.Throws<BadInputException>(() => Args.Parse(["host.example.com"]));
    }
}
