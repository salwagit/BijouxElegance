using Microsoft.AspNetCore.Mvc;
using BijouxElegance.Services;

namespace BijouxElegance.Components
{
    public class CartCountViewComponent : ViewComponent
    {
        private readonly CartService _cartService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartCountViewComponent(CartService cartService, IHttpContextAccessor httpContextAccessor)
        {
            _cartService = cartService;
            _httpContextAccessor = httpContextAccessor;
        }

        public IViewComponentResult Invoke()
        {
            var cartId = _httpContextAccessor.HttpContext.Session.GetString("CartId");
            if (string.IsNullOrEmpty(cartId))
            {
                return Content("0");
            }

            var count = _cartService.GetCartCount(cartId);
            return Content(count.ToString());
        }
    }
}