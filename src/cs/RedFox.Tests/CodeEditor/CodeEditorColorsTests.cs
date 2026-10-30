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
        host.Resources["AccentBrush"] = new SolidColorBrush(Color.Parse("#C68C32"));

        CodeEditorColors colors = CodeEditorColors.FromResources(host);

        Assert.Equal(Color.Parse("#171717"), colors.Background);
        Assert.Equal(Color.Parse("#171717"), colors.TabBackground);
        Assert.Equal(Color.Parse("#C68C32"), colors.Accent);
        Assert.Equal(Color.Parse("#66C68C32"), colors.Selection);
    }

    [Fact]
    public void AnyColourCanBeOverridden()
    {
        Color background = Color.Parse("#101010");

        Assert.Equal(background, (CodeEditorColors.VsCodeDark with { Background = background }).Background);
    }
}
