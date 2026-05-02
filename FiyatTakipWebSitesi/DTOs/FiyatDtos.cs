// =====================================================
// FiyatDtos.cs
// Fiyat geçmişi ve fiyat sorgulama API uç noktaları
// için kullanılan veri transfer nesneleri (DTO).
// =====================================================

namespace FiyatTakipWebSitesi.DTOs;

// ── Fiyat Geçmişi Yanıt Modeli ────────────────────────

public class FiyatGecmisiResponse
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public decimal Fiyat { get; set; }
    public decimal? OncekiFiyat { get; set; }
    public decimal? FiyatDegisimi { get; set; }
    public decimal? FiyatDegisimYuzdesi { get; set; }
    public bool StokDurumuMevcutMu { get; set; }
    public string? Durum { get; set; }
    public DateTime Tarih { get; set; }
}

// ── Fiyat Sorgulama İstek/Yanıt Modeli ───────────────

/// <summary>URL'den fiyat çekmek için istek modeli</summary>
public class FiyatSorgulaRequest
{
    public string URL { get; set; } = string.Empty;
    /// <summary>Dolu gelirse, çekilen fiyat bu ürünün veritabanı kaydına da yazılır</summary>
    public int? UrunId { get; set; }
}

/// <summary>URL'den çekilen fiyat bilgisi yanıt modeli</summary>
public class FiyatSorgulaResponse
{
    public string URL { get; set; } = string.Empty;
    public string FiyatMetin { get; set; } = string.Empty;
    public decimal? FiyatSayi { get; set; }
    public bool Basarili { get; set; }
    public string? HataMesaji { get; set; }
    public DateTime SorgulanmaTarihi { get; set; } = DateTime.UtcNow;
}
