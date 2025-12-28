using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Data;

namespace BijouxElegance.Pages.Admin.Categories
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public Models.Category Category { get; set; }

        public DeleteModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Category = _context.Categories.Find(id);

            if (Category == null)
            {
                return NotFound();
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            var category = _context.Categories.Find(Category.CategoryId);
            if (category != null)
            {
                // Supprimer aussi les produits de cette catégorie
                var products = _context.Products.Where(p => p.CategoryId == Category.CategoryId);
                _context.Products.RemoveRange(products);

                _context.Categories.Remove(category);
                _context.SaveChanges();
            }

            return RedirectToPage("./Index");
        }
    }
}