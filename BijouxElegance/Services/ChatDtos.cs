using System.Collections.Generic;

namespace BijouxElegance.Services
{
    public class LocalCartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class ChatRequestDto
    {
        public string UserMessage { get; set; } = string.Empty;
        public List<LocalCartItemDto> LocalCartItems { get; set; } = new();
    }

    public class ChatResponseDto
    {
        public string Reply { get; set; } = string.Empty;
        public List<ProductSummary>? Products { get; set; }
    }

    // ProductSummary intentionally does NOT include IDs or exact stock quantities
    public class ProductSummary
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        // StockStatus: "Disponible", "Bientôt saturé", "Non disponible"
        public string StockStatus { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
