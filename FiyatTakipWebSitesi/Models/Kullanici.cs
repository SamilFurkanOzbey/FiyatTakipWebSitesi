// =====================================================
// Kullanici.cs
// Bu model, sisteme kayıtlı kullanıcıları temsil eder.
// Ad, soyad, e-posta ve hash’lenmiş şifre bilgilerini;
// hesap durumunu (aktif/pasif, e-posta doğrulama);
// bildirim tercihlerini (e-posta ve push) ve
// kullanıcıya ait ürün/uyarı koleksiyonlarını saklar.
// =====================================================

namespace FiyatTakipWebSitesi.Models;

public class Kullanici
{
    public int Id { get; set; }
    
    public string Ad { get; set; } = string.Empty;
    
    public string Soyad { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    
    public string? TelefonNumarasi { get; set; }
    
    public byte[]? SifreHash { get; set; }
    
    public byte[]? SifreTuzu { get; set; }
    
    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
    
    public DateTime? SonGirisTarihi { get; set; }
    
    public bool Aktif { get; set; } = true;
    
    public bool EmailDogrulandi { get; set; } = false;
    
    // Bildirim ayarları
    public bool EmailBildirimleriniAc { get; set; } = true;
    
    public bool PushBildirimleriniAc { get; set; } = true;
    
    // Relations
    public ICollection<Urun> Urunler { get; set; } = new List<Urun>();
    
    public ICollection<Uyari> Uyarilar { get; set; } = new List<Uyari>();
}
