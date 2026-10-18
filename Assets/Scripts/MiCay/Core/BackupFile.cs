using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MiCay
{
    public static class BackupFile
    {
        public const string FileName = "TiemMyCay_saoluu.txt";
        const int ExtraNames = 5;

        public const string KeychainLabel = "Keychain của iPhone";

        public static string LastWritten { get; private set; }
        public static bool PublicOk { get; private set; }

        static string publicDir;
        static bool publicDirResolved;

        public static string LocalPath => Path.Combine(Application.persistentDataPath, FileName);

        public static string PublicDir
        {
            get
            {
                if (publicDirResolved) return publicDir;
                publicDirResolved = true;
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var env = new AndroidJavaClass("android.os.Environment"))
                    using (var dir = env.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", env.GetStatic<string>("DIRECTORY_DOWNLOADS")))
                        publicDir = dir.Call<string>("getAbsolutePath");
                }
                catch (Exception e) { Debug.LogWarning("Không tìm được thư mục Download: " + e.Message); }
#endif
                return publicDir;
            }
        }

        public static string WhereText
        {
            get
            {
#if UNITY_ANDROID
                return $"Bộ nhớ trong > Download > {FileName}";
#elif UNITY_IOS
                return "Keychain của iPhone (tự động, xóa game vẫn còn)";
#else
                return LocalPath;
#endif
            }
        }

        static IEnumerable<string> PublicCandidates()
        {
            if (string.IsNullOrEmpty(PublicDir)) yield break;
            yield return Path.Combine(PublicDir, FileName);
            for (int i = 2; i <= ExtraNames + 1; i++)
                yield return Path.Combine(PublicDir, Path.GetFileNameWithoutExtension(FileName) + $"_{i}.txt");
        }

        public static string Compose(string code, string summary) =>
            "TIỆM MỲ CAY CỦA CÚN IÊU — FILE SAO LƯU DỮ LIỆU\n" +
            $"Dữ liệu: {summary}\n" +
            $"Cập nhật: {DateTime.Now:dd/MM/yyyy HH:mm}\n\n" +
            "Cài lại game mà game không tự nạp được: mở file này, chọn tất cả, Sao chép, " +
            "rồi vào game > SAO LƯU / KHÔI PHỤC DỮ LIỆU > DÁN TỪ BỘ NHỚ TẠM > KHÔI PHỤC.\n\n" +
            code + "\n";

        public static void Write(string code, string summary)
        {
            string text = Compose(code, summary);
            try
            {
                File.WriteAllText(LocalPath, text);
                LastWritten = LocalPath;
            }
            catch (Exception e) { Debug.LogWarning("Không ghi được file sao lưu: " + e.Message); }

            PublicOk = false;
            if (Keychain.Supported)
            {
                Keychain.SaveBackup(text);
                PublicOk = Keychain.LastOk;
            }
            foreach (var path in PublicCandidates())
            {
                try
                {
                    File.WriteAllText(path, text);
                    LastWritten = path;
                    PublicOk = true;
                    Scan(path);
                    break;
                }
                catch { }
            }
        }

        static void Scan(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var scanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
                    scanner.CallStatic("scanFile", activity, new[] { path }, null, null);
            }
            catch { }
#endif
        }

        public static string FindBest(out string foundAt)
        {
            foundAt = null;
            string best = null;
            int bestDay = -1;
            var paths = new List<string>(PublicCandidates()) { LocalPath };
            if (Keychain.Supported) paths.Insert(0, KeychainLabel);
            foreach (var path in paths)
            {
                string text;
                try
                {
                    if (path == KeychainLabel) text = Keychain.LoadBackup();
                    else if (!File.Exists(path)) continue;
                    else text = File.ReadAllText(path);
                }
                catch { continue; }
                if (string.IsNullOrEmpty(text)) continue;
                int day = GameManager.BackupDay(text);
                if (day > bestDay)
                {
                    bestDay = day;
                    best = text;
                    foundAt = path;
                }
            }
            return best;
        }
    }
}
