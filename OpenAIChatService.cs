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

    public OpenAIChatService(HttpClient? httpClient = null, string? apiKey = null, string model = "gpt-5")
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

    public async Task<string> GetChatCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "You are a friendly assistant embedded in a WPF desktop app." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7
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
