// =====================================================
// FiyatGecmisiService.cs
// Bu servis, ürünlere ait fiyat geçmişi verilerini
// veritabanından okumak ve analiz etmek için kullanılır.
// Belirli bir ürünün tüm fiyat kayıtlarını, tarih
// aralığına göre fiyatları, minimum/maksimum fiyatları,
// son N güne ait ortalama fiyatı ve rekor düşük fiyat
// kontrolünü asenkron olarak sunar.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;

namespace FiyatTakipWebSitesi.Services;

public class FiyatGecmisiService(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    /// <summary>Bir ürünün tüm fiyat geçmişini getirir (en yeniden eskiye)</summary>
    public async Task<List<FiyatGecmisi>> GetByUrunAsync(int urunId, int? limit = null)
    {
        var query = _context.FiyatGecmisleri
            .AsNoTracking()
            .Where(fg => fg.UrunId == urunId)
            .OrderByDescending(fg => fg.Tarih);

        return limit.HasValue
            ? await query.Take(limit.Value).ToListAsync()
            : await query.ToListAsync();
    }

    /// <summary>Tarih aralığına göre fiyat geçmişi</summary>
    public async Task<List<FiyatGecmisi>> GetByDateRangeAsync(int urunId, DateTime baslangic, DateTime bitis)
        => await _context.FiyatGecmisleri
            .AsNoTracking()
            .Where(fg => fg.UrunId == urunId && fg.Tarih >= baslangic && fg.Tarih <= bitis)
            .OrderBy(fg => fg.Tarih)
            .ToListAsync();

    /// <summary>Bir ürünün minimum / maksimum fiyatını döndürür</summary>
    public async Task<(decimal Min, decimal Max)> GetMinMaxAsync(int urunId)
    {
        var fiyatlar = await _context.FiyatGecmisleri
            .Where(fg => fg.UrunId == urunId)
            .Select(fg => fg.Fiyat)
            .ToListAsync();

        if (fiyatlar.Count == 0) return (0, 0);
        return (fiyatlar.Min(), fiyatlar.Max());
    }

    /// <summary>Son N gündeki ortalama fiyatı hesaplar</summary>
    public async Task<decimal> GetOrtalamaBySonGunAsync(int urunId, int gun = 30)
    {
        var baslangic = DateTime.UtcNow.AddDays(-gun);
        var fiyatlar = await _context.FiyatGecmisleri
            .Where(fg => fg.UrunId == urunId && fg.Tarih >= baslangic)
            .Select(fg => fg.Fiyat)
            .ToListAsync();

        return fiyatlar.Count > 0 ? fiyatlar.Average() : 0;
    }

    /// <summary>Son 30 gün içinde en düşük fiyatı yakalamışsa true döner</summary>
    public async Task<bool> IsRekorDusukAsync(int urunId, decimal mevcutFiyat)
    {
        var minFiyat = await _context.FiyatGecmisleri
            .Where(fg => fg.UrunId == urunId)
            .MinAsync(fg => (decimal?)fg.Fiyat);

        return minFiyat.HasValue && mevcutFiyat <= minFiyat.Value;
    }
}
