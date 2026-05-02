// =====================================================
// UrunDtos.cs
// Ürün API uç noktaları için kullanılan veri transfer
// nesneleri (DTO). İstek (request) ve yanıt (response)
// modellerini ayrı tutar; döngüsel referans ve gereksiz
// alan maruziyetini önler.
// =====================================================

namespace FiyatTakipWebSitesi.DTOs;

// ── Yanıt Modelleri ───────────────────────────────────

public class UrunResponse
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string URL { get; set; } = string.Empty;
    public string? Resim { get; set; }
    public int KategoriId { get; set; }
    public string? KategoriAdi { get; set; }
    public decimal BaslangicFiyati { get; set; }
    public decimal SonFiyati { get; set; }
    public decimal? HedefFiyati { get; set; }
    public bool FiyatDususuBildir { get; set; }
    public bool FiyatArtisiiBildir { get; set; }
    public bool Aktif { get; set; }
    public DateTime EklendigiTarih { get; set; }
    public DateTime SonGuncellemeTarihi { get; set; }
}

// ── İstek Modelleri ───────────────────────────────────

/// <summary>Yeni ürün eklemek için kullanılan DTO</summary>
public class UrunEkleRequest
{
    public string Ad { get; set; } = string.Empty;
    public string URL { get; set; } = string.Empty;
    public string? Resim { get; set; }
    public int KategoriId { get; set; }
    public int? UserId { get; set; }
    public decimal BaslangicFiyati { get; set; }
    public decimal? HedefFiyati { get; set; }
    public bool FiyatDususuBildir { get; set; } = true;
    public bool FiyatArtisiiBildir { get; set; } = false;
}

/// <summary>Ürün bilgilerini güncellemek için kullanılan DTO</summary>
public class UrunGuncelleRequest
{
    public string Ad { get; set; } = string.Empty;
    public string URL { get; set; } = string.Empty;
    public string? Resim { get; set; }
    public int KategoriId { get; set; }
    public decimal? HedefFiyati { get; set; }
    public bool FiyatDususuBildir { get; set; }
    public bool FiyatArtisiiBildir { get; set; }
}
