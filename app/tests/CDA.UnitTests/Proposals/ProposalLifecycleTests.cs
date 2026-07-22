using CDA.Domain.Proposals;

namespace CDA.UnitTests.Proposals;

/// <summary>
/// The editing window is the rule that makes a proposal's wording trustworthy: votes attach to
/// text that has stopped moving.
/// </summary>
public class ProposalLifecycleTests
{
    private static readonly DateTime Now = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    private static Proposal Proposal(DateTime? editableUntil = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A toll fee is suggested.", Now, editableUntil);

    [Fact]
    public void A_new_proposal_is_editable_and_not_yet_votable()
    {
        var proposal = Proposal();

        Assert.False(proposal.IsLockedAt(Now));
        Assert.Equal(Now + Domain.Proposals.Proposal.DefaultEditingWindow, proposal.EditableUntilUtc);
    }

    [Fact]
    public void A_proposal_locks_when_its_window_expires()
    {
        var proposal = Proposal(Now.AddDays(1));

        Assert.False(proposal.IsLockedAt(Now.AddHours(23)));
        Assert.True(proposal.IsLockedAt(Now.AddDays(1)));
        Assert.True(proposal.IsLockedAt(Now.AddDays(2)));
    }

    [Fact]
    public void An_editing_window_longer_than_the_ceiling_is_refused()
    {
        // A proposal that never locks can never be voted on, so without a ceiling an author
        // could park one in the pool and keep it permanently beyond judgement.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Proposal(Now + Domain.Proposals.Proposal.MaximumEditingWindow + TimeSpan.FromDays(1)));
    }

    [Fact]
    public void The_editing_window_can_be_shortened_but_not_extended()
    {
        var proposal = Proposal(Now.AddDays(5));

        proposal.BringLockForward(Now.AddDays(2), Now);
        Assert.Equal(Now.AddDays(2), proposal.EditableUntilUtc);

        // Extending would let an author who dislikes how opinion is forming stay out of reach.
        Assert.Throws<InvalidOperationException>(() => proposal.BringLockForward(Now.AddDays(4), Now));
        Assert.Equal(Now.AddDays(2), proposal.EditableUntilUtc);
    }

    [Fact]
    public void Locking_early_ends_the_window_immediately()
    {
        var proposal = Proposal(Now.AddDays(5));

        proposal.LockNow(Now);

        Assert.True(proposal.IsLockedAt(Now));
    }

    [Fact]
    public void A_locked_proposal_cannot_be_reworded()
    {
        var proposal = Proposal(Now.AddDays(1));
        proposal.LockNow(Now);

        var error = Assert.Throws<InvalidOperationException>(
            () => proposal.Edit("Something else entirely", Now));

        Assert.Contains("locked", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("A toll fee is suggested.", proposal.Text);
    }

    [Fact]
    public void Editing_within_the_window_records_that_it_happened()
    {
        var proposal = Proposal(Now.AddDays(1));

        proposal.Edit("A toll fee of one euro is suggested.", Now.AddHours(1));

        Assert.Equal("A toll fee of one euro is suggested.", proposal.Text);
        Assert.Equal(Now.AddHours(1), proposal.EditedAtUtc);
    }

    [Fact]
    public void A_proposal_requires_text()
    {
        Assert.Throws<ArgumentException>(
            () => new Proposal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  ", Now));
    }

    [Fact]
    public void Recording_a_comment_moves_the_recently_discussed_ordering()
    {
        var proposal = Proposal();

        Assert.Null(proposal.LastCommentAtUtc);
        Assert.Equal(0, proposal.CommentCount);

        proposal.RecordComment(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(5), proposal.LastCommentAtUtc);
        Assert.Equal(1, proposal.CommentCount);
    }
}
