using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace OcTubeDownloader.Pages
{
    public partial class AppInfoPage : Page
    {
        // بيانات التطبيق
        private const string AppVersion      = "2.1.1";
        private const string AppStatus       = "STABLE RELEASE";
        private const string DevName         = "Redaessa7";

        private static string YtDlpPath =>
            Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");

        public AppInfoPage()
        {
            InitializeComponent();
            LoadAppDetails();
            _ = LoadEngineVersionAsync();
        }

        private void LoadAppDetails()
        {
            TxtVersion.Text        = AppVersion;
            TxtVersionLabel.Text   = AppStatus;
            TxtCopyright.Text      = DevName;
        }

        private async Task LoadEngineVersionAsync()
        {
            try
            {
                string version = await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = YtDlpPath,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) return "not found";
                    string out_ = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    return proc.ExitCode == 0 ? out_.Trim() : "not found";
                });
                TxtYtDlpVersion.Text = version;
            }
            catch
            {
                TxtYtDlpVersion.Text = "not found";
            }
        }

        private async void BtnUpdateYtDlp_Click(object sender, RoutedEventArgs e)
        {
            BtnUpdateYtDlp.IsEnabled = false;
            TxtYtDlpStatus.Text = "Updating yt-dlp, please wait…";

            try
            {
                string output = await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = YtDlpPath,
                        Arguments = "-U",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) return "Could not start yt-dlp.exe.";
                    string out_ = proc.StandardOutput.ReadToEnd();
                    string err = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    return (out_ + "\n" + err).Trim();
                });

                TxtYtDlpStatus.Text = output.Length > 300
                    ? output[^300..]
                    : output;
                await LoadEngineVersionAsync();
            }
            catch (Exception ex)
            {
                TxtYtDlpStatus.Text = "Update failed: " + ex.Message;
            }
            finally
            {
                BtnUpdateYtDlp.IsEnabled = true;
            }
        }
    }
}