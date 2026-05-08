// =====================================================
// FiyatGuncellemeJob.cs
// Bu sınıf, Hangfire tarafından periyodik olarak (saatlik
// veya günlük) tetiklenen arka plan işini tanımlar.
// Tüm aktif ürünlerin fiyatlarını ScraperService üzerinden
// çekerek UrunService aracılığıyla veritabanına kaydeder.
// Tek bir ürünün scraping hatası tüm işi durdurmaz;
// hatalar loglanıp diğer ürünlere devam edilir.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Services;
using Microsoft.EntityFrameworkCore;
using Polly;

namespace FiyatTakipWebSitesi.Jobs;

public class FiyatGuncellemeJob(
    IServiceScopeFactory scopeFactory,
    ILogger<FiyatGuncellemeJob> logger)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<FiyatGuncellemeJob> _logger = logger;

    /// <summary>
    /// Tüm aktif ürünlerin fiyatlarını sırasıyla scrape eder ve
    /// veritabanını günceller. Hangfire tarafından saatlik/günlük
    /// olarak tetiklenir.
    /// </summary>
    public async Task TumUrunlerGuncelleAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[FiyatGuncellemeJob] Periyodik fiyat güncelleme başladı — {Zaman}", DateTime.Now);
        }

        // Her job çağrısında yeni bir DI scope aç
        // (Scoped servisler: DbContext, UrunService, ScraperService)
        await using var scope = _scopeFactory.CreateAsyncScope();

        var db            = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var urunService   = scope.ServiceProvider.GetRequiredService<UrunService>();
        var scraperService = scope.ServiceProvider.GetRequiredService<ScraperService>();

        // Aktif, URL'si olan tüm ürünleri çek
        var urunler = await db.Urunler
            .Where(u => u.Aktif && !string.IsNullOrEmpty(u.URL))
            .ToListAsync();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[FiyatGuncellemeJob] {Adet} aktif ürün bulundu.", urunler.Count);
        }

        int basarili = 0, hatali = 0;

        scraperService.InitSharedDriver();
        try
        {
            foreach (var urun in urunler)
            {
                try
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("[FiyatGuncellemeJob] Scraping başladı — ürün #{Id}: {Ad}", urun.Id, urun.Ad);
                    }

                    var retryPolicy = Policy
                        .Handle<Exception>()
                        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                            (exception, timeSpan, retryCount, context) =>
                            {
                                _logger.LogWarning("[FiyatGuncellemeJob] Ürün #{Id} için scraping başarısız. {Delay}s sonra tekrar denenecek. Deneme: {RetryCount}. Hata: {Message}", 
                                    urun.Id, timeSpan.TotalSeconds, retryCount, exception.Message);
                            });

                    var fiyatMetin = await retryPolicy.ExecuteAsync(() => scraperService.GetPriceAsync(urun.URL));

                    // "25.649,05 TL" → decimal parse
                    var temiz = fiyatMetin
                        .Replace("TL", "")
                        .Replace(".", "")
                        .Replace(",", ".")
                        .Trim();

                    if (!decimal.TryParse(temiz,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out decimal yeniFiyat))
                    {
                        _logger.LogWarning(
                            "[FiyatGuncellemeJob] Fiyat parse edilemedi — ürün #{Id}: '{Ham}'",
                            urun.Id, fiyatMetin);
                        hatali++;
                        continue;
                    }

                    await urunService.FiyatGuncelleAsync(urun.Id, yeniFiyat, stokVar: true);

                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation(
                            "[FiyatGuncellemeJob] ✓ Ürün #{Id} güncellendi — Yeni fiyat: {Fiyat:N2} TL",
                            urun.Id, yeniFiyat);
                    }
                    basarili++;
                }
                catch (Exception ex)
                {
                    // Tek ürün hatası diğer ürünleri engellemez
                    _logger.LogError(ex,
                        "[FiyatGuncellemeJob] ✗ Ürün #{Id} güncellenirken hata: {Mesaj}",
                        urun.Id, ex.Message);
                    hatali++;
                }
            }
        }
        finally
        {
            scraperService.CloseSharedDriver();
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[FiyatGuncellemeJob] Tamamlandı — Başarılı: {Basarili}, Hatalı: {Hatali}, Toplam: {Toplam}",
                basarili, hatali, urunler.Count);
        }
    }
}
