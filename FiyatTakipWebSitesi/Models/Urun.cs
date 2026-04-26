namespace FiyatTakipWebSitesi.Models;

public class Urun
{
    public int Id { get; set; }
    
    public string Ad { get; set; } = string.Empty;
    
    public string URL { get; set; } = string.Empty;
    
    public string? Resim { get; set; }
    
    public int KategoriId { get; set; }
    
    public Kategori? Kategori { get; set; }
    
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
    public ICollection<FiyatGecmisi> FiyatGecmisleri { get; set; } = new List<FiyatGecmisi>();
    
    public ICollection<Uyari> Uyarilar { get; set; } = new List<Uyari>();
}
