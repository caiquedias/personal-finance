using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseRecurringAndSourceExpenseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "Expense",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceExpenseId",
                table: "Expense",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expense_SourceExpenseId",
                table: "Expense",
                column: "SourceExpenseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expense_Expense_SourceExpenseId",
                table: "Expense",
                column: "SourceExpenseId",
                principalTable: "Expense",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expense_Expense_SourceExpenseId",
                table: "Expense");

            migrationBuilder.DropIndex(
                name: "IX_Expense_SourceExpenseId",
                table: "Expense");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "Expense");

            migrationBuilder.DropColumn(
                name: "SourceExpenseId",
                table: "Expense");
        }
    }
}
