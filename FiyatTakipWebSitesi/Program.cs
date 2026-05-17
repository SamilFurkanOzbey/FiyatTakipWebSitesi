using FiyatTakipWebSitesi.Components;
using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Jobs;
using FiyatTakipWebSitesi.Services;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Razor / Blazor ────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── API Controllers & OpenAPI ───────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options =>
{
    // JWT Bearer Auth için OpenAPI Ayarı
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "ŞAM API";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });
});

// ── Temel Servisler (Cache & Exception) ───────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddExceptionHandler<FiyatTakipWebSitesi.Filters.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // IP başına genel limit: dakikada 100 istek
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
            
    // Scraping gibi maliyetli API uç noktaları için: dakikada 5 istek
    options.AddFixedWindowLimiter("ScrapingPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2;
        opt.AutoReplenishment = true;
    });
});

// ── Veritabanı ────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("'DefaultConnection' bağlantı dizesi bulunamadı.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ── Hangfire (SQLite kullandığımız için in-memory storage) ───────────────────
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseMemoryStorage());

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2; // Aynı anda max 2 job (Selenium bellek dostu)
});

// ── Repositories & Services ───────────────────────────────────────────────────
builder.Services.AddScoped(typeof(FiyatTakipWebSitesi.Repositories.IRepository<>), typeof(FiyatTakipWebSitesi.Repositories.Repository<>));
// SMTP yapılandırıldıysa gerçek mail gönder, değilse log'a yaz (geliştirici/CI)
if (!string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"]))
{
    builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
}
else
{
    builder.Services.AddSingleton<IEmailService, MockEmailService>();
}

// ── Uygulama servisleri ───────────────────────────────────────────────────────
builder.Services.AddScoped<ScraperService>();
builder.Services.AddScoped<UrunService>();
builder.Services.AddScoped<FiyatGecmisiService>();
builder.Services.AddScoped<KategoriService>();
builder.Services.AddScoped<KullaniciService>();
builder.Services.AddScoped<UyariService>();
builder.Services.AddScoped<OturumDurumu>();
builder.Services.AddScoped<TakipModeliService>();
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
// Uygulama her başladığında: bekleyen migration'ları uygular, kategorilerix
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

// Türkçe karakterleri ASCII'ye çevirip slug üretir (Hangfire recurring job ID için)
static string KategoriSlugUret(string ad) =>
    ad.ToLowerInvariant()
      .Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
      .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c")
      .Replace(" & ", "-").Replace(" ", "-");

// ── HTTP Pipeline ─────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(); // Global Exception Handler'ı tetikler
    app.UseHsts();
}
else
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("ŞAM API Reference");
        options.WithDefaultHttpClient(Scalar.AspNetCore.ScalarTarget.CSharp, Scalar.AspNetCore.ScalarClient.HttpClient);
        // Scalar JWT Bearer Authentication Setup
        options.AddServer(new Scalar.AspNetCore.ScalarServer("https://localhost:7164", "Local server"));
        options.HideModels();
    });
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRateLimiter(); // Yetkilendirmeden ve routing'den önce/sonra eklenebilir, middleware sırası önemli
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
// SUNUM modu: Otomatik schedule'lar devre dışı (Cron.Yearly — yılda bir).
// Hangfire dashboard'undan "Trigger now" ile elle çalıştırılabilir.
// Üretime alırken normal cron'a geri dönülebilir: "0 6 * * *" / "0 18 * * *".
RecurringJob.AddOrUpdate<FiyatGuncellemeJob>(
    recurringJobId: "fiyat-guncelle-gunluk-sabah",
    methodCall:     job => job.TumUrunlerGuncelleAsync(),
    cronExpression: Cron.Yearly,
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

RecurringJob.AddOrUpdate<FiyatGuncellemeJob>(
    recurringJobId: "fiyat-guncelle-gunluk-aksam",
    methodCall:     job => job.TumUrunlerGuncelleAsync(),
    cronExpression: Cron.Yearly,
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

// Eski saatlik job artık kullanılmıyor — Hangfire'dan da silinmesi gerekiyor.
RecurringJob.RemoveIfExists("fiyat-guncelle-saatlik");

// Her kategori için "manuel-only" recurring job kaydet.
// Cron.Yearly: yılda bir kez (Jan 1 00:00) — pratikte manuel-only.
// Dashboard'da görünür, "Trigger now" ile o kategori için scrape başlatılabilir.
// NOT: Bu blok UseHangfireDashboard sonrası çalışmalı — yoksa JobStorage henüz
// initialize edilmemiş olur ve InvalidOperationException atar.
await using (var katScope = app.Services.CreateAsyncScope())
{
    var db = katScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var kategoriler = await db.Kategoriler.ToListAsync();
    foreach (var kat in kategoriler)
    {
        var slug = KategoriSlugUret(kat.Ad);
        RecurringJob.AddOrUpdate<FiyatGuncellemeJob>(
            recurringJobId: $"kategori-{kat.Id}-{slug}",
            methodCall:     job => job.KategoriUrunlerGuncelleAsync(kat.Id),
            cronExpression: Cron.Yearly,
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
    }
}

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();


