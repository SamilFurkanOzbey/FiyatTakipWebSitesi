// =====================================================
// FiyatGuncellemeJob.cs
// Bu sınıf, Hangfire tarafından periyodik olarak (06:00 ve 18:00)
// tetiklenen arka plan işini tanımlar.
// Aktif ürünlerin fiyatlarını ScraperService üzerinden PARALEL olarak
// (concurrency = MaxParalelScrape) çekerek UrunService aracılığıyla
// veritabanına kaydeder.
// İki giriş noktası:
//   • TumUrunlerGuncelleAsync()           → tüm aktif ürünler (otomatik schedule)
//   • KategoriUrunlerGuncelleAsync(id)    → sadece bir kategoride (manuel test)
// Tek bir ürünün scraping hatası tüm işi durdurmaz.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Models;
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

    // Aynı anda kaç ürün paralel scrape edilsin. ScraperService içindeki
    // SemaphoreSlim ile uyumlu olmalı.
    private const int MaxParalelScrape = 3;

    /// <summary>
    /// Tüm aktif ürünlerin fiyatlarını paralel olarak günceller.
    /// Hangfire 06:00 ve 18:00 schedule'larıyla otomatik tetiklenir.
    /// </summary>
    public async Task TumUrunlerGuncelleAsync()
    {
        List<Urun> urunler;
        await using (var listeScope = _scopeFactory.CreateAsyncScope())
        {
            var db = listeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            urunler = await db.Urunler
                .Where(u => u.Aktif && !string.IsNullOrEmpty(u.URL))
                .ToListAsync();
        }

        await UrunleriGuncelleAsync(urunler, etiket: "TÜM");
    }

    /// <summary>
    /// Sadece belirli bir kategorideki aktif ürünleri günceller.
    /// Hangfire dashboard'unda "Trigger now" ile manuel tetiklenir
    /// (otomatik schedule yok — Program.cs'te Cron.Yearly ile kayıtlı).
    /// </summary>
    public async Task KategoriUrunlerGuncelleAsync(int kategoriId)
    {
        List<Urun> urunler;
        string kategoriAdi = $"#{kategoriId}";
        await using (var listeScope = _scopeFactory.CreateAsyncScope())
        {
            var db = listeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            urunler = await db.Urunler
                .Where(u => u.Aktif && u.KategoriId == kategoriId && !string.IsNullOrEmpty(u.URL))
                .ToListAsync();

            kategoriAdi = await db.Kategoriler
                .Where(k => k.Id == kategoriId)
                .Select(k => k.Ad)
                .FirstOrDefaultAsync() ?? kategoriAdi;
        }

        await UrunleriGuncelleAsync(urunler, etiket: kategoriAdi);
    }

    /// <summary>
    /// Verilen ürün listesini paralel olarak scrape eder ve günceller.
    /// Her iş parçacığı için ayrı DI scope açılır (DbContext thread-safe değil).
    /// </summary>
    private async Task UrunleriGuncelleAsync(List<Urun> urunler, string etiket)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[FiyatGuncellemeJob] [{Etiket}] Başladı — {Adet} ürün, paralel: {Paralel}",
                etiket, urunler.Count, MaxParalelScrape);
        }

        if (urunler.Count == 0) return;

        // Thread-safe sayaçlar
        int basarili = 0, hatali = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxParalelScrape,
        };

        await Parallel.ForEachAsync(urunler, parallelOptions, async (urun, ct) =>
        {
            // HER ITERATION KENDİ DI SCOPE'UNU AÇAR (DbContext thread-safe değil)
            await using var iterScope = _scopeFactory.CreateAsyncScope();
            var urunService = iterScope.ServiceProvider.GetRequiredService<UrunService>();
            var scraperService = iterScope.ServiceProvider.GetRequiredService<ScraperService>();

            try
            {
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
                    Interlocked.Increment(ref hatali);
                    return;
                }

                await urunService.OtomatikGuncelleAsync(urun.Id, detay);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "[FiyatGuncellemeJob] ✓ Ürün #{Id} güncellendi — Yeni fiyat: {Fiyat:N2} TL",
                        urun.Id, detay.FiyatSayi);
                }
                Interlocked.Increment(ref basarili);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[FiyatGuncellemeJob] ✗ Ürün #{Id} güncellenirken hata: {Mesaj}",
                    urun.Id, ex.Message);
                Interlocked.Increment(ref hatali);
            }
        });

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[FiyatGuncellemeJob] [{Etiket}] Tamamlandı — Başarılı: {Basarili}, Hatalı: {Hatali}, Toplam: {Toplam}",
                etiket, basarili, hatali, urunler.Count);
        }
    }
}
