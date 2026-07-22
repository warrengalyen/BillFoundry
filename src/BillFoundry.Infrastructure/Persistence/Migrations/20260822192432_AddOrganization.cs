using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillFoundry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TaxIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultCurrency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    DefaultPaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    DefaultInvoicePrefix = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    DefaultEstimatePrefix = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    DefaultInvoiceNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DefaultPaymentInstructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LogoStoredFileName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LogoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LogoSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.CheckConstraint("CK_Organizations_PaymentTerms", "[DefaultPaymentTermsDays] >= 0 AND [DefaultPaymentTermsDays] <= 365");
                    table.CheckConstraint("CK_Organizations_SingletonId", "[Id] = '8f3e2c1a-9b7d-4e6f-a1c2-5d8e9f0a1b2c'");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
