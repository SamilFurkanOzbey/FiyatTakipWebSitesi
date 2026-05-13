// =====================================================
// UrunModeli.cs
// Bu model, bir ürünün soyut "modelini" temsil eder
// (örn: "iPhone 15 128GB Siyah"). Aynı modelin farklı
// sitelerdeki listelemeleri Listeler navigation
// property'si üzerinden bağlanır. Yani 1 UrunModeli ↔ N Urun.
// Sistem katalogunu (Kategoriler sayfası) bu tablo besler;
// kullanıcının "Takip Ettiklerim" sayfasına elle eklediği
// serbest URL'ler ise UrunModeli'ne bağlı OLMADAN
// (Urun.UrunModeliId = null) çalışmaya devam eder.
// =====================================================

namespace FiyatTakipWebSitesi.Models;

public class UrunModeli
{
    public int Id { get; set; }

    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    public string? Resim { get; set; }

    public int KategoriId { get; set; }

    public Kategori? Kategori { get; set; }

    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;

    public bool Aktif { get; set; } = true;

    // Bu modelin farklı sitelerdeki listelemeleri (Hepsiburada, Trendyol, ...)
    public ICollection<Urun> Listeler { get; set; } = [];
}
