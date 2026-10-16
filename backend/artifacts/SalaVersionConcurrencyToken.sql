START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016003450_SalaVersionConcurrencyToken') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261016003450_SalaVersionConcurrencyToken', '10.0.12');
    END IF;
END $EF$;
COMMIT;

