using FiyatTakipWebSitesi.Components;
using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Razor / Blazor ────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Veritabanı ────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("'DefaultConnection' bağlantı dizesi bulunamadı.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ── Uygulama servisleri ───────────────────────────────────────────────────────
builder.Services.AddScoped<ScraperService>();
builder.Services.AddScoped<UrunService>();
builder.Services.AddScoped<FiyatGecmisiService>();
builder.Services.AddScoped<KategoriService>();

var app = builder.Build();

// ── Migration & Seed ──────────────────────────────────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var kategoriService = scope.ServiceProvider.GetRequiredService<KategoriService>();
    await kategoriService.SeedVarsayilanKategorilerAsync();
}

// ── HTTP Pipeline ─────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

