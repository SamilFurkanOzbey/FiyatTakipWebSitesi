using Hangfire;
using Hangfire.MemoryStorage; // NuGet'ten kurduğumuz paket
using Microsoft.EntityFrameworkCore;
using FiyatTakipWebSitesi.Data; // ApplicationDbContext için
using FiyatTakipWebSitesi.Services; // KategoriService için
using FiyatTakipWebSitesi.Components; // App bileşeni için

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

// 1. ADIM: VERİTABANI AYARI (SQLite Geçişi)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=FiyatTakip.db")); // LocalDB yerine SQLite dosyası kullanılır

// 2. ADIM: HANGFIRE AYARI (MemoryStorage Geçişi)
builder.Services.AddHangfire(config =>
    config.UseMemoryStorage()); // SQL Hatası almamak için işlemleri bellekte tutar

builder.Services.AddHangfireServer();

// Diğer servislerin eklenmesi
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<KategoriService>();

var app = builder.Build();

// 3. ADIM: VERİTABANI OTOMATİK OLUŞTURMA VE SEED
// Bu kısım uygulama her açıldığında veritabanını ve kategorileri kontrol eder

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // SQLite dosyasını ve tablolarını oluşturur/günceller
    await db.Database.MigrateAsync();

    var kategoriService = scope.ServiceProvider.GetRequiredService<KategoriService>();
    // Başlangıç kategorilerini (Elektronik, Moda vb.) ekler
    await kategoriService.SeedVarsayilanKategorilerAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Hangfire Dashboard (İsteğe bağlı, periyodik işleri görmek için)
app.MapHangfireDashboard();

app.Run();