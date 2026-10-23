using MiShell.Core.Contracts;
using System.Text;

namespace MiShell.Core.Parsing;

/// <summary>Error de sintaxis de la línea (comillas sin cerrar, operador incompleto, redirecciones duplicadas...).</summary>
public sealed class SyntaxException(string message) : Exception(message);

/// <summary>Analiza el subconjunto de sintaxis de MiShell, conservando los operadores entre comillas.</summary>
public sealed class Parser(IShellEnvironment environment)
{
    private sealed record Token(string Text, bool IsOperator = false);

    /// <summary>Convierte una línea de texto en un <see cref="CommandLine"/>: separa tokens respetando comillas, expande $VAR fuera de comillas simples y arma tuberías, redirecciones, && y ||.</summary>
    public CommandLine Parse(string input)
    {
        var tokens = Tokenize(input);
        var index = 0;
        var pipelines = new List<ChainedPipeline>();
        var chain = ChainOperator.Always;
        var background = false;
        while (index < tokens.Count)
        {
            var commands = new List<CommandSpec> { ReadCommand() };
            while (At("|"))
            {
                index++;
                commands.Add(ReadCommand());
            }

            pipelines.Add(new ChainedPipeline(commands, chain));
            if (index == tokens.Count) break;
            var op = tokens[index++].Text;
            if (op == "&")
            {
                if (index != tokens.Count || pipelines.Count != 1)
                    throw new SyntaxException("'&' solo se admite al final de una tubería sin &&/||.");
                background = true;
                break;
            }

            chain = op switch
            {
                "&&" => ChainOperator.AndAlso,
                "||" => ChainOperator.OrElse,
                _ => throw new SyntaxException($"Operador inesperado '{op}'."),
            };
            if (index == tokens.Count) throw new SyntaxException($"Falta un comando después de '{op}'.");
        }

        return new CommandLine(pipelines, background);

        bool At(string op) => index < tokens.Count && tokens[index] is { IsOperator: true } t && t.Text == op;

        CommandSpec ReadCommand()
        {
            string? executable = null;
            var arguments = new List<string>();
            var redirects = new List<Redirection>();
            while (index < tokens.Count)
            {
                var token = tokens[index];
                if (token.IsOperator && token.Text is "|" or "&&" or "||" or "&") break;
                index++;
                if (token.IsOperator)
                {
                    var kind = token.Text switch
                    {
                        "<" => RedirectionKind.Input,
                        ">" => RedirectionKind.OutputTruncate,
                        ">>" => RedirectionKind.OutputAppend,
                        _ => throw new SyntaxException($"Operador inesperado '{token.Text}'."),
                    };
                    if (index == tokens.Count || tokens[index].IsOperator || tokens[index].Text.Length == 0)
                        throw new SyntaxException($"Falta el archivo después de '{token.Text}'.");
                    if (redirects.Any(r => (r.Kind == RedirectionKind.Input) == (kind == RedirectionKind.Input)))
                        throw new SyntaxException("Solo se admite una redirección de entrada y una de salida por comando.");
                    redirects.Add(new Redirection(kind, tokens[index++].Text));
                }
                else if (executable is null) executable = token.Text;
                else arguments.Add(token.Text);
            }

            if (string.IsNullOrEmpty(executable)) throw new SyntaxException("Falta un comando.");
            return new CommandSpec(executable, arguments, redirects);
        }
    }

    private List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        var current = new StringBuilder();
        var unquoted = new StringBuilder();
        var inToken = false;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsWhiteSpace(c)) { Flush(); continue; }
            if (c is '"' or '\'')
            {
                AppendUnquoted();
                inToken = true;
                var quote = c;
                var quoted = new StringBuilder();
                for (i++; i < input.Length && input[i] != quote; i++) quoted.Append(input[i]);
                if (i == input.Length) throw new SyntaxException("Comilla sin cerrar.");
                current.Append(quote == '"' ? environment.Expand(quoted.ToString()) : quoted.ToString());
            }
            else if (c is '>' or '<' or '|' or '&')
            {
                Flush();
                var op = c.ToString();
                if (i + 1 < input.Length && input[i + 1] == c && c is '>' or '|' or '&')
                    op += input[++i];
                tokens.Add(new Token(op, true));
            }
            else { unquoted.Append(c); inToken = true; }
        }

        Flush();
        return tokens;

        void AppendUnquoted()
        {
            current.Append(environment.Expand(unquoted.ToString()));
            unquoted.Clear();
        }

        void Flush()
        {
            if (!inToken) return;
            AppendUnquoted();
            tokens.Add(new Token(current.ToString()));
            current.Clear();
            inToken = false;
        }
    }
}
