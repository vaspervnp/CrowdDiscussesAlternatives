using CDA.Domain.Similarity;

namespace CDA.UnitTests.Similarity;

/// <summary>
/// The folding of duplicates. It decides what a reader sees, so it gets exercised properly.
/// </summary>
public class SimilarityGraphTests
{
    private static readonly Guid A = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid B = new("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid C = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid D = new("dddddddd-0000-0000-0000-000000000004");

    private static Dictionary<Guid, int> Scores(params (Guid Id, int Score)[] scores) =>
        scores.ToDictionary(s => s.Id, s => s.Score);

    [Fact]
    public void With_no_reports_nothing_is_folded()
    {
        var result = SimilarityGraph.Collapse([], Scores());

        Assert.Empty(result.Hidden);
        Assert.Equal(A, result.RepresentativeFor(A));
    }

    [Fact]
    public void A_pair_folds_to_the_one_judged_better_written()
    {
        // The reporter read both closely enough to notice they matched, so their judgement is
        // the best signal available — ahead of the crowd's score.
        var result = SimilarityGraph.Collapse(
            [new SimilarityEdge(A, B, BetterWritten: B)],
            Scores((A, 100), (B, 0)));

        Assert.Equal(B, result.RepresentativeFor(A));
        Assert.Equal(B, result.RepresentativeFor(B));
        Assert.Equal([A], result.Hidden);
    }

    [Fact]
    public void With_no_preference_the_better_supported_proposal_stands_for_the_pair()
    {
        var result = SimilarityGraph.Collapse(
            [new SimilarityEdge(A, B, BetterWritten: null)],
            Scores((A, 5), (B, 2)));

        Assert.Equal(A, result.RepresentativeFor(B));
    }

    [Fact]
    public void A_chain_of_reports_folds_into_one_group()
    {
        // A~B and B~C means all three are the same idea. Showing A and C separately would still
        // split the crowd's support, which is exactly what this exists to prevent.
        var result = SimilarityGraph.Collapse(
            [new SimilarityEdge(A, B, null), new SimilarityEdge(B, C, null)],
            Scores((A, 1), (B, 9), (C, 3)));

        Assert.Equal(B, result.RepresentativeFor(A));
        Assert.Equal(B, result.RepresentativeFor(C));
        Assert.Equal(3, result.Members[B].Count);
        Assert.Equal(2, result.Hidden.Count);
    }

    [Fact]
    public void Separate_groups_stay_separate()
    {
        var result = SimilarityGraph.Collapse(
            [new SimilarityEdge(A, B, null), new SimilarityEdge(C, D, null)],
            Scores((A, 5), (B, 1), (C, 1), (D, 5)));

        Assert.Equal(A, result.RepresentativeFor(B));
        Assert.Equal(D, result.RepresentativeFor(C));
        Assert.Equal(2, result.Members.Count);
    }

    [Fact]
    public void The_most_often_preferred_wording_wins_a_larger_group()
    {
        var result = SimilarityGraph.Collapse(
            [
                new SimilarityEdge(A, B, BetterWritten: B),
                new SimilarityEdge(B, C, BetterWritten: B),
                new SimilarityEdge(A, C, BetterWritten: C),
            ],
            Scores((A, 50), (B, 0), (C, 0)));

        Assert.Equal(B, result.RepresentativeFor(A));
    }

    [Fact]
    public void The_choice_does_not_depend_on_iteration_order()
    {
        // Everything tied: without the final tie-break on id, which proposal represented the
        // group could change between requests and the list would appear to shuffle itself.
        var forwards = SimilarityGraph.Collapse(
            [new SimilarityEdge(A, B, null)], Scores((A, 0), (B, 0)));
        var backwards = SimilarityGraph.Collapse(
            [new SimilarityEdge(B, A, null)], Scores((B, 0), (A, 0)));

        Assert.Equal(forwards.RepresentativeFor(A), backwards.RepresentativeFor(A));
    }

    [Fact]
    public void A_duplicate_report_of_the_same_pair_changes_nothing()
    {
        var result = SimilarityGraph.Collapse(
            [new SimilarityEdge(A, B, null), new SimilarityEdge(A, B, null)],
            Scores((A, 3), (B, 1)));

        Assert.Equal(2, result.Members[A].Count);
        Assert.Single(result.Hidden);
    }

    [Fact]
    public void Every_member_of_a_group_maps_to_the_same_representative()
    {
        var result = SimilarityGraph.Collapse(
            [
                new SimilarityEdge(A, B, null),
                new SimilarityEdge(C, D, null),
                new SimilarityEdge(B, C, null),
            ],
            Scores((A, 1), (B, 2), (C, 3), (D, 4)));

        var representatives = new[] { A, B, C, D }.Select(result.RepresentativeFor).Distinct().ToList();

        Assert.Single(representatives);
        Assert.Equal(D, representatives[0]);
        Assert.Equal(3, result.Hidden.Count);
    }
}
