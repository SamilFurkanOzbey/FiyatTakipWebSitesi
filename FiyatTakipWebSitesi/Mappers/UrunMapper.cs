using FiyatTakipWebSitesi.DTOs;
using FiyatTakipWebSitesi.Models;
using Riok.Mapperly.Abstractions;

namespace FiyatTakipWebSitesi.Mappers;

[Mapper]
public partial class UrunMapper
{
    [MapProperty(nameof(Urun.Kategori.Ad), nameof(UrunResponse.KategoriAdi))]
    [MapperIgnoreSource(nameof(Urun.UserId))]
    [MapperIgnoreSource(nameof(Urun.Kullanici))]
    [MapperIgnoreSource(nameof(Urun.FiyatGecmisleri))]
    [MapperIgnoreSource(nameof(Urun.Uyarilar))]
    public partial UrunResponse UrunToUrunResponse(Urun urun);
}
