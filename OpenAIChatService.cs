using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace sample_wpf;

public sealed class OpenAIChatService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private bool _disposeHttpClient;

    public OpenAIChatService(HttpClient? httpClient = null, string? apiKey = null, string model = "gpt-4o-mini")
    {
        _httpClient = httpClient ?? new HttpClient();
        _disposeHttpClient = httpClient is null;
        var resolvedApiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            throw new InvalidOperationException("Set the OPENAI_API_KEY environment variable before calling OpenAI.");
        }

        _httpClient.BaseAddress ??= new Uri("https://api.openai.com/v1/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", resolvedApiKey);
        _httpClient.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");

        _model = model;
    }

    private const string AppGuide = """
The desktop app hosting you is called "OpenAI Chat Console". It has two tabs:
1. Chat tab: prompt box, Send button, status message, and read-only response output.
2. Todo List tab: textbox labeled "Enter a todo item", Add button, and a list with Remove buttons beside each entry.

When users ask questions about navigating or using the app (for example, how to add or remove todo items), explain the steps clearly based on that UI. Keep answers concise and focus on helping them use the app features.
""";

    private const string CodeContext = """
Key classes:
- sample_wpf.MainWindow (code-behind) owns ObservableCollection<string> Todos, sets the window DataContext, and provides:
  * SendButton_Click: validates PromptTextBox, calls OpenAIChatService, updates ResponseTextBox and StatusTextBlock, manages CancellationTokenSource.
  * AddTodo_Click: trims TodoInputTextBox text, appends to Todos, clears and refocuses the input.
  * RemoveTodo_Click: removes the string bound to the clicked Remove button from Todos.
  * OnClosed: cancels any in-flight request and disposes OpenAIChatService.
- sample_wpf.OpenAIChatService wraps HttpClient, loads OPENAI_API_KEY, POSTs to /v1/chat/completions with gpt-4o-mini, and returns the first choice message or throws with details on failure.

XAML wiring highlights:
- MainWindow hosts a TabControl with Chat and Todo List tabs.
- Todo tab ListBox ItemsSource binds to Todos; each item template includes the Remove button executing RemoveTodo_Click.
""";

    public async Task<string> GetChatCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content =
                        "You are a friendly assistant embedded in a WPF desktop app. " +
                        "Use the following references when you answer.\n\n" +
                        "=== App Guide ===\n" + AppGuide +
                        "\n=== Code Context ===\n" + CodeContext
                },
                new { role = "user", content = prompt }
            },
        };

        using var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"OpenAI call failed ({response.StatusCode}): {json}");
        }

        using var document = JsonDocument.Parse(json);
        var choices = document.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            return "OpenAI returned no choices.";
        }

        return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
