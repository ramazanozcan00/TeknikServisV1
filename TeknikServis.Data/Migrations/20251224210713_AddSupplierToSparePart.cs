using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierToSparePart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "SpareParts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpareParts_SupplierId",
                table: "SpareParts",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_SpareParts_CompanySettings_SupplierId",
                table: "SpareParts",
                column: "SupplierId",
                principalTable: "CompanySettings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpareParts_CompanySettings_SupplierId",
                table: "SpareParts");

            migrationBuilder.DropIndex(
                name: "IX_SpareParts_SupplierId",
                table: "SpareParts");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "SpareParts");
        }
    }
}
