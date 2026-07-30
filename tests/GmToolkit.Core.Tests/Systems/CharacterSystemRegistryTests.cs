using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Tests.Systems;

public class CharacterSystemRegistryTests
{
    [Fact]
    public void FromEmbeddedSystems_includes_at_least_the_generic_system()
    {
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();

        var all = registry.GetAll();

        Assert.Contains(all, s => s.Id == GenericCharacterSystem.Id);
    }

    [Fact]
    public void GetById_returns_the_generic_system()
    {
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();

        var generic = registry.GetById(GenericCharacterSystem.Id);

        Assert.Empty(generic.PcFields);
        Assert.Empty(generic.NpcFields);
    }

    [Fact]
    public void TryGetById_returns_false_for_an_unregistered_id()
    {
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();

        var found = registry.TryGetById("does-not-exist", out var system);

        Assert.False(found);
        Assert.Null(system);
    }

    [Fact]
    public void GetById_throws_KeyNotFoundException_for_an_unregistered_id()
    {
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();

        Assert.Throws<KeyNotFoundException>(() => registry.GetById("does-not-exist"));
    }

    [Fact]
    public void Constructor_rejects_a_colliding_id_and_built_ins_win()
    {
        var builtIn = new CharacterSystem { FormatVersion = 1, Id = "dupe", Name = "Built-in" };
        var colliding = new CharacterSystem { FormatVersion = 1, Id = "dupe", Name = "Colliding pack" };

        // Built-ins must be passed first (see FromEmbeddedSystems) so they always win.
        var ex = Assert.Throws<CharacterSystemLoadException>(() => new CharacterSystemRegistry([builtIn, colliding]));

        Assert.Contains("dupe", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_throws_on_null_systems()
    {
        Assert.Throws<ArgumentNullException>(() => new CharacterSystemRegistry(null!));
    }
}