using FiyatTakipWebSitesi.DTOs;
using FiyatTakipWebSitesi.Models;

namespace FiyatTakipWebSitesi.Mappers;

public class UrunMapper
{
    public UrunResponse UrunToUrunResponse(Urun urun)
    {
        if (urun is null)
            return null!;

        return new UrunResponse
        {
            Id = urun.Id,
            Ad = urun.Ad,
            URL = urun.URL,
            Resim = urun.Resim,
            KategoriId = urun.KategoriId,
            KategoriAdi = urun.Kategori?.Ad,
            Satici = urun.Satici,
            ParaBirimi = urun.ParaBirimi,
            BaslangicFiyati = urun.BaslangicFiyati,
            SonFiyati = urun.SonFiyati,
            HedefFiyati = urun.HedefFiyati,
            FiyatDususuBildir = urun.FiyatDususuBildir,
            FiyatArtisiiBildir = urun.FiyatArtisiiBildir,
            Aktif = urun.Aktif,
            EklendigiTarih = urun.EklendigiTarih,
            SonGuncellemeTarihi = urun.SonGuncellemeTarihi
        };
    }
}
