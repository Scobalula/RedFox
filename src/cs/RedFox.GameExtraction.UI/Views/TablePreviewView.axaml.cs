using Avalonia.Controls;
using Avalonia.Data;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Builds one grid column per table column of the bound <see cref="TablePreviewViewModel"/>.
/// </summary>
public partial class TablePreviewView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TablePreviewView"/> class.
    /// </summary>
    public TablePreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        TableGrid.Columns.Clear();
        if (DataContext is not TablePreviewViewModel viewModel)
        {
            return;
        }

        for (int index = 0; index < viewModel.Columns.Count; index++)
        {
            bool isKeyColumn = index == 0 && viewModel.Columns.Count > 1;
            TableGrid.Columns.Add(new DataGridTextColumn
            {
                Header = viewModel.Columns[index],
                Binding = new Binding($"[{index}]"),
                Width = isKeyColumn ? DataGridLength.Auto : new DataGridLength(1, DataGridLengthUnitType.Star),
                MaxWidth = isKeyColumn ? 420 : double.PositiveInfinity,
            });
        }
    }
}
