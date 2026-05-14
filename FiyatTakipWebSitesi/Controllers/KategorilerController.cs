// =====================================================
// KategorilerController.cs
// /api/kategoriler uç noktalarını yönetir.
// Kategori listeleme, tek kategori getirme ve bir
// kategoriye ait ürün modellerini listeleme işlemlerini sunar.
//
// Endpoint'ler:
//   GET /api/kategoriler                  → Tüm kategoriler
//   GET /api/kategoriler/{id}             → Tek kategori
//   GET /api/kategoriler/{id}/modeller    → Kategoriye ait ürün modelleri
// =====================================================

using FiyatTakipWebSitesi.DTOs;
using FiyatTakipWebSitesi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiyatTakipWebSitesi.Controllers;

[ApiController]
[Route("api/kategoriler")]
[Produces("application/json")]
public class KategorilerController(
    KategoriService kategoriService,
    ILogger<KategorilerController> logger) : ControllerBase
{
    private readonly KategoriService _kategoriService = kategoriService;
    private readonly ILogger<KategorilerController> _logger = logger;

    // ── GET /api/kategoriler ──────────────────────────
    /// <summary>Tüm kategorileri listeler</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var kategoriler = await _kategoriService.GetAllAsync();
            var response = kategoriler.Select(k => new
            {
                k.Id,
                k.Ad,
                k.Aciklama,
                k.Icon,
                UrunSayisi = k.Urunler.Count(u => u.Aktif)
            });
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kategoriler listelenirken hata oluştu.");
            return StatusCode(500, new { hata = "Kategoriler alınırken bir hata oluştu." });
        }
    }

    // ── GET /api/kategoriler/{id} ─────────────────────
    /// <summary>Belirtilen kategoriyi getirir</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var kategori = await _kategoriService.GetByIdAsync(id);
            if (kategori is null)
                return NotFound(new { hata = $"Id={id} olan kategori bulunamadı." });

            return Ok(new
            {
                kategori.Id,
                kategori.Ad,
                kategori.Aciklama,
                kategori.Icon,
                UrunSayisi = kategori.Urunler.Count(u => u.Aktif)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kategori {Id} getirilirken hata oluştu.", id);
            return StatusCode(500, new { hata = "Kategori alınırken bir hata oluştu." });
        }
    }

    // ── GET /api/kategoriler/{id}/modeller ────────────
    /// <summary>
    /// Belirtilen kategoriye ait ürün modellerini,
    /// her modelin satıcı ve fiyat bilgileriyle birlikte döner.
    /// </summary>
    [HttpGet("{id:int}/modeller")]
    [ProducesResponseType(typeof(IEnumerable<UrunModeliResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModeller(int id)
    {
        try
        {
            // Önce kategorinin var olduğunu doğrula
            var kategori = await _kategoriService.GetByIdAsync(id);
            if (kategori is null)
                return NotFound(new { hata = $"Id={id} olan kategori bulunamadı." });

            var modeller = await _kategoriService.GetModellerByKategoriAsync(id);

            var response = modeller.Select(m => new UrunModeliResponse
            {
                Id         = m.Id,
                Ad         = m.Ad,
                Resim      = m.Resim,
                KategoriId = m.KategoriId,
                KategoriAdi = m.Kategori?.Ad,
                Listeler   = [.. m.Listeler.Select(u => new ModelListelemesiResponse
                {
                    Id                   = u.Id,
                    Satici               = u.Satici,
                    SonFiyat             = u.SonFiyati,
                    ParaBirimi           = u.ParaBirimi,
                    URL                  = u.URL,
                    Aktif                = u.Aktif,
                    SonGuncellemeTarihi  = u.SonGuncellemeTarihi
                })]
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kategori {Id} modelleri alınırken hata oluştu.", id);
            return StatusCode(500, new { hata = "Modeller alınırken bir hata oluştu." });
        }
    }
}
