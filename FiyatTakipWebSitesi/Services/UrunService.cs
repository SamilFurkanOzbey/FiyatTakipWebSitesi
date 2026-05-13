// =====================================================
// UrunService.cs
// Bu servis, takip edilen ürünlerin veritabanı işlemlerini
// yönetir. Tüm ürünleri, belirli bir ürünü, kategoriye
// veya kullanıcıya göre ürünleri listeleme; yeni ürün ekleme
// (başlangıç fiyat kaydı ile birlikte), fiyat güncelleme
// (fiyat geçmişine otomatik kayıt atar), ürün bilgilerini
// düzenleme ve soft-delete ile silme işlemlerini sağlar.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;

namespace FiyatTakipWebSitesi.Services;

public class UrunService(
    ApplicationDbContext context,
    UyariService uyariService,
    FiyatGecmisiService fiyatGecmisiService,
    ResimCacheService resimCacheService)
{
    private readonly ApplicationDbContext _context = context;
    private readonly UyariService _uyariService = uyariService;
    private readonly FiyatGecmisiService _fiyatGecmisiService = fiyatGecmisiService;
    private readonly ResimCacheService _resimCacheService = resimCacheService;

    // --- Okuma ---

    public async Task<List<Urun>> GetAllAsync()
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Include(u => u.Kullanici)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    public async Task<Urun?> GetByIdAsync(int id)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Include(u => u.FiyatGecmisleri.OrderByDescending(fg => fg.Tarih).Take(30))
            .Include(u => u.Uyarilar)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<List<Urun>> GetByKategoriAsync(int kategoriId)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Where(u => u.KategoriId == kategoriId && u.Aktif)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    public async Task<List<Urun>> GetByKullaniciAsync(int kullaniciId)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Where(u => u.UserId == kullaniciId && u.Aktif)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    public async Task<List<Urun>> AraAsync(string aramaMetni, int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(aramaMetni))
            return [];

        var metin = aramaMetni.Trim();

        return await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Where(u => u.Aktif && EF.Functions.Like(u.Ad, $"%{metin}%"))
            .OrderByDescending(u => u.EklendigiTarih)
            .Take(limit)
            .ToListAsync();
    }

    // --- Yazma ---

    public async Task<Urun> EkleAsync(Urun urun)
    {
        urun.EklendigiTarih = DateTime.UtcNow;
        urun.SonGuncellemeTarihi = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(urun.Resim))
            urun.Resim = await _resimCacheService.ResimOnbellegeAlAsync(urun.Resim);

        _context.Urunler.Add(urun);
        await _context.SaveChangesAsync();

        // Başlangıç fiyat kaydı
        var ilkFiyat = new FiyatGecmisi
        {
            UrunId = urun.Id,
            Fiyat = urun.BaslangicFiyati,
            Tarih = DateTime.UtcNow,
            StokDurumuMevcutMu = true,
            Durum = "Başlangıç"
        };
        _context.FiyatGecmisleri.Add(ilkFiyat);
        await _context.SaveChangesAsync();

        return urun;
    }

    public async Task<bool> FiyatGuncelleAsync(int urunId, decimal yeniFiyat, bool stokVar = true)
    {
        var urun = await _context.Urunler
            .Include(u => u.Kullanici)
            .FirstOrDefaultAsync(u => u.Id == urunId);
        if (urun is null) return false;

        var eskiFiyat = urun.SonFiyati;
        var degisim = yeniFiyat - eskiFiyat;
        var degisimYuzdesi = eskiFiyat != 0 ? (degisim / eskiFiyat) * 100 : 0;

        urun.SonFiyati = yeniFiyat;
        urun.SonGuncellemeTarihi = DateTime.UtcNow;

        string durum = degisim switch
        {
            < 0 => "Düşüş",
            > 0 => "Artış",
            _ => "Değişmedi"
        };

        var fiyatGecmisi = new FiyatGecmisi
        {
            UrunId = urunId,
            Fiyat = yeniFiyat,
            OncekiFiyat = eskiFiyat,
            FiyatDegisimi = degisim,
            FiyatDegisimYüzdesi = Math.Round(degisimYuzdesi, 2),
            Tarih = DateTime.UtcNow,
            StokDurumuMevcutMu = stokVar,
            Durum = durum
        };

        _context.FiyatGecmisleri.Add(fiyatGecmisi);
        await _context.SaveChangesAsync();

        // Uyarı kontrolü (sadece fiyat değiştiyse)
        if (degisim != 0)
        {
            var isRekorDusuk = await _fiyatGecmisiService.IsRekorDusukAsync(urunId, yeniFiyat);
            await _uyariService.FiyatDegisimUyariKontrolAsync(urun, eskiFiyat, yeniFiyat, isRekorDusuk);
        }

        return true;
    }

    public async Task<bool> GuncelleAsync(Urun urun)
    {
        var mevcut = await _context.Urunler.FindAsync(urun.Id);
        if (mevcut is null) return false;

        if (!string.IsNullOrWhiteSpace(urun.Resim) && urun.Resim != mevcut.Resim)
            mevcut.Resim = await _resimCacheService.ResimOnbellegeAlAsync(urun.Resim);
        else if (!string.IsNullOrWhiteSpace(urun.Resim))
            mevcut.Resim = urun.Resim;

        mevcut.Ad = urun.Ad;
        mevcut.URL = urun.URL;
        mevcut.KategoriId = urun.KategoriId;
        mevcut.HedefFiyati = urun.HedefFiyati;
        mevcut.FiyatDususuBildir = urun.FiyatDususuBildir;
        mevcut.FiyatArtisiiBildir = urun.FiyatArtisiiBildir;
        mevcut.SonGuncellemeTarihi = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SilAsync(int id)
    {
        var urun = await _context.Urunler.FindAsync(id);
        if (urun is null) return false;

        urun.Aktif = false; // Soft delete
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task SeedOrnekUrunlerAsync()
    {
        if (await _context.Urunler.AnyAsync()) return;

        var kategori = await _context.Kategoriler.FirstOrDefaultAsync(k => k.Ad == "Elektronik")
                       ?? await _context.Kategoriler.FirstOrDefaultAsync();

        if (kategori is null) return;

        var urunler = new List<Urun>
        {
            new()
            {
                Ad = "Apple iPhone 15 (128 GB) - Siyah",
                URL = "https://www.hepsiburada.com/iphone-15-128-gb-p-HBCV00004ZEWB8",
                Resim = "https://productimages.hepsiburada.net/s/448/550/110000483863414.jpg",
                Satici = "Hepsiburada",
                BaslangicFiyati = 52999,
                SonFiyati = 51499,
                HedefFiyati = 49000,
                KategoriId = kategori.Id,
                ParaBirimi = "TRY",
                Aktif = true
            },
            new()
            {
                Ad = "Samsung Galaxy S24 Ultra 512 GB",
                URL = "https://www.hepsiburada.com/samsung-galaxy-s24-ultra-512-gb-p-HBCV00005OPYYR",
                Resim = "https://productimages.hepsiburada.net/s/525/550/110000582239665.jpg",
                Satici = "Samsung",
                BaslangicFiyati = 69999,
                SonFiyati = 69999,
                HedefFiyati = 65000,
                KategoriId = kategori.Id,
                ParaBirimi = "TRY",
                Aktif = true
            }
        };

        foreach (var u in urunler)
        {
            await EkleAsync(u);
        }
    }
}
