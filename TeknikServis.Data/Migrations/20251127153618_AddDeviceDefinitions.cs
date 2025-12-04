using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceBrand",
                table: "ServiceTickets");

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceBrandId",
                table: "ServiceTickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceTypeId",
                table: "ServiceTickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "DeviceBrands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceBrands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_DeviceBrandId",
                table: "ServiceTickets",
                column: "DeviceBrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_DeviceTypeId",
                table: "ServiceTickets",
                column: "DeviceTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceTickets_DeviceBrands_DeviceBrandId",
                table: "ServiceTickets",
                column: "DeviceBrandId",
                principalTable: "DeviceBrands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceTickets_DeviceTypes_DeviceTypeId",
                table: "ServiceTickets",
                column: "DeviceTypeId",
                principalTable: "DeviceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceTickets_DeviceBrands_DeviceBrandId",
                table: "ServiceTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceTickets_DeviceTypes_DeviceTypeId",
                table: "ServiceTickets");

            migrationBuilder.DropTable(
                name: "DeviceBrands");

            migrationBuilder.DropTable(
                name: "DeviceTypes");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_DeviceBrandId",
                table: "ServiceTickets");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_DeviceTypeId",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "DeviceBrandId",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "DeviceTypeId",
                table: "ServiceTickets");

            migrationBuilder.AddColumn<string>(
                name: "DeviceBrand",
                table: "ServiceTickets",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
