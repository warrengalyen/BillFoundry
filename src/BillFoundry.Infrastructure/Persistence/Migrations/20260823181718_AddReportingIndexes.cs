using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillFoundry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status_DueDate",
                table: "Invoices",
                columns: new[] { "Status", "DueDate" })
                .Annotation("SqlServer:Include", new[] { "BalanceDue" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_PaymentDate",
                table: "InvoicePayments",
                column: "PaymentDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_Status_DueDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoicePayments_PaymentDate",
                table: "InvoicePayments");
        }
    }
}
