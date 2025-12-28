using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Data;
using BijouxElegance.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace BijouxElegance.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public HomePageViewModel ViewModel { get; set; } = new HomePageViewModel();
        public List<Models.Product> FeaturedProducts { get; set; } = new List<Models.Product>();

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            ViewModel = new HomePageViewModel
            {
                FeaturedProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsFeatured)
                    .Take(8)
                    .ToListAsync(),
                Categories = await _context.Categories.ToListAsync()
            };
        }
    }
}