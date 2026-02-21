using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YoutubeDownloader.Pages;

public partial class DownloaderPage : Page
{
    // ── State ────────────────────────────────────────────────────────────────
    private List<VideoFormat> _formatsList = new();
    private bool   _isPlaylist    = false;
    private string _playlistFolder = "";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private const int DownloadFragments = 8;

    // ── Model ────────────────────────────────────────────────────────────────
    public class VideoFormat
    {
        public string Id       { get; set; } = "";
        public string Display  { get; set; } = "";
        public string Ext      { get; set; } = "";
        public long   Filesize { get; set; }
    }

    public DownloaderPage()
    {
        InitializeComponent();
    }

    // ── Placeholder ──────────────────────────────────────────────────────────
    private void TxtUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtPlaceholder.Visibility = string.IsNullOrEmpty(TxtUrl.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ── Detect playlist URL ──────────────────────────────────────────────────
    private static bool DetectPlaylist(string url)
    {
        bool hasList  = url.Contains("list=", StringComparison.OrdinalIgnoreCase);
        bool hasVideo = Regex.IsMatch(url, @"[?&]v=[\w-]+", RegexOptions.IgnoreCase);
        return hasList && !hasVideo;
    }

    // ── Analyze ──────────────────────────────────────────────────────────────
    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        string url = TxtUrl.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        BtnAnalyze.IsEnabled  = false;
        BtnDownload.IsEnabled = false;
        VideoCard.Visibility    = Visibility.Collapsed;
        PlaylistCard.Visibility = Visibility.Collapsed;
        SetStatus("Analyzing…", "#FF9900");

        _isPlaylist = DetectPlaylist(url);

        if (_isPlaylist)
            await AnalyzePlaylist(url);
        else
            await AnalyzeVideo(url);

        BtnAnalyze.IsEnabled = true;
    }

    // ── Analyze — single video ───────────────────────────────────────────────
    private async Task AnalyzeVideo(string url)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName               = "yt-dlp.exe",
                Arguments              = $"--dump-json \"{url}\"",
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            string json = await Task.Run(() =>
            {
                using var proc = Process.Start(startInfo);
                return proc?.StandardOutput.ReadToEnd() ?? "";
            });

            if (string.IsNullOrWhiteSpace(json))
            {
                SetStatus("No data returned. Check the URL or yt-dlp.", "#FF4444");
                return;
            }

            var node    = JsonNode.Parse(json);
            var formats = node?["formats"]?.AsArray();
            if (formats == null) { SetStatus("Could not parse video formats.", "#FF4444"); return; }

            string title    = node?["title"]?.ToString()    ?? "Unknown Title";
            string channel  = node?["uploader"]?.ToString() ?? "Unknown Channel";
            string thumbUrl = node?["thumbnail"]?.ToString() ?? "";

            _formatsList = formats
                .Where(f => f?["vcodec"]?.ToString() is string vc && vc != "none")
                .Select(f =>
                {
                    long size = 0;
                    if      (f!["filesize"]        is JsonNode fs) size = fs.GetValue<long>();
                    else if (f ["filesize_approx"] is JsonNode fa) size = fa.GetValue<long>();

                    string ext  = f["ext"]?.ToString() ?? "?";
                    string note = f["format_note"]?.ToString()
                               ?? f["resolution"]?.ToString()
                               ?? f["format_id"]!.ToString();
                    string sizeLabel = size > 0 ? $"{size / 1024.0 / 1024.0:F1} MB" : "size N/A";

                    return new VideoFormat
                    {
                        Id       = f["format_id"]!.ToString(),
                        Ext      = ext,
                        Filesize = size,
                        Display  = $"{note} ({ext}) — {sizeLabel}"
                    };
                })
                .OrderByDescending(x => x.Filesize)
                .ToList();

            if (_formatsList.Count == 0)
            {
                SetStatus("No downloadable video formats found.", "#FF4444");
                return;
            }

            TxtTitle.Text   = title;
            TxtChannel.Text = channel;

            if (!string.IsNullOrEmpty(thumbUrl))
            {
                try
                {
                    byte[] imgBytes = await _http.GetByteArrayAsync(thumbUrl);
                    using var ms = new MemoryStream(imgBytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption  = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    ImgThumbnail.Source = bmp;
                }
                catch { ImgThumbnail.Source = null; }
            }

            ComboFormats.ItemsSource   = _formatsList;
            ComboFormats.SelectedIndex = 0;

            VideoCard.Visibility  = Visibility.Visible;
            BtnDownload.IsEnabled = true;
            SetStatus("Ready to download", "#00D26A");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Analysis Error:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("Analysis failed.", "#FF4444");
        }
    }

    // ── Analyze — playlist ───────────────────────────────────────────────────
    private async Task AnalyzePlaylist(string url)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName               = "yt-dlp.exe",
                Arguments              = $"--flat-playlist --dump-json \"{url}\"",
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            var lines = new List<string>();
            await Task.Run(() =>
            {
                using var proc = Process.Start(startInfo)!;
                string? line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
                proc.WaitForExit();
            });

            if (lines.Count == 0)
            {
                SetStatus("No videos found in playlist. Check the URL.", "#FF4444");
                return;
            }

            string playlistTitle   = "Unknown Playlist";
            string playlistChannel = "Unknown Channel";
            try
            {
                var first = JsonNode.Parse(lines[0]);
                playlistTitle   = first?["playlist_title"]?.ToString()
                               ?? first?["playlist"]?.ToString()
                               ?? "YouTube Playlist";
                playlistChannel = first?["uploader"]?.ToString()
                               ?? first?["channel"]?.ToString()
                               ?? "Unknown Channel";
            }
            catch { /* ignore */ }

            TxtPlaylistTitle.Text   = playlistTitle;
            TxtPlaylistChannel.Text = playlistChannel;
            TxtVideoCount.Text      = lines.Count.ToString();

            PlaylistCard.Visibility          = Visibility.Visible;
            PlaylistProgressPanel.Visibility = Visibility.Collapsed;
            BtnDownload.IsEnabled            = true;
            SetStatus($"Playlist ready — {lines.Count} videos found", "#00D26A");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Playlist Analysis Error:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("Playlist analysis failed.", "#FF4444");
        }
    }

    // ── Choose folder ────────────────────────────────────────────────────────
    private void BtnChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description            = "Choose download folder for playlist",
            UseDescriptionForTitle = true,
            ShowNewFolderButton    = true
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _playlistFolder              = dlg.SelectedPath;
            TxtPlaylistFolder.Text       = _playlistFolder;
            TxtPlaylistFolder.Foreground =
                new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
        }
    }

    // ── Format combo changed ─────────────────────────────────────────────────
    private void ComboFormats_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboFormats.SelectedItem is VideoFormat fmt)
            TxtFormatInfo.Text = $"Format ID: {fmt.Id}  ·  Container: {fmt.Ext}";
        else
            TxtFormatInfo.Text = "Select your preferred resolution above";
    }

    // ── Download dispatcher ──────────────────────────────────────────────────
    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaylist)
            await DownloadPlaylist();
        else
            await DownloadSingleVideo();
    }

    // ── Download — single video ──────────────────────────────────────────────
    private async Task DownloadSingleVideo()
    {
        if (ComboFormats.SelectedItem is not VideoFormat selected)
        {
            System.Windows.MessageBox.Show("Please select a quality first.", "No Format Selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string cleanTitle = TxtTitle.Text;
        foreach (char c in Path.GetInvalidFileNameChars())
            cleanTitle = cleanTitle.Replace(c, '_');

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "MP4 Video|*.mp4",
            FileName = $"{cleanTitle}.mp4"
        };
        if (sfd.ShowDialog() != true) return;

        BtnDownload.IsEnabled = false;
        BtnAnalyze.IsEnabled  = false;
        ProgBar.Value         = 0;
        TxtPercent.Text       = "";

        string audioMode = RadioAudioHigh.IsChecked == true ? "bestaudio" : "worstaudio";

        var startInfo = new ProcessStartInfo
        {
            FileName               = "yt-dlp.exe",
            Arguments              = $"-f \"{selected.Id}+{audioMode}\" "
                                   + $"--concurrent-fragments {DownloadFragments} "
                                   + $"--newline --merge-output-format mp4 "
                                   + $"--ffmpeg-location . "
                                   + $"-o \"{sfd.FileName}\" \"{TxtUrl.Text}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        try
        {
            await Task.Run(() =>
            {
                using var process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(ev.Data))
                        ParseProgressLine(ev.Data, isPlaylist: false);
                };
                process.ErrorDataReceived += (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(ev.Data))
                        ParseProgressLine(ev.Data, isPlaylist: false);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            });

            ShowSuccess("Download Complete!", "File saved successfully.");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Download Error:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("Download failed.", "#FF4444");
        }
        finally
        {
            BtnDownload.IsEnabled   = true;
            BtnAnalyze.IsEnabled    = true;
            ProgBar.IsIndeterminate = false;
            ProgBar.Value           = 0;
            TxtPercent.Text         = "";
            TxtSpeed.Text           = "";
            TxtEta.Text             = "";
        }
    }

    // ── Download — playlist ──────────────────────────────────────────────────
    private async Task DownloadPlaylist()
    {
        if (string.IsNullOrEmpty(_playlistFolder) || !Directory.Exists(_playlistFolder))
        {
            System.Windows.MessageBox.Show("Please choose a valid save folder first.",
                "No Folder Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string qualityFilter = BuildPlaylistQualityFilter();
        string audioMode     = RadioAudioHigh.IsChecked == true ? "bestaudio" : "worstaudio";

        string playlistItems = "";
        string from = TxtPlaylistFrom.Text.Trim();
        string to   = TxtPlaylistTo.Text.Trim();
        if (!string.IsNullOrEmpty(from) || !string.IsNullOrEmpty(to))
        {
            string start = string.IsNullOrEmpty(from) ? "1"   : from;
            string end   = string.IsNullOrEmpty(to)   ? "999" : to;
            playlistItems = $"--playlist-items {start}:{end} ";
        }

        string outputTemplate = Path.Combine(_playlistFolder,
            "%(playlist_index)s - %(title)s.%(ext)s");

        var startInfo = new ProcessStartInfo
        {
            FileName               = "yt-dlp.exe",
            Arguments              = $"-f \"{qualityFilter}+{audioMode}\" "
                                   + $"--concurrent-fragments {DownloadFragments} "
                                   + $"--newline --merge-output-format mp4 "
                                   + $"--ffmpeg-location . "
                                   + playlistItems
                                   + $"-o \"{outputTemplate}\" \"{TxtUrl.Text}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        BtnDownload.IsEnabled            = false;
        BtnAnalyze.IsEnabled             = false;
        PlaylistProgressPanel.Visibility = Visibility.Visible;
        ProgBarPlaylist.Value            = 0;
        ProgBarVideo.Value               = 0;

        int totalVideos  = int.Parse(TxtVideoCount.Text);
        int currentIndex = 0;

        SetStatus("Starting playlist download…", "#FF9900");

        try
        {
            await Task.Run(() =>
            {
                using var process = new Process { StartInfo = startInfo };

                process.OutputDataReceived += (s, ev) =>
                {
                    if (string.IsNullOrEmpty(ev.Data)) return;

                    var newVideoMatch = Regex.Match(ev.Data,
                        @"\[download\] Downloading item (\d+) of (\d+)");
                    if (newVideoMatch.Success)
                    {
                        int idx   = int.Parse(newVideoMatch.Groups[1].Value);
                        int total = int.Parse(newVideoMatch.Groups[2].Value);
                        currentIndex = idx;
                        Dispatcher.Invoke(() =>
                        {
                            TxtCurrentVideoLabel.Text = $"Downloading video {idx} of {total}…";
                            TxtPlaylistOverall.Text   = $"{idx - 1}/{total} done";
                            ProgBarPlaylist.Value     = (idx - 1) * 100.0 / total;
                            ProgBarVideo.Value        = 0;
                            TxtVideoCount.Text        = total.ToString();
                        });
                    }

                    var titleMatch = Regex.Match(ev.Data,
                        @"\[download\] Destination: .+[\\/](?:\d+ - )?(.+?)\.(mp4|webm|mkv)");
                    if (titleMatch.Success)
                    {
                        string vTitle = titleMatch.Groups[1].Value;
                        Dispatcher.Invoke(() => TxtCurrentVideoTitle.Text = vTitle);
                    }

                    ParseProgressLine(ev.Data, isPlaylist: true,
                        currentIdx: currentIndex, total: totalVideos);
                };

                process.ErrorDataReceived += (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(ev.Data))
                        ParseProgressLine(ev.Data, isPlaylist: true,
                            currentIdx: currentIndex, total: totalVideos);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            });

            Dispatcher.Invoke(() =>
            {
                ProgBarPlaylist.Value = 100;
                ProgBarVideo.Value   = 100;
            });

            ShowSuccess("Playlist Downloaded!", $"All videos saved to:\n{_playlistFolder}");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Playlist Download Error:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("Playlist download failed.", "#FF4444");
        }
        finally
        {
            BtnDownload.IsEnabled   = true;
            BtnAnalyze.IsEnabled    = true;
            ProgBar.IsIndeterminate = false;
            ProgBar.Value           = 0;
            TxtPercent.Text         = "";
            TxtSpeed.Text           = "";
            TxtEta.Text             = "";
        }
    }

    // ── Quality filter builder ───────────────────────────────────────────────
    private string BuildPlaylistQualityFilter()
    {
        if (ComboPlaylistQuality.SelectedItem is not ComboBoxItem item)
            return "bestvideo";

        return item.Content?.ToString() switch
        {
            "1080p"                    => "bestvideo[height<=1080]",
            "720p"                     => "bestvideo[height<=720]",
            "480p"                     => "bestvideo[height<=480]",
            "360p"                     => "bestvideo[height<=360]",
            "Worst Quality (smallest)" => "worstvideo",
            _                          => "bestvideo"
        };
    }

    // ── Progress parser ──────────────────────────────────────────────────────
    private static readonly Regex _progressRegex = new(
        @"\[download\]\s+(?<pct>[\d.]+)%\s+of\s+.*?\s+at\s+(?<spd>\S+)\s+ETA\s+(?<eta>\S+)",
        RegexOptions.Compiled);

    private void ParseProgressLine(string line, bool isPlaylist,
        int currentIdx = 0, int total = 1)
    {
        var match = _progressRegex.Match(line);
        Dispatcher.Invoke(() =>
        {
            if (match.Success)
            {
                if (double.TryParse(match.Groups["pct"].Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double pct))
                {
                    if (isPlaylist)
                    {
                        ProgBarVideo.IsIndeterminate = false;
                        ProgBarVideo.Value           = pct;

                        double overallPct = total > 0
                            ? ((currentIdx - 1) + pct / 100.0) / total * 100.0
                            : 0;
                        ProgBarPlaylist.Value   = overallPct;
                        TxtPlaylistOverall.Text = $"{currentIdx - 1}/{total} done";
                    }
                    else
                    {
                        ProgBar.IsIndeterminate = false;
                        ProgBar.Value   = pct;
                        TxtPercent.Text = $"{pct:F1}%";
                    }
                }
                TxtSpeed.Text = match.Groups["spd"].Value;
                TxtEta.Text   = $"ETA {match.Groups["eta"].Value}";
                SetStatus("Downloading…", "#FF9900");
            }
            else if (line.Contains("[Merger]"))
            {
                SetStatus("Merging Video & Audio…", "#4488FF");
                if (isPlaylist) ProgBarVideo.IsIndeterminate = true;
                else            ProgBar.IsIndeterminate      = true;
            }
            else if (line.Contains("[download] 100%"))
            {
                if (isPlaylist)
                {
                    ProgBarVideo.IsIndeterminate = false;
                    ProgBarVideo.Value           = 100;
                }
            }
        });
    }

    // ── Status helper ────────────────────────────────────────────────────────
    private void SetStatus(string text, string hexColor)
    {
        TxtStatus.Text = text;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            StatusDot.Fill = new SolidColorBrush(color);
        }
        catch { /* ignore invalid color */ }
    }

    // ── Success overlay ──────────────────────────────────────────────────────
    private void ShowSuccess(string title, string subtitle)
    {
        Dispatcher.Invoke(() =>
        {
            TxtSuccessTitle.Text      = title;
            TxtSuccessSubtitle.Text   = subtitle;
            SuccessOverlay.Visibility = Visibility.Visible;
            SetStatus("Ready", "#2e2e2e");
        });
    }

    private void CloseSuccess_Click(object sender, RoutedEventArgs e)
        => SuccessOverlay.Visibility = Visibility.Collapsed;
}