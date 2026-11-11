using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DjRequests.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDjProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "perfiles_dj",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dj_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    anio_inicio = table.Column<int>(type: "integer", nullable: true),
                    foto_nombre_archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    red_instagram = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    red_facebook = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    red_tiktok = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    red_youtube = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    red_soundcloud = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perfiles_dj", x => x.id);
                    table.CheckConstraint("chk_perfiles_dj_anio_inicio", "anio_inicio IS NULL OR anio_inicio BETWEEN 1900 AND 2100");
                    table.ForeignKey(
                        name: "FK_perfiles_dj_djs_dj_id",
                        column: x => x.dj_id,
                        principalTable: "djs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_perfiles_dj_dj_id",
                table: "perfiles_dj",
                column: "dj_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "perfiles_dj");
        }
    }
}
