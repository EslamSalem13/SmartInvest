using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentTypeProjectLevelAccountingUnitTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingUnits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComponentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLevels", x => x.Id);
                });

            migrationBuilder.Sql(@"
                INSERT INTO ComponentTypes (Name)
                SELECT DISTINCT ComponentType FROM SubProjects
                WHERE ComponentType IS NOT NULL AND LTRIM(RTRIM(ComponentType)) <> ''
            ");
            migrationBuilder.Sql("INSERT INTO ComponentTypes (Name) VALUES (N'غير محدد')");

            migrationBuilder.Sql(@"
                INSERT INTO ProjectLevels (Name)
                SELECT DISTINCT ProjectLevel FROM SubProjects
                WHERE ProjectLevel IS NOT NULL AND LTRIM(RTRIM(ProjectLevel)) <> ''
            ");
            migrationBuilder.Sql("INSERT INTO ProjectLevels (Name) VALUES (N'غير محدد')");

            migrationBuilder.Sql(@"
                INSERT INTO AccountingUnits (Name)
                SELECT DISTINCT AccountingUnit FROM SubProjects
                WHERE AccountingUnit IS NOT NULL AND LTRIM(RTRIM(AccountingUnit)) <> ''
            ");
            migrationBuilder.Sql("INSERT INTO AccountingUnits (Name) VALUES (N'غير محدد')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingUnits");

            migrationBuilder.DropTable(
                name: "ComponentTypes");

            migrationBuilder.DropTable(
                name: "ProjectLevels");
        }
    }
}
