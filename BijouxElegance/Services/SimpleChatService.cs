using BijouxElegance.Data;
using BijouxElegance.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BijouxElegance.Services
{
    public class SimpleChatService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SimpleChatService> _logger;

        public SimpleChatService(ApplicationDbContext db, ILogger<SimpleChatService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<ChatResponse> ProcessMessageAsync(
            string message,
            List<LocalCartItemDTO>? localCart = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var query = message.ToLowerInvariant();

                // 🔍 Rechercher produits correspondant à la requête
                var products = await _db.Products
                    .Include(p => p.Category)
                    .Where(p =>
                        p.StockQuantity > 0 &&
                        (p.Name.ToLower().Contains(query) ||
                         p.Description.ToLower().Contains(query) ||
                         p.Category.Name.ToLower().Contains(query)))
                    .OrderBy(p => p.Price)
                    .Take(5)
                    .ToListAsync();

                // 💬 Construire le message de réponse
                string reply;
                if (products.Any())
                {
                    reply = $"✨ J’ai trouvé {products.Count} bijou(x) qui pourraient vous plaire :";
                }
                else
                {
                    reply = "Je n’ai pas trouvé de bijou correspondant exactement à votre recherche. " +
                            "Puis-je connaître votre budget, l’occasion ou vos préférences pour vous proposer des alternatives raffinées ?";
                }

                sw.Stop();

                return new ChatResponse
                {
                    Success = true,
                    Message = reply,
                    Products = products,
                    HasProducts = products.Any(),
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    Source = "database"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat error");

                return new ChatResponse
                {
                    Success = false,
                    Message = "Une erreur est survenue. Veuillez réessayer ou contacter notre service client.",
                    Products = new List<Product>(),
                    HasProducts = false,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    Source = "error"
                };
            }
        }
    }

    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public List<Product> Products { get; set; } = new();
        public bool HasProducts { get; set; }
        public long ResponseTimeMs { get; set; }
        public string Source { get; set; } = "";
    }
}