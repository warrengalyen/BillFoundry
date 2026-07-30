using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillFoundry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ClientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    PurchaseOrder = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PaymentInstructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Discount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    TaxRatePercent = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    TaxableSubtotal = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    BalanceDue = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    SourceEstimateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.CheckConstraint("CK_Invoices_AmountPaid", "[AmountPaid] >= 0 AND [AmountPaid] <= [Total]");
                    table.CheckConstraint("CK_Invoices_BalanceDue", "[BalanceDue] >= 0");
                    table.CheckConstraint("CK_Invoices_Discount", "[Discount] >= 0 AND [Discount] <= [Subtotal]");
                    table.CheckConstraint("CK_Invoices_DueDate", "[DueDate] >= [IssueDate]");
                    table.CheckConstraint("CK_Invoices_Status", "[Status] IN ('Draft', 'Sent', 'PartiallyPaid', 'Paid', 'Overdue', 'Void')");
                    table.CheckConstraint("CK_Invoices_TaxRate", "[TaxRatePercent] >= 0 AND [TaxRatePercent] <= 100");
                    table.ForeignKey(
                        name: "FK_Invoices_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Estimates_SourceEstimateId",
                        column: x => x.SourceEstimateId,
                        principalTable: "Estimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IsTaxable = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.CheckConstraint("CK_InvoiceLines_LineAmount", "[LineAmount] >= 0");
                    table.CheckConstraint("CK_InvoiceLines_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InvoiceLines_SortOrder", "[SortOrder] >= 0");
                    table.CheckConstraint("CK_InvoiceLines_Unit", "[Unit] IN ('Hour', 'Day', 'Item', 'FlatFee')");
                    table.CheckConstraint("CK_InvoiceLines_UnitPrice", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_InvoiceLines_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "DocumentSequences",
                columns: new[] { "Kind", "NextValue" },
                values: new object[] { "Invoice", 1 });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_CatalogItemId",
                table: "InvoiceLines",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId_SortOrder",
                table: "InvoiceLines",
                columns: new[] { "InvoiceId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientId",
                table: "Invoices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IssueDate",
                table: "Invoices",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                table: "Invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PurchaseOrder",
                table: "Invoices",
                column: "PurchaseOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Sequence",
                table: "Invoices",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SourceEstimateId",
                table: "Invoices",
                column: "SourceEstimateId",
                unique: true,
                filter: "[SourceEstimateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DeleteData(
                table: "DocumentSequences",
                keyColumn: "Kind",
                keyValue: "Invoice");
        }
    }
}
