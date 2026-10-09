namespace MiShell.Core.Parsing;

/// <summary>Tipo de redirección de E/S solicitada para un comando.</summary>
public enum RedirectionKind
{
    /// <summary>stdout a archivo (trunca).</summary>
    OutputTruncate,

    /// <summary>stdout a archivo (añade).</summary>
    OutputAppend,

    /// <summary>stdin desde archivo.</summary>
    Input,
}

/// <summary>Una redirección asociada a un comando de la tubería.</summary>
/// <param name="Kind">Tipo de redirección.</param>
/// <param name="Path">Ruta del archivo destino/origen.</param>
public sealed record Redirection(RedirectionKind Kind, string Path);

/// <summary>Un comando dentro de una tubería: nombre, argumentos y redirecciones.</summary>
/// <param name="Executable">Nombre o ruta del programa.</param>
/// <param name="Arguments">Argumentos ya expandidos (variables de entorno resueltas).</param>
/// <param name="Redirections">Redirecciones pedidas por el usuario.</param>
public sealed record CommandSpec(string Executable, IReadOnlyList<string> Arguments, IReadOnlyList<Redirection> Redirections);

/// <summary>Condicional de encadenamiento entre segmentos con <c>&amp;&amp;</c> u <c>||</c>.</summary>
public enum ChainOperator
{
    Always,
    AndAlso,
    OrElse,
}

/// <summary>Una línea completa: lista de tuberías encadenadas y si corre en segundo plano.</summary>
/// <param name="Pipelines">Segmentos encadenados; cada uno es una tubería de 1..N comandos.</param>
/// <param name="RunInBackground">True si la línea terminaba con <c>&amp;</c>.</param>
public sealed record CommandLine(IReadOnlyList<ChainedPipeline> Pipelines, bool RunInBackground);

/// <summary>Una tubería (posiblemente de un solo comando) con su operador de encadenamiento.</summary>
/// <param name="Commands">Comandos conectados por <c>|</c>.</param>
/// <param name="Chain">Cómo se encadena con la tubería siguiente.</param>
public sealed record ChainedPipeline(IReadOnlyList<CommandSpec> Commands, ChainOperator Chain);
