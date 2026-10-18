using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace MiCay
{
    public static class Keychain
    {
        const string BackupKey = "micay_backup";
        const string SeqKey = "micay_seq";

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] static extern bool MiCayKeychainSet(string key, string value);
        [DllImport("__Internal")] static extern string MiCayKeychainGet(string key);
        public static bool Supported => true;
#else
        public static bool Supported => false;
#endif

        public static bool LastOk { get; private set; }

        static bool Set(string key, string value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try { return MiCayKeychainSet(key, value); }
            catch (Exception e) { Debug.LogWarning("Không ghi được Keychain: " + e.Message); }
#endif
            return false;
        }

        static string Get(string key)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try { return MiCayKeychainGet(key); }
            catch (Exception e) { Debug.LogWarning("Không đọc được Keychain: " + e.Message); }
#endif
            return null;
        }

        public static void SaveBackup(string text) => LastOk = Set(BackupKey, text);

        public static string LoadBackup() => Get(BackupKey);

        public static void SaveSeq(int seq)
        {
            if (seq > LoadSeq()) Set(SeqKey, Guard.Sign(seq.ToString()) + "|" + seq);
        }

        public static int LoadSeq()
        {
            string raw = Get(SeqKey);
            if (string.IsNullOrEmpty(raw)) return 0;
            int bar = raw.LastIndexOf('|');
            if (bar <= 0 || bar >= raw.Length - 1) return 0;
            string sig = raw.Substring(0, bar);
            string num = raw.Substring(bar + 1);
            if (!Guard.Verify(num, sig)) return 0;
            return int.TryParse(num, out int d) ? d : 0;
        }
    }
}
