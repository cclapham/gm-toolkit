using System.Diagnostics.CodeAnalysis;

namespace GmToolkit.Core.Systems;

/// <summary>
/// Looks up an installed <see cref="CharacterSystem"/> by its <see cref="CharacterSystem.Id"/>.
/// Mirrors <c>GmToolkit.Core.Generator.IGeneratorRegistry</c>'s shape/conventions. This is the
/// extensibility point for adding more system content: today,
/// <see cref="CharacterSystemRegistry.FromEmbeddedSystems"/> is the only source (the built-in
/// "generic" system plus whatever <c>Resources/CharacterSystems/*.json</c> packs #84-#87 add);
/// nothing about this interface has to change once a later <c>FromInstalledPacks</c>-style factory
/// (#91, paused) adds a second, downloaded source.
/// </summary>
public interface ICharacterSystemRegistry
{
    /// <summary>Every currently-registered character system, including the built-in "generic" (freeform) system.</summary>
    IReadOnlyCollection<CharacterSystem> GetAll();

    /// <summary>
    /// Gets the system registered under <paramref name="id"/>. Throws <see cref="KeyNotFoundException"/>
    /// if none is registered — matching <see cref="Dictionary{TKey,TValue}"/>'s own indexer
    /// convention. Callers that want a non-throwing lookup should use <see cref="TryGetById"/> instead.
    /// </summary>
    CharacterSystem GetById(string id);

    /// <summary>Non-throwing counterpart to <see cref="GetById"/>.</summary>
    bool TryGetById(string id, [NotNullWhen(true)] out CharacterSystem? system);
}