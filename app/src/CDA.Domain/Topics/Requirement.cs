namespace CDA.Domain.Topics;

/// <summary>
/// One criterion an alternative solution must satisfy to count as solving this topic.
/// </summary>
/// <remarks>
/// Agreed during the discussion phase and then fixed. These are what groups of proposals get
/// scored against later, so a requirement that changes after people have evaluated against it
/// silently invalidates their evaluations — which is why the topic freezes them when it opens
/// for proposals.
/// </remarks>
public sealed class Requirement
{
    public const int TextMaxLength = 500;

    private Requirement()
    {
        // EF Core.
        Text = null!;
    }

    public Requirement(Guid id, Guid topicId, string text, int order, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Id = id;
        TopicId = topicId;
        Text = text.Trim();
        Order = order;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TopicId { get; private set; }

    public string Text { get; private set; }

    /// <summary>Position in the list. Not unique — reordering rewrites the whole list.</summary>
    public int Order { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public void Edit(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text.Trim();
    }

    public void MoveTo(int order) => Order = order;
}
