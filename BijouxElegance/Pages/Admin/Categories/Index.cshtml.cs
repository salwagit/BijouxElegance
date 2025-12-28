using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Data;
using Microsoft.EntityFrameworkCore;

namespace BijouxElegance.Pages.Admin.Categories
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public List<Models.Category> Categories { get; set; }

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public void OnGet()
        {
            Categories = _context.Categories.ToList();
        }
    }
}