// =====================================================
// KategoriDtos.cs
// Kategori API uç noktaları için kullanılan DTO'lar.
// GET /api/kategoriler/{id}/modeller endpoint'i için
// UrunModeli listesini dönen response modelleri içerir.
// =====================================================

namespace FiyatTakipWebSitesi.DTOs;

// ── Listeleme (her satıcı/fiyat kaydı) ──────────────

/// <summary>Bir modelde yer alan tek satıcı+fiyat kaydı</summary>
public class ModelListelemesiResponse
{
    /// <summary>Urun.Id — satıcı listelemasinin benzersiz ID'si</summary>
    public int Id { get; set; }

    /// <summary>Satıcı adı (örn. "Hepsiburada", "Trendyol")</summary>
    public string Satici { get; set; } = string.Empty;

    /// <summary>Güncel fiyat</summary>
    public decimal SonFiyat { get; set; }

    /// <summary>Para birimi (örn. "TRY")</summary>
    public string ParaBirimi { get; set; } = "TRY";

    /// <summary>Ürün sayfasının URL'si</summary>
    public string URL { get; set; } = string.Empty;

    /// <summary>Ürün aktif mi?</summary>
    public bool Aktif { get; set; }

    /// <summary>Son güncelleme zamanı</summary>
    public DateTime SonGuncellemeTarihi { get; set; }
}

// ── UrunModeli ───────────────────────────────────────

/// <summary>
/// GET /api/kategoriler/{id}/modeller endpoint'inin döndürdüğü
/// tek bir ürün modeli yanıt nesnesi
/// </summary>
public class UrunModeliResponse
{
    public int Id { get; set; }

    /// <summary>Model adı (örn. "Samsung Galaxy S24 Ultra 256GB")</summary>
    public string Ad { get; set; } = string.Empty;

    /// <summary>Model görseli</summary>
    public string? Resim { get; set; }

    /// <summary>Bağlı kategorinin adı</summary>
    public string? KategoriAdi { get; set; }

    /// <summary>Bağlı kategorinin ID'si</summary>
    public int KategoriId { get; set; }

    /// <summary>Bu modelde yer alan tüm satıcı/fiyat listelemeleri</summary>
    public List<ModelListelemesiResponse> Listeler { get; set; } = [];
}
