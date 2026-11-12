using System.Globalization;
using System.Security.Cryptography;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Retention.Services;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Retention;

[Collection(IntegrationCollection.Name)]
public class RetentionServiceTests
{
    // Instante fijo sin sub-segundos para que PurgeAt == now sea exacto
    // al round-trip por timestamptz (microsegundos).
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public RetentionServiceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<WebAppFactory> CreateFactoryAsync(int batchSize = 100, bool roomLockPause = false)
    {
        var factory = new WebAppFactory(
            _postgres,
            new Dictionary<string, string?>
            {
                ["Retention:BatchSize"] = batchSize.ToString(CultureInfo.InvariantCulture)
            })
        {
            EnableRoomLockPause = roomLockPause
        };

        await factory.InitializeAsync();

        var fakeTime = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();
        fakeTime.UtcNow = Now;

        return factory;
    }

    #region Helpers de semilla

    private static string NewCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(5));

    private static async Task<Guid> SeedDjAsync(DjRequestsDbContext db, string email)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new DjAccount
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NombreArtistico = "DJ Retention",
            Estado = "activo",
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedSalaAsync(
        DjRequestsDbContext db,
        Guid djId,
        string code,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset purgeAt,
        string estado = "abierta",
        DateTimeOffset? createdAt = null)
    {
        var id = Guid.NewGuid();
        db.Salas.Add(new Sala
        {
            Id = id,
            DjId = djId,
            Nombre = "Sala Retention",
            Codigo = code,
            Estado = estado,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Timezone = "America/Mexico_City",
            PurgeAt = purgeAt,
            MaxSolicitudesSesion = 10,
            MaxPlaylistsSesion = 2,
            IntervaloEnvioSegundos = 30,
            Version = 1,
            CreatedAt = createdAt ?? Now,
            UpdatedAt = Now
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedSesionAsync(
        DjRequestsDbContext db,
        Guid salaId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        var id = Guid.NewGuid();
        db.SesionesInvitado.Add(new SesionInvitado
        {
            Id = id,
            SalaId = salaId,
            TokenHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            RevokedAt = null,
            UltimoEnvioAt = null
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedSolicitudAsync(
        DjRequestsDbContext db,
        Guid salaId,
        Guid sesionId,
        short? generoId = null)
    {
        var id = Guid.NewGuid();
        db.Solicitudes.Add(new Solicitud
        {
            Id = id,
            SalaId = salaId,
            SesionId = sesionId,
            Nombre = "Cancion de prueba",
            Artista = "Artista de prueba",
            GeneroId = generoId,
            Enlace = null,
            Estado = "pendiente",
            IdempotencyKey = Guid.NewGuid(),
            Version = 1,
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedPlaylistAsync(
        DjRequestsDbContext db,
        Guid salaId,
        Guid sesionId)
    {
        var id = Guid.NewGuid();
        db.PlaylistsCompartidas.Add(new PlaylistCompartida
        {
            Id = id,
            SalaId = salaId,
            SesionId = sesionId,
            Nombre = "Playlist de prueba",
            Enlace = "https://example.com/playlist",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = Now
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<int> RunPurgeAsync(WebAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRetentionService>();
        var result = await service.PurgeExpiredBatchAsync();
        return result.DeletedRooms;
    }

    #endregion

    [Fact]
    public async Task Purge_ExpiredRoom_DeletesRoomWithSessionsRequestsAndPlaylists()
    {
        await using var factory = await CreateFactoryAsync();

        Guid expiredSalaId;
        Guid liveSalaId;
        Guid sesionId;
        Guid solicitudId;
        Guid playlistId;
        Guid liveSesionId;
        Guid liveSolicitudId;
        Guid livePlaylistId;

        await using (var db = await factory.CreateDbContextAsync())
        {
            var djId = await SeedDjAsync(db, "expired-content@test.com");

            expiredSalaId = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1), purgeAt: Now.AddHours(-1));
            sesionId = await SeedSesionAsync(db, expiredSalaId, Now.AddDays(-2), Now.AddDays(-1));
            solicitudId = await SeedSolicitudAsync(db, expiredSalaId, sesionId, generoId: 1);
            playlistId = await SeedPlaylistAsync(db, expiredSalaId, sesionId);

            liveSalaId = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddHours(-1), endsAt: Now.AddHours(2), purgeAt: Now.AddHours(170));
            liveSesionId = await SeedSesionAsync(db, liveSalaId, Now, Now.AddHours(2));
            liveSolicitudId = await SeedSolicitudAsync(db, liveSalaId, liveSesionId, generoId: 1);
            livePlaylistId = await SeedPlaylistAsync(db, liveSalaId, liveSesionId);
        }

        var deleted = await RunPurgeAsync(factory);

        Assert.Equal(1, deleted);

        await using var check = await factory.CreateDbContextAsync();

        Assert.False(await check.Salas.AnyAsync(s => s.Id == expiredSalaId));
        Assert.False(await check.SesionesInvitado.AnyAsync(s => s.Id == sesionId && s.SalaId == expiredSalaId));
        Assert.False(await check.Solicitudes.AnyAsync(s => s.Id == solicitudId));
        Assert.False(await check.PlaylistsCompartidas.AnyAsync(p => p.Id == playlistId));

        Assert.True(await check.Salas.AnyAsync(s => s.Id == liveSalaId));
        Assert.True(await check.SesionesInvitado.AnyAsync(s => s.Id == liveSesionId && s.SalaId == liveSalaId));
        Assert.True(await check.Solicitudes.AnyAsync(s => s.Id == liveSolicitudId));
        Assert.True(await check.PlaylistsCompartidas.AnyAsync(p => p.Id == livePlaylistId));
    }

    [Fact]
    public async Task Purge_PurgeAtEqualsNow_DeletesRoom()
    {
        await using var factory = await CreateFactoryAsync();

        Guid salaId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var djId = await SeedDjAsync(db, "exact-now@test.com");
            salaId = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddDays(-8), endsAt: Now.AddDays(-7), purgeAt: Now);
        }

        var deleted = await RunPurgeAsync(factory);

        Assert.Equal(1, deleted);

        await using var check = await factory.CreateDbContextAsync();
        Assert.False(await check.Salas.AnyAsync(s => s.Id == salaId));
    }

    [Fact]
    public async Task Purge_RoomNotYetExpiredIncludingOldRoom_RemainsIntact()
    {
        await using var factory = await CreateFactoryAsync();

        Guid salaId;
        Guid sesionId;
        Guid solicitudId;
        Guid playlistId;

        await using (var db = await factory.CreateDbContextAsync())
        {
            var djId = await SeedDjAsync(db, "current-room@test.com");

            // Creada hace 30 dias: la purga depende de PurgeAt, no de CreatedAt.
            salaId = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddHours(-1), endsAt: Now.AddHours(2), purgeAt: Now.AddHours(170),
                createdAt: Now.AddDays(-30));
            sesionId = await SeedSesionAsync(db, salaId, Now, Now.AddHours(2));
            solicitudId = await SeedSolicitudAsync(db, salaId, sesionId, generoId: 1);
            playlistId = await SeedPlaylistAsync(db, salaId, sesionId);
        }

        var deleted = await RunPurgeAsync(factory);

        Assert.Equal(0, deleted);

        await using var check = await factory.CreateDbContextAsync();
        Assert.True(await check.Salas.AnyAsync(s => s.Id == salaId));
        Assert.True(await check.SesionesInvitado.AnyAsync(s => s.Id == sesionId && s.SalaId == salaId));
        Assert.True(await check.Solicitudes.AnyAsync(s => s.Id == solicitudId));
        Assert.True(await check.PlaylistsCompartidas.AnyAsync(p => p.Id == playlistId));
    }

    [Fact]
    public async Task Purge_ExpiredRoom_KeepsDjProfilePhotoAndGenres()
    {
        await using var factory = await CreateFactoryAsync();

        var photoDir = Path.Combine(
            Path.GetTempPath(),
            "dj-requests-retention-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(photoDir);
        var photoPath = Path.Combine(photoDir, "foto.jpg");
        File.WriteAllBytes(photoPath, new byte[] { 1, 2, 3, 4 });

        try
        {
            Guid djId;
            Guid profileId;
            Guid salaId;
            int genresBefore;

            await using (var db = await factory.CreateDbContextAsync())
            {
                djId = await SeedDjAsync(db, "profile-keep@test.com");

                profileId = Guid.NewGuid();
                db.PerfilesDj.Add(new DjProfile
                {
                    Id = profileId,
                    DjId = djId,
                    Descripcion = "DJ de prueba",
                    FotoNombreArchivo = "foto.jpg",
                    CreatedAt = Now,
                    UpdatedAt = Now
                });
                await db.SaveChangesAsync();

                genresBefore = await db.Generos.CountAsync();

                salaId = await SeedSalaAsync(
                    db, djId, NewCode(),
                    startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1), purgeAt: Now.AddHours(-1));
                var sesionId = await SeedSesionAsync(db, salaId, Now.AddDays(-2), Now.AddDays(-1));
                await SeedSolicitudAsync(db, salaId, sesionId, generoId: 1);
            }

            var deleted = await RunPurgeAsync(factory);

            Assert.Equal(1, deleted);

            await using var check = await factory.CreateDbContextAsync();

            Assert.True(await check.Users.AnyAsync(u => u.Id == djId));
            Assert.True(await check.PerfilesDj.AnyAsync(p => p.Id == profileId && p.DjId == djId));
            Assert.True(File.Exists(photoPath));
            Assert.Equal(genresBefore, await check.Generos.CountAsync());
            Assert.True(await check.Generos.AnyAsync(g => g.Id == 1));
            Assert.False(await check.Salas.AnyAsync(s => s.Id == salaId));
        }
        finally
        {
            try
            {
                Directory.Delete(photoDir, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task Purge_PausedAndClosedRooms_KeepsBeforePurgeAt_DeletesAfter()
    {
        await using var factory = await CreateFactoryAsync();

        Guid pausedFuture;
        Guid closedFuture;
        Guid pausedExpired;
        Guid closedExpired;

        await using (var db = await factory.CreateDbContextAsync())
        {
            var djId = await SeedDjAsync(db, "room-states@test.com");

            pausedFuture = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddHours(-1), endsAt: Now.AddHours(2), purgeAt: Now.AddDays(2),
                estado: "pausada");
            closedFuture = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddHours(-1), endsAt: Now.AddHours(2), purgeAt: Now.AddDays(2),
                estado: "cerrada");
            pausedExpired = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1), purgeAt: Now.AddHours(-1),
                estado: "pausada");
            closedExpired = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1), purgeAt: Now.AddHours(-1),
                estado: "cerrada");
        }

        var deleted = await RunPurgeAsync(factory);

        Assert.Equal(2, deleted);

        await using var check = await factory.CreateDbContextAsync();
        Assert.True(await check.Salas.AnyAsync(s => s.Id == pausedFuture));
        Assert.True(await check.Salas.AnyAsync(s => s.Id == closedFuture));
        Assert.False(await check.Salas.AnyAsync(s => s.Id == pausedExpired));
        Assert.False(await check.Salas.AnyAsync(s => s.Id == closedExpired));
    }

    [Fact]
    public async Task Purge_RunTwice_IsIdempotent()
    {
        await using var factory = await CreateFactoryAsync();

        Guid expiredOne;
        Guid expiredTwo;
        Guid liveSalaId;
        Guid liveSesionId;

        await using (var db = await factory.CreateDbContextAsync())
        {
            var djId = await SeedDjAsync(db, "idempotent@test.com");

            expiredOne = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1), purgeAt: Now.AddHours(-2));
            var sesionOne = await SeedSesionAsync(db, expiredOne, Now.AddDays(-2), Now.AddDays(-1));
            await SeedSolicitudAsync(db, expiredOne, sesionOne, generoId: 1);

            expiredTwo = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1), purgeAt: Now.AddHours(-1));
            var sesionTwo = await SeedSesionAsync(db, expiredTwo, Now.AddDays(-2), Now.AddDays(-1));
            await SeedPlaylistAsync(db, expiredTwo, sesionTwo);

            liveSalaId = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddHours(-1), endsAt: Now.AddHours(2), purgeAt: Now.AddHours(170));
            liveSesionId = await SeedSesionAsync(db, liveSalaId, Now, Now.AddHours(2));
            await SeedSolicitudAsync(db, liveSalaId, liveSesionId, generoId: 1);
        }

        var first = await RunPurgeAsync(factory);
        var second = await RunPurgeAsync(factory);

        Assert.Equal(2, first);
        Assert.Equal(0, second);

        await using var check = await factory.CreateDbContextAsync();
        Assert.False(await check.Salas.AnyAsync(s => s.Id == expiredOne));
        Assert.False(await check.Salas.AnyAsync(s => s.Id == expiredTwo));
        Assert.True(await check.Salas.AnyAsync(s => s.Id == liveSalaId));
        Assert.True(await check.SesionesInvitado.AnyAsync(s => s.Id == liveSesionId && s.SalaId == liveSalaId));
    }

    [Fact]
    public async Task Purge_BatchSizeRespected_SubsequentPassesDrainRemainder()
    {
        await using var factory = await CreateFactoryAsync(batchSize: 2);

        var expiredIds = new List<Guid>();
        Guid liveSalaId;

        await using (var db = await factory.CreateDbContextAsync())
        {
            var djId = await SeedDjAsync(db, "batch-size@test.com");

            for (var i = 0; i < 5; i++)
            {
                var salaId = await SeedSalaAsync(
                    db, djId, NewCode(),
                    startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1),
                    purgeAt: Now.AddMinutes(-5 + i));
                expiredIds.Add(salaId);

                var sesionId = await SeedSesionAsync(db, salaId, Now.AddDays(-2), Now.AddDays(-1));
                await SeedSolicitudAsync(db, salaId, sesionId, generoId: 1);
            }

            liveSalaId = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddHours(-1), endsAt: Now.AddHours(2), purgeAt: Now.AddHours(170));
        }

        var first = await RunPurgeAsync(factory);
        var second = await RunPurgeAsync(factory);
        var third = await RunPurgeAsync(factory);
        var fourth = await RunPurgeAsync(factory);

        Assert.Equal(2, first);
        Assert.Equal(2, second);
        Assert.Equal(1, third);
        Assert.Equal(0, fourth);

        await using var check = await factory.CreateDbContextAsync();
        foreach (var salaId in expiredIds)
        {
            Assert.False(await check.Salas.AnyAsync(s => s.Id == salaId));
        }

        Assert.True(await check.Salas.AnyAsync(s => s.Id == liveSalaId));
    }

    [Fact]
    public async Task Purge_ConcurrentInstances_DeleteEachRoomExactlyOnce()
    {
        await using var factory = await CreateFactoryAsync();

        var expiredIds = new List<Guid>();
        Guid liveSalaId;

        await using (var db = await factory.CreateDbContextAsync())
        {
            var djId = await SeedDjAsync(db, "concurrent@test.com");

            for (var i = 0; i < 5; i++)
            {
                var salaId = await SeedSalaAsync(
                    db, djId, NewCode(),
                    startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1),
                    purgeAt: Now.AddMinutes(-5 + i));
                expiredIds.Add(salaId);

                var sesionId = await SeedSesionAsync(db, salaId, Now.AddDays(-2), Now.AddDays(-1));
                await SeedSolicitudAsync(db, salaId, sesionId, generoId: 1);
            }

            liveSalaId = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddHours(-1), endsAt: Now.AddHours(2), purgeAt: Now.AddHours(170));
        }

        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        var service1 = scope1.ServiceProvider.GetRequiredService<IRetentionService>();
        var service2 = scope2.ServiceProvider.GetRequiredService<IRetentionService>();

        var results = await Task.WhenAll(
            service1.PurgeExpiredBatchAsync(),
            service2.PurgeExpiredBatchAsync());

        // El reparto depende del intercalado, pero el total debe ser exacto:
        // cada sala se borra una sola vez y nunca se propaga un error.
        Assert.Equal(5, results.Sum(r => r.DeletedRooms));

        await using var check = await factory.CreateDbContextAsync();
        foreach (var salaId in expiredIds)
        {
            Assert.False(await check.Salas.AnyAsync(s => s.Id == salaId));
        }

        Assert.True(await check.Salas.AnyAsync(s => s.Id == liveSalaId));
    }

    [Fact]
    public async Task Purge_PurgeAtExtendedWhileWaitingForLock_IsNotDeleted()
    {
        await using var factory = await CreateFactoryAsync(roomLockPause: true);

        Guid salaId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var djId = await SeedDjAsync(db, "revalidate@test.com");
            salaId = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1), purgeAt: Now.AddHours(-1));
        }

        var lockPause = factory.RoomLockPause!;
        var purgeTask = Task.Run(() => RunPurgeAsync(factory));

        // El limpiador ya eligio la sala como candidata y pausa antes del FOR UPDATE.
        await lockPause.Paused.WaitAsync(TimeSpan.FromSeconds(10));

        // Otra transaccion extiende PurgeAt mientras el limpiador aun no bloqueo.
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Salas
                .Where(s => s.Id == salaId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PurgeAt, Now.AddHours(2)));
        }

        lockPause.Resume();

        var deleted = await purgeTask;

        // La revalidacion bajo bloqueo ve el PurgeAt vigente y descarta la sala.
        Assert.Equal(0, deleted);

        await using var check = await factory.CreateDbContextAsync();
        var sala = await check.Salas.AsNoTracking().SingleAsync(s => s.Id == salaId);
        Assert.Equal(Now.AddHours(2), sala.PurgeAt);
    }

    [Fact]
    public async Task Purge_PersistenceFailure_RollsBackWholeBatch()
    {
        await using var factory = await CreateFactoryAsync();

        Guid salaOne;
        Guid salaTwo;
        Guid sesionOne;
        Guid solicitudOne;

        await using (var db = await factory.CreateDbContextAsync())
        {
            var djId = await SeedDjAsync(db, "rollback@test.com");

            salaOne = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1), purgeAt: Now.AddHours(-2));
            sesionOne = await SeedSesionAsync(db, salaOne, Now.AddDays(-2), Now.AddDays(-1));
            solicitudOne = await SeedSolicitudAsync(db, salaOne, sesionOne, generoId: 1);

            salaTwo = await SeedSalaAsync(
                db, djId, NewCode(),
                startsAt: Now.AddDays(-2), endsAt: Now.AddDays(-1), purgeAt: Now.AddHours(-1));
            var sesionTwo = await SeedSesionAsync(db, salaTwo, Now.AddDays(-2), Now.AddDays(-1));
            await SeedSolicitudAsync(db, salaTwo, sesionTwo, generoId: 1);
        }

        // Trigger temporal en la base desechable: rechaza cualquier DELETE de salas.
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE OR REPLACE FUNCTION reject_room_delete_retention_test() RETURNS trigger AS $fn$
                BEGIN
                    RAISE EXCEPTION 'retention test: delete blocked';
                END;
                $fn$ LANGUAGE plpgsql;
                """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER reject_room_delete_retention_test
                BEFORE DELETE ON salas
                FOR EACH ROW
                EXECUTE FUNCTION reject_room_delete_retention_test();
                """);
        }

        await Assert.ThrowsAsync<PostgresException>(() => RunPurgeAsync(factory));

        await using var check = await factory.CreateDbContextAsync();

        Assert.True(await check.Salas.AnyAsync(s => s.Id == salaOne));
        Assert.True(await check.Salas.AnyAsync(s => s.Id == salaTwo));
        Assert.True(await check.SesionesInvitado.AnyAsync(s => s.Id == sesionOne && s.SalaId == salaOne));
        Assert.True(await check.Solicitudes.AnyAsync(s => s.Id == solicitudOne));
    }
}
