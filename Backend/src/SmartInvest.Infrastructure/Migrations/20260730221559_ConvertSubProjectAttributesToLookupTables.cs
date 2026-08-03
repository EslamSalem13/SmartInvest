using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertSubProjectAttributesToLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the 3 new FK columns as nullable first — a brand-new required int
            // column cannot be added to a table with existing rows without a default,
            // and there is no lookup row with Id = 0 to use as that default.
            migrationBuilder.AddColumn<int>(
                name: "ProjectLevelId",
                table: "SubProjects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComponentTypeId",
                table: "SubProjects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountingUnitId",
                table: "SubProjects",
                type: "int",
                nullable: true);

            // Populate the new FK columns by matching the old free-text values against
            // the lookup tables' Name column, then fall back to the "غير محدد" row for
            // anything that didn't match (in practice: every row's AccountingUnit,
            // which has always been blank).
            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ProjectLevelId = pl.Id
                FROM SubProjects sp
                JOIN ProjectLevels pl ON pl.Name = sp.ProjectLevel
            ");
            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ProjectLevelId = (SELECT TOP 1 Id FROM ProjectLevels WHERE Name = N'غير محدد')
                FROM SubProjects sp
                WHERE sp.ProjectLevelId IS NULL
            ");

            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ComponentTypeId = ct.Id
                FROM SubProjects sp
                JOIN ComponentTypes ct ON ct.Name = sp.ComponentType
            ");
            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ComponentTypeId = (SELECT TOP 1 Id FROM ComponentTypes WHERE Name = N'غير محدد')
                FROM SubProjects sp
                WHERE sp.ComponentTypeId IS NULL
            ");

            migrationBuilder.Sql(@"
                UPDATE sp SET sp.AccountingUnitId = au.Id
                FROM SubProjects sp
                JOIN AccountingUnits au ON au.Name = sp.AccountingUnit
            ");
            migrationBuilder.Sql(@"
                UPDATE sp SET sp.AccountingUnitId = (SELECT TOP 1 Id FROM AccountingUnits WHERE Name = N'غير محدد')
                FROM SubProjects sp
                WHERE sp.AccountingUnitId IS NULL
            ");

            // Drop the old string columns only after their data has been fully consumed.
            migrationBuilder.DropColumn(name: "ProjectLevel", table: "SubProjects");
            migrationBuilder.DropColumn(name: "ComponentType", table: "SubProjects");
            migrationBuilder.DropColumn(name: "AccountingUnit", table: "SubProjects");

            // Every row now has a valid FK value, so tighten the columns to NOT NULL.
            migrationBuilder.AlterColumn<int>(
                name: "ProjectLevelId",
                table: "SubProjects",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ComponentTypeId",
                table: "SubProjects",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AccountingUnitId",
                table: "SubProjects",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Indexes/FKs are added last, after the columns are in their final shape.
            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_ProjectLevelId",
                table: "SubProjects",
                column: "ProjectLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_ComponentTypeId",
                table: "SubProjects",
                column: "ComponentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_AccountingUnitId",
                table: "SubProjects",
                column: "AccountingUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubProjects_ProjectLevels_ProjectLevelId",
                table: "SubProjects",
                column: "ProjectLevelId",
                principalTable: "ProjectLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubProjects_ComponentTypes_ComponentTypeId",
                table: "SubProjects",
                column: "ComponentTypeId",
                principalTable: "ComponentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubProjects_AccountingUnits_AccountingUnitId",
                table: "SubProjects",
                column: "AccountingUnitId",
                principalTable: "AccountingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: this Down() is intentionally asymmetric with Up(). Up() preserves
            // data via a two-pass name-match migration; rolling that back on a
            // production-like dataset is unlikely to ever be needed. This Down()
            // restores the string columns and repopulates them by joining back from
            // the FK to the lookup table's Name (a faithful reversal), but does not
            // attempt to reproduce the exact pre-migration blank/unmatched state for
            // any row that fell back to "غير محدد" during Up() — those rows will show
            // "غير محدد" as their string value instead of whatever (if anything) they
            // originally had.
            migrationBuilder.DropForeignKey(
                name: "FK_SubProjects_ProjectLevels_ProjectLevelId",
                table: "SubProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_SubProjects_ComponentTypes_ComponentTypeId",
                table: "SubProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_SubProjects_AccountingUnits_AccountingUnitId",
                table: "SubProjects");

            migrationBuilder.DropIndex(
                name: "IX_SubProjects_ProjectLevelId",
                table: "SubProjects");

            migrationBuilder.DropIndex(
                name: "IX_SubProjects_ComponentTypeId",
                table: "SubProjects");

            migrationBuilder.DropIndex(
                name: "IX_SubProjects_AccountingUnitId",
                table: "SubProjects");

            migrationBuilder.AddColumn<string>(
                name: "ProjectLevel",
                table: "SubProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComponentType",
                table: "SubProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountingUnit",
                table: "SubProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ProjectLevel = pl.Name
                FROM SubProjects sp
                JOIN ProjectLevels pl ON pl.Id = sp.ProjectLevelId
            ");

            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ComponentType = ct.Name
                FROM SubProjects sp
                JOIN ComponentTypes ct ON ct.Id = sp.ComponentTypeId
            ");

            migrationBuilder.Sql(@"
                UPDATE sp SET sp.AccountingUnit = au.Name
                FROM SubProjects sp
                JOIN AccountingUnits au ON au.Id = sp.AccountingUnitId
            ");

            migrationBuilder.AlterColumn<string>(
                name: "ProjectLevel",
                table: "SubProjects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ComponentType",
                table: "SubProjects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountingUnit",
                table: "SubProjects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "ProjectLevelId",
                table: "SubProjects");

            migrationBuilder.DropColumn(
                name: "ComponentTypeId",
                table: "SubProjects");

            migrationBuilder.DropColumn(
                name: "AccountingUnitId",
                table: "SubProjects");
        }
    }
}
