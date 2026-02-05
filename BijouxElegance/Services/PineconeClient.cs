using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BijouxElegance.Services
{
    public class PineconeClient : IVectorDbClient
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<PineconeClient> _logger;
        private readonly string _indexName;
        private readonly string _baseUrl;

        public PineconeClient(IHttpClientFactory httpFactory, IConfiguration config, ILogger<PineconeClient> logger)
        {
            _http = httpFactory.CreateClient();
            _config = config;
            _logger = logger;
            _indexName = _config.GetValue<string>("Pinecone:IndexName") ?? "products-index";

            var apiKey = _config.GetValue<string>("Pinecone:ApiKey");
            var environment = _config.GetValue<string>("Pinecone:Environment");

            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("Pinecone:ApiKey must be configured.");

            if (string.IsNullOrEmpty(environment))
                throw new InvalidOperationException("Pinecone:Environment must be set to the index-specific URL (e.g., 'https://products-index-xxxxx.svc.us-east-1.pinecone.io').");

            // Configuration du HttpClient
            _baseUrl = environment.TrimEnd('/');
            _http.DefaultRequestHeaders.Add("Api-Key", apiKey);
            _http.DefaultRequestHeaders.Add("Accept", "application/json");

            _logger.LogInformation($"Pinecone client initialized for index '{_indexName}' at {_baseUrl}");
        }

        public async Task UpsertAsync(string indexName, string id, float[] vector, Dictionary<string, object>? metadata = null)
        {
            var payload = new
            {
                vectors = new[]
                {
                    new
                    {
                        id = id,
                        values = vector,
                        metadata = metadata ?? new Dictionary<string, object>()
                    }
                }
            };

            // URL pour Pinecone - utilisation directe de l'URL de l'index
            var url = $"{_baseUrl}/vectors/upsert";
            _logger.LogDebug($"Pinecone upsert to: {url}");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var content = JsonContent.Create(payload, options: jsonOptions);
            var response = await _http.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Pinecone upsert failed ({response.StatusCode}): {body}");
                throw new HttpRequestException($"Pinecone upsert failed: {response.StatusCode}");
            }

            _logger.LogDebug($"Successfully upserted vector with ID: {id}");
        }

        public async Task<List<VectorMatch>> QueryAsync(string indexName, float[] vector, int topK = 10)
        {
            var payload = new
            {
                topK = topK,
                vector = vector,
                includeMetadata = true,
                includeValues = false
            };

            var url = $"{_baseUrl}/query";
            _logger.LogDebug($"Pinecone query to: {url}");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var content = JsonContent.Create(payload, options: jsonOptions);
            var response = await _http.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Pinecone query failed ({response.StatusCode}): {body}");
                throw new HttpRequestException($"Pinecone query failed: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var matches = new List<VectorMatch>();

            if (doc.RootElement.TryGetProperty("matches", out var matchesElement))
            {
                foreach (var match in matchesElement.EnumerateArray())
                {
                    var vectorMatch = new VectorMatch
                    {
                        Id = match.GetProperty("id").GetString() ?? string.Empty,
                        Score = match.GetProperty("score").GetSingle()
                    };

                    if (match.TryGetProperty("metadata", out var metadataElement))
                    {
                        vectorMatch.Metadata = new Dictionary<string, object>();
                        foreach (var prop in metadataElement.EnumerateObject())
                        {
                            vectorMatch.Metadata[prop.Name] = prop.Value.ToString();
                        }
                    }

                    matches.Add(vectorMatch);
                }
            }

            return matches;
        }
    }
}