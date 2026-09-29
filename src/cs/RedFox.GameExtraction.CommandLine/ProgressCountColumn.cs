using Spectre.Console;
using Spectre.Console.Rendering;

namespace RedFox.GameExtraction.CommandLine;

internal sealed class ProgressCountColumn(Style style) : ProgressColumn
{
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        return new Text($"{task.Value:N0}/{task.MaxValue:N0}", style);
    }
}
