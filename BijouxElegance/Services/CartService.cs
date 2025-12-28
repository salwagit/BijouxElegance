using BijouxElegance.Data;
using BijouxElegance.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace BijouxElegance.Services
{
    public class CartService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartService(ApplicationDbContext context, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
        }

        // Génère un nouvel ID de panier
        public string GetCartId()
        {
            return Guid.NewGuid().ToString();
        }

        // Récupère ou crée un CartId pour l'utilisateur/session actuel
        public string GetOrCreateCartId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return GetCartId();

            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                // Pour les utilisateurs connectés, utiliser leur ID
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return userId ?? GetCartId();
            }
            else
            {
                // Pour les utilisateurs non connectés, utiliser la session
                var cartId = httpContext.Session.GetString("CartId");
                if (string.IsNullOrEmpty(cartId))
                {
                    cartId = GetCartId();
                    httpContext.Session.SetString("CartId", cartId);
                }
                return cartId;
            }
        }

        // Méthode principale pour ajouter au panier
        public void AddToCart(string cartId, int productId, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;

            var product = _context.Products.Find(productId);
            if (product == null)
                throw new ArgumentException("Produit non trouvé");

            if (quantity > product.StockQuantity)
                throw new InvalidOperationException($"Stock insuffisant. Disponible: {product.StockQuantity}");

            // Pour les utilisateurs connectés ou avec session
            AddToDatabaseCart(cartId, productId, quantity);
        }

        private void AddToDatabaseCart(string cartId, int productId, int quantity)
        {
            var existingItem = _context.CartItems
                .FirstOrDefault(ci => ci.ProductId == productId && ci.CartId == cartId);
            
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var cartItem = new CartItem
                {
                    ItemId = Guid.NewGuid().ToString(),
                    CartId = cartId,
                    ProductId = productId,
                    Quantity = quantity
                };
                _context.CartItems.Add(cartItem);
            }

            _context.SaveChanges();
            _cache.Remove($"cart_count_{cartId}");
            _cache.Remove($"cart_items_{cartId}");
        }

        // Synchroniser localStorage vers base de données
        public void SyncLocalStorageToDatabase(string cartId, List<LocalCartItemDTO> localItems)
        {
            foreach (var item in localItems)
            {
                var existingItem = _context.CartItems
                    .FirstOrDefault(ci => ci.ProductId == item.ProductId && ci.CartId == cartId);

                if (existingItem != null)
                {
                    existingItem.Quantity += item.Quantity;
                }
                else
                {
                    var cartItem = new CartItem
                    {
                        ItemId = Guid.NewGuid().ToString(),
                        CartId = cartId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity
                    };
                    _context.CartItems.Add(cartItem);
                }
            }

            _context.SaveChanges();
            _cache.Remove($"cart_count_{cartId}");
            _cache.Remove($"cart_items_{cartId}");
        }

        // Obtenir le nombre d'articles
        public int GetCartCount(string cartId)
        {
            if (string.IsNullOrEmpty(cartId))
                return 0;

            return _cache.GetOrCreate($"cart_count_{cartId}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return _context.CartItems
                    .Where(ci => ci.CartId == cartId)
                    .Sum(ci => (int?)ci.Quantity) ?? 0;
            });
        }

        // Récupérer les articles du panier
        public List<CartItem> GetCartItems(string cartId)
        {
            if (string.IsNullOrEmpty(cartId))
                return new List<CartItem>();

            return _cache.GetOrCreate($"cart_items_{cartId}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return _context.CartItems
                    .Include(ci => ci.Product)
                        .ThenInclude(p => p.Category)
                    .Where(ci => ci.CartId == cartId)
                    .OrderByDescending(ci => ci.ItemId)
                    .ToList();
            }) ?? new List<CartItem>();
        }

        // Obtenir le total du panier
        public decimal GetCartTotal(string cartId)
        {
            var items = GetCartItems(cartId);
            return items.Sum(ci => ci.Quantity * ci.Product.Price);
        }

        // Mettre à jour la quantité
        public void UpdateCartItem(string cartId, int productId, int quantity)
        {
            if (quantity < 1)
            {
                RemoveFromCart(cartId, productId);
                return;
            }

            var product = _context.Products.Find(productId);
            if (product == null)
                throw new ArgumentException("Produit non trouvé");

            if (quantity > product.StockQuantity)
                throw new InvalidOperationException($"Quantité supérieure au stock disponible ({product.StockQuantity})");

            var cartItem = _context.CartItems
                .FirstOrDefault(ci => ci.ProductId == productId && ci.CartId == cartId);

            if (cartItem != null)
            {
                cartItem.Quantity = quantity;
                _context.SaveChanges();
                _cache.Remove($"cart_count_{cartId}");
                _cache.Remove($"cart_items_{cartId}");
            }
        }

        // Supprimer un article
        public void RemoveFromCart(string cartId, int productId)
        {
            var cartItem = _context.CartItems
                .FirstOrDefault(ci => ci.ProductId == productId && ci.CartId == cartId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                _context.SaveChanges();
                _cache.Remove($"cart_count_{cartId}");
                _cache.Remove($"cart_items_{cartId}");
            }
        }

        // Vider le panier
        public void ClearCart(string cartId)
        {
            var items = _context.CartItems
                .Where(ci => ci.CartId == cartId)
                .ToList();

            if (items.Any())
            {
                _context.CartItems.RemoveRange(items);
                _context.SaveChanges();
                _cache.Remove($"cart_count_{cartId}");
                _cache.Remove($"cart_items_{cartId}");
            }
        }
    }

    // DTO pour les articles du localStorage
    public class LocalCartItemDTO
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}