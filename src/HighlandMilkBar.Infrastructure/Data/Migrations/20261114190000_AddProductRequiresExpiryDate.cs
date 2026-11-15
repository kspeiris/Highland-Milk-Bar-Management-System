using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HighlandMilkBar.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Adds the product-level <c>RequiresExpiryDate</c> flag so the application can apply
    /// expiry rules only to products that need them (perishables) instead of every product.
    /// Existing rows default to <c>false</c>. The authoritative model snapshot lives in
    /// <see cref="AppDbContextModelSnapshot"/>; this migration only records the schema change.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261114190000_AddProductRequiresExpiryDate")]
    public partial class AddProductRequiresExpiryDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresExpiryDate",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RequiresExpiryDate", table: "Products");
        }

        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            // The current model is tracked by AppDbContextModelSnapshot; nothing to build here.
        }
    }
}