using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MiCay
{
    public static class Guard
    {
        static readonly byte[] P1 = { 78, 159, 106, 184, 242, 56, 239, 25, 126, 140, 47, 163, 2, 215, 212, 62, 235, 244, 19, 252, 249, 187, 133, 122, 46, 114, 231, 181, 69, 116, 184, 1 };
        static readonly byte[] P2 = { 16, 225, 146, 177, 226, 76, 236, 115, 165, 5, 248, 113, 79, 32, 24, 27, 77, 253, 254, 118, 129, 58, 155, 237, 90, 216, 9, 115, 27, 77, 70, 140 };

        static byte[] Key()
        {
            var k = new byte[P1.Length];
            for (int i = 0; i < k.Length; i++) k[i] = (byte)(P1[i] ^ P2[i]);
            return k;
        }

        public static string Sign(string message)
        {
            using (var h = new HMACSHA256(Key()))
                return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(message ?? "")));
        }

        public static bool Verify(string message, string signature)
        {
            string expect = Sign(message);
            if (signature == null || signature.Length != expect.Length) return false;
            int diff = 0;
            for (int i = 0; i < expect.Length; i++) diff |= expect[i] ^ signature[i];
            return diff == 0;
        }

        public static string Wrap(string json) => Sign(json) + "\n" + json;

        public static string Unwrap(string signed)
        {
            if (string.IsNullOrEmpty(signed)) return null;
            int nl = signed.IndexOf('\n');
            if (nl <= 0) return null;
            string sig = signed.Substring(0, nl);
            string json = signed.Substring(nl + 1);
            return Verify(json, sig) ? json : null;
        }

        static int N(List<int> l) => l == null ? -1 : l.Count;
        static int N(List<long> l) => l == null ? -1 : l.Count;

        public static string Validate(GameState s)
        {
            if (s == null) return "dữ liệu trống";
            if (s.version != GameState.CurrentVersion) return "phiên bản dữ liệu không khớp";
            if (s.day < 1 || s.day > 100000) return "số ngày không hợp lệ";
            if (s.chapter < 1 || s.chapter > StoryDB.Chapters.Count) return "số chương không hợp lệ";
            if (s.chapterStartDay < 1 || s.chapterStartDay > s.day) return "mốc chương không hợp lệ";
            if (s.maxDayReached < 0 || s.maxDayReached > s.day + 1) return "mốc tiến trình không hợp lệ";
            if (s.reputation < 0.99f || s.reputation > 5.01f) return "uy tín không hợp lệ";
            if (s.violationPoints < 0 || s.violationPoints > 1000) return "điểm vi phạm không hợp lệ";
            if (s.prisonCount < 0 || s.negativeCashDays < 0 || s.evasionCount < 0 || s.saveSeq < 0) return "chỉ số âm bất thường";
            if (s.stage < 0 || s.stage > 2) return "trạng thái ngày không hợp lệ";

            if (s.ledger == null) return "sổ sách hỏng";
            long ledgerSum = 0;
            foreach (var e in s.ledger)
            {
                if (e == null) return "sổ sách hỏng";
                if (e.day < 1 || e.day > s.day) return "sổ sách có ngày không hợp lệ";
                ledgerSum += e.amount;
            }
            if (ledgerSum != s.cash) return "số dư tiền mặt không khớp sổ sách";

            if (s.stoves < 1 || s.stoves > GameManager.MaxStoves) return "số bếp không hợp lệ";
            if (N(s.stoveTiers) != s.stoves || N(s.stoveUpgraded) != s.stoves || N(s.stoveDays) != s.stoves) return "dữ liệu bếp hỏng";
            foreach (var t in s.stoveTiers) if (t < 0 || t >= GameManager.StoveTiers.Length) return "bậc bếp không hợp lệ";
            foreach (var u in s.stoveUpgraded) if (u < 0 || u > GameManager.MaxOldUpgrades) return "nâng cấp bếp không hợp lệ";

            if (N(s.barTiers) < 1 || s.barTiers.Count > GameManager.MaxBars) return "số máy pha không hợp lệ";
            if (N(s.barUpgraded) != s.barTiers.Count || N(s.barDays) != s.barTiers.Count) return "dữ liệu máy pha hỏng";
            foreach (var t in s.barTiers) if (t < 0 || t >= GameManager.BarTiers.Length) return "bậc máy pha không hợp lệ";
            foreach (var u in s.barUpgraded) if (u < 0 || u > GameManager.MaxOldUpgrades) return "nâng cấp máy pha không hợp lệ";

            if (s.tableSlots == null || s.tableSlots.Count > CustomerSystem.MaxTables) return "số bàn không hợp lệ";
            var seen = new HashSet<int>();
            foreach (var t in s.tableSlots)
            {
                if (t < 0 || t >= CustomerSystem.MaxTables) return "vị trí bàn không hợp lệ";
                if (!seen.Add(t)) return "bàn bị trùng";
            }
            if (s.tables != s.tableSlots.Count) return "số bàn không khớp";

            if (s.helpers == null || s.helpers.Count > GameManager.MaxHelpers) return "số nhân viên không hợp lệ";
            if (N(s.helperAreas) != s.helpers.Count || N(s.helperDaysWorked) != s.helpers.Count) return "dữ liệu nhân viên hỏng";
            foreach (var a in s.helperAreas) if (a < 0 || a >= CustomerSystem.Areas) return "khu nhân viên không hợp lệ";
            foreach (var d in s.helperDaysWorked) if (d < 0) return "dữ liệu nhân viên hỏng";
            if (s.staffWage < 0) return "lương âm bất thường";
            if (s.wages == null || s.departed == null) return "dữ liệu lương hỏng";
            foreach (var w in s.wages)
            {
                if (w == null || string.IsNullOrEmpty(w.who) || w.amount < 0) return "dữ liệu lương hỏng";
                if (w.day < 1 || w.day > s.day) return "lương có ngày không hợp lệ";
            }
            foreach (var dep in s.departed)
                if (dep == null || string.IsNullOrEmpty(dep.who) || dep.dueDay < 1) return "dữ liệu lương hỏng";
            if (s.dailyPnl == null) return "sổ sách hỏng";
            foreach (var p in s.dailyPnl)
                if (p == null || p.day < 1 || p.day > s.day) return "sổ sách hỏng";

            foreach (var b in s.inventory)
            {
                if (b == null || GameDB.GetIng(b.ing) == null) return "kho có nguyên liệu lạ";
                if (b.qty < 0 || b.unitCost < 0) return "kho có số âm";
            }
            foreach (var d in s.deliveries)
                if (d == null || GameDB.GetIng(d.ing) == null || d.qty < 0 || d.unitCost < 0) return "đơn hàng lạ";
            foreach (var r in s.disabledRecipes) if (GameDB.GetRecipe(r) == null) return "công thức lạ";
            foreach (var m in s.mastery) if (m == null || GameDB.GetRecipe(m.recipe) == null || m.count < 0) return "dữ liệu tay nghề hỏng";
            foreach (var d in s.debts) if (d == null || d.remaining < 0) return "dữ liệu nợ hỏng";
            foreach (var t in s.taxBills) if (t == null || t.vat < 0 || t.pit < 0) return "dữ liệu thuế hỏng";

            return null;
        }
    }
}
