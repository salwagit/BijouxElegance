using System.Collections.Generic;
using BijouxElegance.Models;

namespace BijouxElegance.ViewModels
{
    public class CartViewModel
    {
        public List<CartItem> CartItems { get; set; } = new List<CartItem>();
        public decimal Total { get; set; }
    }
}