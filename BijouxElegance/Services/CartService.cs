using System;
using System.Collections.Generic;
using System.Linq;
using BijouxElegance.Data;
using BijouxElegance.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace BijouxElegance.Services
{
    public class CartService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;

        public CartService(ApplicationDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public string GetCartId()
        {
            return Guid.NewGuid().ToString();
        }

        public void AddToCart(string cartId, int productId, int quantity)
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

            // Persist to DB cart items to support long-term storage
            var cartItem = _context.CartItems
                .FirstOrDefault(c => c.CartId == cartId && c.ProductId == productId);

            if (cartItem == null)
            {
                cartItem = new CartItem
                {
                    ProductId = productId,
                    CartId = cartId,
                    Quantity = quantity,
                    ItemId = Guid.NewGuid().ToString()
                };
                _context.CartItems.Add(cartItem);
            }
            else
            {
                cartItem.Quantity += quantity;
            }

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                // wrap to provide clearer diagnostics
                throw new InvalidOperationException("Failed to save cart changes to the database.", ex);
            }

            // Also update cache to keep session cart in sync
            try
            {
                var cacheKey = $"cart:{cartId}";
                var existing = _cache.GetString(cacheKey);
                List<CacheEntry>? list = null;
                if (!string.IsNullOrEmpty(existing))
                {
                    try { list = JsonSerializer.Deserialize<List<CacheEntry>>(existing); }
                    catch { list = null; }
                }
                if (list == null) list = new List<CacheEntry>();

                var entry = list.FirstOrDefault(i => i.ProductId == productId);
                if (entry == null)
                {
                    var product = _context.Products.Find(productId);
                    list.Add(new CacheEntry { ProductId = productId, Quantity = quantity, PriceCents = (int)(product?.Price * 100 ?? 0), Product = product });
                }
                else
                {
                    entry.Quantity += quantity;
                }

                var serialized = JsonSerializer.Serialize(list);
                _cache.SetString(cacheKey, serialized, new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(30) });
            }
            catch
            {
                // ignore cache failures
            }
        }

        public List<CartItem> GetCartItems(string cartId)
        {
            // Prefer DB-stored cart items
            var dbItems = _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.CartId == cartId)
                .ToList();

            if (dbItems != null && dbItems.Count > 0)
            {
                // Ensure navigation properties are not null
                foreach (var ci in dbItems)
                {
                    if (ci.Product == null)
                    {
                        var prod = _context.Products.Find(ci.ProductId);
                        ci.Product = prod ?? new Product { ProductId = ci.ProductId, Name = "Produit inconnu", Description = "", Price = 0m, ImageUrl = "/images/placeholder.png", StockQuantity = 0, CategoryId = 0 };
                    }
                }

                return dbItems;
            }

            // Fallback to cache
            try
            {
                var cacheKey = $"cart:{cartId}";
                var existing = _cache.GetString(cacheKey);
                if (string.IsNullOrEmpty(existing)) return new List<CartItem>();

                List<CacheEntry>? list = null;
                try { list = JsonSerializer.Deserialize<List<CacheEntry>>(existing); }
                catch { list = null; }
                if (list == null) return new List<CartItem>();

                var result = new List<CartItem>();
                foreach (var e in list)
                {
                    var prod = e.Product;
                    if (prod == null)
                    {
                        prod = _context.Products.Find(e.ProductId);
                    }

                    // fallback placeholder to avoid null reference
                    if (prod == null)
                    {
                        prod = new Product
                        {
                            ProductId = e.ProductId,
                            Name = "Produit inconnu",
                            Description = string.Empty,
                            Price = 0m,
                            ImageUrl = "/images/placeholder.png",
                            StockQuantity = 0,
                            CategoryId = 0
                        };
                    }

                    result.Add(new CartItem
                    {
                        ItemId = Guid.NewGuid().ToString(),
                        CartId = cartId,
                        ProductId = e.ProductId,
                        Quantity = e.Quantity,
                        Product = prod
                    });
                }

                return result;
            }
            catch
            {
                return new List<CartItem>();
            }
        }

        public void RemoveFromCart(string cartId, int productId)
        {
            var cartItem = _context.CartItems
                .FirstOrDefault(c => c.CartId == cartId && c.ProductId == productId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                _context.SaveChanges();
            }

            // update cache
            try
            {
                var cacheKey = $"cart:{cartId}";
                var existing = _cache.GetString(cacheKey);
                if (string.IsNullOrEmpty(existing)) return;
                var list = JsonSerializer.Deserialize<List<CacheEntry>>(existing) ?? new List<CacheEntry>();
                list.RemoveAll(i => i.ProductId == productId);
                _cache.SetString(cacheKey, JsonSerializer.Serialize(list), new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(30) });
            }
            catch { }
        }

        public void UpdateQuantity(string cartId, int productId, int quantity)
        {
            var cartItem = _context.CartItems
                .FirstOrDefault(c => c.CartId == cartId && c.ProductId == productId);

            if (cartItem != null)
            {
                cartItem.Quantity = quantity;
                _context.SaveChanges();
            }

            try
            {
                var cacheKey = $"cart:{cartId}";
                var existing = _cache.GetString(cacheKey);
                if (string.IsNullOrEmpty(existing)) return;
                var list = JsonSerializer.Deserialize<List<CacheEntry>>(existing) ?? new List<CacheEntry>();
                var entry = list.FirstOrDefault(i => i.ProductId == productId);
                if (entry != null)
                {
                    entry.Quantity = quantity;
                    _cache.SetString(cacheKey, JsonSerializer.Serialize(list), new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(30) });
                }
            }
            catch { }
        }

        public decimal GetTotal(string cartId)
        {
            var items = GetCartItems(cartId);
            return items.Sum(c => c.Quantity * (c.Product?.Price ?? 0));
        }

        public int GetCartCount(string cartId)
        {
            var dbCount = _context.CartItems
                .Where(c => c.CartId == cartId)
                .Sum(c => (int?)c.Quantity) ?? 0;

            if (dbCount > 0) return dbCount;

            try
            {
                var cacheKey = $"cart:{cartId}";
                var existing = _cache.GetString(cacheKey);
                if (string.IsNullOrEmpty(existing)) return 0;
                var list = JsonSerializer.Deserialize<List<CacheEntry>>(existing) ?? new List<CacheEntry>();
                return list.Sum(i => i.Quantity);
            }
            catch
            {
                return 0;
            }
        }

        public void ClearCart(string cartId)
        {
            var cartItems = _context.CartItems
                .Where(c => c.CartId == cartId);

            _context.CartItems.RemoveRange(cartItems);
            _context.SaveChanges();

            try
            {
                var cacheKey = $"cart:{cartId}";
                _cache.Remove(cacheKey);
            }
            catch { }
        }

        private class CacheEntry
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public int PriceCents { get; set; }
            public Product? Product { get; set; }
        }
    }
}