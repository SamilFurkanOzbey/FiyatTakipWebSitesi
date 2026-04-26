namespace FiyatTakipWebSitesi.Models;

public class Uyari
{
    public int Id { get; set; }
    
    public int UrunId { get; set; }
    
    public Urun? Urun { get; set; }
    
    public int UserId { get; set; }
    
    public Kullanici? Kullanici { get; set; }
    
    public string Baslik { get; set; } = string.Empty;
    
    public string Mesaj { get; set; } = string.Empty;
    
    public UyariTipi Tip { get; set; } // FiyatDususu, FiyatArtisi, StokGuncellendi
    
    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
    
    public bool Okundu { get; set; } = false;
    
    public DateTime? OkunduğuTarih { get; set; }
}

public enum UyariTipi
{
    FiyatDususu,
    FiyatArtisi,
    StokGuncellendi,
    HedefFiyataBasti,
    RekorDusuk
}
