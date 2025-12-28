using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Data;
using BijouxElegance.Services;
using Microsoft.EntityFrameworkCore;

namespace BijouxElegance.Pages
{
    public class ProductDetailModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly CartService _cartService;

        public Models.Product Product { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        public ProductDetailModel(ApplicationDbContext context, CartService cartService)
        {
            _context = context;
            _cartService = cartService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (Product == null)
            {
                return NotFound();
            }

            return Page();
        }

        public IActionResult OnPost(int id)
        {
            var cartId = HttpContext.Session.GetString("CartId");
            if (string.IsNullOrEmpty(cartId))
            {
                cartId = _cartService.GetCartId();
                HttpContext.Session.SetString("CartId", cartId);
            }

            _cartService.AddToCart(cartId, id, Quantity);

            TempData["Message"] = "Produit ajouté au panier !";
            return RedirectToPage(new { id });
        }
    }
}