// =====================================================
// OturumDurumu.cs
// Blazor Server Interactive Mode için basit oturum
// yönetimi. Scoped olarak kayıtlı — her tarayıcı bağlantısı
// (circuit) için ayrı bir instance vardır. Kullanıcı giriş
// yapınca KullaniciId set edilir, bağlantı boyunca korunur.
// Sayfalar Degisti event'i ile state güncellemelerini dinler.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;

namespace FiyatTakipWebSitesi.Services;

public class OturumDurumu(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    public int? KullaniciId { get; private set; }
    public string? Email { get; private set; }
    public string? Ad { get; private set; }
    public string? Soyad { get; private set; }

    public bool GirisYapildi => KullaniciId.HasValue;

    /// <summary>State değişince UI'ı bilgilendirmek için event.</summary>
    public event Action? Degisti;

    /// <summary>Kullanıcı bilgilerini set eder ve UI'ı bilgilendirir.</summary>
    public void GirisYap(Kullanici kullanici)
    {
        KullaniciId = kullanici.Id;
        Email = kullanici.Email;
        Ad = kullanici.Ad;
        Soyad = kullanici.Soyad;
        Degisti?.Invoke();
    }

    /// <summary>Oturumu sonlandırır.</summary>
    public void CikisYap()
    {
        KullaniciId = null;
        Email = null;
        Ad = null;
        Soyad = null;
        Degisti?.Invoke();
    }

    /// <summary>Veritabanından güncel kullanıcı bilgisini çeker.</summary>
    public async Task<Kullanici?> KullaniciGetirAsync()
    {
        if (!KullaniciId.HasValue) return null;
        return await _context.Kullanicilar
            .FirstOrDefaultAsync(k => k.Id == KullaniciId.Value);
    }
}
