using BijouxElegance.Data;
using BijouxElegance.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using FuzzySharp;

var builder = WebApplication.CreateBuilder(args);

// Ajouter les services
builder.Services.AddRazorPages();

// Add HttpClient for external AI calls
builder.Services.AddHttpClient();

// Register OllamaService with base address
builder.Services.AddHttpClient<OllamaService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OLLAMA_URL"] ?? "http://localhost:11434");
});

// Determine connection string with fallback
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = "Server=(localdb)\\mssqllocaldb;Database=BijouxEleganceDB;Trusted_Connection=True;MultipleActiveResultSets=true";
    Console.WriteLine("No DefaultConnection found in configuration, using fallback LocalDB connection.");
}

// Configurer la base de données
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Ajouter distributed cache (in-memory for demo; replace with Redis in production)
builder.Services.AddDistributedMemoryCache();

// Ajouter memory cache
builder.Services.AddMemoryCache();

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
builder.Services.AddHttpContextAccessor();

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

// NOTE: Warm-up and caching removed as requested to restore original behavior

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

// Utiliser les sessions
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

        // Récupérer ou créer cartId
        var cartId = http.Session.GetString("CartId");
        if (string.IsNullOrEmpty(cartId))
        {
            cartId = cartService.GetCartId();
            http.Session.SetString("CartId", cartId);
        }

        // Ajouter au panier
        cartService.AddToCart(cartId, productId, quantity);

        var count = cartService.GetCartCount(cartId);
        return Results.Json(new { success = true, count });
    }
    catch (Exception ex)
    {
        Console.WriteLine("/cart/add error: " + ex);
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
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
        if( request?.Items == null || !request.Items.Any())
            return Results.BadRequest(new { success = false, error = "Panier vide" });

        // Récupérer ou créer cartId
        var cartId = http.Session.GetString("CartId");
        if (string.IsNullOrEmpty(cartId))
        {
            cartId = Guid.NewGuid().ToString();
            http.Session.SetString("CartId", cartId);
        }

        // Synchroniser vers la base de données
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
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
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
        var req = await http.Request.ReadFromJsonAsync<ChatRequest>();
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
        else if (userMsg.Contains("prix") || userMsg.Contains("coûte") || userMsg.Contains("prix"))
            reply = "Les prix sont indiqués sur la fiche produit. Si vous avez une question sur un produit précis, envoyez son nom.";
        else
            reply = "Je suis un chatbot d'exemple. Essayez des mots-clés : 'livraison', 'retour', 'prix'.";

        return Results.Json(new { reply });
    }
    catch (Exception ex)
    {
        Console.WriteLine("/chat error: " + ex);
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
});

// AI chatbot using Ollama local model + fuzzy search in DB
app.MapPost("/chat/ai-ollama", async (HttpContext http, OllamaService ollama, ApplicationDbContext db, IConfiguration config) =>
{
    try
    {
        var req = await http.Request.ReadFromJsonAsync<ChatRequest>();
        if (req == null || string.IsNullOrWhiteSpace(req.Message))
            return Results.BadRequest(new { error = "Message vide" });

        var userMessage = req.Message.Trim();

        // Load products from DB (names + description)
        var products = await db.Products.AsNoTracking()
            .Select(p => new { p.ProductId, p.Name, p.Description, p.Price, p.StockQuantity })
            .ToListAsync();

        // Compute fuzzy scores and pick top matches
        var scored = products
            .Select(p => new { Product = p, Score = Fuzz.TokenSetRatio(userMessage, (p.Name ?? string.Empty) + " " + (p.Description ?? string.Empty)) })
            .OrderByDescending(x => x.Score)
            .Take(5)
            .Where(x => x.Score >= 45)
            .ToList();

        var contextText = scored.Any()
            ? string.Join("\n\n", scored.Select(s => $"{s.Product.Name} (ID:{s.Product.ProductId}) - {s.Product.Price:C} - stock:{s.Product.StockQuantity}\n{s.Product.Description}"))
            : "";

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("You are a helpful assistant for Bijoux Elegance. Use the product facts below to answer user questions. Only use the facts provided and do not hallucinate.");
        if (!string.IsNullOrEmpty(contextText))
        {
            promptBuilder.AppendLine("\nProduct facts:\n");
            promptBuilder.AppendLine(contextText);
        }
        promptBuilder.AppendLine("\nUser question:\n");
        promptBuilder.AppendLine(userMessage);
        promptBuilder.AppendLine("\nAnswer in French, be concise and give relevant product information when available.");

        var prompt = promptBuilder.ToString();

        // Use OllamaService to get response
        var generated = await ollama.AskAsync(prompt);

        return Results.Json(new { reply = generated, contextCount = scored.Count });
    }
    catch (Exception ex)
    {
        Console.WriteLine("/chat/ai-ollama error: " + ex);
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
});

// Mapper les Razor Pages
app.MapRazorPages();

app.Run();

// Modèles pour les requêtes API
public class SyncLocalCartRequest
{
    public List<LocalCartItemDTO> Items { get; set; } = new();
}

// Simple DTO for chatbot request
public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}

// Note: LocalCartItemDTO est maintenant défini dans CartService.cs