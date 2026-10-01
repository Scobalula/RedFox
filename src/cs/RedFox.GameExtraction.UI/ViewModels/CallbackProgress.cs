namespace RedFox.GameExtraction.UI.ViewModels;

internal sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
{
    public void Report(T value) => callback(value);
}
