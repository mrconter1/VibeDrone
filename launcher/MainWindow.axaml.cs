using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace VibeDroneLauncher;

public partial class MainWindow : Window
{
    private readonly Updater _up = new();
    private ReleaseInfo? _latest;
    private string? _installed;
    private bool _needsUpdate;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _installed = _up.GetInstalledVersion();
        ActionBtn.IsEnabled = false;
        Status.Text = "Checking for updates…";
        Notes.Text = "";

        try
        {
            _latest = await _up.GetLatestAsync();
        }
        catch
        {
            // Offline or API failure: fall back to playing whatever is installed.
            if (_up.IsInstalled)
            {
                VersionLine.Text = _installed is null ? "Installed" : $"Installed  v{_installed}";
                Notes.Text = "Couldn't reach the update server. You can still play the installed version.";
                Status.Text = "Offline";
                ActionBtn.Content = "Play";
                ActionBtn.IsEnabled = true;
            }
            else
            {
                Notes.Text = "Couldn't reach the update server, and the game isn't installed yet. " +
                             "Check your connection and reopen the launcher.";
                Status.Text = "Offline";
            }
            return;
        }

        _needsUpdate = Updater.NeedsUpdate(_latest.Tag, _installed);
        string latest = _latest.Tag.TrimStart('v', 'V');
        string status = "";

        if (!_up.IsInstalled)
        {
            VersionLine.Text = $"Not installed  ·  latest  v{latest}";
            ActionBtn.Content = "Install & Play";
        }
        else if (_needsUpdate)
        {
            // Updating is required: the only action updates before it launches - there is no
            // "play the old version" path while the update server is reachable.
            VersionLine.Text = $"Installed  v{_installed}  ·  update  v{latest}";
            ActionBtn.Content = "Update & Play";
            status = "A new version is required before you can play.";
        }
        else
        {
            VersionLine.Text = $"Installed  v{_installed}  ·  up to date";
            ActionBtn.Content = "Play";
        }

        Notes.Text = string.IsNullOrWhiteSpace(_latest.Body)
            ? "No release notes for this version."
            : _latest.Body.Trim();
        Status.Text = status;
        ActionBtn.IsEnabled = true;
    }

    private async void OnAction(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        ActionBtn.IsEnabled = false;

        try
        {
            if (_latest is not null && (_needsUpdate || !_up.IsInstalled))
            {
                Progress.IsVisible = true;
                Progress.Value = 0;
                Status.Text = _up.IsInstalled ? "Downloading update…" : "Downloading…";

                var progress = new Progress<double>(p => Dispatcher.UIThread.Post(() =>
                {
                    Progress.Value = p;
                    Status.Text = $"Downloading… {p:0}%";
                }));

                await _up.DownloadAndInstallAsync(_latest, progress);

                Status.Text = "Installed";
                Progress.IsVisible = false;
            }

            Status.Text = "Launching…";
            _up.LaunchGame();
            await Task.Delay(400);
            Close();
        }
        catch (Exception ex)
        {
            Progress.IsVisible = false;
            Status.Text = "Failed: " + ex.Message;
            ActionBtn.IsEnabled = true;
            _busy = false;
        }
    }
}
