using BijouxElegance.Data;
using BijouxElegance.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Ajouter les services
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();

// Register GroqService as typed HTTP client
builder.Services.AddScoped<SimpleChatService>();

builder.Services.AddControllers();

// Configuration de la base de données
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = "Server=(localdb)\\mssqllocaldb;Database=BijouxEleganceDB;Trusted_Connection=True;MultipleActiveResultSets=true";
    Console.WriteLine("No DefaultConnection found in configuration, using fallback LocalDB connection.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Ajouter les sessions
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".BijouxElegance.Session";
});

// Ajouter les services personnalisés
builder.Services.AddScoped<CartService>();

var app = builder.Build();

// Apply pending migrations at startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
        Console.WriteLine("Database migrated successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Database migration failed: " + ex.Message);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

// Minimal API endpoint for AJAX add-to-cart
app.MapPost("/cart/add", async (HttpContext http, CartService cartService) =>
{
    try
    {
        var dict = await http.Request.ReadFromJsonAsync<Dictionary<string, int>>();
        if (dict == null)
            return Results.BadRequest(new { success = false, error = "Invalid payload" });

        dict.TryGetValue("productId", out var productId);
        dict.TryGetValue("quantity", out var quantity);

        if (productId <= 0 || quantity <= 0)
            return Results.BadRequest(new { success = false, error = "Invalid productId or quantity" });

        var cartId = http.Session.GetString("CartId");
        if (string.IsNullOrEmpty(cartId))
        {
            cartId = cartService.GetCartId();
            http.Session.SetString("CartId", cartId);
        }

        cartService.AddToCart(cartId, productId, quantity);
        var count = cartService.GetCartCount(cartId);
        return Results.Json(new { success = true, count });
    }
    catch (Exception ex)
    {
        Console.WriteLine("/cart/add error: " + ex);
        return Results.Json(new { success = false, error = ex.Message });
    }
});

// API pour récupérer le compteur du panier
app.MapGet("/cart/count", (HttpContext http, CartService cartService) =>
{
    try
    {
        var cartId = http.Session.GetString("CartId");
        if (string.IsNullOrEmpty(cartId))
            return Results.Json(new { count = 0 });

        var count = cartService.GetCartCount(cartId);
        return Results.Json(new { count });
    }
    catch (Exception ex)
    {
        Console.WriteLine("/cart/count error: " + ex);
        return Results.Json(new { count = 0 });
    }
});

// API pour synchroniser le panier localStorage
app.MapPost("/cart/sync-local", async (HttpContext http, CartService cartService) =>
{
    try
    {
        var request = await http.Request.ReadFromJsonAsync<SyncLocalCartRequest>();
        if (request?.Items == null || !request.Items.Any())
            return Results.BadRequest(new { success = false, error = "Panier vide" });

        var cartId = http.Session.GetString("CartId");
        if (string.IsNullOrEmpty(cartId))
        {
            cartId = Guid.NewGuid().ToString();
            http.Session.SetString("CartId", cartId);
        }

        cartService.SyncLocalStorageToDatabase(cartId, request.Items);

        return Results.Json(new
        {
            success = true,
            message = "Panier synchronisé",
            cartId
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine("/cart/sync-local error: " + ex);
        return Results.Json(new { success = false, error = ex.Message });
    }
});

// API pour les informations produit
app.MapGet("/api/products/{id}/cart-info", async (int id, ApplicationDbContext context) =>
{
    try
    {
        var product = await context.Products
            .Include(p => p.Category)
            .Where(p => p.ProductId == id)
            .Select(p => new
            {
                p.ProductId,
                p.Name,
                p.Description,
                p.Price,
                p.ImageUrl,
                p.StockQuantity,
                CategoryName = p.Category.Name,
                PriceFormatted = p.Price.ToString("C")
            })
            .FirstOrDefaultAsync();

        if (product == null)
            return Results.Json(new { error = "Produit non trouvé" }, statusCode: 404);

        return Results.Json(product);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
});

// Minimal chatbot endpoint - returns simple canned replies (demo only)
app.MapPost("/chat", async (HttpContext http) =>
{
    try
    {
        var req = await http.Request.ReadFromJsonAsync<SimpleChatRequest>();
        if (req == null || string.IsNullOrWhiteSpace(req.Message))
            return Results.BadRequest(new { error = "Message vide" });

        var userMsg = req.Message.Trim().ToLowerInvariant();
        string reply;

        if (userMsg.Contains("bonjour") || userMsg.Contains("salut"))
            reply = "Bonjour ! Comment puis-je vous aider aujourd'hui ?";
        else if (userMsg.Contains("livraison"))
            reply = "Les délais de livraison sont généralement de 3 à 5 jours ouvrés.";
        else if (userMsg.Contains("retour") || userMsg.Contains("remboursement"))
            reply = "Vous pouvez retourner un produit sous 14 jours. Consultez notre politique de retour pour plus de détails.";
        else if (userMsg.Contains("prix") || userMsg.Contains("coûte"))
            reply = "Les prix sont indiqués sur la fiche produit. Si vous avez une question sur un produit précis, envoyez son nom.";
        else
            reply = "Je suis un chatbot d'exemple. Essayez des mots-clés : 'livraison', 'retour', 'prix'.";

        return Results.Json(new { reply });
    }
    catch (Exception ex)
    {
        Console.WriteLine("/chat error: " + ex);
        return Results.Json(new { error = ex.Message });
    }
});

// Version plus simple
app.MapPost("/api/groq/chat", async (HttpContext http, SimpleChatService chatService) =>
{
    try
    {
        var request = await http.Request.ReadFromJsonAsync<GroqChatRequest>();
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Message requis" });

        Console.WriteLine($"?? Chat request: {request.Message}");

        var response = await chatService.ProcessMessageAsync(
            request.Message,
             request.LocalCart
        );
     
        Console.WriteLine($"? Chat response: {response.Source} en {response.ResponseTimeMs}ms");

        // Créer l'objet de réponse directement
        var result = new
        {
            success = response.Success,
            reply = response.Message,
            products = response.Products?.Select(p => new
            {
                id = p.ProductId,
                name = p.Name,
                price = p.Price,
                stock = p.StockQuantity,
                image = p.ImageUrl,
                category = p.Category?.Name
            }).ToArray() ?? Array.Empty<object>(),  // Utiliser ToArray() au lieu de ToList()
            hasProducts = response.HasProducts,
            responseTimeMs = response.ResponseTimeMs,
            source = response.Source
        };

        return Results.Json(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? /api/groq/chat error: {ex}");
        return Results.Json(new
        {
            success = false,
            reply = "Désolé, le service de chat est temporairement indisponible. Contactez-nous au 01 23 45 67 89.",
            error = ex.Message,
            source = "error"
        }, statusCode: 500);
    }
});

// Health check
app.MapGet("/api/groq/health", () =>
{
    return Results.Json(new
    {
        status = "healthy",
        service = "Groq Chat API",
        timestamp = DateTime.UtcNow
    });
});

app.MapRazorPages();
app.Run();

// Modèles pour les requêtes API
public class SyncLocalCartRequest
{
    public List<LocalCartItemDTO> Items { get; set; } = new();
}

public class SimpleChatRequest
{
    public string Message { get; set; } = string.Empty;
}

public class GroqChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public List<BijouxElegance.Services.LocalCartItemDTO> LocalCart { get; set; } = new();
}



// DTO for local cart (ajoute dans un fichier séparé normalement)
