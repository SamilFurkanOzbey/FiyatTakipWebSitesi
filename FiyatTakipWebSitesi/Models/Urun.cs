// =====================================================
// Urun.cs
// Bu model, kullanıcıların fiyat takibine eklediği
// ürünleri temsil eder. Ürün adı, URL, görsel,
// başlangıç/son fiyat, hedef fiyat ve bildirim
// tercihlerini (düşüş/artış) saklar. Aynı zamanda
// bağlı kategori, kullanıcı, fiyat geçmişi ve
// uyarı koleksiyonlarına navigation property
// üzerinden erişim sağlar. UrunService tarafından
// yönetilir.
// =====================================================

namespace FiyatTakipWebSitesi.Models;

public class Urun
{
    public int Id { get; set; }
    
    public string Ad { get; set; } = string.Empty;
    
    public string URL { get; set; } = string.Empty;
    
    public string? Resim { get; set; }
    
    public string Satici { get; set; } = string.Empty;
    
    public string ParaBirimi { get; set; } = "TRY";
    
    public int KategoriId { get; set; }
    
    public Kategori? Kategori { get; set; }
    
    /// <summary>Bu ürün hangi modele ait (opsiyonel)</summary>
    public int? UrunModeliId { get; set; }
    
    public UrunModeli? UrunModeli { get; set; }
    
    public int? UserId { get; set; }
    
    public Kullanici? Kullanici { get; set; }
    
    public decimal BaslangicFiyati { get; set; }
    
    public decimal SonFiyati { get; set; }
    
    public DateTime EklendigiTarih { get; set; } = DateTime.UtcNow;
    
    public DateTime SonGuncellemeTarihi { get; set; } = DateTime.UtcNow;
    
    public bool Aktif { get; set; } = true;
    
    // Notification ayarları
    public bool FiyatDususuBildir { get; set; } = true;
    
    public bool FiyatArtisiiBildir { get; set; } = false;
    
    public decimal? HedefFiyati { get; set; }
    
    // Relations
    public ICollection<FiyatGecmisi> FiyatGecmisleri { get; set; } = [];
    
    public ICollection<Uyari> Uyarilar { get; set; } = [];
}
