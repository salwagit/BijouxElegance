using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BijouxElegance.Services;
using System.Text.Json;
using System.Threading.Tasks;

namespace BijouxElegance.Pages.Api.Cart
{
    public class AddModel : PageModel
    {
        private readonly CartService _cartService;

        public AddModel(CartService cartService)
        {
            _cartService = cartService;
        }

        public class AddRequest
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            AddRequest? request = null;
            try
            {
                request = await HttpContext.Request.ReadFromJsonAsync<AddRequest>();
            }
            catch { }

            if (request == null)
            {
                try
                {
                    using var sr = new System.IO.StreamReader(HttpContext.Request.Body);
                    var body = await sr.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        request = JsonSerializer.Deserialize<AddRequest>(body, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                }
                catch { }
            }

            if (request == null || request.ProductId <= 0 || request.Quantity <= 0)
            {
                return BadRequest(new { success = false, error = "Invalid request payload" });
            }

            var cartId = HttpContext.Session.GetString("CartId");
            if (string.IsNullOrEmpty(cartId))
            {
                cartId = _cartService.GetCartId();
                HttpContext.Session.SetString("CartId", cartId);
            }

            try
            {
                _cartService.AddToCart(cartId, request.ProductId, request.Quantity);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { success = false, error = "Failed to add to cart: " + ex.Message });
            }

            var count = 0;
            try { count = _cartService.GetCartCount(cartId); } catch { count = 0; }

            return new JsonResult(new { success = true, count });
        }
    }
}
