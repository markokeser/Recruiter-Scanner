using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Recruiter_Scanner.Services
{
    /// <summary>
    /// Shared plumbing for calling the OpenAI Chat Completions API.
    /// </summary>
    public abstract class OpenAIServiceBase
    {
        private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
        private const string DefaultModel = "gpt-4o-mini";

        private static readonly JsonSerializerOptions CaseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;

        protected string Model { get; }

        protected OpenAIServiceBase(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = Environment.GetEnvironmentVariable("AI_PASS") ?? configuration["OpenAI:ApiKey"];
            Model = configuration["OpenAI:Model"] ?? DefaultModel;
        }

        /// <summary>
        /// Sends a system + user prompt and returns the raw text of the first choice.
        /// </summary>
        protected async Task<string> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            double temperature,
            int maxTokens,
            bool jsonMode = false)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("OpenAI API key is not configured. Set the AI_PASS environment variable.");

            var body = new Dictionary<string, object>
            {
                ["model"] = Model,
                ["messages"] = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                ["temperature"] = temperature,
                ["max_tokens"] = maxTokens
            };

            if (jsonMode)
                body["response_format"] = new { type = "json_object" };

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"OpenAI API error {(int)response.StatusCode}: {responseJson}");

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content) &&
                content.GetString() is { Length: > 0 } text)
            {
                return text;
            }

            throw new InvalidOperationException("Unexpected OpenAI API response format.");
        }

        /// <summary>
        /// Same as <see cref="CompleteAsync"/> in JSON mode, deserialized into <typeparamref name="T"/>.
        /// </summary>
        protected async Task<T> CompleteJsonAsync<T>(string systemPrompt, string userPrompt, double temperature, int maxTokens)
        {
            var content = await CompleteAsync(systemPrompt, userPrompt, temperature, maxTokens, jsonMode: true);

            return JsonSerializer.Deserialize<T>(CleanJson(content), CaseInsensitiveOptions)
                ?? throw new InvalidOperationException("Failed to parse the AI response.");
        }

        /// <summary>
        /// Removes blank lines and the stray commas the model occasionally emits
        /// (e.g. <c>"score": 6, ,"reasoning": ...</c>).
        /// </summary>
        protected static string CleanJson(string json)
        {
            var lines = json.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            json = string.Join("\n", lines);

            json = Regex.Replace(json, @",\s*,", ",");
            json = Regex.Replace(json, @"\{\s*,", "{");
            json = Regex.Replace(json, @",\s*\}", "}");

            return json;
        }
    }
}
