using MiShell.UI.Prompt;
using MiShell.UI.Themes;
using System.Text.RegularExpressions;

namespace MiShell.Core.Tests;

/// <summary>Comprueba el formato de hora de 12 horas y la distinción de colores entre los roles del tema.</summary>
public sealed partial class ColorTests
{
    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex Ansi();

    [Theory]
    [InlineData(20, 17, "8:17 PM")]
    [InlineData(0, 5, "12:05 AM")]
    [InlineData(12, 0, "12:00 PM")]
    [InlineData(9, 30, "9:30 AM")]
    public void ClockFormatUsesTwelveHours(int hour, int minute, string expected) =>
        Assert.Equal(expected, ClockFormat.Hour(new DateTime(2026, 10, 22, hour, minute, 0)));

    [Fact]
    public void ClockFormatWithSecondsKeepsMeridiem() =>
        Assert.Equal("8:17:05 PM", ClockFormat.HourWithSeconds(new DateTime(2026, 10, 22, 20, 17, 5)));

    [Theory]
    [InlineData("contrast")]
    [InlineData("paper")]
    [InlineData("dracula")]
    [InlineData("synthwave")]
    [InlineData("crt")]
    public void ColoredThemesSeparatePathAndTag(string name)
    {
        var theme = ThemeManager.Themes[name];
        Assert.NotEqual(theme.Path, theme.Tag);
        Assert.NotEqual(theme.Time, theme.Tag);
        Assert.NotEqual(theme.Muted, theme.Path);
    }

    [Fact]
    public void PromptShowsTwelveHourClockWithoutAnsiInPlainTheme()
    {
        var themes = new ThemeManager();
        themes.TrySelect("plain", out _);
        var prompt = new PromptRenderer(themes, new IconProvider("ascii"), showTime: true).Render(0, null);
        Assert.DoesNotContain('\u001b', prompt);
        Assert.Matches(@"\d{1,2}:\d{2} (AM|PM)", prompt);
    }

    [Fact]
    public void ColorizeUsesTheThemeCommandColorAndMarksOptions()
    {
        var colored = MiShell.UI.Input.LineEditor.Colorize("ls -l", w => w == "ls", ConsoleColor.Yellow, ConsoleColor.DarkCyan);
        Assert.Contains("\u001b[93mls", colored);
        Assert.Contains("\u001b[36m-l", colored);
        Assert.Equal("ls -l", Ansi().Replace(colored, string.Empty));
    }
}
