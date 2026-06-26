using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurgeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurgeRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodYear = table.Column<short>(type: "smallint", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "tinyint", nullable: false),
                    PurgedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    TotalIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalExpense = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpenseCount = table.Column<int>(type: "int", nullable: false),
                    IncomeCount = table.Column<int>(type: "int", nullable: false),
                    CategorySummaryJson = table.Column<string>(type: "nvarchar(4000)", nullable: false),
                    CsvFileName = table.Column<string>(type: "nvarchar(255)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurgeRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurgeRecord_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurgeRecord_UserId",
                table: "PurgeRecord",
                column: "UserId",
                filter: "[DeletedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurgeRecord");
        }
    }
}
