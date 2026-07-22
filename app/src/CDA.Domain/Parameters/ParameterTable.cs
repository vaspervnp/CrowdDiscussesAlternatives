namespace CDA.Domain.Parameters;

/// <summary>
/// The direction and strength of one factor's effect on another.
/// </summary>
/// <remarks>
/// Deliberately five coarse steps rather than a number. The point of the exercise is to notice
/// that pushing one factor helps one thing and hurts another; putting a figure on it would
/// invite arithmetic the underlying judgement cannot support. Persisted — do not renumber.
/// </remarks>
public enum InfluenceEffect
{
    StronglyNegative = -2,
    Negative = -1,
    None = 0,
    Positive = 1,
    StronglyPositive = 2,
}

/// <summary>
/// One participant's map of how the factors in a problem push on each other.
/// </summary>
/// <remarks>
/// <para>
/// Solutions usually fail not because a measure does not work but because it works while
/// damaging something else that mattered. This is a place to write that down: a grid where each
/// row is a factor being pushed and each column is a factor affected, filled in with a
/// direction rather than a number.
/// </para>
/// <para>
/// Each person builds their own. A shared table is offered to the topic as one participant's
/// reading of the problem, not as an agreed fact — which is why it carries an owner's name and
/// is never merged with anyone else's.
/// </para>
/// </remarks>
public sealed class ParameterTable
{
    public const int NameMaxLength = 200;

    /// <summary>
    /// The most factors one table may hold.
    /// </summary>
    /// <remarks>
    /// The grid is n by n, so twelve factors already means 132 judgements to make and a table
    /// nobody can read across. A limit that forces the author to decide what is actually key is
    /// truer to the exercise than one that lets the grid become unreadable.
    /// </remarks>
    public const int MaxParameters = 12;

    private ParameterTable()
    {
        // EF Core.
        Name = null!;
    }

    public ParameterTable(Guid id, Guid topicId, Guid ownerId, string name, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        TopicId = topicId;
        OwnerId = ownerId;
        Name = name.Trim();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TopicId { get; private set; }

    public Guid OwnerId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Whether the other members of the topic can read it.</summary>
    public bool IsShared { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Rename(string name, DateTime atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        UpdatedAtUtc = atUtc;
    }

    public void Share(bool shared, DateTime atUtc)
    {
        IsShared = shared;
        UpdatedAtUtc = atUtc;
    }

    public void Touch(DateTime atUtc) => UpdatedAtUtc = atUtc;
}

/// <summary>One factor in a problem — something that can be pushed on, and that pushes back.</summary>
public sealed class Parameter
{
    public const int NameMaxLength = 120;

    private Parameter()
    {
        // EF Core.
        Name = null!;
    }

    public Parameter(Guid id, Guid tableId, string name, int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        TableId = tableId;
        Name = name.Trim();
        Order = order;
    }

    public Guid Id { get; private set; }

    public Guid TableId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Position in both the rows and the columns; the grid is square.</summary>
    public int Order { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void MoveTo(int order) => Order = order;
}

/// <summary>What increasing one factor does to another.</summary>
public sealed class ParameterInfluence
{
    public const int NoteMaxLength = 300;

    private ParameterInfluence()
    {
        // EF Core.
    }

    public ParameterInfluence(
        Guid tableId,
        Guid fromParameterId,
        Guid toParameterId,
        InfluenceEffect effect,
        string? note)
    {
        if (fromParameterId == toParameterId)
        {
            throw new ArgumentException(
                "A factor's effect on itself says nothing; the diagonal of the grid is not filled in.",
                nameof(toParameterId));
        }

        TableId = tableId;
        FromParameterId = fromParameterId;
        ToParameterId = toParameterId;
        Effect = Validated(effect);
        Note = Trimmed(note);
    }

    public Guid TableId { get; private set; }

    /// <summary>The factor being increased.</summary>
    public Guid FromParameterId { get; private set; }

    /// <summary>The factor affected by that increase.</summary>
    public Guid ToParameterId { get; private set; }

    public InfluenceEffect Effect { get; private set; }

    /// <summary>Why, in the author's words. Optional, and the most useful part of the table.</summary>
    public string? Note { get; private set; }

    public void ChangeTo(InfluenceEffect effect, string? note)
    {
        Effect = Validated(effect);
        Note = Trimmed(note);
    }

    private static InfluenceEffect Validated(InfluenceEffect effect) => Enum.IsDefined(effect)
        ? effect
        : throw new ArgumentOutOfRangeException(nameof(effect), effect, "Unknown effect.");

    private static string? Trimmed(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}

/// <summary>How each effect is written, for people who cannot rely on the colour.</summary>
/// <remarks>
/// The grid uses colour as reinforcement only. Roughly one man in twelve cannot distinguish red
/// from green, and this table's whole content is "helps" against "harms" — carried in colour
/// alone it would be unreadable for them.
/// </remarks>
public static class InfluenceSymbols
{
    public static string Symbol(this InfluenceEffect effect) => effect switch
    {
        InfluenceEffect.StronglyNegative => "− −",
        InfluenceEffect.Negative => "−",
        InfluenceEffect.Positive => "+",
        InfluenceEffect.StronglyPositive => "+ +",
        _ => "·",
    };

    public static string Describe(this InfluenceEffect effect) => effect switch
    {
        InfluenceEffect.StronglyNegative => "strongly harms",
        InfluenceEffect.Negative => "harms",
        InfluenceEffect.Positive => "helps",
        InfluenceEffect.StronglyPositive => "strongly helps",
        _ => "no effect",
    };
}
