using BijouxElegance.Data;
using BijouxElegance.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BijouxElegance.Services
{
    public class ProductSearchService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductSearchService> _logger;

        public ProductSearchService(ApplicationDbContext context, ILogger<ProductSearchService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ProductSearchResult> SearchRelevantProductsAsync(string query)
        {
            try
            {
                _logger.LogInformation("Recherche produits pour: {Query}", query);

                var allProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.StockQuantity > 0) // Uniquement en stock
                    .ToListAsync();

                if (!allProducts.Any())
                {
                    _logger.LogWarning("Aucun produit en stock trouvé");
                    return new ProductSearchResult { Products = new List<Product>(), Context = "Aucun produit disponible" };
                }

                // Filtrer selon la requête
                var relevantProducts = FilterProductsByQuery(allProducts, query);

                // Construire le contexte pour le LLM
                var context = BuildProductContext(relevantProducts, query);

                return new ProductSearchResult
                {
                    Products = relevantProducts,
                    Context = context,
                    FoundResults = relevantProducts.Any()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche produits");
                return new ProductSearchResult
                {
                    Products = new List<Product>(),
                    Context = "Erreur lors de la recherche",
                    FoundResults = false
                };
            }
        }

        // New helper to fetch products by ids (used to include local cart context)
        public async Task<List<Product>> GetProductsByIdsAsync(IEnumerable<int> ids)
        {
            if (ids == null) return new List<Product>();
            var idList = ids.Distinct().ToList();
            if (!idList.Any()) return new List<Product>();

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => idList.Contains(p.ProductId))
                .ToListAsync();

            return products;
        }

        private List<Product> FilterProductsByQuery(List<Product> products, string query)
        {
            var lowerQuery = query.ToLower();
            var results = new List<Product>();

            // Dictionnaire de mots-clés par type de bijou
            var jewelryKeywords = new Dictionary<string, string[]>
            {
                ["bague"] = new[] { "bague", "alliance", "anneau", "chevalière", "solitaire" },
                ["collier"] = new[] { "collier", "pendentif", "sautoir", "ras-de-cou", "médaillon" },
                ["bracelet"] = new[] { "bracelet", "gourmette", "manchette", "chaîne" },
                ["boucle"] = new[] { "boucle", "clou", "creole", "pompon" },
                ["bijou"] = new[] { "bijou", "joyau", "parure", "ornement" }
            };

            // Matériaux
            var materialKeywords = new Dictionary<string, string[]>
            {
                ["or"] = new[] { "or", "gold", "carat" },
                ["argent"] = new[] { "argent", "silver", "argenté" },
                ["diamant"] = new[] { "diamant", "diamond", "brillant" },
                ["pierre"] = new[] { "pierre", "gemme", "saphir", "rubis", "émeraude", "topaze" }
            };

            foreach (var product in products)
            {
                var productText = $"{product.Name} {product.Description} {product.Category?.Name}".ToLower();
                var score = 0;

                // 1. Points pour correspondance exacte
                if (product.Name.ToLower().Contains(lowerQuery)) score += 10;

                // 2. Points pour type de bijou
                foreach (var jewelryType in jewelryKeywords)
                {
                    if (lowerQuery.Contains(jewelryType.Key))
                    {
                        if (jewelryType.Value.Any(keyword => productText.Contains(keyword)))
                            score += 8;
                    }
                }

                // 3. Points pour matériau
                foreach (var material in materialKeywords)
                {
                    if (lowerQuery.Contains(material.Key))
                    {
                        if (material.Value.Any(keyword => productText.Contains(keyword)))
                            score += 6;
                    }
                }

                // 4. Points pour budget
                if (lowerQuery.Contains("pas cher") || lowerQuery.Contains("moins de"))
                {
                    if (product.Price < 200) score += 5;
                }
                else if (lowerQuery.Contains("budget") && product.Price < 500)
                {
                    score += 3;
                }

                // 5. Points pour produits en vedette
                if (product.IsFeatured) score += 2;

                // 6. Points pour correspondance partielle
                var queryWords = lowerQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in queryWords)
                {
                    if (productText.Contains(word) && word.Length > 3)
                        score += 1;
                }

                if (score > 0)
                {
                    results.Add(product);
                }
            }

            return results.OrderByDescending(p => p.Price).Take(5).ToList();
        }

        private string BuildProductContext(List<Product> products, string originalQuery)
        {
            if (!products.Any())
            {
                return $"Aucun produit trouvé pour: '{originalQuery}'. Réponds de manière générale sur nos bijoux.";
            }

            var context = new System.Text.StringBuilder();
            context.AppendLine($"📊 J'ai trouvé {products.Count} produits pertinents :");
            context.AppendLine();

            foreach (var product in products.Take(3))
            {
                context.AppendLine($"💎 **{product.Name}**");
                context.AppendLine($"   💰 Prix : {product.Price}€");
                context.AppendLine($"   📦 Stock : {product.StockQuantity} unités disponibles");
                if (!string.IsNullOrEmpty(product.Category?.Name))
                    context.AppendLine($"   🏷️ Catégorie : {product.Category.Name}");
                if (!string.IsNullOrEmpty(product.Description))
                {
                    var shortDesc = product.Description.Length > 80
                        ? product.Description.Substring(0, 80) + "..."
                        : product.Description;
                    context.AppendLine($"   📝 {shortDesc}");
                }
                context.AppendLine();
            }

            context.AppendLine("💡 Conseils :");
            context.AppendLine("- Recommande les produits ci-dessus si pertinents");
            context.AppendLine("- Mentionne les prix EXACTS indiqués");
            context.AppendLine("- Propose de visiter la fiche produit pour plus de détails");

            return context.ToString();
        }
    }

    public class ProductSearchResult
    {
        public List<Product> Products { get; set; }
        public string Context { get; set; }
        public bool FoundResults { get; set; }
    }
}