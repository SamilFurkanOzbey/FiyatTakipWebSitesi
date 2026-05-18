# 🛒 Fiyat Takip Sistemi

Hepsiburada, Çiçeksepeti, Teknosa, n11 ve PttAVM'deki ürünlerin fiyatlarını tek bir ekrandan takip etmenizi sağlayan, fiyat düştüğünde otomatik e-posta gönderen yeni nesil, akıllı fiyat takip asistanı.

![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Server-5C2D91?style=for-the-badge&logo=blazor)
![Selenium](https://img.shields.io/badge/Selenium-WebDriver-43B02A?style=for-the-badge&logo=selenium)
![Hangfire](https://img.shields.io/badge/Hangfire-Background_Jobs-007acc?style=for-the-badge)

---

## 📌 Projenin Amacı

E-ticaret platformlarında fiyatlar anlık ve dinamik olarak değişmektedir. Tüketicilerin aradıkları ürünlerin en uygun fiyatlı haline ulaşması ciddi bir manuel takip ve zaman kaybı gerektirir. Bu proje, kullanıcının istediği ürünleri kategorize ederek tek bir panelden izleyebileceği, fiyat grafiklerini görebileceği ve hedef fiyatına düştüğünde e-posta ile otomatik uyarı alabileceği tam otomatize bir sistem sunar.

---

## 🚀 Öne Çıkan Özellikler

- **Tek Ekrandan Karşılaştırma** — Farklı e-ticaret sitelerindeki aynı ürünleri otomatik olarak eşleştirir ve en ucuzunu bulur.
- **Otomatik Fiyat Güncelleme** — Selenium WebDriver ve Hangfire kullanılarak arka planda siteler taranır ve fiyatlar güncel tutulur.
- **Akıllı E-Posta Bildirimleri** — Kullanıcının takip ettiği ürünün fiyatı düştüğünde anında e-posta gönderilir.
- **Fiyat Geçmişi & Grafik** — Her ürün için tarihsel fiyat grafiği görüntülenir.
- **Manuel URL Takibi** — Katalogda olmayan bir ürünü link yapıştırarak takibe ekle.
- **Premium Karanlık Tema (Dark Mode)** — Glassmorphism ve neon detaylarla zenginleştirilmiş, kullanıcı dostu modern Blazor arayüzü.
- **JWT Tabanlı Kimlik Doğrulama** — Çoklu kullanıcı destekli, güvenli (stateless) oturum yönetimi.
- **Kategori Yönetimi** — Elektronik, Ev & Yaşam, Moda, Kozmetik, Otomotiv ve daha fazlası.

---

## 🏗️ Nesne Yönelimli Programlama (OOP) ve Mimari Yapı

Bu proje, Nesne Yönelimli Tasarım prensiplerine (Object-Oriented Design) sıkı sıkıya bağlı kalarak geliştirilmiştir:

- **Sınıf (Class) Yapısı** — `Urun`, `Kategori`, `UrunModeli`, `TakipModeli`, `Kullanici` başta olmak üzere birbirleriyle ilişkisel (One-to-Many) olarak bağlanan 10'dan fazla sınıf bulunmaktadır.
- **Encapsulation (Kapsülleme)** — Tüm modellerdeki veri alanları `get; set;` property'leri ile dış erişime karşı güvenli hale getirilmiştir.
- **Inheritance (Kalıtım)** — Arayüz bileşenleri `LayoutComponentBase` sınıfından, veritabanı bağlamı ise Entity Framework'ün `DbContext` sınıfından kalıtım almaktadır.
- **Polymorphism (Çok Biçimlilik)** — E-posta gönderimi için `IEmailService` arayüzü oluşturulmuş; geliştirme ortamı için `MockEmailService`, canlı ortam için `SmtpEmailService` olmak üzere iki farklı biçimde implemente edilmiştir.
- **Exception Handling (Hata Yönetimi)** — Özel bir `GlobalExceptionHandler` (middleware) katmanı yazılmış ve kritik bloklar `try-catch-finally` mimarisiyle güvence altına alınmıştır.
- **CRUD ve Veri Saklama** — Veriler Entity Framework Core ORM aracı kullanılarak SQLite veritabanında kalıcı olarak saklanmaktadır.

---

## 👥 Ekip ve Görev Dağılımı

Proje, 3 kişilik bir ekip tarafından modüler bir iş bölümü ile geliştirilmiştir:

1. *Mehmet Ali Toros — Frontend & UI/UX Developer**
   - Blazor Server arayüz bileşenlerinin kodlanması.
   - Premium Karanlık Tema (Dark Mode) tasarımı ve entegrasyonu.
   - Kategori filtreleme, ürün listeleme ve "URL ile Ekle" modüllerinin ön yüz mantığı.

2. *Ahmet Sevban Kurban — Backend & Security**
   - JWT (JSON Web Token) tabanlı üyelik sisteminin yazılması.
   - Entity Framework Core ve SQLite veritabanı mimarisinin (Code-First) kurulması.
   - Hangfire ile arka plan (Background Job) görevlerinin yönetilmesi.

3. *Şamil Furkan Özbey — Core Services & Data Engineer**
   - `IEmailService` üzerinden Polymorphic e-posta uyarı sisteminin entegrasyonu.
   - `GlobalExceptionHandler` ile hata yönetimi ve test süreçleri.
   - Selenium WebDriver ile Web Scraping (Veri Kazıma) botlarının geliştirilmesi.

---

## 🛠️ Teknoloji Yığını

| Katman | Teknoloji |
|--------|-----------|
| Framework | .NET 10 · Blazor Server (InteractiveServer) |
| Veritabanı | SQLite · EF Core 10 |
| Scraping | Selenium WebDriver 4 · ChromeDriver · HtmlAgilityPack |
| Zamanlama | Hangfire 1.8 (Memory Storage) |
| Auth | JWT Bearer |
| API Docs | Scalar / OpenAPI |
| Mapping | Riok.Mapperly |
| Resilience | Polly |

---

## 📁 Proje Yapısı

```
FiyatTakipWebSitesi/
├── Components/
│   ├── Layout/          # NavMenu, MainLayout
│   └── Pages/           # Blazor sayfaları (Home, KategoriUrunler, ModelDetay, Takip, Grafik…)
├── Controllers/         # REST API uç noktaları (Auth, Urunler, FiyatGecmisi, Uyarilar…)
├── Data/                # ApplicationDbContext + Migrations
├── DTOs/                # Request/Response nesneleri
├── Jobs/                # FiyatGuncellemeJob (Hangfire)
├── Models/              # EF Core entity'leri (Urun, UrunModeli, Kategori, Kullanici…)
├── Repositories/        # Generic repository pattern
├── Services/            # İş mantığı (ScraperService, UrunService, UyariService…)
├── Filters/             # Global exception handler, Hangfire auth filter
└── wwwroot/             # Statik dosyalar
```

---

## ⚙️ Kurulum ve Çalıştırma

Projenin yerel ortamınızda çalışması için sisteminizde **.NET 10 SDK** ve **Google Chrome** kurulu olmalıdır.

```bash
# 1. Repoyu klonlayın
git clone https://github.com/SamilFurkanOzbey/FiyatTakipWebSitesi.git
cd FiyatTakipWebSitesi

# 2. Bağımlılıkları yükleyin
dotnet restore

# 3. Uygulamayı başlatın (migration + seed otomatik çalışır)
dotnet run --project FiyatTakipWebSitesi
```

Uygulama varsayılan olarak `https://localhost:7164` adresinde açılır.

---

## 🔧 Yapılandırma

`appsettings.json` içindeki alanları düzenleyin:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=FiyatTakip.db"
  },
  "Jwt": {
    "Key": "<en az 32 karakter gizli anahtar>",
    "Issuer": "FiyatTakipWebSitesi",
    "Audience": "FiyatTakipKullanicilari",
    "ExpireMinutes": 1440
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "User": "ornek@gmail.com",
    "Password": "uygulama-sifresi",
    "From": "ornek@gmail.com",
    "FromName": "ŞAM Bildirim"
  }
}
```

> **Not:** `Smtp.Host` boş bırakılırsa sistem gerçek mail göndermek yerine konsola yazar (geliştirme modu).

---

## 📊 Hangfire Dashboard

Zamanlanmış scraping işleri `https://localhost:7164/hangfire` adresinden yönetilir.  
Geliştirme ortamında herkese açık; canlı ortamda kimlik doğrulaması gerekir.

---

## 📖 API Referansı

Scalar arayüzüne geliştirme ortamında `https://localhost:7164/scalar/v1` adresinden ulaşılabilir.

---

## 📄 Lisans

Bu proje eğitim amaçlı geliştirilmiştir.
