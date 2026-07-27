using GmToolkit.Core.Generator;

namespace GmToolkit.Core.Tests.Generator;

public class WeightedPickerTests
{
    [Fact]
    public void Pick_throws_for_an_empty_list()
    {
        var random = new SystemRandomSource(1);

        Assert.Throws<ArgumentException>(() => WeightedPicker.Pick([], random));
    }

    [Fact]
    public void Pick_always_returns_the_only_entry_in_a_single_entry_table()
    {
        var entry = new GeneratorTableEntry { Value = "Only", Weight = 42 };
        var random = new SystemRandomSource(1);

        for (var i = 0; i < 100; i++)
        {
            var result = WeightedPicker.Pick([entry], random);
            Assert.Same(entry, result);
        }
    }

    [Fact]
    public void Pick_over_10000_draws_respects_skewed_weights_within_tolerance()
    {
        // "Heavy" is weighted 10x "Light": of a total weight of 11, it should be picked ~10/11 of
        // the time (~90.9%) and "Light" ~1/11 (~9.1%).
        var heavy = new GeneratorTableEntry { Value = "Heavy", Weight = 10 };
        var light = new GeneratorTableEntry { Value = "Light", Weight = 1 };
        IReadOnlyList<GeneratorTableEntry> entries = [heavy, light];
        var random = new SystemRandomSource(12345);

        var counts = new Dictionary<string, int> { ["Heavy"] = 0, ["Light"] = 0 };
        const int draws = 10_000;
        for (var i = 0; i < draws; i++)
        {
            var picked = WeightedPicker.Pick(entries, random);
            counts[picked.Value]++;
        }

        var heavyFraction = counts["Heavy"] / (double)draws;
        var lightFraction = counts["Light"] / (double)draws;

        // Expected ~0.909 / ~0.091; allow a generous +/-0.03 absolute tolerance for sampling noise.
        Assert.InRange(heavyFraction, 0.879, 0.939);
        Assert.InRange(lightFraction, 0.061, 0.121);
    }

    [Fact]
    public void Pick_over_10000_draws_distributes_roughly_evenly_across_uniform_weights()
    {
        IReadOnlyList<GeneratorTableEntry> entries =
        [
            new GeneratorTableEntry { Value = "A" },
            new GeneratorTableEntry { Value = "B" },
            new GeneratorTableEntry { Value = "C" },
            new GeneratorTableEntry { Value = "D" },
        ];
        var random = new SystemRandomSource(999);

        var counts = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0, ["C"] = 0, ["D"] = 0 };
        const int draws = 10_000;
        for (var i = 0; i < draws; i++)
        {
            var picked = WeightedPicker.Pick(entries, random);
            counts[picked.Value]++;
        }

        // Each entry should land close to the uniform 25% share; a structural bug in the
        // cumulative-weight scan (e.g. always favoring the first or last entry) would skew one
        // entry's share noticeably more than sampling noise alone would.
        foreach (var count in counts.Values)
        {
            var fraction = count / (double)draws;
            Assert.InRange(fraction, 0.20, 0.30);
        }
    }

    [Fact]
    public void Pick_over_10000_draws_almost_never_returns_an_entry_with_a_vastly_smaller_weight()
    {
        // A 1,000,000:1 weight ratio -- much more extreme than the 10:1 skew above -- exercises the
        // same cumulative-weight scan at the opposite end of the scale: with totalWeight dominated by
        // one huge value, does floating-point summation/comparison still let the tiny entry win its
        // (vanishingly small but nonzero) fair share, or does rounding silently swallow it entirely?
        var giant = new GeneratorTableEntry { Value = "Giant", Weight = 1_000_000 };
        var tiny = new GeneratorTableEntry { Value = "Tiny", Weight = 1 };
        IReadOnlyList<GeneratorTableEntry> entries = [giant, tiny];
        var random = new SystemRandomSource(31415);

        var counts = new Dictionary<string, int> { ["Giant"] = 0, ["Tiny"] = 0 };
        const int draws = 10_000;
        for (var i = 0; i < draws; i++)
        {
            var picked = WeightedPicker.Pick(entries, random);
            counts[picked.Value]++;
        }

        // Expected share for "Tiny" is ~1/1,000,001 -- over 10,000 draws that's an expected count
        // near 0.01, so seeing it picked at all would be a fluke; the real assertion is that "Giant"
        // dominates as expected and the picker never throws or otherwise misbehaves at this scale.
        Assert.Equal(draws, counts["Giant"] + counts["Tiny"]);
        Assert.True(counts["Giant"] >= draws - 1, "Expected the overwhelmingly heavier entry to win nearly every draw.");
    }

    [Fact]
    public void Pick_is_deterministic_given_identically_seeded_random_sources()
    {
        IReadOnlyList<GeneratorTableEntry> entries =
        [
            new GeneratorTableEntry { Value = "A", Weight = 3 },
            new GeneratorTableEntry { Value = "B", Weight = 1 },
            new GeneratorTableEntry { Value = "C", Weight = 5 },
        ];

        var randomA = new SystemRandomSource(2026);
        var randomB = new SystemRandomSource(2026);

        var resultsA = Enumerable.Range(0, 50).Select(_ => WeightedPicker.Pick(entries, randomA).Value).ToList();
        var resultsB = Enumerable.Range(0, 50).Select(_ => WeightedPicker.Pick(entries, randomB).Value).ToList();

        Assert.Equal(resultsA, resultsB);
    }
}