using Azure.AI.Inference;
using Spectre.Console;

namespace GitHubModelsChat.Utils;

/// <summary>
/// Utility helpers for consistent, styled console I/O using Spectre.Console.
/// </summary>
internal static class ConsoleHelper
{
    // Default validation bounds for free-text prompts.
    private const int DefaultMinLength = 3;
    private const int DefaultMaxLength = 200;

    /// <summary>
    /// Clears the console and renders the app header.
    /// </summary>
    public static void ShowHeader()
    {
        AnsiConsole.Clear();

        Grid grid = new();
        grid.AddColumn();

        // Big title in a Figlet font.
        grid.AddRow(new FigletText("GitHub Models").Centered().Color(Color.Red));

        // Sub-title / credit line.
        grid.AddRow(
            Align.Center(
                new Panel("[red]Sample by Thomas Sebastian Jensen ([link]https://www.tsjdev-apps.de[/])[/]")
            )
        );

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Shows a prompt, validates the input length, and returns the trimmed result.
    /// </summary>
    /// <param name="prompt">Message displayed to the user.</param>
    /// <param name="shouldClear">When true, clears the console and reprints the header first.</param>
    /// <param name="minLength">Minimum allowed length (defaults to 3).</param>
    /// <param name="maxLength">Maximum allowed length (defaults to 200).</param>
    /// <returns>Trimmed user input.</returns>
    public static string GetString(
        string prompt,
        bool shouldClear = true,
        int minLength = DefaultMinLength,
        int maxLength = DefaultMaxLength)
    {
        if (shouldClear)
        {
            ShowHeader();
        }

        if (minLength < 0)
        {
            minLength = 0;
        }

        if (maxLength < minLength)
        {
            maxLength = minLength;
        }

        string result = AnsiConsole.Prompt(
            new TextPrompt<string>(prompt)
                .PromptStyle("white")
                .ValidationErrorMessage("[red]Invalid input[/]")
                .Validate(input =>
                {
                    string value = (input ?? string.Empty).Trim();

                    if (value.Length < minLength)
                        return ValidationResult.Error($"[red]Value too short (min {minLength})[/]");

                    if (value.Length > maxLength)
                        return ValidationResult.Error($"[red]Value too long (max {maxLength})[/]");

                    return ValidationResult.Success();
                }));

        return result.Trim();
    }

    /// <summary>
    /// Prompts for a secret value (hidden input), validates, and returns the trimmed result.
    /// </summary>
    /// <param name="message">Prompt message shown to the user.</param>
    /// <returns>The secret input value as string.</returns>
    public static string GetSecret(string message)
    {
        string result = AnsiConsole.Prompt(
            new TextPrompt<string>(message)
                .PromptStyle("white")
                .Secret() // hides input
                .ValidationErrorMessage("[red]Invalid input[/]")
                .Validate(s =>
                    string.IsNullOrWhiteSpace(s) || s.Trim().Length < DefaultMinLength
                        ? ValidationResult.Error($"[red]Value too short (min {DefaultMinLength})[/]")
                        : ValidationResult.Success()));

        return result.Trim();
    }

    /// <summary>
    /// Writes an error message with red styling.
    /// </summary>
    /// <param name="message">Error text to display.</param>
    public static void DisplayError(string message)
        => AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");

    /// <summary>
    /// Prints token usage statistics if available.
    /// </summary>
    /// <param name="usage">Usage information from Azure AI Inference completions (nullable).</param>
    public static void WriteUsage(CompletionsUsage? usage)
    {
        if (usage is null)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]--- Usage ---[/]");
        AnsiConsole.MarkupLine($"[grey]Prompt Tokens: {usage.PromptTokens:N0}[/]");
        AnsiConsole.MarkupLine($"[grey]Completion Tokens: {usage.CompletionTokens:N0}[/]");
        AnsiConsole.MarkupLine($"[grey]Total Tokens: {usage.TotalTokens:N0}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }
}
