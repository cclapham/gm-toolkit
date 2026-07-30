using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.Core.Tests.Systems.Formula;

public class FormulaEvaluatorTests
{
    [Fact]
    public void Evaluate_computes_gurps_basic_speed_example_from_systems_md()
    {
        var node = FormulaParser.Parse("(ht + dx) / 4 + speedAdjustment");
        var resolved = new Dictionary<string, decimal>
        {
            ["ht"] = 12,
            ["dx"] = 13,
            ["speedAdjustment"] = 0,
        };

        var value = FormulaEvaluator.Evaluate(node, resolved);

        Assert.Equal(6.25m, value);
    }

    [Fact]
    public void Evaluate_supports_chained_field_references()
    {
        // basicMove referencing basicSpeed, mirroring SYSTEMS.md's derived-referencing-derived example.
        var basicSpeed = FormulaParser.Parse("(ht + dx) / 4 + speedAdjustment");
        var resolved = new Dictionary<string, decimal> { ["ht"] = 12, ["dx"] = 13, ["speedAdjustment"] = 0 };
        var basicSpeedValue = FormulaEvaluator.Evaluate(basicSpeed, resolved);
        resolved["basicSpeed"] = basicSpeedValue;

        var basicMove = FormulaParser.Parse("basicSpeed + moveAdjustment");
        resolved["moveAdjustment"] = 0;

        var value = FormulaEvaluator.Evaluate(basicMove, resolved);

        Assert.Equal(6.25m, value);
    }

    [Fact]
    public void Evaluate_division_by_zero_throws_DivideByZeroException()
    {
        var node = FormulaParser.Parse("a / b");
        var resolved = new Dictionary<string, decimal> { ["a"] = 10, ["b"] = 0 };

        Assert.Throws<DivideByZeroException>(() => FormulaEvaluator.Evaluate(node, resolved));
    }

    [Fact]
    public void Evaluate_decimal_overflow_throws_OverflowException()
    {
        var node = FormulaParser.Parse("a * a");
        var resolved = new Dictionary<string, decimal> { ["a"] = decimal.MaxValue };

        Assert.Throws<OverflowException>(() => FormulaEvaluator.Evaluate(node, resolved));
    }

    [Fact]
    public void Evaluate_unresolvable_field_reference_throws_KeyNotFoundException()
    {
        var node = FormulaParser.Parse("unknownField + 1");

        Assert.Throws<KeyNotFoundException>(() => FormulaEvaluator.Evaluate(node, new Dictionary<string, decimal>()));
    }

    [Fact]
    public void Evaluate_never_produces_NaN_or_infinite_style_silent_failure()
    {
        // The whole point of using decimal instead of double: there is no NaN/Infinity value for a
        // poisoned computation to silently become. A failing operation always throws instead.
        var node = FormulaParser.Parse("a / b");
        var resolved = new Dictionary<string, decimal> { ["a"] = 1, ["b"] = 0 };

        var ex = Record.Exception(() => FormulaEvaluator.Evaluate(node, resolved));

        Assert.IsType<DivideByZeroException>(ex);
    }
}