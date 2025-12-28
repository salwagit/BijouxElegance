using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BijouxElegance.Models;

namespace BijouxElegance.Pages.Cart
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public List<CartItem> CartItems { get; set; } = new();
        public decimal Total { get; set; }

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        private string GetOrCreateCartId()
        {
            // Récupérer ou créer un CartId (basé sur session ou utilisateur)
            if (User.Identity?.IsAuthenticated == true)
            {
                // Pour les utilisateurs connectés, utiliser leur ID
                return User.FindFirstValue(ClaimTypes.NameIdentifier);
            }
            else
            {
                // Pour les utilisateurs non connectés, utiliser SessionId
                if (HttpContext.Session.GetString("CartId") == null)
                {
                    var cartId = Guid.NewGuid().ToString();
                    HttpContext.Session.SetString("CartId", cartId);
                }
                return HttpContext.Session.GetString("CartId");
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var cartId = GetOrCreateCartId();

            CartItems = await _context.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Category)
                .Where(ci => ci.CartId == cartId)
                .ToListAsync();

            Total = CartItems.Sum(ci => ci.Quantity * ci.Product.Price);

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int productId, int quantity)
        {
            var cartId = GetOrCreateCartId();

            if (quantity < 1)
            {
                return await OnPostRemoveAsync(productId);
            }

            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.ProductId == productId && ci.CartId == cartId);

            if (cartItem != null)
            {
                cartItem.Quantity = quantity;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveAsync(int productId)
        {
            var cartId = GetOrCreateCartId();
            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.ProductId == productId && ci.CartId == cartId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostClearAsync()
        {
            var cartId = GetOrCreateCartId();
            var userCartItems = await _context.CartItems
                .Where(ci => ci.CartId == cartId)
                .ToListAsync();

            if (userCartItems.Any())
            {
                _context.CartItems.RemoveRange(userCartItems);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddToCartAsync(int productId, int quantity = 1)
        {
            if (quantity < 1)
                quantity = 1;

            var cartId = GetOrCreateCartId();
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                TempData["Error"] = "Produit non trouvé.";
                return RedirectToPage("/Products/Index");
            }

            // Vérifier le stock
            if (quantity > product.StockQuantity)
            {
                TempData["Error"] = $"Quantité indisponible. Stock disponible: {product.StockQuantity}";
                return RedirectToPage("/Product/Details", new { id = productId });
            }

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.ProductId == productId && ci.CartId == cartId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                if (existingItem.Quantity > product.StockQuantity)
                {
                    existingItem.Quantity = product.StockQuantity;
                    TempData["Warning"] = "Quantité ajustée au stock maximum disponible.";
                }
            }
            else
            {
                var newItem = new CartItem
                {
                    ItemId = Guid.NewGuid().ToString(),
                    CartId = cartId,
                    ProductId = productId,
                    Quantity = quantity
                };
                _context.CartItems.Add(newItem);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{product.Name} ajouté au panier.";

            return RedirectToPage("/Product/Details", new { id = productId });
        }
    }
}