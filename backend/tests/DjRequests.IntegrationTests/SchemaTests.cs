using DjRequests.Api.Entities;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DjRequests.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class SchemaTests
{
    private readonly DatabaseFixture _fixture;

    public SchemaTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<DjRequestsDbContext> ContextoLimpioAsync()
    {
        return await _fixture.CrearContextoAisladoAsync();
    }

    private static string Normalizado(string correo) => correo.ToUpperInvariant();

    private static async Task<DjAccount> CrearDjAsync(DjRequestsDbContext db, string correo)
    {
        var dj = new DjAccount
        {
            Id = Guid.NewGuid(),
            UserName = correo,
            Email = correo,
            NormalizedEmail = Normalizado(correo),
            NormalizedUserName = Normalizado(correo),
            NombreArtistico = "DJ Test",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(dj);
        await db.SaveChangesAsync();
        return dj;
    }

    private static Sala CrearSala(DjAccount dj, string codigo)
    {
        var inicio = DateTimeOffset.UtcNow.AddHours(1);
        var fin = inicio.AddHours(4);
        return new Sala
        {
            Id = Guid.NewGuid(),
            DjId = dj.Id,
            Nombre = "Sala de prueba",
            Codigo = codigo,
            Estado = "abierta",
            StartsAt = inicio,
            EndsAt = fin,
            Timezone = "America/Mexico_City",
            PurgeAt = fin.AddHours(168),
            MaxSolicitudesSesion = 10,
            MaxPlaylistsSesion = 2,
            IntervaloEnvioSegundos = 30,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<SesionInvitado> CrearSesionAsync(DjRequestsDbContext db, Sala sala, string tokenHash)
    {
        var ahora = DateTimeOffset.UtcNow;
        var sesion = new SesionInvitado
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            TokenHash = tokenHash,
            CreatedAt = ahora,
            ExpiresAt = ahora.AddHours(2)
        };
        db.SesionesInvitado.Add(sesion);
        await db.SaveChangesAsync();
        return sesion;
    }

    // 1. Correo DJ normalizado duplicado falla; no hashes/credenciales en DTO.
    [Fact]
    public async Task CorreoDjkNormalizadoDuplicado_Falla()
    {
        await using var db = await ContextoLimpioAsync();
        await CrearDjAsync(db, "dj@example.com");

        var duplicado = new DjAccount
        {
            Id = Guid.NewGuid(),
            UserName = "dj2@example.com",
            Email = "DJ@Example.com",
            NormalizedEmail = Normalizado("dj@example.com"),
            NormalizedUserName = Normalizado("dj2@example.com"),
            NombreArtistico = "DJ Duplicado",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(duplicado);

        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        if (ex.InnerException is PostgresException pg)
        {
            Assert.Equal("23505", pg.SqlState);
            Assert.Contains("normalized_email", pg.ConstraintName?.ToLowerInvariant());
        }
    }

    // 2. Sala con ends_at <= starts_at falla.
    [Fact]
    public async Task Sala_ConEndsAtMenorOIgualQueStartsAt_Falla()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj2@example.com");

        var sala = CrearSala(dj, "ABCDEFGHIJ");
        sala.EndsAt = sala.StartsAt.AddHours(-1);
        sala.PurgeAt = sala.StartsAt.AddHours(168);
        db.Salas.Add(sala);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // 2b. Sala válida pasa.
    [Fact]
    public async Task Sala_PurgeAtSeCalculaEnServicio_NoEnDB()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj3@example.com");

        var sala = CrearSala(dj, "ABCDEFGHIJ");
        db.Salas.Add(sala);
        await db.SaveChangesAsync();

        Assert.Equal(168, (sala.PurgeAt - sala.EndsAt).TotalHours);
    }

    // 3. Solicitud sin título o con estado desconocido falla.
    [Fact]
    public async Task Solicitud_SinTitulo_Falla()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj4@example.com");
        var sala = CrearSala(dj, "ABCDEFGHIJ");
        db.Salas.Add(sala);
        await db.SaveChangesAsync();
        var sesion = await CrearSesionAsync(db, sala, "hash-sin-titulo");

        db.Solicitudes.Add(new Solicitud
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Nombre = "   ",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        if (ex.InnerException is PostgresException pg)
        {
            Assert.Equal("23514", pg.SqlState);
        }
    }

    [Fact]
    public async Task Solicitud_ConEstadoDesconocido_Falla()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj5@example.com");
        var sala = CrearSala(dj, "ABCDEFGHIJ");
        db.Salas.Add(sala);
        await db.SaveChangesAsync();
        var sesion = await CrearSesionAsync(db, sala, "hash-estado");

        db.Solicitudes.Add(new Solicitud
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Nombre = "Cancion valida",
            Estado = "inventado",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        if (ex.InnerException is PostgresException pg)
        {
            Assert.Equal("23514", pg.SqlState);
        }
    }

    // 4. Sesión de sala A referenciada por petición/playlist de sala B falla por FK.
    [Fact]
    public async Task SesionDeSalaA_ReferencedFromSalaB_FallaPorFk()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj6@example.com");
        var salaA = CrearSala(dj, "AAAABBBBB1");
        var salaB = CrearSala(dj, "CCCCDDDDD1");
        db.Salas.AddRange(salaA, salaB);
        await db.SaveChangesAsync();

        var sesionA = await CrearSesionAsync(db, salaA, "hash-sala-a");

        db.Solicitudes.Add(new Solicitud
        {
            Id = Guid.NewGuid(),
            SalaId = salaB.Id,
            SesionId = sesionA.Id,
            Nombre = "Cancion cruzada",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var ex1 = await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        if (ex1.InnerException is PostgresException pg1)
        {
            Assert.Equal("23503", pg1.SqlState);
        }

        await using var db2 = _fixture.CrearContextoEnLaMismaBase(db);
        db2.ChangeTracker.Clear();

        db2.PlaylistsCompartidas.Add(new PlaylistCompartida
        {
            Id = Guid.NewGuid(),
            SalaId = salaB.Id,
            SesionId = sesionA.Id,
            Enlace = "https://open.spotify.com/playlist/ejemplo",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        });

        var ex2 = await Assert.ThrowsAnyAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        if (ex2.InnerException is PostgresException pg2)
        {
            Assert.Equal("23503", pg2.SqlState);
        }
    }

    // 5. Misma idempotency_key/sesión no crea segunda fila.
    [Fact]
    public async Task MismaIdempotencyKeyEnMismaSesion_NoCreaSegundaFila()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj7@example.com");
        var sala = CrearSala(dj, "AAAABBBBB2");
        db.Salas.Add(sala);
        await db.SaveChangesAsync();
        var sesion = await CrearSesionAsync(db, sala, "hash-idem");

        var clave = Guid.NewGuid();
        db.Solicitudes.Add(new Solicitud
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Nombre = "Primera peticion",
            IdempotencyKey = clave,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        db.Solicitudes.Add(new Solicitud
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Nombre = "Reintento con misma clave",
            IdempotencyKey = clave,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await using var db2 = _fixture.CrearContextoEnLaMismaBase(db);
        Assert.Equal(1, await db2.Solicitudes.CountAsync(s => s.SesionId == sesion.Id));
    }

    // 6. Borrar sala elimina contenido y sesiones; conserva DJ y catálogo.
    [Fact]
    public async Task BorrarSala_EliminaContenidoYSesiones_ConservaDjYCatalogo()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj8@example.com");
        var sala = CrearSala(dj, "AAAABBBBB3");
        db.Salas.Add(sala);
        await db.SaveChangesAsync();
        var sesion = await CrearSesionAsync(db, sala, "hash-borrado");

        var solicitud = new Solicitud
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Nombre = "Cancion a eliminar",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var playlist = new PlaylistCompartida
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Enlace = "https://open.spotify.com/playlist/eliminar",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Solicitudes.Add(solicitud);
        db.PlaylistsCompartidas.Add(playlist);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM salas WHERE id = {sala.Id}");

        await using var db2 = _fixture.CrearContextoEnLaMismaBase(db);
        Assert.False(await db2.Solicitudes.AnyAsync(s => s.Id == solicitud.Id));
        Assert.False(await db2.PlaylistsCompartidas.AnyAsync(p => p.Id == playlist.Id));
        Assert.False(await db2.SesionesInvitado.AnyAsync(s => s.SalaId == sala.Id));
        Assert.True(await db2.Users.AnyAsync(u => u.Id == dj.Id));
        Assert.Equal(11, await db2.Generos.CountAsync());
    }

    // 7. Artista/género/enlace omitidos son null; playlist sin enlace falla.
    [Fact]
    public async Task ArtistaGeneroEnlaceOmitidos_ResultanNull()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj9@example.com");
        var sala = CrearSala(dj, "AAAABBBBB4");
        db.Salas.Add(sala);
        await db.SaveChangesAsync();
        var sesion = await CrearSesionAsync(db, sala, "hash-nulls");

        var solicitud = new Solicitud
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Nombre = "Solo nombre",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Solicitudes.Add(solicitud);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var persistida = await db.Solicitudes.AsNoTracking().SingleAsync(s => s.Id == solicitud.Id);
        Assert.Null(persistida.Artista);
        Assert.Null(persistida.GeneroId);
        Assert.Null(persistida.Enlace);
    }

    [Fact]
    public async Task Playlist_SinEnlace_Falla()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj10@example.com");
        var sala = CrearSala(dj, "AAAABBBBB5");
        db.Salas.Add(sala);
        await db.SaveChangesAsync();
        var sesion = await CrearSesionAsync(db, sala, "hash-playlist-vacia");

        db.PlaylistsCompartidas.Add(new PlaylistCompartida
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Enlace = string.Empty,
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        });

        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        if (ex.InnerException is PostgresException pg)
        {
            Assert.Equal("23514", pg.SqlState);
        }
    }

    // FK RESTRICT hacia djs: no se puede borrar DJ con salas.
    [Fact]
    public async Task BorrarDj_ConSalas_FallaPorFkRestrict()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj11@example.com");
        db.Salas.Add(CrearSala(dj, "AAAABBBBB6"));
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM djs WHERE \"Id\" = {dj.Id}"));
        Assert.Equal("23503", ex.SqlState);
        Assert.Equal("FK_salas_djs_dj_id", ex.ConstraintName);

        await using var db2 = _fixture.CrearContextoEnLaMismaBase(db);
        Assert.True(await db2.Salas.AnyAsync(s => s.DjId == dj.Id));
    }

    [Fact]
    public async Task DjAccount_EmailObligatorio_Falla()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = new DjAccount
        {
            Id = Guid.NewGuid(),
            UserName = "dj_no_email@example.com",
            Email = null!,
            NormalizedEmail = null!,
            NormalizedUserName = Normalizado("dj_no_email@example.com"),
            NombreArtistico = "DJ",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(dj);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DjAccount_EstadoInvalido_Falla()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = new DjAccount
        {
            Id = Guid.NewGuid(),
            UserName = "dj_estado@example.com",
            Email = "dj_estado@example.com",
            NormalizedEmail = Normalizado("dj_estado@example.com"),
            NormalizedUserName = Normalizado("dj_estado@example.com"),
            NombreArtistico = "DJ",
            Estado = "invalido",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(dj);
        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        if (ex.InnerException is PostgresException pg)
        {
            Assert.Equal("23514", pg.SqlState);
        }
    }

    [Fact]
    public async Task Sala_EstadoInvalido_Falla()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj_sala_estado@example.com");
        var sala = CrearSala(dj, "ZZZZZZZZZ1");
        sala.Estado = "inventado";
        db.Salas.Add(sala);
        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        if (ex.InnerException is PostgresException pg)
        {
            Assert.Equal("23514", pg.SqlState);
        }
    }

    [Fact]
    public async Task Sala_NombreVacio_Falla()
    {
        await using var db = await ContextoLimpioAsync();
        var dj = await CrearDjAsync(db, "dj_sala_nombre@example.com");
        var sala = CrearSala(dj, "ZZZZZZZZZ2");
        sala.Nombre = "   ";
        db.Salas.Add(sala);
        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        if (ex.InnerException is PostgresException pg)
        {
            Assert.Equal("23514", pg.SqlState);
        }
    }
}