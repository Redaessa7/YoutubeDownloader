using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YoutubeDownloader;

public partial class MainWindow : Window
{
    private List<VideoFormat> _formatsList = new();

    // HttpClient واحد مشترك — أفضل ممارسة في .NET
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // ── تعديل وحيد لتسريع التحميل ──────────────────────────────────────────
    // concurrent-fragments 8  →  يقسّم الملف لـ 8 أجزاء متوازية (fragments)
    // N2 = عدد الأجزاء، يمكن رفعه لـ 16 على إنترنت سريع
    private const int DownloadFragments = 8;

    public MainWindow()
    {
        InitializeComponent();
    }

    // ─── Model ───────────────────────────────────────────────────────────────
    public class VideoFormat
    {
        public string Id       { get; set; } = "";
        public string Display  { get; set; } = "";
        public string Ext      { get; set; } = "";
        public long   Filesize { get; set; }
    }

    // ─── Placeholder visibility ──────────────────────────────────────────────
    private void TxtUrl_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        TxtPlaceholder.Visibility = string.IsNullOrEmpty(TxtUrl.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ─── Analyze ─────────────────────────────────────────────────────────────
    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        string url = TxtUrl.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        BtnAnalyze.IsEnabled  = false;
        BtnDownload.IsEnabled = false;
        VideoCard.Visibility  = Visibility.Collapsed;
        SetStatus("Analyzing video…", "#FF9900");

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
            if (formats == null)
            {
                SetStatus("Could not parse video formats.", "#FF4444");
                return;
            }

            // ── Metadata ─────────────────────────────────────────────────────
            string title    = node?["title"]?.ToString()    ?? "Unknown Title";
            string channel  = node?["uploader"]?.ToString() ?? "Unknown Channel";
            string thumbUrl = node?["thumbnail"]?.ToString() ?? "";

            // ── Build formats (video streams only) ────────────────────────────
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

                    string sizeLabel = size > 0
                        ? $"{size / 1024.0 / 1024.0:F1} MB"
                        : "size N/A";

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

            // ── Update UI ─────────────────────────────────────────────────────
            TxtTitle.Text   = title;
            TxtChannel.Text = channel;

            // ── تحميل الصورة المصغّرة بشكل صحيح عبر HttpClient + MemoryStream
            // السبب: BitmapImage من URI خارجي لا يمكن Freeze() لأنه async بطبيعته
            // الحل: نحمّل البايتات أولاً في background thread، ثم نبني BitmapImage
            //        من MemoryStream على الـ UI thread — هذا يسمح بـ Freeze() آمن
            if (!string.IsNullOrEmpty(thumbUrl))
            {
                try
                {
                    byte[] imgBytes = await _http.GetByteArrayAsync(thumbUrl);
                    using var ms = new MemoryStream(imgBytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption  = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;          // من stream وليس URI → يسمح بـ Freeze
                    bmp.EndInit();
                    bmp.Freeze();                   // آمن الآن ← يحسّن الأداء ويمنع memory leaks
                    ImgThumbnail.Source = bmp;
                }
                catch
                {
                    // لو فشل تحميل الصورة، نتجاهل بصمت — لا نوقف العملية كلها
                    ImgThumbnail.Source = null;
                }
            }

            ComboFormats.ItemsSource   = _formatsList;
            ComboFormats.SelectedIndex = 0;

            VideoCard.Visibility  = Visibility.Visible;
            BtnDownload.IsEnabled = true;
            SetStatus("Ready to download", "#00D26A");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Analysis Error:\n{ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("Analysis failed.", "#FF4444");
        }
        finally
        {
            BtnAnalyze.IsEnabled = true;
        }
    }

    // ─── Format picker info ──────────────────────────────────────────────────
    private void ComboFormats_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ComboFormats.SelectedItem is VideoFormat fmt)
            TxtFormatInfo.Text = $"Format ID: {fmt.Id}  ·  Container: {fmt.Ext}";
        else
            TxtFormatInfo.Text = "Select your preferred resolution above";
    }

    // ─── Download ────────────────────────────────────────────────────────────
    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        if (ComboFormats.SelectedItem is not VideoFormat selected)
        {
            MessageBox.Show("Please select a quality first.", "No Format Selected",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Sanitize filename
        string cleanTitle = TxtTitle.Text;
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            cleanTitle = cleanTitle.Replace(c, '_');

        var sfd = new SaveFileDialog
        {
            Filter   = "MP4 Video|*.mp4",
            FileName = $"{cleanTitle}.mp4"
        };
        if (sfd.ShowDialog() != true) return;

        BtnDownload.IsEnabled = false;
        BtnAnalyze.IsEnabled  = false;
        ProgBar.Value         = 0;
        TxtPercent.Text       = "";

        // RadioAudioHigh / RadioAudioLow ← يطابقان XAML بالضبط
        string audioMode = RadioAudioHigh.IsChecked == true ? "bestaudio" : "worstaudio";

        // ── --concurrent-fragments N  →  core of the speed improvement ────────
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
                    if (!string.IsNullOrEmpty(ev.Data)) ParseProgressLine(ev.Data);
                };
                process.ErrorDataReceived += (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(ev.Data)) ParseProgressLine(ev.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            });

            SuccessOverlay.Visibility = Visibility.Visible;
            SetStatus("Ready", "#2e2e2e");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download Error:\n{ex.Message}", "Error",
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

    // ─── Progress Parser ─────────────────────────────────────────────────────
    private static readonly Regex _progressRegex = new(
        @"\[download\]\s+(?<pct>[\d.]+)%\s+of\s+.*?\s+at\s+(?<spd>\S+)\s+ETA\s+(?<eta>\S+)",
        RegexOptions.Compiled);

    private void ParseProgressLine(string line)
    {
        var match = _progressRegex.Match(line);
        Dispatcher.Invoke(() =>
        {
            if (match.Success)
            {
                ProgBar.IsIndeterminate = false;
                if (double.TryParse(match.Groups["pct"].Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double pct))
                {
                    ProgBar.Value   = pct;
                    TxtPercent.Text = $"{pct:F1}%";
                }
                TxtSpeed.Text = match.Groups["spd"].Value;
                TxtEta.Text   = $"ETA {match.Groups["eta"].Value}";
                SetStatus("Downloading…", "#FF9900");
            }
            else if (line.Contains("[Merger]"))
            {
                SetStatus("Merging Video & Audio…", "#4488FF");
                ProgBar.IsIndeterminate = true;
            }
        });
    }

    // ─── Status helper (text + dot colour) ───────────────────────────────────
    private void SetStatus(string text, string hexColor)
    {
        TxtStatus.Text = text;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            StatusDot.Fill = new SolidColorBrush(color);
        }
        catch { /* ignore bad hex */ }
    }

    // ─── Success overlay ─────────────────────────────────────────────────────
    private void CloseSuccess_Click(object sender, RoutedEventArgs e)
        => SuccessOverlay.Visibility = Visibility.Collapsed;
}