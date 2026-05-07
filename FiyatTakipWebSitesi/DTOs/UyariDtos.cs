// =====================================================
// UyariDtos.cs
// Uyarı (bildirim) API endpoint'leri için kullanılan
// veri transfer nesneleri. UyariResponse listeleme için,
// UyariOkunduRequest okundu işaretleme için kullanılır.
// =====================================================

using FiyatTakipWebSitesi.Models;

namespace FiyatTakipWebSitesi.DTOs;

/// <summary>Kullanıcıya dönen uyarı bilgisi</summary>
public class UyariResponse
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public string? UrunAdi { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public string Mesaj { get; set; } = string.Empty;
    public UyariTipi Tip { get; set; }
    public string TipAdi => Tip.ToString();
    public DateTime OlusturulmaTarihi { get; set; }
    public bool Okundu { get; set; }
    public DateTime? OkunduguTarih { get; set; }
}
