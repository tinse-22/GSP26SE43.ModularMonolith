using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestGeneration.Services;

internal static partial class LlmSuggestionFeedbackTextSanitizer
{
    public const int MaxNotesLength = 4000;
    public const int MaxPromptNoteSnippetLength = 240;
    public const int MaxPromptNotesPerEndpoint = 3;

    public static string NormalizeForStorage(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var normalized = notes.Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    public static string NormalizeForPrompt(string notes)
    {
        var normalized = NormalizeForStorage(notes);
        if (normalized == null)
        {
            return null;
        }

        normalized = WhitespacePattern().Replace(normalized, " ").Trim();

        if (normalized.Length <= MaxPromptNoteSnippetLength)
        {
            return normalized;
        }

        return normalized[..MaxPromptNoteSnippetLength].TrimEnd() + "...";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
