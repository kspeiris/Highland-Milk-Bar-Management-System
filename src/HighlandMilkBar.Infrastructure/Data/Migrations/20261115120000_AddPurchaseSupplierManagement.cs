using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HighlandMilkBar.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Step 08 – Purchase &amp; Supplier Management schema additions.
    ///
    /// Adds supplier registration/contact fields, an internal unique purchase number,
    /// per-line discount and unit snapshots, purchase-level additional charges and due
    /// date, a separate payment-status column, and the supplier-invoice-unique-per-supplier
    /// and supplier-code-unique indexes. The authoritative model snapshot lives in
    /// <see cref="AppDbContextModelSnapshot"/>; this migration only records the schema change
    /// so it can be applied to existing databases without data loss.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261115120000_AddPurchaseSupplierManagement")]
    public partial class AddPurchaseSupplierManagement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Suppliers ----
            migrationBuilder.AddColumn<string>(
                name: "Code", table: "Suppliers", type: "TEXT", maxLength: 64, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "BusinessRegNo", table: "Suppliers", type: "TEXT", maxLength: 64, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "TaxRegNo", table: "Suppliers", type: "TEXT", maxLength: 64, nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt", table: "Suppliers", type: "TEXT", nullable: true);
            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL AND [Code] <> ''");

            // ---- Purchases ----
            migrationBuilder.AddColumn<string>(
                name: "PurchaseNumber", table: "Purchases", type: "TEXT", maxLength: 64, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate", table: "Purchases", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedDate", table: "Purchases", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalCharges", table: "Purchases", type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus", table: "Purchases", type: "INTEGER", nullable: false, defaultValue: 0);

            // InvoiceNumber stays NOT NULL (SQLite cannot alter a column in place); a blank
            // value means "no supplier invoice supplied". The unique-per-supplier index below
            // ignores blanks so multiple invoice-less purchases are allowed.

            // Backfill an internal purchase number from the (unique) primary key so the
            // new unique index can be created safely on existing data.
            migrationBuilder.Sql(
                "UPDATE Purchases SET PurchaseNumber = 'PUR-' || printf('%08d', Id) WHERE PurchaseNumber IS NULL OR PurchaseNumber = '';");
            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PurchaseNumber",
                table: "Purchases",
                column: "PurchaseNumber",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Purchases_SupplierId_InvoiceNumber",
                table: "Purchases",
                columns: new[] { "SupplierId", "InvoiceNumber" },
                unique: true,
                filter: "[InvoiceNumber] IS NOT NULL AND [InvoiceNumber] <> ''");

            // ---- PurchaseItems ----
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount", table: "PurchaseItems", type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<string>(
                name: "Unit", table: "PurchaseItems", type: "TEXT", maxLength: 32, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Unit", table: "PurchaseItems");
            migrationBuilder.DropColumn(name: "DiscountAmount", table: "PurchaseItems");

            migrationBuilder.DropIndex(name: "IX_Purchases_SupplierId_InvoiceNumber", table: "Purchases");
            migrationBuilder.DropIndex(name: "IX_Purchases_PurchaseNumber", table: "Purchases");

            migrationBuilder.DropColumn(name: "PaymentStatus", table: "Purchases");
            migrationBuilder.DropColumn(name: "AdditionalCharges", table: "Purchases");
            migrationBuilder.DropColumn(name: "ReceivedDate", table: "Purchases");
            migrationBuilder.DropColumn(name: "DueDate", table: "Purchases");
            migrationBuilder.DropColumn(name: "PurchaseNumber", table: "Purchases");

            migrationBuilder.DropIndex(name: "IX_Suppliers_Code", table: "Suppliers");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Suppliers");
            migrationBuilder.DropColumn(name: "TaxRegNo", table: "Suppliers");
            migrationBuilder.DropColumn(name: "BusinessRegNo", table: "Suppliers");
            migrationBuilder.DropColumn(name: "Code", table: "Suppliers");
        }

        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            // The current model is tracked by AppDbContextModelSnapshot; nothing to build here.
        }
    }
}