using MiShell.Core.Contracts;
using MiShell.Core.Jobs;

namespace MiShell.Commands.BuiltinCommands;

/// <summary><c>jobs</c>: lista los trabajos en segundo plano con PID, estado, resultado, inicio, duración y comando.</summary>
public sealed class JobsCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "jobs";

    public string Description => "Lista los procesos lanzados en segundo plano.";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var jobs = context.Jobs.Jobs;
        if (jobs.Count == 0)
        {
            renderer.WriteInfo("No hay trabajos en segundo plano.");
            return Task.FromResult(CommandResult.Ok);
        }

        renderer.RenderTable(
            "Trabajos en segundo plano",
            ["Job", "PID(s)", "Estado", "Resultado", "Inicio", "Duración", "Comando"],
            jobs.Select(j => (string[])
            [
                j.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Join(", ", j.Processes.Select(p => p.Id)),
                j.Status,
                j.Detail,
                MiShell.Core.ClockFormat.HourWithSeconds(j.StartedAt),
                $"{(DateTime.Now - j.StartedAt).TotalSeconds:F0} s",
                j.CommandLine,
            ]),
            cellColor: (row, column) => column == 2
                ? row[2] switch { "corriendo" => renderer.Theme.Info, "terminado" => renderer.Theme.Success, _ => renderer.Theme.ErrorColor }
                : column == 0 ? renderer.Theme.Muted : null);

        return Task.FromResult(CommandResult.Ok);
    }
}
