using Microsoft.AspNetCore.Mvc;
using BijouxElegance.Data;
using Microsoft.EntityFrameworkCore;

namespace BijouxElegance.Components
{
    public class CategoriesMenuViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public CategoriesMenuViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var categories = await _context.Categories.ToListAsync();
            return View(categories);
        }
    }
}