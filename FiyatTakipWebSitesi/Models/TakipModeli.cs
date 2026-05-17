// =====================================================
// TakipModeli.cs
// Bir kullanıcının takip ettiği UrunModeli'ni temsil eder.
// Hibrit takip sistemi: kullanıcı bir modeli takibe alır,
// arka plan tüm site listelemelerini izler, modelin EN UCUZ
// fiyatı değişince mail/uyarı gönderir.
// UNIQUE(UserId, UrunModeliId) — aynı modeli iki kez takipe
// almak engellenir.
// =====================================================

namespace FiyatTakipWebSitesi.Models;

public class TakipModeli
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public Kullanici? Kullanici { get; set; }

    public int UrunModeliId { get; set; }
    public UrunModeli? UrunModeli { get; set; }

    /// <summary>Kullanıcının hedef fiyatı (opsiyonel). En ucuz bunun altına düşerse uyarı.</summary>
    public decimal? HedefFiyat { get; set; }

    public bool FiyatDususuBildir { get; set; } = true;
    public bool FiyatArtisiBildir { get; set; } = false;

    public DateTime EklendigiTarih { get; set; } = DateTime.UtcNow;
    public bool Aktif { get; set; } = true;

    /// <summary>
    /// Son bildirilen en ucuz fiyat — baseline. Yeni en ucuz bunun ALTINA
    /// düşerse fiyat düşüş uyarısı tetiklenir, ÜZERİNE çıkarsa fiyat artış.
    /// Spam mail engelleme: aynı seviye için sürekli mail atmaz.
    /// </summary>
    public decimal? SonBildirilenEnUcuz { get; set; }
}
