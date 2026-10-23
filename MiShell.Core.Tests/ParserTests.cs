using MiShell.Core.Contracts;
using MiShell.Core.Parsing;

namespace MiShell.Core.Tests;

public sealed class FakeEnvironment : IShellEnvironment
{
    public string? GetVariable(string name) => name == "HOME" ? @"C:\Users\test" : null;

    public void SetVariable(string name, string value) => throw new NotSupportedException();

    public string Expand(string text) => text.Replace("$HOME", @"C:\Users\test");

    public IEnumerable<(string Name, string Value)> EnumerateVariables() => [];
}

public sealed class ParserTests
{
    private readonly Parser _parser = new(new FakeEnvironment());

    [Fact]
    public void ParseSimpleCommandHasNameAndArguments()
    {
        var line = _parser.Parse("echo hola mundo");
        var spec = Assert.Single(line.Pipelines).Commands.Single();
        Assert.Equal("echo", spec.Executable);
        Assert.Equal(["hola", "mundo"], spec.Arguments);
        Assert.False(line.RunInBackground);
    }

    [Fact]
    public void ParseQuotedArgumentKeepsSpaces()
    {
        var spec = Assert.Single(_parser.Parse(@"echo ""hola mundo""").Pipelines).Commands.Single();
        var arg = Assert.Single(spec.Arguments);
        Assert.Equal("hola mundo", arg);
    }

    [Fact]
    public void ParsePipelineConnectsThreeCommands()
    {
        var commands = Assert.Single(_parser.Parse("a | b | c").Pipelines).Commands;
        Assert.Equal(["a", "b", "c"], commands.Select(c => c.Executable));
    }

    [Theory]
    [InlineData(">", RedirectionKind.OutputTruncate)]
    [InlineData(">>", RedirectionKind.OutputAppend)]
    [InlineData("<", RedirectionKind.Input)]
    public void ParseRedirectionKindAndPath(string op, RedirectionKind expected)
    {
        var spec = Assert.Single(_parser.Parse($"cmd {op} archivo.txt").Pipelines).Commands.Single();
        var redirection = Assert.Single(spec.Redirections);
        Assert.Equal(expected, redirection.Kind);
        Assert.Equal("archivo.txt", redirection.Path);
    }

    [Fact]
    public void ParseBackgroundSetsFlag()
    {
        var line = _parser.Parse("cmd arg &");
        Assert.True(line.RunInBackground);
    }

    [Fact]
    public void ParseChainingAndThen()
    {
        var line = _parser.Parse("a && b || c");
        Assert.Equal(3, line.Pipelines.Count);
        Assert.Equal(ChainOperator.Always, line.Pipelines[0].Chain);
        Assert.Equal(ChainOperator.AndAlso, line.Pipelines[1].Chain);
        Assert.Equal(ChainOperator.OrElse, line.Pipelines[2].Chain);
    }

    [Fact]
    public void ParseVariableExpansionReplacesDollar()
    {
        var spec = Assert.Single(_parser.Parse("cd $HOME").Pipelines).Commands.Single();
        Assert.Equal("cd", spec.Executable);
        var arg = Assert.Single(spec.Arguments);
        Assert.Equal(@"C:\Users\test", arg);
    }

    [Fact]
    public void ParseUnclosedQuoteThrowsSyntax()
    {
        Assert.Throws<SyntaxException>(() => _parser.Parse("echo \"hola"));
    }

    [Fact]
    public void ParseEmptyInputProducesNoPipelines()
    {
        Assert.Empty(_parser.Parse("   ").Pipelines);
    }

    [Theory]
    [InlineData("a |")]
    [InlineData("a &&")]
    [InlineData("a ||")]
    [InlineData("| a")]
    [InlineData("a | | b")]
    [InlineData("a > | b")]
    [InlineData("a >")]
    [InlineData("a <")]
    [InlineData("&")]
    [InlineData("a & b")]
    [InlineData("a && b &")]
    [InlineData("a > one > two")]
    public void IncompleteSyntaxIsRejected(string input) =>
        Assert.Throws<SyntaxException>(() => _parser.Parse(input));

    [Theory]
    [InlineData("|")]
    [InlineData("&")]
    [InlineData("&&")]
    [InlineData("||")]
    [InlineData(">")]
    [InlineData(">>")]
    [InlineData("<")]
    public void QuotedOperatorsAreLiteralArguments(string literal)
    {
        var line = _parser.Parse($"echo \"{literal}\"");
        var command = Assert.Single(Assert.Single(line.Pipelines).Commands);
        Assert.Equal(literal, Assert.Single(command.Arguments));
        Assert.Empty(command.Redirections);
    }

    [Fact]
    public void QuotedPartsAndEmptyArgumentsArePreserved()
    {
        var command = Assert.Single(Assert.Single(_parser.Parse("echo prefix\"hello world\"tail \"\"").Pipelines).Commands);
        Assert.Equal(["prefixhello worldtail", ""], command.Arguments);
    }

    [Fact]
    public void SingleQuotesSuppressVariableExpansion()
    {
        var command = Assert.Single(Assert.Single(_parser.Parse("echo '$HOME' \"$HOME\"").Pipelines).Commands);
        Assert.Equal(["$HOME", @"C:\Users\test"], command.Arguments);
    }
}

public sealed class LineEditorColorizeTests
{
    [Fact]
    public void ColorizeKeepsVisibleTextAndMarksCommands()
    {
        var colored = MiShell.UI.Input.LineEditor.Colorize("ls | foo \"a b\" $X", w => w == "ls");
        var plain = System.Text.RegularExpressions.Regex.Replace(colored, "\u001b\\[[0-9;]*m", "");
        Assert.Equal("ls | foo \"a b\" $X", plain);
        Assert.Contains("\u001b[92mls", colored);
        Assert.Contains("\u001b[91mfoo", colored);
    }
}
