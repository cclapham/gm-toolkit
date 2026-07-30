using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.Core.Tests.Systems.Formula;

public class FormulaParserTests
{
    [Theory]
    [InlineData("1 + 2")]
    [InlineData("(ht + dx) / 4 + speedAdjustment")] // SYSTEMS.md's own GURPS basicSpeed example
    [InlineData("-5")]
    [InlineData("--5")]
    [InlineData("(1 + 2) * 3")]
    public void Parse_accepts_valid_arithmetic_shapes(string formula)
    {
        var node = FormulaParser.Parse(formula);

        Assert.NotNull(node);
    }

    [Fact]
    public void Parse_of_pure_literal_arithmetic_evaluates_to_expected_value()
    {
        var node = FormulaParser.Parse("(1 + 2) * 3 - 1");

        var value = FormulaEvaluator.Evaluate(node, new Dictionary<string, decimal>());

        Assert.Equal(8m, value);
    }

    [Fact]
    public void Parse_rejects_trailing_unparsed_input()
    {
        // SYSTEMS.md's own worked example: a naive parser that stops at a valid expression prefix
        // would evaluate this as 3, discarding the rest. The full `formula := expression EOF`
        // production must reject it instead.
        var ex = Assert.Throws<FormulaParseException>(() => FormulaParser.Parse("1 + 2 THIS IS NOT ARITHMETIC"));

        Assert.Contains("trailing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_rejects_unbalanced_parens()
    {
        Assert.Throws<FormulaParseException>(() => FormulaParser.Parse("(1 + 2"));
    }

    [Fact]
    public void Parse_rejects_empty_string()
    {
        Assert.Throws<FormulaParseException>(() => FormulaParser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_rejects_exponent_notation()
    {
        // The grammar's number-literal production has no `e`/`E` -- "1e400" must not parse as a
        // single literal at all (it should fail as trailing/unexpected input, not silently become
        // Infinity or a huge decimal).
        Assert.Throws<FormulaParseException>(() => FormulaParser.Parse("1e400"));
    }

    [Fact]
    public void Parse_accepts_formula_at_the_500_character_length_limit()
    {
        var formula = BuildFlatSumFormula(FormulaParser.MaxFormulaLength);
        Assert.True(formula.Length <= FormulaParser.MaxFormulaLength);

        var node = FormulaParser.Parse(formula);

        Assert.NotNull(node);
    }

    [Fact]
    public void Parse_rejects_formula_over_the_500_character_length_limit()
    {
        var formula = new string('1', FormulaParser.MaxFormulaLength + 1);

        var ex = Assert.Throws<FormulaParseException>(() => FormulaParser.Parse(formula));

        Assert.Contains("500", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_accepts_formula_at_the_32_level_nesting_limit()
    {
        var formula = new string('(', FormulaParser.MaxNestingDepth) + "1" + new string(')', FormulaParser.MaxNestingDepth);

        var node = FormulaParser.Parse(formula);

        Assert.NotNull(node);
    }

    [Fact]
    public void Parse_rejects_formula_over_the_32_level_nesting_limit()
    {
        var depth = FormulaParser.MaxNestingDepth + 1;
        var formula = new string('(', depth) + "1" + new string(')', depth);

        var ex = Assert.Throws<FormulaParseException>(() => FormulaParser.Parse(formula));

        Assert.Contains("nesting", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_rejects_unary_minus_nesting_over_the_32_level_limit()
    {
        var depth = FormulaParser.MaxNestingDepth + 1;
        var formula = new string('-', depth) + "1";

        Assert.Throws<FormulaParseException>(() => FormulaParser.Parse(formula));
    }

    [Fact]
    public void Parse_of_over_deep_nesting_does_not_overflow_the_stack()
    {
        // The adversarial case SYSTEMS.md's own security review found: a from-scratch
        // implementation of an earlier, unbounded draft of this grammar crashed with an
        // uncatchable StackOverflowException at roughly 20,000 levels of paren nesting. This
        // formula is still well under the 500-character length cap on its own (proving the depth
        // check, not just the length check, is what's catching it), and must fail with an ordinary,
        // catchable FormulaParseException -- never crash the process.
        var depth = 200; // (200 * 2) + 1 = 401 characters, under the 500-char cap
        var formula = new string('(', depth) + "1" + new string(')', depth);

        var ex = Record.Exception(() => FormulaParser.Parse(formula));

        Assert.IsType<FormulaParseException>(ex);
    }

    [Fact]
    public void Parse_rejects_a_number_literal_too_large_for_decimal()
    {
        var hugeLiteral = new string('9', 60); // far beyond decimal's ~29 significant digits

        Assert.Throws<FormulaParseException>(() => FormulaParser.Parse(hugeLiteral));
    }

    [Fact]
    public void Parse_rejects_a_lone_decimal_point_literal_with_no_trailing_digit()
    {
        Assert.Throws<FormulaParseException>(() => FormulaParser.Parse("1."));
    }

    [Fact]
    public void Parse_field_reference_captures_the_full_identifier()
    {
        var node = FormulaParser.Parse("hpAdjustment");

        var fieldRef = Assert.IsType<FieldRefNode>(node);
        Assert.Equal("hpAdjustment", fieldRef.Key);
    }

    /// <summary>Builds "1+1+1+...+1" as close to <paramref name="maxLength"/> characters as a "+1" repeat unit allows.</summary>
    private static string BuildFlatSumFormula(int maxLength)
    {
        var builder = new System.Text.StringBuilder("1");
        while (builder.Length + 2 <= maxLength)
        {
            builder.Append("+1");
        }

        return builder.ToString();
    }
}