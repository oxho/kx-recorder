using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ScreenToGif.Controls;

public partial class StudioBanner : UserControl
{
    private const string AppId = "a3f894de5d95";
    private const string Endpoint = "https://8.137.20.181/v1/apps/" + AppId + "/ads";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(5), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
    private CancellationTokenSource _refresh;
    private Uri _target;
    private bool _dismissed;

    public StudioBanner() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NetworkChange.NetworkAvailabilityChanged += NetworkChanged;
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        NetworkChange.NetworkAvailabilityChanged -= NetworkChanged;
        Stop();
    }

    private void NetworkChanged(object sender, NetworkAvailabilityEventArgs e) =>
        Dispatcher.InvokeAsync(Refresh);

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => Refresh();

    private void Stop()
    {
        _refresh?.Cancel();
        _refresh = null;
        _target = null;
        if (Card != null)
            Card.Visibility = Visibility.Collapsed;
    }

    private async void Refresh()
    {
        Stop();
        if (!IsLoaded || !IsVisible || _dismissed || !NetworkInterface.GetIsNetworkAvailable())
            return;

        using var refresh = new CancellationTokenSource();
        _refresh = refresh;
        var token = refresh.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var json = await Client.GetStringAsync(Endpoint, token);
                    var config = JsonSerializer.Deserialize<Configuration>(json);
                    var ad = config?.app_id == AppId ? config.placements?.FirstOrDefault(p =>
                        p?.slot == "export-complete" && p.kind == "banner" && !string.IsNullOrWhiteSpace(p.title)) : null;
                    if (ad != null && Https(ad.asset, out var asset) && Https(ad.target, out var target))
                    {
                        using var stream = new MemoryStream(await Client.GetByteArrayAsync(asset, token));
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 160;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        token.ThrowIfCancellationRequested();
                        Artwork.Source = bitmap;
                        Headline.Text = ad.title;
                        _target = target;
                        Card.Visibility = Visibility.Visible;
                    }
                    else
                        Card.Visibility = Visibility.Collapsed;
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    // An unavailable placement or unreadable creative leaves the footer empty.
                    Card.Visibility = Visibility.Collapsed;
                }

                await Task.Delay(TimeSpan.FromSeconds(30), token);
            }
        }
        catch (Exception) when (token.IsCancellationRequested) { }
        finally
        {
            if (_refresh == refresh)
                _refresh = null;
        }
    }

    private static bool Https(string value, out Uri uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);

    private void OpenTarget(object sender, RoutedEventArgs e)
    {
        if (_target == null)
            return;
        try { Process.Start(new ProcessStartInfo(_target.AbsoluteUri) { UseShellExecute = true }); }
        catch (Win32Exception) { }
    }

    private void Dismiss(object sender, RoutedEventArgs e)
    {
        _dismissed = true;
        Stop();
    }

    private sealed record Configuration(string app_id, Placement[] placements);
    private sealed record Placement(string slot, string kind, string title, string asset, string target);
}
