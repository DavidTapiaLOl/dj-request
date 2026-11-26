using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DjRequests.Api.Persistence.AdminMigrations
{
    /// <inheritdoc />
    public partial class AddAuditoriaAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria_admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    administrador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tipo_recurso = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    recurso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditoria_admin", x => x.id);
                    table.CheckConstraint("chk_auditoria_admin_accion_no_vacio", "btrim(\"accion\") <> ''");
                    table.CheckConstraint("chk_auditoria_admin_resultado_no_vacio", "btrim(\"resultado\") <> ''");
                    table.CheckConstraint("chk_auditoria_admin_tipo_recurso_no_vacio", "btrim(\"tipo_recurso\") <> ''");
                    table.ForeignKey(
                        name: "FK_auditoria_admin_admin_accounts_administrador_id",
                        column: x => x.administrador_id,
                        principalTable: "admin_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_admin_administrador_id_created_at",
                table: "auditoria_admin",
                columns: new[] { "administrador_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_admin_created_at",
                table: "auditoria_admin",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditoria_admin");
        }
    }
}
