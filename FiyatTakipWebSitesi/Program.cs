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

/*
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var kategoriService = scope.ServiceProvider.GetRequiredService<KategoriService>();
    await kategoriService.SeedVarsayilanKategorilerAsync();
}

*/

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
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = app.Environment.IsDevelopment() 
        ? [] // Geliştirme ortamında herkese açık
        : [new FiyatTakipWebSitesi.Filters.HangfireAuthorizationFilter()] // Canlı ortamda kimlik doğrulaması gerektir
});

// ── Periyodik Job Kayıtları ───────────────────────────────────────────────────
RecurringJob.AddOrUpdate<FiyatGuncellemeJob>(
    recurringJobId: "fiyat-guncelle-saatlik",
    methodCall:     job => job.TumUrunlerGuncelleAsync(),
    cronExpression: Cron.Hourly,
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

// Ayrıca her gün 06:00'da tam güncelleme (saatlik ile aynı metod)
RecurringJob.AddOrUpdate<FiyatGuncellemeJob>(
    recurringJobId: "fiyat-guncelle-gunluk-sabah",
    methodCall:     job => job.TumUrunlerGuncelleAsync(),
    cronExpression: "0 6 * * *",
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

