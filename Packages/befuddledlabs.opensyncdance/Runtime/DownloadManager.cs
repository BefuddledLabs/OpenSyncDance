#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BefuddledLabs.OpenSyncDance
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
        private static string ytdlpParams =  $"--ffmpeg-location \"{FFmpegPath}\" " // Use this ffmpeg
                                            + "-f bestaudio --audio-quality 0 -x --audio-format wav " // Convert to audio file
                                            + "--force-keyframes-at-cuts -i {0} --download-sections \"*{1}-{2}\" " // Download specific section of song
                                            + "--force-overwrites --no-mtime -v -o \"{3}\" " // Overwrite file and use curret date time
                                            + "--retry-sleep 5 --retries 3"; // Retry because youtube can be youtube

        private static string ffmpegParams =  "-y -i \"{0}\" -ss {1} -t {2} \"{3}\"";

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

            ytdlpDownloader.DownloadFileCompleted += (_, _) => { AssetDatabase.Refresh(); EditorUtility.ClearProgressBar(); };
            ytdlpDownloader.DownloadFileAsync(new Uri(YtdlpURL), BinariesPath + "/yt-dlp.exe");
        }

        private static string TimeSpanToYtdlpString(TimeSpan timeSpan) => timeSpan.ToString(@"mm\:ss\.fff");
        
        public static bool TryParseTimeSpan(string timeString, out TimeSpan timeSpan)
        {
            timeString = timeString.Trim();
            string[] formats = { // Uhh, I'm stupid, this is awful
                @"mm\:ss\.fff",
                @"mm\:ss\.ff",
                @"mm\:ss\.f",
                @"mm\:ss",
                @"m\:ss\.fff",
                @"m\:ss\.ff",
                @"m\:ss\.f",
                @"m\:ss",
                @"mm\:s\.fff",
                @"mm\:s\.ff",
                @"mm\:s\.f",
                @"mm\:s",
                @"m\:s\.fff",
                @"m\:s\.ff",
                @"m\:s\.f",
                @"m\:s",
                @"ss\.fff",
                @"ss\.ff",
                @"ss\.f",
                @"s\.fff",
                @"s\.ff",
                @"s\.f",
            };
            foreach (var format in formats)
                if (TimeSpan.TryParseExact(timeString, format, null, out timeSpan))
                    return true;
            timeSpan = default;
            return false;
        }

        public static AudioClip DownloadYouTubeLink(SyncedAnimation anim) 
        {
            var animLength = TimeSpan.Zero;
            if (anim.animationClip != null)
                animLength = TimeSpan.FromSeconds(anim.animationClip.length);
            return DownloadYouTubeLink(anim.audio.audioUrl, anim.audio.startTimeStamp, anim.audio.endTimeStamp, anim.animationClip.name, animLength);
        }

        public static AudioClip DownloadYouTubeLink(string youtubeLink, string start, string end, string outputFileName, TimeSpan animLength) 
        {
            if (!TryParseTimeSpan(start, out var startTime))
            {
                EditorUtility.DisplayDialog("Start Time Error",
                    "Start time is formatted wrong, should be minutes:seconds.milliseconds", "ok");
                return null;
            }

            TimeSpan endTime;
            if (end.Equals("auto", StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrWhiteSpace(end)) 
            {
                endTime = startTime + animLength;
            }
            else if (!TryParseTimeSpan(end, out endTime))
            {
                EditorUtility.DisplayDialog("End Time Error",
                    "End time is formatted wrong, should be minutes:seconds.milliseconds or \"auto\"", "ok");
                return null;
            }
                        
            if (startTime > endTime)
            {
                EditorUtility.DisplayDialog("Time Error", "End time is formatted wrong, should be minutes:seconds.milliseconds or \"auto\"", "ok");
                return null;
            }

            DownloadYouTubeLink(youtubeLink, startTime, endTime, outputFileName);

            return AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/OpenSyncDance/Audio/{outputFileName}.wav");
        }
        public static void DownloadYouTubeLink(string youtubeLink, TimeSpan start, TimeSpan end, string outputFileName) 
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
            var tempSongPath = $"{Application.dataPath}/OpenSyncDance/Audio/{outputFileName}_temp";
            var localPath = $"Assets/OpenSyncDance/Audio/{outputFileName}.wav";
            var localTempPath = $"Assets/OpenSyncDance/Audio/{outputFileName}_temp.wav";
            
            if (File.Exists(songPath+".wav"))
                File.Delete(songPath+".wav");
            if (File.Exists(tempSongPath+".wav"))
                File.Delete(tempSongPath+".wav");

            // Start new process with yt-dlp to download music from YouTube
            var ytdlpStartInfo = new ProcessStartInfo {
                FileName = ytdlpPath,
                Arguments = string.Format(
                    ytdlpParams,
                    youtubeLink,
                    TimeSpanToYtdlpString(TimeSpan.FromTicks(0)),
                    TimeSpanToYtdlpString(end + TimeSpan.FromSeconds(1)),
                    tempSongPath),
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var downloadAttempts = 3; // Retry the download command a couple of times in case it fails..
            for (int i = 0; i < downloadAttempts; i++) {
                var ytdlpProcess = Process.Start(ytdlpStartInfo);
                if (ytdlpProcess == null) 
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Error", "Couldn't Start yt-dlp", "ok");
                    return;
                }
            
                ytdlpProcess.WaitForExit();
                AssetDatabase.Refresh();

                if (AssetImporter.GetAtPath(localTempPath) as AudioImporter)
                    break;
                
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            }
            
            if (!(AssetImporter.GetAtPath(localTempPath) as AudioImporter)) {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error",
                    $"Failed to set audio import settings for {localPath}", "ok");
                AssetDatabase.Refresh();
                return;
            }
            
            var ffmpegStartInfo = new ProcessStartInfo {
                FileName = FFmpegPath,
                Arguments = string.Format(
                    ffmpegParams,
                    tempSongPath+".wav",
                    TimeSpanToYtdlpString(start),
                    TimeSpanToYtdlpString(end - start),
                    songPath+".wav"),
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            
            var ffmpegProcess = Process.Start(ffmpegStartInfo);
            if (ffmpegProcess == null) 
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", "Couldn't Start ffmpeg to trim", "ok");
                return;
            }
            ffmpegProcess.WaitForExit();
            
            if (File.Exists(tempSongPath+".wav"))
                File.Delete(tempSongPath+".wav");
            
            AssetDatabase.Refresh();
            
            var importer = AssetImporter.GetAtPath(localPath) as AudioImporter;
            importer.loadInBackground = true;
            
            // Default settings for the downloaded audio file
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                // Streaming uses more CPU which is big no no in VRChat.
                loadType = AudioClipLoadType.CompressedInMemory,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.8f,
                sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
            };
            
            importer.SaveAndReimport();
            EditorUtility.ClearProgressBar();
        }
    }
}
#endif