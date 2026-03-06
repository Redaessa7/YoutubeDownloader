using System.Windows.Controls;

namespace OcTubeDownloader.Pages
{
    public partial class AppInfoPage : Page
    {
        // بيانات التطبيق
        private const string AppVersion      = "2.0.1";
        private const string AppStatus       = "STABLE RELEASE";
        private const string DevName         = "Redaessa7";

        public AppInfoPage()
        {
            InitializeComponent();
            LoadAppDetails();
        }

        private void LoadAppDetails()
        {
            TxtVersion.Text        = AppVersion;
            TxtVersionLabel.Text   = AppStatus;
            TxtCopyright.Text      = DevName;
        }
    }
}