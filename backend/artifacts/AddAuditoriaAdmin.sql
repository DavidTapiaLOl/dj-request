START TRANSACTION;
CREATE TABLE auditoria_admin (
    id uuid NOT NULL,
    administrador_id uuid,
    accion character varying(80) NOT NULL,
    tipo_recurso character varying(40) NOT NULL,
    recurso_id uuid,
    resultado character varying(20) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_auditoria_admin" PRIMARY KEY (id),
    CONSTRAINT chk_auditoria_admin_accion_no_vacio CHECK (btrim("accion") <> ''),
    CONSTRAINT chk_auditoria_admin_resultado_no_vacio CHECK (btrim("resultado") <> ''),
    CONSTRAINT chk_auditoria_admin_tipo_recurso_no_vacio CHECK (btrim("tipo_recurso") <> ''),
    CONSTRAINT "FK_auditoria_admin_admin_accounts_administrador_id" FOREIGN KEY (administrador_id) REFERENCES admin_accounts ("Id") ON DELETE SET NULL
);

CREATE INDEX "IX_auditoria_admin_administrador_id_created_at" ON auditoria_admin (administrador_id, created_at);

CREATE INDEX "IX_auditoria_admin_created_at" ON auditoria_admin (created_at);

INSERT INTO "__EFMigrationsHistory_Admin" ("MigrationId", "ProductVersion")
VALUES ('20261126035100_AddAuditoriaAdmin', '10.0.12');

COMMIT;
