using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DjRequests.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "djs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_artistico = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    estado = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "activo"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_djs", x => x.Id);
                    table.CheckConstraint("chk_djs_email_no_vacio", "btrim(\"Email\") <> ''");
                    table.CheckConstraint("chk_djs_estado_valido", "estado IN ('activo','suspendido')");
                    table.CheckConstraint("chk_djs_normalized_email_no_vacio", "btrim(\"NormalizedEmail\") <> ''");
                });

            migrationBuilder.CreateTable(
                name: "generos",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "salas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dj_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    codigo = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    estado = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "abierta"),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "America/Mexico_City"),
                    purge_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    max_solicitudes_sesion = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    max_playlists_sesion = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    intervalo_envio_segundos = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_salas", x => x.id);
                    table.CheckConstraint("chk_sala_ends_gt_starts", "ends_at > starts_at");
                    table.CheckConstraint("chk_sala_intervalo", "intervalo_envio_segundos >= 0");
                    table.CheckConstraint("chk_sala_max_playlists", "max_playlists_sesion > 0");
                    table.CheckConstraint("chk_sala_max_solicitudes", "max_solicitudes_sesion > 0");
                    table.CheckConstraint("chk_sala_purge_gt_ends", "purge_at > ends_at");
                    table.CheckConstraint("chk_salas_estado_valido", "estado IN ('abierta','pausada','cerrada')");
                    table.CheckConstraint("chk_salas_nombre_no_vacio", "btrim(nombre) <> ''");
                    table.ForeignKey(
                        name: "FK_salas_djs_dj_id",
                        column: x => x.dj_id,
                        principalTable: "djs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_claims_djs_UserId",
                        column: x => x.UserId,
                        principalTable: "djs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_user_logins_djs_UserId",
                        column: x => x.UserId,
                        principalTable: "djs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_user_tokens_djs_UserId",
                        column: x => x.UserId,
                        principalTable: "djs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_claims_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_djs_UserId",
                        column: x => x.UserId,
                        principalTable: "djs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sesiones_invitado",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sala_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_envio_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesiones_invitado", x => new { x.id, x.sala_id });
                    table.CheckConstraint("chk_sesion_expires_gt_created", "expires_at > created_at");
                    table.ForeignKey(
                        name: "FK_sesiones_invitado_salas_sala_id",
                        column: x => x.sala_id,
                        principalTable: "salas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlists_compartidas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sala_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    enlace = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlists_compartidas", x => x.id);
                    table.CheckConstraint("chk_playlist_enlace_no_vacio", "btrim(enlace) <> ''");
                    table.ForeignKey(
                        name: "FK_playlists_compartidas_salas_sala_id",
                        column: x => x.sala_id,
                        principalTable: "salas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_playlists_compartidas_sesiones_invitado_sesion_id_sala_id",
                        columns: x => new { x.sesion_id, x.sala_id },
                        principalTable: "sesiones_invitado",
                        principalColumns: new[] { "id", "sala_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "solicitudes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sala_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    artista = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    genero_id = table.Column<short>(type: "smallint", nullable: true),
                    enlace = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    estado = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pendiente"),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitudes", x => x.id);
                    table.CheckConstraint("chk_solicitud_estado_valido", "estado IN ('pendiente', 'aceptada', 'reproducida', 'rechazada')");
                    table.CheckConstraint("chk_solicitud_nombre_no_vacio", "btrim(nombre) <> ''");
                    table.ForeignKey(
                        name: "FK_solicitudes_generos_genero_id",
                        column: x => x.genero_id,
                        principalTable: "generos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_salas_sala_id",
                        column: x => x.sala_id,
                        principalTable: "salas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_solicitudes_sesiones_invitado_sesion_id_sala_id",
                        columns: x => new { x.sesion_id, x.sala_id },
                        principalTable: "sesiones_invitado",
                        principalColumns: new[] { "id", "sala_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "generos",
                columns: new[] { "id", "activo", "nombre" },
                values: new object[,]
                {
                    { (short)1, true, "Pop" },
                    { (short)2, true, "Rock" },
                    { (short)3, true, "Reguetón" },
                    { (short)4, true, "Regional mexicano" },
                    { (short)5, true, "Electrónica" },
                    { (short)6, true, "Salsa" },
                    { (short)7, true, "Cumbia" },
                    { (short)8, true, "Bachata" },
                    { (short)9, true, "Hip-hop/Rap" },
                    { (short)10, true, "R&B" },
                    { (short)11, true, "Otro" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_djs_normalized_email",
                table: "djs",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "djs",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_generos_nombre",
                table: "generos",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_playlists_compartidas_sala_id_created_at_id",
                table: "playlists_compartidas",
                columns: new[] { "sala_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_playlists_compartidas_sesion_id_idempotency_key",
                table: "playlists_compartidas",
                columns: new[] { "sesion_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_playlists_compartidas_sesion_id_sala_id",
                table: "playlists_compartidas",
                columns: new[] { "sesion_id", "sala_id" });

            migrationBuilder.CreateIndex(
                name: "IX_role_claims_RoleId",
                table: "role_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_salas_codigo",
                table: "salas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_salas_dj_id_created_at",
                table: "salas",
                columns: new[] { "dj_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_salas_purge_at",
                table: "salas",
                column: "purge_at");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_invitado_sala_id",
                table: "sesiones_invitado",
                column: "sala_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_invitado_sala_id_id",
                table: "sesiones_invitado",
                columns: new[] { "sala_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_invitado_token_hash",
                table: "sesiones_invitado",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_genero_id",
                table: "solicitudes",
                column: "genero_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_sala_id_created_at_id",
                table: "solicitudes",
                columns: new[] { "sala_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_sala_id_estado_created_at_id",
                table: "solicitudes",
                columns: new[] { "sala_id", "estado", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_sesion_id_idempotency_key",
                table: "solicitudes",
                columns: new[] { "sesion_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_sesion_id_sala_id",
                table: "solicitudes",
                columns: new[] { "sesion_id", "sala_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_claims_UserId",
                table: "user_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_logins_UserId",
                table: "user_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playlists_compartidas");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "solicitudes");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "generos");

            migrationBuilder.DropTable(
                name: "sesiones_invitado");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "salas");

            migrationBuilder.DropTable(
                name: "djs");
        }
    }
}
