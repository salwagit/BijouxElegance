using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using BijouxElegance.Data;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Linq;

namespace BijouxElegance.Pages.Admin.Products
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        [BindProperty]
        public Models.Product Product { get; set; }

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        public CreateModel(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public IActionResult OnGet()
        {
            var categories = _context.Categories.OrderBy(c => c.CategoryId).ToList();
            ViewData["Categories"] = new SelectList(categories, "CategoryId", "Name");

            // set default category if any to avoid required validation failure when admin forgets to choose
            if (categories.Any())
            {
                Product ??= new Models.Product();
                if (Product.CategoryId == 0)
                {
                    Product.CategoryId = categories.First().CategoryId;
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("Create.OnPostAsync called");

            if (!ModelState.IsValid)
            {
                // Log model state errors
                var sb = new StringBuilder();
                sb.AppendLine("Model state invalid:");
                foreach (var kv in ModelState)
                {
                    foreach (var error in kv.Value.Errors)
                    {
                        sb.AppendLine($" - {kv.Key}: {error.ErrorMessage}");
                    }
                }
                Console.WriteLine(sb.ToString());

                TempData["ErrorMessage"] = "Données invalides dans le formulaire. Vérifiez les champs obligatoires.";
                ViewData["Categories"] = new SelectList(_context.Categories, "CategoryId", "Name");
                return Page();
            }

            try
            {
                Console.WriteLine($"Creating product: Name={Product?.Name}, Price={Product?.Price}, CategoryId={Product?.CategoryId}");

                // Handle image upload
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
                    Console.WriteLine("Saved image to: " + Product.ImageUrl);
                }

                _context.Products.Add(Product);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Produit créé avec succès.";
                Console.WriteLine("Product saved with id: " + Product.ProductId);
                return RedirectToPage("./Index");
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Create product failed: " + ex);
                TempData["ErrorMessage"] = "Erreur lors de la création du produit: " + ex.Message;
                ViewData["Categories"] = new SelectList(_context.Categories, "CategoryId", "Name");
                return Page();
            }
        }
    }
}