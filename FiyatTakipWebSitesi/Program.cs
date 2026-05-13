using FiyatTakipWebSitesi.Components;
using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Jobs;
using FiyatTakipWebSitesi.Services;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Razor / Blazor ────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── API Controllers ───────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── Veritabanı ────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("'DefaultConnection' bağlantı dizesi bulunamadı.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ── Hangfire ─────────────────────────────────────────────────────────────────
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout       = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout  = TimeSpan.FromMinutes(5),
        QueuePollInterval           = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks           = true
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2; // Aynı anda max 2 job (Selenium bellek dostu)
});

// ── Uygulama servisleri ───────────────────────────────────────────────────────
builder.Services.AddScoped<ScraperService>();
builder.Services.AddScoped<UrunService>();
builder.Services.AddScoped<FiyatGecmisiService>();
builder.Services.AddScoped<KategoriService>();
builder.Services.AddScoped<KullaniciService>();
builder.Services.AddScoped<UyariService>();
builder.Services.AddHttpClient<ResimCacheService>();
builder.Services.AddTransient<FiyatGuncellemeJob>();


// ── Options Pattern ───────────────────────────────────────────────────────────
builder.Services.Configure<FiyatTakipWebSitesi.Models.JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<FiyatTakipWebSitesi.Models.JwtSettings>() ?? new();

// ── Authentication & Authorization ────────────────────────────────────────────
var jwtKey = jwtSettings.Key;
var issuer = jwtSettings.Issuer;
var audience = jwtSettings.Audience;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// ── Migration & Seed ──────────────────────────────────────────────────────────
// Uygulama her başladığında: bekleyen migration'ları uygular, kategorileri
// (eğer tablo boşsa) seed eder, sistem katalogunu (eğer boşsa) seed eder.
// Her iki seed metodu da idempotent — kayıt varsa hiçbir şey yapmaz.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var kategoriService = scope.ServiceProvider.GetRequiredService<KategoriService>();
    await kategoriService.SeedVarsayilanKategorilerAsync();

    var urunService = scope.ServiceProvider.GetRequiredService<UrunService>();
    await urunService.SeedKatalogAsync();
}

// ── HTTP Pipeline ─────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Hangfire Dashboard ────────────────────────────────────────────────────────
// /hangfire altındaki istekler için thread culture'ını en-US'a sabitler.
// Bu sayede dashboard arayüzü İngilizce render edilir; uygulamanın kalanı
// (Razor sayfaları, hata mesajları vs.) sistem dilinde kalmaya devam eder.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hangfire"))
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.CurrentCulture = culture;
        System.Globalization.CultureInfo.CurrentUICulture = culture;
    }
    await next();
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = app.Environment.IsDevelopment()
        ? [] // Geliştirme ortamında herkese açık
        : [new FiyatTakipWebSitesi.Filters.HangfireAuthorizationFilter()] // Canlı ortamda kimlik doğrulaması gerektir
});

// ── Periyodik Job Kayıtları ───────────────────────────────────────────────────
// Günde 2 kez: 06:00 (sabah) ve 18:00 (akşam). Selenium yükü ve anti-bot
// riskini düşük tutmak için saatlik tarama yapılmıyor — kullanıcı tek ürünü
// elle yenilemek isterse UrunService.TekUrunYenileAsync üzerinden tetikler.
RecurringJob.AddOrUpdate<FiyatGuncellemeJob>(
    recurringJobId: "fiyat-guncelle-gunluk-sabah",
    methodCall:     job => job.TumUrunlerGuncelleAsync(),
    cronExpression: "0 6 * * *",
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

RecurringJob.AddOrUpdate<FiyatGuncellemeJob>(
    recurringJobId: "fiyat-guncelle-gunluk-aksam",
    methodCall:     job => job.TumUrunlerGuncelleAsync(),
    cronExpression: "0 18 * * *",
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

// Eski saatlik job artık kullanılmıyor — Hangfire'dan da silinmesi gerekiyor.
RecurringJob.RemoveIfExists("fiyat-guncelle-saatlik");

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

