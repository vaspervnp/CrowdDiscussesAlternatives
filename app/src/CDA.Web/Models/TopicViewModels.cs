using System.ComponentModel.DataAnnotations;
using CDA.Application.Proposals;
using CDA.Application.Topics;
using CDA.Domain.Topics;
using CDA.Infrastructure.Discussion;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.Topics;

namespace CDA.Web.Models;

public sealed class CreateTopicViewModel
{
    [Required]
    [StringLength(Topic.SubjectMaxLength, MinimumLength = 5)]
    [Display(Name = "Subject", Description = "The question the crowd is being asked to solve.")]
    public string Subject { get; set; } = string.Empty;

    [StringLength(Topic.DescriptionMaxLength)]
    [DataType(DataType.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Who can read this")]
    public TopicVisibility Visibility { get; set; } = TopicVisibility.Public;

    [DataType(DataType.Date)]
    [Display(Name = "Target completion date", Description = "When the discussion should have concluded.")]
    public DateTime? ClosesAt { get; set; }

    [Display(Name = "Hide vote counts until the topic closes")]
    public bool HideVoteCountsUntilClose { get; set; }
}

public sealed class TopicDetailsViewModel
{
    public required TopicView Topic { get; init; }

    public required IReadOnlyList<Requirement> Requirements { get; init; }

    public required IReadOnlyList<CommentView> Comments { get; init; }

    /// <summary>False once the topic has opened for proposals; the list is fixed from then on.</summary>
    public required bool RequirementsAreEditable { get; init; }

    public required bool CanComment { get; init; }
}

public sealed class ProposalListViewModel
{
    public required TopicView Topic { get; init; }

    public required IReadOnlyList<ProposalView> Proposals { get; init; }

    public required ProposalSort Sort { get; init; }

    public Guid? AuthorFilter { get; init; }

    public string? AuthorFilterName { get; init; }

    public string? NextCursor { get; init; }

    public required bool CanAdd { get; init; }
}

public sealed class ProposalDetailsViewModel
{
    public required TopicView Topic { get; init; }

    public required ProposalView Proposal { get; init; }

    public required IReadOnlyList<CommentView> Comments { get; init; }
}

public sealed class TopicListViewModel
{
    public required IReadOnlyList<TopicView> Topics { get; init; }

    public required TopicSort Sort { get; init; }

    public string? NextCursor { get; init; }
}
