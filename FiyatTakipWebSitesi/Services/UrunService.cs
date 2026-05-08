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

public class UrunService
{
    private readonly ApplicationDbContext _context;

    public UrunService(ApplicationDbContext context)
    {
        _context = context;
    }

    // --- Okuma ---

    public async Task<List<Urun>> GetAllAsync()
        => await _context.Urunler
            .Include(u => u.Kategori)
            .Include(u => u.Kullanici)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    public async Task<Urun?> GetByIdAsync(int id)
        => await _context.Urunler
            .Include(u => u.Kategori)
            .Include(u => u.FiyatGecmisleri.OrderByDescending(fg => fg.Tarih).Take(30))
            .Include(u => u.Uyarilar)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<List<Urun>> GetByKategoriAsync(int kategoriId)
        => await _context.Urunler
            .Include(u => u.Kategori)
            .Where(u => u.KategoriId == kategoriId)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    public async Task<List<Urun>> GetByKullaniciAsync(int kullaniciId)
        => await _context.Urunler
            .Include(u => u.Kategori)
            .Where(u => u.UserId == kullaniciId)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    // --- Yazma ---

    public async Task<Urun> EkleAsync(Urun urun)
    {
        urun.EklendigiTarih = DateTime.UtcNow;
        urun.SonGuncellemeTarihi = DateTime.UtcNow;

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
        var urun = await _context.Urunler.FindAsync(urunId);
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
        return true;
    }

    public async Task<bool> GuncelleAsync(Urun urun)
    {
        var mevcut = await _context.Urunler.FindAsync(urun.Id);
        if (mevcut is null) return false;

        mevcut.Ad = urun.Ad;
        mevcut.URL = urun.URL;
        mevcut.Resim = urun.Resim;
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
}
