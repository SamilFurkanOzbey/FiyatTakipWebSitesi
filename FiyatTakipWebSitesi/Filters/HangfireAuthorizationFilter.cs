using Hangfire.Dashboard;

namespace FiyatTakipWebSitesi.Filters;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        
        // Sadece giriş yapmış kullanıcılara (veya belirli bir role) izin verir.
        // Projenin yetkilendirme mekanizmasına göre güncellenebilir.
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
