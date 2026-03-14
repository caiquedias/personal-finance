using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── LOOKUP TABLES ─────────────────────────────────────────────────────
            // Criadas antes das entidades de domínio por serem referenciadas como FK

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Role", x => x.Id));

            migrationBuilder.CreateTable(
                name: "SourceType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_SourceType", x => x.Id));

            migrationBuilder.CreateTable(
                name: "FortnightType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_FortnightType", x => x.Id));

            migrationBuilder.CreateTable(
                name: "PaymentStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_PaymentStatus", x => x.Id));

            // ── SEED — LOOKUP TABLES ──────────────────────────────────────────────

            migrationBuilder.InsertData("Role", new[] { "Id", "Name", "Description" }, new object[,]
            {
            { 1, "Admin", "Administrador do sistema — acesso total"       },
            { 2, "User",  "Usuário padrão — acesso às próprias finanças"  }
            });

            migrationBuilder.InsertData("SourceType", new[] { "Id", "Name" }, new object[,]
            {
            { 1, "Parental" },
            { 2, "Personal" }
            });

            migrationBuilder.InsertData("FortnightType", new[] { "Id", "Name" }, new object[,]
            {
            { 1, "First"  },
            { 2, "Second" }
            });

            migrationBuilder.InsertData("PaymentStatus", new[] { "Id", "Name", "Description" }, new object[,]
            {
            { 1, "Pending",   "Despesa pendente de pagamento" },
            { 2, "Paid",      "Despesa paga"                  },
            { 3, "Cancelled", "Despesa cancelada"             },
            { 4, "Partial",   "Despesa parcialmente paga"     }
            });

            // ── USER ──────────────────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.UniqueConstraint("UQ_User_Email", x => x.Email);
                });

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "User",
                column: "Email",
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_User_IsActive",
                table: "User",
                column: "IsActive",
                filter: "[DeletedAt] IS NULL");

            // ── USER ROLE ─────────────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "UserRole",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => new { x.UserId, x.RoleId });
                    table.ForeignKey("FK_UserRole_User", x => x.UserId, "User", "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_UserRole_Role", x => x.RoleId, "Role", "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_UserId",
                table: "UserRole",
                column: "UserId");

            // ── CATEGORY ─────────────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "Category",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    IsGlobal = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Category", x => x.Id);
                    table.ForeignKey("FK_Category_User", x => x.UserId, "User", "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Category_UserId",
                table: "Category",
                column: "UserId",
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Category_IsGlobal",
                table: "Category",
                column: "IsGlobal",
                filter: "[DeletedAt] IS NULL");

            // ── PERIOD ────────────────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "Period",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Month = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Period", x => x.Id);
                    table.ForeignKey("FK_Period_User", x => x.UserId, "User", "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.UniqueConstraint("UQ_Period_UserYearMonth", x => new { x.UserId, x.Year, x.Month });
                    table.CheckConstraint("CK_Period_Month", "[Month] BETWEEN 1 AND 12");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Period_UserId",
                table: "Period",
                column: "UserId",
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Period_YearMonth",
                table: "Period",
                columns: new[] { "Year", "Month" });

            // ── EXPENSE ───────────────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "Expense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceTypeId = table.Column<int>(type: "int", nullable: false),
                    FortnightTypeId = table.Column<int>(type: "int", nullable: false),
                    PaymentStatusId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expense", x => x.Id);
                    table.ForeignKey("FK_Expense_Period", x => x.PeriodId, "Period", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Expense_User", x => x.UserId, "User", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Expense_Category", x => x.CategoryId, "Category", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Expense_SourceType", x => x.SourceTypeId, "SourceType", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Expense_FortnightType", x => x.FortnightTypeId, "FortnightType", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Expense_PaymentStatus", x => x.PaymentStatusId, "PaymentStatus", "Id", onDelete: ReferentialAction.Restrict);
                    table.CheckConstraint("CK_Expense_Amount", "[Amount] > 0");
                });

            migrationBuilder.CreateIndex(name: "IX_Expense_PeriodId", table: "Expense", column: "PeriodId", filter: "[DeletedAt] IS NULL");
            migrationBuilder.CreateIndex(name: "IX_Expense_UserId", table: "Expense", column: "UserId", filter: "[DeletedAt] IS NULL");
            migrationBuilder.CreateIndex(name: "IX_Expense_CategoryId", table: "Expense", column: "CategoryId", filter: "[DeletedAt] IS NULL");
            migrationBuilder.CreateIndex(name: "IX_Expense_PaymentStatusId", table: "Expense", column: "PaymentStatusId", filter: "[DeletedAt] IS NULL");
            migrationBuilder.CreateIndex(name: "IX_Expense_DueDate", table: "Expense", column: "DueDate");

            // ── INCOME ────────────────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "Income",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FortnightTypeId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceivedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Income", x => x.Id);
                    table.ForeignKey("FK_Income_Period", x => x.PeriodId, "Period", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Income_User", x => x.UserId, "User", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Income_FortnightType", x => x.FortnightTypeId, "FortnightType", "Id", onDelete: ReferentialAction.Restrict);
                    table.CheckConstraint("CK_Income_Amount", "[Amount] > 0");
                });

            migrationBuilder.CreateIndex(name: "IX_Income_PeriodId", table: "Income", column: "PeriodId", filter: "[DeletedAt] IS NULL");
            migrationBuilder.CreateIndex(name: "IX_Income_UserId", table: "Income", column: "UserId", filter: "[DeletedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ordem inversa de criação para respeitar as FKs
            migrationBuilder.DropTable(name: "Income");
            migrationBuilder.DropTable(name: "Expense");
            migrationBuilder.DropTable(name: "Period");
            migrationBuilder.DropTable(name: "Category");
            migrationBuilder.DropTable(name: "UserRole");
            migrationBuilder.DropTable(name: "User");
            migrationBuilder.DropTable(name: "PaymentStatus");
            migrationBuilder.DropTable(name: "FortnightType");
            migrationBuilder.DropTable(name: "SourceType");
            migrationBuilder.DropTable(name: "Role");
        }
    }
}
