using MiShell.Core.Contracts;
using System.Runtime.InteropServices;

namespace MiShell.Commands.SystemInfo;

/// <summary><c>version</c>: versión de MiShell, del runtime de .NET y del sistema, con el equipo de desarrollo y la asignatura.</summary>
public sealed class VersionCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    /// <summary>Integrantes del equipo con su carné; un único lugar para cambiarlos.</summary>
    private static readonly (string Name, string Id)[] Team =
    [
        ("Francis Dayan Flores Munguía", "2021-0214U"),
        ("Erick Hazael Morales Jarquín", "2023-0863U"),
        ("Denis Josué Wong Sánchez", "2019-0919U"),
    ];

    private const string Course = "Arquitectura de Sistemas Operativos";

    public string Name => "version";

    public string Description => "Muestra la versión de MiShell, de .NET y del sistema, y el equipo de desarrollo.";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var assembly = typeof(VersionCommand).Assembly.GetName().Version;
        var theme = renderer.Theme;
        renderer.WriteLine($"MiShell 1.0 · .NET {Environment.Version} · {RuntimeInformation.OSDescription} · {RuntimeInformation.OSArchitecture}", theme.Accent);
        renderer.WriteLine();
        renderer.WriteLine("  Equipo de desarrollo", theme.Accent);
        var width = Team.Max(m => m.Name.Length);
        foreach (var (name, id) in Team)
        {
            renderer.Write("    ● ", theme.Success);
            renderer.Write(name.PadRight(width), theme.Command);
            renderer.WriteLine("   " + id, theme.Muted);
        }

        renderer.WriteLine();
        renderer.Write("  Asignatura: ", theme.Muted);
        renderer.WriteLine(Course);
        renderer.WriteLine();
        renderer.RenderList("Detalles técnicos",
        [
            ("MiShell", $"1.0 (ensamblado {assembly})"),
            ("Runtime", RuntimeInformation.FrameworkDescription),
            ("Sistema operativo", RuntimeInformation.OSDescription),
            ("Arquitectura del SO", RuntimeInformation.OSArchitecture.ToString()),
            ("Equipo", Environment.MachineName),
            ("Usuario", Environment.UserName),
            ("PID de MiShell", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]);
        return Task.FromResult(CommandResult.Ok);
    }
}
