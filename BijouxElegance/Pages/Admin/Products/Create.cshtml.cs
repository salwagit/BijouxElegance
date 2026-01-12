using BijouxElegance.Data;
using BijouxElegance.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BijouxElegance.Pages.Admin.Products
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        [BindProperty]
        public CreateProductViewModel ProductVM { get; set; }

        [BindProperty]
        public IFormFile ImageFile { get; set; }

        public SelectList Categories { get; set; }

        // ViewModel pour le formulaire
        public class CreateProductViewModel
        {
            [Required(ErrorMessage = "Le nom est obligatoire")]
            public string Name { get; set; }

            [Required(ErrorMessage = "La catégorie est obligatoire")]
            [Range(1, int.MaxValue, ErrorMessage = "Veuillez sélectionner une catégorie")]
            public int CategoryId { get; set; }

            public string Description { get; set; }

            [Required(ErrorMessage = "Le prix est obligatoire")]
            [Range(0.01, double.MaxValue, ErrorMessage = "Le prix doit être supérieur à 0")]
            public decimal Price { get; set; }

            public decimal? OldPrice { get; set; }

            [Required(ErrorMessage = "La quantité est obligatoire")]
            [Range(0, int.MaxValue, ErrorMessage = "La quantité ne peut être négative")]
            public int StockQuantity { get; set; }

            public bool IsFeatured { get; set; }
        }

        public CreateModel(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadCategoriesAsync();

            // Initialiser le ViewModel
            ProductVM = new CreateProductViewModel
            {
                StockQuantity = 0,
                Price = 0,
                IsFeatured = false
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                Console.WriteLine("=== DÉBUT OnPostAsync ===");

                // Charger les catégories
                await LoadCategoriesAsync();

                // Debug : Voir ce qui est reçu dans le formulaire
                Console.WriteLine($"Form data received:");
                Console.WriteLine($"- Name: {Request.Form["ProductVM.Name"]}");
                Console.WriteLine($"- CategoryId: {Request.Form["ProductVM.CategoryId"]}");
                Console.WriteLine($"- Price: {Request.Form["ProductVM.Price"]}");
                Console.WriteLine($"- StockQuantity: {Request.Form["ProductVM.StockQuantity"]}");

                // Vérifier manuellement la valeur de CategoryId
                if (string.IsNullOrEmpty(Request.Form["ProductVM.CategoryId"]) ||
                    !int.TryParse(Request.Form["ProductVM.CategoryId"], out int catId) ||
                    catId <= 0)
                {
                    Console.WriteLine("ERROR: CategoryId is invalid or not selected");
                    ModelState.AddModelError("ProductVM.CategoryId", "Veuillez sélectionner une catégorie");
                }
                else
                {
                    // Vérifier que la catégorie existe
                    var categoryExists = await _context.Categories.AnyAsync(c => c.CategoryId == catId);
                    if (!categoryExists)
                    {
                        ModelState.AddModelError("ProductVM.CategoryId", "La catégorie sélectionnée n'existe pas");
                    }
                }

                if (!ModelState.IsValid)
                {
                    // Log des erreurs
                    Console.WriteLine("ModelState invalid:");
                    foreach (var key in ModelState.Keys)
                    {
                        var errors = ModelState[key].Errors;
                        if (errors.Count > 0)
                        {
                            Console.WriteLine($"- {key}: {string.Join(", ", errors.Select(e => e.ErrorMessage))}");
                        }
                    }

                    TempData["ErrorMessage"] = "Veuillez corriger les erreurs dans le formulaire.";
                    return Page();
                }

                // Gestion de l'image
                string imageUrl = "/images/default-product.png";

                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                    var extension = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(extension))
                    {
                        ModelState.AddModelError("ImageFile", "Format d'image non supporté");
                        return Page();
                    }

                    if (ImageFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ImageFile", "L'image est trop volumineuse (max 5MB)");
                        return Page();
                    }

                    var imagesFolder = Path.Combine(_env.WebRootPath, "images");
                    if (!Directory.Exists(imagesFolder))
                    {
                        Directory.CreateDirectory(imagesFolder);
                    }

                    var fileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(imagesFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(stream);
                    }

                    imageUrl = $"/images/{fileName}";
                    Console.WriteLine($"Image saved: {imageUrl}");
                }

                // Créer l'objet Product à partir du ViewModel
                var product = new Product
                {
                    Name = ProductVM.Name,
                    CategoryId = ProductVM.CategoryId,
                    Description = ProductVM.Description,
                    Price = ProductVM.Price,
                    OldPrice = ProductVM.OldPrice,
                    StockQuantity = ProductVM.StockQuantity,
                    ImageUrl = imageUrl,
                    IsFeatured = ProductVM.IsFeatured,
                };

                // Ajouter à la base de données
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Produit créé avec ID: {product.ProductId}");
                TempData["SuccessMessage"] = $"Le produit '{product.Name}' a été créé avec succès.";

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERREUR CRITIQUE: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                TempData["ErrorMessage"] = $"Une erreur est survenue: {ex.Message}";
                return Page();
            }
        }

        private async Task LoadCategoriesAsync()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            Categories = new SelectList(categories, "CategoryId", "Name");
            ViewData["Categories"] = Categories;
        }
    }
}