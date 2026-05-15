using System.Security.Cryptography;
using System.Text;

namespace FiyatTakipWebSitesi.Services;

public class ResimCacheService(IWebHostEnvironment env, HttpClient httpClient, ILogger<ResimCacheService> logger)
{
    private readonly IWebHostEnvironment _env = env;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<ResimCacheService> _logger = logger;

    public async Task<string?> ResimOnbellegeAlAsync(string? resimUrl)
    {
        if (string.IsNullOrWhiteSpace(resimUrl))
            return null;

        // Zaten yerel bir resimse dokunma
        if (!resimUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return resimUrl;

        try
        {
            var cacheRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var cacheDir = Path.Combine(cacheRoot, "images", "cache");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            // Uzantı belirleme
            var ext = ".jpg";
            if (Uri.TryCreate(resimUrl, UriKind.Absolute, out var uri))
            {
                var pathExt = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(pathExt) && pathExt.Length <= 5)
                    ext = pathExt;
            }

            // URL'ye göre benzersiz dosya adı oluştur
            var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(resimUrl));
            var fileName = Convert.ToHexString(hashBytes).ToLowerInvariant() + ext;
            var fullPath = Path.Combine(cacheDir, fileName);
            var relativePath = $"/images/cache/{fileName}";

            if (File.Exists(fullPath))
                return relativePath;

            // Hotlink engelini aşmak için header ekle
            using var request = new HttpRequestMessage(HttpMethod.Get, resimUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.IsSuccessStatusCode)
            {
                using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                return relativePath;
            }

            _logger.LogWarning("[ResimCache] İndirme başarısız ({Status}) | URL: {Url}", response.StatusCode, resimUrl);
            return resimUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ResimCache] Hata: {Msg} | URL: {Url}", ex.Message, resimUrl);
            return resimUrl;
        }
    }
}
