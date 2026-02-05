using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BijouxElegance.Data;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BijouxElegance.Services
{
    public class ProductsIndexerHostedService : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ProductsIndexerHostedService> _logger;
        private readonly IndexingOptions _options;

        public ProductsIndexerHostedService(IServiceProvider services, ILogger<ProductsIndexerHostedService> logger, IOptions<IndexingOptions> options)
        {
            _services = services;
            _logger = logger;
            _options = options.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting product indexing...");

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var vector = scope.ServiceProvider.GetRequiredService<IVectorDbClient>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProductsIndexerHostedService>>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var indexName = config.GetValue<string>("Pinecone:IndexName") ?? "products-index";

                // Récupérer uniquement les produits qui n'ont pas encore été indexés
                var products = db.Products
                    .ToList();

                _logger.LogInformation($"Found {products.Count} products to index");

                if (products.Count == 0)
                {
                    _logger.LogInformation("No products need indexing.");
                    return;
                }

                int batchSize = _options.BatchSize;
                int delayBetweenBatches = _options.DelayBetweenBatches;
                int delayBetweenRequests = _options.DelayBetweenRequests;

                int processed = 0;
                int failed = 0;

                for (int i = 0; i < products.Count; i += batchSize)
                {
                    var batch = products.Skip(i).Take(batchSize).ToList();

                    _logger.LogInformation($"Processing batch {i / batchSize + 1}/{(products.Count + batchSize - 1) / batchSize} ({batch.Count} products)");

                    foreach (var product in batch)
                    {
                        try
                        {
                            await Task.Delay(delayBetweenRequests, cancellationToken); // Délai entre chaque requête

                            var text = (product.Name + "\n\n" + product.Description).Trim();

                            _logger.LogDebug($"Generating embedding for product {product.ProductId}: {product.Name}");

                            var embedding = await EmbeddingsHelper.CreateEmbeddingAsync(text, scope.ServiceProvider);

                            // Nous n'écrivons pas d'embedding dans la table Products ici (modèle actuel ne contient pas de colonne Embedding).
                            // L'embedding est uniquement envoyé au Vector DB (Pinecone) via Upsert.

                            var metadata = new Dictionary<string, object>
                            {
                                { "productId", product.ProductId },
                                { "name", product.Name },
                                { "price", product.Price },
                                { "category", product.CategoryId },
                                { "stock", product.StockQuantity },
                                { "isFeatured", product.IsFeatured }
                            };

                            await vector.UpsertAsync(indexName, product.ProductId.ToString(), embedding, metadata);

                            processed++;
                            _logger.LogInformation($"Successfully indexed product {product.ProductId}: {product.Name}");
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            logger.LogError(ex, "Indexing failed for product {ProductId}", product.ProductId);
                        }
                    }

                    // Sauvegarder les modifications après chaque batch
                    await db.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation($"Batch completed. Processed: {processed}, Failed: {failed}");

                    // Délai entre les batches (sauf pour le dernier batch)
                    if (i + batchSize < products.Count)
                    {
                        _logger.LogInformation($"Waiting {delayBetweenBatches / 1000} seconds before next batch...");
                        await Task.Delay(delayBetweenBatches, cancellationToken);
                    }
                }

                _logger.LogInformation($"Product indexing finished. Total: {products.Count}, Success: {processed}, Failed: {failed}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during product indexing");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}