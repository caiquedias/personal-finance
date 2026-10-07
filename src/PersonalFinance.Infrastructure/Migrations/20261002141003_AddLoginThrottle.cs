using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginThrottle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoginThrottle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    WindowStart = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginThrottle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginThrottle_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginThrottle_UserId_IpAddress",
                table: "LoginThrottle",
                columns: new[] { "UserId", "IpAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginThrottle_WindowStart",
                table: "LoginThrottle",
                column: "WindowStart");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginThrottle");
        }
    }
}
