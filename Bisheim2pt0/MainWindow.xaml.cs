using System.Windows;
using Bisheim2pt0.Services;

namespace Bisheim2pt0;

public partial class MainWindow : Window
{
    private readonly LauncherService _launcher = new();
    private LauncherState? _state;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync(bool manageBusy = true)
    {
        if (manageBusy) SetBusy(true, "Checking Valheim and modpack…");
        try
        {
            _state = await _launcher.GetStateAsync();
            VersionText.Text = _state.DisplayVersion;
            ServerText.Text = _state.ServerAddress;
            StatusText.Text = _state.Status;
            PlayButton.Content = _state.NeedsInstall ? "INSTALL & PLAY" : "PLAY VALHEIM";
        }
        catch (Exception ex)
        {
            _state = null;
            StatusText.Text = ex.Message;
            if (!manageBusy) throw;
        }
        finally
        {
            if (manageBusy) SetBusy(false);
        }
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        await RunInstallAsync(force: false, launchWhenDone: true);
    }

    private async void RepairButton_Click(object sender, RoutedEventArgs e)
    {
        await RunInstallAsync(force: true, launchWhenDone: false);
    }

    private async Task RunInstallAsync(bool force, bool launchWhenDone)
    {
        SetBusy(true, force ? "Repairing Bisheim…" : "Preparing Bisheim…");
        try
        {
            var progress = new Progress<InstallProgress>(p =>
            {
                StatusText.Text = p.Message;
                Progress.Value = p.Percent;
            });
            await _launcher.InstallAsync(force, progress);
            await RefreshAsync(manageBusy: false);
            if (launchWhenDone)
                await _launcher.LaunchValheimAsync();
            else
                MessageBox.Show("Bisheim was repaired successfully.", "Bisheim 2.0");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Bisheim 2.0", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        PlayButton.IsEnabled = !busy && _state is not null;
        RepairButton.IsEnabled = !busy;
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (message is not null) StatusText.Text = message;
    }
}
