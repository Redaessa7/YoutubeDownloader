using System.Windows;
using System.Windows.Media;
using YoutubeDownloader.Pages;

namespace YoutubeDownloader;

public partial class MainWindow : Window
{
    private static readonly Color _activeRed    = Color.FromRgb(0xFF, 0x20, 0x20);
    private static readonly Color _iconActive   = Color.FromRgb(0x1A, 0x05, 0x05);
    private static readonly Color _iconInactive = Colors.Transparent;
    private static readonly Color _labelActive  = Color.FromRgb(0xFF, 0x20, 0x20);
    private static readonly Color _labelInactive= Color.FromRgb(0x66, 0x66, 0x66);
    private static readonly Color _strokeActive = Color.FromRgb(0xFF, 0x20, 0x20);
    private static readonly Color _strokeInactive=Color.FromRgb(0x55, 0x55, 0x55);

    public MainWindow()
    {
        InitializeComponent();
        NavigateTo(NavPage.Downloader);
    }

    private enum NavPage { Downloader, AppInfo }

    private void BtnNavDownloader_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Downloader);

    private void BtnNavAppInfo_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.AppInfo);

    private void NavigateTo(NavPage page)
    {
        switch (page)
        {
            case NavPage.Downloader:
                MainFrame.Navigate(new DownloaderPage());
                SetNavState(downloaderActive: true);
                break;
            case NavPage.AppInfo:
                MainFrame.Navigate(new AppInfoPage());
                SetNavState(downloaderActive: false);
                break;
        }
    }

    private void SetNavState(bool downloaderActive)
    {
        // ── Downloader ──
        IconBgDownloader.Background  = new SolidColorBrush(downloaderActive ? _iconActive   : _iconInactive);
        IconDownloader.Stroke        = new SolidColorBrush(downloaderActive ? _strokeActive : _strokeInactive);
        LabelDownloader.Foreground   = new SolidColorBrush(downloaderActive ? _labelActive  : _labelInactive);
        LabelDownloader.FontWeight   = downloaderActive ? FontWeights.SemiBold : FontWeights.Normal;

        // ── App Info ──
        IconBgAppInfo.Background     = new SolidColorBrush(downloaderActive ? _iconInactive : _iconActive);
        IconAppInfo.Stroke           = new SolidColorBrush(downloaderActive ? _strokeInactive: _strokeActive);
        LabelAppInfo.Foreground      = new SolidColorBrush(downloaderActive ? _labelInactive : _labelActive);
        LabelAppInfo.FontWeight      = downloaderActive ? FontWeights.Normal : FontWeights.SemiBold;
    }
    
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }
}