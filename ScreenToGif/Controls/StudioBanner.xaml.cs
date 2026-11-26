using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows.Threading;
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
    private const string Origin = "https://8.137.20.181";
    private const string Endpoint = Origin + "/v2/apps/" + AppId + "/ads";
    private Placement _shown;
    private Guid _exposure;
    private bool _impressed;
    private long _visibleSince;
    private readonly DispatcherTimer _visibility = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(5), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
    private CancellationTokenSource _refresh;
    private Uri _target;
    private bool _dismissed;

    public StudioBanner()
    {
        InitializeComponent();
        _visibility.Tick += (_, _) => CheckVisibility();
    }

    private void CheckVisibility()
    {
        var window = Window.GetWindow(this);
        if (_shown == null || !Card.IsVisible || window == null || window.WindowState == WindowState.Minimized ||
            Card.ActualWidth <= 0 || Card.ActualHeight <= 0) { _visibleSince = 0; return; }
        var bounds = Card.TransformToAncestor(window).TransformBounds(new Rect(Card.RenderSize));
        var clip = Rect.Intersect(bounds, new Rect(window.RenderSize));
        if (clip.IsEmpty || clip.Width * clip.Height < bounds.Width * bounds.Height / 2) { _visibleSince = 0; return; }
        if (_visibleSince == 0) _visibleSince = Environment.TickCount64;
        if (!_impressed && Environment.TickCount64 - _visibleSince >= 1000) { _impressed = true; Report("impression"); }
    }

    private async void Report(string type)
    {
        if (_shown?.creative_id == null) return;
        var payload = new { event_id = Guid.NewGuid(), exposure_id = _exposure, event_type = type,
            slot = _shown.slot, creative_id = _shown.creative_id, creative_version = _shown.creative_version,
            campaign_id = _shown.campaign_id, is_test = _shown.is_test, platform = "Windows",
            app_version = typeof(StudioBanner).Assembly.GetName().Version.ToString(3) };
        try { using var result = await Client.PostAsJsonAsync(Origin + "/v2/apps/" + AppId + "/events", payload); }
        catch (Exception) { /* Telemetry must not interrupt exports. */ }
    }

    private void HideAd()
    {
        _shown = null;
        _visibility.Stop();
        if (Card != null) Card.Visibility = Visibility.Collapsed;
    }

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
        HideAd();
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
                    var testing = Environment.GetEnvironmentVariable("KX_AD_TEST") == "1";
                    var version = typeof(StudioBanner).Assembly.GetName().Version.ToString(3);
                    var url = Endpoint + "?platform=Windows&app_version=" + version + (testing ? "&test=1" : "");
                    using var response = await Client.GetAsync(url, token);
                    var json = !testing && response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? await Client.GetStringAsync(Endpoint.Replace("/v2/", "/v1/"), token)
                        : await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync(token);
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
                        if (_shown == null || _shown.creative_id != ad.creative_id || _shown.creative_version.ToString() != ad.creative_version.ToString() || _shown.campaign_id != ad.campaign_id || _shown.is_test != ad.is_test)
                        {
                            _exposure = Guid.NewGuid();
                            _visibleSince = 0;
                            _impressed = false;
                        }
                        _shown = ad;
                        _visibility.Start();
                        Artwork.Source = bitmap;
                        Headline.Text = ad.title;
                        _target = target;
                        Card.Visibility = Visibility.Visible;
                    }
                    else
                        HideAd();
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    // An unavailable placement or unreadable creative leaves the footer empty.
                    HideAd();
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
        try { Report("click"); Process.Start(new ProcessStartInfo(_target.AbsoluteUri) { UseShellExecute = true }); }
        catch (Win32Exception) { }
    }

    private void Dismiss(object sender, RoutedEventArgs e)
    {
        Report("dismiss");
        _dismissed = true;
        Stop();
    }

    private sealed record Configuration(string app_id, Placement[] placements);
    private sealed record Placement(string slot, string kind, string title, string asset, string target, string creative_id, JsonElement creative_version, string campaign_id, bool is_test);
}
