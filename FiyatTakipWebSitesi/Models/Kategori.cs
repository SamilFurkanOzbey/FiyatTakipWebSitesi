namespace FiyatTakipWebSitesi.Models;

public class Kategori
{
    public int Id { get; set; }
    
    public string Ad { get; set; } = string.Empty;
    
    public string? Aciklama { get; set; }
    
    public string? Icon { get; set; }
    
    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
    
    // Relations
    public ICollection<Urun> Urunler { get; set; } = new List<Urun>();
}
