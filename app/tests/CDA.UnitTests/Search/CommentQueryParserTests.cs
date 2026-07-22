using CDA.Domain.Search;

namespace CDA.UnitTests.Search;

/// <summary>
/// Boolean mode has no AND or OR — a term prefixed with + is required and a bare term is
/// optional — so the whole translation happens here, and it is worth pinning down.
/// </summary>
public class CommentQueryParserTests
{
    private static string Expression(string input)
    {
        var query = CommentQueryParser.Parse(input);
        Assert.True(query.IsUsable, query.Error);
        return query.BooleanExpression;
    }

    [Fact]
    public void A_single_word_is_required()
    {
        Assert.Equal("+congestion", Expression("congestion"));
    }

    [Fact]
    public void Adjacent_words_mean_and()
    {
        // How nearly every search box behaves, and what people expect without thinking about it.
        Assert.Equal("+congestion +charging", Expression("congestion charging"));
    }

    [Fact]
    public void An_explicit_and_reads_the_same_as_adjacency()
    {
        Assert.Equal(Expression("congestion charging"), Expression("congestion AND charging"));
    }

    [Fact]
    public void Or_makes_both_sides_optional()
    {
        Assert.Equal("+(congestion charging)", Expression("congestion OR charging"));
    }

    [Fact]
    public void And_binds_more_tightly_than_or()
    {
        // "toll AND fee OR charge" is (toll AND fee) OR charge, as in every other language.
        Assert.Equal("+(+toll +fee charge)", Expression("toll AND fee OR charge"));
    }

    [Fact]
    public void Brackets_override_the_default_grouping()
    {
        Assert.Equal("+toll +(fee charge)", Expression("toll AND (fee OR charge)"));
    }

    [Fact]
    public void Two_bracketed_alternatives_can_be_required_together()
    {
        Assert.Equal("+(toll charge) +(bus tram)", Expression("(toll OR charge) AND (bus OR tram)"));
    }

    [Fact]
    public void A_quoted_phrase_is_kept_together()
    {
        Assert.Equal("+\"congestion charge\"", Expression("\"congestion charge\""));
    }

    [Fact]
    public void A_phrase_combines_with_other_terms()
    {
        Assert.Equal("+\"congestion charge\" +london", Expression("\"congestion charge\" london"));
    }

    [Fact]
    public void A_minus_excludes_a_word()
    {
        Assert.Equal("+toll -london", Expression("toll -london"));
    }

    [Fact]
    public void The_word_not_excludes_as_well()
    {
        Assert.Equal("+toll -london", Expression("toll NOT london"));
    }

    [Fact]
    public void Exclusion_stays_absolute_inside_an_or()
    {
        // Boolean mode has no notion of "optionally excluded", and neither does anyone's
        // intuition: a minus means the word must not appear.
        Assert.Equal("+(toll -london)", Expression("toll OR -london"));
    }

    [Fact]
    public void Operators_typed_inside_words_are_stripped_rather_than_obeyed()
    {
        // Otherwise a stray + or * from someone's clipboard silently changes what they searched
        // for — or makes the query invalid and errors at the database.
        Assert.Equal("+tollfee", Expression("toll+fee"));
        Assert.Equal("+congestion", Expression("*congestion*"));
    }

    [Fact]
    public void Words_the_index_cannot_match_are_reported_rather_than_dropped_quietly()
    {
        // innodb_ft_min_token_size is 3 on this server: shorter words are simply not indexed,
        // so a search containing them would otherwise return nothing with no explanation.
        var query = CommentQueryParser.Parse("bus is late");

        Assert.True(query.IsUsable, query.Error);
        Assert.Equal("+bus +late", query.BooleanExpression);
        Assert.Equal(["is"], query.IgnoredShortTerms);
    }

    [Fact]
    public void A_search_made_entirely_of_unindexable_words_says_so()
    {
        var query = CommentQueryParser.Parse("is it");

        Assert.False(query.IsUsable);
        Assert.Contains("too short", query.Error!);
    }

    [Fact]
    public void A_short_word_inside_a_phrase_is_kept()
    {
        // Phrase matching does not go through the same token filter, so "is" survives here.
        Assert.Equal("+\"the bus is late\"", Expression("\"the bus is late\""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void An_empty_search_is_refused(string? input)
    {
        Assert.False(CommentQueryParser.Parse(input).IsUsable);
    }

    [Fact]
    public void An_unclosed_bracket_is_reported()
    {
        var query = CommentQueryParser.Parse("toll AND (fee OR charge");

        Assert.False(query.IsUsable);
        Assert.Contains("unclosed bracket", query.Error!);
    }

    [Fact]
    public void An_unmatched_closing_bracket_is_reported()
    {
        var query = CommentQueryParser.Parse("toll) fee");

        Assert.False(query.IsUsable);
        Assert.Contains("unmatched closing bracket", query.Error!);
    }

    [Fact]
    public void A_trailing_operator_is_reported()
    {
        var query = CommentQueryParser.Parse("toll OR");

        Assert.False(query.IsUsable);
        Assert.Contains("ends in an operator", query.Error!);
    }

    [Fact]
    public void An_unterminated_quote_takes_the_rest_of_the_line()
    {
        // Nearly always a half-typed phrase rather than a mistake worth refusing.
        Assert.Equal("+\"congestion charge\"", Expression("\"congestion charge"));
    }

    [Fact]
    public void Operators_are_recognised_whatever_their_case()
    {
        Assert.Equal(Expression("toll AND fee"), Expression("toll and fee"));
        Assert.Equal(Expression("toll OR fee"), Expression("toll Or fee"));
    }

    [Fact]
    public void The_pros_and_cons_workflow_from_the_documents_parses()
    {
        // The use the platform's own documents describe: tagging proposals by writing marker
        // words in their comments, then pulling them back out.
        Assert.Equal("+(pros cons)", Expression("pros OR cons"));
        Assert.Equal("+cons -resolved", Expression("cons -resolved"));
    }
}
