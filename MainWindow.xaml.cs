using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging; // لإدارة الصور

namespace YoutubeDownloader;

public partial class MainWindow : Window
{
    private List<VideoFormat> _formatsList = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    public class VideoFormat
    {
        public string Id { get; set; } = "";
        public string Display { get; set; } = "";
    }

    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        string url = TxtUrl.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        BtnAnalyze.IsEnabled = false;
        BtnDownload.IsEnabled = false;
        VideoCard.Visibility = Visibility.Collapsed; // إخفاء الكارت القديم أثناء البحث
        TxtStatus.Text = "Analyzing video...";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                Arguments = $"--dump-json \"{url}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            string json = await Task.Run(() => {
                using var proc = Process.Start(startInfo);
                return proc?.StandardOutput.ReadToEnd() ?? "";
            });

            var node = JsonNode.Parse(json);
            var formats = node?["formats"]?.AsArray();
            if (formats == null) return;

            // استخراج بيانات الفيديو الجديدة
            string title = node?["title"]?.ToString() ?? "Unknown";
            string channel = node?["uploader"]?.ToString() ?? "Unknown Channel";
            string thumbUrl = node?["thumbnail"]?.ToString() ?? "";

            _formatsList = formats
                .Where(f => f["vcodec"]?.ToString() != "none")
                .Select(f => new VideoFormat {
                    Id = f["format_id"]!.ToString(),
                    Display = $"{f["format_note"] ?? f["resolution"]} ({f["ext"]}) - {(((long?)(f["filesize"] ?? f["filesize_approx"]) ?? 0) / 1024.0 / 1024.0):F2} MB"
                })
                .OrderByDescending(x => x.Display)
                .ToList();

            // تحديث الواجهة
            TxtTitle.Text = title;
            TxtChannel.Text = channel;
            if (!string.IsNullOrEmpty(thumbUrl))
                ImgThumbnail.Source = new BitmapImage(new Uri(thumbUrl));

            ComboFormats.ItemsSource = _formatsList;
            ComboFormats.SelectedIndex = 0; // اختيار أول جودة تلقائياً

            VideoCard.Visibility = Visibility.Visible; // إظهار الكارت
            TxtStatus.Text = "Ready to download";
            BtnDownload.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}");
            TxtStatus.Text = "Analysis failed.";
        }
        finally
        {
            BtnAnalyze.IsEnabled = true;
        }
    }

    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        var selected = (VideoFormat)ComboFormats.SelectedItem;
        if (selected == null) { MessageBox.Show("Select quality!"); return; }


        string cleanTitle = TxtTitle.Text;
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
        {
            cleanTitle = cleanTitle.Replace(c, '_'); // استبدال الرموز غير المسموحة بشرطة سفلية
        }

        SaveFileDialog sfd = new SaveFileDialog { Filter = "MP4 Video|*.mp4", FileName = $"{cleanTitle}.mp4" };
        if (sfd.ShowDialog() != true) return;

        BtnDownload.IsEnabled = false;
        BtnAnalyze.IsEnabled = false;
        ProgBar.Value = 0;

        string audioMode = RadioHigh.IsChecked == true ? "bestaudio" : "worstaudio";
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "yt-dlp.exe",
            Arguments = $"-f \"{selected.Id}+{audioMode}\" --newline --merge-output-format mp4 --ffmpeg-location . -o \"{sfd.FileName}\" \"{TxtUrl.Text}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try 
        {
            await Task.Run(() => {
                using var process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += (s, ev) => {
                    if (!string.IsNullOrEmpty(ev.Data)) UpdateUI(ev.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
            });

            SuccessOverlay.Visibility = Visibility.Visible;
            TxtStatus.Text = "Ready";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download Error: {ex.Message}");
        }
        finally
        {
            BtnDownload.IsEnabled = true;
            BtnAnalyze.IsEnabled = true;
            ProgBar.IsIndeterminate = false;
            ProgBar.Value = 0;
            TxtPercent.Text = "0%";
        }
    }

    private void UpdateUI(string rawData)
    {
        var match = Regex.Match(rawData, @"\[download\]\s+(?<pct>[\d.]+)% of\s+.*?\s+at\s+(?<spd>.*?)\s+ETA\s+(?<eta>.*)");
        Dispatcher.Invoke(() => {
            if (match.Success)
            {
                ProgBar.IsIndeterminate = false;
                double pct = double.Parse(match.Groups["pct"].Value);
                ProgBar.Value = pct;
                TxtPercent.Text = $"{pct}%";
                TxtSpeed.Text = $"Speed: {match.Groups["spd"].Value}";
                TxtEta.Text = $"ETA: {match.Groups["eta"].Value}";
                TxtStatus.Text = "Downloading...";
            }
            else if (rawData.Contains("[Merger]"))
            {
                TxtStatus.Text = "Merging Video & Audio...";
                ProgBar.IsIndeterminate = true;
            }
        });
    }

    private void CloseSuccess_Click(object sender, RoutedEventArgs e) => SuccessOverlay.Visibility = Visibility.Collapsed;
}