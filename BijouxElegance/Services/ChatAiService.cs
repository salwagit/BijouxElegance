using BijouxElegance.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BijouxElegance.Services
{
    public class ChatAiService
    {
        private readonly ApplicationDbContext _db;
        private readonly HttpClient _http;
        private readonly ILogger<ChatAiService> _logger;
        private readonly IConfiguration _config;
        private readonly string _groqApiUrl = "https://api.groq.com/openai/v1/chat/completions";
        private readonly string _apiKey;
        private readonly string _model;
        private readonly IVectorDbClient _vectorClient;
        private readonly IServiceProvider _services;

        // Default pricing per 1000 tokens (EUR) as fallback if not provided in configuration
        private readonly Dictionary<string, decimal> _defaultPricingPerThousand = new()
        {
            { "llama-3.1-8b-instant", 0.002m },      // example: 0.002 EUR per 1k tokens
            { "llama-3.3-70b-versatile", 0.02m }   // example: 0.02 EUR per 1k tokens
        };

        public ChatAiService(ApplicationDbContext db, IHttpClientFactory httpFactory, ILogger<ChatAiService> logger, IConfiguration config, IVectorDbClient vectorClient, IServiceProvider services)
        {
            _db = db;
            _http = httpFactory.CreateClient();
            _logger = logger;
            _config = config;
            _vectorClient = vectorClient;
            _services = services;

            _apiKey = _config.GetValue<string>("Groq:ApiKey") ?? string.Empty;
            _model = _config.GetValue<string>("Groq:Model") ?? "llama3-8b-8192";

            // set base address (optional)
            _http.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        }

        private static string GetStockStatus(int stock)
        {
            if (stock <= 0) return "Non disponible";
            // Interpret 1..3 as "Bientôt saturé", >3 as "Disponible"
            if (stock <= 3) return "Bientôt saturé";
            return "Disponible";
        }

        private static string Truncate(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        public async Task<ChatResponseDto> GetAiResponseAsync(ChatRequestDto request)
        {
            // 0) quick validation
            if (string.IsNullOrWhiteSpace(request?.UserMessage))
                return new ChatResponseDto { Reply = "Message vide" };

            var userText = request.UserMessage.Trim();
            var userLower = userText.ToLowerInvariant();

            // Heuristics / business rules before calling embeddings or vector DB
            // 1) Greeting or empty intent -> ask clarifying questions
            var greetings = new[] { "bonjour", "salut", "bonsoir", "hello", "hi" };
            if (greetings.Any(g => Regex.IsMatch(userLower, $"^\\b{Regex.Escape(g)}\\b", RegexOptions.IgnoreCase)) || userLower.Length <= 6)
            {
                return new ChatResponseDto
                {
                    Reply = "Bonjour! Je suis Sophie, l'assistante virtuelle de Bijoux Élégance, une boutique de bijoux de luxe pour vous aider, merci de préciser :\n- Quel type de bijou souhaitez-vous (bague, collier, bracelet, boucles d'oreilles) ?\n- Matériau préféré (or, argent, diamant) ?\n- Budget approximatif ?"
                };
            }

            // 2) If user asks about panier but has no local items -> invite to add products
            var asksAboutCart = new[] { "panier", "acheter", "checkout", "commander" };
            if (asksAboutCart.Any(a => userLower.Contains(a)) && (request.LocalCartItems == null || !request.LocalCartItems.Any()))
            {
                // Provide short actionable suggestion and list a few featured products
                var featured = await _db.Products.Include(p => p.Category).Where(p => p.IsFeatured).Take(3).ToListAsync();
                if (featured.Any())
                {
                    var list = string.Join("; ", featured.Select(p => $"{p.Name} ({p.Price.ToString("C")})"));
                    return new ChatResponseDto { Reply = $"Votre panier est vide. Voulez-vous ajouter un article ? Par exemple : {list}." };
                }
                return new ChatResponseDto { Reply = "Votre panier est vide. Souhaitez-vous que je vous propose des articles à ajouter ?" };
            }

            // 3) Wedding rings rule: if user mentions mariage / alliances -> later only recommend rings
            var ringKeywords = new[] { "mariage", "alliances", "alliance", "bague de mariage", "bague" };
            var requireRingsOnly = ringKeywords.Any(k => userLower.Contains(k));

            // 1) Create embedding for user query
            float[] queryEmbedding;
            try
            {
                queryEmbedding = await EmbeddingsHelper.CreateEmbeddingAsync(request.UserMessage, _services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create embedding for query");
                // fallback: return a helpful error to user
                return new ChatResponseDto { Reply = "Impossible de traiter votre demande pour le moment (erreur embedding)." };
            }

            if (queryEmbedding == null || queryEmbedding.Length == 0)
            {
                return new ChatResponseDto { Reply = "Impossible de générer un embedding pour la requête." };
            }

            // 2) Query vector DB (Pinecone)
            var indexName = _config.GetValue<string>("Pinecone:IndexName") ?? "products-index";
            List<VectorMatch> matches;
            try
            {
                matches = await _vectorClient.QueryAsync(indexName, queryEmbedding, topK: 10);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector DB query failed");
                return new ChatResponseDto { Reply = "Recherche produit indisponible pour le moment." };
            }

            if (matches == null || matches.Count == 0)
            {
                return new ChatResponseDto { Reply = "Aucun produit pertinent trouvé." };
            }

            // 3) Fetch products from DB for matched IDs
            var ids = matches.Select(m => { if (int.TryParse(m.Id, out var v)) return v; return -1; }).Where(id => id > 0).ToArray();

            var matchedProducts = await _db.Products
                .Include(p => p.Category)
                .Where(p => ids.Contains(p.ProductId))
                .ToListAsync();

            // If wedding rings requested, filter to rings (category or name)
            if (requireRingsOnly)
            {
                var ringProducts = matchedProducts.Where(p => (p.Category != null && p.Category.Name != null && p.Category.Name.ToLower().Contains("bag")) || p.Name.ToLower().Contains("bague")).ToList();
                if (!ringProducts.Any())
                {
                    return new ChatResponseDto { Reply = "Je n'ai pas de bagues de mariage pertinentes dans le contexte. Voulez-vous que je cherche des bagues similaires ou indiquez un matériau préféré (or, argent, diamant) ?" };
                }
                matchedProducts = ringProducts;
                matches = matches.Where(m => matchedProducts.Any(p => p.ProductId.ToString() == m.Id)).ToList();
            }

            // 4) Re-rank: preserve match score order, then boost featured and availability
            var ranked = matches
                .Select(m => new { Match = m, Product = matchedProducts.FirstOrDefault(p => p.ProductId.ToString() == m.Id) })
                .Where(x => x.Product != null)
                .Select(x => new
                {
                    Product = x.Product!,
                    Score = x.Match.Score,
                    IsFeatured = x.Product.IsFeatured ? 1 : 0,
                    Availability = x.Product.StockQuantity > 0 ? 1 : 0
                })
                .OrderByDescending(x => x.IsFeatured)
                .ThenByDescending(x => x.Availability)
                .ThenByDescending(x => x.Score)
                .Select(x => x.Product)
                .ToList();

            if (!ranked.Any())
            {
                return new ChatResponseDto { Reply = "Aucun produit pertinent disponible à recommander." };
            }

            // Detect explicit recommendation intent — if present, return concrete suggestions directly (server-side)
            var recommendKeywords = new[] { "sugg", "propose", "recommande", "conseil", "cherche", "je veux", "montre", "quel", "quels", "suggest" };
            var wantsSuggestions = recommendKeywords.Any(k => userLower.Contains(k));

            if (wantsSuggestions)
            {
                var suggestions = ranked.Take(4).ToList();
                var productSummaries = suggestions.Select(p => new ProductSummary
                {
                    Name = p.Name,
                    Price = p.Price,
                    StockStatus = GetStockStatus(p.StockQuantity),
                    Category = p.Category?.Name ?? string.Empty
                }).ToList();

                // Build a short, purchase-oriented reply server-side to guarantee specificity
                var lines = productSummaries.Select((ps, idx) => $"{idx+1}) {ps.Name} — {ps.Price.ToString("C")} — {ps.StockStatus}");
                var replyText = "Voici des suggestions adaptées : " + string.Join("; ", lines.Take(4)) + ".\nSouhaitez-vous ajouter l'un de ces articles à votre panier ?";

                return new ChatResponseDto { Reply = replyText, Products = productSummaries };
            }


            // 5) Build concise context (no IDs, no exact stock/quantities)
            var sbContext = new StringBuilder();
            sbContext.AppendLine("Contexte produits extraits de la base de données (pas d'ID, pas de stock exact, pas de quantités):\n");

            foreach (var p in ranked.Take(8))
            {
                var stockStatus = GetStockStatus(p.StockQuantity);
                var desc = Truncate(p.Description, 220);
                sbContext.AppendLine($"- Nom: {p.Name} | Prix: {p.Price} | Stock: {stockStatus} | Catégorie: {p.Category?.Name} | Description: {desc}");
            }

            if (request.LocalCartItems != null && request.LocalCartItems.Any())
            {
                // map local cart product presence to statuses
                var localIds = request.LocalCartItems.Select(i => i.ProductId).Distinct().ToArray();
                var localProducts = await _db.Products.Where(p => localIds.Contains(p.ProductId)).ToListAsync();
                if (localProducts.Any())
                {
                    sbContext.AppendLine("\nContenu du panier local (pas d'ID ni de quantités exactes):");
                    foreach (var p in localProducts)
                    {
                        var stockStatus = GetStockStatus(p.StockQuantity);
                        sbContext.AppendLine($"- Nom: {p.Name} | Prix: {p.Price} | Stock: {stockStatus} | Catégorie: {p.Category?.Name}");
                    }
                }
            }

            // 6) Prompt system strict (updated with business rules)
            var systemPrompt = $@"Tu es Sophie, l'assistante virtuelle de Bijoux Élégance.
Règles IMPORTANTES (OBLIGATOIRE) :
1) SOIS SPÉCIFIQUE : Quand l'utilisateur demande des bijoux, PROPOSE DES PRODUITS CONCRETS du catalogue fournis dans le CONTEXTE.
2) UTILISE LES PRODUITS DISPONIBLES : Ne recommande que des produits présents dans le CONTEXTE ci?dessous.
3) SI AMBIGU : Pose une question de clarification courte :
   - Quel type de bijou (bague, collier, bracelet, boucles d'oreilles) ?
   - Matériau préféré (or, argent, diamant) ?
   - Budget approximatif ?
4) ANNEAUX DE MARIAGE : Si la demande concerne le mariage / alliances, RECOMMANDE SPÉCIFIQUEMENT DES BAGUES uniquement.
5) PANIER : Si l'utilisateur n'a pas de panier réel, propose d'ajouter des produits concrets.
6) Ne jamais afficher d'IDs, ni de quantités EXACTES, ni de stock exact. Utilise uniquement les statuts : Disponible / Bientôt saturé / Non disponible.
7) Réponses : courtes, claires, orientées achat (max 3 phrases sauf demande explicite). Si tu proposes des suggestions, fournis 3 à 4 produits maximum en citant leur nom, prix et statut.

CONTEXTE (ne pas inventer d'informations) :\n{sbContext}\n
Réponds maintenant en français de manière concise et respecte les règles ci?dessus.";

            // 7) Build request to Groq
            var body = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = request.UserMessage }
                },
                temperature = _config.GetValue<double>("Groq:Temperature", 0.2),
                max_tokens = _config.GetValue<int>("Groq:MaxTokens", 500),
                top_p = 0.9,
                stream = false
            };

            try
            {
                // Set auth header for this request
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var jsonOpt = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var httpResp = await _http.PostAsJsonAsync("chat/completions", body, jsonOpt);

                // If model decommissioned (400 with specific code), retry with fallback model if configured
                if (!httpResp.IsSuccessStatusCode)
                {
                    var err = await httpResp.Content.ReadAsStringAsync();
                    _logger.LogError("Groq API error: {Status} - {Body}", (int)httpResp.StatusCode, err);

                    if ((int)httpResp.StatusCode == 400 && err != null && err.Contains("model_decommissioned"))
                    {
                        var fallback = _config.GetValue<string>("Groq:FallbackModel");
                        if (!string.IsNullOrEmpty(fallback) && fallback != _model)
                        {
                            _logger.LogInformation("Retrying Groq request with fallback model {Model}", fallback);
                            var body2 = new
                            {
                                model = fallback,
                                messages = body.messages,
                                temperature = _config.GetValue<double>("Groq:Temperature", 0.2),
                                max_tokens = _config.GetValue<int>("Groq:MaxTokens", 500),
                                top_p = 0.9,
                                stream = false
                            };

                            httpResp = await _http.PostAsJsonAsync("chat/completions", body2, jsonOpt);
                        }
                    }
                }

                if (!httpResp.IsSuccessStatusCode)
                {
                    var err = await httpResp.Content.ReadAsStringAsync();
                    _logger.LogError("Groq API error: {Status} - {Body}", (int)httpResp.StatusCode, err);
#if DEBUG
                    return new ChatResponseDto { Reply = $"Le service de chat est temporairement indisponible. Code: {(int)httpResp.StatusCode}. Body: {err}" };
#else
                    return new ChatResponseDto { Reply = "Le service de chat est temporairement indisponible." };
#endif
                }

                using var stream = await httpResp.Content.ReadAsStreamAsync();
                var doc = await JsonDocument.ParseAsync(stream);

                // Parsing choices[0].message.content
                var reply = string.Empty;
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
                    {
                        reply = content.GetString() ?? string.Empty;
                    }
                }

                // Parse usage if available
                int? promptTokens = null;
                int? completionTokens = null;
                int? totalTokens = null;
                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number && pt.TryGetInt32(out var ptv))
                        promptTokens = ptv;
                    if (usage.TryGetProperty("completion_tokens", out var ct) && ct.ValueKind == JsonValueKind.Number && ct.TryGetInt32(out var ctv))
                        completionTokens = ctv;
                    if (usage.TryGetProperty("total_tokens", out var tt) && tt.ValueKind == JsonValueKind.Number && tt.TryGetInt32(out var ttv))
                        totalTokens = ttv;
                }

                // Estimate cost (EUR) using config pricing or fallback table
                decimal? estimatedCost = null;
                try
                {
                    var modelKey = _config.GetValue<string>("Groq:Model") ?? _model;
                    decimal? pricePerThousand = null;
                    // Look for configuration entry Groq:Pricing:{model}
                    var cfgKey = $"Groq:Pricing:{modelKey}";
                    var cfgValue = _config.GetValue<string>(cfgKey);
                    if (!string.IsNullOrEmpty(cfgValue) && Decimal.TryParse(cfgValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    {
                        pricePerThousand = parsed;
                    }
                    else if (_defaultPricingPerThousand.TryGetValue(modelKey, out var dflt))
                    {
                        pricePerThousand = dflt;
                    }

                    if (pricePerThousand.HasValue && totalTokens.HasValue)
                    {
                        estimatedCost = (totalTokens.Value / 1000m) * pricePerThousand.Value;
                        // round to 4 decimal places
                        estimatedCost = Math.Round(estimatedCost.Value, 4);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to estimate cost");
                }

                // Prepare product summaries for frontend (max 4 suggestions)
                var suggestions = ranked
                    .Take(4)
                    .Select(p => new ProductSummary
                    {
                        Name = p.Name,
                        Price = p.Price,
                        StockStatus = GetStockStatus(p.StockQuantity),
                        Category = p.Category?.Name ?? string.Empty
                    })
                    .ToList();

                var response = new ChatResponseDto
                {
                    Reply = reply ?? string.Empty,
                    Products = suggestions
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur appel Groq");
                return new ChatResponseDto { Reply = "Erreur lors de la génération de la réponse." };
            }
        }
    }
}
