// =====================================================
// OturumDurumu.cs
// Blazor Server Interactive Mode için oturum yönetimi.
// Scoped olarak kayıtlı — her tarayıcı bağlantısı için
// ayrı instance. "Beni Hatırla" tıklanırsa kullanıcı ID'si
// ProtectedLocalStorage'a (şifreli) yazılır, sonraki
// ziyaretlerde otomatik giriş yapılır.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

namespace FiyatTakipWebSitesi.Services;

public class OturumDurumu(ApplicationDbContext context, ProtectedLocalStorage storage)
{
    private const string StorageKey = "fiyatTakip:kullaniciId";

    private readonly ApplicationDbContext _context = context;
    private readonly ProtectedLocalStorage _storage = storage;

    public int? KullaniciId { get; private set; }
    public string? Email { get; private set; }
    public string? Ad { get; private set; }
    public string? Soyad { get; private set; }

    public bool GirisYapildi => KullaniciId.HasValue;

    /// <summary>State değişince UI'ı bilgilendirmek için event.</summary>
    public event Action? Degisti;

    /// <summary>
    /// Kullanıcı bilgilerini in-memory state'e set eder. Eğer hatirla=true ise
    /// tarayıcı localStorage'a da yazar (şifreli) — bir dahaki ziyarette otomatik
    /// giriş için.
    /// </summary>
    public async Task GirisYapAsync(Kullanici kullanici, bool hatirla = false)
    {
        KullaniciId = kullanici.Id;
        Email = kullanici.Email;
        Ad = kullanici.Ad;
        Soyad = kullanici.Soyad;
        Degisti?.Invoke();

        if (hatirla)
        {
            try
            {
                await _storage.SetAsync(StorageKey, kullanici.Id);
            }
            catch
            {
                // Prerender sırasında veya JS henüz hazır değilse hata atar — sessizce devam.
            }
        }
        else
        {
            // "Beni hatırla" işaretli değilse → eski kayıt varsa temizle
            try { await _storage.DeleteAsync(StorageKey); } catch { }
        }
    }

    /// <summary>Oturumu sonlandırır + localStorage'ı temizler.</summary>
    public async Task CikisYapAsync()
    {
        KullaniciId = null;
        Email = null;
        Ad = null;
        Soyad = null;
        Degisti?.Invoke();

        try { await _storage.DeleteAsync(StorageKey); } catch { }
    }

    /// <summary>
    /// Uygulama açılırken çağrılır. localStorage'da "beni hatırla" verisi varsa
    /// kullanıcıyı otomatik giriş yaptırır. OturumBootstrap component'inden tetiklenir.
    /// </summary>
    public async Task BaslangictaYukleAsync()
    {
        if (GirisYapildi) return; // Zaten girişli

        try
        {
            var sonuc = await _storage.GetAsync<int>(StorageKey);
            if (!sonuc.Success) return;

            var kullanici = await _context.Kullanicilar
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Id == sonuc.Value && k.Aktif);

            if (kullanici is null)
            {
                // Kullanıcı silinmiş / pasif olmuş → localStorage'ı temizle
                await _storage.DeleteAsync(StorageKey);
                return;
            }

            KullaniciId = kullanici.Id;
            Email = kullanici.Email;
            Ad = kullanici.Ad;
            Soyad = kullanici.Soyad;
            Degisti?.Invoke();
        }
        catch
        {
            // Prerender, JS hazır değil, vs. — ignore
        }
    }

    /// <summary>Veritabanından güncel kullanıcı bilgisini çeker.</summary>
    public async Task<Kullanici?> KullaniciGetirAsync()
    {
        if (!KullaniciId.HasValue) return null;
        return await _context.Kullanicilar
            .FirstOrDefaultAsync(k => k.Id == KullaniciId.Value);
    }
}
