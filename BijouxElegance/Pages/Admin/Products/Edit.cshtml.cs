using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using BijouxElegance.Data;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;

namespace BijouxElegance.Pages.Admin.Products
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        [BindProperty]
        public Models.Product Product { get; set; }

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        public EditModel(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public IActionResult OnGet(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Product = _context.Products.Find(id);

            if (Product == null)
            {
                return NotFound();
            }

            ViewData["Categories"] = new SelectList(_context.Categories, "CategoryId", "Name");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ViewData["Categories"] = new SelectList(_context.Categories, "CategoryId", "Name");
                return Page();
            }

            // Handle image upload if a new file was provided
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "images");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                var fileName = Path.GetRandomFileName() + Path.GetExtension(ImageFile.FileName);
                var filePath = Path.Combine(uploads, fileName);
                using (var stream = System.IO.File.Create(filePath))
                {
                    await ImageFile.CopyToAsync(stream);
                }

                Product.ImageUrl = "/images/" + fileName;
            }

            _context.Products.Update(Product);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}