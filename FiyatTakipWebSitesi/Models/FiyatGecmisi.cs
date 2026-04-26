namespace FiyatTakipWebSitesi.Models;

public class FiyatGecmisi
{
    public int Id { get; set; }
    
    public int UrunId { get; set; }
    
    public Urun? Urun { get; set; }
    
    public decimal Fiyat { get; set; }
    
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    
    public decimal? OncekiFiyat { get; set; }
    
    public decimal? FiyatDegisimi { get; set; }
    
    public decimal? FiyatDegisimYüzdesi { get; set; }
    
    public bool StokDurumuMevcutMu { get; set; } = true;
    
    public string? Durum { get; set; } // "Yüksek", "Düşük", "Normal", "Rekor Düşük", vb.
}
