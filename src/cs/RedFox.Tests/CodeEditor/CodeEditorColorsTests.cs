using Avalonia.Controls;
using Avalonia.Media;
using RedFox.Avalonia.CodeEditor;

namespace RedFox.Tests.CodeEditor;

public sealed class CodeEditorColorsTests
{
    [Fact]
    public void MissingResourcesKeepTheVsCodePalette()
    {
        Assert.Equal(CodeEditorColors.VsCodeDark.Foreground, CodeEditorColors.FromResources(new Border()).Foreground);
    }

    [Fact]
    public void AppResourcesReplaceThePalette()
    {
        Border host = new();
        host.Resources["ThemeCardBrush"] = new SolidColorBrush(Color.Parse("#171717"));

        CodeEditorColors colors = CodeEditorColors.FromResources(host);

        Assert.Equal(Color.Parse("#171717"), colors.Background);
    }

    [Fact]
    public void TextAndSelectionStayVsCodeOnAnyTheme()
    {
        Border host = new();
        host.Resources["ThemeForegroundBrush"]	= new SolidColorBrush(Color.Parse("#FAFAFA"));
        host.Resources["AccentBrush"]			= new SolidColorBrush(Color.Parse("#414B57"));

        CodeEditorColors colors = CodeEditorColors.FromResources(host);

        Assert.Equal(CodeEditorColors.VsCodeDark.Foreground, colors.Foreground);
        Assert.Equal(CodeEditorColors.VsCodeDark.Selection, colors.Selection);
    }

    [Fact]
    public void AnyColourCanBeOverridden()
    {
        Color background = Color.Parse("#101010");

        Assert.Equal(background, (CodeEditorColors.VsCodeDark with { Background = background }).Background);
    }
}
