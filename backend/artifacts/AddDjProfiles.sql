START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108212801_AddDjProfiles') THEN
    CREATE TABLE perfiles_dj (
        id uuid NOT NULL,
        dj_id uuid NOT NULL,
        descripcion character varying(1500),
        anio_inicio integer,
        foto_nombre_archivo character varying(260),
        red_instagram character varying(2048),
        red_facebook character varying(2048),
        red_tiktok character varying(2048),
        red_youtube character varying(2048),
        red_soundcloud character varying(2048),
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_perfiles_dj" PRIMARY KEY (id),
        CONSTRAINT chk_perfiles_dj_anio_inicio CHECK (anio_inicio IS NULL OR anio_inicio BETWEEN 1900 AND 2100),
        CONSTRAINT "FK_perfiles_dj_djs_dj_id" FOREIGN KEY (dj_id) REFERENCES djs ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108212801_AddDjProfiles') THEN
    CREATE UNIQUE INDEX "IX_perfiles_dj_dj_id" ON perfiles_dj (dj_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108212801_AddDjProfiles') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108212801_AddDjProfiles', '10.0.12');
    END IF;
END $EF$;
COMMIT;

