using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MiCay
{
    public enum ItemStatus { Waiting, Cooking, Ready }

    public class OrderItem
    {
        public int uid;
        public RecipeDef recipe;
        public int spice;
        public ItemStatus status;
        public bool spiceOk = true;
        public float quality = 1f;
        public bool stale;
        public string cook;
        public readonly List<string> extras = new List<string>();
        public readonly List<string> omits = new List<string>();

        public bool HasExtras => extras.Count > 0;
        public bool HasOmits => omits.Count > 0;
        public bool IsCustom => HasExtras || HasOmits;
        public string OmitText => omits.Count == 0 ? "" : " • không " + string.Join(", không ", omits.Select(o => GameDB.OmitName(o)));
        public string ExtrasText => extras.Count == 0 ? ""
            : " +" + string.Join(" +", extras.GroupBy(x => x).Select(g => (g.Count() > 1 ? $"{g.Count()} " : "") + GameDB.GetIng(g.Key).name));
        public string broth;
        public string BrothText => broth == null || recipe.broth != "any" ? "" : " • nước " + GameDB.BrothShort(broth);
        public string Label => (recipe.spicy ? $"{recipe.name} • Cấp {spice}" : recipe.name) + BrothText + OmitText + ExtrasText;
        public long ExtrasPrice => extras.Sum(e => (long)GameDB.GetIng(e).toppingPrice);
        public long BasePrice => recipe.price + ExtrasPrice;

        public Dictionary<string, int> Needs()
        {
            var want = new Dictionary<string, int>();
            foreach (var ri in recipe.items) { if (omits.Contains(ri.ing)) continue; want.TryGetValue(ri.ing, out int c); want[ri.ing] = c + ri.qty; }
            if (broth != null) { want.TryGetValue(broth, out int b); want[broth] = b + 1; }
            foreach (var e in extras) { want.TryGetValue(e, out int c); want[e] = c + 1; }
            return want;
        }
    }

    public class CustomerGroup
    {
        public int id, table, size;
        public int[] looks;
        public string name;
        public List<OrderItem> items = new List<OrderItem>();
        public float patience, maxPatience;

        public int fromSide;
        public float walkIn, walkTotal;
        public string orderLine;
        public bool serving;
        public bool eating;
        public int arrivedSeq;
        public float eatTimer;
        public const float EatSeconds = 5f;

        public bool Arrived => walkIn <= 0;
        public float EatProgress => eating ? Mathf.Clamp01(1f - eatTimer / EatSeconds) : 0f;
        public float WalkProgress => walkTotal <= 0 ? 1f : Mathf.Clamp01(1f - walkIn / walkTotal);
        public bool AllReady => items.All(i => i.status == ItemStatus.Ready);
        public float PatienceRatio => maxPatience <= 0 ? 0 : patience / maxPatience;
    }

    public class Leaver
    {
        public int table, size, side;
        public int[] looks;
        public float t, total;
        public bool angry;
        public float Progress => total <= 0 ? 1f : Mathf.Clamp01(t / total);
    }

    public enum WaiterPhase { Idle, Going, Handing, Returning }

    public class Waiter
    {
        public string id;
        public WaiterPhase phase;
        public int groupId = -1, table;
        public float t, dur;
        public readonly List<RecipeDef> tray = new List<RecipeDef>();
        public float Progress => dur <= 0 ? 1f : Mathf.Clamp01(t / dur);
    }

    public class CustomerSystem
    {
        static readonly string[] MaleNames = { "Anh Huy", "Anh Tài", "Bạn Khoa", "Chú Sáu", "Anh Phúc", "Anh Long", "Bạn Quân", "Anh Bảo", "Chú Bảy" };
        static readonly string[] FemaleNames = { "Chị Mai", "Bé Na", "Cô Ba", "Chị Thảo", "Chị Hằng", "Bạn Linh", "Cô Hạnh", "Chị Ngọc", "Bạn Trâm" };
        static readonly int[] FemaleLooks = { 0, 4, 6, 9 };

        static string NameFor(int look)
        {
            var pool = Array.IndexOf(FemaleLooks, look % CustomerLooks) >= 0 ? FemaleNames : MaleNames;
            return pool[Random.Range(0, pool.Length)];
        }
        static readonly int[] SpiceWeights = { 8, 14, 18, 18, 14, 12, 9, 7 };
        public const float DrinkChance = 0.85f;
        public const float CustomOrderChance = 0.4f;
        public const float PriceCommentChance = 0.35f;
        static readonly string[] PriceUpLines =
        {
            "Ơ, hôm nay đắt hơn mọi khi hả chị?",
            "Quán tăng giá rồi à? Mắc hơn tí nhỉ.",
            "Sao bữa nay tính cao vậy chị ơi?",
            "Tăng giá hả? Thôi được, ủng hộ quán.",
        };
        static readonly string[] PriceDownLines =
        {
            "Ồ hôm nay rẻ hơn mọi khi, sướng ghê!",
            "Quán giảm giá à? Mai rủ thêm bạn tới!",
            "Rẻ vậy luôn, ăn thả ga nha chị!",
        };
        public static float AvgSpice => (float)SpiceWeights.Select((w, lv) => w * lv).Sum() / SpiceWeights.Sum();
        public static float FoodWeight(RecipeDef r) => r.cat == DishCategory.Noodle ? 3f : 2f;
        public static float DrinkWeight(RecipeDef r) => r.price <= 15000 ? 2f : 1f;
        public const int CustomerLooks = 12;
        static readonly string[] OrderLines =
        {
            "Cho em {0} nha!", "Bán cho anh {0} với!", "Làm giùm {0} nhé, đói quá rồi!",
            "Cho {0}, nhanh nhanh giùm nha!", "Hôm nay ăn {0} cho đã!",
        };
        static readonly string[] HappyLines = { "Ngon xuất sắc, lên món nhanh, 10 điểm!", "Nóng hổi, đậm vị, nhất định quay lại!", "Đúng chuẩn, không chê vào đâu được!", "Quán ngon mà phục vụ nhanh ghê!" };
        static readonly string[] AngryLines = { "Lâu quá, đi quán khác!", "Chờ hoài không thấy món...", "Thôi về, trễ giờ làm rồi!" };

        readonly GameManager gm;
        public readonly List<CustomerGroup> groups = new List<CustomerGroup>();
        public readonly List<Waiter> waiters = new List<Waiter>();
        public readonly List<Leaver> leavers = new List<Leaver>();
        readonly List<int> serveQueue = new List<int>();
        const float HandOverSeconds = 0.6f;

        public const int Areas = 4;
        public const int TablesPerArea = 8;
        public const int MaxTables = Areas * TablesPerArea;
        public const float WorldScreens = Areas;
        public static float TableX(int t) => ((t / TablesPerArea) * 4 + (t % 4) + 0.5f) / (4f * Areas);
        public static float TableY(int t) => (t % TablesPerArea) / 4 == 0 ? 0.33f : 0.065f;
        public static float AreaMin(int t) => (t / TablesPerArea) / WorldScreens;
        public static float CustomerStartX(int t, int side) => side < 0 ? AreaMin(t) - 0.03f / WorldScreens : AreaMin(t) + 1.03f / WorldScreens;
        public static float LeaverExitX(int t, int side) => side < 0 ? AreaMin(t) - 0.32f / WorldScreens : AreaMin(t) + 1.32f / WorldScreens;
        public const float CounterX = 0.5f / WorldScreens;
        float spawnTimer = 3f;
        int nextId = 1, nextUid = 1, arrivalCounter, kimchiToday, thaiToday;

        public CustomerSystem(GameManager gm) { this.gm = gm; }

        public void Reset()
        {
            groups.Clear();
            serveQueue.Clear();
            leavers.Clear();
            arrivalCounter = 0;
            kimchiToday = thaiToday = 0;
            spawnTimer = 3f;
            EnsureWaiters();
            foreach (var w in waiters) { w.phase = WaiterPhase.Idle; w.groupId = -1; w.tray.Clear(); }
            foreach (var h in helpers) { h.serveGroup = -1; h.claim = -1; h.helpLane = -1; h.workLane = -1; h.assistGroup = -1; h.returning = false; h.prep = 0; h.t = 0; h.hand = 0; h.tray.Clear(); }
            AssignHelpers();
        }

        void EnsureWaiters()
        {
            string[] ids = { "anh", "vy" };
            int want = Mathf.Clamp(1 + gm.State.ActiveStaff, 1, ids.Length);
            while (waiters.Count < want) waiters.Add(new Waiter { id = ids[waiters.Count] });
            while (waiters.Count > want) waiters.RemoveAt(waiters.Count - 1);
        }

        public Waiter WaiterFor(int groupId) => waiters.FirstOrDefault(w => w.phase != WaiterPhase.Idle && w.groupId == groupId);

        public class Helper
        {
            public string name;
            public int area = -1;
            public float prep;
            public int claim = -1, helpLane = -1, workLane = -1, assistGroup = -1;
            public int serveGroup = -1, table;
            public float t, dur, hand;
            public bool returning;
            public readonly List<RecipeDef> tray = new List<RecipeDef>();
            public bool Walking => serveGroup >= 0 || returning || hand > 0;
            public bool Handing => hand > 0;
            public float Progress => dur <= 0 ? 1f : Mathf.Clamp01(t / dur);
        }

        public readonly List<Helper> helpers = new List<Helper>();
        const float HelperPrepSeconds = 5f, HelpNoticeGap = 20f, HelperIdleCheck = 0.25f;
        public const int FrontStaffMax = 2, AreaStaffMax = 3;
        public const string OwnerCook = "anh";
        public static readonly string[] AreaNames = { "Sân trước", "Khu mở rộng", "Sân sau", "Khu vỉa hè" };
        readonly Dictionary<string, float> helpNoticeAt = new Dictionary<string, float>();

        public static int AreaOf(int table) => table / TablesPerArea;

        public static int StaffMax(int area) => area == 0 ? FrontStaffMax : AreaStaffMax;

        public static string LaneName(int lane) => lane == 0 ? "chỗ chủ quán (Sân trước)" : AreaNames[lane];

        public bool Covered(CustomerGroup g) => g != null && AreaOf(g.table) > 0 && helpers.Any(h => h.area == AreaOf(g.table));

        public Helper ClaimedBy(CustomerGroup g) => g == null ? null : helpers.FirstOrDefault(h => h.claim == g.id);

        public bool OwnerWorking(CustomerGroup g) =>
            g != null && (gm.Kitchen.targetGroup == g.id || g.items.Any(i => i.status != ItemStatus.Waiting && i.cook == OwnerCook));

        public void AssignHelpers()
        {
            var s = gm.State;
            var keep = new List<Helper>();
            for (int i = 0; i < s.helpers.Count; i++)
            {
                var name = s.helpers[i];
                var h = helpers.FirstOrDefault(x => x.name == name && !keep.Contains(x)) ?? new Helper { name = name };
                h.area = i < s.helperAreas.Count ? s.helperAreas[i] : -1;
                keep.Add(h);
            }
            foreach (var gone in helpers.Where(x => !keep.Contains(x)))
            {
                var sg = gone.serveGroup >= 0 ? Find(gone.serveGroup) : null;
                if (sg != null) sg.serving = false;
            }
            helpers.Clear();
            helpers.AddRange(keep);
            gm.MarkDirty();
        }

        void TickHelpers(float dt)
        {
            foreach (var h in helpers.OrderBy(ClaimSeq).ToList())
            {
                if (h.Walking)
                {
                    if (h.hand > 0)
                    {
                        h.hand -= dt;
                        if (h.hand > 0) continue;
                        h.hand = 0;
                        h.returning = true;
                        h.t = 0;
                        gm.MarkDirty();
                        continue;
                    }
                    h.t += dt;
                    if (h.t < h.dur) continue;
                    if (h.serveGroup >= 0)
                    {
                        var sg = Find(h.serveGroup);
                        if (sg != null) Deliver(sg);
                        if (h.claim == h.serveGroup) h.claim = -1;
                        h.serveGroup = -1;
                        h.tray.Clear();
                        h.hand = HandOverSeconds;
                    }
                    else h.returning = false;
                    gm.MarkDirty();
                    continue;
                }
                if (h.prep > 0) { h.prep -= dt; continue; }
                var g = h.claim >= 0 ? Find(h.claim) : null;
                if (g == null || g.eating || g.serving || !g.Arrived)
                {
                    if (h.claim >= 0) gm.MarkDirty();
                    h.claim = -1;
                    if (!FindJob(h)) { h.workLane = -1; h.prep = HelperIdleCheck; }
                    continue;
                }
                if (g.AllReady) { StartHelperServe(h, g); continue; }
                bool started = false;
                foreach (var it in g.items)
                    if (it.status == ItemStatus.Waiting && gm.Kitchen.HelperCook(g, it, h.name)) { started = true; h.prep = HelperPrepSeconds; break; }
                if (started) continue;
                h.claim = -1;
                gm.MarkDirty();
                if (!FindJob(h)) { h.workLane = -1; h.prep = HelperIdleCheck; }
            }
        }

        int ClaimSeq(Helper h) => Find(h.claim)?.arrivedSeq ?? int.MaxValue;

        int OwnLane(Helper h) => h.area == 0 ? 0 : h.area > 0 && gm.State.AreaHasTables(h.area) ? h.area : -1;

        bool Open(CustomerGroup g) => g.Arrived && !g.eating && !g.serving && ClaimedBy(g) == null && !OwnerWorking(g) &&
                                      (g.AllReady || g.items.Any(i => i.status == ItemStatus.Waiting));

        int WorkingIn(int lane, Helper except) => helpers.Count(x => x != except && x.workLane == lane);

        bool IsOwnerTarget(CustomerGroup g, OrderItem it) => gm.Kitchen.targetGroup == g.id && gm.Kitchen.targetItem == it.uid;

        bool OwnerHelpable(CustomerGroup g) => g.Arrived && !g.eating && !g.serving && ClaimedBy(g) == null && OwnerWorking(g) &&
                                               g.items.Any(i => i.status == ItemStatus.Waiting && !IsOwnerTarget(g, i));

        bool HasCookableNow(CustomerGroup g) =>
            g.items.Any(it => it.status == ItemStatus.Waiting && !IsOwnerTarget(g, it) && gm.Kitchen.CanHelperCookNow(it));

        bool Actionable(CustomerGroup g) => g.AllReady || HasCookableNow(g);

        CustomerGroup OwnerReady() =>
            groups.Where(g => Lane(g) == 0 && g.Arrived && !g.eating && ClaimedBy(g) == null && OwnerWorking(g) && g.AllReady &&
                              (!g.serving || serveQueue.Contains(g.id)))
                  .OrderBy(g => g.arrivedSeq).FirstOrDefault();

        int Need(int lane) => groups.Count(g => Lane(g) == lane && (Open(g) || lane == 0 && OwnerHelpable(g))) + (lane == 0 && OwnerReady() != null ? 1 : 0);

        bool FindJob(Helper h)
        {
            int own = OwnLane(h);
            if (own >= 0 && WorkIn(h, own)) { h.helpLane = -1; return true; }
            int best = -1, most = 0;
            for (int lane = 0; lane < Areas; lane++)
            {
                if (lane == own || lane > 0 && !helpers.Any(x => x.area == lane)) continue;
                if (WorkingIn(lane, h) >= StaffMax(lane)) continue;
                int n = Need(lane);
                if (n > most) { best = lane; most = n; }
            }
            if (best < 0) return false;
            if (h.helpLane != best) Notice(h, $"{h.name} rảnh tay, sang phụ {LaneName(best)}.");
            h.helpLane = best;
            return WorkIn(h, best);
        }

        bool WorkIn(Helper h, int lane)
        {
            if (lane == 0)
            {
                var ready = OwnerReady();
                if (ready != null)
                {
                    h.workLane = 0;
                    StartHelperServe(h, ready);
                    return true;
                }
            }
            foreach (var g in groups.Where(x => Lane(x) == lane && (Open(x) || lane == 0 && OwnerHelpable(x))).OrderBy(x => x.arrivedSeq).ToList())
            {
                if (Open(g) && Actionable(g))
                {
                    h.claim = g.id;
                    h.workLane = lane;
                    gm.MarkDirty();
                    return true;
                }
                foreach (var it in g.items)
                {
                    if (it.status != ItemStatus.Waiting || IsOwnerTarget(g, it)) continue;
                    if (!gm.Kitchen.HelperCook(g, it, h.name)) continue;
                    h.workLane = lane;
                    h.prep = HelperPrepSeconds;
                    if (h.assistGroup != g.id) Notice(h, $"{h.name} vào phụ chủ quán làm món cho bàn {g.table + 1}.");
                    h.assistGroup = g.id;
                    return true;
                }
            }
            return false;
        }

        void Notice(Helper h, string text)
        {
            if (helpNoticeAt.TryGetValue(h.name, out var at) && Time.time - at < HelpNoticeGap) return;
            helpNoticeAt[h.name] = Time.time;
            gm.Toast(text);
        }

        int LaneServers(int lane) => lane == 0 ? 1 + helpers.Count(x => x.area == 0) : Mathf.Max(1, helpers.Count(x => x.area == lane));

        void StartHelperServe(Helper h, CustomerGroup g)
        {
            serveQueue.Remove(g.id);
            g.serving = true;
            h.serveGroup = g.id;
            h.table = g.table;
            h.t = 0;
            h.returning = false;
            h.hand = 0;
            h.dur = WalkToTable(g.table);
            h.tray.Clear();
            h.tray.AddRange(g.items.Select(i => i.recipe));
            gm.MarkDirty();
        }

        public int CounterSlot(Helper h) => helpers.IndexOf(h);

        public bool HelperServing(CustomerGroup g) => helpers.Any(h => h.serveGroup == g.id);

        static float WalkToTable(int t) => 0.8f + 2.2f * WorldScreens * Mathf.Abs(TableX(t) - CounterX) + (TableY(t) > 0.2f ? 0.18f : 0.53f);

        public CustomerGroup Find(int id) => groups.FirstOrDefault(g => g.id == id);

        public IEnumerable<CustomerGroup> WaitingInOrder(StationType station) =>
            groups.Where(g => g.Arrived && !g.serving && !g.eating && g.items.Any(i => i.status == ItemStatus.Waiting && i.recipe.Station == station))
                  .OrderBy(g => g.arrivedSeq);

        public CustomerGroup FirstWaitingBefore(CustomerGroup g, StationType station) =>
            WaitingInOrder(station).FirstOrDefault(x => x.arrivedSeq < g.arrivedSeq && (!Covered(x) || Lane(x) == Lane(g)) && ClaimedBy(x) == null);

        public CustomerGroup FirstMakeableBefore(CustomerGroup g, StationType station) =>
            WaitingInOrder(station).FirstOrDefault(x => x.arrivedSeq < g.arrivedSeq && (!Covered(x) || Lane(x) == Lane(g)) && ClaimedBy(x) == null &&
                x.items.Any(i => i.status == ItemStatus.Waiting && i.recipe.Station == station && gm.Kitchen.CanMakeNow(i)));
        public CustomerGroup AtTable(int t) => groups.FirstOrDefault(g => g.table == t);

        public OrderItem FindItem(int groupId, int uid, out CustomerGroup group)
        {
            group = Find(groupId);
            return group?.items.FirstOrDefault(i => i.uid == uid);
        }

        public static float CapacityFactor(GameState s)
        {
            float tables = 0.025f * Mathf.Max(0, s.tables - GameState.StartTables);
            return 1f + tables + 0.25f * s.helpers.Count;
        }

        public const float SlowPace = 0.8f, FastPace = 2.5f, TopPace = 2.7f, FreePace = 2.8f;

        static float ChapterPace(GameState s)
        {
            if (s.finished) return FreePace;
            if (s.chapter <= 10) return SlowPace;
            if (s.chapter <= 30) return SlowPace * Mathf.Pow(FastPace / SlowPace, (s.chapter - 10) / 21f);
            return Mathf.Lerp(FastPace, TopPace, (s.chapter - 31) / 19f);
        }

        static float StarFactor(GameState s) => Mathf.Lerp(0.6f, 1.3f, Mathf.InverseLerp(1f, 5f, s.reputation));

        public const float OpeningDayRate = 1.3f;

        static float OpeningDayFactor(GameState s) => s.day == 1 && !s.finished ? OpeningDayRate : 1f;

        float RushFactor(float clockMin)
        {
            if (clockMin >= 11 * 60 && clockMin < 13 * 60) return 1.7f;
            if (clockMin >= 17.5f * 60 && clockMin < 19.5f * 60) return 1.6f;
            if (clockMin >= 14 * 60 && clockMin < 16.5f * 60) return 0.6f;
            return 1f;
        }

        public void Tick(float dt, bool allowSpawn)
        {
            if (allowSpawn)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0 && gm.TutorialCalm && (groups.Count > 0 || !gm.TutorialSpawnAllowed)) spawnTimer = 1f;
                else if (spawnTimer <= 0)
                {
                    TrySpawn();
                    var s = gm.State;
                    float price = s.priceAdjust < 0 ? 1.2f : s.priceAdjust > 0 ? 0.8f : 1f;
                    float rate = ChapterPace(s) * gm.Chapter.customerRate * StarFactor(s) * CapacityFactor(s) * price * s.CustomerMul * RushFactor(gm.ClockMinutes) * OpeningDayFactor(s);
                    spawnTimer = 13f / Mathf.Max(0.2f, rate) * Random.Range(0.7f, 1.3f);
                }
            }

            TickWaiters(dt);
            TickHelpers(dt);

            for (int i = leavers.Count - 1; i >= 0; i--)
            {
                leavers[i].t += dt;
                if (leavers[i].t >= leavers[i].total) { leavers.RemoveAt(i); gm.MarkDirty(); }
            }

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                var g = groups[i];
                if (g.eating)
                {
                    g.eatTimer -= dt;
                    if (g.eatTimer <= 0) { Pay(g); Depart(g, false); }
                    continue;
                }
                if (g.walkIn > 0)
                {
                    g.walkIn -= dt;
                    if (g.walkIn <= 0)
                    {
                        g.walkIn = 0;
                        g.arrivedSeq = ++arrivalCounter;
                        gm.Chat(g.name, g.looks[0], g.orderLine);
                        if (gm.State.priceAdjust != 0 && Random.value < PriceCommentChance)
                            gm.Chat(g.name, g.looks[0], gm.State.priceAdjust > 0
                                ? PriceUpLines[Random.Range(0, PriceUpLines.Length)]
                                : PriceDownLines[Random.Range(0, PriceDownLines.Length)]);
                        Sfx.Play("order");
                        gm.MarkDirty();
                    }
                    continue;
                }
                g.patience -= dt * (!g.serving && !g.AllReady && ClaimedBy(g) == null && !OwnerWorking(g) && EarlierPending(g) ? QueueDrain : 1f);
                if (g.patience <= 0) Leave(g, g.serving ? "chờ mang món ra lâu quá, bỏ về!" : "chờ lâu quá, bỏ về!");
            }
        }

        void TrySpawn()
        {
            var s = gm.State;
            var foods = GameDB.UnlockedRecipes(s.chapter).Where(r => !r.IsDrink && !s.disabledRecipes.Contains(r.id)).ToList();
            var drinks = GameDB.UnlockedRecipes(s.chapter).Where(r => r.IsDrink && !s.disabledRecipes.Contains(r.id)).ToList();
            if (foods.Count == 0) return;

            int table = PickTable(s);
            if (table < 0)
            {
                s.Today.groupsNoSeat++;
                gm.MarkDirty();
                return;
            }

            float r01 = Random.value;
            int size = r01 < 0.30f ? 1 : r01 < 0.68f ? 2 : r01 < 0.88f ? 3 : 4;
            var g = new CustomerGroup
            {
                id = nextId++, table = table, size = size,
            };
            g.looks = new int[size];
            for (int p = 0; p < size; p++) g.looks[p] = Random.Range(0, CustomerLooks);
            g.name = size == 1 ? NameFor(g.looks[0]) : $"{NameFor(g.looks[0])} +{size - 1}";
            for (int p = 0; p < size; p++)
            {
                g.items.Add(MakeItem(PickFood(foods)));
                if (drinks.Count > 0 && Random.value < DrinkChance) g.items.Add(MakeItem(PickDrink(drinks)));
            }
            float staffBonus = 1f + 0.15f * s.ActiveStaff;
            float tutorial = gm.TutorialBasics ? 2f : 1f;
            float own = 44f + g.items.Sum(i => (i.recipe.cookSeconds + 10f) * QuickFillPatience(i.recipe));
            float groupMul = 1f + 0.08f * (size - 1);
            float crowd = size >= 2 ? 2.5f * groups.Where(o => !o.eating).Sum(o => o.size) : 0f;
            g.maxPatience = own * gm.Chapter.patienceMul * staffBonus * tutorial * groupMul + QueueAllowance(g) + crowd;
            g.patience = g.maxPatience;
            g.fromSide = Random.value < 0.5f ? -1 : 1;
            float screens = Mathf.Abs(TableX(table) - CustomerStartX(table, g.fromSide)) * WorldScreens;
            g.walkTotal = g.walkIn = 1.2f + 2.1f * screens;
            var order = string.Join(", ", g.items.GroupBy(i => i.Label).Select(x => $"{x.Count()} {x.Key.Replace(" • Cấp", " cấp").Replace(" • nước", ", nước")}"));
            g.orderLine = string.Format(OrderLines[Random.Range(0, OrderLines.Length)], order);
            groups.Add(g);
            Sfx.Play("door");
            gm.MarkDirty();
        }

        int PickTable(GameState s)
        {
            int bestArea = -1;
            float bestRatio = 2f;
            for (int a = 0; a < Areas; a++)
            {
                int owned = 0, used = 0;
                for (int t = a * TablesPerArea; t < (a + 1) * TablesPerArea; t++)
                {
                    if (!s.HasTable(t)) continue;
                    owned++;
                    if (AtTable(t) != null) used++;
                }
                if (owned == 0 || used >= owned) continue;
                float ratio = used / (float)owned + Random.value * 0.05f;
                if (ratio < bestRatio) { bestRatio = ratio; bestArea = a; }
            }
            if (bestArea < 0) return -1;
            var free = new List<int>();
            for (int t = bestArea * TablesPerArea; t < (bestArea + 1) * TablesPerArea; t++)
                if (s.HasTable(t) && AtTable(t) == null) free.Add(t);
            return free[Random.Range(0, free.Count)];
        }

        float QuickFillPatience(RecipeDef r)
        {
            var s = gm.State;
            if (s.GetMastery(r.id) < KitchenSystem.MasteryForQuickFill) return 1f;
            if (s.finished || s.chapter > 30) return 0.65f;
            if (s.chapter > 15) return 0.72f;
            if (s.chapter > 5) return 0.8f;
            return 0.9f;
        }

        const float QueueDrain = 0.5f;

        int Lane(CustomerGroup g) => Covered(g) ? AreaOf(g.table) : 0;

        bool EarlierPending(CustomerGroup g)
        {
            int lane = Lane(g);
            foreach (var o in groups)
                if (o != g && o.Arrived && !o.eating && o.arrivedSeq < g.arrivedSeq && Lane(o) == lane) return true;
            return false;
        }

        float QueueAllowance(CustomerGroup g)
        {
            var s = gm.State;
            int lane = Lane(g);
            bool owner = lane == 0;
            float stoveMul = gm.Kitchen.stations.Where(x => x.type == StationType.Stove).Select(x => x.SpeedMul).DefaultIfEmpty(1f).Average();
            float barMul = gm.Kitchen.stations.Where(x => x.type == StationType.DrinkBar).Select(x => x.SpeedMul).DefaultIfEmpty(1f).Average();
            int carriers = AreaOf(g.table) == 0 ? 1 : Mathf.Max(1, waiters.Count);
            float wait = 2f * WalkToTable(g.table) + HandOverSeconds, others = 0f;
            foreach (var other in groups)
            {
                if (other == g || other.eating || Lane(other) != lane) continue;
                others += (2f * WalkToTable(other.table) + HandOverSeconds) / (owner ? carriers : 1);
                foreach (var it in other.items)
                {
                    if (it.status == ItemStatus.Ready) continue;
                    bool drink = it.recipe.IsDrink;
                    float cook = it.recipe.cookSeconds * (drink ? barMul : stoveMul) / Mathf.Max(1, drink ? s.DrinkSlots : s.stoves);
                    if (it.status == ItemStatus.Waiting) others += (owner ? (drink ? 5f : 10f) : 5f) + cook;
                    else others += cook * 0.5f;
                }
            }
            wait += others / LaneServers(lane);
            return Mathf.Min(300f, wait * 0.75f);
        }

        OrderItem MakeItem(RecipeDef r)
        {
            var it = new OrderItem { uid = nextUid++, recipe = r };
            if (r.HasBroth)
            {
                it.broth = r.broth == "any" ? PickBroth() : r.broth;
                if (it.broth == GameDB.BrothThai) thaiToday++; else kimchiToday++;
            }
            if (r.spicy)
            {
                int total = SpiceWeights.Sum(), roll = Random.Range(0, total);
                for (int lv = 0; lv < SpiceWeights.Length; lv++)
                {
                    roll -= SpiceWeights[lv];
                    if (roll < 0) { it.spice = lv; break; }
                }
            }
            var s = gm.State;
            if (!gm.TutorialBasics && s.day >= 2 && Random.value < CustomOrderChance)
            {
                var tops = GameDB.DailyToppingsFor(r, s.day, s.chapter);
                var omittable = GameDB.Omittable(r);
                bool doExtras = tops.Count > 0 && (omittable.Count == 0 || Random.value < 0.6f);
                if (doExtras)
                {
                    it.extras.Add(tops[Random.Range(0, tops.Count)].id);
                    if (s.chapter >= 5 && Random.value < 0.2f) it.extras.Add(tops[Random.Range(0, tops.Count)].id);
                }
                else if (omittable.Count > 0)
                    it.omits.Add(omittable[Random.Range(0, omittable.Count)]);
            }
            return it;
        }

        string PickBroth()
        {
            float p = Mathf.Clamp(ThaiChance() + 0.15f * (kimchiToday - thaiToday), 0.05f, 0.95f);
            return Random.value < p ? GameDB.BrothThai : GameDB.BrothKimchi;
        }

        public float ThaiChance()
        {
            var s = gm.State;
            float kimchi = 0, thai = 0, any = 0;
            foreach (var r in GameDB.UnlockedRecipes(s.chapter))
            {
                if (r.IsDrink || !r.HasBroth || s.disabledRecipes.Contains(r.id)) continue;
                float w = FoodWeight(r);
                if (r.broth == "any") any += w;
                else if (r.broth == GameDB.BrothThai) thai += w;
                else kimchi += w;
            }
            if (any <= 0) return 0.5f;
            return Mathf.Clamp((0.5f * (kimchi + thai + any) - thai) / any, 0.25f, 0.85f);
        }

        static readonly string[] StaleLines = { "Ngon đấy, nhưng thịt rau không còn tươi lắm.", "Vị ổn, mà nguyên liệu chưa tươi, chưa phải tuyệt hảo.", "Ăn được, nhưng topping có vẻ để hơi lâu rồi." };
        static readonly string[] LateLines = { "Ngon, nhưng lên món chậm hơn mình mong một chút.", "Món ổn, chỉ là phải chờ hơi lâu.", "Vị được, lần sau ra món nhanh hơn chút nha." };
        static readonly string[] SlowLines = { "Chờ lâu quá, đói mốc meo mới có món.", "Món tạm được, nhưng phục vụ chậm.", "Ngồi chờ mãi, lần sau chắc phải cân nhắc." };
        static readonly string[] VerySlowLines = { "Chờ muốn xỉu, món ra thì nguội cả rồi.", "Phục vụ quá chậm, chắc không quay lại.", "Đợi lâu kinh khủng, mất cả hứng ăn." };
        static readonly string[] VeryStaleLines = { "Nguyên liệu không tươi, ăn thấy rõ luôn.", "Thịt có mùi để lâu rồi, không ổn chút nào.", "Rau héo, topping cũ, hơi thất vọng." };
        static readonly string[] SlowStaleLines = { "Đã chờ lâu mà đồ ăn còn không tươi...", "Chậm, lại còn nguyên liệu kém tươi, thất vọng ghê." };
        static readonly string[] SpiceLines = { "Ơ, em gọi cấp cay khác mà... làm sai rồi.", "Không đúng độ cay em dặn, mất cả ngon." };
        const float LateGreenSeconds = 10f, StaleMildChance = 0.55f;
        static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];

        void AddReview(CustomerGroup g, int stars, string text)
        {
            gm.State.Today.reviews.Add(new Review { table = g.table + 1, name = g.name, stars = stars, text = text, clock = gm.ClockMinutes });
        }

        static RecipeDef PickFood(List<RecipeDef> foods)
        {
            float total = foods.Sum(FoodWeight);
            float roll = Random.value * total;
            foreach (var f in foods)
            {
                roll -= FoodWeight(f);
                if (roll <= 0) return f;
            }
            return foods[foods.Count - 1];
        }

        static RecipeDef PickDrink(List<RecipeDef> drinks)
        {
            float total = drinks.Sum(DrinkWeight);
            float roll = Random.value * total;
            foreach (var d in drinks)
            {
                roll -= DrinkWeight(d);
                if (roll <= 0) return d;
            }
            return drinks[drinks.Count - 1];
        }

        public void Serve(CustomerGroup g)
        {
            if (g == null || !g.AllReady || g.serving) return;
            g.serving = true;
            serveQueue.Add(g.id);
            EnsureWaiters();
            Dispatch();
            gm.MarkDirty();
        }

        void Dispatch()
        {
            serveQueue.RemoveAll(id => Find(id) == null);
            var anh = waiters.FirstOrDefault(w => w.id == "anh");
            var vy = waiters.FirstOrDefault(w => w.id != "anh");
            if (vy != null) DispatchTo(vy, g => AreaOf(g.table) > 0);
            if (anh != null) DispatchTo(anh, g => true);
            if (vy != null) DispatchTo(vy, g => true);
        }

        void DispatchTo(Waiter w, Func<CustomerGroup, bool> ok)
        {
            if (w.phase != WaiterPhase.Idle) return;
            int k = serveQueue.FindIndex(id => ok(Find(id)));
            if (k < 0) return;
            var g = Find(serveQueue[k]);
            serveQueue.RemoveAt(k);
            w.phase = WaiterPhase.Going;
            w.groupId = g.id;
            w.table = g.table;
            w.t = 0;
            w.dur = WalkToTable(g.table);
            w.tray.Clear();
            w.tray.AddRange(g.items.Select(i => i.recipe));
        }

        void TickWaiters(float dt)
        {
            foreach (var w in waiters)
            {
                if (w.phase == WaiterPhase.Idle) continue;
                w.t += dt;
                if (w.t < w.dur) continue;
                switch (w.phase)
                {
                    case WaiterPhase.Going:
                        w.phase = WaiterPhase.Handing;
                        w.t = 0; w.dur = HandOverSeconds;
                        w.tray.Clear();
                        break;
                    case WaiterPhase.Handing:
                        var g = Find(w.groupId);
                        if (g != null) Deliver(g);
                        w.phase = WaiterPhase.Returning;
                        w.t = 0; w.dur = WalkToTable(w.table);
                        break;
                    case WaiterPhase.Returning:
                        w.phase = WaiterPhase.Idle;
                        w.groupId = -1;
                        Dispatch();
                        break;
                }
                gm.MarkDirty();
            }
        }

        void Pay(CustomerGroup g)
        {
            if (g == null || !g.AllReady) return;
            var s = gm.State;
            var today = s.Today;
            long bill = 0, extra = 0;
            int wrongSpice = 0;
            foreach (var it in g.items)
            {
                bill += (long)Math.Round(it.BasePrice * s.PriceFactor / 1000.0) * 1000;
                extra += (long)Math.Round(it.ExtrasPrice * s.PriceFactor / 1000.0) * 1000;
                if (it.recipe.IsDrink) today.drinksSold++; else today.dishesSold++;
                today.AddSold(it.recipe.id);
                if (!it.spiceOk) wrongSpice++;
            }
            today.spiceWrong += wrongSpice;
            float ratio = g.PatienceRatio;
            int timeStars = ratio > 0.5f ? (g.patience - 0.5f * g.maxPatience < LateGreenSeconds ? 4 : 5) : ratio > 0.25f ? 3 : 2;
            bool stale = g.items.Any(i => i.stale);
            int foodPenalty = stale ? (Random.value < StaleMildChance ? 1 : 2) : 0;
            int rating = Mathf.Clamp(timeStars - foodPenalty - Math.Min(2, wrongSpice), 1, 5);
            string comment =
                wrongSpice > 0 ? Pick(SpiceLines) :
                rating >= 5 ? Pick(HappyLines) :
                stale && timeStars >= 5 ? Pick(foodPenalty >= 2 ? VeryStaleLines : StaleLines) :
                stale ? Pick(SlowStaleLines) :
                timeStars == 4 ? Pick(LateLines) :
                timeStars == 3 ? Pick(SlowLines) : Pick(VerySlowLines);
            AddReview(g, rating, comment);

            gm.AddLedger(Finance.CatRevenue, $"Bàn {g.table + 1}: {g.name}", bill);
            today.groupsServed++;
            Sfx.Play("cash");
            Rate(rating);
            gm.Chat(g.name, g.looks[0], $"<color=#7CE07C>(Bàn {g.table + 1} ăn xong, trả {Finance.Money(bill)}" +
                (extra > 0 ? $", gồm {Finance.Money(extra)} gọi thêm" : "") + $" • {rating}/5 sao)</color> {comment}");
            gm.MarkDirty();
        }

        void Deliver(CustomerGroup g)
        {
            if (g == null || !g.AllReady || g.eating) return;
            g.eating = true;
            g.eatTimer = CustomerGroup.EatSeconds;
            gm.MarkDirty();
        }

        void Depart(CustomerGroup g, bool angry)
        {
            Remove(g);
            int side = g.fromSide != 0 ? g.fromSide : (Random.value < 0.5f ? -1 : 1);
            float screens = Mathf.Abs(TableX(g.table) - LeaverExitX(g.table, side)) * WorldScreens;
            leavers.Add(new Leaver
            {
                table = g.table, size = g.size, looks = g.looks, side = side, angry = angry,
                total = 1.2f + 2.1f * screens,
            });
            gm.MarkDirty();
        }

        public void SendHome(CustomerGroup g, string reason)
        {
            if (g.eating) { Pay(g); Depart(g, false); }
            else Leave(g, reason);
        }

        public void Leave(CustomerGroup g, string reason)
        {
            serveQueue.Remove(g.id);
            var w = WaiterFor(g.id);
            if (w != null && w.phase != WaiterPhase.Returning)
            {
                w.t = w.phase == WaiterPhase.Going ? w.dur - w.t : 0;
                w.dur = w.phase == WaiterPhase.Going ? w.dur : WalkToTable(w.table);
                w.phase = WaiterPhase.Returning;
                w.tray.Clear();
            }
            foreach (var h in helpers)
                if (h.serveGroup == g.id)
                {
                    h.serveGroup = -1;
                    h.returning = true;
                    h.t = Mathf.Max(0f, h.dur - h.t);
                    h.tray.Clear();
                }
            var s = gm.State;
            s.Today.groupsLeft++;
            Sfx.Play("angry");
            Rate(1);
            string angry = AngryLines[Random.Range(0, AngryLines.Length)];
            AddReview(g, 1, $"({reason}) {angry}");
            gm.Chat(g.name, g.looks[0], $"<color=#FF8A80>(Bàn {g.table + 1} {reason})</color> " + angry);
            Depart(g, true);
        }

        void Rate(int stars)
        {
            var s = gm.State;
            s.Today.ratingSum += stars;
            s.Today.ratingCount++;
            s.reputation = Mathf.Clamp(s.reputation * 0.95f + stars * 0.05f, 1f, 5f);
        }

        void Remove(CustomerGroup g)
        {
            groups.Remove(g);
            gm.Kitchen.OnGroupRemoved(g);
            gm.MarkDirty();
        }
    }

    public class Station
    {
        public StationType type;
        public int index;
        public int groupId = -1, itemUid = -1;
        public string label;
        public float timeLeft, total;
        public bool Busy => total > 0;
        public int tier, level;
        public int machine = -1, slot, slots = 1;
        public bool IsMachine => type == StationType.DrinkBar;
        public float SpeedMul => tier == 0 ? GameManager.OldUpgradeMul[Mathf.Clamp(level, 0, GameManager.MaxOldUpgrades)]
            : type == StationType.Stove ? GameManager.StoveTiers[Mathf.Clamp(tier, 0, GameManager.StoveTiers.Length - 1)].mul
            : GameManager.BarTiers[Mathf.Clamp(tier, 0, GameManager.BarTiers.Length - 1)].mul;
        string OldMark => tier != 0 ? "" : level >= 2 ? " (cũ++)" : level == 1 ? " (cũ+)" : "";
        public string Name => type == StationType.Stove
            ? $"Bếp {index + 1}{(tier == 1 ? " (mới)" : tier == 2 ? " (CN)" : tier == 3 ? " (từ)" : OldMark)}"
            : $"Máy pha {machine + 1}{(slots > 1 ? (slot == 0 ? "A" : "B") : OldMark)}";
    }

    public class KitchenSystem
    {
        public const int MasteryForQuickFill = 3;
        readonly GameManager gm;
        public readonly List<Station> stations = new List<Station>();
        public readonly List<string> pot = new List<string>();
        public int targetGroup = -1, targetItem = -1;

        public KitchenSystem(GameManager gm) { this.gm = gm; }

        public void RebuildStations()
        {
            var s = gm.State;
            var old = stations.ToList();
            stations.Clear();
            for (int i = 0; i < s.stoves; i++)
            {
                var st = old.FirstOrDefault(o => o.type == StationType.Stove && o.index == i) ?? new Station { type = StationType.Stove, index = i };
                st.tier = gm.StoveTier(i);
                st.level = gm.StoveLevel(i);
                stations.Add(st);
            }
            int n = 0;
            for (int m = 0; m < s.barTiers.Count; m++)
            {
                int tier = gm.BarTier(m), slots = GameManager.BarTiers[tier].slots;
                for (int k = 0; k < slots; k++)
                {
                    int mm = m, kk = k;
                    var st = old.FirstOrDefault(o => o.type == StationType.DrinkBar && o.machine == mm && o.slot == kk)
                             ?? new Station { type = StationType.DrinkBar, machine = m, slot = k };
                    st.index = n++;
                    st.tier = tier;
                    st.level = gm.BarLevel(m);
                    st.slots = slots;
                    stations.Add(st);
                }
            }
        }

        public bool IsBroken(Station st) => st.type == StationType.Stove && st.index == 0 && gm.State.StoveBroken;

        public Station FreeStation(StationType type) =>
            stations.Where(x => x.type == type && !x.Busy && !IsBroken(x)).OrderBy(x => x.SpeedMul).FirstOrDefault();

        public bool CanMakeNow(OrderItem it)
        {
            if (it == null) return false;
            var s = gm.State;
            var need = it.Needs();
            if (it.recipe.spicy && it.spice > 0) need[GameDB.Chili] = it.spice;
            foreach (var kv in need)
                if (Inventory.Count(s, kv.Key) - PotCount(kv.Key) < kv.Value) return false;
            return true;
        }

        public bool CanHelperCookNow(OrderItem it)
        {
            if (it == null || it.status != ItemStatus.Waiting) return false;
            if (FreeStation(it.recipe.Station) == null) return false;
            var mine = Target;
            if (mine != null && mine != it && mine.status == ItemStatus.Waiting && pot.Count > 0 && mine.recipe.Station == it.recipe.Station &&
                stations.Count(x => x.type == it.recipe.Station && !x.Busy && !IsBroken(x)) <= 1) return false;
            return CanMakeNow(it);
        }

        public bool HelperCook(CustomerGroup g, OrderItem it, string who)
        {
            if (!CanHelperCookNow(it)) return false;
            var st = FreeStation(it.recipe.Station);
            var s = gm.State;
            var need = it.Needs();
            if (it.recipe.spicy && it.spice > 0) need[GameDB.Chili] = it.spice;
            long cost = 0;
            float freshSum = 0, freshMin = 1f;
            int freshN = 0;
            bool stale = false;
            foreach (var kv in need)
            {
                cost += Inventory.Consume(s, kv.Key, kv.Value, out float f, out bool old);
                if (kv.Key == GameDB.Chili) continue;
                freshSum += f;
                freshMin = Mathf.Min(freshMin, f);
                freshN++;
                stale |= old;
            }
            s.Today.cogs += cost;
            it.quality = freshN == 0 ? 1f : 0.5f * (freshSum / freshN) + 0.5f * freshMin;
            it.stale = stale;
            it.spiceOk = true;
            it.status = ItemStatus.Cooking;
            st.groupId = g.id;
            st.itemUid = it.uid;
            st.label = $"{it.Label} (Bàn {g.table + 1} • {who})";
            it.cook = who;
            st.total = st.timeLeft = it.recipe.cookSeconds * st.SpeedMul;
            gm.MarkDirty();
            return true;
        }

        public void ResetDay()
        {
            foreach (var st in stations) { st.total = 0; st.timeLeft = 0; st.groupId = -1; st.itemUid = -1; }
            pot.Clear();
            targetGroup = targetItem = -1;
        }

        public OrderItem Target => targetGroup < 0 ? null : gm.Customers.FindItem(targetGroup, targetItem, out _);
        public CustomerGroup TargetGroup => gm.Customers.Find(targetGroup);

        public void Select(CustomerGroup g, OrderItem it)
        {
            if (!g.Arrived || it.status != ItemStatus.Waiting) return;
            var by = gm.Customers.ClaimedBy(g);
            if (by != null)
            {
                Sfx.Play("error");
                gm.Toast($"{by.name} đang làm cho bàn {g.table + 1} rồi, chị làm cho khách khác nhé.");
                return;
            }
            var first = gm.Customers.FirstMakeableBefore(g, it.recipe.Station);
            if (first != null)
            {
                Sfx.Play("error");
                gm.Toast($"Khách bàn {first.table + 1} đến trước mà chưa có {(it.recipe.IsDrink ? "đồ uống" : "món")} — hãy làm cho bàn {first.table + 1} trước!");
                return;
            }
            if (targetGroup != g.id || targetItem != it.uid) pot.Clear();
            targetGroup = g.id;
            targetItem = it.uid;
            gm.MarkDirty();
        }

        int PotCount(string ing) => pot.Count(p => p == ing);

        public void AddToPot(string ing)
        {
            if (Target == null) { gm.Toast("Hãy chọn một món trong đơn của khách trước."); return; }
            if (Inventory.Count(gm.State, ing) - PotCount(ing) <= 0)
            {
                Sfx.Play("error");
                gm.Toast($"Hết {GameDB.GetIng(ing).name}! Vào \"Nhập hàng\" để mua.");
                return;
            }
            pot.Add(ing);
            Sfx.Play("pop");
            gm.MarkDirty();
        }

        public void RemoveFromPot(string ing)
        {
            int idx = pot.LastIndexOf(ing);
            if (idx >= 0) { pot.RemoveAt(idx); gm.MarkDirty(); }
        }

        public void ClearPot() { pot.Clear(); gm.MarkDirty(); }

        public bool Mastered(OrderItem it) => it != null && gm.State.GetMastery(it.recipe.id) >= MasteryForQuickFill;
        public bool CanQuickFill(OrderItem it) => Mastered(it) && !it.IsCustom;

        public void QuickFill()
        {
            var it = Target;
            if (Mastered(it) && it.IsCustom)
            {
                Sfx.Play("error");
                gm.Toast("Khách có yêu cầu riêng, không ghép nhanh được — tự cho từng nguyên liệu nhé!");
                return;
            }
            if (!CanQuickFill(it)) return;
            pot.Clear();
            var need = new List<string>();
            foreach (var kv in it.Needs()) for (int k = 0; k < kv.Value; k++) need.Add(kv.Key);
            if (it.recipe.spicy) for (int k = 0; k < it.spice; k++) need.Add(GameDB.Chili);
            foreach (var g in need.GroupBy(x => x))
            {
                if (Inventory.Count(gm.State, g.Key) < g.Count())
                {
                    gm.Toast($"Không đủ {GameDB.GetIng(g.Key).name} để ghép nhanh.");
                    gm.MarkDirty();
                    return;
                }
            }
            pot.AddRange(need);
            Sfx.Play("pop");
            gm.MarkDirty();
        }

        public void Cook()
        {
            var it = Target;
            var g = TargetGroup;
            if (it == null || g == null) { gm.Toast("Chưa chọn món để nấu."); return; }
            if (it.status != ItemStatus.Waiting) return;
            if (pot.Count == 0) { gm.Toast("Nồi đang trống!"); return; }
            var st = FreeStation(it.recipe.Station);
            if (st == null) { gm.Toast(it.recipe.IsDrink ? "Máy pha đang bận hết!" : "Tất cả bếp đang bận!"); return; }

            var s = gm.State;
            long cost = 0;
            float freshSum = 0, freshMin = 1f;
            int freshN = 0;
            bool stale = false;
            foreach (var grp in pot.GroupBy(x => x))
            {
                cost += Inventory.Consume(s, grp.Key, grp.Count(), out float f, out bool old);
                if (grp.Key == GameDB.Chili) continue;
                freshSum += f;
                freshMin = Mathf.Min(freshMin, f);
                freshN++;
                stale |= old;
            }
            it.quality = freshN == 0 ? 1f : 0.5f * (freshSum / freshN) + 0.5f * freshMin;
            it.stale = stale;
            s.Today.cogs += cost;

            var want = it.Needs();
            var have = pot.Where(p => p != GameDB.Chili).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
            bool match = want.Count == have.Count && want.All(kv => have.TryGetValue(kv.Key, out var h) && h == kv.Value);
            int chili = pot.Count(p => p == GameDB.Chili);
            pot.Clear();

            if (!match)
            {
                s.Today.ruined++;
                Sfx.Play("error");
                s.Today.wasteCost += cost;
                gm.Toast($"Sai công thức! {it.recipe.name} bị hỏng, mất {Finance.Money(cost)} nguyên liệu." + (it.IsCustom ? " (Khách dặn:" + it.OmitText + it.ExtrasText + ")" : ""));
                gm.MarkDirty();
                return;
            }

            it.spiceOk = it.recipe.spicy ? chili == it.spice : chili == 0;
            it.status = ItemStatus.Cooking;
            Sfx.Play(it.recipe.IsDrink ? "pour" : "sizzle");
            st.groupId = g.id;
            st.itemUid = it.uid;
            st.label = $"{it.Label} (Bàn {g.table + 1})";
            it.cook = CustomerSystem.OwnerCook;
            st.total = st.timeLeft = it.recipe.cookSeconds * st.SpeedMul;
            targetGroup = targetItem = -1;
            gm.MarkDirty();
        }

        public void Tick(float dt)
        {
            foreach (var st in stations)
            {
                if (!st.Busy) continue;
                st.timeLeft -= dt;
                if (st.timeLeft > 0) continue;
                var it = gm.Customers.FindItem(st.groupId, st.itemUid, out var g);
                if (it != null)
                {
                    it.status = ItemStatus.Ready;
                    Sfx.Play("ding");
                    gm.State.AddMastery(it.recipe.id);
                    if (gm.State.GetMastery(it.recipe.id) == MasteryForQuickFill)
                        gm.Toast($"Đã thuộc công thức \"{it.recipe.name}\" — mở khóa GHÉP NHANH!");
                }
                st.total = st.timeLeft = 0;
                st.groupId = st.itemUid = -1;
                gm.MarkDirty();
            }
        }

        public void OnGroupRemoved(CustomerGroup g)
        {
            if (targetGroup == g.id) { targetGroup = targetItem = -1; pot.Clear(); }
            foreach (var st in stations)
            {
                if (st.groupId != g.id) continue;
                st.groupId = st.itemUid = -1;
                st.label += " — khách đã về";
            }
        }
    }
}
