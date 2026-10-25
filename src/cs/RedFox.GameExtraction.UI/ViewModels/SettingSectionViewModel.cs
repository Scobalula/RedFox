namespace RedFox.GameExtraction.UI.ViewModels;

public sealed class SettingSectionViewModel
{
    public string? Title { get; }

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

    public IReadOnlyList<GameExtractionSettingViewModel> Settings { get; }

    public SettingSectionViewModel(string? title, IReadOnlyList<GameExtractionSettingViewModel> settings)
    {
        Title		= title;
        Settings	= settings;
    }
}
