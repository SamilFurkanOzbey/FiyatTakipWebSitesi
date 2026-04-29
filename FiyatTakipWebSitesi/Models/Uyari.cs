// =====================================================
// Uyari.cs
// Bu model, sistemin kullanıcılara gönderdiği fiyat ve
// stok bildirimlerini temsil eder. Bir uyarı; hangi ürüne
// ait olduğunu, kime gönderildiğini, ne tür bir değişiklik
// olduğunu ve okunup okunmadığını tutar.
// =====================================================

namespace FiyatTakipWebSitesi.Models;

public class Uyari
{
    // Uyarının benzersiz kimliği
    public int Id { get; set; }

    // Uyarının hangi ürüne ait olduğunun ID'si
    public int UrunId { get; set; }

    // UrunId üzerinden bağlı ürün nesnesi (navigation property)
    public Urun? Urun { get; set; }

    // Uyarının hangi kullanıcıya ait olduğunun ID'si
    public int UserId { get; set; }

    // UserId üzerinden bağlı kullanıcı nesnesi (navigation property)
    public Kullanici? Kullanici { get; set; }

    // Uyarının başlığı (örn: "Fiyat Düştü!")
    public string Baslik { get; set; } = string.Empty;

    // Uyarının detay mesajı (örn: "Ürün fiyatı 299₺'ye düştü.")
    public string Mesaj { get; set; } = string.Empty;

    // Uyarının türü: fiyat düşüşü, artışı, stok güncellemesi vb.
    public UyariTipi Tip { get; set; }

    // Uyarının oluşturulduğu tarih, varsayılan olarak şu anki UTC zamanı
    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;

    // Kullanıcının bu uyarıyı okuyup okumadığı, varsayılan olarak okunmamış
    public bool Okundu { get; set; } = false;

    // Uyarı okunduysa hangi tarihte okunduğu (okunmadıysa null)
    public DateTime? OkunduğuTarih { get; set; }
}

// Uyarı türlerini tanımlayan enum
public enum UyariTipi
{
    FiyatDususu,        // Ürün fiyatı düştü
    FiyatArtisi,        // Ürün fiyatı arttı
    StokGuncellendi,    // Ürünün stok durumu değişti
    HedefFiyataBasti,   // Ürün kullanıcının belirlediği hedef fiyata ulaştı
    RekorDusuk          // Ürün takip edildiğinden bu yana en düşük fiyatına ulaştı
}