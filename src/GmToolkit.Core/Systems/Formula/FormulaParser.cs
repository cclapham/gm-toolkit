using System.Globalization;

namespace GmToolkit.Core.Systems.Formula;

/// <summary>
/// Hand-rolled recursive-descent parser for SYSTEMS.md's derived-formula grammar:
/// <code>
/// formula    := expression EOF
/// expression := term (("+" | "-") term)*
/// term       := factor (("*" | "/") factor)*
/// factor     := number-literal | field-reference | "(" expression ")" | "-" factor
/// number-literal  := digit+ ["." digit+]
/// field-reference := key of another field visible in scope
/// </code>
/// Arithmetic only, over named-field references — no function calls, no conditionals, no loops.
/// See SYSTEMS.md's "The derived-formula grammar" for why this shape is a strict security
/// requirement, not a design nicety.
/// </summary>
public static class FormulaParser
{
    /// <summary>
    /// SYSTEMS.md's "Resource limits": formula string length, checked and failed closed *before*
    /// parsing begins.
    /// </summary>
    public const int MaxFormulaLength = 500;

    /// <summary>
    /// SYSTEMS.md's "Resource limits": maximum levels of <c>"(" expression ")"</c> / unary
    /// <c>-factor</c> nesting. The counter is incremented and checked against this limit
    /// *before* each recursive descent into either production, and the parse fails closed at that
    /// same point if it would be exceeded — never after, and never by recursing "just this once"
    /// and relying on the call eventually returning. An implementation that only checks depth on
    /// the way back out (or checks, but recurses anyway) still overflows the call stack on
    /// sufficiently pathological input; incrementing-then-checking-then-recursing is the only order
    /// that actually prevents the recursive call from ever being made.
    /// </summary>
    public const int MaxNestingDepth = 32;

    /// <summary>
    /// Parses <paramref name="formula"/> as a full <c>formula := expression EOF</c> production.
    /// Throws <see cref="FormulaParseException"/> — never any other exception type — for anything
    /// that doesn't parse cleanly as one whole expression with nothing left over, exceeds
    /// <see cref="MaxFormulaLength"/> or <see cref="MaxNestingDepth"/>, or contains a
    /// number-literal too large to represent as <see cref="decimal"/>.
    /// </summary>
    public static FormulaNode Parse(string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);

        // Checked, and failed closed, before parsing begins -- SYSTEMS.md's own wording for this
        // specific bound. Must happen before a single character is consumed: an over-length string
        // is rejected on length alone, regardless of what it contains.
        if (formula.Length > MaxFormulaLength)
        {
            throw new FormulaParseException(
                $"Formula is {formula.Length} characters long, exceeding the {MaxFormulaLength}-character maximum.");
        }

        var state = new ParserState(formula);
        var node = ParseExpression(state, depth: 0);

        state.SkipWhitespace();
        if (!state.IsAtEnd)
        {
            // The top-level `formula := expression EOF` production is load-bearing: a parser that
            // parses a valid expression prefix and silently discards the rest would evaluate
            // "1 + 2 THIS IS NOT ARITHMETIC" as 3. Any leftover input is a parse failure, full stop.
            throw new FormulaParseException(
                $"Formula has unparsed trailing input starting at position {state.Position}: \"{formula[state.Position..]}\".");
        }

        return node;
    }

    private static FormulaNode ParseExpression(ParserState state, int depth)
    {
        var left = ParseTerm(state, depth);

        while (true)
        {
            state.SkipWhitespace();
            if (state.TryConsume('+'))
            {
                left = new BinaryOpNode('+', left, ParseTerm(state, depth));
            }
            else if (state.TryConsume('-'))
            {
                left = new BinaryOpNode('-', left, ParseTerm(state, depth));
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private static FormulaNode ParseTerm(ParserState state, int depth)
    {
        var left = ParseFactor(state, depth);

        while (true)
        {
            state.SkipWhitespace();
            if (state.TryConsume('*'))
            {
                left = new BinaryOpNode('*', left, ParseFactor(state, depth));
            }
            else if (state.TryConsume('/'))
            {
                left = new BinaryOpNode('/', left, ParseFactor(state, depth));
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private static FormulaNode ParseFactor(ParserState state, int depth)
    {
        state.SkipWhitespace();

        if (state.IsAtEnd)
        {
            throw new FormulaParseException(
                $"Unexpected end of formula at position {state.Position}; expected a number, field reference, '(' or unary '-'.");
        }

        var current = state.Current;

        if (current == '-')
        {
            var nextDepth = depth + 1;
            if (nextDepth > MaxNestingDepth)
            {
                throw new FormulaParseException(
                    $"Formula exceeds the {MaxNestingDepth}-level maximum nesting depth at position {state.Position}.");
            }

            state.Advance();
            return new UnaryNegateNode(ParseFactor(state, nextDepth));
        }

        if (current == '(')
        {
            var nextDepth = depth + 1;
            if (nextDepth > MaxNestingDepth)
            {
                throw new FormulaParseException(
                    $"Formula exceeds the {MaxNestingDepth}-level maximum nesting depth at position {state.Position}.");
            }

            state.Advance();
            var inner = ParseExpression(state, nextDepth);

            state.SkipWhitespace();
            if (!state.TryConsume(')'))
            {
                throw new FormulaParseException($"Expected ')' to close a parenthesized expression, at position {state.Position}.");
            }

            return inner;
        }

        if (char.IsAsciiDigit(current))
        {
            return ParseNumberLiteral(state);
        }

        if (char.IsAsciiLetter(current) || current == '_')
        {
            return ParseFieldReference(state);
        }

        throw new FormulaParseException($"Unexpected character '{current}' at position {state.Position} in formula.");
    }

    private static LiteralNode ParseNumberLiteral(ParserState state)
    {
        var start = state.Position;

        while (!state.IsAtEnd && char.IsAsciiDigit(state.Current))
        {
            state.Advance();
        }

        if (!state.IsAtEnd && state.Current == '.')
        {
            state.Advance();
            if (state.IsAtEnd || !char.IsAsciiDigit(state.Current))
            {
                throw new FormulaParseException($"Expected at least one digit after the decimal point at position {state.Position}.");
            }

            while (!state.IsAtEnd && char.IsAsciiDigit(state.Current))
            {
                state.Advance();
            }
        }

        var text = state.Text[start..state.Position];

        // The grammar's number-literal production is digit+ ["." digit+] only -- no exponent
        // notation, no sign -- so the only way this can fail is a literal too large/precise for
        // decimal to represent (e.g. hundreds of digits). Caught here as an ordinary load-time
        // parse failure rather than reaching decimal.Parse's own OverflowException uncaught.
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormulaParseException($"Number literal \"{text}\" at position {start} is not representable as a decimal.");
        }

        return new LiteralNode(value);
    }

    private static FieldRefNode ParseFieldReference(ParserState state)
    {
        var start = state.Position;

        state.Advance(); // first character already validated as a letter or '_' by the caller
        while (!state.IsAtEnd && (char.IsAsciiLetterOrDigit(state.Current) || state.Current == '_'))
        {
            state.Advance();
        }

        return new FieldRefNode(state.Text[start..state.Position]);
    }

    /// <summary>Mutable cursor over the formula text. Kept as a tiny private class rather than
    /// threading (string, ref int) pairs through every parse method.</summary>
    private sealed class ParserState(string text)
    {
        public string Text { get; } = text;

        public int Position { get; private set; }

        public bool IsAtEnd => Position >= Text.Length;

        public char Current => Text[Position];

        public void Advance() => Position++;

        public void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Current))
            {
                Advance();
            }
        }

        public bool TryConsume(char expected)
        {
            if (!IsAtEnd && Current == expected)
            {
                Advance();
                return true;
            }

            return false;
        }
    }
}