namespace GmToolkit.Core.Import;

/// <summary>
/// The outcome of validating an import DTO (or a tree of them) against <see cref="ImportValidator"/>'s
/// rules — a list of blocking <see cref="Errors"/> and non-blocking <see cref="Warnings"/>, both
/// human-readable and safe to show a GM directly (matching <c>DataAccessException.Message</c>'s
/// convention of always being display-ready). <see cref="IsValid"/> only looks at <see cref="Errors"/>
/// — a warning never blocks an import.
/// </summary>
public sealed record ValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;

    /// <summary>A result with no errors and no warnings.</summary>
    public static ValidationResult Success() => new([], []);

    /// <summary>A result with a single blocking error and no warnings.</summary>
    public static ValidationResult Failure(string error) => new([error], []);

    /// <summary>
    /// Combines several results (e.g. a campaign's own checks plus each nested PC's/NPC's) into one,
    /// concatenating every error and warning in order.
    /// </summary>
    public static ValidationResult Merge(IEnumerable<ValidationResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var result in results)
        {
            errors.AddRange(result.Errors);
            warnings.AddRange(result.Warnings);
        }

        return new ValidationResult(errors, warnings);
    }
}