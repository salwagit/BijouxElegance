using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BijouxElegance.Models
{
    public class CartItem
    {
        [Key]
        public string ItemId { get; set; } = string.Empty;
        public string CartId { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}