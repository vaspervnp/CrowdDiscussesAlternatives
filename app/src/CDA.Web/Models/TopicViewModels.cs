using System.ComponentModel.DataAnnotations;
using CDA.Application.Topics;
using CDA.Domain.Topics;
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

public sealed class TopicListViewModel
{
    public required IReadOnlyList<TopicView> Topics { get; init; }

    public required TopicSort Sort { get; init; }

    public string? NextCursor { get; init; }
}
