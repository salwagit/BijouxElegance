using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using BijouxElegance.Data;
using System.Collections.Generic;
using System.Linq;
using BijouxElegance.Models;

namespace BijouxElegance.Pages.Cart
{
    public class AddModel : PageModel
    {
        private readonly CartService _cartService;
        private readonly IWebHostEnvironment _env;
        private readonly IDistributedCache _cache;
        private readonly ApplicationDbContext _context;

        public AddModel(CartService cartService, IWebHostEnvironment env, IDistributedCache cache, ApplicationDbContext context)
        {
            _cartService = cartService;
            _env = env;
            _cache = cache;
            _context = context;
        }

        public class AddRequest
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        private class CachedCartItem
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public Product Product { get; set; } = new Product();
        }

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            AddRequest? request = null;

            try
            {
                request = await HttpContext.Request.ReadFromJsonAsync<AddRequest>();
            }
            catch { }

            if (request == null && HttpContext.Request.HasFormContentType)
            {
                try
                {
                    var form = await HttpContext.Request.ReadFormAsync();
                    var pidStr = form["productId"].FirstOrDefault() ?? form["ProductId"].FirstOrDefault();
                    var qtyStr = form["quantity"].FirstOrDefault() ?? form["Quantity"].FirstOrDefault();
                    if (int.TryParse(pidStr, out var pid) && int.TryParse(qtyStr, out var qty))
                    {
                        request = new AddRequest { ProductId = pid, Quantity = qty };
                    }
                }
                catch { }
            }

            if (request == null)
            {
                try
                {
                    HttpContext.Request.Body.Position = 0;
                }
                catch { }

                try
                {
                    using var sr = new System.IO.StreamReader(HttpContext.Request.Body);
                    var body = await sr.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        try
                        {
                            request = JsonSerializer.Deserialize<AddRequest>(body, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        }
                        catch { }
                    }
                }
                catch { }
            }

            if (request == null || request.ProductId <= 0 || request.Quantity <= 0)
            {
                return BadRequest(new { success = false, error = "Invalid request payload" });
            }

            // Validate product exists and get product snapshot
            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                return BadRequest(new { success = false, error = $"Product with id {request.ProductId} not found" });
            }

            // Get or create cartId in session
            var cartId = HttpContext.Session.GetString("CartId");
            if (string.IsNullOrEmpty(cartId))
            {
                cartId = System.Guid.NewGuid().ToString();
                HttpContext.Session.SetString("CartId", cartId);
            }

            var cacheKey = $"cart:{cartId}";
            List<CachedCartItem> items;
            try
            {
                var existing = await _cache.GetStringAsync(cacheKey);
                if (string.IsNullOrEmpty(existing))
                {
                    items = new List<CachedCartItem>();
                }
                else
                {
                    items = JsonSerializer.Deserialize<List<CachedCartItem>>(existing) ?? new List<CachedCartItem>();
                }

                var existingItem = items.FirstOrDefault(i => i.ProductId == request.ProductId);
                if (existingItem == null)
                {
                    // create product snapshot
                    var snapshot = new Product
                    {
                        ProductId = product.ProductId,
                        Name = product.Name,
                        Description = product.Description,
                        Price = product.Price,
                        OldPrice = product.OldPrice,
                        ImageUrl = product.ImageUrl,
                        StockQuantity = product.StockQuantity,
                        CategoryId = product.CategoryId,
                        IsFeatured = product.IsFeatured
                    };

                    items.Add(new CachedCartItem
                    {
                        ProductId = request.ProductId,
                        Quantity = request.Quantity,
                        Product = snapshot
                    });
                }
                else
                {
                    existingItem.Quantity += request.Quantity;
                }

                var serialized = JsonSerializer.Serialize(items);
                // set cache with sliding expiration (e.g., 30 days)
                var options = new DistributedCacheEntryOptions
                {
                    SlidingExpiration = System.TimeSpan.FromDays(30)
                };
                await _cache.SetStringAsync(cacheKey, serialized, options);

                var count = items.Sum(i => i.Quantity);
                return new JsonResult(new { success = true, count });
            }
            catch (System.Exception ex)
            {
                // return more info in development
                if (_env.IsDevelopment())
                {
                    return StatusCode(500, new { success = false, error = ex.ToString() });
                }
                return StatusCode(500, new { success = false, error = "Failed to add to cart" });
            }
        }
    }
}
