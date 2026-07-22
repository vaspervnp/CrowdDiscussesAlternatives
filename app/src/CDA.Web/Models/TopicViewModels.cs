using System.ComponentModel.DataAnnotations;
using CDA.Application.Proposals;
using CDA.Application.Topics;
using CDA.Domain.Topics;
using CDA.Infrastructure.Attachments;
using CDA.Infrastructure.Discussion;
using CDA.Infrastructure.Messaging;
using CDA.Infrastructure.Notifications;
using CDA.Infrastructure.Evaluation;
using CDA.Infrastructure.Groups;
using CDA.Infrastructure.Parameters;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.Search;
using CDA.Infrastructure.References;
using CDA.Infrastructure.Similarity;
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

    /// <summary>Whether the reader has asked for duplicates to be folded together.</summary>
    public required bool Collapse { get; init; }

    /// <summary>How much agreement a similarity report needs before it folds a pair, for this reader.</summary>
    public required int Threshold { get; init; }
}

public sealed class ProposalDetailsViewModel
{
    public required TopicView Topic { get; init; }

    public required ProposalView Proposal { get; init; }

    public required IReadOnlyList<CommentView> Comments { get; init; }

    public required IReadOnlyList<ReferenceView> References { get; init; }

    public required IReadOnlyList<SimilarityView> Similarities { get; init; }

    public required IReadOnlyList<AttachmentView> Attachments { get; init; }

    public required bool CanCite { get; init; }
}

public sealed class GroupListViewModel
{
    public required TopicView Topic { get; init; }

    public required IReadOnlyList<GroupView> Groups { get; init; }

    public required GroupSort Sort { get; init; }

    public string? NextCursor { get; init; }

    /// <summary>Whose alternatives are listed first, and why.</summary>
    public required IReadOnlyList<string> TopCiters { get; init; }

    public required bool CanAssemble { get; init; }
}

public sealed class GroupDetailsViewModel
{
    public required TopicView Topic { get; init; }

    public required GroupView Group { get; init; }

    public required IReadOnlyList<CommentView> Comments { get; init; }

    public required bool CanComment { get; init; }
}

public sealed class AssembleGroupViewModel
{
    public required TopicView Topic { get; init; }

    public required IReadOnlyList<ProposalView> Pool { get; init; }

    public Guid? ImprovesGroupId { get; init; }

    public string? ImprovesDescription { get; init; }
}

public sealed class EvaluateViewModel
{
    public required Guid TopicId { get; init; }

    public required string TopicSubject { get; init; }

    public required EvaluationView Evaluation { get; init; }
}

public sealed class CompareViewModel
{
    public required Guid TopicId { get; init; }

    public required string TopicSubject { get; init; }

    public required Comparison Comparison { get; init; }
}

public sealed class SearchViewModel
{
    public required Guid TopicId { get; init; }

    public required string TopicSubject { get; init; }

    public string? Query { get; init; }

    public required SearchResultMode Mode { get; init; }

    public Guid? AuthorFilter { get; init; }

    public string? AuthorFilterName { get; init; }

    /// <summary>Null until a search has actually been run.</summary>
    public SearchResults? Results { get; init; }

    /// <summary>Everyone who has said anything in this topic, for the author filter.</summary>
    public required IReadOnlyDictionary<Guid, string> Contributors { get; init; }
}

public sealed class ParameterListViewModel
{
    public required Guid TopicId { get; init; }

    public required string TopicSubject { get; init; }

    public required IReadOnlyList<ParameterTableSummary> Tables { get; init; }

    public required bool CanCreate { get; init; }
}

public sealed class ParameterTableViewModel
{
    public required Guid TopicId { get; init; }

    public required string TopicSubject { get; init; }

    public required ParameterTableView Table { get; init; }
}

public sealed class ConversationListViewModel
{
    public required IReadOnlyList<ConversationSummary> Conversations { get; init; }
}

public sealed class ConversationViewModel
{
    public required Guid WithUserId { get; init; }

    public required string WithDisplayName { get; init; }

    public required IReadOnlyList<MessageView> Messages { get; init; }
}

public sealed class NotificationsViewModel
{
    public required IReadOnlyList<NotificationView> Notifications { get; init; }

    public required CDA.Domain.Notifications.NotificationDelivery Delivery { get; init; }

    /// <summary>False when no mail host is configured, so the page can say so.</summary>
    public required bool EmailWorks { get; init; }
}

public sealed class TopicListViewModel
{
    public required IReadOnlyList<TopicView> Topics { get; init; }

    public required TopicSort Sort { get; init; }

    public string? NextCursor { get; init; }
}
