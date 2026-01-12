using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BijouxElegance.Data;
using BijouxElegance.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace BijouxElegance.Pages.Admin.Products
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        [BindProperty]
        public EditProductViewModel ProductVM { get; set; }

        [BindProperty]
        public IFormFile ImageFile { get; set; }

        public SelectList Categories { get; set; }

        // ViewModel pour l'édition (identique à Create mais avec ProductId)
        public class EditProductViewModel
        {
            public int ProductId { get; set; }

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

            public string CurrentImageUrl { get; set; }
        }

        public EditModel(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            await LoadCategoriesAsync();

            // Remplir le ViewModel avec les données du produit
            ProductVM = new EditProductViewModel
            {
                ProductId = product.ProductId,
                Name = product.Name,
                CategoryId = product.CategoryId,
                Description = product.Description,
                Price = product.Price,
                OldPrice = product.OldPrice,
                StockQuantity = product.StockQuantity,
                IsFeatured = product.IsFeatured,
                CurrentImageUrl = product.ImageUrl
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                Console.WriteLine("=== DÉBUT OnPostAsync EDIT ===");

                // Charger les catégories
                await LoadCategoriesAsync();

                // Debug : Voir ce qui est reçu dans le formulaire
                Console.WriteLine($"Form data received:");
                Console.WriteLine($"- ProductId: {ProductVM.ProductId}");
                Console.WriteLine($"- Name: {Request.Form["ProductVM.Name"]}");
                Console.WriteLine($"- CategoryId: {Request.Form["ProductVM.CategoryId"]}");

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

                // Récupérer le produit existant
                var existingProduct = await _context.Products.FindAsync(ProductVM.ProductId);
                if (existingProduct == null)
                {
                    TempData["ErrorMessage"] = "Produit non trouvé.";
                    return NotFound();
                }

                // Gestion de l'image
                string imageUrl = ProductVM.CurrentImageUrl; // Conserver l'image actuelle par défaut

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
                    Console.WriteLine($"Nouvelle image sauvegardée: {imageUrl}");

                    // Optionnel: Supprimer l'ancienne image si ce n'est pas l'image par défaut
                    if (!string.IsNullOrEmpty(ProductVM.CurrentImageUrl) &&
                        !ProductVM.CurrentImageUrl.Contains("default-product"))
                    {
                        var oldImagePath = Path.Combine(_env.WebRootPath, ProductVM.CurrentImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                            Console.WriteLine($"Ancienne image supprimée: {oldImagePath}");
                        }
                    }
                }

                // Mettre à jour les propriétés du produit
                existingProduct.Name = ProductVM.Name;
                existingProduct.CategoryId = ProductVM.CategoryId;
                existingProduct.Description = ProductVM.Description;
                existingProduct.Price = ProductVM.Price;
                existingProduct.OldPrice = ProductVM.OldPrice;
                existingProduct.StockQuantity = ProductVM.StockQuantity;
                existingProduct.IsFeatured = ProductVM.IsFeatured;
                existingProduct.ImageUrl = imageUrl;

                await _context.SaveChangesAsync();

                Console.WriteLine($"Produit mis à jour avec ID: {existingProduct.ProductId}");
                TempData["SuccessMessage"] = $"Le produit '{existingProduct.Name}' a été mis à jour avec succès.";

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