using Microsoft.AspNetCore.Mvc;
using BijouxElegance.Services;

namespace BijouxElegance.Controllers
{
    [ApiController]
    [Route("api/groq")]
    public class GroqChatController : ControllerBase
    {
        private readonly SimpleChatService _chatService;

        public GroqChatController(SimpleChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatApiRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return Ok(new
                {
                    success = true,
                    reply = "Je suis là 🤍 Que puis-je faire pour vous ?",
                    products = new List<object>()
                });
            }

            var result = await _chatService.HandleAsync(
                request.Message,
                request.LocalCart
            );

            return Ok(new
            {
                success = true,
                reply = result.Reply,
                products = result.Products
            });
        }
    }

    public class ChatApiRequest
    {
        public string Message { get; set; }
        public List<LocalCartItem>? LocalCart { get; set; }
    }
}