using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RedFox.Graphics3D.Preview.ViewModels;

namespace RedFox.Graphics3D.Preview;

public partial class MainWindow : Window
{
    private readonly PreviewCliOptions _options;

    public MainWindow() : this(new PreviewCliOptions())
    {
    }

    public MainWindow(PreviewCliOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeComponent();

        Width = _options.Width;
        Height = _options.Height;
        Title = BuildTitle(_options.InputFiles);
        DataContext = new MainWindowViewModel(_options);

        if (_options.Hidden)
        {
            Opacity = 0.0;
            ShowInTaskbar = false;
        }

        DragDrop.SetAllowDrop(ViewerHost, true);
        ViewerHost.AddHandler(DragDrop.DragOverEvent, OnViewerDragOver);
        ViewerHost.AddHandler(DragDrop.DropEvent, OnViewerDrop);

        Viewer.FrameRendered += OnViewerFrameRendered;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OnAddFilesClick(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> paths = await PickFilesAsync(
            title: "Add scene files",
            allowMultiple: true,
            patterns: ["*.*"]);

        await ViewModel.AddFilesAsync(paths);
    }

    private async void OnBrowseEnvironmentMapClick(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> paths = await PickFilesAsync(
            title: "Select environment map",
            allowMultiple: false,
            patterns: ["*.hdr", "*.exr", "*.png", "*.jpg", "*.jpeg", "*.dds", "*.ktx", "*.*"]);

        string? path = paths.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(path))
            ViewModel.EnvironmentMapPath = path;
    }

    private void OnClearEnvironmentMapClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.ClearEnvironmentMap();
    }

    private async void OnViewerDrop(object? sender, DragEventArgs e)
    {
        IReadOnlyList<string> paths = ExtractLocalPaths(e);
        if (paths.Count == 0)
            return;

        await ViewModel.AddFilesAsync(paths);
        e.Handled = true;
    }

    private void OnViewerDragOver(object? sender, DragEventArgs e)
    {
        bool hasFiles = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnViewerFrameRendered(object? sender, EventArgs e)
    {
        if (_options.MaxFrames > 0 && Viewer.RenderedFrameCount >= _options.MaxFrames)
            Close();
    }

    private async Task<IReadOnlyList<string>> PickFilesAsync(string title, bool allowMultiple, IReadOnlyList<string> patterns)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            FileTypeFilter =
            [
                new FilePickerFileType("Supported files")
                {
                    Patterns = patterns.ToList(),
                },
            ],
        });

        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();
    }

    private static IReadOnlyList<string> ExtractLocalPaths(DragEventArgs e)
    {
        return e.DataTransfer.TryGetFiles()?
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList()
            ?? [];
    }

    private static string BuildTitle(IReadOnlyList<string> inputFiles)
    {
        if (inputFiles.Count == 0)
            return "RedFox Graphics3D Preview";

        if (inputFiles.Count == 1)
            return $"RedFox Graphics3D Preview - {Path.GetFileName(inputFiles[0])}";

        return $"RedFox Graphics3D Preview - {inputFiles.Count} files";
    }
}
