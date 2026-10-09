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
}
