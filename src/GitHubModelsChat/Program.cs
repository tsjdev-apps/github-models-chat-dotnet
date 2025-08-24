using Azure;
using Azure.AI.Inference;
using GitHubModelsChat.Utils;
using Spectre.Console;
using System.Text;

// --- Configuration ---

const string DefaultSystemPrompt = "You are a helpful assistant.";

// soft cap to prevent unbounded memory growth
const int MaxHistoryMessages = 10;

// --- Local helpers ---

static void TrimHistory(ChatCompletionsOptions options, int maxMessages)
{
    // Keep the system message + the last N messages.
    if (options.Messages.Count <= maxMessages + 1)
    {
        return;
    }

    // Preserve the first message if it's a system message.
    int startIndex
        = options.Messages.Count - maxMessages;
    List<ChatRequestMessage> keep
        = [.. options.Messages.Skip(startIndex)];

    // Rebuild list with system message (if any) + tail.
    ChatRequestMessage? system
        = options.Messages.FirstOrDefault(m => m is ChatRequestSystemMessage);

    options.Messages.Clear();

    if (system is not null)
    {
        options.Messages.Add(system);
    }

    foreach (ChatRequestMessage m in keep)
    {
        options.Messages.Add(m);
    }
}

static void WriteAiHeader()
    => AnsiConsole.MarkupLine("\n[bold]AI:[/]");

// --- Main program ---

try
{
    // Draw header
    ConsoleHelper.ShowHeader();

    // Ask for GitHub PAT securely (masked input)
    string pat
        = ConsoleHelper.GetSecret(
            "Enter your [red]GitHub Personal Access Token (PAT)[/]:");

    // Ask for the model (e.g., microsoft/Phi-4 or microsoft/phi-4-mini-instruct)
    string model
        = ConsoleHelper.GetString(
            "Enter the [red]GitHub Model[/] you want to use (e.g. microsoft/Phi-4):",
            shouldClear: true);

    // Create the client once and reuse it
    Uri endpoint = new("https://models.github.ai/inference");
    AzureKeyCredential credential = new(pat);
    ChatCompletionsClient client = new(
        endpoint,
        credential,
        new AzureAIInferenceClientOptions());

    ChatCompletionsOptions requestOptions = new()
    {
        Model = model,
        Messages = { new ChatRequestSystemMessage(DefaultSystemPrompt) }
    };

    // Support Ctrl+C to break out cleanly
    using CancellationTokenSource cts = new();
    Console.CancelKeyPress += (_, e) =>
    {
        // prevent immediate process kill
        e.Cancel = true;
        cts.Cancel();
    };

    // Redraw header before entering the loop
    ConsoleHelper.ShowHeader();

    // Chat loop
    while (!cts.IsCancellationRequested)
    {
        // Prompt the user; allow '/exit' to quit quickly
        string userMessage
            = ConsoleHelper.GetString(
                "Enter your message (or /exit):",
                shouldClear: false).Trim();

        if (userMessage.Equals("/exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            continue;
        }

        // Add user message to history
        requestOptions.Messages.Add(
            new ChatRequestUserMessage(userMessage));

        // Stream assistant reply
        WriteAiHeader();

        CompletionsUsage? usage = null;
        StringBuilder answer = new();

        try
        {
            // Send request and stream tokens as they arrive
            StreamingResponse<StreamingChatCompletionsUpdate> response
                = await client.CompleteStreamingAsync(requestOptions, cts.Token);

            await foreach (StreamingChatCompletionsUpdate? update
                in response.WithCancellation(cts.Token))
            {
                if (!string.IsNullOrEmpty(update.ContentUpdate))
                {
                    answer.Append(update.ContentUpdate);
                    Console.Write(update.ContentUpdate);
                }

                if (update.Usage is not null)
                {
                    usage = update.Usage;
                }
            }
        }
        catch (OperationCanceledException)
        {
            ConsoleHelper.DisplayError("Operation cancelled.");
            break;
        }
        catch (Exception ex)
        {
            ConsoleHelper.DisplayError($"Request failed: {ex.Message}");
            // Remove the last user message if the request failed,
            // so a transient error doesn't taint the conversation state.
            requestOptions.Messages.RemoveAt(requestOptions.Messages.Count - 1);
            continue;
        }

        // Persist assistant message in history
        string finalText = answer.ToString();
        requestOptions.Messages.Add(new ChatRequestAssistantMessage(finalText));

        // Trim history to avoid excessive growth over long chats
        TrimHistory(requestOptions, MaxHistoryMessages);

        // Show token usage (if available)
        ConsoleHelper.WriteUsage(usage);
    }
}
catch (Exception ex)
{
    // Final safety net
    ConsoleHelper.DisplayError($"Fatal error: {ex.Message}");
}
