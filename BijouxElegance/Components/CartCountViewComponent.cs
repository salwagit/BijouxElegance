using Microsoft.AspNetCore.Mvc;
using BijouxElegance.Services;
using System.Security.Claims;

namespace BijouxElegance.ViewComponents
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

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return View(0);

            string cartId;

            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                cartId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
            else
            {
                cartId = httpContext.Session.GetString("CartId");
            }

            int count = 0;
            if (!string.IsNullOrEmpty(cartId))
            {
                count = _cartService.GetCartCount(cartId);
            }

            return View(count);
        }
    }
}