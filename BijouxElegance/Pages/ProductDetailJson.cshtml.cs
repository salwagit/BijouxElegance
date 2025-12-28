using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Data;
using System.Linq;

namespace BijouxElegance.Pages
{
    public class ProductDetailJsonModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ProductDetailJsonModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet(int id)
        {
            var product = _context.Products
                .Where(p => p.ProductId == id)
                .Select(p => new
                {
                    productId = p.ProductId,
                    name = p.Name,
                    description = p.Description,
                    price = p.Price,
                    priceFormatted = p.Price.ToString("C"),
                    imageUrl = p.ImageUrl,
                    categoryName = p.Category != null ? p.Category.Name : string.Empty,
                    stock = p.StockQuantity
                })
                .FirstOrDefault();

            if (product == null) return NotFound();
            return new JsonResult(product);
        }
    }
}
