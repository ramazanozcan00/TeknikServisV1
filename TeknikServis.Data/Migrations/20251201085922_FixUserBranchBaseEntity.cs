using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServis.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixUserBranchBaseEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserBranches",
                table: "UserBranches");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "UserBranches",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "UserBranches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "UserBranches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "UserBranches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserBranches",
                table: "UserBranches",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_UserId_BranchId",
                table: "UserBranches",
                columns: new[] { "UserId", "BranchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserBranches",
                table: "UserBranches");

            migrationBuilder.DropIndex(
                name: "IX_UserBranches_UserId_BranchId",
                table: "UserBranches");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "UserBranches");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "UserBranches");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "UserBranches");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "UserBranches");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserBranches",
                table: "UserBranches",
                columns: new[] { "UserId", "BranchId" });
        }
    }
}
