using BijouxElegance.Data;
using BijouxElegance.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Ajouter les services
builder.Services.AddRazorPages();

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
        if (request?.Items == null || !request.Items.Any())
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

// Mapper les Razor Pages
app.MapRazorPages();

app.Run();

// Modèles pour les requêtes API
public class SyncLocalCartRequest
{
    public List<LocalCartItemDTO> Items { get; set; } = new();
}

// Note: LocalCartItemDTO est maintenant défini dans CartService.cs