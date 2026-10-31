using System.Windows.Input;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Runs an editor's save action from its key binding.
/// </summary>
/// <param name="save">The action to run.</param>
internal sealed class CodeEditorSaveCommand(Action save) : ICommand
{
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => save();
}
