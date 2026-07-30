using GmToolkit.Core.Systems;
using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.Core.Tests.Systems.Formula;

public class DerivedFieldGraphTests
{
    [Fact]
    public void Build_orders_a_simple_two_field_chain_dependency_first()
    {
        var fields = new List<StatFieldDefinition>
        {
            NumberField("ht"),
            NumberField("dx"),
            DerivedField("basicSpeed", "(ht + dx) / 4"),
            DerivedField("basicMove", "basicSpeed + 1"),
        };

        var graph = DerivedFieldGraph.Build(fields);

        Assert.Equal(["basicSpeed", "basicMove"], graph.EvaluationOrder);
        Assert.Equal(2, graph.LongestChainLength);
    }

    [Fact]
    public void Build_rejects_a_self_reference()
    {
        var fields = new List<StatFieldDefinition> { DerivedField("a", "a + 1") };

        Assert.Throws<DerivedFieldGraphException>(() => DerivedFieldGraph.Build(fields));
    }

    [Fact]
    public void Build_rejects_a_mutual_reference()
    {
        var fields = new List<StatFieldDefinition>
        {
            DerivedField("a", "b + 1"),
            DerivedField("b", "a + 1"),
        };

        Assert.Throws<DerivedFieldGraphException>(() => DerivedFieldGraph.Build(fields));
    }

    [Fact]
    public void Build_accepts_a_dependency_chain_at_the_64_field_maximum()
    {
        var fields = BuildChain(DerivedFieldGraph.MaxChainDepth);

        var graph = DerivedFieldGraph.Build(fields);

        Assert.Equal(DerivedFieldGraph.MaxChainDepth, graph.LongestChainLength);
    }

    [Fact]
    public void Build_rejects_a_dependency_chain_over_the_64_field_maximum()
    {
        var fields = BuildChain(DerivedFieldGraph.MaxChainDepth + 1);

        var ex = Assert.Throws<DerivedFieldGraphException>(() => DerivedFieldGraph.Build(fields));

        Assert.Contains("64", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_does_not_overflow_the_stack_detecting_a_cycle_among_many_fields()
    {
        // The cycle-check step is Kahn's algorithm (queue-driven), deliberately not a recursive
        // depth-first walk -- this proves a wide cycle among a couple hundred fields is detected
        // via an ordinary, catchable exception rather than a stack overflow.
        var fields = new List<StatFieldDefinition>();
        const int count = 200;
        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            fields.Add(DerivedField($"f{i}", $"f{next} + 1"));
        }

        var ex = Record.Exception(() => DerivedFieldGraph.Build(fields));

        Assert.IsType<DerivedFieldGraphException>(ex);
    }

    [Fact]
    public void Build_ignores_references_to_non_derived_fields_as_graph_edges()
    {
        // A derived field referencing a plain `number` field is a data read, not a dependency edge
        // -- it must not count toward chain depth or be treated as part of the graph.
        var fields = new List<StatFieldDefinition>
        {
            NumberField("st"),
            DerivedField("hp", "st + 5"),
        };

        var graph = DerivedFieldGraph.Build(fields);

        Assert.Equal(["hp"], graph.EvaluationOrder);
        Assert.Equal(1, graph.LongestChainLength);
    }

    /// <summary>Builds a chain of <paramref name="length"/> derived fields, each referencing the previous one.</summary>
    private static List<StatFieldDefinition> BuildChain(int length)
    {
        var fields = new List<StatFieldDefinition> { DerivedField("f0", "1") };
        for (var i = 1; i < length; i++)
        {
            fields.Add(DerivedField($"f{i}", $"f{i - 1} + 1"));
        }

        return fields;
    }

    private static StatFieldDefinition NumberField(string key) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.Number,
    };

    private static StatFieldDefinition DerivedField(string key, string formula) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.Derived,
        Formula = formula,
    };
}