using System.Text;

namespace CDA.Domain.Search;

/// <summary>
/// A parsed search, ready to hand to MariaDB.
/// </summary>
/// <param name="BooleanExpression">The query in InnoDB boolean-mode syntax.</param>
/// <param name="IgnoredShortTerms">
/// Words the index cannot match, because they are shorter than the server's minimum token
/// length. Surfaced rather than dropped silently — a search that quietly ignores half of what
/// was typed and returns nothing is worse than one that explains itself.
/// </param>
/// <param name="Error">Why the query could not be parsed, or null.</param>
public sealed record CommentQuery(
    string BooleanExpression,
    IReadOnlyList<string> IgnoredShortTerms,
    string? Error)
{
    public bool IsUsable => Error is null && BooleanExpression.Length > 0;

    public static CommentQuery Failed(string error) => new(string.Empty, [], error);
}

/// <summary>
/// Turns a search people type into the boolean expression the full-text index understands.
/// </summary>
/// <remarks>
/// <para>
/// The platform's documents ask for words combined with AND and OR, which is what this
/// supports, plus parentheses, quoted phrases and a leading minus for exclusion. Adjacent words
/// with no operator between them are treated as AND, which is how nearly every search box
/// behaves.
/// </para>
/// <para>
/// The translation to boolean mode is not one-to-one. Boolean mode has no AND or OR: a term
/// prefixed with <c>+</c> is required and a bare term is optional, so <c>a AND b</c> becomes
/// <c>+a +b</c> and <c>a OR b</c> becomes <c>a b</c>. Groups nest, so <c>a AND (b OR c)</c>
/// becomes <c>+a +(b c)</c>.
/// </para>
/// </remarks>
public static class CommentQueryParser
{
    /// <summary>
    /// Words shorter than this are not in the index and can never match.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>innodb_ft_min_token_size</c>, which is 3 on the deployment this was built
    /// against and cannot be changed without restarting the server.
    /// </remarks>
    public const int MinimumTermLength = 3;

    /// <summary>Characters boolean mode treats as operators; they are stripped from user words.</summary>
    private const string OperatorCharacters = "+-<>()~*\"@";

    public static CommentQuery Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return CommentQuery.Failed("Type something to search for.");
        }

        var tokens = Tokenize(input);

        if (tokens.Count == 0)
        {
            return CommentQuery.Failed("There is nothing searchable in that.");
        }

        var reader = new TokenReader(tokens);
        Node node;

        try
        {
            node = ParseOr(reader);
        }
        catch (FormatException error)
        {
            return CommentQuery.Failed(error.Message);
        }

        if (!reader.AtEnd)
        {
            return CommentQuery.Failed("There is an unmatched closing bracket in that search.");
        }

        var ignored = new List<string>();
        var builder = new StringBuilder();
        Render(node, builder, required: true, ignored);

        var expression = builder.ToString().Trim();

        return expression.Length == 0
            ? new CommentQuery(string.Empty, ignored, "Every word in that search is too short to look up.")
            : new CommentQuery(expression, ignored, null);
    }

    // --- tokens -----------------------------------------------------------------------------

    private enum TokenKind { Word, Phrase, And, Or, Not, Open, Close }

    private readonly record struct Token(TokenKind Kind, string Text);

    private static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        var position = 0;

        while (position < input.Length)
        {
            var current = input[position];

            if (char.IsWhiteSpace(current))
            {
                position++;
                continue;
            }

            switch (current)
            {
                case '(':
                    tokens.Add(new Token(TokenKind.Open, "("));
                    position++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenKind.Close, ")"));
                    position++;
                    continue;
                case '-' when position + 1 < input.Length && !char.IsWhiteSpace(input[position + 1]):
                    tokens.Add(new Token(TokenKind.Not, "-"));
                    position++;
                    continue;
                case '"':
                {
                    var end = input.IndexOf('"', position + 1);

                    // An unterminated quote takes the rest of the line rather than failing:
                    // it is nearly always a half-typed phrase, not a mistake worth refusing.
                    var phrase = end < 0
                        ? input[(position + 1)..]
                        : input[(position + 1)..end];

                    position = end < 0 ? input.Length : end + 1;

                    var cleaned = Clean(phrase);
                    if (cleaned.Length > 0)
                    {
                        tokens.Add(new Token(TokenKind.Phrase, cleaned));
                    }

                    continue;
                }
            }

            var start = position;
            while (position < input.Length
                   && !char.IsWhiteSpace(input[position])
                   && input[position] is not ('(' or ')' or '"'))
            {
                position++;
            }

            var word = input[start..position];

            tokens.Add(word.ToUpperInvariant() switch
            {
                "AND" => new Token(TokenKind.And, "AND"),
                "OR" => new Token(TokenKind.Or, "OR"),
                "NOT" => new Token(TokenKind.Not, "-"),
                _ => new Token(TokenKind.Word, Clean(word)),
            });
        }

        return [.. tokens.Where(token => token.Kind is not TokenKind.Word || token.Text.Length > 0)];
    }

    private static string Clean(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (!OperatorCharacters.Contains(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Trim();
    }

    // --- syntax tree ------------------------------------------------------------------------

    private abstract record Node;

    private sealed record TermNode(string Text, bool IsPhrase, bool Negated) : Node;

    private sealed record AndNode(IReadOnlyList<Node> Children) : Node;

    private sealed record OrNode(IReadOnlyList<Node> Children) : Node;

    private sealed class TokenReader(List<Token> tokens)
    {
        private int _position;

        public bool AtEnd => _position >= tokens.Count;

        public Token Peek => tokens[_position];

        public Token Next() => tokens[_position++];

        public bool TryTake(TokenKind kind)
        {
            if (AtEnd || tokens[_position].Kind != kind)
            {
                return false;
            }

            _position++;
            return true;
        }
    }

    private static Node ParseOr(TokenReader reader)
    {
        var children = new List<Node> { ParseAnd(reader) };

        while (!reader.AtEnd && reader.Peek.Kind == TokenKind.Or)
        {
            reader.Next();
            children.Add(ParseAnd(reader));
        }

        return children.Count == 1 ? children[0] : new OrNode(children);
    }

    private static Node ParseAnd(TokenReader reader)
    {
        var children = new List<Node> { ParseTerm(reader) };

        while (!reader.AtEnd && reader.Peek.Kind is not (TokenKind.Or or TokenKind.Close))
        {
            // An explicit AND is consumed; adjacency without one means the same thing.
            reader.TryTake(TokenKind.And);

            if (reader.AtEnd || reader.Peek.Kind is TokenKind.Or or TokenKind.Close)
            {
                break;
            }

            children.Add(ParseTerm(reader));
        }

        return children.Count == 1 ? children[0] : new AndNode(children);
    }

    private static Node ParseTerm(TokenReader reader)
    {
        if (reader.AtEnd)
        {
            throw new FormatException("That search ends in an operator with nothing after it.");
        }

        var token = reader.Next();

        switch (token.Kind)
        {
            case TokenKind.Not:
                return ParseTerm(reader) switch
                {
                    TermNode term => term with { Negated = !term.Negated },
                    // Excluding a whole bracketed group is not something boolean mode expresses
                    // cleanly, and it is not something the documents ask for.
                    _ => throw new FormatException("Only a single word or phrase can be excluded with a minus."),
                };

            case TokenKind.Open:
            {
                var inner = ParseOr(reader);

                if (!reader.TryTake(TokenKind.Close))
                {
                    throw new FormatException("There is an unclosed bracket in that search.");
                }

                return inner;
            }

            case TokenKind.Word:
                return new TermNode(token.Text, IsPhrase: false, Negated: false);

            case TokenKind.Phrase:
                return new TermNode(token.Text, IsPhrase: true, Negated: false);

            default:
                throw new FormatException($"'{token.Text}' cannot start a search term.");
        }
    }

    // --- rendering --------------------------------------------------------------------------

    private static void Render(Node node, StringBuilder builder, bool required, List<string> ignored)
    {
        switch (node)
        {
            case TermNode term:
            {
                if (!term.IsPhrase && term.Text.Length < MinimumTermLength)
                {
                    ignored.Add(term.Text);
                    return;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                // A negated term is always "-", whatever the surrounding operator: boolean mode
                // treats exclusion as absolute. An optional term carries no prefix at all —
                // appending a space here as well as the separator above would double it.
                if (term.Negated)
                {
                    builder.Append('-');
                }
                else if (required)
                {
                    builder.Append('+');
                }

                builder.Append(term.IsPhrase ? $"\"{term.Text}\"" : term.Text);
                break;
            }

            case AndNode and:
                foreach (var child in and.Children)
                {
                    Render(child, builder, required: true, ignored);
                }

                break;

            case OrNode or:
            {
                var inner = new StringBuilder();

                foreach (var child in or.Children)
                {
                    // Inside an OR nothing is required — that is what makes it an OR.
                    Render(child, inner, required: false, ignored);
                }

                var rendered = inner.ToString().Trim();

                if (rendered.Length == 0)
                {
                    return;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(required ? "+(" : "(").Append(rendered).Append(')');
                break;
            }
        }
    }
}
