using DjRequests.Api.Modules.Administration.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DjRequests.Api.Modules.Administration;

public static class AdminProvisioningCommand
{
    public static bool IsRequested(string[] args) =>
        args.Any(a => string.Equals(a, "--provision-admin", StringComparison.OrdinalIgnoreCase));

    public static async Task<int> RunAsync(IServiceProvider services)
    {
        Console.Write("Nombre del administrador: ");
        var name = Console.ReadLine();

        Console.Write("Correo electronico: ");
        var email = Console.ReadLine();

        Console.Write("Contrasena: ");
        var password = ReadPassword();

        Console.Write("Repita la contrasena: ");
        var confirmation = ReadPassword();

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Las contrasenas no coinciden.");
            return 1;
        }

        using var scope = services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<AdminProvisioningService>();

        try
        {
            var adminId = await provisioning.ProvisionAsync(name ?? string.Empty, email ?? string.Empty, password);
            Console.WriteLine($"Administrador creado: {adminId}");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (DbUpdateException)
        {
            Console.Error.WriteLine(
                "No se pudo guardar la cuenta administrativa. Verifica la migracion AddAdminIdentity y que el correo no este duplicado.");
            return 1;
        }
        catch (NpgsqlException)
        {
            Console.Error.WriteLine(
                "No se pudo acceder a la base de datos. Verifica la conexion y que la migracion AddAdminIdentity este aplicada.");
            return 1;
        }
    }

    private static string ReadPassword()
    {
        var chars = new List<char>();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                    Console.Write("\b \b");
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
                Console.Write("*");
            }
        }

        return new string(chars.ToArray());
    }
}
