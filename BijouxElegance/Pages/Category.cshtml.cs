using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Data;
using Microsoft.EntityFrameworkCore;

namespace BijouxElegance.Pages
{
    public class CategoryModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public Models.Category Category { get; set; }

        public CategoryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (Category == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}