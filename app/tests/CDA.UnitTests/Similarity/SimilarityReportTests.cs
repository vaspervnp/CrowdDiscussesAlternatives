using CDA.Domain.Similarity;

namespace CDA.UnitTests.Similarity;

public class SimilarityReportTests
{
    private static readonly DateTime Now = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Low = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid High = new("99999999-0000-0000-0000-000000000009");

    [Fact]
    public void The_pair_is_stored_in_a_fixed_order_whichever_way_it_is_reported()
    {
        // Otherwise "A is like B" and "B is like A" are two rows making one claim, and the
        // votes that decide whether it takes effect are split between them.
        var forwards = SimilarityReport.Between(Guid.NewGuid(), Low, High, Guid.NewGuid(), null, null, Now);
        var backwards = SimilarityReport.Between(Guid.NewGuid(), High, Low, Guid.NewGuid(), null, null, Now);

        Assert.Equal(forwards.ProposalAId, backwards.ProposalAId);
        Assert.Equal(forwards.ProposalBId, backwards.ProposalBId);
        Assert.True(forwards.ProposalAId.CompareTo(forwards.ProposalBId) < 0);
    }

    [Fact]
    public void A_proposal_cannot_be_similar_to_itself()
    {
        Assert.Throws<ArgumentException>(
            () => SimilarityReport.Between(Guid.NewGuid(), Low, Low, Guid.NewGuid(), null, null, Now));
    }

    [Fact]
    public void The_better_written_proposal_must_be_one_of_the_pair()
    {
        Assert.Throws<ArgumentException>(
            () => SimilarityReport.Between(
                Guid.NewGuid(), Low, High, Guid.NewGuid(), Guid.NewGuid(), null, Now));
    }

    [Fact]
    public void A_report_takes_effect_only_once_it_clears_the_readers_threshold()
    {
        var report = SimilarityReport.Between(Guid.NewGuid(), Low, High, Guid.NewGuid(), null, null, Now);
        report.ApplyVoteDelta(2, 2);

        Assert.True(report.IsActiveAt(1));
        Assert.True(report.IsActiveAt(2));
        Assert.False(report.IsActiveAt(3));
    }

    [Fact]
    public void A_report_the_crowd_rejects_never_takes_effect()
    {
        var report = SimilarityReport.Between(Guid.NewGuid(), Low, High, Guid.NewGuid(), null, null, Now);
        report.ApplyVoteDelta(-3, 3);

        Assert.False(report.IsActiveAt(1));
    }

    [Fact]
    public void Either_side_can_be_asked_for_the_other()
    {
        var report = SimilarityReport.Between(Guid.NewGuid(), Low, High, Guid.NewGuid(), null, null, Now);

        Assert.Equal(High, report.Other(Low));
        Assert.Equal(Low, report.Other(High));
        Assert.True(report.Involves(Low));
        Assert.False(report.Involves(Guid.NewGuid()));
    }

    [Fact]
    public void Blank_justification_is_stored_as_nothing_rather_than_whitespace()
    {
        var report = SimilarityReport.Between(Guid.NewGuid(), Low, High, Guid.NewGuid(), null, "   ", Now);

        Assert.Null(report.Justification);
    }
}
