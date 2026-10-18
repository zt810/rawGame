using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiCay
{
    public static class Inventory
    {
        public static int Count(GameState s, string ing)
        {
            int n = 0;
            foreach (var b in s.inventory) if (b.ing == ing) n += b.qty;
            return n;
        }

        public static void Add(GameState s, string ing, int qty, long unitCost, int arrivalDay)
        {
            var def = GameDB.GetIng(ing);
            int expiry = arrivalDay + Math.Max(1, def.shelfDays) - 1;
            foreach (var b in s.inventory)
                if (b.ing == ing && b.expiryDay == expiry && b.unitCost == unitCost) { b.qty += qty; return; }
            s.inventory.Add(new Batch { ing = ing, qty = qty, unitCost = unitCost, expiryDay = expiry });
        }

        public static float Freshness(Batch b, int day)
        {
            int shelf = Math.Max(1, GameDB.GetIng(b.ing).shelfDays);
            return Math.Max(0f, Math.Min(1f, (b.expiryDay - day + 1) / (float)shelf));
        }

        public static bool NearExpiry(Batch b, int day) => b.expiryDay - day <= 1 && Freshness(b, day) <= 0.5f;

        public static long Consume(GameState s, string ing, int qty) => Consume(s, ing, qty, out _, out _);

        public static long Consume(GameState s, string ing, int qty, out float freshness) => Consume(s, ing, qty, out freshness, out _);

        public static long Consume(GameState s, string ing, int qty, out float freshness, out bool stale)
        {
            long cost = 0;
            float fresh = 0;
            int taken = 0;
            stale = false;
            var batches = s.inventory.Where(b => b.ing == ing).OrderBy(b => b.expiryDay).ToList();
            foreach (var b in batches)
            {
                if (qty <= 0) break;
                int take = Math.Min(qty, b.qty);
                fresh += Freshness(b, s.day) * take;
                if (NearExpiry(b, s.day)) stale = true;
                taken += take;
                b.qty -= take;
                qty -= take;
                cost += take * b.unitCost;
                if (b.qty <= 0) s.inventory.Remove(b);
            }
            freshness = taken > 0 ? fresh / taken : 1f;
            return cost;
        }

        public static long ProcessSpoilage(GameState s, int day, List<string> report)
        {
            long value = 0;
            for (int i = s.inventory.Count - 1; i >= 0; i--)
            {
                var b = s.inventory[i];
                if (b.expiryDay < day)
                {
                    value += b.qty * b.unitCost;
                    report?.Add($"{GameDB.GetIng(b.ing).name} x{b.qty} hết hạn, phải bỏ");
                    s.spoilLog.Add(new SpoilRecord { day = day, ing = b.ing, qty = b.qty, value = b.qty * b.unitCost });
                    s.inventory.RemoveAt(i);
                }
            }
            s.spoilLog.RemoveAll(r => r.day < day - 13);
            return value;
        }

        public static long TotalValue(GameState s) => s.inventory.Sum(b => b.qty * b.unitCost);

        public static int NearestExpiry(GameState s, string ing)
        {
            int best = int.MaxValue;
            foreach (var b in s.inventory) if (b.ing == ing && b.qty > 0) best = Math.Min(best, b.expiryDay);
            return best;
        }
    }

    public class Market
    {
        public const float RetailDelaySeconds = 5f;
        readonly GameManager gm;
        public Market(GameManager gm) { this.gm = gm; }

        public bool Instant => gm.State.day == 1 && gm.Phase == GamePhase.Prep;

        public int PackQty(string ing) => gm.Day1Stocking ? gm.Day1Qty(GameDB.GetIng(ing)) : GameDB.GetIng(ing).pack;

        public static float BulkDiscount(int packs) => packs >= 5 ? 0.10f : packs >= 3 ? 0.05f : 0f;

        static uint Hash(int day, string id, int salt)
        {
            uint h = 2166136261u;
            unchecked
            {
                foreach (char c in id) { h ^= c; h *= 16777619u; }
                h ^= (uint)(day * 374761393 + salt * 668265263);
                h *= 16777619u;
                h ^= h >> 13;
            }
            return h;
        }

        static float Noise01(int day, string id, int salt) => (Hash(day, id, salt) % 10000) / 10000f;

        float DayFactor(string ing) => 1f + (Noise01(gm.State.day, ing, 1) - 0.5f) * 0.16f;
        float PriceMul(string ing) => gm.Chapter.ingredientPriceMul * gm.State.TempPriceMul * DayFactor(ing);

        static long Round100(double v) => Math.Max(100, (long)Math.Round(v / 100.0) * 100);

        public const long RetailRunFee = 30_000;

        public long RetailPrice(string ing) => Round100(GameDB.GetIng(ing).retailPrice * PriceMul(ing));
        public long RetailCost(string ing, int units) => RetailPrice(ing) * units + RetailRunFee;
        public long WholesaleUnit(string ing) => Round100(GameDB.GetIng(ing).wholesale * PriceMul(ing));
        public long WholesalePack(string ing) => gm.Day1Stocking ? WholesaleUnit(ing) * PackQty(ing) : Round100(GameDB.GetIng(ing).packPrice * PriceMul(ing));
        public long WholesaleCost(string ing, int packs) =>
            gm.Day1Stocking ? WholesalePack(ing) * packs : Round100(WholesalePack(ing) * packs * (1.0 - BulkDiscount(packs)));

        public float OrderSeconds(string ing) => GameDB.GetIng(ing).OrderSeconds * gm.Chapter.orderDelayMul * gm.State.TempDelayMul;

        public bool RetailAvailable(string ing) =>
            gm.Chapter.shortageChance <= 0 || Noise01(gm.State.day, ing, 7) >= gm.Chapter.shortageChance;

        public int PendingQty(string ing) => gm.State.deliveries.Where(d => d.ing == ing).Sum(d => d.qty);

        public static string Wait(float seconds) => seconds >= 60 ? $"{seconds / 60f:0.#} phút" : $"{Math.Ceiling(seconds)} giây";

        void Receive(string ing, int qty, long unitCost, bool wholesale, float delay, string label)
        {
            if (Instant)
            {
                Inventory.Add(gm.State, ing, qty, unitCost, gm.State.day);
                gm.Toast($"Đã nhận {label} (hôm nay hàng có ngay)");
            }
            else
            {
                gm.State.deliveries.Add(new PendingDelivery { ing = ing, qty = qty, unitCost = unitCost, wholesale = wholesale, arriveTimer = delay });
                gm.Toast($"Đã {(wholesale ? "đặt" : "nhờ chạy chợ mua")} {label}, về sau ~{Wait(delay)}");
            }
            gm.MarkDirty();
        }

        public bool BuyRetail(string ing, int units)
        {
            var def = GameDB.GetIng(ing);
            if (units <= 0) return false;
            if (!RetailAvailable(ing)) { Sfx.Play("error"); gm.Toast($"Chợ hôm nay hết {def.name}!"); return false; }
            int qty = def.retailQty * units;
            long goods = RetailPrice(ing) * units;
            if (gm.State.cash < goods + RetailRunFee) { Sfx.Play("error"); gm.Toast("Không đủ tiền mặt!"); return false; }
            gm.AddLedger(Finance.CatPurchase, $"Mua lẻ {def.name} {units} x {def.retailUnit}", -goods);
            gm.AddLedger(Finance.CatPurchase, "Tiền xe ôm chạy chợ", -RetailRunFee);
            Sfx.Play("coin");
            Receive(ing, qty, Math.Max(1, goods / qty), false, RetailDelaySeconds, $"{def.name} {units} x {def.retailUnit} ({qty} phần)");
            return true;
        }

        public bool BuyWholesale(string ing, int packs)
        {
            var def = GameDB.GetIng(ing);
            if (packs <= 0) return false;
            int qty = PackQty(ing) * packs;
            long cost = WholesaleCost(ing, packs);
            if (gm.State.cash < cost) { Sfx.Play("error"); gm.Toast("Không đủ tiền mặt!"); return false; }
            string label = gm.Day1Stocking ? $"{def.name} ({qty} phần)" : $"{def.name} {packs} x {def.packUnit} ({qty} phần)";
            gm.AddLedger(Finance.CatPurchase, $"Đặt sỉ {label}", -cost);
            Sfx.Play("coin");
            Receive(ing, qty, Math.Max(1, cost / qty), true, OrderSeconds(ing), label);
            return true;
        }

        public void Tick(float dt)
        {
            var list = gm.State.deliveries;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var d = list[i];
                d.arriveTimer -= dt;
                if (d.arriveTimer <= 0)
                {
                    Inventory.Add(gm.State, d.ing, d.qty, d.unitCost, gm.State.day);
                    list.RemoveAt(i);
                    gm.Toast($"Hàng đã về: {GameDB.GetIng(d.ing).name} x{d.qty}");
                    Sfx.Play("pop");
                    gm.MarkDirty();
                }
            }
        }

        public void MorningDeliveries(List<string> report)
        {
            var list = gm.State.deliveries;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var d = list[i];
                Inventory.Add(gm.State, d.ing, d.qty, d.unitCost, gm.State.day);
                report.Add($"Hàng về trong đêm: {GameDB.GetIng(d.ing).name} x{d.qty}");
                list.RemoveAt(i);
            }
        }
    }

    public class Report
    {
        public int from, to;
        public long revenue, cogs, wasteCost, spoiled, rent, utility, staff, tax, fines, interest;
        public long purchases, debtPaid, invest, otherIncome, loanIn, losses, otherExpense, personal, assetSale, theft;
        public int served, left, noSeat, dishes, drinks, ruined, spiceWrong, ratingSum, ratingCount;
        public Dictionary<string, int> sold = new Dictionary<string, int>();

        public int Days => to - from + 1;
        public long GrossProfit => revenue - cogs;
        public long DeductibleExpenses => cogs + spoiled + rent + utility + staff + interest + otherExpense;
        public long NetProfit => revenue + otherIncome - DeductibleExpenses - losses - tax - fines;
        public long CashFlow => revenue + otherIncome + loanIn + assetSale - purchases - rent - utility - staff - tax - fines
                                - debtPaid - invest - losses - otherExpense - personal - theft;
        public float AvgRating => ratingCount == 0 ? 0 : (float)ratingSum / ratingCount;
    }

    public static class Finance
    {
        public const string CatRevenue = "Doanh thu";
        public const string CatPurchase = "Nhập hàng";
        public const string CatRent = "Mặt bằng";
        public const string CatUtility = "Điện nước gas";
        public const string CatStaff = "Lương nhân viên";
        public const string CatTax = "Thuế";
        public const string CatFine = "Phạt & chậm nộp";
        public const string CatDebt = "Trả nợ";
        public const string CatInvest = "Đầu tư";
        public const string CatOther = "Thu khác";
        public const string CatLoan = "Vay tiền";
        public const string CatLoss = "Mất mát";
        public const string CatExpense = "Chi phí khác";
        public const string CatPersonal = "Chi việc nhà";
        public const string CatAssetSale = "Thanh lý tài sản";
        public const string CatTheft = "Bị trộm";

        public static string Money(long v)
        {
            string s = Math.Abs(v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".");
            return (v < 0 ? "-" : "") + s + "đ";
        }

        public static string Short(long v)
        {
            double a = Math.Abs(v);
            string s = a >= 1_000_000_000 ? (a / 1_000_000_000.0).ToString("0.##") + " tỷ"
                : a >= 1_000_000 ? (a / 1_000_000.0).ToString("0.##") + "tr"
                : a >= 1000 ? (a / 1000.0).ToString("0.#") + "k" : a.ToString("0");
            return (v < 0 ? "-" : "") + s;
        }

        public static Report Build(GameState s, int from, int to)
        {
            var r = new Report { from = from, to = to };
            foreach (var e in s.ledger)
            {
                if (e.day < from || e.day > to) continue;
                switch (e.cat)
                {
                    case CatRevenue: r.revenue += e.amount; break;
                    case CatPurchase: r.purchases -= e.amount; break;
                    case CatRent: r.rent -= e.amount; break;
                    case CatUtility: r.utility -= e.amount; break;
                    case CatStaff: r.staff -= e.amount; break;
                    case CatTax: r.tax -= e.amount; break;
                    case CatFine: r.fines -= e.amount; break;
                    case CatDebt: r.debtPaid -= e.amount; break;
                    case CatInvest: r.invest -= e.amount; break;
                    case CatOther: r.otherIncome += e.amount; break;
                    case CatLoan: r.loanIn += e.amount; break;
                    case CatLoss: r.losses -= e.amount; break;
                    case CatExpense: r.otherExpense -= e.amount; break;
                    case CatPersonal: r.personal -= e.amount; break;
                    case CatAssetSale: r.assetSale += e.amount; break;
                    case CatTheft: r.theft -= e.amount; break;
                }
            }
            foreach (var d in s.stats)
            {
                if (d.day < from || d.day > to) continue;
                r.cogs += d.cogs; r.wasteCost += d.wasteCost; r.spoiled += d.spoiled; r.interest += d.interestAccrued;
                r.served += d.groupsServed; r.left += d.groupsLeft; r.noSeat += d.groupsNoSeat;
                r.dishes += d.dishesSold; r.drinks += d.drinksSold; r.ruined += d.ruined; r.spiceWrong += d.spiceWrong;
                r.ratingSum += d.ratingSum; r.ratingCount += d.ratingCount;
                foreach (var nc in d.sold)
                {
                    r.sold.TryGetValue(nc.id, out int c);
                    r.sold[nc.id] = c + nc.count;
                }
            }
            return r;
        }

        static void Line(StringBuilder sb, string label, long v, bool expense = true)
        {
            sb.Append("  ").Append(label).Append(": ").AppendLine(expense && v != 0 ? Money(-v) : Money(v));
        }

        public static string ReportText(GameState s, Report r, string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{title}</b>");
            sb.AppendLine(r.Days > 1 ? $"(Ngày {r.from} - {r.to}, {r.Days} ngày)" : $"(Ngày {r.from})");
            sb.AppendLine();
            sb.AppendLine("<b>KẾT QUẢ KINH DOANH</b>");
            Line(sb, "Doanh thu bán hàng", r.revenue, false);
            Line(sb, "Giá vốn nguyên liệu đã dùng", r.cogs);
            if (r.wasteCost > 0) sb.AppendLine($"     (trong đó món hỏng / khách bỏ: {Money(r.wasteCost)})");
            long gm = r.GrossProfit;
            sb.AppendLine($"  = Lợi nhuận gộp: {Money(gm)}" + (r.revenue > 0 ? $" ({gm * 100 / r.revenue}% doanh thu)" : ""));
            Line(sb, "Hao hụt (nguyên liệu hết hạn)", r.spoiled);
            Line(sb, "Tiền mặt bằng", r.rent);
            Line(sb, "Điện, nước, gas", r.utility);
            Line(sb, "Lương nhân viên", r.staff);
            Line(sb, "Lãi vay phát sinh", r.interest);
            if (r.otherExpense > 0) Line(sb, "Chi phí khác (sửa chữa, giấy phép...)", r.otherExpense);
            if (r.losses > 0) Line(sb, "Mất mát (đổ vỡ, hư hỏng, bị lừa...)", r.losses);
            Line(sb, "Thuế đã nộp", r.tax);
            Line(sb, "Tiền phạt & chậm nộp", r.fines);
            if (r.otherIncome > 0) Line(sb, "Thu nhập khác", r.otherIncome, false);
            long np = r.NetProfit;
            string color = np >= 0 ? "#1B7F2A" : "#C62828";
            sb.AppendLine($"  <b><color={color}>=> {(np >= 0 ? "LỢI NHUẬN" : "LỖ")}: {Money(Math.Abs(np))}</color></b>");
            if (r.Days > 1) sb.AppendLine($"  Trung bình / ngày: doanh thu {Money(r.revenue / r.Days)}, lãi {Money(np / r.Days)}");
            sb.AppendLine();
            sb.AppendLine("<b>DÒNG TIỀN MẶT</b>");
            Line(sb, "Tiền nhập hàng (đặt hàng + mua lẻ)", r.purchases);
            Line(sb, "Trả nợ", r.debtPaid);
            Line(sb, "Đầu tư (bếp, bàn...)", r.invest);
            if (r.personal > 0) Line(sb, "Chi việc nhà", r.personal);
            if (r.theft > 0) Line(sb, "Bị trộm (không tính vào lãi lỗ kinh doanh)", r.theft);
            if (r.loanIn > 0) Line(sb, "Tiền vay vào", r.loanIn, false);
            if (r.assetSale > 0) Line(sb, "Thanh lý tài sản bị tịch thu", r.assetSale, false);
            sb.AppendLine($"  Dòng tiền thuần: {Money(r.CashFlow)}   |   Tiền mặt hiện tại: {Money(s.cash)}");
            sb.AppendLine($"  Giá trị tồn kho: {Money(Inventory.TotalValue(s))}");
            sb.AppendLine();
            sb.AppendLine("<b>KHÁCH HÀNG</b>");
            sb.AppendLine($"  Lượt bàn phục vụ: {r.served}   |   Khách bỏ về: {r.left}   |   Hết chỗ ngồi: {r.noSeat}");
            sb.AppendLine($"  Món ăn bán: {r.dishes}   |   Đồ uống: {r.drinks}   |   Món hỏng: {r.ruined}   |   Sai độ cay: {r.spiceWrong}");
            sb.AppendLine($"  Đánh giá trung bình: {(r.ratingCount > 0 ? r.AvgRating.ToString("0.0") + "/5" : "-")}   |   Uy tín quán: {s.reputation:0.00}/5");
            if (r.sold.Count > 0)
            {
                var top = r.sold.OrderByDescending(k => k.Value).Take(5)
                    .Select(k => $"{GameDB.GetRecipe(k.Key)?.name ?? k.Key} ({k.Value})");
                sb.AppendLine("  Bán chạy: " + string.Join(", ", top));
            }
            return sb.ToString();
        }
    }

    public static class Legal
    {
        public const int PrisonPoints = 6;
        public const int DecayDays = 10;

        public static void AddViolation(GameManager gm, int points, string what, List<DialogueLine> lines)
        {
            var s = gm.State;
            s.violationPoints += points;
            s.lastViolationDay = s.day;
            Sfx.Play("alarm");
            lines?.Add(new DialogueLine("narr",
                $"GHI NHẬN VI PHẠM: {what} (+{points} điểm, tổng {s.violationPoints}/{PrisonPoints}). Đủ {PrisonPoints} điểm là bị truy cứu và ĐI TÙ!"));
            if (s.violationPoints >= PrisonPoints)
                gm.SendToPrison($"Tích lũy {s.violationPoints} điểm vi phạm (lần cuối: {what}).");
        }

        public static void DailyDecay(GameState s)
        {
            if (s.violationPoints > 0 && s.day - s.lastViolationDay >= DecayDays)
            {
                s.violationPoints--;
                s.lastViolationDay = s.day;
            }
        }

        public static bool Seize(GameManager gm, string reason, List<DialogueLine> lines)
        {
            var s = gm.State;
            if (s.cash >= 0) return true;

            var seized = new List<string>();
            long stock = Inventory.TotalValue(s) / 2;
            if (stock > 0)
            {
                s.inventory.Clear();
                s.deliveries.Clear();
                gm.AddLedger(Finance.CatAssetSale, "Thanh lý hàng trong kho bị tịch thu", stock);
                seized.Add($"toàn bộ hàng trong kho ({Finance.Money(stock)})");
            }
            long equip = 0;
            while (s.cash + equip < 0 && s.stoves > 2) { equip += gm.StoveResale(s.stoves - 1); gm.RemoveStoveAt(s.stoves - 1); }
            while (s.cash + equip < 0 && s.barTiers.Count > 1) { equip += gm.BarResale(s.barTiers.Count - 1); gm.RemoveBarAt(s.barTiers.Count - 1); }
            while (s.cash + equip < 0 && s.tables > GameState.StartTables)
            {
                s.tableSlots.Remove(s.tableSlots.Max());
                s.SyncCounts();
                equip += GameManager.TableResale;
            }
            if (equip > 0)
            {
                gm.AddLedger(Finance.CatAssetSale, "Thanh lý bếp, máy pha, bàn bị tịch thu", equip);
                gm.Kitchen.RebuildStations();
                gm.Customers.AssignHelpers();
                seized.Add($"thiết bị ({Finance.Money(equip)})");
            }
            if (seized.Count > 0)
                lines?.Add(new DialogueLine("narr", $"CƯỠNG CHẾ ({reason}): tịch thu {string.Join(", ", seized)} để trừ nợ."));
            if (s.cash >= 0) return true;

            gm.SendToPrison($"{reason}: đã tịch thu toàn bộ tài sản nhưng vẫn còn thiếu {Finance.Money(-s.cash)}.");
            return false;
        }
    }

    public static class TaxSystem
    {
        public const int PeriodDays = 7;
        public const int DueAfterDays = 3;
        public const int RealDaysPerGameDay = 13;
        public const int EnforceAfterLateDays = 7;
        public const int PrisonEvasions = 3;
        public const double LateInterestPerDay = 0.0003;

        static int ChapterForTax(GameState s, int day) => day >= s.chapterStartDay ? s.chapter : StoryDB.ChapterOfDay(day);

        public static TaxBill ClosePeriod(GameState s, int from, int to)
        {
            var r = Finance.Build(s, from, to);
            int days = to - from + 1;
            var b = new TaxBill
            {
                id = s.nextBillId++, fromDay = from, toDay = to, dueDay = to + DueAfterDays,
                revenue = r.revenue, expenses = r.DeductibleExpenses,
                method = StoryDB.GetChapter(ChapterForTax(s, to)).taxMethod,
                annualized = r.revenue * 365 / days,
            };

            int segFrom = from;
            while (segFrom <= to)
            {
                int n = ChapterForTax(s, segFrom);
                int segTo = segFrom;
                while (segTo < to && ChapterForTax(s, segTo + 1) == n) segTo++;
                var ch = StoryDB.GetChapter(n);
                if (segFrom > from) b.splitChapters = true;
                if (ch.forceAboveThreshold || b.annualized > ch.taxThresholdYear)
                {
                    b.aboveThreshold = true;
                    var seg = Finance.Build(s, segFrom, segTo);
                    int segDays = segTo - segFrom + 1;
                    b.vat += (long)Math.Round(seg.revenue * ch.vatRate);
                    long taxable = ch.taxMethod == TaxMethod.RevenuePercent
                        ? seg.revenue - ch.taxThresholdYear * segDays / 365
                        : seg.revenue - seg.DeductibleExpenses;
                    b.pit += (long)Math.Round(Math.Max(0, taxable) * ch.pitRate);
                }
                segFrom = segTo + 1;
            }
            s.taxBills.Add(b);
            return b;
        }

        public static int LateDays(TaxBill b, int day) => Math.Max(0, day - b.dueDay);

        public static long Interest(TaxBill b, int day) =>
            (long)Math.Round(b.Total * LateInterestPerDay * LateDays(b, day) * RealDaysPerGameDay);

        public static long FilingFine(int realLateDays)
        {
            if (realLateDays <= 5) return 0;
            if (realLateDays <= 30) return 2_500_000;
            if (realLateDays <= 60) return 4_000_000;
            if (realLateDays <= 90) return 7_500_000;
            return 12_500_000;
        }

        public static long FineIfPaidOn(TaxBill b, int day) => FilingFine(LateDays(b, day) * RealDaysPerGameDay);

        public static long AmountDue(TaxBill b, int day) => b.Total + Interest(b, day) + FineIfPaidOn(b, day);

        public static bool Pay(GameManager gm, TaxBill b)
        {
            var s = gm.State;
            if (b.paid) return false;
            long interest = Interest(b, s.day);
            long fine = FineIfPaidOn(b, s.day);
            long total = b.Total + interest + fine;
            if (s.cash < total) { Sfx.Play("error"); gm.Toast("Không đủ tiền để nộp thuế!"); return false; }
            if (b.Total > 0) gm.AddLedger(Finance.CatTax, $"Thuế kỳ ngày {b.fromDay}-{b.toDay}", -b.Total);
            if (interest + fine > 0) gm.AddLedger(Finance.CatFine, $"Chậm nộp/phạt kỳ {b.fromDay}-{b.toDay}", -(interest + fine));
            b.paid = true; b.paidDay = s.day; b.interestPaid = interest; b.finePaid = fine;
            Sfx.Play("stamp");
            gm.Toast(b.Total > 0 ? $"Đã khai & nộp thuế {Finance.Money(total)}" : "Đã nộp tờ khai (không phát sinh thuế)");
            gm.MarkDirty();
            return true;
        }

        public static int UnpaidCount(GameState s) => s.taxBills.Count(b => !b.paid);
        public static bool HasOverdue(GameState s) => s.taxBills.Any(b => !b.paid && s.day > b.dueDay);

        public static void EndOfDayCheck(GameManager gm, List<DialogueLine> lines)
        {
            var s = gm.State;
            foreach (var b in s.taxBills)
            {
                if (b.paid || LateDays(b, s.day) < EnforceAfterLateDays) continue;
                s.evasionCount++;
                int mult = Math.Min(3, s.evasionCount);
                long interest = Interest(b, s.day);
                long filing = FineIfPaidOn(b, s.day);
                long evasion = b.Total * mult;
                long total = b.Total + interest + filing + evasion;
                if (b.Total > 0) gm.AddLedger(Finance.CatTax, $"Truy thu thuế kỳ {b.fromDay}-{b.toDay}", -b.Total);
                gm.AddLedger(Finance.CatFine, $"Cưỡng chế kỳ {b.fromDay}-{b.toDay}", -(interest + filing + evasion));
                b.paid = true; b.enforced = true; b.paidDay = s.day; b.interestPaid = interest; b.finePaid = filing + evasion;
                s.closedDay = s.day + 1;
                Sfx.Play("alarm");
                s.reputation = Math.Max(1f, s.reputation - 0.4f);
                lines.Add(new DialogueLine("lan",
                    $"Quán em đã quá hạn kỳ thuế ngày {b.fromDay}-{b.toDay} khoảng {LateDays(b, s.day) * RealDaysPerGameDay} ngày. " +
                    "Đội kiểm tra phải ra quyết định cưỡng chế."));
                lines.Add(new DialogueLine("lan",
                    $"Truy thu thuế {Finance.Money(b.Total)}, tiền chậm nộp {Finance.Money(interest)}, phạt không nộp hồ sơ {Finance.Money(filing)}" +
                    (evasion > 0 ? $", phạt trốn thuế {mult} lần = {Finance.Money(evasion)}" : "") +
                    $". Tổng cộng {Finance.Money(total)}. Ngày mai quán bị tạm đình chỉ kinh doanh 1 ngày."));
                lines.Add(new DialogueLine("anh", "Trời ơi... Biết vậy em nộp sớm cho rồi."));

                if (!Legal.Seize(gm, "nợ thuế", lines)) return;
                if (s.evasionCount >= PrisonEvasions)
                {
                    gm.SendToPrison($"Bị cưỡng chế thuế lần thứ {s.evasionCount}: bị truy cứu tội trốn thuế.");
                    return;
                }
                Legal.AddViolation(gm, 2, $"không khai nộp thuế kỳ {b.fromDay}-{b.toDay}", lines);
                if (gm.PrisonPending) return;
            }
        }
    }

    public static class DebtSystem
    {
        public static bool Pay(GameManager gm, Debt d, long amount)
        {
            var s = gm.State;
            if (d.id == "me" && s.day <= 3) { Sfx.Play("error"); gm.Toast("Mẹ dặn cứ lo buôn bán cái đã, mấy ngày đầu chưa trả vội. Trả cho mẹ từ ngày thứ 4 trở đi nha."); return false; }
            amount = Math.Min(amount, d.remaining);
            if (amount <= 0) return false;
            if (s.cash < amount) { Sfx.Play("error"); gm.Toast("Không đủ tiền mặt!"); return false; }
            gm.AddLedger(Finance.CatDebt, $"Trả nợ {d.name}", -amount);
            d.remaining -= amount;
            d.paidThisPeriod += amount;
            Sfx.Play("coin");
            gm.Toast(d.remaining <= 0 ? $"Đã trả hết nợ {d.name}!" : $"Đã trả {Finance.Money(amount)} cho {d.creditor}");
            gm.MarkDirty();
            return true;
        }

        public static long DueThisPeriod(Debt d) => Math.Max(0, Math.Min(d.installment, d.remaining + d.paidThisPeriod) - d.paidThisPeriod);

        public static bool EndOfDay(GameManager gm, List<DialogueLine> lines)
        {
            var s = gm.State;
            bool fail = false;
            foreach (var d in s.debts.ToList())
            {
                if (d.remaining <= 0) continue;
                if (d.kind == Debt.Bank) { BankSystem.EndOfDay(gm, d, lines); if (gm.PrisonPending) return false; continue; }
                if (d.periodDays <= 0 || s.day < d.nextDueDay) continue;
                long shortBy = DueThisPeriod(d);
                if (shortBy > 0)
                {
                    d.missed++;
                    long penalty = (long)Math.Round(d.remaining * 0.05);
                    d.remaining += penalty;
                    s.Today.interestAccrued += penalty;
                    lines.Add(new DialogueLine(d.kind == Debt.Shark ? "khai" : d.creditor == "Anh Khải" ? "khai" : "narr",
                        $"Kỳ này còn thiếu {Finance.Money(shortBy)} cho khoản \"{d.name}\" (mỗi kỳ phải trả {Finance.Money(d.installment)}). Phạt trễ hạn {Finance.Money(penalty)}. (Trễ {d.missed} lần)"));
                    if (d.mandatory && d.missed >= 2) fail = true;
                }
                long interest = (long)Math.Round(d.remaining * d.interestPerPeriod);
                if (interest > 0)
                {
                    d.remaining += interest;
                    s.Today.interestAccrued += interest;
                }
                d.paidThisPeriod = 0;
                d.nextDueDay += d.periodDays;
            }
            return fail;
        }
    }

    public static class BankSystem
    {
        public static readonly (int days, float rate)[] Terms = { (7, 0.02f), (14, 0.045f), (21, 0.07f) };
        public static readonly long[] Amounts = { 5_000_000, 10_000_000, 30_000_000, 50_000_000, 70_000_000 };
        public const int GraceDays = 3;
        public const double OverduePerDay = 0.01;

        public static long Limit(GameState s) => 70_000_000;

        public static long Outstanding(GameState s) => s.debts.Where(d => d.kind == Debt.Bank).Sum(d => Math.Max(0, d.remaining));

        public static Debt MakeLoan(GameState s, long amount, int days, float rate, string name)
        {
            int n = s.nextBankId++;
            return new Debt
            {
                id = "bank_" + n, kind = Debt.Bank, name = $"{name} #{n}", creditor = "Ngân hàng",
                remaining = amount + (long)Math.Round(amount * rate), dueDay = s.day + days,
            };
        }

        public static bool Borrow(GameManager gm, long amount, int termIndex)
        {
            var s = gm.State;
            var (days, rate) = Terms[termIndex];
            if (Outstanding(s) + amount > Limit(s)) { Sfx.Play("error"); gm.Toast($"Vượt hạn mức vay ({Finance.Money(Limit(s))})!"); return false; }
            if (s.debts.Any(d => d.kind == Debt.Bank && d.remaining > 0 && s.day > d.dueDay))
            { Sfx.Play("error"); gm.Toast("Đang có khoản vay quá hạn, ngân hàng từ chối cho vay thêm!"); return false; }
            var d = MakeLoan(s, amount, days, rate, "Vay ngân hàng");
            s.debts.Add(d);
            gm.AddLedger(Finance.CatLoan, d.name, amount);
            Sfx.Play("coin");
            gm.Toast($"Đã vay {Finance.Money(amount)}, phải trả {Finance.Money(d.remaining)} trước hết ngày {d.dueDay}");
            gm.MarkDirty();
            return true;
        }

        public static void EndOfDay(GameManager gm, Debt d, List<DialogueLine> lines)
        {
            var s = gm.State;
            if (s.day < d.dueDay) return;
            if (s.day == d.dueDay)
            {
                lines.Add(new DialogueLine("narr", $"Hôm nay là hạn chót khoản \"{d.name}\" ({Finance.Money(d.remaining)}). Từ mai bị tính lãi phạt 1%/ngày, trễ {GraceDays} ngày là bị cưỡng chế thu hồi!"));
                return;
            }
            int late = s.day - d.dueDay;
            long penalty = (long)Math.Round(d.remaining * OverduePerDay);
            d.remaining += penalty;
            s.Today.interestAccrued += penalty;
            if (late < GraceDays)
            {
                lines.Add(new DialogueLine("narr", $"Khoản \"{d.name}\" quá hạn {late} ngày, lãi phạt {Finance.Money(penalty)}. Còn {GraceDays - late} ngày trước khi ngân hàng cưỡng chế!"));
                return;
            }
            long owed = d.remaining;
            gm.AddLedger(Finance.CatDebt, $"Ngân hàng cưỡng chế thu hồi {d.name}", -owed);
            d.remaining = 0;
            lines.Add(new DialogueLine("narr", $"Ngân hàng cưỡng chế thu hồi khoản \"{d.name}\": {Finance.Money(owed)}."));
            if (!Legal.Seize(gm, "nợ ngân hàng quá hạn", lines)) return;
            Legal.AddViolation(gm, 3, $"nợ ngân hàng quá hạn {late} ngày", lines);
        }
    }
}
