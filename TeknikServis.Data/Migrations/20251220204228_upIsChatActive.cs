using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServis.Data.Migrations
{
    /// <inheritdoc />
    public partial class upIsChatActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsChatActive",
                table: "CompanySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsChatActive",
                table: "CompanySettings");
        }
    }
}
