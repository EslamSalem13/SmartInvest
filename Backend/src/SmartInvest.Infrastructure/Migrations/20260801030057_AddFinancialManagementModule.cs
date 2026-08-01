using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialManagementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SubProjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractAwards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdvancePaymentDone = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SubProjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAwards_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialEvaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SubProjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialEvaluations_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningEnvelopes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SubProjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningEnvelopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningEnvelopes_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PresentationMemos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresentationMemos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalEvaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SubProjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalEvaluations_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SubProjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderDocuments_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnnouncementId = table.Column<int>(type: "int", nullable: false),
                    NewspaperAdvertisement_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NewspaperAdvertisement_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NewspaperAdvertisement_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    NewspaperAdvertisement_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PortalAdvertisement_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PortalAdvertisement_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PortalAdvertisement_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    PortalAdvertisement_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CompetentAuthorityApproval_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CompetentAuthorityApproval_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CompetentAuthorityApproval_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    CompetentAuthorityApproval_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementVersions_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractAwardVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractAwardId = table.Column<int>(type: "int", nullable: false),
                    AwardOrder_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AwardOrder_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AwardOrder_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    AwardOrder_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Contract_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Contract_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Contract_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Contract_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAwardVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAwardVersions_ContractAwards_ContractAwardId",
                        column: x => x.ContractAwardId,
                        principalTable: "ContractAwards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialEvaluationVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FinancialEvaluationId = table.Column<int>(type: "int", nullable: false),
                    FinancialBidOpeningMinutes_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FinancialBidOpeningMinutes_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FinancialBidOpeningMinutes_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FinancialBidOpeningMinutes_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    FinancialEvaluationReport_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FinancialEvaluationReport_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FinancialEvaluationReport_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FinancialEvaluationReport_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    EstimatedCostSheet_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EstimatedCostSheet_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EstimatedCostSheet_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    EstimatedCostSheet_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialEvaluationVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialEvaluationVersions_FinancialEvaluations_FinancialEvaluationId",
                        column: x => x.FinancialEvaluationId,
                        principalTable: "FinancialEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningEnvelopesVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OpeningEnvelopesId = table.Column<int>(type: "int", nullable: false),
                    File_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    File_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    File_FileSize = table.Column<long>(type: "bigint", nullable: false),
                    File_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningEnvelopesVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningEnvelopesVersions_OpeningEnvelopes_OpeningEnvelopesId",
                        column: x => x.OpeningEnvelopesId,
                        principalTable: "OpeningEnvelopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PresentationMemoSubProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PresentationMemoId = table.Column<int>(type: "int", nullable: false),
                    SubProjectId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresentationMemoSubProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresentationMemoSubProjects_PresentationMemos_PresentationMemoId",
                        column: x => x.PresentationMemoId,
                        principalTable: "PresentationMemos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PresentationMemoSubProjects_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PresentationMemoVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PresentationMemoId = table.Column<int>(type: "int", nullable: false),
                    File_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    File_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    File_FileSize = table.Column<long>(type: "bigint", nullable: false),
                    File_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresentationMemoVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresentationMemoVersions_PresentationMemos_PresentationMemoId",
                        column: x => x.PresentationMemoId,
                        principalTable: "PresentationMemos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalEvaluationVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TechnicalEvaluationId = table.Column<int>(type: "int", nullable: false),
                    FirstCommitteeReport_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FirstCommitteeReport_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FirstCommitteeReport_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FirstCommitteeReport_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SecondCommitteeReport_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SecondCommitteeReport_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SecondCommitteeReport_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    SecondCommitteeReport_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    FinalTechnicalEvaluationReport_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FinalTechnicalEvaluationReport_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FinalTechnicalEvaluationReport_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FinalTechnicalEvaluationReport_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalEvaluationVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalEvaluationVersions_TechnicalEvaluations_TechnicalEvaluationId",
                        column: x => x.TechnicalEvaluationId,
                        principalTable: "TechnicalEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderDocumentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderDocumentId = table.Column<int>(type: "int", nullable: false),
                    File_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    File_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    File_FileSize = table.Column<long>(type: "bigint", nullable: false),
                    File_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderDocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderDocumentVersions_TenderDocuments_TenderDocumentId",
                        column: x => x.TenderDocumentId,
                        principalTable: "TenderDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_SubProjectId",
                table: "Announcements",
                column: "SubProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementVersions_AnnouncementId_VersionNumber",
                table: "AnnouncementVersions",
                columns: new[] { "AnnouncementId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAwards_SubProjectId",
                table: "ContractAwards",
                column: "SubProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAwardVersions_ContractAwardId_VersionNumber",
                table: "ContractAwardVersions",
                columns: new[] { "ContractAwardId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvaluations_SubProjectId",
                table: "FinancialEvaluations",
                column: "SubProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvaluationVersions_FinancialEvaluationId_VersionNumber",
                table: "FinancialEvaluationVersions",
                columns: new[] { "FinancialEvaluationId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningEnvelopes_SubProjectId",
                table: "OpeningEnvelopes",
                column: "SubProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningEnvelopesVersions_OpeningEnvelopesId_VersionNumber",
                table: "OpeningEnvelopesVersions",
                columns: new[] { "OpeningEnvelopesId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PresentationMemoSubProjects_PresentationMemoId_SubProjectId",
                table: "PresentationMemoSubProjects",
                columns: new[] { "PresentationMemoId", "SubProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PresentationMemoSubProjects_SubProjectId",
                table: "PresentationMemoSubProjects",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PresentationMemoVersions_PresentationMemoId_VersionNumber",
                table: "PresentationMemoVersions",
                columns: new[] { "PresentationMemoId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalEvaluations_SubProjectId",
                table: "TechnicalEvaluations",
                column: "SubProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalEvaluationVersions_TechnicalEvaluationId_VersionNumber",
                table: "TechnicalEvaluationVersions",
                columns: new[] { "TechnicalEvaluationId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_SubProjectId",
                table: "TenderDocuments",
                column: "SubProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocumentVersions_TenderDocumentId_VersionNumber",
                table: "TenderDocumentVersions",
                columns: new[] { "TenderDocumentId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementVersions");

            migrationBuilder.DropTable(
                name: "ContractAwardVersions");

            migrationBuilder.DropTable(
                name: "FinancialEvaluationVersions");

            migrationBuilder.DropTable(
                name: "OpeningEnvelopesVersions");

            migrationBuilder.DropTable(
                name: "PresentationMemoSubProjects");

            migrationBuilder.DropTable(
                name: "PresentationMemoVersions");

            migrationBuilder.DropTable(
                name: "TechnicalEvaluationVersions");

            migrationBuilder.DropTable(
                name: "TenderDocumentVersions");

            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "ContractAwards");

            migrationBuilder.DropTable(
                name: "FinancialEvaluations");

            migrationBuilder.DropTable(
                name: "OpeningEnvelopes");

            migrationBuilder.DropTable(
                name: "PresentationMemos");

            migrationBuilder.DropTable(
                name: "TechnicalEvaluations");

            migrationBuilder.DropTable(
                name: "TenderDocuments");
        }
    }
}
