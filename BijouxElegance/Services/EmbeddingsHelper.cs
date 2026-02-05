using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BijouxElegance.Services
{
    public static class EmbeddingsHelper
    {
        // Uses OpenAI embeddings endpoint; configuration in appsettings: OpenAI:ApiKey and OpenAI:Endpoint
        public static async Task<float[]> CreateEmbeddingAsync(string text, IServiceProvider services)
        {
            var config = services.GetRequiredService<IConfiguration>();
            var httpFactory = services.GetRequiredService<IHttpClientFactory>();
            // cannot use ILogger<EmbeddingsHelper> because EmbeddingsHelper is static; use non-generic logger
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("EmbeddingsHelper");
            var env = services.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

            // Use mock in Development if registered
            if (env != null && env.IsDevelopment())
            {
                var mock = services.GetService<MockEmbeddingsService>();
                if (mock != null)
                {
                    logger.LogDebug("Using MockEmbeddingsService for development");
                    return await mock.CreateMockEmbeddingAsync(text);
                }
            }

            var apiKey = config.GetValue<string>("OpenAI:ApiKey");
            var endpoint = config.GetValue<string>("OpenAI:Endpoint") ?? "https://api.openai.com/v1/embeddings";
            var model = config.GetValue<string>("OpenAI:Model") ?? "text-embedding-3-small";

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured.");
            }

            // Use named HttpClient if available (configured with Polly)
            var http = httpFactory.CreateClient("OpenAI");
            if (http == null) http = httpFactory.CreateClient();

            if (!http.DefaultRequestHeaders.Contains("Authorization"))
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // Configuration des retries avec backoff exponentiel
            const int maxRetries = 5;
            int retryCount = 0;
            TimeSpan delay = TimeSpan.FromSeconds(2);

            while (retryCount <= maxRetries)
            {
                try
                {
                    var body = new { input = text, model = model };

                    logger.LogDebug("Sending embedding request (attempt {Attempt}/{Max})", retryCount + 1, maxRetries + 1);

                    var resp = await http.PostAsJsonAsync(endpoint, body);

                    if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        if (retryCount >= maxRetries)
                        {
                            logger.LogWarning("Rate limit reached after maximum retries");
                            throw new HttpRequestException("Rate limit exceeded after maximum retries");
                        }

                        // Vérifier s'il y a un header Retry-After
                        if (resp.Headers.TryGetValues("Retry-After", out var retryValues))
                        {
                            if (int.TryParse(retryValues.FirstOrDefault(), out int retrySeconds))
                            {
                                delay = TimeSpan.FromSeconds(retrySeconds + 1);
                            }
                        }
                        else
                        {
                            // Backoff exponentiel avec jitter
                            var random = new Random();
                            var jitter = TimeSpan.FromMilliseconds(random.Next(100, 1000));
                            delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)) + jitter;
                        }

                        logger.LogWarning("Rate limited. Retrying in {Delay}s", delay.TotalSeconds);
                        await Task.Delay(delay);
                        retryCount++;
                        continue;
                    }

                    resp.EnsureSuccessStatusCode();

                    using var stream = await resp.Content.ReadAsStreamAsync();
                    var doc = await JsonDocument.ParseAsync(stream);

                    if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                    {
                        var embeddingElem = data[0].GetProperty("embedding");
                        var list = new List<float>();
                        foreach (var v in embeddingElem.EnumerateArray()) list.Add(v.GetSingle());
                        return list.ToArray();
                    }

                    return Array.Empty<float>();
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (retryCount >= maxRetries)
                    {
                        logger.LogError(ex, "Rate limit exceeded after maximum retries");
                        throw;
                    }

                    var random = new Random();
                    var jitter = TimeSpan.FromMilliseconds(random.Next(200, 1500));
                    delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)) + jitter;

                    logger.LogWarning(ex, "HTTP request failed with 429. Retrying in {Delay}s", delay.TotalSeconds);
                    await Task.Delay(delay);
                    retryCount++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create embedding");
                    throw;
                }
            }

            throw new Exception("Failed to create embedding after maximum retries");
        }
    }
}