using MiShell.Core.Contracts;

namespace MiShell.Core.Parsing;

/// <summary>Error de sintaxis detectado durante el análisis.</summary>
public sealed class SyntaxException(string message) : Exception(message);

/// <summary>Convierte una línea de entrada en un <see cref="CommandLine"/> (AST).</summary>
public sealed class Parser(IShellEnvironment environment)
{
    public CommandLine Parse(string input)
    {
        var tokens = Tokenize(input);
        var pipelines = new List<ChainedPipeline>();
        var chain = ChainOperator.Always;
        var runInBackground = false;

        while (tokens.Count > 0)
        {
            var (commands, nextChain, background) = ParsePipeline(tokens);
            runInBackground |= background;
            if (commands.Count == 0)
            {
                break;
            }

            pipelines.Add(new ChainedPipeline(commands, chain));
            chain = nextChain;
        }

        if (pipelines.Count == 0)
        {
            return new CommandLine([], false);
        }

        return tokens.Count > 0 ? throw new SyntaxException($"Token inesperado '{tokens[0]}'.") : new CommandLine(pipelines, runInBackground);
    }

    private static (List<CommandSpec> Commands, ChainOperator NextChain, bool Background) ParsePipeline(List<string> tokens)
    {
        var commands = new List<CommandSpec>();
        var background = false;

        while (tokens.Count > 0)
        {
            var (spec, chain, endedPipeline, sawBackground) = ParseCommand(tokens);
            background |= sawBackground;
            if (spec is not null)
            {
                commands.Add(spec);
            }

            if (endedPipeline)
            {
                return (commands, chain, background);
            }

            if (tokens.Count == 0)
            {
                break;
            }

            if (tokens[0] != "|") continue;

            tokens.RemoveAt(0);
            if (tokens.Count == 0 || tokens[0] is "|" or "&" or "&&" or "||")
            {
                throw new SyntaxException("Falta un comando después de '|'.");
            }
        }

        return (commands, ChainOperator.Always, background);
    }

    private static (CommandSpec? Spec, ChainOperator Chain, bool EndedPipeline, bool Background) ParseCommand(List<string> tokens)
    {
        string? executable = null;
        var arguments = new List<string>();
        var redirections = new List<Redirection>();

        while (tokens.Count > 0)
        {
            var token = tokens[0];
            if (token is "|" or "&&" or "||" or "&")
            {
                tokens.RemoveAt(0);
                var chain = token switch
                {
                    "&&" => ChainOperator.AndAlso,
                    "||" => ChainOperator.OrElse,
                    _ => ChainOperator.Always,
                };
                return (MakeSpec(executable, arguments, redirections), chain, token != "|", token == "&");
            }

            tokens.RemoveAt(0);
            if (RedirectionKindFor(token) is { } kind)
            {
                if (tokens.Count == 0)
                {
                    throw new SyntaxException($"Falta el archivo después de '{token}'.");
                }

                redirections.Add(new Redirection(kind, tokens[0]));
                tokens.RemoveAt(0);
                continue;
            }

            if (executable is null)
            {
                executable = token;
            }
            else
            {
                arguments.Add(token);
            }
        }

        return (MakeSpec(executable, arguments, redirections), ChainOperator.Always, true, false);
    }

    private static CommandSpec? MakeSpec(string? executable, List<string> arguments, List<Redirection> redirections) =>
        executable is null
            ? redirections.Count > 0
                ? throw new SyntaxException("Una redirección necesita un comando.")
                : null
            : new CommandSpec(executable, arguments, redirections);

    private static RedirectionKind? RedirectionKindFor(string token) => token switch
    {
        ">" => RedirectionKind.OutputTruncate,
        ">>" => RedirectionKind.OutputAppend,
        "<" => RedirectionKind.Input,
        _ => null,
    };

    /// <summary>Divide la entrada respetando comillas y expande variables <c>$VAR</c>.</summary>
    private List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inToken = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            switch (c)
            {
                case ' ' or '\t':
                    Flush();
                    break;
                case '"' or '\'':
                    Flush();
                    i = ReadQuoted(input, i, current);
                    inToken = true;
                    break;
                case '>' or '<' or '|':
                    {
                        Flush();
                        string op = c == '|' && i + 1 < input.Length && input[i + 1] == '|' ? "||" : c.ToString();
                        if (op == "||")
                        {
                            i++;
                        }
                        else if (c == '>' && i + 1 < input.Length && input[i + 1] == '>')
                        {
                            op = ">>";
                            i++;
                        }

                        tokens.Add(op);
                        break;
                    }

                case '&':
                    Flush();
                    tokens.Add(i + 1 < input.Length && input[i + 1] == '&' ? "&&" : "&");
                    if (i + 1 < input.Length && input[i + 1] == '&')
                    {
                        i++;
                    }

                    break;
                default:
                    current.Append(c);
                    inToken = true;
                    break;
            }
        }

        Flush();
        return tokens;

        void Flush()
        {
            if (!inToken) return;

            tokens.Add(environment.Expand(current.ToString()));
            current.Clear();
            inToken = false;
        }
    }

    private static int ReadQuoted(string input, int quoteIndex, System.Text.StringBuilder target)
    {
        var quote = input[quoteIndex];
        for (var i = quoteIndex + 1; i < input.Length; i++)
        {
            var c = input[i];
            if (c == quote)
            {
                return i;
            }

            target.Append(c);
        }

        throw new SyntaxException($"Comilla sin cerrar cerca de '{input[Math.Max(0, quoteIndex - 5)..]}'.".Trim());
    }
}
