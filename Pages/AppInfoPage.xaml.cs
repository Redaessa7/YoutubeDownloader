using System.Windows.Controls;

namespace YoutubeDownloader.Pages
{
    public partial class AppInfoPage : Page
    {
        // بيانات التطبيق
        private const string AppVersion      = "2.0.1";
        private const string AppStatus       = "STABLE RELEASE";
        private const string DevName         = "Reda Essa"; // ضع اسمك هنا
        private const string DevEmail        = "redaessa.dev@gmail.com";

        public AppInfoPage()
        {
            InitializeComponent();
            LoadAppDetails();
        }

        private void LoadAppDetails()
        {
            TxtVersion.Text        = AppVersion;
            TxtVersionLabel.Text   = AppStatus;
            TxtDeveloperName.Text  = DevName;
            TxtDeveloperEmail.Text = DevEmail;
            TxtCopyright.Text      = DevName;
        }
    }
}