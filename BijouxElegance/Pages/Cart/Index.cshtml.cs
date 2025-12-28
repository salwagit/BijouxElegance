using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Services;
using BijouxElegance.ViewModels;
using System.Linq;
using System.Collections.Generic;
using BijouxElegance.Models;
using Microsoft.AspNetCore.Http;

namespace BijouxElegance.Pages.Cart
{
    public class IndexModel : PageModel
    {
        private readonly CartService _cartService;

        [BindProperty]
        public int ProductId { get; set; }

        [BindProperty]
        public int Quantity { get; set; }

        public CartViewModel ViewModel { get; set; } = new CartViewModel();

        public List<CartItem> CartItems => ViewModel.CartItems;

        public decimal Total => ViewModel.Total;

        public IndexModel(CartService cartService)
        {
            _cartService = cartService;
        }

        public void OnGet()
        {
            var cartId = GetCartId();
            var items = _cartService.GetCartItems(cartId);

            ViewModel.CartItems = items;
            ViewModel.Total = _cart_service_total_fallback(cartId);
        }

        public IActionResult OnPostUpdate()
        {
            var cartId = GetCartId();
            _cartService.UpdateQuantity(cartId, ProductId, Quantity);
            return RedirectToPage();
        }

        public IActionResult OnPostRemove()
        {
            var cartId = GetCartId();
            _cartService.RemoveFromCart(cartId, ProductId);
            return RedirectToPage();
        }

        private string GetCartId()
        {
            var cartId = HttpContext.Session.GetString("CartId");
            if (string.IsNullOrEmpty(cartId))
            {
                cartId = _cartService.GetCartId();
                HttpContext.Session.SetString("CartId", cartId);
            }
            return cartId!;
        }

        private decimal _cart_service_total_fallback(string cartId)
        {
            try { return _cartService.GetTotal(cartId); }
            catch { return 0; }
        }
    }
}
