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

namespace FiyatTakipWebSitesi.Services;

public class KategoriService
{
    private readonly ApplicationDbContext _context;

    public KategoriService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Kategori>> GetAllAsync()
        => await _context.Kategoriler
            .Include(k => k.Urunler)
            .OrderBy(k => k.Ad)
            .ToListAsync();

    public async Task<Kategori?> GetByIdAsync(int id)
        => await _context.Kategoriler
<<<<<<< Updated upstream
            .Include(k => k.Urunler.Where(u => u.Aktif))
=======
            .AsNoTracking()
            .Include(k => k.Urunler)
>>>>>>> Stashed changes
            .FirstOrDefaultAsync(k => k.Id == id);

    public async Task<Kategori> EkleAsync(Kategori kategori)
    {
        kategori.OlusturulmaTarihi = DateTime.UtcNow;
        _context.Kategoriler.Add(kategori);
        await _context.SaveChangesAsync();
        return kategori;
    }

    public async Task<bool> GuncelleAsync(Kategori kategori)
    {
        var mevcut = await _context.Kategoriler.FindAsync(kategori.Id);
        if (mevcut is null) return false;

        mevcut.Ad = kategori.Ad;
        mevcut.Aciklama = kategori.Aciklama;
        mevcut.Icon = kategori.Icon;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SilAsync(int id)
    {
        var kategori = await _context.Kategoriler.FindAsync(id);
        if (kategori is null) return false;

        _context.Kategoriler.Remove(kategori);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>Seed: varsayılan kategorileri ekler (sadece boşsa)</summary>
    public async Task SeedVarsayilanKategorilerAsync()
    {
        if (await _context.Kategoriler.AnyAsync()) return;

        var kategoriler = new List<Kategori>
        {
            new() { Ad = "Elektronik",       Aciklama = "Telefon, bilgisayar, tablet vb.", Icon = "💻" },
            new() { Ad = "Giyim",            Aciklama = "Kıyafet ve aksesuar",             Icon = "👕" },
            new() { Ad = "Ev & Yaşam",       Aciklama = "Ev aletleri ve dekorasyon",       Icon = "🏠" },
            new() { Ad = "Spor",             Aciklama = "Spor ekipmanları",                Icon = "⚽" },
            new() { Ad = "Kitap & Hobi",     Aciklama = "Kitap, oyun ve hobi ürünleri",    Icon = "📚" },
            new() { Ad = "Kozmetik",         Aciklama = "Güzellik ve kişisel bakım",        Icon = "💄" },
            new() { Ad = "Diğer",            Aciklama = "Diğer ürünler",                   Icon = "📦" },
        };

        _context.Kategoriler.AddRange(kategoriler);
        await _context.SaveChangesAsync();
    }
}
