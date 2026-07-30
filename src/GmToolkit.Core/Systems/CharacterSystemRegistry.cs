using System.Diagnostics.CodeAnalysis;

namespace GmToolkit.Core.Systems;

/// <summary>
/// Default <see cref="ICharacterSystemRegistry"/>, built from an ordered set of already-loaded
/// <see cref="CharacterSystem"/>s (see <see cref="FromEmbeddedSystems"/> for the app's real
/// startup path). Enforces SYSTEMS.md's collision rule: <see cref="CharacterSystem.Id"/> must be
/// unique among all installed systems, and built-ins always win — the caller is expected to pass
/// built-ins first (see <see cref="FromEmbeddedSystems"/>), and a later entry declaring an
/// already-registered <see cref="CharacterSystem.Id"/> is rejected outright rather than silently
/// replacing (or being silently dropped in favor of) the earlier one, so an id collision can never
/// silently rebind an existing campaign's <c>CharacterSystemId</c> to a different pack's fields.
/// </summary>
public sealed class CharacterSystemRegistry : ICharacterSystemRegistry
{
    private readonly Dictionary<string, CharacterSystem> _systemsById;

    public CharacterSystemRegistry(IEnumerable<CharacterSystem> systems)
    {
        ArgumentNullException.ThrowIfNull(systems);

        var byId = new Dictionary<string, CharacterSystem>(StringComparer.Ordinal);
        foreach (var system in systems)
        {
            if (!byId.TryAdd(system.Id, system))
            {
                throw new CharacterSystemLoadException(
                    $"Character system '{system.Id}' collides with an already-registered system of the same id; " +
                    "the earlier-registered (built-in) system wins and the colliding one is rejected.");
            }
        }

        _systemsById = byId;
    }

    /// <summary>
    /// Builds a registry from <see cref="GenericCharacterSystem.Instance"/> (always first, so it
    /// always wins any collision) plus every embedded character system pack (the app's real
    /// startup path). Leaves room for a later <c>FromInstalledPacks</c>-style factory (#91,
    /// paused) that would combine these same built-ins with a second, downloaded source — nothing
    /// about this factory's shape needs to change to add that.
    /// </summary>
    public static CharacterSystemRegistry FromEmbeddedSystems()
    {
        var generic = GenericCharacterSystem.Instance;
        CharacterSystemLoader.Validate(generic, "generic (built-in)");

        var systems = new List<CharacterSystem> { generic };
        systems.AddRange(CharacterSystemLoader.LoadAll());

        return new CharacterSystemRegistry(systems);
    }

    public IReadOnlyCollection<CharacterSystem> GetAll() => _systemsById.Values;

    public CharacterSystem GetById(string id)
    {
        if (!_systemsById.TryGetValue(id, out var system))
        {
            throw new KeyNotFoundException($"No character system registered with id '{id}'.");
        }

        return system;
    }

    public bool TryGetById(string id, [NotNullWhen(true)] out CharacterSystem? system)
        => _systemsById.TryGetValue(id, out system);
}