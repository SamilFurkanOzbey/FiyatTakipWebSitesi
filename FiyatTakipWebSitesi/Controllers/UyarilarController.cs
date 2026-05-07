// =====================================================
// UyarilarController.cs
// /api/uyarilar uç noktasını yönetir. Kullanıcıya ait
// uyarıları listeler, tekil veya toplu okundu işareti
// koyar. Tüm endpoint'ler [Authorize] ile korunur.
// GET /api/uyarilar                 → Kullanıcı uyarıları
// PUT /api/uyarilar/{id}/okundu     → Tekil okundu
// PUT /api/uyarilar/tumu-okundu     → Toplu okundu
// =====================================================

using System.Security.Claims;
using FiyatTakipWebSitesi.DTOs;
using FiyatTakipWebSitesi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiyatTakipWebSitesi.Controllers;

[ApiController]
[Route("api/uyarilar")]
[Produces("application/json")]
[Authorize]
public class UyarilarController : ControllerBase
{
    private readonly UyariService _uyariService;
    private readonly ILogger<UyarilarController> _logger;

    public UyarilarController(UyariService uyariService, ILogger<UyarilarController> logger)
    {
        _uyariService = uyariService;
        _logger = logger;
    }

    // ── GET /api/uyarilar ─────────────────────────────
    /// <summary>Oturum açmış kullanıcının uyarılarını listeler</summary>
    /// <param name="sadeceokunmamis">true → sadece okunmamışlar</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UyariResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool? sadeceokunmamis)
    {
        var kullaniciId = KullaniciIdAl();
        if (kullaniciId is null)
            return Unauthorized(new { hata = "Geçersiz token." });

        try
        {
            var uyarilar = await _uyariService.GetByKullaniciAsync(kullaniciId.Value, sadeceokunmamis);
            var yanit = uyarilar.Select(u => new UyariResponse
            {
                Id                  = u.Id,
                UrunId              = u.UrunId,
                UrunAdi             = u.Urun?.Ad,
                Baslik              = u.Baslik,
                Mesaj               = u.Mesaj,
                Tip                 = u.Tip,
                OlusturulmaTarihi   = u.OlusturulmaTarihi,
                Okundu              = u.Okundu,
                OkunduguTarih       = u.OkunduğuTarih
            });

            return Ok(yanit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uyarılar listelenirken hata oluştu.");
            return StatusCode(500, new { hata = "Uyarılar alınırken bir hata oluştu." });
        }
    }

    // ── PUT /api/uyarilar/{id}/okundu ─────────────────
    /// <summary>Belirtilen uyarıyı okundu olarak işaretler</summary>
    [HttpPut("{id:int}/okundu")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OkunduIsaretle(int id)
    {
        var kullaniciId = KullaniciIdAl();
        if (kullaniciId is null)
            return Unauthorized(new { hata = "Geçersiz token." });

        try
        {
            var basarili = await _uyariService.OkunduIsaretle(id, kullaniciId.Value);
            if (!basarili)
                return NotFound(new { hata = $"Id={id} olan uyarı bulunamadı." });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uyarı {Id} okundu işaretlenirken hata oluştu.", id);
            return StatusCode(500, new { hata = "İşlem sırasında bir hata oluştu." });
        }
    }

    // ── PUT /api/uyarilar/tumu-okundu ─────────────────
    /// <summary>Oturum açmış kullanıcının tüm okunmamış uyarılarını okundu yapar</summary>
    [HttpPut("tumu-okundu")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TumunuOkunduIsaretle()
    {
        var kullaniciId = KullaniciIdAl();
        if (kullaniciId is null)
            return Unauthorized(new { hata = "Geçersiz token." });

        try
        {
            var adet = await _uyariService.TumunuOkunduIsaretle(kullaniciId.Value);
            return Ok(new { okunduIsaretlenenAdet = adet });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Toplu okundu işareti sırasında hata oluştu.");
            return StatusCode(500, new { hata = "İşlem sırasında bir hata oluştu." });
        }
    }

    // ── Yardımcı ──────────────────────────────────────
    private int? KullaniciIdAl()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
