# YouTube Downloader

A modern, high-performance YouTube video and playlist downloader built with C# and WPF. This application provides a user-friendly graphical interface for `yt-dlp`, allowing you to download videos in various resolutions and formats with ease.

## ✨ Features

- **Single Video Download**: Download individual videos by simply pasting the URL.
- **Playlist Support**: Analyze and download entire playlists, with options to select specific ranges.
- **Quality Selection**: Choose from various resolutions (1080p, 720p, 480p, etc.) and formats.
- **Concurrent Fragments**: High-speed downloads using multiple concurrent fragments.
- **Audio Options**: Options to download best or worst audio quality as needed.
- **Real-time Progress**: Visual progress bars, speed indicators, and ETA for all downloads.
- **Modern UI**: Sleek, glassmorphism-inspired design with dark mode support and smooth transitions.
- **FFmpeg Integration**: Automatic merging of video and audio streams into high-quality MP4 files.

## 🛠️ Built With

- **Framework**: [.NET 10](https://dotnet.microsoft.com/)
- **UI Architecture**: WPF (Windows Presentation Foundation)
- **External Tools**: 
  - [yt-dlp](https://github.com/yt-dlp/yt-dlp) - The powerful core downloader.
  - [FFmpeg](https://ffmpeg.org/) - For multimedia handling and merging.
- **Primary Libraries**: `WindowsAPICodePack` (for folder selection dialogs).

## 🚀 Getting Started

### Prerequisites

To run or build this project, you need:
1.  **.NET 10 SDK** or later.
2.  **yt-dlp.exe**: Should be placed in the application directory.
3.  **ffmpeg.exe**: Should be placed in the application directory (or available in PATH).

### Installation

1.  Clone the repository:
    ```bash
    git clone https://github.com/Redaessa7/YoutubeDownloader.git
    ```
2.  Navigate to the project folder:
    ```bash
    cd YoutubeDownloader
    ```
3.  Ensure `yt-dlp.exe` and `ffmpeg.exe` are in the project root or the same directory as the executable.
4.  Build the project:
    ```bash
    dotnet build
    ```

## 📖 Usage

1.  **Paste URL**: Enter the YouTube video or playlist URL in the input field.
2.  **Analyze**: Click the "Analyze" button to fetch metadata and available formats.
3.  **Configure**:
    - For **Videos**: Choose your preferred resolution from the dropdown.
    - For **Playlists**: Select the save folder and optionally define the video range (e.g., from 1 to 10).
4.  **Download**: Click "Download" and watch the progress in real-time.
5.  **Success**: Once finished, a success message will appear, and your file will be ready!

## 🤝 Contributing

Contributions are welcome! If you have any ideas, suggestions, or bug reports, please open an issue or submit a pull request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details (if applicable).

---
*Created with ❤️ by [Redaessa7](https://github.com/Redaessa7)*
