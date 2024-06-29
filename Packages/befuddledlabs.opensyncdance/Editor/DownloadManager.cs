using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BefuddledLabs.OpenSyncDance.Editor 
{
    public static class DownloadManager {
        
        /// <summary>
        /// Path to where we should have or are going to download FFmpeg
        /// </summary>
        public static string FFmpegPath => BinariesPath + "/ffmpegBinaries/ffmpeg.exe";
        
        /// <summary>
        /// Path to where we expect to find yt-dlp
        /// </summary>
        public static string ytdlpPath => BinariesPath + "/yt-dlp.exe";
        
        /// <summary>
        /// If we can find FFmpeg, otherwise we should download it
        /// </summary>
        public static bool HasFFmpeg => File.Exists(FFmpegPath);
        
        /// <summary>
        /// If we can find yt-dlp
        /// </summary>
        public static bool Hasytdlp => File.Exists(ytdlpPath);

        /// <summary>
        /// Path to the root of the unity project
        /// </summary>
        private static string BasePath => Application.dataPath.Replace("/Assets", "");
        
        /// <summary>
        /// Full system path to the binaries folder
        /// </summary>
        private static string BinariesPath => BasePath + "/Packages/befuddledlabs.opensyncdance/Binaries";
        
        private const string FFmpegURL = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl-shared.zip";
        private const string YtdlpURL = "https://github.com/yt-dlp/yt-dlp/releases/download/2024.05.27/yt-dlp_min.exe";

        /// <summary>
        /// 0: url
        /// 1: start timestamp
        /// 2: end timestamp
        /// 3: output file
        /// </summary>
        private static string ytdlpParams = $"--ffmpeg-location \"{FFmpegPath}\" " + "-f bestaudio --audio-quality 0 -x --audio-format vorbis --force-keyframes-at-cuts -i {0} --download-sections \"*{1}-{2}\" --force-overwrites --no-mtime -v -o \"{3}\"";

        private static void CreateBinariesFolder() 
        {
            if (!Directory.Exists(BinariesPath))
                Directory.CreateDirectory(BinariesPath);
        }

        private static void CreateAudioFolder() {
            List<string> assetFolderPath = new() { "Assets", "OpenSyncDance", "Audio" };
            for (int i = 1; i < assetFolderPath.Count; i++)
            {
                var prefixPath = string.Join('/', assetFolderPath.Take(i));
                if (!AssetDatabase.IsValidFolder($"{prefixPath}/{assetFolderPath[i]}"))
                    AssetDatabase.CreateFolder(prefixPath, assetFolderPath[i]);
            }
        }

        private static void FFmpegDownloadFinished(object sender, AsyncCompletedEventArgs e) 
        {
            EditorUtility.DisplayProgressBar("Extracting", "ffmpeg is being extracted.", 0);
            
            if (Directory.Exists(BinariesPath + "/ffmpegExtracted"))
                Directory.Delete(BinariesPath + "/ffmpegExtracted", true);
            if (Directory.Exists(BinariesPath + "/ffmpegBinaries"))
                Directory.Delete(BinariesPath + "/ffmpegBinaries", true);
            
            ZipFile.ExtractToDirectory(BinariesPath + "/ffmpeg.zip", BinariesPath + "/ffmpegExtracted");
            //Directory.GetFiles(BinariesPath + "/ffmpegBinaries", "")
            var ffmpegBin = Directory.GetDirectories(BinariesPath + "/ffmpegExtracted", "bin", SearchOption.AllDirectories);
            if (ffmpegBin.Length < 1) {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to find ffmpeg binary in {BinariesPath + "/ffmpegExtracted"}", "ok");
                return;
            }
            
            Directory.Move(ffmpegBin[0], BinariesPath + "/ffmpegBinaries");
            File.Delete(BinariesPath + "/ffmpeg.zip");
            Directory.Delete(BinariesPath + "/ffmpegExtracted", true);
            
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        [MenuItem("OpenSyncDance/Download Binaries")]
        public static void DownloadBoth() {
            StartDownloadFFmpeg((a, b) => {
                StartDownloadYtdlp();
            });
        }

        public static void StartDownloadFFmpeg(AsyncCompletedEventHandler callback) 
        {
            CreateBinariesFolder();
            EditorUtility.DisplayProgressBar("Downloading FFmpeg", "", 0);
            
            var ffmpegDownloader = new WebClient();
            ffmpegDownloader.DownloadProgressChanged += (sender, args) => {
                EditorUtility.DisplayProgressBar("Downloading FFmpeg", $"{(float)args.BytesReceived / args.TotalBytesToReceive * 100}% of {args.TotalBytesToReceive / 1024 / 1024}MiB", args.ProgressPercentage / 100f);
            };
            ffmpegDownloader.DownloadFileCompleted += FFmpegDownloadFinished;
            ffmpegDownloader.DownloadFileCompleted += callback;
            
            ffmpegDownloader.DownloadFileAsync(new Uri(FFmpegURL), BinariesPath + "/ffmpeg.zip");
        }
        
        public static void StartDownloadYtdlp() 
        {
            CreateBinariesFolder();
            EditorUtility.DisplayProgressBar("Downloading yt-dlp", "", 0);
            
            var ytdlpDownloader = new WebClient();
            ytdlpDownloader.DownloadProgressChanged += (sender, args) => {
                EditorUtility.DisplayProgressBar("Downloading yt-dlp", $"{(float)args.BytesReceived / args.TotalBytesToReceive * 100}% of {args.TotalBytesToReceive / 1024 / 1024}MiB", args.ProgressPercentage / 100f);
            };

            ytdlpDownloader.DownloadFileCompleted += (_, _) => { AssetDatabase.Refresh(); };
            
            ytdlpDownloader.DownloadFileAsync(new Uri(YtdlpURL), BinariesPath + "/yt-dlp.exe");
            
            EditorUtility.ClearProgressBar();
        }

        private static string TimeSpanToYtdlpString(TimeSpan timeSpan) => timeSpan.ToString(@"mm\:ss\.fff");

        public static void DownloadYoutubeLink(string youtubeLink, TimeSpan start, TimeSpan end, string outputFileName) 
        {
            CreateAudioFolder();
            
            if (!Hasytdlp) 
            {
                EditorUtility.DisplayDialog("Error",
                    "No yt-dlp installed, do you have VRChat installed?", "ok");
                return;
            }

            if (!HasFFmpeg) 
            {
                EditorUtility.DisplayDialog("Error",
                    "No FFmpeg installed, please install ffmpeg", "ok");
                return;
            }
            
            EditorUtility.DisplayProgressBar("Downloading Youtube link", "", 0);

            var songPath = $"{Application.dataPath}/OpenSyncDance/Audio/{outputFileName}";

            // Start new process with yt-dlp to download music from YouTube
            var startInfo = new ProcessStartInfo {
                FileName = ytdlpPath,
                Arguments = string.Format(ytdlpParams, youtubeLink, TimeSpanToYtdlpString(start), TimeSpanToYtdlpString(end),
                    songPath),
                UseShellExecute = true,
            };
            
            
            var ytdlpProcess = Process.Start(startInfo);
            if (ytdlpProcess == null) 
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", "Couldn't Start yt-dlp", "ok");
                return;
            }
            
            ytdlpProcess.WaitForExit();
            AssetDatabase.Refresh();
            
            var localPath = $"Assets/OpenSyncDance/Audio/{outputFileName}.ogg";
            var importer = AssetImporter.GetAtPath(localPath) as AudioImporter;
            if (importer == null) {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error",
                    $"Failed to set audio import settings for {localPath}", "ok");
                AssetDatabase.Refresh();
                return;
            }

            importer.forceToMono = true;
            importer.loadInBackground = true;
            
            // Default settings for the downloaded audio file
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.Streaming,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.8f,
                sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
            };
            
            importer.SaveAndReimport();
            EditorUtility.ClearProgressBar();
        }
    }
}
