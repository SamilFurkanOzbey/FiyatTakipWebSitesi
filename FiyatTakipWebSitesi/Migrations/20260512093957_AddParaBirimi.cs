using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiyatTakipWebSitesi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParaBirimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParaBirimi",
                table: "Urunler",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ParaBirimi",
                table: "FiyatGecmisleri",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParaBirimi",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "ParaBirimi",
                table: "FiyatGecmisleri");
        }
    }
}
