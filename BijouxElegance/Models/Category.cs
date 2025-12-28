using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BijouxElegance.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [Display(Name = "Nom")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Icône")]
        public string IconClass { get; set; } = "fas fa-gem";

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}