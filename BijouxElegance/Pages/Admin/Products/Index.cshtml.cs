using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Data;
using Microsoft.EntityFrameworkCore;

namespace BijouxElegance.Pages.Admin.Products
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public List<Models.Product> Products { get; set; }

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public void OnGet()
        {
            Products = _context.Products
                .Include(p => p.Category)
                .ToList();
        }
    }
}