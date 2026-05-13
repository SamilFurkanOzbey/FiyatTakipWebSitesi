// =====================================================
// UrunModeli.cs
// Bir ürün modelini temsil eder (örn. "iPhone 15 Pro 256GB").
// Birden fazla Urun kaydı (satıcı listeleme) aynı modele
// bağlı olabilir. Kategori ile ilişkilidir.
// =====================================================

namespace FiyatTakipWebSitesi.Models;

public class UrunModeli
{
    public int Id { get; set; }

    /// <summary>Model adı (örn. "Samsung Galaxy S24 Ultra 256GB")</summary>
    public string Ad { get; set; } = string.Empty;

    /// <summary>Model görseli (URL veya path)</summary>
    public string? Resim { get; set; }

    public int KategoriId { get; set; }

    public Kategori? Kategori { get; set; }

    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;

    // Relations – bu modelde listelenen ürünler (her biri ayrı satıcı/fiyat)
    public ICollection<Urun> Urunler { get; set; } = [];
}
