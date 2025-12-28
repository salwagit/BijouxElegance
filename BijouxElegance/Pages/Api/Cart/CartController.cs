using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BijouxElegance.Data;

namespace BijouxElegance.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetCartCount()
        {
            string cartId;

            if (User.Identity.IsAuthenticated)
            {
                cartId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }
            else
            {
                cartId = HttpContext.Session.GetString("CartId");
            }

            if (string.IsNullOrEmpty(cartId))
                return Ok(0);

            var count = await _context.CartItems
                .Where(ci => ci.CartId == cartId)
                .SumAsync(ci => ci.Quantity);

            return Ok(count);
        }

        [HttpPost("sync-local-cart")]
        public async Task<IActionResult> SyncLocalCart([FromBody] SyncCartRequest request)
        {
            if (string.IsNullOrEmpty(request.CartId))
                return BadRequest("CartId requis");

            foreach (var item in request.CartItems)
            {
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci =>
                        ci.ProductId == item.ProductId &&
                        ci.CartId == request.CartId);

                if (existingItem != null)
                {
                    existingItem.Quantity += item.Quantity;
                }
                else
                {
                    var newItem = new Models.CartItem
                    {
                        ItemId = Guid.NewGuid().ToString(),
                        CartId = request.CartId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity
                    };
                    _context.CartItems.Add(newItem);
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }

    public class SyncCartRequest
    {
        public string CartId { get; set; }
        public List<LocalCartItem> CartItems { get; set; }
    }

    public class LocalCartItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}