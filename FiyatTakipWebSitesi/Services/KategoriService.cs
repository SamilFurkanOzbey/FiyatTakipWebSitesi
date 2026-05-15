// =====================================================
// KategoriService.cs
// Bu servis, ürün kategorilerinin yönetimini sağlar.
// Veritabanındaki kategorileri listeleme, ID ile getirme,
// yeni kategori ekleme, güncelleme ve silme işlemlerini
// asenkron olarak gerçekleştirir. Aynı zamanda uygulama
// ilk çalıştığında varsayılan kategorileri
// otomatik olarak seed eden bir metot da içerir.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using FiyatTakipWebSitesi.Repositories;

namespace FiyatTakipWebSitesi.Services;

public class KategoriService(IRepository<Kategori> kategoriRepository, IRepository<UrunModeli> modelRepository, IMemoryCache cache)
{
    private readonly IRepository<Kategori> _kategoriRepository = kategoriRepository;
    private readonly IRepository<UrunModeli> _modelRepository = modelRepository;
    private readonly IMemoryCache _cache = cache;

    private const string KategoriListesiCacheKey = "KategoriListesi";
    private static string KategoriModelleriCacheKey(int id) => $"KategoriModelleri_{id}";

    public async Task<List<Kategori>> GetAllAsync()
    {
        return await _cache.GetOrCreateAsync(KategoriListesiCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            var result = await _kategoriRepository.GetAllAsync(k => k.Urunler);
            return result.OrderBy(k => k.Ad).ToList();
        }) ?? [];
    }

    public async Task<Kategori?> GetByIdAsync(int id)
    {
        var result = await _kategoriRepository.FindAsync(k => k.Id == id, k => k.Urunler.Where(u => u.Aktif));
        return result.FirstOrDefault();
    }

    /// <summary>
    /// Belirli bir kategoriye ait tüm UrunModeli kayıtlarını,
    /// her modelin satıcı/fiyat listelemeleri (Urunler) ile birlikte döner.
    /// </summary>
    public async Task<List<UrunModeli>> GetModellerByKategoriAsync(int kategoriId)
    {
        return await _cache.GetOrCreateAsync(KategoriModelleriCacheKey(kategoriId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var result = await _modelRepository.FindAsync(m => m.KategoriId == kategoriId, m => m.Kategori!, m => m.Listeler.Where(u => u.Aktif));
            return result.OrderBy(m => m.Ad).ToList();
        }) ?? [];
    }

    public async Task<Kategori> EkleAsync(Kategori kategori)
    {
        kategori.OlusturulmaTarihi = DateTime.UtcNow;
        await _kategoriRepository.AddAsync(kategori);
        await _kategoriRepository.SaveChangesAsync();
        
        _cache.Remove(KategoriListesiCacheKey);
        return kategori;
    }

    public async Task<bool> GuncelleAsync(Kategori kategori)
    {
        var mevcut = await _kategoriRepository.GetByIdAsync(kategori.Id);
        if (mevcut is null) return false;

        mevcut.Ad = kategori.Ad;
        mevcut.Aciklama = kategori.Aciklama;
        mevcut.Icon = kategori.Icon;

        _kategoriRepository.Update(mevcut);
        await _kategoriRepository.SaveChangesAsync();
        
        _cache.Remove(KategoriListesiCacheKey);
        _cache.Remove(KategoriModelleriCacheKey(kategori.Id));
        return true;
    }

    public async Task<bool> SilAsync(int id)
    {
        var kategori = await _kategoriRepository.GetByIdAsync(id);
        if (kategori is null) return false;

        _kategoriRepository.Remove(kategori);
        await _kategoriRepository.SaveChangesAsync();
        
        _cache.Remove(KategoriListesiCacheKey);
        _cache.Remove(KategoriModelleriCacheKey(id));
        return true;
    }

    /// <summary>Seed: varsayılan kategorileri ekler (sadece boşsa)</summary>
    public async Task SeedVarsayilanKategorilerAsync()
    {
        var sayi = (await _kategoriRepository.GetAllAsync()).Count();
        if (sayi > 0) return;

        var kategoriler = new List<Kategori>
        {
            new() { Ad = "Elektronik", Aciklama = "Bilgisayar, telefon ve diğer elektronik ürünler", Icon = "💻" },
            new() { Ad = "Ev & Yaşam", Aciklama = "Mobilya, dekorasyon ve ev eşyaları", Icon = "🏠" },
            new() { Ad = "Moda", Aciklama = "Giyim, ayakkabı ve aksesuarlar", Icon = "👕" },
            new() { Ad = "Kozmetik", Aciklama = "Kişisel bakım ve makyaj ürünleri", Icon = "💄" },
            new() { Ad = "Otomotiv", Aciklama = "Araç içi aksesuarlar ve yedek parçalar", Icon = "🚗" }
        };

        await _kategoriRepository.AddRangeAsync(kategoriler);
        await _kategoriRepository.SaveChangesAsync();
    }
}
