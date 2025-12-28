using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BijouxElegance.Models
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        [Display(Name = "Nom")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Prix")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Display(Name = "Ancien prix")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? OldPrice { get; set; }

        [Required]
        [Display(Name = "Catégorie")]
        public int CategoryId { get; set; }

        [Display(Name = "Image")]
        public string ImageUrl { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Quantité en stock")]
        public int StockQuantity { get; set; }

        [Display(Name = "Produit vedette")]
        public bool IsFeatured { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;
    }
}