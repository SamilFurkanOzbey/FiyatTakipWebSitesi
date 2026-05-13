// =====================================================
// UrunModeli.cs
<<<<<<< HEAD
// Bir ürün modelini temsil eder (örn. "iPhone 15 Pro 256GB").
// Birden fazla Urun kaydı (satıcı listeleme) aynı modele
// bağlı olabilir. Kategori ile ilişkilidir.
=======
// Bu model, bir ürünün soyut "modelini" temsil eder
// (örn: "iPhone 15 128GB Siyah"). Aynı modelin farklı
// sitelerdeki listelemeleri Listeler navigation
// property'si üzerinden bağlanır. Yani 1 UrunModeli ↔ N Urun.
// Sistem katalogunu (Kategoriler sayfası) bu tablo besler;
// kullanıcının "Takip Ettiklerim" sayfasına elle eklediği
// serbest URL'ler ise UrunModeli'ne bağlı OLMADAN
// (Urun.UrunModeliId = null) çalışmaya devam eder.
>>>>>>> f7bda775aa08461dd11e4cea24373251c138b397
// =====================================================

namespace FiyatTakipWebSitesi.Models;

public class UrunModeli
{
    public int Id { get; set; }

<<<<<<< HEAD
    /// <summary>Model adı (örn. "Samsung Galaxy S24 Ultra 256GB")</summary>
    public string Ad { get; set; } = string.Empty;

    /// <summary>Model görseli (URL veya path)</summary>
=======
    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

>>>>>>> f7bda775aa08461dd11e4cea24373251c138b397
    public string? Resim { get; set; }

    public int KategoriId { get; set; }

    public Kategori? Kategori { get; set; }

    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;

<<<<<<< HEAD
    // Relations – bu modelde listelenen ürünler (her biri ayrı satıcı/fiyat)
    public ICollection<Urun> Urunler { get; set; } = [];
=======
    public bool Aktif { get; set; } = true;

    // Bu modelin farklı sitelerdeki listelemeleri (Hepsiburada, Trendyol, ...)
    public ICollection<Urun> Listeler { get; set; } = [];
>>>>>>> f7bda775aa08461dd11e4cea24373251c138b397
}
