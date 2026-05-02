// =====================================================
// UrunlerController.cs
// /api/urunler ve /api/urunler/{id} uç noktalarını
// yönetir. Ürün listeleme, tek ürün sorgulama, yeni
// ürün ekleme, ürün güncelleme ve soft-delete silme
// işlemlerini sunar.
// =====================================================

using FiyatTakipWebSitesi.DTOs;
using FiyatTakipWebSitesi.Models;
using FiyatTakipWebSitesi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiyatTakipWebSitesi.Controllers;

[ApiController]
[Route("api/urunler")]
[Produces("application/json")]
public class UrunlerController : ControllerBase
{
    private readonly UrunService _urunService;
    private readonly ILogger<UrunlerController> _logger;

    public UrunlerController(UrunService urunService, ILogger<UrunlerController> logger)
    {
        _urunService = urunService;
        _logger = logger;
    }

    // ── GET /api/urunler ──────────────────────────────
    /// <summary>Tüm aktif ürünleri listeler</summary>
    /// <param name="kategoriId">Opsiyonel: Kategoriye göre filtrele</param>
    /// <param name="kullaniciId">Opsiyonel: Kullanıcıya göre filtrele</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UrunResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? kategoriId,
        [FromQuery] int? kullaniciId)
    {
        try
        {
            List<Urun> urunler;

            if (kategoriId.HasValue)
                urunler = await _urunService.GetByKategoriAsync(kategoriId.Value);
            else if (kullaniciId.HasValue)
                urunler = await _urunService.GetByKullaniciAsync(kullaniciId.Value);
            else
                urunler = await _urunService.GetAllAsync();

            var response = urunler.Select(MapToResponse);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ürünler listelenirken hata oluştu.");
            return StatusCode(500, new { hata = "Ürünler alınırken bir hata oluştu." });
        }
    }

    // ── POST /api/urunler ─────────────────────────────
    /// <summary>Yeni ürün ekler</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UrunResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] UrunEkleRequest dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var urun = new Urun
            {
                Ad = dto.Ad.Trim(),
                URL = dto.URL.Trim(),
                Resim = dto.Resim,
                KategoriId = dto.KategoriId,
                UserId = dto.UserId,
                BaslangicFiyati = dto.BaslangicFiyati,
                SonFiyati = dto.BaslangicFiyati,
                HedefFiyati = dto.HedefFiyati,
                FiyatDususuBildir = dto.FiyatDususuBildir,
                FiyatArtisiiBildir = dto.FiyatArtisiiBildir
            };

            var eklendi = await _urunService.EkleAsync(urun);
            var response = MapToResponse(eklendi);

            return CreatedAtAction(nameof(GetById), new { id = eklendi.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ürün eklenirken hata oluştu.");
            return StatusCode(500, new { hata = "Ürün eklenirken bir hata oluştu." });
        }
    }

    // ── GET /api/urunler/{id} ─────────────────────────
    /// <summary>Belirtilen ürünü fiyat geçmişiyle birlikte getirir</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UrunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var urun = await _urunService.GetByIdAsync(id);
            if (urun is null)
                return NotFound(new { hata = $"Id={id} olan ürün bulunamadı." });

            return Ok(MapToResponse(urun));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ürün {Id} getirilirken hata oluştu.", id);
            return StatusCode(500, new { hata = "Ürün alınırken bir hata oluştu." });
        }
    }

    // ── PUT /api/urunler/{id} ─────────────────────────
    /// <summary>Ürün bilgilerini günceller</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UrunGuncelleRequest dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var guncelUrun = new Urun
            {
                Id = id,
                Ad = dto.Ad.Trim(),
                URL = dto.URL.Trim(),
                Resim = dto.Resim,
                KategoriId = dto.KategoriId,
                HedefFiyati = dto.HedefFiyati,
                FiyatDususuBildir = dto.FiyatDususuBildir,
                FiyatArtisiiBildir = dto.FiyatArtisiiBildir
            };

            var basarili = await _urunService.GuncelleAsync(guncelUrun);
            if (!basarili)
                return NotFound(new { hata = $"Id={id} olan ürün bulunamadı." });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ürün {Id} güncellenirken hata oluştu.", id);
            return StatusCode(500, new { hata = "Ürün güncellenirken bir hata oluştu." });
        }
    }

    // ── DELETE /api/urunler/{id} ──────────────────────
    /// <summary>Ürünü pasif hale getirir (soft-delete)</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var basarili = await _urunService.SilAsync(id);
            if (!basarili)
                return NotFound(new { hata = $"Id={id} olan ürün bulunamadı." });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ürün {Id} silinirken hata oluştu.", id);
            return StatusCode(500, new { hata = "Ürün silinirken bir hata oluştu." });
        }
    }

    // ── Yardımcı Dönüşüm Metodu ───────────────────────

    private static UrunResponse MapToResponse(Urun u) => new()
    {
        Id = u.Id,
        Ad = u.Ad,
        URL = u.URL,
        Resim = u.Resim,
        KategoriId = u.KategoriId,
        KategoriAdi = u.Kategori?.Ad,
        BaslangicFiyati = u.BaslangicFiyati,
        SonFiyati = u.SonFiyati,
        HedefFiyati = u.HedefFiyati,
        FiyatDususuBildir = u.FiyatDususuBildir,
        FiyatArtisiiBildir = u.FiyatArtisiiBildir,
        Aktif = u.Aktif,
        EklendigiTarih = u.EklendigiTarih,
        SonGuncellemeTarihi = u.SonGuncellemeTarihi
    };
}
