using ScreenToGif.Util;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace ScreenToGif.Views.Settings;

public partial class AboutSettings : Page
{
    public AboutSettings()
    {
        InitializeComponent();

#if FULL_MULTI_MSIX_STORE
        StoreTextBlock.Visibility = Visibility.Visible;
#endif
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;

        try
        {
            ProcessHelper.StartWithShell(e.Uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            LogWriter.Log(ex, "Open Hyperlink");
        }
    }
}