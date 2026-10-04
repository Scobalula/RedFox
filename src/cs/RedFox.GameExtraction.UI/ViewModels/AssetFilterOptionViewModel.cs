using CommunityToolkit.Mvvm.ComponentModel;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Represents one selectable value in a multi-select asset filter.
/// </summary>
public sealed class AssetFilterOptionViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetFilterOptionViewModel"/> class.
    /// </summary>
    /// <param name="name">The option label.</param>
    /// <param name="isSelected">Whether the option is selected initially.</param>
    /// <param name="selectionChanged">The callback for selection changes.</param>
    public AssetFilterOptionViewModel(string name, bool isSelected, Action selectionChanged)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _isSelected = isSelected;
        _selectionChanged = selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));
    }

    /// <summary>
    /// Gets the option label.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets whether the option is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged();
            }
        }
    }
}
