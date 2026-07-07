using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModuleA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderReceipts",
                schema: "modulea",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderReceipts_IntegrationEventId",
                schema: "modulea",
                table: "OrderReceipts",
                column: "IntegrationEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderReceipts",
                schema: "modulea");
        }
    }
}
