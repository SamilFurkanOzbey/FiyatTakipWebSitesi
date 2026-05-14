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
using Hangfire;

namespace FiyatTakipWebSitesi.Jobs;

public class FiyatGuncellemeJob(
    IServiceScopeFactory scopeFactory,
    ILogger<FiyatGuncellemeJob> logger)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<FiyatGuncellemeJob> _logger = logger;

    /// <summary>
    /// Tüm aktif ürünlerin fiyatlarını güncellemek için her bir ürün adına 
    /// ayrı bir Hangfire job'ı kuyruğa (enqueue) ekler.
    /// </summary>
    public async Task TumUrunlerGuncelleAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[FiyatGuncellemeJob] Periyodik fiyat güncelleme tetiklendi — {Zaman}", DateTime.Now);
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Aktif, URL'si olan tüm ürünlerin sadece ID'lerini çek
        var urunIdListesi = await db.Urunler
            .Where(u => u.Aktif && !string.IsNullOrEmpty(u.URL))
            .Select(u => u.Id)
            .ToListAsync();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[FiyatGuncellemeJob] {Adet} aktif ürün için tekil güncelleme görevleri kuyruğa ekleniyor.", urunIdListesi.Count);
        }

        foreach (var id in urunIdListesi)
        {
            BackgroundJob.Enqueue<FiyatGuncellemeJob>(job => job.TekUrunGuncelleAsync(id));
        }
    }

    /// <summary>
    /// Tek bir ürünün fiyatını ScraperService üzerinden çeker ve veritabanını günceller.
    /// Hangfire tarafından arka planda asenkron çalıştırılır.
    /// </summary>
    public async Task TekUrunGuncelleAsync(int urunId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var urunService = scope.ServiceProvider.GetRequiredService<UrunService>();
        var scraperService = scope.ServiceProvider.GetRequiredService<ScraperService>();

        var urun = await db.Urunler.FirstOrDefaultAsync(u => u.Id == urunId);
        
        if (urun == null || !urun.Aktif || string.IsNullOrEmpty(urun.URL))
        {
            _logger.LogWarning("[FiyatGuncellemeJob] Ürün #{Id} bulunamadı, inaktif veya URL'si boş. Görev iptal ediliyor.", urunId);
            return;
        }

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
                    _logger.LogWarning(
                        "[FiyatGuncellemeJob] Scraping hatası, tekrar deneniyor ({RetryCount}/3) — ürün #{Id}. Hata: {Mesaj}",
                        retryCount, urun.Id, exception.Message);
                });

            var detay = await retryPolicy.ExecuteAsync(async () => await scraperService.GetUrunDetayAsync(urun.URL));

            if (detay.FiyatSayi <= 0)
            {
                _logger.LogWarning(
                    "[FiyatGuncellemeJob] Fiyat alınamadı veya sıfır — ürün #{Id}: '{Ham}'",
                    urun.Id, detay.Fiyat);
                return;
            }
            
            decimal yeniFiyat = detay.FiyatSayi;

            await urunService.FiyatGuncelleAsync(urun.Id, yeniFiyat, stokVar: true);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "[FiyatGuncellemeJob] ✓ Ürün #{Id} güncellendi — Yeni fiyat: {Fiyat:N2} TL",
                    urun.Id, yeniFiyat);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[FiyatGuncellemeJob] ✗ Ürün #{Id} güncellenirken kritik hata: {Mesaj}",
                urun.Id, ex.Message);
            
            // Hangfire'ın job'ı retry yapabilmesi için hatayı fırlatıyoruz
            throw;
        }
    }
}
