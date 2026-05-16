using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiyatTakipWebSitesi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kategoriler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", nullable: false),
                    Aciklama = table.Column<string>(type: "TEXT", nullable: true),
                    Icon = table.Column<string>(type: "TEXT", nullable: true),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kategoriler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", nullable: false),
                    Soyad = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    TelefonNumarasi = table.Column<string>(type: "TEXT", nullable: true),
                    SifreHash = table.Column<byte[]>(type: "BLOB", nullable: true),
                    SifreTuzu = table.Column<byte[]>(type: "BLOB", nullable: true),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SonGirisTarihi = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Aktif = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmailDogrulandi = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmailBildirimleriniAc = table.Column<bool>(type: "INTEGER", nullable: false),
                    PushBildirimleriniAc = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UrunModelleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", nullable: false),
                    Aciklama = table.Column<string>(type: "TEXT", nullable: true),
                    Resim = table.Column<string>(type: "TEXT", nullable: true),
                    KategoriId = table.Column<int>(type: "INTEGER", nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Aktif = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrunModelleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UrunModelleri_Kategoriler_KategoriId",
                        column: x => x.KategoriId,
                        principalTable: "Kategoriler",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Urunler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", nullable: false),
                    URL = table.Column<string>(type: "TEXT", nullable: false),
                    Resim = table.Column<string>(type: "TEXT", nullable: true),
                    Satici = table.Column<string>(type: "TEXT", nullable: false),
                    ParaBirimi = table.Column<string>(type: "TEXT", nullable: false),
                    KategoriId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UrunModeliId = table.Column<int>(type: "INTEGER", nullable: true),
                    BaslangicFiyati = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    SonFiyati = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    EklendigiTarih = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SonGuncellemeTarihi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Aktif = table.Column<bool>(type: "INTEGER", nullable: false),
                    FiyatDususuBildir = table.Column<bool>(type: "INTEGER", nullable: false),
                    FiyatArtisiiBildir = table.Column<bool>(type: "INTEGER", nullable: false),
                    HedefFiyati = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Urunler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Urunler_Kategoriler_KategoriId",
                        column: x => x.KategoriId,
                        principalTable: "Kategoriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Urunler_Kullanicilar_UserId",
                        column: x => x.UserId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Urunler_UrunModelleri_UrunModeliId",
                        column: x => x.UrunModeliId,
                        principalTable: "UrunModelleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FiyatGecmisleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UrunId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fiyat = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "TEXT", nullable: false),
                    Tarih = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OncekiFiyat = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    FiyatDegisimi = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    FiyatDegisimYüzdesi = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    StokDurumuMevcutMu = table.Column<bool>(type: "INTEGER", nullable: false),
                    Durum = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiyatGecmisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiyatGecmisleri_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Uyarilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UrunId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Baslik = table.Column<string>(type: "TEXT", nullable: false),
                    Mesaj = table.Column<string>(type: "TEXT", nullable: false),
                    Tip = table.Column<int>(type: "INTEGER", nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Okundu = table.Column<bool>(type: "INTEGER", nullable: false),
                    OkunduğuTarih = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uyarilar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Uyarilar_Kullanicilar_UserId",
                        column: x => x.UserId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Uyarilar_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiyatGecmisleri_Tarih",
                table: "FiyatGecmisleri",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_FiyatGecmisleri_UrunId",
                table: "FiyatGecmisleri",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_Email",
                table: "Kullanicilar",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_KategoriId",
                table: "Urunler",
                column: "KategoriId");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_UrunModeliId",
                table: "Urunler",
                column: "UrunModeliId");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_UserId",
                table: "Urunler",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UrunModelleri_KategoriId",
                table: "UrunModelleri",
                column: "KategoriId");

            migrationBuilder.CreateIndex(
                name: "IX_Uyarilar_OlusturulmaTarihi",
                table: "Uyarilar",
                column: "OlusturulmaTarihi");

            migrationBuilder.CreateIndex(
                name: "IX_Uyarilar_UrunId",
                table: "Uyarilar",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Uyarilar_UserId",
                table: "Uyarilar",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiyatGecmisleri");

            migrationBuilder.DropTable(
                name: "Uyarilar");

            migrationBuilder.DropTable(
                name: "Urunler");

            migrationBuilder.DropTable(
                name: "Kullanicilar");

            migrationBuilder.DropTable(
                name: "UrunModelleri");

            migrationBuilder.DropTable(
                name: "Kategoriler");
        }
    }
}
