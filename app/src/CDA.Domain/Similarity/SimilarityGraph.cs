namespace CDA.Domain.Similarity;

/// <summary>One active similarity report, reduced to what the collapse needs.</summary>
/// <param name="BetterWritten">
/// Which of the pair its reporter judged the better-written; null if they had no preference.
/// </param>
public readonly record struct SimilarityEdge(Guid ProposalA, Guid ProposalB, Guid? BetterWritten);

/// <summary>
/// The result of collapsing duplicate proposals: who stands for whom.
/// </summary>
public sealed class CollapsedProposals
{
    internal CollapsedProposals(
        Dictionary<Guid, Guid> representativeOf,
        Dictionary<Guid, List<Guid>> members)
    {
        RepresentativeOf = representativeOf;
        Members = members.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Guid>)pair.Value);
        Hidden = [.. representativeOf.Where(pair => pair.Key != pair.Value).Select(pair => pair.Key)];
    }

    public static readonly CollapsedProposals None = new([], []);

    /// <summary>Maps every proposal in a duplicate group to the one shown for the group.</summary>
    public IReadOnlyDictionary<Guid, Guid> RepresentativeOf { get; }

    /// <summary>Maps a representative to every proposal it stands for, itself included.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Members { get; }

    /// <summary>Proposals that a representative stands for, and which are therefore not listed.</summary>
    public IReadOnlyCollection<Guid> Hidden { get; }

    public Guid RepresentativeFor(Guid proposalId) =>
        RepresentativeOf.TryGetValue(proposalId, out var representative) ? representative : proposalId;
}

/// <summary>
/// Folds proposals that participants have judged to be duplicates into one entry each.
/// </summary>
/// <remarks>
/// <para>
/// The platform never decides that two proposals are the same. People report pairs, other
/// people vote on those reports, and each reader chooses the vote threshold at which a report
/// takes effect for them. This class does the folding, given the reports that clear whatever
/// threshold the reader chose.
/// </para>
/// <para>
/// Reports are treated as a graph rather than as isolated pairs, and connected components are
/// collapsed together. If A is reported similar to B and B to C, then showing A and C
/// separately would still split the crowd's support between two entries that everyone involved
/// considers the same idea — which is the whole problem this exists to solve.
/// </para>
/// </remarks>
public static class SimilarityGraph
{
    public static CollapsedProposals Collapse(
        IEnumerable<SimilarityEdge> activeReports,
        IReadOnlyDictionary<Guid, int> scores)
    {
        ArgumentNullException.ThrowIfNull(activeReports);
        ArgumentNullException.ThrowIfNull(scores);

        var edges = activeReports.ToList();

        if (edges.Count == 0)
        {
            return CollapsedProposals.None;
        }

        var parent = new Dictionary<Guid, Guid>();

        foreach (var edge in edges)
        {
            Union(parent, edge.ProposalA, edge.ProposalB);
        }

        // How often each proposal was singled out as the better-written of a pair.
        var preferred = new Dictionary<Guid, int>();

        foreach (var edge in edges)
        {
            if (edge.BetterWritten is { } winner)
            {
                preferred[winner] = preferred.GetValueOrDefault(winner) + 1;
            }
        }

        var components = new Dictionary<Guid, List<Guid>>();

        foreach (var proposal in parent.Keys)
        {
            var root = Find(parent, proposal);

            if (!components.TryGetValue(root, out var component))
            {
                components[root] = component = [];
            }

            component.Add(proposal);
        }

        var representativeOf = new Dictionary<Guid, Guid>();
        var members = new Dictionary<Guid, List<Guid>>();

        foreach (var component in components.Values)
        {
            // Whoever the reporters judged best written, then whoever the crowd supports most,
            // then by id so the choice never depends on iteration order.
            var representative = component
                .OrderByDescending(id => preferred.GetValueOrDefault(id))
                .ThenByDescending(id => scores.GetValueOrDefault(id))
                .ThenBy(id => id)
                .First();

            var ordered = component.OrderBy(id => id).ToList();
            members[representative] = ordered;

            foreach (var proposal in ordered)
            {
                representativeOf[proposal] = representative;
            }
        }

        return new CollapsedProposals(representativeOf, members);
    }

    private static Guid Find(Dictionary<Guid, Guid> parent, Guid node)
    {
        if (!parent.TryGetValue(node, out var value))
        {
            parent[node] = node;
            return node;
        }

        if (value == node)
        {
            return node;
        }

        // Path compression: components are small but a long chain of reports is possible.
        var root = Find(parent, value);
        parent[node] = root;
        return root;
    }

    private static void Union(Dictionary<Guid, Guid> parent, Guid left, Guid right)
    {
        var a = Find(parent, left);
        var b = Find(parent, right);

        if (a != b)
        {
            parent[b] = a;
        }
    }
}
