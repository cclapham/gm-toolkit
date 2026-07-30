using GmToolkit.Core.Systems;
using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.Core.Tests.Systems.Formula;

public class DerivedFieldEvaluatorTests
{
    [Fact]
    public void EvaluateAll_computes_gurps_basic_speed_and_basic_move_chain()
    {
        var fields = new List<StatFieldDefinition>
        {
            NumberField("ht"),
            NumberField("dx"),
            NumberField("speedAdjustment"),
            NumberField("moveAdjustment"),
            DerivedField("basicSpeed", "(ht + dx) / 4 + speedAdjustment", precision: 2),
            DerivedField("basicMove", "basicSpeed + moveAdjustment", precision: 0, rounding: RoundingModes.Floor),
        };
        var graph = DerivedFieldGraph.Build(fields);
        var rawValues = new Dictionary<string, string>
        {
            ["ht"] = "12",
            ["dx"] = "13",
            ["speedAdjustment"] = "0",
            ["moveAdjustment"] = "0.9",
        };

        var results = DerivedFieldEvaluator.EvaluateAll(fields, graph.EvaluationOrder, rawValues);

        Assert.Equal(6.25m, results["basicSpeed"]);
        Assert.Equal(7m, results["basicMove"]); // floor(6.25 + 0.9) = floor(7.15) = 7
    }

    [Fact]
    public void EvaluateAll_dnd_ability_modifier_floors_correctly()
    {
        var fields = new List<StatFieldDefinition>
        {
            NumberField("dex"),
            DerivedField("dexMod", "(dex - 10) / 2", precision: 0, rounding: RoundingModes.Floor),
        };
        var graph = DerivedFieldGraph.Build(fields);

        var results = DerivedFieldEvaluator.EvaluateAll(fields, graph.EvaluationOrder, new Dictionary<string, string> { ["dex"] = "15" });

        // (15 - 10) / 2 = 2.5, floor => 2 -- matches D&D 5e's ability modifier rules.
        Assert.Equal(2m, results["dexMod"]);
    }

    [Fact]
    public void EvaluateAll_division_by_zero_fails_the_field_closed_not_an_exception()
    {
        var fields = new List<StatFieldDefinition>
        {
            NumberField("a"),
            NumberField("b"),
            DerivedField("quotient", "a / b"),
        };
        var graph = DerivedFieldGraph.Build(fields);

        var results = DerivedFieldEvaluator.EvaluateAll(
            fields, graph.EvaluationOrder, new Dictionary<string, string> { ["a"] = "10", ["b"] = "0" });

        Assert.Null(results["quotient"]);
    }

    [Fact]
    public void EvaluateAll_decimal_overflow_fails_the_field_closed()
    {
        var fields = new List<StatFieldDefinition>
        {
            NumberField("a"),
            DerivedField("squared", "a * a"),
        };
        var graph = DerivedFieldGraph.Build(fields);

        var results = DerivedFieldEvaluator.EvaluateAll(
            fields, graph.EvaluationOrder, new Dictionary<string, string> { ["a"] = decimal.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture) });

        Assert.Null(results["squared"]);
    }

    [Fact]
    public void EvaluateAll_unresolvable_reference_fails_the_field_closed()
    {
        // Defense in depth: this shouldn't happen after load-time validation, but a formula
        // referencing a field with no stored value at all (missing from the character's raw
        // Stats bag) must still fail closed, never throw.
        var fields = new List<StatFieldDefinition>
        {
            NumberField("st"),
            DerivedField("hp", "st + hpAdjustment"), // hpAdjustment never provided
        };
        var graph = DerivedFieldGraph.Build(fields);

        var results = DerivedFieldEvaluator.EvaluateAll(
            fields, graph.EvaluationOrder, new Dictionary<string, string> { ["st"] = "12" });

        Assert.Null(results["hp"]);
    }

    [Fact]
    public void EvaluateAll_propagates_a_failure_transitively_through_the_dependency_chain()
    {
        var fields = new List<StatFieldDefinition>
        {
            NumberField("a"),
            NumberField("b"),
            DerivedField("basicSpeed", "a / b"), // fails: division by zero
            DerivedField("basicMove", "basicSpeed + 1"), // depends on the failed field
        };
        var graph = DerivedFieldGraph.Build(fields);

        var results = DerivedFieldEvaluator.EvaluateAll(
            fields, graph.EvaluationOrder, new Dictionary<string, string> { ["a"] = "10", ["b"] = "0" });

        Assert.Null(results["basicSpeed"]);
        Assert.Null(results["basicMove"]);
    }

    [Fact]
    public void EvaluateAll_clamps_before_rounding_per_systems_md_order_of_operations()
    {
        var fields = new List<StatFieldDefinition>
        {
            NumberField("raw"),
            new()
            {
                Key = "clamped",
                Label = "Clamped",
                Type = StatFieldTypes.Derived,
                Formula = "raw",
                Min = 0,
                Max = 10,
                Precision = 0,
                Rounding = RoundingModes.Round,
            },
        };
        var graph = DerivedFieldGraph.Build(fields);

        // Raw value 10.6 would round to 11 if rounded first, but must clamp to 10 first, then
        // round 10 (a no-op) -- so the correct final result is 10, not 11.
        var results = DerivedFieldEvaluator.EvaluateAll(
            fields, graph.EvaluationOrder, new Dictionary<string, string> { ["raw"] = "10.6" });

        Assert.Equal(10m, results["clamped"]);
    }

    [Theory]
    [InlineData(RoundingModes.None, 2.5, 2)] // ties to even
    [InlineData(RoundingModes.Round, 2.5, 3)] // ties away from zero
    [InlineData(RoundingModes.Floor, 2.9, 2)]
    [InlineData(RoundingModes.Ceiling, 2.1, 3)]
    [InlineData(RoundingModes.Truncate, -2.9, -2)]
    public void EvaluateAll_applies_each_rounding_mode_as_documented(string rounding, double rawValue, double expected)
    {
        var fields = new List<StatFieldDefinition>
        {
            NumberField("raw"),
            DerivedField("rounded", "raw", precision: 0, rounding: rounding),
        };
        var graph = DerivedFieldGraph.Build(fields);

        var results = DerivedFieldEvaluator.EvaluateAll(
            fields, graph.EvaluationOrder, new Dictionary<string, string> { ["raw"] = rawValue.ToString(System.Globalization.CultureInfo.InvariantCulture) });

        Assert.Equal((decimal)expected, results["rounded"]);
    }

    [Fact]
    public void EvaluateAll_evaluates_each_derived_field_at_most_once_even_when_referenced_many_times()
    {
        // Not directly observable via call counting (the formula language has no side effects to
        // count), but this proves the *shape* that would be exponential under naive re-evaluation
        // -- a moderately deep chain where each link is referenced by the next -- still completes
        // via the topological order without recomputing shared sub-results, by asserting the
        // correct numeric answer for a chain deep enough that re-deriving it from scratch per
        // reference would be a strong (if not exhaustive) smoke test of the memoization.
        var fields = new List<StatFieldDefinition> { NumberField("seed") };
        const int chainLength = 40;
        fields.Add(DerivedField("f0", "seed"));
        for (var i = 1; i < chainLength; i++)
        {
            fields.Add(DerivedField($"f{i}", $"f{i - 1} + f{i - 1}")); // doubles each link, referencing the same predecessor twice
        }

        var graph = DerivedFieldGraph.Build(fields);

        var results = DerivedFieldEvaluator.EvaluateAll(fields, graph.EvaluationOrder, new Dictionary<string, string> { ["seed"] = "1" });

        // f0 = 1, f_i = 2 * f_(i-1) => f_(chainLength-1) = 2^(chainLength-1)
        var expected = 1m;
        for (var i = 1; i < chainLength; i++)
        {
            expected *= 2m;
        }

        Assert.Equal(expected, results[$"f{chainLength - 1}"]);
    }

    private static StatFieldDefinition NumberField(string key) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.Number,
    };

    private static StatFieldDefinition DerivedField(string key, string formula, int? precision = null, string? rounding = null) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.Derived,
        Formula = formula,
        Precision = precision,
        Rounding = rounding,
    };
}