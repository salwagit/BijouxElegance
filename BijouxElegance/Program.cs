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

// Ajouter les sessions
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
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

        // session cart id
        var cartId = http.Session.GetString("CartId");
        if (string.IsNullOrEmpty(cartId))
        {
            cartId = cartService.GetCartId();
            http.Session.SetString("CartId", cartId);
        }

        // Use CartService to add to cart; it will persist to DB and update cache
        cartService.AddToCart(cartId, productId, quantity);

        var count = cartService.GetCartCount(cartId);
        return Results.Json(new { success = true, count });
    }
    catch (Exception ex)
    {
        Console.WriteLine("/cart/add error: " + ex);
        http.Response.StatusCode = 500;
        return Results.Json(new { success = false, error = ex.Message });
    }
});

// Mapper les Razor Pages
app.MapRazorPages();

app.Run();