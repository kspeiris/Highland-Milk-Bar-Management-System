using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HighlandMilkBar.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Step 09 – Customer Management &amp; Loyalty Foundation schema additions.
    ///
    /// Extends the existing <c>Customers</c> table with a unique customer code, type,
    /// alternative phone, city, date of birth, notes and an update timestamp, and adds the
    /// append-only <c>LoyaltyTransactions</c> ledger. Existing customer and sales rows are
    /// preserved: new columns use safe defaults and customer codes are backfilled from the
    /// primary key before the unique index is created. The authoritative model snapshot lives
    /// in <see cref="AppDbContextModelSnapshot"/>; this migration only records the schema change.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261115130000_AddCustomerManagementLoyalty")]
    public partial class AddCustomerManagementLoyalty : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Customers ----
            migrationBuilder.AddColumn<string>(
                name: "CustomerCode", table: "Customers", type: "TEXT", maxLength: 32, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<int>(
                name: "Type", table: "Customers", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(
                name: "AlternativePhone", table: "Customers", type: "TEXT", maxLength: 32, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "City", table: "Customers", type: "TEXT", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth", table: "Customers", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "Notes", table: "Customers", type: "TEXT", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt", table: "Customers", type: "TEXT", nullable: true);

            // Backfill an internal customer code from the (unique) primary key so the new
            // unique index can be created safely on existing data.
            migrationBuilder.Sql(
                "UPDATE Customers SET CustomerCode = 'CUS-' || printf('%08d', Id) WHERE CustomerCode IS NULL OR CustomerCode = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerCode",
                table: "Customers",
                column: "CustomerCode",
                unique: true);
            migrationBuilder.CreateIndex(name: "IX_Customers_Type", table: "Customers", column: "Type");
            migrationBuilder.CreateIndex(name: "IX_Customers_IsActive", table: "Customers", column: "IsActive");
            migrationBuilder.CreateIndex(name: "IX_Customers_Name", table: "Customers", column: "Name");

            // ---- LoyaltyTransactions ----
            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    SalesReturnId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    BalanceAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_SalesReturns_SalesReturnId",
                        column: x => x.SalesReturnId,
                        principalTable: "SalesReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_CustomerId",
                table: "LoyaltyTransactions",
                column: "CustomerId");
            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_CustomerId_TransactionDate",
                table: "LoyaltyTransactions",
                columns: new[] { "CustomerId", "TransactionDate" });
            // Prevent awarding points twice for the same sale (earned entries only).
            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_SaleId",
                table: "LoyaltyTransactions",
                column: "SaleId",
                unique: true,
                filter: "[SaleId] IS NOT NULL AND [Type] = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LoyaltyTransactions");

            migrationBuilder.DropIndex(name: "IX_Customers_Name", table: "Customers");
            migrationBuilder.DropIndex(name: "IX_Customers_IsActive", table: "Customers");
            migrationBuilder.DropIndex(name: "IX_Customers_Type", table: "Customers");
            migrationBuilder.DropIndex(name: "IX_Customers_CustomerCode", table: "Customers");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Customers");
            migrationBuilder.DropColumn(name: "Notes", table: "Customers");
            migrationBuilder.DropColumn(name: "DateOfBirth", table: "Customers");
            migrationBuilder.DropColumn(name: "City", table: "Customers");
            migrationBuilder.DropColumn(name: "AlternativePhone", table: "Customers");
            migrationBuilder.DropColumn(name: "Type", table: "Customers");
            migrationBuilder.DropColumn(name: "CustomerCode", table: "Customers");
        }

        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            // The current model is tracked by AppDbContextModelSnapshot; nothing to build here.
        }
    }
}