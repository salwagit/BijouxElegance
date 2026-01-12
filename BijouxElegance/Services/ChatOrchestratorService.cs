/*using BijouxElegance.Models;
using Microsoft.Extensions.Logging;

namespace BijouxElegance.Services
{
    public class ChatOrchestratorService
    {
        private readonly GroqService _groqService;
        private readonly ProductSearchService _searchService;
        private readonly ILogger<ChatOrchestratorService> _logger;

        public ChatOrchestratorService(
            GroqService groqService,
            ProductSearchService searchService,
            ILogger<ChatOrchestratorService> logger)
        {
            _groqService = groqService;
            _searchService = searchService;
            _logger = logger;
        }

        public async Task<ChatResponse> ProcessMessageAsync(string userMessage, List<LocalCartItemDto>? localCart = null)
        {
            _logger.LogInformation("Traitement message: {Message}", userMessage);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 1. Recherche RAG dans les produits
                var searchResult = await _searchService.SearchRelevantProductsAsync(userMessage);

                // 2. Si local cart fourni, récupérer les détails des produits
                var localCartProducts = new List<Product>();
                var localCartContext = string.Empty;

                if (localCart != null && localCart.Any())
                {
                    var ids = localCart.Select(x => x.ProductId).Distinct();
                    localCartProducts = await _searchService.GetProductsByIdsAsync(ids);

                    if (localCartProducts.Any())
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("🧾 Contenu du panier local :");
                        foreach (var p in localCartProducts)
                        {
                            var qty = localCart.FirstOrDefault(x => x.ProductId == p.ProductId)?.Quantity ?? 0;
                            sb.AppendLine($"- {p.Name} (ID:{p.ProductId}) - {p.Price}€ - qty:{qty} - stock:{p.StockQuantity}");
                        }
                        localCartContext = sb.ToString();
                    }
                }

                // 3. Combiner les contextes
                var combinedContext = string.Empty;
                if (!string.IsNullOrEmpty(localCartContext)) combinedContext += localCartContext + "\n\n";
                if (!string.IsNullOrEmpty(searchResult.Context)) combinedContext += searchResult.Context;

                // 4. Appeler Groq avec le contexte combiné
                var aiResponse = await _groqService.GetChatResponseAsync(
                    userMessage,
                    combinedContext
                );

                stopwatch.Stop();

                // 5. Fusionner les listes de produits
                var mergedProducts = new List<Product>(searchResult.Products ?? new List<Product>());
                foreach (var p in localCartProducts)
                {
                    if (!mergedProducts.Any(mp => mp.ProductId == p.ProductId))
                        mergedProducts.Add(p);
                }

                return new ChatResponse
                {
                    Success = true,
                    Message = aiResponse,
                    Products = mergedProducts,
                    HasProducts = mergedProducts.Any(),
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Source = "groq+rag+localcart"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur orchestrateur");

                // Fallback basique
                return new ChatResponse
                {
                    Success = false,
                    Message = GetFallbackResponse(userMessage),
                    Products = new List<Product>(),
                    HasProducts = false,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Source = "fallback"
                };
            }
        }

        private string GetFallbackResponse(string query)
        {
            var lowerQuery = query.ToLower();

            // Réponses de secours basées sur règles
            if (lowerQuery.Contains("bonjour") || lowerQuery.Contains("salut"))
                return "Bonjour ! Bienvenue chez Bijoux Élégance. 💎";

            if (lowerQuery.Contains("prix") || lowerQuery.Contains("coût"))
                return "Nos prix varient de 50€ à 5000€ selon les créations. Consultez nos fiches produits !";

            if (lowerQuery.Contains("livraison"))
                return "Livraison offerte à partir de 150€ d'achat ! 🚚";

            if (lowerQuery.Contains("contact"))
                return "📞 01 23 45 67 89 | ✉️ contact@bijouxelegance.com";

            if (lowerQuery.Contains("heure") || lowerQuery.Contains("ouvert"))
                return "Ouvert du lundi au samedi, 10h-19h. Dimanche sur rendez-vous.";

            return "Pour une réponse précise, contactez-nous directement au 01 23 45 67 89.";
        }
    }

    // DTO for local cart items passed from client
    public class LocalCartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<Product> Products { get; set; }
        public bool HasProducts { get; set; }
        public long ResponseTimeMs { get; set; }
        public string Source { get; set; }
    }
}*/