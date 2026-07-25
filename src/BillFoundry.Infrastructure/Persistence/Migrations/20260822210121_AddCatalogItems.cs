using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillFoundry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Sku = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    UnitType = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    DefaultUnitPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IsTaxable = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                    table.CheckConstraint("CK_CatalogItems_UnitPrice", "[DefaultUnitPrice] >= 0");
                    table.CheckConstraint("CK_CatalogItems_UnitType", "[UnitType] IN ('Hour', 'Day', 'Item', 'FlatFee')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_IsActive",
                table: "CatalogItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_Name",
                table: "CatalogItems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_Sku",
                table: "CatalogItems",
                column: "Sku",
                unique: true,
                filter: "[Sku] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_UnitType",
                table: "CatalogItems",
                column: "UnitType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItems");
        }
    }
}
