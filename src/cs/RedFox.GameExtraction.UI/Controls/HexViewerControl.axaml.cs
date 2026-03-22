using System.Text;
using Avalonia;
using Avalonia.Controls;

namespace RedFox.GameExtraction.UI.Controls;

public partial class HexViewerControl : UserControl
{
    public static readonly StyledProperty<byte[]?> DataProperty =
        AvaloniaProperty.Register<HexViewerControl, byte[]?>(nameof(Data));

    public byte[]? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public HexViewerControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataProperty)
        {
            RenderHex(change.GetNewValue<byte[]?>());
        }
    }

    private void RenderHex(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            HexContent.Text = "No data to display";
            return;
        }

        var sb = new StringBuilder();
        const int bytesPerRow = 16;
        // Limit display to avoid performance issues with very large data
        int maxBytes = Math.Min(data.Length, 1024 * 4); // 4 KB display limit for fast rendering

        for (int offset = 0; offset < maxBytes; offset += bytesPerRow)
        {
            // Offset column — 8 hex chars + 4 spaces to align with header "Offset    "
            sb.Append($"{offset:X8}  ");

            // Hex bytes — matches header "00 01 02 ... 0F"
            int count = Math.Min(bytesPerRow, maxBytes - offset);
            for (int i = 0; i < bytesPerRow; i++)
            {
                if (i == 8) sb.Append(' '); // Extra space between groups of 8

                if (i < count)
                    sb.Append($"{data[offset + i]:X2} ");
                else
                    sb.Append("   ");
            }

            sb.Append(' ');

            // ASCII column
            for (int i = 0; i < count; i++)
            {
                byte b = data[offset + i];
                sb.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
            }

            sb.AppendLine();
        }

        if (maxBytes < data.Length)
        {
            sb.AppendLine();
            sb.AppendLine($"... ({data.Length - maxBytes:N0} more bytes not shown)");
        }

        HexContent.Text = sb.ToString();
    }
}
