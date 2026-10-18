using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace MiCay
{
    public enum GamePhase { Title, Prep, Open, Closing, DayEnd }

    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        public const float DayLengthSeconds = 360f;
        public const float OpenMin = 9 * 60, CloseMin = 23 * 60, LastOrderMin = 22.5f * 60;
        public const string ShopName = "TIỆM MỲ CAY CỦA CÚN IÊU";
        public const long StartCapital = 20_000_000;
        public const long PrisonLoan = 15_000_000;
        public const int PrisonLoanDays = 30;

        public GameState State { get; private set; }
        public ChapterDef Chapter => State.finished ? StoryDB.FreeMode : StoryDB.GetChapter(State.chapter);
        public Market Market { get; private set; }
        public CustomerSystem Customers { get; private set; }
        public KitchenSystem Kitchen { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.Title;
        public float DayTime { get; private set; }
        public float ClockMinutes => OpenMin + DayTime / DayLengthSeconds * (CloseMin - OpenMin);
        public int Speed { get; set; } = 1;
        public bool UserPaused { get; set; }
        public int ModalCount { get; set; }
        public int PausingModals { get; set; }
        public bool DialogueActive { get; private set; }
        public bool IsPaused => UserPaused || PausingModals > 0 || DialogueActive || TutorialPausing;
        public readonly List<string> MorningNotes = new List<string>();

        public event Action Changed;
        public event Action<string> ToastRaised;
        bool dirty;
        UI.MainUI ui;
        readonly System.Random rng = new System.Random();

        static string SavePath => Path.Combine(Application.persistentDataPath, "micay_save.json");
        static string SnapshotPath => Path.Combine(Application.persistentDataPath, "micay_chapter_start.json");
        public static bool HasSave => File.Exists(SavePath);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("MiCayGame");
            DontDestroyOnLoad(go);
            go.AddComponent<GameManager>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = new Color(0.98f, 0.93f, 0.84f);
            }
            EnsureEventSystem();
            Sfx.Init(gameObject);
            Sfx.PlayMusic("bgm_main");

            State = new GameState();
            Market = new Market(this);
            Customers = new CustomerSystem(this);
            Kitchen = new KitchenSystem(this);

            ui = gameObject.AddComponent<UI.MainUI>();
            ui.Build(this);
            ui.ShowTitle();
        }

        static void EnsureEventSystem()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                var module = go.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
                DontDestroyOnLoad(go);
                es = go.GetComponent<EventSystem>();
            }
            es.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt(Screen.dpi / 12f));
        }

        public void MarkDirty() => dirty = true;

        public class ChatLine
        {
            public string name, text;
            public int look = -1;
            public bool notice;
        }

        public readonly List<ChatLine> ChatLog = new List<ChatLine>();

        public void Chat(string name, int look, string text) => AddChat(new ChatLine { name = name, look = look, text = text });

        public void Toast(string msg)
        {
            AddChat(new ChatLine { name = "Thông báo", text = msg, notice = true });
            ToastRaised?.Invoke(msg);
        }

        void AddChat(ChatLine line)
        {
            ChatLog.Add(line);
            if (ChatLog.Count > 12) ChatLog.RemoveAt(0);
            MarkDirty();
        }

        public void AddLedger(string cat, string note, long amount)
        {
            State.cash += amount;
            State.ledger.Add(new LedgerEntry { day = State.day, cat = cat, note = note, amount = amount });
            MarkDirty();
        }

        public void PlayDialogue(IList<DialogueLine> lines, Action onDone)
        {
            if (lines == null || lines.Count == 0) { onDone?.Invoke(); return; }
            DialogueActive = true;
            ui.Dialogue.Play(lines, () =>
            {
                DialogueActive = false;
                onDone?.Invoke();
            });
        }

        static DialogueLine L(string who, string text) => new DialogueLine(who, text);

        class PendingChoice
        {
            public string title, text, yes, no;
            public Action<List<DialogueLine>> onYes, onNo;
        }

        readonly List<PendingChoice> choices = new List<PendingChoice>();

        void Choice(string title, string text, string yes, string no, Action<List<DialogueLine>> onYes, Action<List<DialogueLine>> onNo) =>
            choices.Add(new PendingChoice { title = title, text = text, yes = yes, no = no, onYes = onYes, onNo = onNo });

        void RunChoices(Action done)
        {
            if (choices.Count == 0 || PrisonPending) { choices.Clear(); done(); return; }
            var c = choices[0];
            choices.RemoveAt(0);
            void Pick(Action<List<DialogueLine>> outcome)
            {
                var lines = new List<DialogueLine>();
                outcome?.Invoke(lines);
                MarkDirty();
                PlayDialogue(lines, () => RunChoices(done));
            }
            ui.ShowChoice(c.title, c.text, c.yes, c.no, () => Pick(c.onYes), () => Pick(c.onNo));
        }

        const int StagePrep = 0, StageOpen = 1, StageEnded = 2;

        public void Save() => Save(true);

        public void Save(bool writeBackup)
        {
            State.saveSeq = Math.Max(State.saveSeq, Keychain.LoadSeq()) + 1;
            try { File.WriteAllText(SavePath, Guard.Wrap(JsonUtility.ToJson(State))); }
            catch (Exception e) { Debug.LogWarning("Không lưu được: " + e.Message); return; }
            Keychain.SaveSeq(State.saveSeq);
            if (!writeBackup) return;
            try { BackupFile.Write(ExportBackupCode(), $"Ngày {State.day}, chương {State.chapter}, tiền mặt {Finance.Money(State.cash)}"); }
            catch (Exception e) { Debug.LogWarning("Không ghi được file sao lưu: " + e.Message); }
        }

        void SaveSnapshot()
        {
            try { File.WriteAllText(SnapshotPath, Guard.Wrap(JsonUtility.ToJson(State))); }
            catch (Exception e) { Debug.LogWarning("Không lưu được mốc chương: " + e.Message); }
        }

        static GameState ReadState(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = Guard.Unwrap(File.ReadAllText(path));
                return json == null ? null : JsonUtility.FromJson<GameState>(json);
            }
            catch { return null; }
        }

        int SeqMark()
        {
            var saved = ReadState(SavePath);
            return Math.Max(Keychain.LoadSeq(), saved?.saveSeq ?? 0);
        }

        bool Migrate()
        {
            if (State.version < GameState.CurrentVersion) return false;
            if (State.tables < GameState.StartTables) State.tables = GameState.StartTables;
            if (State.stoveTiers == null) State.stoveTiers = new List<int>();
            while (State.stoveTiers.Count < State.stoves) State.stoveTiers.Add(0);
            while (State.stoveTiers.Count > State.stoves) State.stoveTiers.RemoveAt(State.stoveTiers.Count - 1);
            if (State.stoveUpgraded == null) State.stoveUpgraded = new List<int>();
            while (State.stoveUpgraded.Count < State.stoves) State.stoveUpgraded.Add(0);
            while (State.stoveUpgraded.Count > State.stoves) State.stoveUpgraded.RemoveAt(State.stoveUpgraded.Count - 1);
            State.stoveDays = Fit(State.stoveDays, State.stoves, 1);
            if (State.barTiers == null || State.barTiers.Count != State.drinkBars + State.drinkMachines)
            {
                State.barTiers = new List<int>();
                for (int i = 0; i < Math.Max(1, State.drinkBars); i++) State.barTiers.Add(0);
                for (int i = 0; i < State.drinkMachines; i++) State.barTiers.Add(1);
            }
            State.barUpgraded = Fit(State.barUpgraded, State.barTiers.Count, 0);
            State.barDays = Fit(State.barDays, State.barTiers.Count, 1);
            if (State.tableSlots == null || State.tableSlots.Count != State.tables)
            {
                State.tableSlots = new List<int>();
                for (int t = 0; t < Math.Min(State.tables, CustomerSystem.MaxTables); t++) State.tableSlots.Add(t);
            }
            State.SyncCounts();
            if (State.helpers == null) State.helpers = new List<string>();
            if (State.wages == null) State.wages = new List<WageEntry>();
            if (State.departed == null) State.departed = new List<DepartedDebtor>();
            if (State.dailyPnl == null) State.dailyPnl = new List<DayPnL>();
            if (State.helperAreas == null || State.helperAreas.Count != State.helpers.Count)
            {
                State.helperAreas = new List<int>();
                for (int i = 0; i < State.helpers.Count; i++) State.helperAreas.Add(FreeArea());
            }
            State.helperDaysWorked = Fit(State.helperDaysWorked, State.helpers.Count, 0);
            var mom = State.GetDebt("me");
            if (mom != null && mom.dueDay <= 0) mom.dueDay = 15;
            return true;
        }

        static List<long> FitLong(List<long> list, int n)
        {
            list = list ?? new List<long>();
            while (list.Count < n) list.Add(0);
            while (list.Count > n) list.RemoveAt(list.Count - 1);
            return list;
        }

        static List<int> Fit(List<int> list, int n, int fill)
        {
            list = list ?? new List<int>();
            while (list.Count < n) list.Add(fill);
            while (list.Count > n) list.RemoveAt(list.Count - 1);
            return list;
        }

        public void NewGame()
        {
            State = new GameState();
            State.debts.Add(StoryDB.MakeDebt("me", State.day));
            AddLedger(Finance.CatLoan, "Mẹ cho mượn vốn", StartCapital);
            Kitchen.RebuildStations();
            StartChapter(1);
        }

        public void ContinueGame()
        {
            var st = ReadState(SavePath);
            string err = st == null ? "không đọc được hoặc chữ ký không hợp lệ" : null;
            if (st != null)
            {
                State = st;
                if (!Migrate()) err = "bản lưu thuộc phiên bản cũ";
                else err = Guard.Validate(State);
                if (err == null && Keychain.Supported && State.saveSeq < Keychain.LoadSeq())
                    err = "dữ liệu cũ hơn tiến trình đã lưu trên máy";
            }
            if (err != null)
            {
                State = new GameState();
                Sfx.Play("error");
                ui.ShowMessage("KHÔNG MỞ ĐƯỢC DỮ LIỆU",
                    $"Dữ liệu đã lưu không hợp lệ hoặc đã bị chỉnh sửa ({err}). Nếu có mã sao lưu hợp lệ, hãy dùng chức năng Khôi phục.",
                    BackToTitle);
                return;
            }
            Kitchen.RebuildStations();
            Customers.Reset();
            Kitchen.ResetDay();
            MorningNotes.Clear();
            if (State.stage == StageEnded)
            {
                ui.ShowGame();
                ApplyEndOutcome();
                return;
            }
            if (State.stage == StageOpen)
            {
                MorningNotes.Add($"Phiên trước thoát giữa buổi bán: ngày {State.day} được tính là đã đóng cửa.");
                ui.ShowGame();
                DayTime = DayLengthSeconds;
                Phase = GamePhase.Closing;
                MarkDirty();
                return;
            }
            MorningNotes.Add("Đã tải dữ liệu đã lưu.");
            DayTime = 0;
            Phase = GamePhase.Prep;
            ui.ShowGame();
            MarkDirty();
        }

        [Serializable]
        class Backup
        {
            public string save;
            public string snap;
        }

        const string BackupPrefix = "MICAY2";

        public string ExportBackupCode()
        {
            string saveJson = Guard.Unwrap(File.Exists(SavePath) ? File.ReadAllText(SavePath) : null) ?? JsonUtility.ToJson(State);
            string snapJson = Guard.Unwrap(File.Exists(SnapshotPath) ? File.ReadAllText(SnapshotPath) : null);
            var b = new Backup
            {
                save = Slim(saveJson),
                snap = string.IsNullOrEmpty(snapJson) ? "" : Slim(snapJson),
            };
            var raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(b));
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) gz.Write(raw, 0, raw.Length);
                string body = Convert.ToBase64String(ms.ToArray());
                return $"{BackupPrefix}-{Guard.Sign(body)}-{body}";
            }
        }

        const int BackupKeepDays = 35;

        static string Slim(string json)
        {
            try
            {
                var st = JsonUtility.FromJson<GameState>(json);
                int keep = Math.Min(st.chapterStartDay, st.day - BackupKeepDays);
                if (keep <= 1) return json;
                int old = keep - 1;
                var oldLedger = st.ledger.Where(e => e.day < keep).ToList();
                st.ledger.RemoveAll(e => e.day < keep);
                st.ledger.InsertRange(0, oldLedger.GroupBy(e => e.cat).Select(g => new LedgerEntry
                {
                    day = old, cat = g.Key, note = $"Gộp sổ ngày 1–{old}", amount = g.Sum(e => e.amount),
                }));
                var oldStats = st.stats.Where(x => x.day < keep).ToList();
                st.stats.RemoveAll(x => x.day < keep);
                if (oldStats.Count > 0)
                {
                    var sum = new DayStats { day = old };
                    foreach (var x in oldStats)
                    {
                        sum.groupsServed += x.groupsServed; sum.groupsLeft += x.groupsLeft; sum.groupsNoSeat += x.groupsNoSeat;
                        sum.dishesSold += x.dishesSold; sum.drinksSold += x.drinksSold; sum.ruined += x.ruined; sum.spiceWrong += x.spiceWrong;
                        sum.revenue += x.revenue; sum.cogs += x.cogs; sum.wasteCost += x.wasteCost; sum.spoiled += x.spoiled; sum.interestAccrued += x.interestAccrued;
                        sum.ratingSum += x.ratingSum; sum.ratingCount += x.ratingCount;
                        foreach (var n in x.sold) sum.AddSold(n.id, n.count);
                    }
                    st.stats.Insert(0, sum);
                }
                return JsonUtility.ToJson(st);
            }
            catch { return json; }
        }

        public static int BackupDay(string code)
        {
            try { return JsonUtility.FromJson<GameState>(DecodeBackup(code).save).day; }
            catch { return -1; }
        }

        public static string BackupSummary(string code)
        {
            try
            {
                var st = JsonUtility.FromJson<GameState>(DecodeBackup(code).save);
                return $"Ngày {st.day}, chương {st.chapter}, tiền mặt {Finance.Money(st.cash)}";
            }
            catch { return null; }
        }

        static Backup DecodeBackup(string code)
        {
            code = code ?? "";
            int at = code.IndexOf(BackupPrefix + "-", StringComparison.Ordinal);
            if (at > 0) code = code.Substring(at);
            code = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
            var parts = code.Split(new[] { '-' }, 3);
            if (parts.Length != 3 || parts[0] != BackupPrefix) throw new Exception("Mã không đúng định dạng (phải bắt đầu bằng MICAY2-).");
            if (!Guard.Verify(parts[2], parts[1])) throw new Exception("Mã không hợp lệ hoặc đã bị chỉnh sửa (sai chữ ký).");
            using (var input = new MemoryStream(Convert.FromBase64String(parts[2])))
            using (var gz = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gz.CopyTo(output);
                var b = JsonUtility.FromJson<Backup>(Encoding.UTF8.GetString(output.ToArray()));
                if (b == null || string.IsNullOrEmpty(b.save)) throw new Exception("Mã không chứa dữ liệu chơi.");
                return b;
            }
        }

        public bool ImportBackupCode(string code, out string error)
        {
            error = null;
            try
            {
                var b = DecodeBackup(code);
                var st = JsonUtility.FromJson<GameState>(b.save);
                string bad = Guard.Validate(st);
                if (bad != null) throw new Exception("Dữ liệu trong mã không hợp lệ (" + bad + ").");
                if (st.saveSeq < SeqMark()) throw new Exception("Mã này cũ hơn tiến trình đã chơi trên máy. Không thể quay về trạng thái cũ hơn.");
                File.WriteAllText(SavePath, Guard.Wrap(JsonUtility.ToJson(st)));
                string snapJson = !string.IsNullOrEmpty(b.snap) ? b.snap : b.save;
                var snapSt = JsonUtility.FromJson<GameState>(snapJson);
                if (Guard.Validate(snapSt) != null) snapSt = st;
                File.WriteAllText(SnapshotPath, Guard.Wrap(JsonUtility.ToJson(snapSt)));
                Keychain.SaveSeq(st.saveSeq);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        void RestoreSnapshot()
        {
            var st = ReadState(SnapshotPath);
            if (st == null) { NewGame(); return; }
            State = st;
            if (!Migrate() || Guard.Validate(State) != null) { NewGame(); return; }
            Kitchen.RebuildStations();
            StartChapter(State.chapter);
        }

        void StartChapter(int n, IList<DialogueLine> introOverride = null)
        {
            State.chapter = n;
            State.chapterStartDay = State.day;
            State.stage = StagePrep;
            if (State.day > State.maxDayReached) State.maxDayReached = State.day;
            var ch = Chapter;
            State.rentPerDay = Math.Max(State.rentPerDay, ch.rentPerDay);
            State.utilityPerDay = Math.Max(State.utilityPerDay, ch.utilityPerDay);
            SaveSnapshot();
            Phase = GamePhase.Prep;
            MorningNotes.Clear();
            MorningNotes.Add($"Bắt đầu {ch.title}.");
            Customers.Reset();
            Kitchen.ResetDay();
            ui.ShowGame();
            MarkDirty();

            var intro = new List<DialogueLine>(introOverride ?? ch.intro);
            var dishes = GameDB.Recipes.Where(r => r.chapter == n).Select(r => r.name).ToList();
            if (n > 1 && dishes.Count > 0) intro.Add(L("narr", "MÓN MỚI: " + string.Join(", ", dishes) + "."));
            string hard = ch.hard ? "\n\n<color=#C62828><b>CHƯƠNG ĐỊA NGỤC</b> — chuẩn bị thật kỹ!</color>" : "";
            PlayDialogue(intro, () =>
                ui.ShowMessage(ch.title, $"{ch.subtitle}\n\n<b>MỤC TIÊU:</b> {ch.goalText}\n\nThời hạn: {ch.days} ngày.{hard}", StartPrep));
        }

        public List<(string text, bool done)> GoalStatus()
        {
            var list = new List<(string, bool)>();
            if (State.finished) { list.Add(("Kinh doanh tự do — quán mở cửa mãi mãi", true)); return list; }
            var ch = Chapter;
            var r = Finance.Build(State, State.chapterStartDay, State.day);
            if (ch.goalRevenue.HasValue)
                list.Add(($"Doanh thu {Finance.Short(r.revenue)}/{Finance.Short(ch.goalRevenue.Value)}", r.revenue >= ch.goalRevenue.Value));
            if (ch.goalNetProfit.HasValue)
                list.Add(($"Lãi ròng {Finance.Short(r.NetProfit)}/{Finance.Short(ch.goalNetProfit.Value)}", r.NetProfit >= ch.goalNetProfit.Value));
            if (ch.goalServed.HasValue)
                list.Add(($"Phục vụ {r.served}/{ch.goalServed.Value} bàn", r.served >= ch.goalServed.Value));
            if (ch.goalReputation.HasValue)
                list.Add(($"Uy tín {State.reputation:0.0}/{ch.goalReputation.Value:0.0}", State.reputation >= ch.goalReputation.Value));
            if (ch.goalCash.HasValue)
                list.Add(($"Tiền mặt {Finance.Short(State.cash)}/{Finance.Short(ch.goalCash.Value)}", State.cash >= ch.goalCash.Value));
            if (ch.goalDebtCleared == "*shark")
            {
                long left = State.debts.Where(d => d.kind == Debt.Shark).Sum(d => Math.Max(0, d.remaining));
                list.Add((left > 0 ? $"Nợ vay nóng còn {Finance.Short(left)}" : "Không nợ vay nóng", left <= 0));
            }
            else if (!string.IsNullOrEmpty(ch.goalDebtCleared))
            {
                var d = State.GetDebt(ch.goalDebtCleared);
                list.Add((d == null ? "Trả nợ" : $"Nợ còn {Finance.Short(d.remaining)}", d == null || d.remaining <= 0));
            }
            if (ch.goalNoOverdueTax)
                list.Add(("Không nợ thuế quá hạn", !TaxSystem.HasOverdue(State)));
            if (list.Count == 0)
                list.Add(($"Trụ vững hết chương (ngày {State.ChapterDay}/{ch.days}) — bán được bao nhiêu hưởng bấy nhiêu", true));
            return list;
        }

        void EvaluateChapter()
        {
            var goals = GoalStatus();
            if (goals.All(g => g.done))
            {
                var lines = new List<DialogueLine>(Chapter.outro ?? new DialogueLine[0]);
                bool last = State.chapter >= StoryDB.Chapters.Count;
                if (last)
                {
                    var mom = State.GetDebt("me");
                    lines.Insert(0, mom != null && mom.remaining <= 0
                        ? L("me", "Con trả đủ tiền cho mẹ rồi, mẹ để dành làm quà cưới cho con đó. Mẹ tự hào về con lắm.")
                        : L("me", "Tiền của mẹ con cứ giữ mà làm ăn. Thấy con thành công là mẹ vui rồi."));
                }
                Sfx.Play("fanfare");
                PlayDialogue(lines, () =>
                {
                    State.day++;
                    if (!last)
                    {
                        StartChapter(State.chapter + 1);
                    }
                    else
                    {
                        State.finished = true;
                        ui.ShowMessage("CHÚC MỪNG!", "Bạn đã vượt qua cả 50 chương!\nQuán không bao giờ đóng cửa: từ giờ là chế độ kinh doanh tự do, sự cố vẫn có thể xảy ra bất cứ lúc nào — tiếp tục làm giàu nào!", StartPrep);
                    }
                });
            }
            else
            {
                string detail = string.Join("\n", goals.Select(g => (g.done ? "[ĐẠT] " : "[CHƯA] ") + g.text));
                FailChapter("Hết thời hạn chương nhưng chưa đạt mục tiêu:\n" + detail);
            }
        }

        void FailChapter(string reason)
        {
            Sfx.Play("fail");
            ui.ShowMessage("THẤT BẠI", reason + "\n\nBạn sẽ chơi lại chương này từ đầu.", () =>
                PlayDialogue(Chapter.fail, RestoreSnapshot));
        }

        string prisonReason;
        public bool PrisonPending => prisonReason != null;

        public void SendToPrison(string reason)
        {
            if (prisonReason == null) prisonReason = reason;
        }

        void GoToPrison()
        {
            string reason = prisonReason;
            prisonReason = null;
            choices.Clear();
            Phase = GamePhase.DayEnd;
            Customers.Reset();
            Kitchen.ResetDay();
            Sfx.Play("fail");
            int count = State.prisonCount + 1;
            var lines = new[]
            {
                L("congan", "Chị Ánh, mời chị về đồn làm việc. Đây là quyết định khởi tố."),
                L("narr", $"Quán bị niêm phong, toàn bộ tài sản bị tịch thu. Ánh phải chấp hành án... (lần thứ {count})"),
                L("me", "Con ơi... Mẹ vẫn chờ con về. Làm lại từ đầu, lần này đàng hoàng nghe con."),
            };
            ui.ShowMessage("ĐI TÙ",
                $"{reason}\n\nQuán bị tịch thu toàn bộ tài sản. Ra tù, bạn phải làm lại từ NGÀY 1 - CHƯƠNG 1 với khoản vay ngân hàng {Finance.Money(PrisonLoan)} (lãi 5%, trả hết trong {PrisonLoanDays} ngày).",
                () => PlayDialogue(lines, () => RestartAfterPrison(count)));
        }

        void RestartAfterPrison(int count)
        {
            State = new GameState { prisonCount = count };
            State.tutDone.AddRange(Lessons.Select(l => l.id));
            var loan = BankSystem.MakeLoan(State, PrisonLoan, PrisonLoanDays, 0.05f, "Vay vốn làm lại");
            State.debts.Add(loan);
            AddLedger(Finance.CatLoan, loan.name, PrisonLoan);
            Kitchen.RebuildStations();
            StartChapter(1, new[]
            {
                L("narr", "Ngày ra tù, Ánh đứng trước con ngõ cũ trên phố Thái Hà. Tiệm xưa đã thành cửa hàng khác."),
                L("bank", $"Hồ sơ vay vốn làm lại của chị được duyệt: {Finance.Money(PrisonLoan)}, lãi 5%, phải trả đủ {Finance.Money(loan.remaining)} trước hết ngày {loan.dueDay}. Trễ là bị cưỡng chế đó chị."),
                L("nam", "Cô cho con thuê lại mặt bằng. Ai cũng có lúc vấp ngã, quan trọng là đứng dậy."),
                L("lan", "Lần này khai thuế, nộp thuế đúng hạn nha em."),
                L("anh", "Dạ. Làm lại từ tô mì đầu tiên."),
            });
        }

        void FireEvents(bool atEnd, List<DialogueLine> lines)
        {
            if (State.finished)
            {
                if (!atEnd && rng.NextDouble() < 0.15)
                {
                    var ev = StoryDB.RandomFreeEvent(rng, State);
                    if (ev.lines != null) lines.AddRange(ev.lines);
                    ApplyEvent(ev, lines);
                }
                return;
            }
            var ch = Chapter;
            for (int i = 0; i < ch.events.Count; i++)
            {
                var ev = ch.events[i];
                if (ev.day != State.ChapterDay || ev.atEnd != atEnd) continue;
                string key = $"c{ch.number}_e{i}";
                if (State.firedEvents.Contains(key)) continue;
                State.firedEvents.Add(key);
                if (ev.lines != null) lines.AddRange(ev.lines);
                ApplyEvent(ev, lines);
            }
        }

        void ApplyEvent(DayEvent ev, List<DialogueLine> lines)
        {
            var s = State;
            int until = s.day + Math.Max(1, ev.days) - 1 + (ev.atEnd ? 1 : 0);
            string M(long v) => Finance.Money(v);
            switch (ev.action)
            {
                case EventAction.AddDebt:
                    if (s.GetDebt(ev.param) == null) s.debts.Add(StoryDB.MakeDebt(ev.param, s.day));
                    break;
                case EventAction.GiveCash:
                    AddLedger(Finance.CatOther, "Thu khác", ev.amount);
                    break;
                case EventAction.RentChange:
                    s.rentPerDay = ev.amount;
                    lines.Add(L("narr", $"Tiền mặt bằng mới: {M(ev.amount)}/ngày."));
                    break;
                case EventAction.UtilityChange:
                    s.utilityPerDay = ev.amount;
                    lines.Add(L("narr", $"Điện, nước, gas mới: {M(ev.amount)}/ngày."));
                    break;
                case EventAction.HireStaff:
                    s.staff = 1;
                    s.staffWage = ev.amount;
                    break;
                case EventAction.ReputationChange:
                    s.reputation = Mathf.Clamp(s.reputation + ev.amount / 100f, 1f, 5f);
                    if (ev.amount < 0) lines.Add(L("narr", $"Uy tín quán giảm còn {s.reputation:0.0}."));
                    break;
                case EventAction.TaxAudit:
                    TaxAudit(lines);
                    break;

                case EventAction.Theft:
                    {
                        long take = Math.Min(ev.amount, Math.Max(0, s.cash * 3 / 10));
                        if (take > 0) AddLedger(Finance.CatTheft, "Bị trộm tiền mặt", -take);
                        long goods = 0;
                        foreach (var b in s.inventory.ToList())
                        {
                            var cat = GameDB.GetIng(b.ing).cat;
                            if (cat != IngCategory.Meat && cat != IngCategory.Seafood) continue;
                            int lost = (b.qty + 1) / 2;
                            goods += lost * b.unitCost;
                            b.qty -= lost;
                            if (b.qty <= 0) s.inventory.Remove(b);
                        }
                        Sfx.Play("alarm");
                        lines.Add(L("narr", $"Mất {M(take)} tiền mặt và số thịt, hải sản trị giá {M(goods)} (không tính vào lãi lỗ của chương, nhưng phải nhập hàng bù)." +
                            (take >= ev.amount ? "" : " (May mà không để nhiều tiền mặt trong quán.)")));
                        break;
                    }
                case EventAction.Loss:
                    AddLedger(Finance.CatLoss, ev.param ?? "Mất mát", -ev.amount);
                    break;
                case EventAction.SpillBroth:
                    {
                        long wasted = 0;
                        foreach (var b in s.inventory.Where(b => GameDB.Broths.Contains(b.ing)).ToList())
                        {
                            wasted += b.qty * b.unitCost;
                            s.inventory.Remove(b);
                        }
                        s.Today.spoiled += wasted;
                        if (ev.amount > 0) AddLedger(Finance.CatLoss, "Thuốc bỏng, dọn bếp", -ev.amount);
                        Sfx.Play("fail");
                        lines.Add(L("narr", $"Đổ bỏ số nước lẩu trị giá {M(wasted)}, tốn thêm {M(ev.amount)} thuốc men, dọn dẹp."));
                        break;
                    }
                case EventAction.PersonalExpense:
                    AddLedger(Finance.CatPersonal, ev.param ?? "Chi việc nhà", -ev.amount);
                    break;
                case EventAction.Invest:
                    AddLedger(Finance.CatInvest, ev.param ?? "Mua sắm thiết bị", -ev.amount);
                    break;
                case EventAction.Expense:
                    if (ev.param == "deposit") RentDepositChoice();
                    else AddLedger(Finance.CatExpense, ev.param ?? "Chi phí khác", -ev.amount);
                    break;

                case EventAction.StoveBroken:
                    Choice("Bếp hư", $"Sửa bếp ngay hết {M(ev.amount)}?\nKhông sửa thì bếp số 1 nghỉ {Math.Max(1, ev.days)} ngày.", $"Sửa ngay ({Finance.Short(ev.amount)})", "Để đó",
                        r => { AddLedger(Finance.CatExpense, "Sửa bếp", -ev.amount); r.Add(L("thoxua", "Xong rồi, bếp chạy ngon lành.")); },
                        r => { s.brokenStoveUntil = until; r.Add(L("anh", $"Bếp số 1 tạm nghỉ tới hết ngày {until}. Phải nấu bằng các bếp còn lại.")); Kitchen.ResetDay(); });
                    break;
                case EventAction.CustomerMod:
                    s.tempCustomerMul = ev.amount / 100f;
                    s.tempCustomerUntil = until;
                    lines.Add(L("narr", ev.amount >= 100
                        ? $"Khách đông hơn {ev.amount - 100}% tới hết ngày {until}."
                        : $"Khách vắng hơn {100 - ev.amount}% tới hết ngày {until}."));
                    break;
                case EventAction.PriceSpike:
                    s.tempPriceMul = ev.amount / 100f;
                    s.tempPriceUntil = until;
                    lines.Add(L("narr", $"Giá nguyên liệu tăng {ev.amount - 100}% tới hết ngày {until}."));
                    break;
                case EventAction.SupplierDelay:
                    s.tempDelayMul = ev.amount / 100f;
                    s.tempDelayUntil = until;
                    lines.Add(L("narr", $"Hàng đặt về chậm gấp {ev.amount / 100f:0.#} lần tới hết ngày {until}. Nhớ đặt sớm!"));
                    break;
                case EventAction.StaffAway:
                    if (s.staff > 0)
                    {
                        s.staffAwayUntil = until;
                        lines.Add(L("narr", $"Vy nghỉ tới hết ngày {until}: không có người phụ bưng món, khách kém kiên nhẫn hơn (không phải trả lương những ngày này)."));
                    }
                    break;
                case EventAction.StaffRaise:
                    if (s.staff <= 0) break;
                    Choice("Tăng lương", $"Tăng lương Vy từ {M(s.staffWage)} lên {M(ev.amount)}/ngày?\nKhông đồng ý thì Vy sẽ nghỉ việc.", "Đồng ý", "Không",
                        r => { s.staffWage = ev.amount; r.Add(L("vy", "Em cảm ơn chị! Em sẽ làm hết sức!")); },
                        r => { s.staff = 0; r.Add(L("vy", "Dạ... vậy em xin nghỉ. Chúc chị buôn bán đắt hàng.")); r.Add(L("narr", "Mất nhân viên: khách kém kiên nhẫn hơn, chỉ còn mình bạn bưng món.")); });
                    break;
                case EventAction.ClosedDay:
                    s.closedDay = ev.atEnd ? s.day + 1 : s.day;
                    break;

                case EventAction.ShadySupplier:
                    Choice("Hàng trôi nổi", "Nhận lô thịt, hải sản không rõ nguồn gốc, rẻ hơn 60%?\n(Bị kiểm tra vệ sinh trong 5 ngày tới là phạt rất nặng và bị ghi điểm vi phạm)", "Lấy hàng", "Từ chối",
                        r => ShadyGoods(r),
                        r => r.Add(L("anh", "Thôi anh ơi, quán em chỉ lấy hàng có nguồn gốc rõ ràng.")));
                    break;
                case EventAction.HealthInspection:
                    HealthInspection(ev.amount, lines);
                    break;
                case EventAction.FireInspection:
                    if (s.fireSafetyOk) { lines.Add(L("pccc", "Quán đã trang bị đầy đủ PCCC. Đạt yêu cầu!")); break; }
                    Choice("Kiểm tra PCCC", $"Trang bị PCCC theo yêu cầu hết {M(ev.amount)}?\nKhông trang bị: phạt {M(ev.amount * 2)} và bị ghi 1 điểm vi phạm.", $"Trang bị ({Finance.Short(ev.amount)})", "Không",
                        r => { AddLedger(Finance.CatInvest, "Trang bị PCCC", -ev.amount); s.fireSafetyOk = true; r.Add(L("pccc", "Tốt lắm, quán đạt yêu cầu.")); },
                        r => { AddLedger(Finance.CatFine, "Phạt vi phạm PCCC", -ev.amount * 2); Legal.AddViolation(this, 1, "vi phạm quy định PCCC", r); });
                    break;
                case EventAction.EInvoice:
                    if (s.eInvoiceOk) break;
                    Choice("Hóa đơn điện tử", $"Mua phần mềm hóa đơn điện tử hết {M(ev.amount)}?\nKhông mua thì bị phạt 5 triệu + 1 điểm vi phạm mỗi lần thanh tra thuế.", $"Mua ({Finance.Short(ev.amount)})", "Để sau",
                        r => { AddLedger(Finance.CatInvest, "Phần mềm hóa đơn điện tử", -ev.amount); s.eInvoiceOk = true; r.Add(L("lan", "Tốt, quán em đã kết nối hóa đơn điện tử.")); },
                        r => r.Add(L("anh", "(Thôi để sau... hy vọng không bị thanh tra.)")));
                    break;
                case EventAction.Sidewalk:
                    if (s.sidewalkPermit) break;
                    Choice("Trật tự vỉa hè", $"Xin giấy phép tạm sử dụng vỉa hè hết {M(ev.amount)}?\nKhông xin mà quán có hơn 10 bàn (hiện có {s.tables}) thì bị phạt {M(ev.amount * 3)} + 1 điểm vi phạm.", $"Xin phép ({Finance.Short(ev.amount)})", "Không",
                        r => { AddLedger(Finance.CatExpense, "Giấy phép tạm dùng vỉa hè", -ev.amount); s.sidewalkPermit = true; r.Add(L("congan", "Giấy phép đây, nhớ chừa lối đi cho người đi bộ.")); },
                        r =>
                        {
                            if (s.tables <= 10) { r.Add(L("congan", "Quán em kê bàn gọn trong nhà, không vi phạm.")); return; }
                            AddLedger(Finance.CatFine, "Phạt lấn chiếm vỉa hè", -ev.amount * 3);
                            Legal.AddViolation(this, 1, "lấn chiếm vỉa hè", r);
                        });
                    break;
                case EventAction.Scam:
                    Choice("Cẩn thận!", "Làm theo yêu cầu chuyển tiền?", ev.param ?? "Chuyển tiền", "Từ chối, xác minh lại",
                        r =>
                        {
                            long lost = Math.Min(ev.amount, Math.Max(0, s.cash));
                            AddLedger(Finance.CatLoss, "Bị lừa đảo chuyển tiền", -lost);
                            Sfx.Play("fail");
                            r.Add(L("narr", $"Số điện thoại tắt máy. Mất trắng {M(lost)}..."));
                            r.Add(L("congan", "Đây là chiêu lừa đảo quen thuộc. Không chuyển tiền cho người lạ, có gì gọi cho cơ quan chức năng xác minh trước."));
                        },
                        r => r.Add(L("congan", "Em làm đúng lắm. Đó là lừa đảo, bên anh đang truy bắt.")));
                    break;
                case EventAction.LoanShark:
                    Choice("Vay nóng?", $"Nhận {M(ev.amount)} ngay, phải trả {M(ev.amount * 3 / 2)}: mỗi 3 ngày góp {M(ev.amount / 2)}, lãi 10%/kỳ.\nTrễ 2 kỳ là bị siết quán!", "Vay", "Từ chối",
                        r =>
                        {
                            AddLedger(Finance.CatLoan, "Vay nóng", ev.amount);
                            s.debts.Add(StoryDB.MakeShark(ev.amount, s.day));
                            r.Add(L("la", "Nhớ trả đúng hạn nha em. Trễ là anh em tới quán \"nói chuyện\" đó."));
                        },
                        r => r.Add(L("me", "Con từ chối là đúng. Tín dụng đen là cái bẫy, cần tiền thì vay ngân hàng đàng hoàng.")));
                    break;
            }
        }

        void RentDepositChoice()
        {
            const long deposit = 15_000_000;
            var s = State;
            long newRent = s.rentPerDay * 3 / 2;
            Choice("Hợp đồng mặt bằng", $"Đặt cọc thêm {Finance.Money(deposit)} để giữ giá thuê,\nhay chấp nhận tiền thuê tăng lên {Finance.Money(newRent)}/ngày?", "Đặt cọc", "Tăng tiền thuê",
                r =>
                {
                    if (s.cash < deposit)
                    {
                        s.rentPerDay = newRent;
                        r.Add(L("nam", $"Con không đủ tiền cọc rồi, vậy cô tính tiền thuê {Finance.Money(newRent)}/ngày nha."));
                        return;
                    }
                    AddLedger(Finance.CatInvest, "Đặt cọc mặt bằng", -deposit);
                    r.Add(L("nam", "Được, cô giữ giá cũ cho con."));
                },
                r => { s.rentPerDay = newRent; r.Add(L("nam", $"Vậy từ nay tiền thuê là {Finance.Money(newRent)}/ngày nha con.")); });
        }

        void ShadyGoods(List<DialogueLine> r)
        {
            var s = State;
            long cost = 0;
            foreach (var d in GameDB.UnlockedIngredients(s.chapter).Where(d => d.cat == IngCategory.Meat || d.cat == IngCategory.Seafood))
            {
                int qty = Math.Max(5, Math.Min(20, d.pack / 2));
                long unit = Math.Max(100, d.wholesale * 4 / 10);
                Inventory.Add(s, d.id, qty, unit, s.day);
                cost += qty * unit;
            }
            if (cost > 0) AddLedger(Finance.CatPurchase, "Hàng trôi nổi giá rẻ", -cost);
            s.shadyUntil = s.day + 5;
            r.Add(L("la", "Hàng đây, đếm đi em. Có ai hỏi thì nói lấy ở chợ nha."));
            r.Add(L("narr", $"Nhận hàng trị giá {Finance.Money(cost)} (rẻ hơn 60%). Trong 5 ngày tới mà bị kiểm tra vệ sinh thì rất nguy!"));
        }

        void HealthInspection(long fine, List<DialogueLine> lines)
        {
            var s = State;
            if (!s.ShadyGoods)
            {
                s.reputation = Mathf.Min(5f, s.reputation + 0.1f);
                lines.Add(L("attp", "Nguyên liệu có nguồn gốc rõ ràng, bếp sạch sẽ. Quán ĐẠT yêu cầu! (Uy tín tăng)"));
                return;
            }
            AddLedger(Finance.CatFine, "Phạt vi phạm vệ sinh an toàn thực phẩm", -fine);
            long destroyed = 0;
            foreach (var b in s.inventory.ToList())
            {
                var cat = GameDB.GetIng(b.ing).cat;
                if (cat != IngCategory.Meat && cat != IngCategory.Seafood) continue;
                destroyed += b.qty * b.unitCost;
                s.inventory.Remove(b);
            }
            s.Today.spoiled += destroyed;
            s.shadyUntil = 0;
            s.reputation = Mathf.Max(1f, s.reputation - 0.6f);
            Sfx.Play("alarm");
            lines.Add(L("attp", $"Phát hiện thịt, hải sản không rõ nguồn gốc! Tiêu hủy toàn bộ (trị giá {Finance.Money(destroyed)}), phạt {Finance.Money(fine)}."));
            Legal.AddViolation(this, 2, "dùng thực phẩm không rõ nguồn gốc", lines);
            Legal.Seize(this, "tiền phạt vệ sinh thực phẩm", lines);
        }

        void TaxAudit(List<DialogueLine> lines)
        {
            var s = State;
            var overdue = s.taxBills.Where(b => !b.paid && s.day > b.dueDay).ToList();
            int latePaid = s.taxBills.Count(b => b.paid && b.paidDay > b.dueDay);
            bool clean = true;
            if (overdue.Count == 0)
            {
                lines.Add(L("lan", latePaid == 0
                    ? "Các kỳ trước quán em khai, nộp đầy đủ và đúng hạn. Rất tốt, chị ghi nhận!"
                    : $"Không có kỳ nào đang nợ, nhưng có {latePaid} kỳ nộp trễ. Lần sau em chú ý hạn nộp nha."));
            }
            else
            {
                clean = false;
                long tax = overdue.Sum(b => b.Total);
                long fine = Math.Max(2_000_000, (long)Math.Round(tax * 0.2));
                AddLedger(Finance.CatFine, "Phạt qua kiểm tra thuế", -fine);
                s.reputation = Mathf.Max(1f, s.reputation - 0.2f);
                lines.Add(L("lan",
                    $"Phát hiện {overdue.Count} kỳ thuế quá hạn chưa khai nộp (số thuế {Finance.Money(tax)}). " +
                    $"Quán bị xử phạt {Finance.Money(fine)}, và vẫn phải nộp đủ thuế kèm tiền chậm nộp."));
                Legal.AddViolation(this, 1, "chậm khai nộp thuế", lines);
            }
            if (s.chapter >= 26 && !s.eInvoiceOk)
            {
                clean = false;
                AddLedger(Finance.CatFine, "Phạt không dùng hóa đơn điện tử", -5_000_000);
                lines.Add(L("lan", "Quán chưa dùng hóa đơn điện tử theo quy định: phạt 5 triệu."));
                Legal.AddViolation(this, 1, "không sử dụng hóa đơn điện tử", lines);
            }
            if (clean && latePaid == 0) s.reputation = Mathf.Min(5f, s.reputation + 0.1f);
            Legal.Seize(this, "tiền phạt thuế", lines);
        }

        class Lesson
        {
            public string id;
            public string[] steps;
            public bool urgent;
            public bool pauses;
            public Func<GameManager, bool> when;
        }

        static readonly Lesson[] Lessons =
        {
            new Lesson { id = "restock", steps = new[] { "restock_market", "restock_buy", "close_market", "restock_done" }, urgent = true, pauses = true,
                         when = g => g.InChapter1 && g.Phase == GamePhase.Open && g.ServedSomeone && g.VisibleLowStock != null },
            new Lesson { id = "ordering", steps = new[] { "market2", "stock", "close_market", "ordering_done" },
                         urgent = true, when = g => g.State.day >= 2 && g.Phase == GamePhase.Prep },
            new Lesson { id = "tax_pay", steps = new[] { "tax_open", "tax_pay", "close_tax", "tax_pay_done" },
                         urgent = true, when = g => g.Phase == GamePhase.Prep && g.State.taxBills.Any(b => !b.paid) },
            new Lesson { id = "debt", steps = new[] { "finance", "debt_tab", "close_finance", "debt_done" },
                         urgent = true, when = g => g.Phase == GamePhase.Prep && g.State.debts.Any(d => d.remaining > 0 && d.periodDays > 0) },
            new Lesson { id = "bank", steps = new[] { "finance", "bank_tab", "close_finance", "bank_done" },
                         urgent = true, when = g => g.Phase == GamePhase.Prep && g.State.day >= 2 && (g.State.cash < 3_000_000 || g.State.negativeCashDays > 0) },
            new Lesson { id = "books", steps = new[] { "finance", "ledger", "close_finance", "tax", "close_tax", "books_done" },
                         when = g => g.State.day >= 2 && g.Phase == GamePhase.Prep },
            new Lesson { id = "menu", steps = new[] { "menu", "price", "close_menu", "menu_done" },
                         when = g => g.State.day >= 3 && g.Phase == GamePhase.Prep },
            new Lesson { id = "topping", steps = new[] { "topping", "topping_done" }, urgent = true, pauses = true,
                         when = g => g.Phase == GamePhase.Open && g.Kitchen.Target == null && g.ToppingOrder() != null },
            new Lesson { id = "quick", steps = new[] { "quick_pick", "quick", "quick_done" }, urgent = true, pauses = true,
                         when = g => g.Phase == GamePhase.Open && (g.QuickTargetOk || g.QuickOrder() != null) },
        };

        Lesson CurrentLesson => TutorialActive ? Lessons.FirstOrDefault(l => l.id == State.tutLesson) : null;

        public bool TutorialActive => State != null && !string.IsNullOrEmpty(State.tutLesson);
        public string TutorialStepId
        {
            get
            {
                var l = CurrentLesson;
                return l != null && State.tutStep < l.steps.Length ? l.steps[State.tutStep] : null;
            }
        }
        public bool TutorialLocking => TutorialStepId != null && !TutorialStepId.EndsWith("_done");
        public bool TutorialBasics => State != null && State.tutLesson == "basics";
        public bool TutorialCalm => TutorialBasics && State.tutStep < Array.IndexOf(CurrentLesson.steps, "hint_hide");
        public bool TutorialSpawnAllowed => !TutorialBasics || State.tutStep >= Array.IndexOf(CurrentLesson.steps, "order");
        public bool TutorialPausing => CurrentLesson != null && CurrentLesson.pauses && TutorialLocking;
        float tutStepAt;

        public OrderItem ToppingOrder()
        {
            foreach (var g in Customers.groups)
                if (g.Arrived && !g.serving && !g.eating)
                    foreach (var it in g.items)
                        if (it.HasExtras && it.status == ItemStatus.Waiting && Customers.ClaimedBy(g) == null && Customers.FirstWaitingBefore(g, it.recipe.Station) == null)
                            return it;
            return null;
        }

        bool QuickTargetOk => Kitchen.Target != null && Kitchen.CanQuickFill(Kitchen.Target);

        public OrderItem QuickOrder()
        {
            foreach (var g in Customers.groups.Where(x => x.Arrived && !x.serving && !x.eating).OrderBy(x => x.arrivedSeq))
                foreach (var it in g.items)
                    if (it.status == ItemStatus.Waiting && Kitchen.CanQuickFill(it) && Customers.ClaimedBy(g) == null &&
                        Customers.FirstWaitingBefore(g, it.recipe.Station) == null)
                        return it;
            return null;
        }

        static string TutSignal(string step)
        {
            switch (step)
            {
                case "market": case "market2": case "restock_market": return "market";
                case "restock_buy": return "order";
                case "stock": return "mtab2";
                case "menu": return "menu";
                case "price": return "price";
                case "finance": case "tour_finance": return "finance";
                case "ledger": return "ftab5";
                case "debt_tab": return "ftab6";
                case "bank_tab": return "ftab7";
                case "tax": case "tax_open": return "tax";
                case "tax_pay": return "taxpay";
                case "area_right": return "right";
                case "area_left": return "left";
                case "service": return "service";
                case "hint_hide": case "hint_show": return "hint";
                case "quick": return "quick";
                case "close_market": case "close_menu": case "close_finance": case "close_tax": case "close_service": return "close";
                default: return null;
            }
        }

        public string TutorialText
        {
            get
            {
                switch (TutorialStepId)
                {
                    case "area_right":
                        return "Chào mừng tới tiệm! Làm theo chỉ dẫn nhé. Quán có 4 khu đặt bàn: bấm mũi tên \">\" để xem khu bên cạnh.";
                    case "area_left":
                        return "Chỗ chưa có bàn hiện bàn mờ, bấm \"+ Thêm bàn\" để mua. Khách ngồi ở khu đang khuất thì mũi tên chuyển ĐỎ. Bấm \"<\" để quay về.";
                    case "market":
                        return "Tủ bếp đang trống trơn: bấm NHẬP HÀNG để mua nguyên liệu.";
                    case "buy":
                        var next = TutorialNextIngredient;
                        return "Bấm ĐẶT SỈ để mua TỪNG nguyên liệu" +
                               (next != null ? $" (tiếp theo: <b>{next.name}</b>, còn {TutorialMissingCount} loại)" : "") +
                               ". Đặt hàng là mua giá gốc. Sáng nay khai trương nên hàng về NGAY, và chỉ nhập vừa đủ bán trong ngày.";

                    case "restock_market":
                        return "Ô nguyên liệu NHÁY ĐỎ trong tủ là hàng SẮP HẾT" + (TutIngName != null ? $" (<b>{TutIngName}</b>)" : "") +
                               ". Hàng sỉ phải chờ 1 phút mới về, để hết sạch mới đặt là khách bỏ về. Bấm NHẬP HÀNG để đặt ngay.";
                    case "restock_buy":
                        return "Dòng tô đỏ là hàng sắp hết. Bấm + nếu muốn đặt nhiều hơn, rồi bấm ĐẶT SỈ" + (TutIngName != null ? $" cho <b>{TutIngName}</b>" : "") +
                               ". Hàng về sau 1 phút, không kịp hôm nay thì về trong đêm, sáng mai có sẵn.";
                    case "restock_done":
                        return "Thói quen tốt: thấy ô nào nháy đỏ là đặt sỉ NGAY, đừng chờ hết sạch. Hàng chưa về kịp hôm nay thì sáng mai có sẵn.";

                    case "tour_finance":
                        return "Hết giờ bán rồi! Trước khi tổng kết, làm quen nốt các nút còn lại. Bấm SỔ SÁCH: nơi xem doanh thu, chi phí, lời lỗ của quán.";
                    case "tour_done":
                        return "Dải phía trên khu quán hiện ĐÁNH GIÁ của khách, bấm vào để đọc sao và lời bình. Nút ĐÓNG CỬA cạnh đó để nghỉ sớm. Nút II để tạm dừng, chỉnh âm thanh hoặc về màn hình chính. Xong ngày đầu tiên rồi!";
                    case "close_market":
                        return "Bấm ĐÓNG để quay lại quán.";
                    case "open":
                        return "Đủ hàng rồi! Bấm MỞ CỬA BÁN HÀNG để đón khách. Quán mở từ 9:00 tới 23:00 (khoảng 6 phút).";
                    case "order":
                        return "Khách vào bàn và gọi món (lời khách hiện ở thanh chat dưới đáy). Thanh màu dưới bong bóng là độ kiên nhẫn. Chạm vào MÓN trong bong bóng để bắt đầu làm.";
                    case "cook":
                        return "Phiếu bên phải ghi CÔNG THỨC. Chạm từng thẻ NGUYÊN LIỆU để cho vào nồi (mì cay: số muỗng BỘT ỚT = số đỏ trên món). Cho nhầm thì bấm ĐỔ NỒI. Xong bấm CHẾ BIẾN.";
                    case "serve":
                        return "Bếp đang nấu. Đồ uống làm ở MÁY PHA: cũng chọn món, cho nguyên liệu rồi bấm PHA CHẾ. Bàn đủ món thì bấm GIAO MÓN để Ánh bưng ra cho khách.";
                    case "hint_hide":
                        return "Nút CÔNG THỨC: ẩn công thức trên phiếu để tự nhớ cách làm, quen tay thì nấu nhanh hơn. Bấm để ẩn thử.";
                    case "hint_show":
                        return "Công thức đã ẩn. Bấm lần nữa để hiện lại khi cần xem.";
                    case "basics_done":
                        return "Giỏi lắm! Cứ bán tiếp tới 23:00. Hết ngày sẽ có báo cáo, sáng mai học tiếp nhé.";

                    case "market2":
                        return "Từ hôm nay, kể cả buổi sáng, hàng ĐẶT cũng phải chờ chú Tư giao 1 phút mới tới. Mở cửa mà tủ trống là khách bỏ về! Bấm NHẬP HÀNG để đặt trước.";
                    case "stock":
                        return "Tab KHO & HÀNG ĐANG VỀ: xem hàng còn trong tủ, hạn dùng từng lô và hàng đang trên đường về. Bấm vào tab này.";
                    case "ordering_done":
                        return "Hàng nào cũng chỉ để được vài ngày, hết hạn là phải bỏ. Đặt vừa đủ dùng thôi nhé!";

                    case "finance":
                        return "Bấm SỔ SÁCH: nơi xem doanh thu, chi phí, lời lỗ của quán.";
                    case "ledger":
                        return "Báo cáo xem theo hôm nay, 7 ngày, 30 ngày hoặc cả chương. Tab NHẬT KÝ ghi từng khoản thu chi. Bấm tab NHẬT KÝ.";
                    case "close_finance":
                        return "Bấm ĐÓNG.";
                    case "tax":
                        return "Bấm THUẾ: hộ kinh doanh tự khai, tự nộp thuế. Cứ 7 ngày là một kỳ.";
                    case "close_tax":
                        return "Hết kỳ thì có 3 ngày để nộp. Trễ bị phạt, để lâu bị cưỡng chế, tái phạm có thể ĐI TÙ. Bấm ĐÓNG.";
                    case "books_done":
                        return "Nhớ ghé SỔ SÁCH để biết mình lời hay lỗ, và THUẾ để không trễ hạn.";

                    case "tax_open":
                        return "Đã có kỳ thuế cần khai nộp! Bấm THUẾ.";
                    case "tax_pay":
                        return "Bấm \"Khai & nộp\" để nộp tờ khai và tiền thuế của kỳ này (doanh thu thấp thì chỉ nộp tờ khai, không mất tiền).";
                    case "tax_pay_done":
                        return "Xong! Mỗi kỳ thuế nhớ nộp trước hạn chót ghi trong bảng.";

                    case "debt_tab":
                        return "Tab NỢ: xem các khoản nợ, số phải trả mỗi kỳ và hạn chót. Bấm tab NỢ.";
                    case "debt_done":
                        return "Mỗi kỳ phải trả ít nhất số tiền ghi trong tab NỢ trước hạn. Trễ 2 kỳ là bị siết quán!";

                    case "bank_tab":
                        return "Tiền mặt đang thấp. Tab NGÂN HÀNG cho vay 7 hoặc 14 ngày. Bấm tab NGÂN HÀNG để xem.";
                    case "bank_done":
                        return "Vay thì PHẢI TRẢ ĐÚNG HẠN: trễ 3 ngày bị cưỡng chế, tịch thu tài sản, không đủ thì ĐI TÙ.";

                    case "menu":
                        return "Bấm THỰC ĐƠN: nơi xem các món, chỉnh giá bán và bật/tắt món.";
                    case "price":
                        return "GIÁ BÁN: giảm 10% thì đông khách hơn, tăng 10% thì lời hơn nhưng vắng hơn. Món TẠM NGƯNG thì khách không gọi. Bấm \"Giá gốc\".";
                    case "close_menu":
                        return "Bấm ĐÓNG.";
                    case "menu_done":
                        return "Khách đông mà bếp nấu không kịp thì vào DỊCH VỤ mua thêm BẾP trước rồi mới thêm bàn.";

                    case "service":
                        return "Bấm DỊCH VỤ: mua thêm bếp (3 loại, bếp càng xịn nấu càng nhanh), máy pha đồ uống, chỗ ngồi và thuê nhân viên phụ trách các khu.";
                    case "close_service":
                        return "Mở SỔ SÁCH, THUẾ, THỰC ĐƠN, DỊCH VỤ thì game tự dừng. Riêng NHẬP HÀNG thì thời gian vẫn trôi, khách vẫn chờ! Bấm ĐÓNG.";

                    case "topping":
                        return "Món có dấu + màu hồng là khách gọi THÊM topping (tính thêm tiền). Phải cho thêm đúng topping đó vào nồi, thiếu là hỏng món. Chạm vào món này.";
                    case "topping_done":
                        return "Dòng \"+ THÊM\" trên phiếu ghi topping khách gọi. Nhớ trữ sẵn các loại topping hay được gọi trong tủ.";

                    case "quick_pick":
                        var q = QuickOrder();
                        return $"Đã {(q != null && q.recipe.IsDrink ? "pha" : "nấu")} {(q != null ? $"<b>{q.recipe.name}</b>" : "món này")} đủ {KitchenSystem.MasteryForQuickFill} lần: mở khóa GHÉP NHANH! Chạm vào {(q != null && q.recipe.IsDrink ? "đồ uống" : "món")} này trong đơn của khách.";
                    case "quick":
                        return "Bấm GHÉP NHANH: tự cho đủ nguyên liệu vào nồi (đồ uống thì vào ly). Chỉ dùng được với món theo công thức gốc; khách có yêu cầu riêng (dấu − hoặc +) thì phải tự làm.";
                    case "quick_done":
                        return "Giờ chỉ cần bấm CHẾ BIẾN (đồ uống thì bấm PHA CHẾ). Món nào làm càng nhiều thì càng nhanh tay!";
                    default:
                        return "";
                }
            }
        }

        bool TutorialMissing(IngredientDef d) => Inventory.Count(State, d.id) + Market.PendingQty(d.id) <= 0;

        public IngredientDef TutorialNextIngredient =>
            GameDB.UnlockedIngredients(State.chapter).OrderBy(d => d.IsDrink ? 1 : 0).FirstOrDefault(TutorialMissing);

        public int TutorialMissingCount => GameDB.UnlockedIngredients(State.chapter).Count(TutorialMissing);

        const float Day1FoodOrders = 44f * CustomerSystem.SlowPace * CustomerSystem.OpeningDayRate;

        public bool Day1Stocking => State.day == 1 && Phase == GamePhase.Prep;

        public int Day1Qty(IngredientDef d)
        {
            var recipes = GameDB.UnlockedRecipes(State.chapter).ToList();
            var foods = recipes.Where(r => !r.IsDrink).ToList();
            var drinks = recipes.Where(r => r.IsDrink).ToList();
            float foodW = foods.Sum(CustomerSystem.FoodWeight), drinkW = drinks.Sum(CustomerSystem.DrinkWeight);
            float need = 0;
            foreach (var r in foods)
                need += Day1FoodOrders * CustomerSystem.FoodWeight(r) / foodW * Uses(r, d.id);
            foreach (var r in drinks)
                need += Day1FoodOrders * CustomerSystem.DrinkChance * CustomerSystem.DrinkWeight(r) / drinkW * Uses(r, d.id);
            return Math.Max(3, Mathf.CeilToInt(need));
        }

        float Uses(RecipeDef r, string ing)
        {
            float n = r.items.Where(i => i.ing == ing).Sum(i => i.qty);
            if (r.spicy && ing == GameDB.Chili) n += CustomerSystem.AvgSpice;
            if (r.broth == "any")
            {
                float thai = Customers.ThaiChance();
                if (ing == GameDB.BrothThai) n += thai;
                else if (ing == GameDB.BrothKimchi) n += 1f - thai;
            }
            else if (r.HasBroth && r.broth == ing) n += 1f;
            return n;
        }

        int Stock(string ing) => Inventory.Count(State, ing) + Market.PendingQty(ing);

        public bool InChapter1 => !State.finished && State.chapter == 1;

        public IngredientDef TutIng => !string.IsNullOrEmpty(State.tutIng) && GameDB.Ing.TryGetValue(State.tutIng, out var d) ? d : null;

        public string TutIngName => TutIng?.name;


        public static int LowStockAt(string id) => id == GameDB.Chili ? 10 : 3;

        public bool IsLowStock(string id)
        {
            int left = Inventory.Count(State, id) - Kitchen.pot.Count(p => p == id);
            return left > 0 && left <= LowStockAt(id) && Market.PendingQty(id) <= 0;
        }

        public IngredientDef LowStockIngredient =>
            GameDB.UnlockedIngredients(State.chapter).FirstOrDefault(d => IsLowStock(d.id));

        public bool ServedSomeone => State.stats.Any(st => st.groupsServed > 0);

        public IngredientDef VisibleLowStock
        {
            get
            {
                bool drinks = Kitchen.Target != null && Kitchen.Target.recipe.IsDrink;
                return GameDB.UnlockedIngredients(State.chapter).FirstOrDefault(d => d.IsDrink == drinks && IsLowStock(d.id));
            }
        }

        public IngredientDef RestockIngredient =>
            TutIng ?? LowStockIngredient ?? GameDB.UnlockedIngredients(State.chapter).OrderBy(d => (float)Stock(d.id) / Math.Max(1, d.pack)).FirstOrDefault();

        public bool TutorialHidesPrep => TutorialStepId == "area_right" || TutorialStepId == "area_left";

        public void TutorialSignal(string what)
        {
            if (!TutorialActive) return;
            var sig = TutSignal(TutorialStepId);
            if (sig != null && sig == what) AdvanceTutorial();
        }

        public void SkipTutorial() => FinishLesson();

        void StartLesson(Lesson l)
        {
            State.tutLesson = l.id;
            State.tutStep = 0;
            State.tutIng = l.id == "restock" ? (VisibleLowStock ?? LowStockIngredient)?.id ?? "" : "";
            if (!l.urgent) State.tutLastDay = State.day;
            tutStepAt = Time.unscaledTime;
            Sfx.Play("blip");
            MarkDirty();
        }

        void FinishLesson() => FinishLesson(true);

        void FinishLesson(bool resume)
        {
            if (!TutorialActive) return;
            var covers = new List<string> { State.tutLesson };
            if (State.tutLesson == "tour") covers.AddRange(new[] { "books", "menu" });
            if (!resume && !string.IsNullOrEmpty(State.tutResume)) covers.Add(State.tutResume);
            foreach (var id in covers)
                if (!State.tutDone.Contains(id)) State.tutDone.Add(id);
            State.tutLesson = "";
            State.tutStep = 0;
            if (resume && !string.IsNullOrEmpty(State.tutResume))
            {
                State.tutLesson = State.tutResume;
                State.tutStep = State.tutResumeStep;
                tutStepAt = Time.unscaledTime;
            }
            State.tutResume = "";
            MarkDirty();
        }

        static readonly string[] Interrupters = { "restock", "quick" };

        bool TryInterruptForRestock()
        {
            var l = CurrentLesson;
            if (l == null || l.id != "basics") return false;
            if (State.tutStep < Array.IndexOf(l.steps, "hint_hide") || DialogueActive || ModalCount > 0) return false;
            foreach (var id in Interrupters)
            {
                if (State.tutDone.Contains(id)) continue;
                var next = Lessons.First(x => x.id == id);
                if (!next.when(this)) continue;
                State.tutResume = State.tutLesson;
                State.tutResumeStep = State.tutStep;
                StartLesson(next);
                return true;
            }
            return false;
        }

        void AdvanceTutorial()
        {
            State.tutStep++;
            tutStepAt = Time.unscaledTime;
            if (TutorialStepId == null) FinishLesson();
            Sfx.Play("blip");
            MarkDirty();
        }

        static readonly bool TutorialsOn = false;

        void TutorialTick()
        {
            if (!TutorialsOn)
            {
                if (TutorialActive) FinishLesson(false);
                return;
            }
            if (Phase == GamePhase.Title || PrisonPending) return;
            if (!TutorialActive)
            {
                if (DialogueActive || ModalCount > 0 || choices.Count > 0 || Phase == GamePhase.DayEnd || State.day == 1) return;
                foreach (var l in Lessons)
                {
                    if (State.tutDone.Contains(l.id)) continue;
                    if (!l.urgent && State.tutLastDay == State.day) continue;
                    if (!l.when(this)) continue;
                    StartLesson(l);
                    break;
                }
                return;
            }

            if (CurrentLesson == null) { FinishLesson(); return; }
            if (TryInterruptForRestock()) return;
            string id = TutorialStepId;
            if (id != null && id.EndsWith("_done"))
            {
                if (Time.unscaledTime - tutStepAt > (id == "basics_done" ? 15f : 9f)) FinishLesson();
                return;
            }
            switch (id)
            {
                case "buy":
                    if (GameDB.UnlockedIngredients(State.chapter).All(d => Inventory.Count(State, d.id) > 0)) AdvanceTutorial();
                    break;
                case "open":
                    if (Phase == GamePhase.Open) AdvanceTutorial();
                    break;
                case "order":
                    if (Kitchen.targetGroup >= 0) AdvanceTutorial();
                    break;
                case "cook":
                    if (Customers.groups.Any(g => g.items.Any(i => i.status != ItemStatus.Waiting))) AdvanceTutorial();
                    break;
                case "serve":
                    if (State.Today.groupsServed > 0 || Customers.groups.Any(x => x.eating)) AdvanceTutorial();
                    break;
                case "topping":
                    if (Kitchen.Target != null && Kitchen.Target.HasExtras) AdvanceTutorial();
                    else if (ToppingOrder() == null) FinishLesson();
                    break;
                case "quick_pick":
                    if (QuickTargetOk) AdvanceTutorial();
                    else if (QuickOrder() == null) FinishLesson();
                    break;
                case "quick":
                    if (!Kitchen.CanQuickFill(Kitchen.Target)) FinishLesson();
                    break;
                case "tax_pay":
                    if (!State.taxBills.Any(b => !b.paid)) AdvanceTutorial();
                    break;
            }
        }

        void StartPrep()
        {
            var s = State;
            s.stage = StagePrep;
            if (s.day > s.maxDayReached) s.maxDayReached = s.day;
            ChatLog.Clear();
            MorningNotes.Clear();
            Legal.DailyDecay(s);
            Market.MorningDeliveries(MorningNotes);
            long spoiled = Inventory.ProcessSpoilage(s, s.day, MorningNotes);
            if (spoiled > 0)
            {
                s.Today.spoiled += spoiled;
                MorningNotes.Add($"Tổng hao hụt do hết hạn: {Finance.Money(spoiled)}");
            }
            if (s.day >= 2)
            {
                var tops = GameDB.DailyToppings(s.day, s.chapter);
                if (tops.Count > 0)
                    MorningNotes.Add("<color=#1E5AA8>Topping khách hay gọi thêm hôm nay: " + string.Join(", ", tops.Select(t => t.name)) + ". Nhớ trữ sẵn trong tủ!</color>");
            }
            foreach (var b in s.taxBills.Where(b => !b.paid))
            {
                int late = TaxSystem.LateDays(b, s.day);
                MorningNotes.Add(late > 0
                    ? $"<color=#C62828>QUÁ HẠN thuế kỳ {b.fromDay}-{b.toDay}: {late} ngày (~{late * TaxSystem.RealDaysPerGameDay} ngày thực)! Cưỡng chế cuối ngày {b.dueDay + TaxSystem.EnforceAfterLateDays}.</color>"
                    : $"Thuế kỳ {b.fromDay}-{b.toDay}: hạn nộp hết ngày {b.dueDay}");
            }
            foreach (var d in s.debts.Where(d => d.remaining > 0))
            {
                if (d.kind == Debt.Bank)
                    MorningNotes.Add(s.day > d.dueDay
                        ? $"<color=#C62828>NỢ NGÂN HÀNG QUÁ HẠN: {d.name} còn {Finance.Money(d.remaining)} — cưỡng chế cuối ngày {d.dueDay + BankSystem.GraceDays}!</color>"
                        : $"Nợ ngân hàng {d.name}: {Finance.Money(d.remaining)}, hạn trả hết ngày {d.dueDay}");
                else if (d.id == "me")
                    MorningNotes.Add(s.day > d.dueDay
                        ? $"<color=#C62828>Vẫn còn nợ mẹ {Finance.Money(d.remaining)} (đã quá hạn ngày {d.dueDay}). Mẹ đang cần khoản đó, cố trả sớm cho mẹ nha!</color>"
                        : s.day <= 3
                            ? $"Nợ mẹ {Finance.Money(d.remaining)} — mẹ dặn mấy ngày đầu cứ lo buôn bán, trả mẹ trước hết ngày {d.dueDay}."
                            : $"Nợ mẹ {Finance.Money(d.remaining)} — nhớ trả mẹ trước hết ngày {d.dueDay} (mẹ cần cho việc riêng).");
                else if (d.periodDays > 0)
                    MorningNotes.Add($"Nợ {d.name}: còn {Finance.Money(d.remaining)}, kỳ này cần trả thêm {Finance.Money(DebtSystem.DueThisPeriod(d))} trước hết ngày {d.nextDueDay}");
            }
            if (s.violationPoints > 0)
                MorningNotes.Add($"<color=#C62828>Điểm vi phạm: {s.violationPoints}/{Legal.PrisonPoints} (đủ {Legal.PrisonPoints} là đi tù; {Legal.DecayDays} ngày không vi phạm giảm 1 điểm).</color>");
            if (s.StoveBroken) MorningNotes.Add($"Bếp số 1 đang hỏng tới hết ngày {s.brokenStoveUntil}.");
            if (s.day <= s.staffAwayUntil && s.staff > 0) MorningNotes.Add($"Vy nghỉ tới hết ngày {s.staffAwayUntil}.");
            foreach (var name in s.helpers)
            {
                int d = OwedDays(name);
                if (d >= WageUrgeDays)
                    MorningNotes.Add($"<color=#C62828>{name} nhắc trả lương: đã nợ {d} ngày ({Finance.Money(OwedFor(name))}). Nợ tới {WageQuitDays} ngày là tự nghỉ, {WageQuitDays + 1} ngày là báo công an!</color>");
            }
            if (s.staff > 0 && OwedDays(VyName) >= WageUrgeDays)
                MorningNotes.Add($"<color=#C62828>Vy nhắc trả lương: đã nợ {OwedDays(VyName)} ngày ({Finance.Money(OwedFor(VyName))}).</color>");
            foreach (var dep in s.departed)
            {
                long owe = OwedFor(dep.who);
                if (owe > 0)
                    MorningNotes.Add($"<color=#C62828>{dep.who} đã nghỉ nhưng quán còn nợ {Finance.Money(owe)} — phải trả hết trong hôm nay (hạn ngày {dep.dueDay}), trễ là bị báo công an, phạt {Finance.Short(WageFine)}!</color>");
            }
            if (s.CustomerMul != 1f) MorningNotes.Add($"Lượng khách x{s.CustomerMul:0.##} tới hết ngày {s.tempCustomerUntil}.");
            if (s.TempPriceMul != 1f) MorningNotes.Add($"Giá nguyên liệu x{s.TempPriceMul:0.##} tới hết ngày {s.tempPriceUntil}.");
            if (s.TempDelayMul != 1f) MorningNotes.Add($"Hàng đặt về chậm x{s.TempDelayMul:0.#} tới hết ngày {s.tempDelayUntil}.");
            if (s.closedDay == s.day)
                MorningNotes.Add("<color=#C62828>HÔM NAY QUÁN PHẢI ĐÓNG CỬA.</color>");

            Customers.Reset();
            Kitchen.ResetDay();
            DayTime = 0;
            Phase = GamePhase.Prep;
            ui.ShowGame();
            MarkDirty();

            var lines = new List<DialogueLine>();
            FireEvents(false, lines);
            MomDeadlineBeat(lines);
            PlayDialogue(lines, () => RunChoices(() =>
            {
                if (PrisonPending) { GoToPrison(); return; }
                Save();
                MarkDirty();
            }));
        }

        void MomDeadlineBeat(List<DialogueLine> lines)
        {
            var s = State;
            var mom = s.GetDebt("me");
            if (s.day != 15 || mom == null || mom.remaining <= 0 || s.firedEvents.Contains("mom_d15")) return;
            s.firedEvents.Add("mom_d15");
            lines.Add(L("me", $"Con à, hôm nay 15 rồi. Mẹ cần dùng tới {Finance.Money(mom.remaining)} kia cho việc riêng, con thu xếp trả mẹ trong hôm nay nha."));
            Choice("Tiền của mẹ", "Mẹ nói cần tiền cho \"việc riêng\" mà không chịu nói rõ. Con có gặng hỏi mẹ không?",
                "Gặng hỏi mẹ", "Thôi, để con lo trả mẹ",
                r =>
                {
                    r.Add(L("anh", "Việc riêng gì mà gấp vậy mẹ? Mẹ nói con nghe với, con lo được mà."));
                    r.Add(L("me", "... Ừ thì... mẹ đi khám sức khỏe thôi con. Dạo này mẹ hay mệt trong người, bác sĩ hẹn đúng hôm nay tái khám. Con đừng lo, chắc không sao đâu."));
                    r.Add(L("anh", "Mẹ... sao mẹ lại giấu con chuyện này. Để con trả mẹ ngay, mẹ nhớ đi khám cho cẩn thận."));
                },
                r => r.Add(L("me", "Ừ, cảm ơn con. Con ngoan quá.")));
        }
        void MomDeadlineMiss(List<DialogueLine> lines)
        {
            var s = State;
            var mom = s.GetDebt("me");
            if (s.day != 15 || mom == null || mom.remaining <= 0 || s.firedEvents.Contains("mom_d15_miss")) return;
            s.firedEvents.Add("mom_d15_miss");
            lines.Add(L("me", "Chưa trả kịp cũng không sao đâu con, mẹ xoay tạm chỗ khác được. Con cứ lo cho quán, giữ gìn sức khỏe là mẹ mừng rồi."));
            lines.Add(L("anh", "Con xin lỗi mẹ... Con sẽ trả mẹ sớm nhất có thể."));
        }

        public void OpenShop()
        {
            if (Phase != GamePhase.Prep) return;
            if (State.closedDay == State.day) { Toast("Hôm nay quán phải đóng cửa."); return; }
            Phase = GamePhase.Open;
            DayTime = 0;
            State.stage = StageOpen;
            Save();
            Sfx.Play("open");
            Toast("Quán mở cửa! 9:00");
            if (State.priceAdjust > 0) Toast("Hôm nay quán TĂNG giá 10% — có thể khách sẽ thắc mắc, phàn nàn.");
            else if (State.priceAdjust < 0) Toast("Hôm nay quán GIẢM giá 10% — khách sẽ khoái đó!");
            MarkDirty();
        }

        public void CloseNow()
        {
            if (Phase == GamePhase.Prep)
            {
                if (State.closedDay == State.day) EndDay();
                return;
            }
            if (Phase != GamePhase.Open && Phase != GamePhase.Closing) return;
            foreach (var g in Customers.groups.ToList()) Customers.SendHome(g, "phải về vì quán đóng cửa");
            Phase = GamePhase.Closing;
            MarkDirty();
        }

        void Update()
        {
            if (State == null || Phase == GamePhase.Title) return;
            float dt = Time.deltaTime * Speed;
            if (Phase == GamePhase.Prep)
            {
                if (!DialogueActive && State.deliveries.Count > 0) Market.Tick(dt);
            }
            else if ((Phase == GamePhase.Open || Phase == GamePhase.Closing) && !IsPaused)
            {
                DayTime += dt;
                Market.Tick(dt);
                Customers.Tick(dt, Phase == GamePhase.Open && ClockMinutes < LastOrderMin);
                Kitchen.Tick(dt);
                if (Phase == GamePhase.Open && ClockMinutes >= CloseMin)
                {
                    Phase = GamePhase.Closing;
                    Toast("23:00 — Hết giờ bán, phục vụ nốt các bàn còn lại.");
                    MarkDirty();
                }
                if (Phase == GamePhase.Closing && Customers.groups.Count == 0 && Customers.leavers.Count == 0) EndDay();
            }
            TutorialTick();
        }

        void LateUpdate()
        {
            if (!dirty) return;
            dirty = false;
            Changed?.Invoke();
        }

        void EndDay()
        {
            var s = State;
            Phase = GamePhase.DayEnd;
            Sfx.Play("close");
            Customers.Reset();
            Kitchen.ResetDay();
            FinishLesson(false);

            AddLedger(Finance.CatRent, "Tiền mặt bằng", -s.rentPerDay);
            bool closedAllDay = s.closedDay == s.day;
            if (!closedAllDay)
            {
                AddLedger(Finance.CatUtility, "Điện, nước, gas", -s.utilityPerDay);
                long daySales = Finance.Build(s, s.day, s.day).revenue;
                long perStaff = (long)Math.Round(daySales * StaffShareRate / 1000.0) * 1000;
                for (int i = 0; i < s.helpers.Count; i++)
                {
                    if (i < s.helperDaysWorked.Count) s.helperDaysWorked[i]++;
                    if (perStaff > 0) s.wages.Add(new WageEntry { who = s.helpers[i], day = s.day, amount = perStaff });
                }
                if (s.ActiveStaff > 0 && perStaff > 0) s.wages.Add(new WageEntry { who = VyName, day = s.day, amount = perStaff });
            }

            var lines = new List<DialogueLine>();
            PayrollEndOfDay(lines);
            MomDeadlineMiss(lines);
            bool debtFail = DebtSystem.EndOfDay(this, lines);

            if (s.day % TaxSystem.PeriodDays == 0)
            {
                var b = TaxSystem.ClosePeriod(s, s.day - TaxSystem.PeriodDays + 1, s.day);
                lines.Add(L("lan",
                    $"Hết kỳ thuế ngày {b.fromDay}–{b.toDay}. Doanh thu kỳ {Finance.Money(b.revenue)}, quy ra cả năm ≈ {Finance.Money(b.annualized)}. " +
                    (b.aboveThreshold
                        ? $"Phải nộp: GTGT {Finance.Money(b.vat)}, TNCN {Finance.Money(b.pit)}."
                        : "Chưa vượt ngưỡng nên không phát sinh thuế, nhưng vẫn phải nộp tờ khai.") +
                    $" Hạn chót: hết ngày {b.dueDay}. Vào mục THUẾ để nộp."));
            }
            if (!PrisonPending) TaxSystem.EndOfDayCheck(this, lines);

            bool bankrupt = false;
            if (!PrisonPending)
            {
                if (s.cash < 0)
                {
                    s.negativeCashDays++;
                    bankrupt = s.negativeCashDays >= 3;
                    lines.Add(L("anh", $"Tiền mặt đang âm {Finance.Money(-s.cash)}... ({s.negativeCashDays}/3 ngày nữa là phá sản! Có thể vay ngân hàng trong SỔ SÁCH.)"));
                }
                else s.negativeCashDays = 0;
                FireEvents(true, lines);
            }
            MarkDirty();

            PlayDialogue(lines, () => RunChoices(() =>
            {
                CommitDayEnd(bankrupt, debtFail);
                ui.ShowDayReport(ApplyEndOutcome);
            }));
        }

        void CommitDayEnd(bool bankrupt, bool debtFail)
        {
            var s = State;
            long dayNet = Finance.Build(s, s.day, s.day).NetProfit;
            s.dailyPnl.RemoveAll(x => x.day == s.day);
            s.dailyPnl.Add(new DayPnL { day = s.day, net = dayNet });
            if (PrisonPending) { s.endOutcome = 1; s.endReason = prisonReason ?? ""; }
            else if (bankrupt) { s.endOutcome = 2; s.endReason = "PHÁ SẢN: tiền mặt âm 3 ngày liên tiếp."; }
            else if (debtFail) { s.endOutcome = 2; s.endReason = "Trễ hạn trả nợ 2 kỳ — chủ nợ đã siết quán."; }
            else if (!s.finished && s.ChapterDay >= Chapter.days) { s.endOutcome = 3; s.endReason = ""; }
            else { s.endOutcome = 0; s.endReason = ""; }
            s.stage = StageEnded;
            Save();
        }

        void ApplyEndOutcome()
        {
            var s = State;
            int outcome = s.endOutcome;
            string reason = s.endReason;
            s.endOutcome = 0;
            s.endReason = "";
            switch (outcome)
            {
                case 1:
                    if (prisonReason == null) prisonReason = string.IsNullOrEmpty(reason) ? "vi phạm nghiêm trọng" : reason;
                    GoToPrison();
                    break;
                case 2:
                    FailChapter(string.IsNullOrEmpty(reason) ? "Không đạt mục tiêu của chương." : reason);
                    break;
                case 3:
                    EvaluateChapter();
                    break;
                default:
                    NextDay();
                    break;
            }
        }

        void NextDay()
        {
            State.day++;
            StartPrep();
        }

        public static readonly (string id, string name, long cost, int max)[] Upgrades =
        {
            ("table", "Thêm 1 chỗ ngồi (bàn)", 800_000, CustomerSystem.MaxTables),
        };

        public const int MaxStoves = 9, MaxBars = 6, MaxOldUpgrades = 2;
        public static readonly float[] OldUpgradeMul = { 1f, 0.93f, 0.88f };
        public const long StoveUpgradeCost = 300_000, BarUpgradeCost = 85_000;
        public const long OldStoveValue = 1_200_000, OldBarValue = 600_000, TableResale = 400_000;
        public static readonly (string name, long cost, float mul, string desc, string sprite)[] StoveTiers =
        {
            ("Bếp ga cũ", 0, 1f, "bếp có sẵn từ ngày khai trương", "scene/stove"),
            ("Bếp gas đơn đời mới", 2_500_000, 0.85f, "inox sáng bóng, lửa xanh đều, nấu nhanh hơn bếp cũ 15%", "scene/stove_t1"),
            ("Bếp gas công nghiệp", 3_800_000, 0.7f, "3 họng lửa khè, chảo gang lớn, nấu nhanh hơn bếp cũ 30%", "scene/stove_t2"),
            ("Bếp từ cao cấp", 5_000_000, 0.55f, "mặt kính, nóng tức thì, nấu nhanh hơn bếp cũ 45%", "scene/stove_t3"),
        };

        public static string HelperSprite(string name)
        {
            int i = Array.IndexOf(HelperNames, name);
            return "nv" + (i < 0 ? 1 : i + 1);
        }

        public const int MaxHelpers = 10;
        public const double StaffShareRate = 0.035;
        public const string VyName = "Vy";
        static readonly string[] HelperNames = { "Tùng", "Hoa", "Minh", "Trang", "Đức", "Lan Anh", "Hải", "Yến", "Toàn", "My" };

        public static readonly (string name, long cost, float mul, int slots, string desc, string sprite)[] BarTiers =
        {
            ("Máy pha cũ", 0, 1f, 1, "máy có sẵn từ ngày khai trương, pha từng ly một", "scene/drinkbar"),
            ("Máy pha bán tự động", 1_000_000, 0.85f, 2, "2 vòi pha cùng lúc 2 ly, nhanh hơn máy cũ 15%", "scene/drinkmachine"),
            ("Máy pha công suất lớn", 2_000_000, 0.7f, 2, "bình lớn, bơm mạnh, pha cùng lúc 2 ly, nhanh hơn máy cũ 30%", "scene/drinkmachine_t2"),
            ("Máy pha tự động cao cấp", 3_000_000, 0.55f, 2, "cảm ứng, tự định lượng, pha cùng lúc 2 ly, nhanh hơn máy cũ 45%", "scene/drinkmachine_t3"),
        };

        static int At(List<int> list, int i, int fallback = 0) => list != null && i >= 0 && i < list.Count ? list[i] : fallback;

        public static long Resale(long value, int daysUsed) =>
            (long)Math.Round(value * (0.2 + 0.6 * Math.Pow(0.985, Math.Max(0, daysUsed))) / 10_000.0) * 10_000;

        public int StoveTier(int i) => Mathf.Clamp(At(State.stoveTiers, i), 0, StoveTiers.Length - 1);
        public int StoveLevel(int i) => Mathf.Clamp(At(State.stoveUpgraded, i), 0, MaxOldUpgrades);
        public float StoveMul(int i) => StoveTier(i) == 0 ? OldUpgradeMul[StoveLevel(i)] : StoveTiers[StoveTier(i)].mul;
        public int StoveDaysUsed(int i) => Math.Max(0, State.day - At(State.stoveDays, i, 1));
        public long StoveResale(int i) => Resale(StoveTier(i) == 0 ? OldStoveValue : StoveTiers[StoveTier(i)].cost, StoveDaysUsed(i));
        public string StoveLabel(int i) => StoveTier(i) == 0
            ? "bếp ga cũ" + (StoveLevel(i) > 0 ? $" nâng {StoveLevel(i)}/{MaxOldUpgrades}" : "")
            : StoveTiers[StoveTier(i)].name.ToLower();

        public int BarTier(int m) => Mathf.Clamp(At(State.barTiers, m), 0, BarTiers.Length - 1);
        public int BarLevel(int m) => Mathf.Clamp(At(State.barUpgraded, m), 0, MaxOldUpgrades);
        public float BarMul(int m) => BarTier(m) == 0 ? OldUpgradeMul[BarLevel(m)] : BarTiers[BarTier(m)].mul;
        public int BarDaysUsed(int m) => Math.Max(0, State.day - At(State.barDays, m, 1));
        public long BarResale(int m) => Resale(BarTier(m) == 0 ? OldBarValue : BarTiers[BarTier(m)].cost, BarDaysUsed(m));
        public string BarLabel(int m) => BarTier(m) == 0
            ? "máy pha cũ" + (BarLevel(m) > 0 ? $" nâng {BarLevel(m)}/{MaxOldUpgrades}" : "")
            : BarTiers[BarTier(m)].name.ToLower();
        float BarPower(int m) => BarTiers[BarTier(m)].slots / BarMul(m);

        public int StoveToReplace(int tier)
        {
            int best = -1;
            for (int i = 0; i < State.stoves; i++)
            {
                if (StoveMul(i) <= StoveTiers[tier].mul) continue;
                if (best < 0 || StoveMul(i) > StoveMul(best) || StoveMul(i) == StoveMul(best) && StoveResale(i) < StoveResale(best)) best = i;
            }
            return best;
        }

        public int BarToReplace(int tier)
        {
            float target = BarTiers[tier].slots / BarTiers[tier].mul;
            int best = -1;
            for (int m = 0; m < State.barTiers.Count; m++)
            {
                if (BarPower(m) >= target) continue;
                if (best < 0 || BarPower(m) < BarPower(best) || BarPower(m) == BarPower(best) && BarResale(m) < BarResale(best)) best = m;
            }
            return best;
        }

        public void BuyStove(int tier, int replace)
        {
            if (tier <= 0 || tier >= StoveTiers.Length) return;
            var t = StoveTiers[tier];
            if (replace < 0 && State.stoves >= MaxStoves) { Toast("Đã đủ 9 bếp: phải bán một bếp cũ mới lắp thêm được."); return; }
            long back = replace >= 0 ? StoveResale(replace) : 0;
            if (State.cash + back < t.cost) { Toast("Không đủ tiền!"); return; }
            if (replace >= 0)
            {
                AddLedger(Finance.CatAssetSale, $"Bán bếp {replace + 1} ({StoveLabel(replace)})", back);
                State.stoveTiers[replace] = tier;
                State.stoveUpgraded[replace] = 0;
                State.stoveDays[replace] = State.day;
            }
            else
            {
                State.stoves++;
                State.stoveTiers.Add(tier);
                State.stoveUpgraded.Add(0);
                State.stoveDays.Add(State.day);
            }
            AddLedger(Finance.CatInvest, "Mua " + t.name, -t.cost);
            Sfx.Play("coin");
            Kitchen.RebuildStations();
            Toast(replace >= 0 ? $"Đã bán bếp cũ được {Finance.Money(back)} và lắp {t.name} vào bếp {replace + 1}." : "Đã lắp thêm: " + t.name);
            MarkDirty();
        }

        public void BuyBar(int tier, int replace)
        {
            if (tier <= 0 || tier >= BarTiers.Length) return;
            var t = BarTiers[tier];
            if (replace < 0 && State.barTiers.Count >= MaxBars) { Toast("Đã đủ 6 máy pha: phải bán một máy cũ mới lắp thêm được."); return; }
            long back = replace >= 0 ? BarResale(replace) : 0;
            if (State.cash + back < t.cost) { Toast("Không đủ tiền!"); return; }
            if (replace >= 0)
            {
                AddLedger(Finance.CatAssetSale, $"Bán máy pha {replace + 1} ({BarLabel(replace)})", back);
                State.barTiers[replace] = tier;
                State.barUpgraded[replace] = 0;
                State.barDays[replace] = State.day;
            }
            else
            {
                State.barTiers.Add(tier);
                State.barUpgraded.Add(0);
                State.barDays.Add(State.day);
            }
            State.SyncCounts();
            AddLedger(Finance.CatInvest, "Mua " + t.name, -t.cost);
            Sfx.Play("coin");
            Kitchen.RebuildStations();
            Toast(replace >= 0 ? $"Đã bán máy cũ được {Finance.Money(back)} và lắp {t.name} vào chỗ máy pha {replace + 1}." : "Đã lắp thêm: " + t.name);
            MarkDirty();
        }

        public bool CanUpgradeStove(int i) => i >= 0 && i < State.stoves && StoveTier(i) == 0 && StoveLevel(i) < MaxOldUpgrades;

        public void UpgradeStove(int i)
        {
            if (!CanUpgradeStove(i)) { Toast("Bếp này không nâng cấp được nữa."); return; }
            if (State.cash < StoveUpgradeCost) { Toast("Không đủ tiền!"); return; }
            int lv = StoveLevel(i) + 1;
            AddLedger(Finance.CatInvest, $"Nâng cấp bếp {i + 1} (lần {lv})", -StoveUpgradeCost);
            Sfx.Play("coin");
            State.stoveUpgraded[i] = lv;
            Kitchen.RebuildStations();
            Toast($"Đã nâng cấp bếp {i + 1} lần {lv}: nấu nhanh hơn {Mathf.RoundToInt((1f - OldUpgradeMul[lv]) * 100)}% so với lúc chưa nâng.");
            MarkDirty();
        }

        public bool CanUpgradeBar(int m) => m >= 0 && m < State.barTiers.Count && BarTier(m) == 0 && BarLevel(m) < MaxOldUpgrades;

        public void UpgradeBar(int m)
        {
            if (!CanUpgradeBar(m)) { Toast("Máy này không nâng cấp được nữa."); return; }
            if (State.cash < BarUpgradeCost) { Toast("Không đủ tiền!"); return; }
            int lv = BarLevel(m) + 1;
            AddLedger(Finance.CatInvest, $"Nâng cấp máy pha {m + 1} (lần {lv})", -BarUpgradeCost);
            Sfx.Play("coin");
            State.barUpgraded[m] = lv;
            Kitchen.RebuildStations();
            Toast($"Đã nâng cấp máy pha {m + 1} lần {lv}: pha nhanh hơn {Mathf.RoundToInt((1f - OldUpgradeMul[lv]) * 100)}% so với lúc chưa nâng.");
            MarkDirty();
        }

        public void RemoveStoveAt(int i)
        {
            if (i < 0 || i >= State.stoves) return;
            State.stoves--;
            State.stoveTiers.RemoveAt(i);
            State.stoveUpgraded.RemoveAt(i);
            State.stoveDays.RemoveAt(i);
        }

        public void RemoveBarAt(int m)
        {
            if (m < 0 || m >= State.barTiers.Count) return;
            State.barTiers.RemoveAt(m);
            State.barUpgraded.RemoveAt(m);
            State.barDays.RemoveAt(m);
            State.SyncCounts();
        }

        public int StaffInArea(int area) => State.helperAreas.Count(a => a == area);

        public bool AreaUsable(int area) => area == 0 || area > 0 && area < CustomerSystem.Areas && State.AreaHasTables(area);

        public int FreeArea()
        {
            int best = -1;
            for (int a = 1; a < CustomerSystem.Areas; a++)
                if (AreaUsable(a) && StaffInArea(a) < CustomerSystem.StaffMax(a) && (best < 0 || StaffInArea(a) < StaffInArea(best))) best = a;
            if (best < 0 && StaffInArea(0) < CustomerSystem.FrontStaffMax) best = 0;
            return best;
        }

        public string AreaLabel(int area) => area >= 0 && area < CustomerSystem.Areas ? CustomerSystem.AreaNames[area] : "chưa có khu";

        public void HireHelper()
        {
            if (State.helpers.Count >= MaxHelpers) { Toast($"Đã đủ tối đa {MaxHelpers} nhân viên."); return; }
            int area = FreeArea();
            if (area < 0) { Toast("Các khu đã đủ người (Sân trước tối đa 2, mỗi khu khác tối đa 3). Mua thêm bàn ở khu khác để mở khu mới."); return; }
            var name = HelperNames.Where(n => !State.helpers.Contains(n)).OrderBy(_ => rng.Next()).First();
            State.helpers.Add(name);
            State.helperAreas.Add(area);
            State.helperDaysWorked.Add(0);
            Customers.AssignHelpers();
            Sfx.Play("coin");
            Toast($"Đã thuê {name}, phụ trách {AreaLabel(area)}. Lương = 3,5% doanh thu bán hàng mỗi ngày, cộng dồn trả mỗi 7 ngày.");
            MarkDirty();
        }

        public bool CanFireStaff => Phase == GamePhase.Prep || Phase == GamePhase.DayEnd;

        public void FireHelper(string name)
        {
            if (!CanFireStaff) { Toast("Chỉ cho nghỉ được khi đã hết ngày bán (buổi sáng trước khi mở cửa)."); return; }
            int i = State.helpers.IndexOf(name);
            if (i < 0) return;
            if (OwedFor(name) > 0)
            {
                Sfx.Play("error");
                Toast($"Phải trả hết {Finance.Money(OwedFor(name))} lương ({OwedDays(name)} ngày) cho {name} rồi mới cho nghỉ được.");
                return;
            }
            State.helpers.RemoveAt(i);
            if (i < State.helperAreas.Count) State.helperAreas.RemoveAt(i);
            if (i < State.helperDaysWorked.Count) State.helperDaysWorked.RemoveAt(i);
            Customers.AssignHelpers();
            Toast($"{name} đã nghỉ việc.");
            MarkDirty();
        }

        public int NextAreaFor(string name)
        {
            int i = State.helpers.IndexOf(name);
            if (i < 0) return -1;
            int cur = State.helperAreas[i];
            for (int step = 1; step <= CustomerSystem.Areas; step++)
            {
                int a = ((cur < 0 ? 0 : cur) + step) % CustomerSystem.Areas;
                if (a == cur) continue;
                if (AreaUsable(a) && StaffInArea(a) < CustomerSystem.StaffMax(a)) return a;
            }
            return -1;
        }

        public void MoveHelper(string name)
        {
            int i = State.helpers.IndexOf(name), a = NextAreaFor(name);
            if (i < 0 || a < 0) { Toast("Các khu khác đã đủ người."); return; }
            State.helperAreas[i] = a;
            Customers.AssignHelpers();
            Toast($"{name} chuyển sang phụ trách {AreaLabel(a)}.");
            MarkDirty();
        }

        public long WagesOwed => State.wages.Sum(w => w.amount);

        public long OwedFor(string who) => State.wages.Where(w => w.who == who).Sum(w => w.amount);

        public int OwedDays(string who) => State.wages.Count(w => w.who == who);

        public int DaysWorked(string name)
        {
            int i = State.helpers.IndexOf(name);
            return i >= 0 && i < State.helperDaysWorked.Count ? State.helperDaysWorked[i] : 0;
        }

        public (int day, long amount) NextWage(string who)
        {
            var e = State.wages.Where(w => w.who == who).OrderBy(w => w.day).FirstOrDefault();
            return e == null ? (-1, 0L) : (e.day, e.amount);
        }

        public const long WageFine = 10_000_000;
        public const int WageUrgeDays = 7, WageQuitDays = 9, WagePayCycle = 7;

        void AfterWagePaid()
        {
            State.departed.RemoveAll(d => OwedFor(d.who) <= 0);
        }

        public void PayWageDay(string who)
        {
            var e = State.wages.Where(w => w.who == who).OrderBy(w => w.day).FirstOrDefault();
            if (e == null) { Toast("Nhân viên này không còn nợ lương."); return; }
            if (State.cash < e.amount) { Sfx.Play("error"); Toast("Không đủ tiền mặt!"); return; }
            AddLedger(Finance.CatStaff, $"Trả lương {who} ngày thứ {e.day}", -e.amount);
            State.wages.Remove(e);
            AfterWagePaid();
            Sfx.Play("coin");
            Toast($"Đã trả lương {who} ngày thứ {e.day}: {Finance.Money(e.amount)}.");
            MarkDirty();
        }

        public void PayWageAll(string who)
        {
            long total = OwedFor(who);
            if (total <= 0) { Toast("Nhân viên này không còn nợ lương."); return; }
            if (State.cash < total) { Sfx.Play("error"); Toast("Không đủ tiền mặt!"); return; }
            int days = OwedDays(who);
            AddLedger(Finance.CatStaff, $"Trả toàn bộ lương {who} ({days} ngày)", -total);
            State.wages.RemoveAll(w => w.who == who);
            AfterWagePaid();
            Sfx.Play("coin");
            Toast($"Đã trả toàn bộ lương {who}: {Finance.Money(total)}.");
            MarkDirty();
        }

        public void PayWages()
        {
            long total = WagesOwed;
            if (total <= 0) { Toast("Hiện không nợ lương ai."); return; }
            if (State.cash < total) { Sfx.Play("error"); Toast("Không đủ tiền mặt để trả lương!"); return; }
            AddLedger(Finance.CatStaff, "Trả lương toàn bộ nhân viên", -total);
            State.wages.Clear();
            AfterWagePaid();
            Sfx.Play("coin");
            Toast($"Đã trả lương {Finance.Money(total)}.");
            MarkDirty();
        }

        void PayrollEndOfDay(List<DialogueLine> lines)
        {
            var s = State;
            foreach (var name in s.helpers.ToList())
                if (OwedDays(name) >= WageQuitDays) QuitForUnpaid(name, lines);
            if (s.staff > 0 && OwedDays(VyName) >= WageQuitDays)
            {
                s.staff = 0;
                s.departed.Add(new DepartedDebtor { who = VyName, dueDay = s.day + 1 });
                lines.Add(L("vy", $"Em bị nợ lương {OwedDays(VyName)} ngày rồi, em xin nghỉ. Quán trả hết {Finance.Money(OwedFor(VyName))} cho em trước hết ngày {s.day + 1}, không thì em báo công an."));
            }
            foreach (var dep in s.departed.ToList())
            {
                long owe = OwedFor(dep.who);
                if (owe <= 0) { s.departed.Remove(dep); continue; }
                if (s.day < dep.dueDay) continue;
                lines.Add(L("congan", $"{dep.who} trình báo quán nợ lương {Finance.Money(owe)} quá hạn. Phạt hành chính {Finance.Money(WageFine)} và buộc bồi thường đủ lương!"));
                AddLedger(Finance.CatFine, $"Phạt nợ lương {dep.who}", -WageFine);
                AddLedger(Finance.CatStaff, $"Bồi thường lương {dep.who} (bị buộc trả)", -owe);
                s.wages.RemoveAll(w => w.who == dep.who);
                s.departed.Remove(dep);
                Legal.AddViolation(this, 1, $"nợ lương {dep.who} quá hạn", lines);
                if (s.cash < 0) Legal.Seize(this, "nợ lương nhân viên", lines);
            }
        }

        void QuitForUnpaid(string name, List<DialogueLine> lines)
        {
            var s = State;
            int i = s.helpers.IndexOf(name);
            if (i < 0) return;
            int d = OwedDays(name);
            long owe = OwedFor(name);
            s.helpers.RemoveAt(i);
            if (i < s.helperAreas.Count) s.helperAreas.RemoveAt(i);
            if (i < s.helperDaysWorked.Count) s.helperDaysWorked.RemoveAt(i);
            s.departed.Add(new DepartedDebtor { who = name, dueDay = s.day + 1 });
            Customers.AssignHelpers();
            lines.Add(L("narr", $"{name} đã TỰ Ý NGHỈ VIỆC vì bị nợ lương {d} ngày ({Finance.Money(owe)}). Quán phải trả hết trước hết ngày {s.day + 1}, nếu không {name} sẽ báo công an (phạt {Finance.Short(WageFine)})."));
        }

        public int UpgradeLevel(string id) => id == "stove" ? State.stoves : State.tables;
        public int UpgradeMax(string id) => Upgrades.First(x => x.id == id).max;

        public void BuyUpgrade(string id)
        {
            if (id != "table") return;
            for (int t = 0; t < CustomerSystem.MaxTables; t++)
                if (!State.HasTable(t)) { BuyTable(t); return; }
            Toast("Đã đạt tối đa.");
        }

        public void BuyTable(int t)
        {
            var u = Upgrades.First(x => x.id == "table");
            if (t < 0 || t >= CustomerSystem.MaxTables || State.HasTable(t)) return;
            if (State.cash < u.cost) { Toast("Không đủ tiền!"); return; }
            AddLedger(Finance.CatInvest, $"Mua bàn số {t + 1}", -u.cost);
            Sfx.Play("coin");
            State.tableSlots.Add(t);
            State.tableSlots.Sort();
            State.SyncCounts();
            Customers.AssignHelpers();
            Toast($"Đã mua bàn số {t + 1} ({CustomerSystem.AreaNames[CustomerSystem.AreaOf(t)]}).");
            MarkDirty();
        }

        public void BackToTitle()
        {
            Phase = GamePhase.Title;
            prisonReason = null;
            choices.Clear();
            Customers.Reset();
            Kitchen.ResetDay();
            ui.ShowTitle();
        }
    }
}