using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace BijouxElegance.Services
{
    public class GroqService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GroqService> _logger;

        public GroqService(HttpClient httpClient, IMemoryCache cache,
                          IConfiguration configuration, ILogger<GroqService> logger)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration["Groq:ApiKey"]}");
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GetChatResponseAsync(string userMessage, string context = "")
        {
            try
            {
                // Vérifier le cache (5 minutes)
                var cacheKey = $"groq_{userMessage.GetHashCode()}_{context.GetHashCode()}";
                if (_cache.TryGetValue(cacheKey, out string cachedResponse))
                {
                    _logger.LogInformation("Retour depuis le cache");
                    return cachedResponse;
                }

                // Construire le prompt avec contexte RAG (context may include product facts and local cart)
                var systemPrompt = BuildSystemPrompt(context);

                // Read temperature/max tokens from config with sensible defaults
                var temperature = double.TryParse(_configuration["Groq:Temperature"], out var t) ? t : 0.7;
                var maxTokens = int.TryParse(_configuration["Groq:MaxTokens"], out var m) ? m : 500;

                // Appeler Groq API
                var requestBody = new
                {
                    model = _configuration["Groq:Model"] ?? "llama3-8b-8192",
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userMessage }
                    },
                    temperature = temperature,
                    max_tokens = maxTokens,
                    top_p = 0.9,
                    stream = false
                };

                _logger.LogInformation("Appel à Groq API...");
                var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadFromJsonAsync<GroqResponse>();
                var aiResponse = jsonResponse?.choices?.FirstOrDefault()?.message?.content?.Trim()
                                ?? "Je ne peux pas répondre pour le moment.";

                // Mettre en cache
                _cache.Set(cacheKey, aiResponse, TimeSpan.FromMinutes(_configuration.GetValue<int>("Groq:CacheDurationMinutes", 5)));

                return aiResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur Groq API");
                throw;
            }
        }

        private string BuildSystemPrompt(string productContext)
        {
            // productContext may contain product facts and local cart summary. Provide clear instructions to the model.
            return $@"
Tu es Sophie, l'assistante virtuelle experte de Bijoux Élégance, bijouterie de luxe à Paris.

But: Always follow these rules strictly:
- Réponds en français, de façon concise et professionnelle (maximum 3 phrases sauf si une liste de produits est demandée).
- Utilise uniquement les informations fournies dans le contexte produit et le contenu du panier local (ne pas halluciner de prix, de stock ou de promotions non fournis).
- Si le contexte contient des produits pertinents, propose jusqu'à 3 produits pertinents en expliquant brièvement POURQUOI (1 phrase) et fournis leur `id` pour un lien vers la fiche produit (ex: /Product/Details/{{id}}).
- Si l'utilisateur a un panier local, mentionne brièvement les articles présents et donne des recommandations adaptées (ex: complément, suggestion cadeau) si approprié.
- Si la question est ambiguë, demande une clarification courte.
- Si aucune information produit n'est pertinente, donne une réponse utile et propose des catégories à visiter ou demande une précision.

Contrainte de sécurité:
- Ne jamais inventer un prix, un stock, une offre ou une date de livraison.
- Invite l'utilisateur à consulter la fiche produit pour les détails techniques.

Voici les faits disponibles (contexte) :

{productContext}

Réponds maintenant à l'utilisateur de manière concise et actionnable.";
        }
    }

    public class GroqResponse
    {
        public List<Choice> choices { get; set; }
        public class Choice
        {
            public Message message { get; set; }
        }
        public class Message
        {
            public string content { get; set; }
        }
    }
}