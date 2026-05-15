using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiyatTakipWebSitesi.Data.Migrations
{
    /// <inheritdoc />
    public partial class UrunModeliEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UrunModeliId",
                table: "Urunler",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UrunModelleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Resim = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KategoriId = table.Column<int>(type: "int", nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_UrunModeliId",
                table: "Urunler",
                column: "UrunModeliId");

            migrationBuilder.CreateIndex(
                name: "IX_UrunModelleri_KategoriId",
                table: "UrunModelleri",
                column: "KategoriId");

            migrationBuilder.AddForeignKey(
                name: "FK_Urunler_UrunModelleri_UrunModeliId",
                table: "Urunler",
                column: "UrunModeliId",
                principalTable: "UrunModelleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Urunler_UrunModelleri_UrunModeliId",
                table: "Urunler");

            migrationBuilder.DropTable(
                name: "UrunModelleri");

            migrationBuilder.DropIndex(
                name: "IX_Urunler_UrunModeliId",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "UrunModeliId",
                table: "Urunler");
        }
    }
}
