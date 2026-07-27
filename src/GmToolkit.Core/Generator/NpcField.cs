namespace GmToolkit.Core.Generator;

/// <summary>
/// The set of <see cref="GeneratedNpc"/> fields that <see cref="INpcGenerator"/> can produce,
/// individually or as part of a whole NPC. Exists so a caller (e.g. #28's per-field reroll button)
/// can name exactly one field to regenerate via <see cref="INpcGenerator.GenerateField"/> without
/// re-running the other five.
/// </summary>
/// <remarks>
/// <b>"Locked" has no meaning at this layer.</b> Neither this type, <see cref="INpcGenerator"/>,
/// nor any other type in <c>GmToolkit.Core.Generator</c> knows or cares whether a field is "locked" —
/// that concept exists purely in <c>GmToolkit.UI.ViewModels.GeneratorViewModel</c>'s <c>IsXLocked</c>
/// flags (issue #28), which is implemented entirely by the view model choosing not to call
/// <see cref="INpcGenerator.GenerateField(NpcField, IRandomSource)"/> for a locked field — Core simply
/// never sees a request for that field while it's locked. Consequently, "lock semantics: locked
/// fields never change" (issue #30's own task list) has nothing to test at this layer; it's covered
/// by <c>GeneratorViewModelTests</c>'s <c>Regenerating_with_the_name_locked_leaves_the_name_unchanged...</c>
/// and <c>Locking_a_field_blocks_its_own_reroll_button_too</c> tests instead.
/// </remarks>
public enum NpcField
{
    Name,
    Role,
    Appearance,
    Mannerism,
    Motivation,
    Secret,
}