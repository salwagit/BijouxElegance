using BijouxElegance.Data;
using BijouxElegance.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Ajouter les services
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();

// HttpClient for OpenAI (no Polly configured)
builder.Services.AddHttpClient("OpenAI");

builder.Services.AddHttpClient();

// register services
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<ChatAiService>();

// Vector DB and indexer
builder.Services.AddSingleton<IVectorDbClient, PineconeClient>();
builder.Services.AddHostedService<ProductsIndexerHostedService>();

// Mock Embeddings service for development
builder.Services.AddSingleton<MockEmbeddingsService>();

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

// Add options
builder.Services.Configure<IndexingOptions>(builder.Configuration.GetSection("Indexing"));

// Ajouter les sessions
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".BijouxElegance.Session";
});

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

// Groq healthcheck
using (var scope = app.Services.CreateScope())
{
    try
    {
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("GroqHealth");

        var groqKey = config.GetValue<string>("Groq:ApiKey");
        var model = config.GetValue<string>("Groq:Model");
        if (!string.IsNullOrEmpty(groqKey) && !string.IsNullOrEmpty(model))
        {
            var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", groqKey);
            client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");

            try
            {
                var resp = await client.GetAsync($"models/{model}");
                if (resp.IsSuccessStatusCode)
                {
                    logger.LogInformation("Groq model {Model} is available.", model);
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    logger.LogWarning("Groq model check returned {Status}: {Body}", (int)resp.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Groq model healthcheck failed");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Groq healthcheck failed: " + ex.Message);
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

// Chat endpoint
app.MapPost("/api/chat/ask", async (ChatRequestDto req, ChatAiService chat) =>
{
    if (req == null || string.IsNullOrWhiteSpace(req.UserMessage))
        return Results.BadRequest(new { error = "Message requis" });

    var resp = await chat.GetAiResponseAsync(req);
    return Results.Json(resp);
});

// Admin endpoint to reindex on demand
app.MapPost("/admin/index-products", async (IServiceProvider services) =>
{
    using var scope = services.CreateScope();
    var indexer = scope.ServiceProvider.GetRequiredService<ProductsIndexerHostedService>();
    await indexer.StartAsync(CancellationToken.None);
    return Results.Ok(new { success = true });
});

app.MapRazorPages();
app.Run();

// Modèles pour les requêtes API
public class SyncLocalCartRequest
{
    // Use the DTO defined inside CartService (LocalCartItemDTO)
    public List<BijouxElegance.Services.LocalCartItemDTO> Items { get; set; } = new();
}

// End of file
