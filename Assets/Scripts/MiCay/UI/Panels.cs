using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MiCay.UI
{
    public partial class MainUI
    {
        class Modal
        {
            public GameObject go;
            public RectTransform body;
            public Action<RectTransform> build;
            public Action onClose;
            public bool pauses;
            public Func<string> liveKey;
            public string lastKey;
        }

        Modal modal;
        int marketTab, financeTab, reportTab;
        Button modalCloseBtn, modalOkBtn;
        RectTransform marketTabsRow, financeTabsRow;
        Button taxPayBtn;
        readonly Button[] priceBtns = new Button[3];
        readonly Dictionary<string, Button> orderBtns = new Dictionary<string, Button>();
        readonly Dictionary<string, Button> retailBtns = new Dictionary<string, Button>();
        readonly Dictionary<string, int> buyQty = new Dictionary<string, int>();

        void OpenModal(string title, Action<RectTransform> build, bool closable = true, Action onClose = null,
            float x0 = 0.04f, float y0 = 0.04f, float x1 = 0.96f, float y1 = 0.96f, bool pauses = true, Func<string> liveKey = null)
        {
            CloseModal(false);
            gm.ModalCount++;
            if (pauses) gm.PausingModals++;
            var dim = UIKit.Box(modalLayer, Pal.Dim, false, "Modal");
            dim.rectTransform.Fill();
            var win = UIKit.Box(dim.transform, Pal.Paper, true, "Window");
            win.rectTransform.Anchors(x0, y0, x1, y1);
            if (Art.Paper != null) { Art.Sliced(win, Art.Paper); win.color = Color.white; }

            var head = UIKit.Box(win.transform, Pal.WoodDark, true, "Head");
            if (Art.Btn != null) head.sprite = Art.Btn;
            head.rectTransform.Anchors(0, 0.9f, 1, 1);
            var t = UIKit.Label(head.transform, title, 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            t.rectTransform.Anchors(0.02f, 0, 0.85f, 1);
            modalCloseBtn = null;
            modalOkBtn = null;
            marketTabsRow = null;
            financeTabsRow = null;
            taxPayBtn = null;
            orderBtns.Clear();
            retailBtns.Clear();
            if (closable)
            {
                var close = UIKit.Button(head.transform, "ĐÓNG", () => CloseModal(true), Pal.Red, 26);
                modalCloseBtn = close;
                ((RectTransform)close.transform).Anchors(0.87f, 0.12f, 0.99f, 0.88f);
            }
            var body = UIKit.NewRect("Body", win.transform).Anchors(0.01f, 0.015f, 0.99f, 0.885f);
            modal = new Modal { go = dim.gameObject, body = body, build = build, onClose = onClose, pauses = pauses, liveKey = liveKey };
            modal.lastKey = liveKey?.Invoke();
            build(body);
        }

        void CloseModal(bool invoke)
        {
            if (modal == null) return;
            var m = modal;
            modal = null;
            Destroy(m.go);
            gm.ModalCount = Mathf.Max(0, gm.ModalCount - 1);
            if (m.pauses) gm.PausingModals = Mathf.Max(0, gm.PausingModals - 1);
            if (invoke) gm.TutorialSignal("close");
            if (invoke) m.onClose?.Invoke();
        }

        void RefreshModal()
        {
            if (modal == null) return;
            if (modal.liveKey != null)
            {
                string key = modal.liveKey();
                if (key == modal.lastKey) return;
                modal.lastKey = key;
            }
            RebuildModal();
        }

        void RebuildModal()
        {
            if (modal == null) return;
            var sr = modal.body.GetComponentInChildren<ScrollRect>();
            float pos = sr != null ? sr.verticalNormalizedPosition : 1f;
            UIKit.Clear(modal.body);
            modal.build(modal.body);
            var sr2 = modal.body.GetComponentsInChildren<ScrollRect>().FirstOrDefault(x => x.gameObject.activeInHierarchy);
            if (sr2 != null)
            {
                Canvas.ForceUpdateCanvases();
                sr2.verticalNormalizedPosition = pos;
            }
        }

        static RectTransform Tabs(RectTransform body, string[] names, int current, Action<int> onTab, float y0 = 0.9f)
        {
            var row = UIKit.NewRect("Tabs", body).Anchors(0, y0, 1, 1);
            var h = UIKit.HList(row, 8, 2);
            h.childForceExpandWidth = true;
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                UIKit.Button(row, names[i], () => onTab(idx), i == current ? Pal.Red : Pal.Wood, 24);
            }
            return row;
        }

        public void ShowMessage(string title, string text, Action onOk)
        {
            OpenModal(title, body =>
            {
                var t = UIKit.Label(body, text, 30, Pal.Text, TextAnchor.MiddleCenter);
                t.rectTransform.Anchors(0.04f, 0.25f, 0.96f, 0.98f);
                t.Fit(30, 16);
                var ok = UIKit.Button(body, "TIẾP TỤC", () => CloseModal(true), Pal.Orange, 32);
                ((RectTransform)ok.transform).Anchors(0.35f, 0.04f, 0.65f, 0.2f);
                modalOkBtn = ok;
            }, false, onOk, 0.15f, 0.12f, 0.85f, 0.88f);
        }

        void ShowConfirm(string title, string text, string yes, Action onYes, Action onNo = null)
        {
            OpenModal(title, body =>
            {
                var t = UIKit.Label(body, text, 30, Pal.Text, TextAnchor.MiddleCenter);
                t.rectTransform.Anchors(0.04f, 0.3f, 0.96f, 0.95f);
                var y = UIKit.Button(body, yes, () => { CloseModal(false); onYes(); }, Pal.Red, 30);
                ((RectTransform)y.transform).Anchors(0.1f, 0.05f, 0.45f, 0.25f);
                var n = UIKit.Button(body, "Bỏ qua", () => { CloseModal(false); onNo?.Invoke(); }, Pal.Gray, 30);
                ((RectTransform)n.transform).Anchors(0.55f, 0.05f, 0.9f, 0.25f);
            }, false, null, 0.25f, 0.25f, 0.75f, 0.75f);
        }

        public void ShowChoice(string title, string text, string yes, string no, Action onYes, Action onNo)
        {
            OpenModal(title, body =>
            {
                var t = UIKit.Label(body, text, 28, Pal.Text, TextAnchor.MiddleCenter);
                t.rectTransform.Anchors(0.04f, 0.3f, 0.96f, 0.95f);
                t.Fit(28, 14);
                var y = UIKit.Button(body, yes, () => { CloseModal(false); onYes(); }, Pal.Red, 28);
                ((RectTransform)y.transform).Anchors(0.06f, 0.05f, 0.47f, 0.25f);
                var n = UIKit.Button(body, no, () => { CloseModal(false); onNo(); }, Pal.Blue, 28);
                ((RectTransform)n.transform).Anchors(0.53f, 0.05f, 0.94f, 0.25f);
            }, false, null, 0.2f, 0.22f, 0.8f, 0.78f);
        }

        void OpenPauseMenu()
        {
            OpenModal("Tạm dừng", body =>
            {
                var col = UIKit.NewRect("Col", body).Anchors(0.2f, 0.05f, 0.8f, 0.95f);
                UIKit.VList(col, 16, 0, TextAnchor.MiddleCenter);
                UIKit.Button(col, "Tiếp tục chơi", () => CloseModal(true), Pal.Orange, 30).Size(h: 80);
                UIKit.Button(col, "Hướng dẫn", () => ShowMessage("HƯỚNG DẪN", HelpText, null), Pal.Blue, 30).Size(h: 80);
                UIKit.Button(col, Sfx.MusicEnabled ? "Nhạc nền: BẬT" : "Nhạc nền: TẮT", () =>
                {
                    Sfx.MusicEnabled = !Sfx.MusicEnabled;
                    RebuildModal();
                }, Sfx.MusicEnabled ? Pal.Teal : Pal.Gray, 30).Size(h: 80);
                UIKit.Button(col, Sfx.Enabled ? "Hiệu ứng âm thanh: BẬT" : "Hiệu ứng âm thanh: TẮT", () =>
                {
                    ToggleSound();
                    RebuildModal();
                }, Sfx.Enabled ? Pal.Teal : Pal.Gray, 30).Size(h: 80);
                UIKit.Button(col, "Sao lưu / khôi phục", OpenBackup, Pal.Blue, 30).Size(h: 70);
                UIKit.Button(col, "Về màn hình chính", () =>
                    ShowConfirm("Về màn hình chính?", "Game tự lưu vào mỗi buổi sáng. Tiến độ trong ngày hôm nay sẽ mất.", "Đồng ý", gm.BackToTitle),
                    Pal.Gray, 30).Size(h: 80);
            }, true, null, 0.3f, 0.1f, 0.7f, 0.9f);
        }

        const string HelpText =
            "<b>1. Nhập hàng:</b> mỗi dòng có ô số lượng: bấm + để tăng rồi bấm ĐẶT SỈ (theo thùng/túi/bao, giá gốc, hàng về sau 1 phút) hoặc MUA LẺ (theo kg/gói, đắt hơn và mất thêm tiền xe ôm chạy chợ, về sau 5 giây). Sáng ngày 1 hàng có ngay. Hàng đặt chưa về lúc đóng cửa sẽ về trong đêm. Mỗi loại chỉ để được vài ngày trong tủ.\n" +
            "<b>2. Mở cửa:</b> khách ngồi vào bàn và gọi món. Chạm món trong bong bóng của khách > chạm thẻ nguyên liệu để cho vào nồi > bấm CHẾ BIẾN (đồ uống thì bấm PHA CHẾ). Chờ thanh tiến độ chạy xong.\n" +
            "<b>3. Độ cay:</b> mì cay cấp N cần đúng N muỗng bột ớt (chạm thẻ BỘT ỚT N lần). Sai độ cay khách vẫn ăn nhưng chê. Sai nguyên liệu là HỎNG món.\n" +
            "<b>Khu ăn uống:</b> vuốt ngang để xem khu mở rộng (bàn 9-16). Mũi tên đỏ ở mép cho biết bên đó có bao nhiêu bàn đang chờ.\n" +
            "<b>4. Giao món:</b> khi bàn đủ món, bấm GIAO MÓN. Khách ăn xong mới thanh toán. Khách hết kiên nhẫn sẽ bỏ về.\n" +
            "<b>Đánh giá:</b> khách chấm theo màu thanh chờ lúc nhận món: xanh 5 sao (còn dưới 10 giây là sang cam thì 4 sao), cam 3 sao, đỏ 2 sao, hết thanh thì bỏ về 1 sao. Dùng nguyên liệu sắp hết hạn bị trừ 1 hoặc 2 sao tùy khách, sai độ cay bị trừ thêm.\n" +
            "<b>5. Thuế:</b> mỗi 7 ngày là 1 kỳ, phải khai & nộp trong 3 ngày. Trễ lâu bị cưỡng chế, tịch thu tài sản; tái phạm là ĐI TÙ.\n" +
            "<b>Pháp luật:</b> vi phạm (thuế, vệ sinh, PCCC, nợ ngân hàng...) bị cộng điểm. Đủ 6 điểm là đi tù và làm lại từ ngày 1 với khoản vay ngân hàng.\n" +
            "<b>Ngân hàng:</b> SỔ SÁCH > NGÂN HÀNG cho vay 7 hoặc 14 ngày. Trả đúng hạn, trễ quá 3 ngày bị cưỡng chế.\n" +
            "<b>Dịch vụ:</b> mua thêm bếp (gas đơn / gas công nghiệp / bếp từ, bếp càng xịn nấu càng nhanh), máy pha đồ uống (3 loại, pha cùng lúc 2 ly), chỗ ngồi và thuê nhân viên (tối đa 10 người: Sân trước 2, mỗi khu khác 3; lương cộng dồn trả mỗi 7 ngày, trễ bị phạt nặng).\n" +
            "<b>6. Ghép nhanh:</b> nấu đúng một món 3 lần sẽ thuộc công thức, mở nút GHÉP NHANH. Khách có yêu cầu riêng (dấu − xanh: bỏ bớt món, không đá; dấu + hồng: thêm topping) thì KHÔNG ghép nhanh được, phải tự cho từng nguyên liệu.";

        static string Stars(int n) => $"<color=#FFB300>{n}/5 sao</color>";

        static void StarIcons(Transform parent, int n)
        {
            var holder = UIKit.NewRect("Stars", parent);
            holder.Size(w: 150);
            UIKit.HList(holder, 2, 0, TextAnchor.MiddleLeft);
            for (int i = 0; i < 5; i++)
                Art.Pixel(holder, Art.Get(i < n ? "ui/star_full" : "ui/star_empty"), 2.2f, "Star").Size(w: 26);
        }

        string ReviewStripText()
        {
            var list = S.Today.reviews;
            if (list.Count == 0) return "<color=#FFD27F>Đánh giá hôm nay:</color> chưa có — bấm vào đây để xem đánh giá của khách";
            var last = list[list.Count - 1];
            return $"<color=#FFD27F>Đánh giá hôm nay:</color> {list.Average(r => r.stars):0.0} sao ({list.Count} lượt)   •   Bàn {last.table}: {Stars(last.stars)} \"{last.text}\"   <color=#9CC8FF>[Xem]</color>";
        }

        int reviewDayOffset;

        void OpenReviews()
        {
            reviewDayOffset = 0;
            OpenModal("ĐÁNH GIÁ CỦA KHÁCH", BuildReviews);
        }

        void BuildReviews(RectTransform body)
        {
            var days = S.stats.Where(st => st.reviews.Count > 0 || st.day == S.day).OrderByDescending(st => st.day).Take(7).ToList();
            if (days.Count == 0) days.Add(S.Today);
            reviewDayOffset = Mathf.Clamp(reviewDayOffset, 0, days.Count - 1);
            Tabs(body, days.Select(st => st.day == S.day ? "Hôm nay" : $"Ngày {st.day}").ToArray(), reviewDayOffset,
                i => { reviewDayOffset = i; RebuildModal(); }, 0.91f);
            var stat = days[reviewDayOffset];
            var list = stat.reviews;
            string head = list.Count == 0
                ? "Chưa có đánh giá nào."
                : $"<b>Trung bình {list.Average(r => r.stars):0.0} sao</b> từ {list.Count} lượt   •   " +
                  string.Join("   ", Enumerable.Range(1, 5).Reverse().Select(n => $"{n} sao: {list.Count(r => r.stars == n)}")) +
                  "\n<size=18>Sao phụ thuộc vào thời gian chờ (kể cả lúc chờ mang món ra) và độ ngon: nguyên liệu càng gần hết hạn, món càng kém ngon.</size>";
            var info = UIKit.Label(body, head, 22, Pal.Text);
            info.rectTransform.Anchors(0.01f, 0.8f, 0.99f, 0.9f);
            info.Fit(22, 12);
            var area = UIKit.NewRect("Area", body).Anchors(0, 0, 1, 0.79f);
            var scroll = UIKit.ScrollList(area, 4, 4);
            ((RectTransform)scroll.parent.parent).Fill();
            foreach (var r in list.AsEnumerable().Reverse())
            {
                var row = UIKit.Box(scroll, r.stars <= 2 ? new Color(1f, 0.9f, 0.88f) : r.stars >= 5 ? new Color(0.9f, 1f, 0.9f) : Color.white, true, "Review");
                row.Size(h: 56);
                UIKit.HList(row, 10, 6);
                int hh = Mathf.FloorToInt(r.clock / 60f), mm = Mathf.FloorToInt(r.clock % 60f);
                UIKit.Label(row.transform, $"{hh:00}:{mm:00}", 20, Pal.Gray).Size(w: 70);
                UIKit.Label(row.transform, $"Bàn {r.table} • {r.name}", 20).Size(w: 230);
                StarIcons(row.transform, r.stars);
                UIKit.Label(row.transform, r.text, 20).Size(w: 600, flexW: 1);
            }
        }

        void OpenMarket()
        {
            buyQty.Clear();
            OpenModal("NHẬP HÀNG & KHO", BuildMarket, pauses: false, liveKey: MarketKey);
            gm.TutorialSignal("market");
        }

        string MarketKey()
        {
            var sb = new StringBuilder();
            sb.Append(S.cash).Append('|').Append(gm.Phase).Append('|').Append(gm.TutorialStepId);
            foreach (var b in S.inventory) sb.Append('|').Append(b.ing).Append(b.qty);
            foreach (var d in S.deliveries) sb.Append('|').Append(d.ing).Append(d.qty).Append('/').Append(Mathf.CeilToInt(d.arriveTimer / 5f));
            return sb.ToString();
        }

        void BuildMarket(RectTransform body)
        {
            orderBtns.Clear();
            retailBtns.Clear();
            marketTabsRow = Tabs(body, new[] { "Nguyên liệu nấu ăn", "Nguyên liệu pha chế", "Kho & hàng đang về" }, marketTab,
                i => { marketTab = i; RebuildModal(); gm.TutorialSignal("mtab" + i); }, 0.91f);
            var ch = gm.Chapter;
            var m = gm.Market;
            float priceUp = ch.ingredientPriceMul * S.TempPriceMul;
            float slow = ch.orderDelayMul * S.TempDelayMul;
            string rules = m.Instant
                ? "<color=#1B7F2A>SÁNG NAY (ngày 1) mua gì cũng có NGAY.</color> Sau khi mở cửa: ĐẶT SỈ chờ 1 phút, MUA LẺ 5 giây là có nhưng đắt hơn."
                : $"ĐẶT SỈ: giá gốc, theo thùng/túi/bao, chờ 1 phút{(slow > 1.01f ? $" <color=#C62828>(đang chậm x{slow:0.#})</color>" : "")}   •   MUA LẺ: theo kg/gói, đắt hơn + {Finance.Short(Market.RetailRunFee)} xe ôm mỗi lần, chờ {Market.RetailDelaySeconds:0} giây";
            var info = UIKit.Label(body,
                $"Tiền mặt: <b>{Finance.Money(S.cash)}</b>   •   {rules}" +
                (priceUp > 1.01f ? $"   •   <color=#C62828>Giá đang tăng {Mathf.RoundToInt((priceUp - 1) * 100)}%</color>" : ""),
                21, Pal.Text);
            info.rectTransform.Anchors(0.01f, 0.84f, 0.99f, 0.905f);
            info.Fit(21, 12);

            float listTop = 0.835f;
            bool buying = gm.TutorialStepId == "buy";
            var next = buying ? gm.TutorialNextIngredient : null;

            var area = UIKit.NewRect("Area", body).Anchors(0, 0, 1, listTop);
            var list = UIKit.ScrollList(area, 6, 4);
            ((RectTransform)list.parent.parent).Fill();

            if (marketTab == 2) { BuildStock(list); return; }
            bool drinks = marketTab == 1;
            var rows = GameDB.UnlockedIngredients(S.chapter).Where(x => x.IsDrink == drinks);
            if (buying)
                rows = rows.OrderBy(x => Inventory.Count(S, x.id) + gm.Market.PendingQty(x.id) > 0 ? 1 : 0);
            foreach (var d in rows) MarketRow(list, d, next != null && d.id == next.id);
        }

        void MarketRow(RectTransform list, IngredientDef d, bool highlight = false)
        {
            var m = gm.Market;
            int stock = Inventory.Count(S, d.id);
            int pending = m.PendingQty(d.id);
            int exp = Inventory.NearestExpiry(S, d.id);
            bool avail = m.RetailAvailable(d.id);

            var row = UIKit.Box(list, highlight ? new Color(1f, 0.93f, 0.45f) : stock < 5 ? new Color(1f, 0.9f, 0.88f) : Color.white, true, "Row");
            row.Size(h: 116);
            UIKit.HList(row, 8, 6);

            Art.Pixel(row.transform, Art.Ing(d.id), 2.6f, "Icon").Size(w: 56);
            var name = UIKit.Label(row.transform, $"<b>{d.name}</b>\n<size=17>1 phần = {d.unit}\nđể được {d.shelfDays} ngày</size>", 22);
            name.Size(w: 220, flexW: 1);
            string stockLine = pending > 0 ? $"\n<size=17>đang về +{pending}</size>" : exp < int.MaxValue ? $"\n<size=17>dùng tới hết ngày {exp}</size>" : "";
            UIKit.Label(row.transform, $"Kho: <b>{stock}</b> phần{stockLine}", 22, stock < 5 ? Pal.Red : Pal.Text).Size(w: 160);

            var buy = UIKit.NewRect("Buy", row.transform);
            buy.Size(w: 800);
            UIKit.VList(buy, 4, 0, TextAnchor.MiddleLeft);

            string retailWait = m.Instant ? "có ngay" : $"{Market.RetailDelaySeconds:0} giây";
            string retailInfo = avail
                ? $"<b>Lẻ:</b> {Finance.Money(m.RetailPrice(d.id))}/{d.retailUnit}\n<size=16>= {d.retailQty} phần • +{Finance.Short(Market.RetailRunFee)} xe ôm • {retailWait}</size>"
                : "<color=#C62828>Chợ hôm nay hết hàng lẻ</color>";
            var retailBuy = QtyLine(buy, "r_" + d.id, retailInfo, avail, Pal.Orange,
                n => $"MUA LẺ {Finance.Short(m.RetailCost(d.id, n))}",
                n => { if (m.BuyRetail(d.id, n)) { gm.TutorialSignal("retail"); return true; } return false; });
            if (avail)
            {
                retailBtns[d.id] = retailBuy;
            }

            string orderWait = m.Instant ? "có ngay" : Market.Wait(m.OrderSeconds(d.id));
            string packInfo = gm.Day1Stocking
                ? $"<b>Sỉ:</b> {Finance.Money(m.WholesaleUnit(d.id))}/phần\n<size=16>hôm nay nhập vừa đủ bán: {m.PackQty(d.id)} phần • {orderWait}</size>"
                : $"<b>Sỉ:</b> {Finance.Money(m.WholesalePack(d.id))}/{d.packUnit}\n<size=16>1 {d.packUnit} = <b>{d.pack} phần</b>{(GameDB.AmountText(d, d.pack) is string amt ? $" ({amt})" : "")} • {orderWait} • đặt 3 −5%, 5 −10%</size>";
            var packBuy = QtyLine(buy, "p_" + d.id, packInfo, true, Pal.Teal,
                n => Market.BulkDiscount(n) > 0 && !gm.Day1Stocking
                    ? $"ĐẶT SỈ {Finance.Short(m.WholesaleCost(d.id, n))} (−{Market.BulkDiscount(n) * 100:0}%)"
                    : $"ĐẶT SỈ {Finance.Short(m.WholesaleCost(d.id, n))}",
                n => { if (m.BuyWholesale(d.id, n)) { gm.TutorialSignal("order"); return true; } return false; });
            orderBtns[d.id] = packBuy;
        }

        Button QtyLine(RectTransform parent, string key, string info, bool enabled, Color color, Func<int, string> caption, Func<int, bool> buy)
        {
            var line = UIKit.NewRect("Line", parent);
            line.Size(h: 50);
            UIKit.HList(line, 6, 0);
            UIKit.Label(line, info, 20).Size(w: 350);
            int n = buyQty.TryGetValue(key, out var saved) ? saved : 1;
            Text qty = null;
            Button buyBtn = null;
            void Show()
            {
                buyQty[key] = n;
                qty.text = n.ToString();
                buyBtn.Caption().text = caption(n);
            }
            var minus = UIKit.Button(line, "−", () => { n = Mathf.Max(1, n - 1); Show(); }, Pal.Gray, 24);
            minus.Size(w: 50);
            var box = UIKit.Box(line, new Color(1f, 0.97f, 0.88f), true, "Qty");
            box.Size(w: 64);
            qty = UIKit.Label(box.transform, "1", 26, Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            qty.rectTransform.Fill();
            var plus = UIKit.Button(line, "+", () => { n = Mathf.Min(99, n + 1); Show(); }, color, 26);
            plus.Size(w: 64);
            buyBtn = UIKit.Button(line, "", () => { if (buy(n)) { n = 1; Show(); } }, color, 20);
            buyBtn.Size(w: 230);
            minus.interactable = plus.interactable = buyBtn.interactable = enabled;
            Show();
            return buyBtn;
        }

        void BuildStock(RectTransform list)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>Giá trị tồn kho: {Finance.Money(Inventory.TotalValue(S))}</b>");
            sb.AppendLine();
            sb.AppendLine("<b>HÀNG ĐANG VỀ</b>");
            if (S.deliveries.Count == 0) sb.AppendLine("  (không có)");
            foreach (var d in S.deliveries)
                sb.AppendLine($"  {GameDB.GetIng(d.ing).name} x{d.qty} — " +
                              $"{(d.wholesale ? "hàng đặt" : "mua lẻ")}, tới sau ~{Mathf.CeilToInt(Mathf.Max(0, d.arriveTimer))} giây" +
                              (gm.Phase == GamePhase.DayEnd ? " (về trong đêm)" : ""));
            sb.AppendLine();
            sb.AppendLine("<b>TRONG KHO (theo lô)</b>");
            if (S.inventory.Count == 0) sb.AppendLine("  Kho trống!");
            foreach (var b in S.inventory.OrderBy(b => GameDB.IngList.IndexOf(GameDB.GetIng(b.ing))).ThenBy(b => b.expiryDay))
            {
                var def = GameDB.GetIng(b.ing);
                string exp = b.expiryDay <= S.day ? "<color=#C62828>hết hạn cuối hôm nay</color>" : $"HSD tới ngày {b.expiryDay}";
                sb.AppendLine($"  {def.name}: {b.qty} phần ({def.unit}) — {exp} — giá vốn {Finance.Money(b.unitCost)}/phần");
            }
            sb.AppendLine();
            sb.AppendLine("<b>ĐÃ BỎ DO HẾT HẠN (14 ngày gần nhất)</b>");
            if (S.spoilLog.Count == 0) sb.AppendLine("  (chưa có)");
            foreach (var r in S.spoilLog.OrderByDescending(r => r.day))
                sb.AppendLine($"  Sáng ngày {r.day}: {GameDB.GetIng(r.ing)?.name ?? r.ing} x{r.qty} ({Finance.Money(r.value)}) — <color=#C62828>bị bỏ do hết hạn</color>");
            var t = UIKit.Label(list, sb.ToString(), 22, Pal.Text, TextAnchor.UpperLeft);
            t.Flow();
        }

        string pastedCode = "";

        void OpenBackup()
        {
            pastedCode = "";
            OpenModal("SAO LƯU & KHÔI PHỤC DỮ LIỆU", BuildBackup, true, null, 0.12f, 0.08f, 0.88f, 0.92f);
        }

        void BuildBackup(RectTransform body)
        {
            var top = UIKit.NewRect("Export", body).Anchors(0.01f, 0.56f, 0.99f, 0.99f);
            UIKit.VList(top, 8, 4);
            UIKit.Label(top, "<b>1. SAO LƯU</b> — Mỗi buổi sáng game TỰ GHI file sao lưu vào máy: <b>" + BackupFile.WhereText + "</b>" +
                (BackupFile.LastWritten == null ? "" : BackupFile.PublicOk || !(Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer) ? " <color=#1B7F2A>(đã ghi)</color>" : " <color=#C62828>(máy này không cho ghi, hãy dùng nút sao chép mã)</color>") +
                ". Cẩn thận hơn thì sao chép mã và cất thêm ở Ghi chú / email.", 21).Size(h: 64);
            if (GameManager.HasSave)
            {
                string code = gm.ExportBackupCode();
                string info = GameManager.BackupSummary(code) ?? "";
                UIKit.Label(top, $"Dữ liệu sẽ sao lưu: <b>{info}</b> (bản lưu gần nhất, tự lưu mỗi buổi sáng)\n<size=17>Mã dài {code.Length:N0} ký tự: {code.Substring(0, Mathf.Min(48, code.Length))}...</size>", 20).Size(h: 56);
                UIKit.Button(top, "SAO CHÉP MÃ SAO LƯU", () =>
                {
                    GUIUtility.systemCopyBuffer = code;
                    Sfx.Play("coin");
                    gm.Toast("Đã sao chép mã! Dán vào Ghi chú / Zalo / email để cất giữ.");
                }, Pal.Green, 28).Size(h: 66);
            }
            else UIKit.Label(top, "<color=#C62828>Chưa có dữ liệu chơi nào để sao lưu.</color>", 22).Size(h: 40);

            var bottom = UIKit.NewRect("Import", body).Anchors(0.01f, 0.01f, 0.99f, 0.54f);
            UIKit.Label(bottom, (Application.platform == RuntimePlatform.IPhonePlayer
                    ? "<b>2. KHÔI PHỤC</b> — Cài lại game (cùng Apple ID) thì game tự tìm bản sao lưu trong Keychain và hỏi khôi phục. Nếu không có: dán mã đã cất (Ghi chú / email) rồi bấm KHÔI PHỤC."
                    : "<b>2. KHÔI PHỤC</b> — Cài lại game thì game tự tìm file sao lưu. Nếu không tự nạp được: mở ứng dụng Tệp (Files), vào <b>" + BackupFile.WhereText +
                      "</b>, mở file, chọn tất cả, Sao chép rồi quay lại đây bấm DÁN và KHÔI PHỤC."), 20)
                .rectTransform.Anchors(0, 0.8f, 1, 1);
            string pasted = pastedCode.Length == 0 ? "<color=#888888>Chưa dán mã nào.</color>"
                : GameManager.BackupSummary(pastedCode) is string sum ? $"<color=#1B7F2A>Đã dán mã hợp lệ ({pastedCode.Length:N0} ký tự): <b>{sum}</b></color>"
                : "<color=#C62828>Nội dung vừa dán không phải mã sao lưu hợp lệ. Hãy sao chép lại đầy đủ mã MICAY2-...</color>";
            var box = UIKit.Box(bottom, Color.white, true, "Pasted");
            box.rectTransform.Anchors(0, 0.22f, 1, 0.78f);
            var pl = UIKit.Label(box.transform, "Bấm DÁN TỪ BỘ NHỚ TẠM sau khi đã sao chép mã sao lưu (từ Ghi chú, Zalo, email...).\n\n" + pasted, 22, Pal.Text, TextAnchor.MiddleCenter);
            pl.rectTransform.Fill(14);
            pl.Fit(22, 12);
            var row = UIKit.NewRect("Row", bottom).Anchors(0, 0, 1, 0.17f);
            var h = UIKit.HList(row, 14, 0);
            h.childForceExpandWidth = true;
            UIKit.Button(row, "TÌM FILE TRONG MÁY", () =>
            {
                string found = BackupFile.FindBest(out var at);
                if (found == null) { Sfx.Play("error"); gm.Toast("Không tự đọc được file sao lưu. Hãy mở file bằng ứng dụng Tệp, sao chép rồi bấm DÁN."); return; }
                pastedCode = found;
                gm.Toast("Đã tìm thấy: " + at);
                RebuildModal();
            }, Pal.Teal, 22);
            UIKit.Button(row, "DÁN TỪ BỘ NHỚ TẠM", () =>
            {
                pastedCode = (GUIUtility.systemCopyBuffer ?? "").Trim();
                if (pastedCode.Length == 0) gm.Toast("Bộ nhớ tạm đang trống. Hãy sao chép mã trước.");
                RebuildModal();
            }, Pal.Blue, 24);
            UIKit.Button(row, "KHÔI PHỤC", () =>
            {
                string code = pastedCode;
                string info = GameManager.BackupSummary(code);
                if (info == null) { Sfx.Play("error"); gm.Toast("Mã không hợp lệ: hãy sao chép lại đầy đủ, không thiếu ký tự."); return; }
                ShowConfirm("Khôi phục dữ liệu?", $"Mã này chứa: {info}.\n" + (GameManager.HasSave ? "Dữ liệu đang lưu trên máy sẽ bị thay thế." : ""), "Khôi phục", () =>
                {
                    if (gm.ImportBackupCode(code, out var err))
                    {
                        Sfx.Play("fanfare");
                        gm.ContinueGame();
                        gm.Toast("Khôi phục thành công!");
                    }
                    else { Sfx.Play("error"); ShowMessage("KHÔNG KHÔI PHỤC ĐƯỢC", err, OpenBackup); }
                }, OpenBackup);
            }, Pal.Red, 28);
        }

        void OpenServices()
        {
            OpenModal("DỊCH VỤ — BẾP, CHỖ NGỒI, NHÂN VIÊN", BuildServices);
            gm.TutorialSignal("service");
        }

        void ServiceRow(RectTransform list, string text, string btn, bool enabled, Color color, Action onClick, float h = 70, Sprite icon = null)
        {
            var row = UIKit.Box(list, Color.white, true, "Row");
            row.Size(h: icon != null ? Mathf.Max(h, 96) : h);
            UIKit.HList(row, 10, 8);
            if (icon != null)
            {
                var holder = UIKit.NewRect("Icon", row.transform);
                holder.Size(w: 110);
                Art.Fit(holder, icon, false, "Img");
            }
            var lbl = UIKit.Label(row.transform, text, 21);
            lbl.Size(flexW: 1);
            lbl.Fit(21, 12);
            if (btn == null) return;
            var b = UIKit.Button(row.transform, btn, onClick, color, 22);
            b.Size(w: 260);
            b.interactable = enabled;
        }

        void ServiceRow2(RectTransform list, string text, string btn1, bool on1, Color c1, Action click1, string btn2, bool on2, Color c2, Action click2, float h, Sprite icon)
        {
            var row = UIKit.Box(list, Color.white, true, "Row");
            row.Size(h: icon != null ? Mathf.Max(h, 96) : h);
            UIKit.HList(row, 10, 8);
            if (icon != null)
            {
                var holder = UIKit.NewRect("Icon", row.transform);
                holder.Size(w: 110);
                Art.Fit(holder, icon, false, "Img");
            }
            var lbl = UIKit.Label(row.transform, text, 21);
            lbl.Size(flexW: 1);
            lbl.Fit(21, 12);
            var b1 = UIKit.Button(row.transform, btn1, click1, c1, 20);
            b1.Size(w: 230);
            b1.interactable = on1;
            var b2 = UIKit.Button(row.transform, btn2, click2, c2, 20);
            b2.Size(w: 200);
            b2.interactable = on2;
        }

        void ServiceHeader(RectTransform list, string text) =>
            UIKit.Label(list, text, 25, new Color(0.55f, 0.15f, 0.1f), TextAnchor.LowerLeft, FontStyle.Bold).Size(h: 44);

        void ConfirmBuy(string what, long cost, Action buy) =>
            ShowConfirm("Mua?", $"{what} với giá {Finance.Money(cost)}?\nTiền mặt hiện có: {Finance.Money(S.cash)}", "Mua", () => { buy(); OpenServices(); }, OpenServices);

        void BuyEquipFlow(bool bar, int tier)
        {
            string name = bar ? GameManager.BarTiers[tier].name : GameManager.StoveTiers[tier].name;
            long cost = bar ? GameManager.BarTiers[tier].cost : GameManager.StoveTiers[tier].cost;
            ShowConfirm("Mua?", $"{name} với giá {Finance.Money(cost)}?\nTiền mặt hiện có: {Finance.Money(S.cash)}", "Mua", () =>
            {
                int old = bar ? gm.BarToReplace(tier) : gm.StoveToReplace(tier);
                bool full = bar ? S.barTiers.Count >= GameManager.MaxBars : S.stoves >= GameManager.MaxStoves;
                if (old < 0)
                {
                    if (full) gm.Toast(bar ? "Đã đủ 6 máy pha và không máy nào kém hơn máy định mua." : "Đã đủ 9 bếp và không bếp nào kém hơn bếp định mua.");
                    else if (bar) gm.BuyBar(tier, -1);
                    else gm.BuyStove(tier, -1);
                    OpenServices();
                    return;
                }
                string what = bar ? $"Máy pha {old + 1} ({gm.BarLabel(old)})" : $"Bếp {old + 1} ({gm.StoveLabel(old)})";
                long back = bar ? gm.BarResale(old) : gm.StoveResale(old);
                int used = bar ? gm.BarDaysUsed(old) : gm.StoveDaysUsed(old);
                ShowChoice(bar ? "Bán máy pha cũ?" : "Bán bếp cũ?",
                    $"Bán {what}, đã dùng {used} ngày, được {Finance.Money(back)}?\n" +
                    (full ? "Quán đã hết chỗ: không bán thì không lắp thêm được." : "Bán thì đồ mới thay vào chỗ đó, không bán thì lắp thêm bên cạnh."),
                    "Có, bán", full ? "Bỏ qua" : "Không bán",
                    () => { if (bar) gm.BuyBar(tier, old); else gm.BuyStove(tier, old); OpenServices(); },
                    () => { if (!full) { if (bar) gm.BuyBar(tier, -1); else gm.BuyStove(tier, -1); } OpenServices(); });
            }, OpenServices);
        }

        static readonly Color DimRed = new Color(0.72f, 0.36f, 0.34f);

        void EquipLimitMsg() =>
            ShowMessage("ĐÃ ĐẠT GIỚI HẠN", "Đã đạt giới hạn mua bếp hoặc máy làm đồ uống, không thể mua thêm nữa.", OpenServices);

        void BuildServices(RectTransform body)
        {
            var area = UIKit.NewRect("Area", body).Fill();
            var list = UIKit.ScrollList(area, 6, 6);
            ((RectTransform)list.parent.parent).Fill();
            UIKit.Label(list, $"Tiền mặt: <b>{Finance.Money(S.cash)}</b>   •   Mặt bằng {Finance.Money(S.rentPerDay)}/ngày   •   Điện nước gas {Finance.Money(S.utilityPerDay)}/ngày", 21).Size(h: 36);

            ServiceHeader(list, $"BẾP NẤU ({S.stoves}/{GameManager.MaxStoves})");
            var have = string.Join(", ", Enumerable.Range(0, S.stoves).Select(i => $"Bếp {i + 1}: {gm.StoveLabel(i)}"));
            UIKit.Label(list, $"<size=19>Đang có: {have}. Ba loại bếp bán ở đây đều xịn hơn bếp ga cũ. Bếp mua về không nâng cấp được, muốn xịn hơn thì mua loại khác: lúc mua sẽ được hỏi có bán bếp kém nhất để thay vào chỗ đó không.</size>", 19, Pal.Gray).Size(h: 60);
            bool stoveFull = S.stoves >= GameManager.MaxStoves;
            for (int t = 1; t < GameManager.StoveTiers.Length; t++)
            {
                int tier = t;
                var st = GameManager.StoveTiers[t];
                ServiceRow(list, $"<b>{st.name}</b>\n<size=18>{st.desc}. Thời gian nấu {Mathf.RoundToInt(st.mul * 100)}% so với bếp cũ.</size>",
                    stoveFull ? "Đã đủ bếp" : $"Mua {Finance.Short(st.cost)}", true, stoveFull ? DimRed : Pal.Blue,
                    stoveFull ? (Action)EquipLimitMsg : () => BuyEquipFlow(false, tier), 96, Art.Get(st.sprite));
            }

            var oldStoves = Enumerable.Range(0, S.stoves).Where(i => gm.StoveTier(i) == 0).ToList();
            if (oldStoves.Count > 0)
            {
                ServiceHeader(list, "NÂNG CẤP BẾP GA CŨ");
                UIKit.Label(list, $"<size=19>Thay béc lửa, vệ sinh ống dẫn ga. Mỗi bếp ga cũ nâng được tối đa {GameManager.MaxOldUpgrades} lần, mỗi lần {Finance.Money(GameManager.StoveUpgradeCost)}. Nâng đủ {GameManager.MaxOldUpgrades} lần vẫn chậm hơn bếp rẻ nhất bán ở trên.</size>", 19, Pal.Gray).Size(h: 44);
                foreach (var i in oldStoves)
                {
                    int idx = i, lv = gm.StoveLevel(i);
                    bool max = lv >= GameManager.MaxOldUpgrades;
                    string next = max ? "" : $", nâng lần {lv + 1} còn {Mathf.RoundToInt(GameManager.OldUpgradeMul[lv + 1] * 100)}%";
                    ServiceRow(list, $"<b>Bếp {i + 1}</b> — bếp ga cũ, đã nâng {lv}/{GameManager.MaxOldUpgrades} lần\n<size=18>Thời gian nấu {Mathf.RoundToInt(GameManager.OldUpgradeMul[lv] * 100)}% so với lúc chưa nâng{next}.</size>",
                        max ? "Đã nâng tối đa" : $"Nâng cấp {Finance.Short(GameManager.StoveUpgradeCost)}", !max, Pal.Teal,
                        () => ConfirmBuy($"Nâng cấp bếp {idx + 1} (lần {lv + 1})", GameManager.StoveUpgradeCost, () => gm.UpgradeStove(idx)), 96, Art.Get("scene/stove"));
                }
            }

            ServiceHeader(list, $"MÁY PHA ĐỒ UỐNG ({S.barTiers.Count}/{GameManager.MaxBars} máy, {S.DrinkSlots} vị trí pha)");
            var bars = string.Join(", ", Enumerable.Range(0, S.barTiers.Count).Select(m => $"Máy {m + 1}: {gm.BarLabel(m)} ({GameManager.BarTiers[gm.BarTier(m)].slots} ly/lần)"));
            UIKit.Label(list, $"<size=19>Đang có: {bars}. Ba loại máy bán ở đây đều pha cùng lúc 2 ly và nhanh hơn máy cũ. Máy mua về không nâng cấp được, muốn xịn hơn thì mua loại khác: lúc mua sẽ được hỏi có bán máy kém nhất để thay không.</size>", 19, Pal.Gray).Size(h: 60);
            bool barFull = S.barTiers.Count >= GameManager.MaxBars;
            for (int t = 1; t < GameManager.BarTiers.Length; t++)
            {
                int tier = t;
                var bt = GameManager.BarTiers[t];
                ServiceRow(list, $"<b>{bt.name}</b>\n<size=18>{bt.desc}. Thời gian pha {Mathf.RoundToInt(bt.mul * 100)}% so với máy cũ.</size>",
                    barFull ? "Đã đủ máy" : $"Mua {Finance.Short(bt.cost)}", true, barFull ? DimRed : Pal.Blue,
                    barFull ? (Action)EquipLimitMsg : () => BuyEquipFlow(true, tier), 96, Art.Get(bt.sprite) ?? Art.Get("scene/drinkbar"));
            }

            var oldBars = Enumerable.Range(0, S.barTiers.Count).Where(m => gm.BarTier(m) == 0).ToList();
            if (oldBars.Count > 0)
            {
                ServiceHeader(list, "NÂNG CẤP MÁY PHA CŨ");
                UIKit.Label(list, $"<size=19>Thay vòi, thay ron, vệ sinh bình chứa. Mỗi máy cũ nâng được tối đa {GameManager.MaxOldUpgrades} lần, mỗi lần {Finance.Money(GameManager.BarUpgradeCost)}. Nâng đủ {GameManager.MaxOldUpgrades} lần vẫn chậm hơn máy rẻ nhất bán ở trên và vẫn chỉ pha 1 ly một lúc.</size>", 19, Pal.Gray).Size(h: 44);
                foreach (var m in oldBars)
                {
                    int idx = m, lv = gm.BarLevel(m);
                    bool max = lv >= GameManager.MaxOldUpgrades;
                    string next = max ? "" : $", nâng lần {lv + 1} còn {Mathf.RoundToInt(GameManager.OldUpgradeMul[lv + 1] * 100)}%";
                    ServiceRow(list, $"<b>Máy pha {m + 1}</b> — máy pha cũ, đã nâng {lv}/{GameManager.MaxOldUpgrades} lần\n<size=18>Thời gian pha {Mathf.RoundToInt(GameManager.OldUpgradeMul[lv] * 100)}% so với lúc chưa nâng{next}.</size>",
                        max ? "Đã nâng tối đa" : $"Nâng cấp {Finance.Short(GameManager.BarUpgradeCost)}", !max, Pal.Teal,
                        () => ConfirmBuy($"Nâng cấp máy pha {idx + 1} (lần {lv + 1})", GameManager.BarUpgradeCost, () => gm.UpgradeBar(idx)), 96, Art.Get("scene/drinkbar"));
                }
            }

            foreach (var u in GameManager.Upgrades)
            {
                var id = u.id;
                int lv = gm.UpgradeLevel(id), max = gm.UpgradeMax(id);
                ServiceHeader(list, $"CHỖ NGỒI ({lv}/{max} bàn)");
                string note = "Thêm bàn = thêm chỗ cho khách. Bàn 9-16 ở Khu mở rộng, 17-24 ở Sân sau, 25-32 ở Khu vỉa hè. Chỗ trống nào trong quán cũng có nút \"+ Thêm bàn\" giá 800 nghìn, thích chỗ nào mua chỗ đó. Nút ở đây mua chỗ trống đầu tiên.";
                ServiceRow(list, $"<b>{u.name}</b>\n<size=18>{note}</size>", lv >= max ? "Đã tối đa" : $"Mua {Finance.Short(u.cost)}", lv < max, Pal.Green,
                    () => ConfirmBuy(u.name, u.cost, () => gm.BuyUpgrade(id)), 110, Art.Get("scene/table_blue"));
            }

            ServiceHeader(list, $"NHÂN VIÊN PHỤ GIÚP ({S.helpers.Count}/{GameManager.MaxHelpers})");
            UIKit.Label(list, $"<size=19>Lương mỗi nhân viên = <b>3,5% doanh thu bán hàng trong ngày</b> (không trừ chi phí): bán nhiều hưởng nhiều, bán ít hưởng ít. Cộng dồn theo từng ngày (trả theo ngày hoặc trả dồn). Nợ đủ {GameManager.WageUrgeDays} ngày nhân viên sẽ nhắc, nợ {GameManager.WageQuitDays} ngày là TỰ Ý NGHỈ, {GameManager.WageQuitDays + 1} ngày là báo công an (phạt {Finance.Short(GameManager.WageFine)} + bồi thường lương). Phải trả hết lương mới cho nghỉ được. " +
                $"Mỗi khu có giới hạn người: Sân trước thêm tối đa {CustomerSystem.FrontStaffMax} người phụ chủ quán, các khu khác tối đa {CustomerSystem.AreaStaffMax} người. " +
                "Nhân viên cùng khu làm song song: mỗi người nhận một bàn theo thứ tự ai đến trước phục vụ trước, xong bàn này thì nhận bàn kế tiếp. " +
                "Ở Sân trước, nhân viên rảnh vào phụ chủ quán nấu các món còn lại của bàn chủ quán đang làm (trừ món chủ quán đang chọn) và mang món ra giúp nếu không bận. Rảnh hơn nữa thì sang phụ khu đang đông khách nhất (Sân trước nhận tối đa 2 người). " +
                "Bàn đang có nhân viên làm sẽ phủ màu xanh nhạt trong bong bóng gọi món.</size>", 19, Pal.Gray).Size(h: 128);
            var cap = string.Join("   •   ", Enumerable.Range(0, CustomerSystem.Areas).Select(a =>
                gm.AreaUsable(a) ? $"{CustomerSystem.AreaNames[a]}: {gm.StaffInArea(a)}/{CustomerSystem.StaffMax(a)}" : $"{CustomerSystem.AreaNames[a]}: chưa có bàn"));
            UIKit.Label(list, $"<size=19><b>Phân bổ:</b> {cap}</size>", 19, Pal.Text).Size(h: 32);
            long owedAll = gm.WagesOwed;
            if (owedAll > 0)
                ServiceRow(list, $"<b>Lương đang nợ: {Finance.Money(owedAll)}</b>\n<size=18>Trả theo ngày hoặc trả dồn cho từng người.</size>",
                    "Tới mục Nợ", true, Pal.Blue, () => { CloseModal(true); OpenFinance(6); }, 80);
            foreach (var name in S.helpers.ToList())
            {
                int i = S.helpers.IndexOf(name);
                int hArea = i < S.helperAreas.Count ? S.helperAreas[i] : -1;
                long owed = gm.OwedFor(name);
                int days = gm.OwedDays(name);
                int next = gm.NextAreaFor(name);
                string nm = name;
                string wage = days > 0
                    ? $" • nợ lương {days} ngày (ngày thứ {Math.Min(days, GameManager.WagePayCycle)}/{GameManager.WagePayCycle}{(days > GameManager.WagePayCycle ? " — quá hạn!" : "")}): {Finance.Money(owed)}"
                    : " • đã trả đủ lương";
                ServiceRow2(list, $"<b>{nm}</b> — phụ trách {gm.AreaLabel(hArea)}\n<size=18>Đã làm {gm.DaysWorked(nm)} ngày{wage}</size>",
                    next >= 0 ? $"Sang {gm.AreaLabel(next)}" : "Các khu đủ người", next >= 0, Pal.Teal, () => { gm.MoveHelper(nm); OpenServices(); },
                    gm.CanFireStaff ? "Cho nghỉ" : "Cho nghỉ (hết ngày)", gm.CanFireStaff, Pal.Red,
                    () =>
                    {
                        if (gm.OwedFor(nm) > 0)
                            ShowMessage("Chưa trả hết lương", $"Còn nợ {nm} {Finance.Money(gm.OwedFor(nm))} ({gm.OwedDays(nm)} ngày). Phải trả hết lương rồi mới cho {nm} nghỉ được.", OpenServices);
                        else
                            ShowConfirm("Cho nghỉ?", $"Cho {nm} nghỉ việc?", "Cho nghỉ", () => { gm.FireHelper(nm); OpenServices(); }, OpenServices);
                    },
                    96, Art.Char(GameManager.HelperSprite(name)));
            }
            if (S.helpers.Count < GameManager.MaxHelpers)
            {
                int free = gm.FreeArea();
                ServiceRow(list, free >= 0
                        ? $"<b>Thuê thêm 1 nhân viên</b>\n<size=18>Mặc đồng phục quán, tự nấu, pha và bưng món. Người mới sẽ phụ trách {gm.AreaLabel(free)} (đổi khu được).</size>"
                        : "<b>Thuê thêm 1 nhân viên</b>\n<size=18>Các khu đã đủ người. Mua thêm bàn ở khu khác để mở khu mới.</size>",
                    "Thuê nhân viên", free >= 0, Pal.Orange,
                    () => ShowConfirm("Thuê nhân viên?", "Lương = 3,5% doanh thu bán hàng mỗi ngày, cộng dồn trả mỗi 7 ngày.", "Thuê", () => { gm.HireHelper(); OpenServices(); }, OpenServices), 96, Art.Char("nv1"));
            }
            if (S.staff > 0)
                ServiceRow(list, $"<b>Vy</b> (bán thời gian) — lương 3,5% doanh thu mỗi ngày\n<size=18>Bưng món ra Khu mở rộng, Sân sau, Khu vỉa hè; rảnh tay thì phụ Ánh bưng món ở sân trước. Khách kiên nhẫn hơn 15%.</size>", null, false, Pal.Gray, null, 96, Art.Char("vy"));
        }

        void OpenMenu()
        {
            OpenModal("THỰC ĐƠN", BuildMenu,
                liveKey: () => $"{S.priceAdjust}|{S.day}|{S.chapter}|{string.Join(",", S.disabledRecipes)}");
            gm.TutorialSignal("menu");
        }

        void BuildMenu(RectTransform body)
        {
            var top = UIKit.NewRect("Top", body).Anchors(0, 0.8f, 1, 1);
            UIKit.VList(top, 6, 2);

            var priceRow = UIKit.NewRect("Price", top);
            priceRow.Size(h: 60);
            UIKit.HList(priceRow, 8, 0);
            UIKit.Label(priceRow, "<b>Giá bán:</b>", 24).Size(w: 140);
            string[] pl = { "Giảm 10% (đông khách hơn)", "Giá gốc", "Tăng 10% (ít khách hơn)" };
            bool canPrice = gm.Phase == GamePhase.Prep;
            for (int i = -1; i <= 1; i++)
            {
                int v = i;
                var pb = UIKit.Button(priceRow, pl[i + 1], () => { S.priceAdjust = v; gm.MarkDirty(); gm.TutorialSignal("price"); RebuildModal(); }, S.priceAdjust == v ? Pal.Red : Pal.Wood, 22);
                pb.Size(w: 330);
                pb.interactable = canPrice;
                priceBtns[i + 1] = pb;
            }

            var tops = S.day >= 2 ? GameDB.DailyToppings(S.day, S.chapter) : new List<IngredientDef>();
            string priceNote = canPrice ? "" : "<color=#C62828>Đang bán — không đổi giá được. Chỉ đổi giá vào buổi sáng, trước khi mở cửa.</color>\n";
            UIKit.Label(top, priceNote + (tops.Count == 0
                ? "<i>Giá vốn tính theo giá sỉ gốc; mì cay tính trung bình 3 muỗng ớt. Món tạm ngưng sẽ không có khách gọi.</i>"
                : "<i>Topping khách hay gọi THÊM hôm nay (cộng tiền): " + string.Join(", ", tops.Select(i => $"{i.name} +{Finance.Short(i.toppingPrice)}")) + "</i>"), 19, Pal.Gray).Size(h: canPrice ? 30 : 52);

            var area = UIKit.NewRect("Area", body).Anchors(0, 0, 1, 0.79f);
            var list = UIKit.ScrollList(area, 5, 4);
            ((RectTransform)list.parent.parent).Fill();
            foreach (var r in GameDB.Recipes.OrderBy(r => r.chapter > S.chapter).ThenBy(r => r.cat).ThenBy(r => r.chapter))
                MenuRow(list, r);
        }

        void MenuRow(RectTransform list, RecipeDef r)
        {
            bool unlocked = r.chapter <= S.chapter;
            bool on = unlocked && !S.disabledRecipes.Contains(r.id);
            var row = UIKit.Box(list, unlocked ? Color.white : new Color(0.9f, 0.9f, 0.9f), true, "Row");
            row.Size(h: 82);
            UIKit.HList(row, 10, 6);

            if (unlocked)
            {
                var id = r.id;
                UIKit.Button(row.transform, on ? "ĐANG BÁN" : "TẠM NGƯNG", () =>
                {
                    if (S.disabledRecipes.Contains(id)) S.disabledRecipes.Remove(id);
                    else S.disabledRecipes.Add(id);
                    gm.MarkDirty();
                    RebuildModal();
                }, on ? Pal.Green : Pal.Gray, 20).Size(w: 150);
            }
            else UIKit.Label(row.transform, $"Mở ở chương {r.chapter}", 20, Pal.Gray, TextAnchor.MiddleCenter).Size(w: 150);

            var icon = Art.Pixel(row.transform, Art.Food(r.id), 2.6f, "Icon");
            icon.Size(w: 64);
            if (!unlocked) icon.color = new Color(1, 1, 1, 0.4f);
            string cat = r.cat == DishCategory.Noodle ? "Mì" : r.cat == DishCategory.Rice ? "Cơm" : "Đồ uống";
            long price = (long)Math.Round(r.price * S.PriceFactor / 1000.0) * 1000;
            UIKit.Label(row.transform, $"<b>{r.name}</b>{(r.spicy ? " <color=#C62828>(cấp 0-7)</color>" : "")}\n<size=18>{cat} • {Finance.Money(price)} • ~{r.realMinutes} phút</size>", 22).Size(w: 420);
            long cost = GameDB.WholesaleCost(r);
            int margin = price > 0 ? (int)((price - cost) * 100 / price) : 0;
            UIKit.Label(row.transform, $"Giá vốn ~{Finance.Money(cost)}\n<size=18>lãi gộp ~{margin}%</size>", 21).Size(w: 220);
            int mastery = S.GetMastery(r.id);
            UIKit.Label(row.transform, $"Đã nấu: {mastery}\n<size=18>{(mastery >= KitchenSystem.MasteryForQuickFill ? "<color=#1B7F2A>Ghép nhanh</color>" : $"ghép nhanh: {mastery}/{KitchenSystem.MasteryForQuickFill}")}</size>", 21).Size(w: 180);
            var recipe = UIKit.Label(row.transform, GameDB.RecipeText(r, 3).Replace("Bột ớt x3", "Bột ớt x(cấp)"), 17, Pal.Gray);
            recipe.Fit(17, 11);
            recipe.Size(flexW: 1);
        }

        void OpenFinance(int tab)
        {
            financeTab = tab;
            OpenModal("SỔ SÁCH — CHI TIÊU, LỢI NHUẬN, LỖ", BuildFinance);
        }

        void BuildFinance(RectTransform body)
        {
            financeTabsRow = Tabs(body, new[] { "Hôm nay", "7 ngày", "30 ngày", "Chương này", "Toàn bộ", "Nhật ký", "Nợ", "Ngân hàng" }, financeTab,
                i => { financeTab = i; RebuildModal(); gm.TutorialSignal("ftab" + i); }, 0.91f);
            var area = UIKit.NewRect("Area", body).Anchors(0, 0, 1, 0.9f);
            var list = UIKit.ScrollList(area, 6, 8);
            ((RectTransform)list.parent.parent).Fill();

            switch (financeTab)
            {
                case 5: BuildLedger(list); return;
                case 6: BuildDebts(list); return;
                case 7: BuildBank(list); return;
            }
            var (from, title) = ReportRange(financeTab);
            var t = UIKit.Label(list, Finance.ReportText(S, Finance.Build(S, from, S.day), title), 23, Pal.Text, TextAnchor.UpperLeft);
            t.Flow();
        }

        (int from, string title) ReportRange(int tab)
        {
            switch (tab)
            {
                case 1: return (Mathf.Max(1, S.day - 6), "BÁO CÁO 7 NGÀY GẦN NHẤT");
                case 2: return (Mathf.Max(1, S.day - 29), "BÁO CÁO 30 NGÀY GẦN NHẤT");
                case 3: return (S.chapterStartDay, $"BÁO CÁO {gm.Chapter.title.ToUpper()}");
                case 4: return (1, "BÁO CÁO TỪ NGÀY KHAI TRƯƠNG");
                default: return (S.day, $"BÁO CÁO NGÀY {S.day}");
            }
        }

        void BuildLedger(RectTransform list)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>LÃI / LỖ TỪNG NGÀY</b>  <size=17>(lưu toàn bộ, không xóa ngày nào)</size>");
            long todayNet = Finance.Build(S, S.day, S.day).NetProfit;
            if (S.dailyPnl.All(x => x.day != S.day))
            {
                string tc = todayNet >= 0 ? "#1B7F2A" : "#C62828";
                sb.AppendLine($"Ngày {S.day} (đang diễn ra):  <color={tc}>{(todayNet >= 0 ? "Lời " : "Lỗ ")}{Finance.Money(Math.Abs(todayNet))}</color>");
            }
            foreach (var p in S.dailyPnl.OrderByDescending(x => x.day))
            {
                string c = p.net >= 0 ? "#1B7F2A" : "#C62828";
                sb.AppendLine($"Ngày {p.day}:  <color={c}>{(p.net >= 0 ? "Lời " : "Lỗ ")}{Finance.Money(Math.Abs(p.net))}</color>");
            }

            sb.AppendLine();
            sb.AppendLine("<b>NHẬT KÝ THU CHI (50 dòng gần nhất)</b>");
            foreach (var e in Enumerable.Reverse(S.ledger).Take(50))
            {
                string c = e.amount >= 0 ? "#1B7F2A" : "#C62828";
                sb.AppendLine($"Ngày {e.day}  |  {e.cat}  |  {e.note}  |  <color={c}>{(e.amount >= 0 ? "+" : "")}{Finance.Money(e.amount)}</color>");
            }
            var t = UIKit.Label(list, sb.ToString(), 21, Pal.Text, TextAnchor.UpperLeft);
            t.Flow();
        }

        void BuildWages(RectTransform list)
        {
            long owedAll = gm.WagesOwed;
            UIKit.Label(list, "<b>LƯƠNG NHÂN VIÊN</b>  <size=18>(3,5% doanh thu bán hàng mỗi ngày)</size>", 24).Size(h: 44);
            if (owedAll <= 0)
            {
                UIKit.Label(list, "Hiện không nợ lương nhân viên nào.", 22, Pal.Gray).Size(h: 48);
                return;
            }
            UIKit.Label(list, $"Tổng đang nợ: <b>{Finance.Money(owedAll)}</b>. Nợ {GameManager.WageQuitDays} ngày nhân viên tự nghỉ, {GameManager.WageQuitDays + 1} ngày báo công an (phạt {Finance.Short(GameManager.WageFine)} + bồi thường).", 21).Size(h: 48);

            var names = new List<string>();
            if (gm.OwedFor(GameManager.VyName) > 0) names.Add(GameManager.VyName);
            foreach (var n in S.helpers) if (gm.OwedFor(n) > 0 && !names.Contains(n)) names.Add(n);
            foreach (var dep in S.departed) if (gm.OwedFor(dep.who) > 0 && !names.Contains(dep.who)) names.Add(dep.who);

            foreach (var who in names)
            {
                var person = who;
                long owed = gm.OwedFor(person);
                int days = gm.OwedDays(person);
                bool gone = S.departed.Any(x => x.who == person);
                var next = gm.NextWage(person);
                var box = UIKit.Box(list, Color.white, true, "Wage");
                UIKit.VList(box, 6, 12);
                UIKit.Label(box.transform, $"<b>{person}</b>{(gone ? " <color=#C62828>(đã nghỉ, phải bồi thường)</color>" : "")} — nợ {days} ngày (ngày thứ {Math.Min(days, GameManager.WagePayCycle)}/{GameManager.WagePayCycle}{(days > GameManager.WagePayCycle ? " — quá hạn" : "")}), tổng {Finance.Money(owed)}", 22).Size(h: 40);
                var row = UIKit.NewRect("Pay", box.transform);
                row.Size(h: 60);
                UIKit.HList(row, 10, 0);
                if (next.day > 0)
                    UIKit.Button(row, $"Trả tiền ngày thứ {next.day} ({Finance.Money(next.amount)})", () => { gm.PayWageDay(person); RebuildModal(); }, Pal.Orange, 22).Size(w: 430);
                UIKit.Button(row, $"Trả toàn bộ cho {person} ({Finance.Money(owed)})", () => { gm.PayWageAll(person); RebuildModal(); }, Pal.Green, 22).Size(w: 430);
            }

            var allRow = UIKit.NewRect("PayAll", list);
            allRow.Size(h: 66);
            var allH = UIKit.HList(allRow, 10, 0);
            allH.childForceExpandWidth = true;
            UIKit.Button(allRow, $"TRẢ TOÀN BỘ CHO TẤT CẢ NHÂN VIÊN ({Finance.Money(owedAll)})", () => { gm.PayWages(); RebuildModal(); }, Pal.Red, 24);
            UIKit.Label(list, " ", 12).Size(h: 16);
        }

        void BuildDebts(RectTransform list)
        {
            BuildWages(list);
            var debts = S.debts.Where(x => x.kind != Debt.Bank || x.remaining > 0).ToList();
            if (debts.Count == 0) { UIKit.Label(list, "Không có khoản nợ vay nào.", 24).Size(h: 60); return; }
            foreach (var d in debts)
            {
                var debt = d;
                var box = UIKit.Box(list, d.remaining > 0 ? Color.white : new Color(0.88f, 0.96f, 0.88f), true, "Debt");
                UIKit.VList(box, 6, 12);
                var sb = new StringBuilder();
                sb.Append($"<b>{d.name}</b> — chủ nợ: {d.creditor}\n");
                if (d.remaining <= 0) sb.Append("<color=#1B7F2A>ĐÃ TRẢ HẾT</color>");
                else
                {
                    sb.Append($"Còn nợ: <b>{Finance.Money(d.remaining)}</b>");
                    if (d.kind == Debt.Bank)
                        sb.Append($"   •   Trả một lần, hạn chót hết ngày {d.dueDay}" + (S.day > d.dueDay ? $"   <color=#C62828>QUÁ HẠN {S.day - d.dueDay} ngày — cưỡng chế cuối ngày {d.dueDay + BankSystem.GraceDays}!</color>" : ""));
                    else if (d.periodDays > 0)
                        sb.Append($"   •   Lãi {d.interestPerPeriod * 100:0.#}%/kỳ {d.periodDays} ngày   •   Mỗi kỳ trả tối thiểu {Finance.Money(d.installment)}\n" +
                                  $"Kỳ này đã trả {Finance.Money(d.paidThisPeriod)}, còn phải trả <b>{Finance.Money(DebtSystem.DueThisPeriod(d))}</b> trước khi hết ngày {d.nextDueDay}   •   Đã trễ {d.missed} kỳ");
                    else if (d.id == "me")
                        sb.Append($"   •   Không lãi. Mẹ cần dùng cho việc riêng — phải trả hết trước hết ngày {d.dueDay}" + (S.day > d.dueDay ? $"   <color=#C62828>(ĐÃ QUÁ HẠN {S.day - d.dueDay} ngày)</color>" : ""));
                    else sb.Append("   •   Không lãi, không kỳ hạn — trả khi nào có điều kiện.");
                }
                UIKit.Label(box.transform, sb.ToString(), 22).Size(h: d.periodDays > 0 && d.remaining > 0 ? 90 : 62);
                if (d.remaining <= 0) continue;
                if (d.id == "me" && S.day <= 3)
                {
                    UIKit.Label(box.transform, "<color=#C62828>Mẹ dặn mấy ngày đầu cứ lo buôn bán — trả cho mẹ được từ ngày thứ 4.</color>", 20, Pal.Gray).Size(h: 48);
                    continue;
                }

                var row = UIKit.NewRect("Pay", box.transform);
                row.Size(h: 60);
                UIKit.HList(row, 10, 0);
                long due = DebtSystem.DueThisPeriod(d);
                if (due > 0) UIKit.Button(row, $"Trả kỳ này {Finance.Money(due)}", () => DebtSystem.Pay(gm, debt, due), Pal.Red, 22).Size(w: 340);
                UIKit.Button(row, "Trả 1.000.000đ", () => DebtSystem.Pay(gm, debt, 1_000_000), Pal.Orange, 22).Size(w: 240);
                UIKit.Button(row, "Trả 5.000.000đ", () => DebtSystem.Pay(gm, debt, 5_000_000), Pal.Orange, 22).Size(w: 240);
                UIKit.Button(row, $"Trả hết {Finance.Money(d.remaining)}", () => DebtSystem.Pay(gm, debt, debt.remaining), Pal.Green, 22).Size(w: 340);
            }
        }

        void BuildBank(RectTransform list)
        {
            long limit = BankSystem.Limit(S), owed = BankSystem.Outstanding(S);
            var head = UIKit.Label(list,
                $"<b>VAY NGÂN HÀNG</b>   Hạn mức: {Finance.Money(limit)}   •   Đang nợ: {Finance.Money(owed)}   •   Còn vay được: {Finance.Money(Math.Max(0, limit - owed))}\n" +
                $"Trả một lần khi tới hạn (trả sớm ở tab NỢ). Quá hạn: lãi phạt 1%/ngày, trễ {BankSystem.GraceDays} ngày bị CƯỠNG CHẾ thu hồi + 3 điểm vi phạm; tịch thu không đủ thì ĐI TÙ.", 22);
            head.Size(h: 96);
            for (int ti = 0; ti < BankSystem.Terms.Length; ti++)
            {
                var (days, rate) = BankSystem.Terms[ti];
                int term = ti;
                var row = UIKit.NewRect("Term", list);
                row.Size(h: 70);
                UIKit.HList(row, 10, 0);
                UIKit.Label(row, $"<b>Kỳ hạn {days} ngày</b>\n<size=18>lãi {rate * 100:0.#}% cả kỳ</size>", 22).Size(w: 200);
                foreach (var amt in BankSystem.Amounts)
                {
                    long a = amt;
                    long repay = a + (long)Math.Round(a * rate);
                    var b = UIKit.Button(row, $"Vay {Finance.Short(a)}\n<size=16>trả {Finance.Short(repay)}</size>",
                        () => ShowConfirm("Vay ngân hàng?", $"Vay {Finance.Money(a)}, phải trả {Finance.Money(repay)} trước hết ngày {S.day + days}.\nTrễ hạn sẽ bị cưỡng chế!", "Vay",
                            () => { BankSystem.Borrow(gm, a, term); OpenFinance(7); }, () => OpenFinance(7)),
                        Pal.Blue, 20);
                    b.Size(w: 150);
                    b.interactable = owed + a <= limit;
                }
            }
        }

        void OpenTax()
        {
            OpenModal("THUẾ HỘ KINH DOANH", BuildTax);
            gm.TutorialSignal("tax");
        }

        void BuildTax(RectTransform body)
        {
            var ch = gm.Chapter;
            string method = ch.taxMethod == TaxMethod.RevenuePercent
                ? $"TNCN {ch.pitRate * 100:0.#}% × (doanh thu − ngưỡng)"
                : $"TNCN {ch.pitRate * 100:0.#}% × (doanh thu − chi phí hợp lý)";
            var rules = UIKit.Label(body,
                "• Từ 2026 bỏ thuế khoán & lệ phí môn bài, hộ kinh doanh TỰ KHAI – TỰ NỘP (NQ 198/2025/QH15).\n" +
                (ch.forceAboveThreshold
                    ? $"• Quán đã được xác định doanh thu năm > {Finance.Short(ch.taxThresholdYear)}: MỌI kỳ đều phát sinh thuế (NĐ 68/2026, sửa đổi bởi NĐ 141/2026).\n"
                    : $"• Doanh thu ≤ {Finance.Short(ch.taxThresholdYear)}/năm: không chịu GTGT, TNCN nhưng VẪN PHẢI NỘP TỜ KHAI (NĐ 68/2026, sửa đổi bởi NĐ 141/2026).\n") +
                $"• Quán ăn uống hiện áp dụng: <b>GTGT {ch.vatRate * 100:0.#}% doanh thu + {method}</b>.\n" +
                "• Trễ hạn: tiền chậm nộp 0,03%/ngày + phạt chậm nộp hồ sơ 2,5–12,5 triệu. Quá ~90 ngày: CƯỠNG CHẾ, phạt 1–3 lần số thuế, đình chỉ kinh doanh.\n" +
                $"• Trong game: 1 kỳ = {TaxSystem.PeriodDays} ngày (tương ứng 1 quý), hạn nộp {TaxSystem.DueAfterDays} ngày sau kỳ, 1 ngày trễ ≈ {TaxSystem.RealDaysPerGameDay} ngày thực. " +
                $"Bị cưỡng chế {TaxSystem.PrisonEvasions} lần = ĐI TÙ.\n<color=#C62828>Điểm vi phạm: {S.violationPoints}/{Legal.PrisonPoints}\nĐã bị cưỡng chế: {S.evasionCount}/{TaxSystem.PrisonEvasions}</color>",
                20, Pal.Text, TextAnchor.UpperLeft);
            rules.rectTransform.Anchors(0.01f, 0.72f, 0.99f, 1f);
            rules.Fit(20, 12);

            int curFrom = (S.day - 1) / TaxSystem.PeriodDays * TaxSystem.PeriodDays + 1;
            var cur = Finance.Build(S, curFrom, S.day);
            int days = S.day - curFrom + 1;
            long annual = cur.revenue * 365 / Math.Max(1, days);
            var est = UIKit.Label(body,
                $"<b>Kỳ hiện tại (ngày {curFrom}–{curFrom + TaxSystem.PeriodDays - 1}):</b> doanh thu tới nay {Finance.Money(cur.revenue)} > quy năm ≈ {Finance.Money(annual)} " +
                (ch.forceAboveThreshold || annual > ch.taxThresholdYear ? "<color=#C62828>(sẽ phát sinh thuế)</color>" : "(dưới ngưỡng)"),
                21, Pal.Blue, TextAnchor.MiddleLeft);
            est.rectTransform.Anchors(0.01f, 0.65f, 0.99f, 0.72f);
            est.Fit(21, 12);

            var area = UIKit.NewRect("Area", body).Anchors(0, 0, 1, 0.645f);
            var list = UIKit.ScrollList(area, 6, 4);
            ((RectTransform)list.parent.parent).Fill();
            if (S.taxBills.Count == 0)
            {
                UIKit.Label(list, $"Chưa có kỳ thuế nào kết thúc. Kỳ đầu tiên chốt vào cuối ngày {TaxSystem.PeriodDays}.", 22).Size(h: 60);
                return;
            }
            taxPayBtn = null;
            foreach (var b in Enumerable.Reverse(S.taxBills)) TaxRow(list, b);
        }

        void TaxRow(RectTransform list, TaxBill b)
        {
            int late = TaxSystem.LateDays(b, S.day);
            var row = UIKit.Box(list, b.paid ? new Color(0.92f, 0.97f, 0.92f) : late > 0 ? new Color(1f, 0.88f, 0.86f) : Color.white, true, "Bill");
            row.Size(h: 120);
            UIKit.HList(row, 10, 8);
            string m = b.splitChapters ? "tính riêng theo luật từng chương"
                : b.method == TaxMethod.RevenuePercent ? "theo doanh thu" : $"theo lợi nhuận (chi phí {Finance.Money(b.expenses)})";
            var sb = new StringBuilder();
            sb.Append($"<b>Kỳ ngày {b.fromDay}–{b.toDay}</b>  •  Doanh thu {Finance.Money(b.revenue)}  •  Quy năm ≈ {Finance.Money(b.annualized)} ({(b.aboveThreshold ? "vượt ngưỡng" : "dưới ngưỡng")})\n");
            sb.Append($"TNCN {m}: {Finance.Money(b.pit)}  •  GTGT: {Finance.Money(b.vat)}  •  <b>Tổng thuế: {Finance.Money(b.Total)}</b>\n");
            if (b.paid)
                sb.Append(b.enforced
                    ? $"<color=#C62828>BỊ CƯỠNG CHẾ ngày {b.paidDay}: chậm nộp {Finance.Money(b.interestPaid)}, phạt {Finance.Money(b.finePaid)}</color>"
                    : $"<color=#1B7F2A>Đã khai & nộp ngày {b.paidDay}</color>" + (b.interestPaid + b.finePaid > 0 ? $" (chậm nộp {Finance.Money(b.interestPaid)}, phạt {Finance.Money(b.finePaid)})" : ""));
            else if (late > 0)
                sb.Append($"<color=#C62828>QUÁ HẠN {late} ngày (~{late * TaxSystem.RealDaysPerGameDay} ngày thực). Tiền chậm nộp {Finance.Money(TaxSystem.Interest(b, S.day))}, phạt hồ sơ {Finance.Money(TaxSystem.FineIfPaidOn(b, S.day))}. " +
                          $"Sẽ bị CƯỠNG CHẾ vào cuối ngày {b.dueDay + TaxSystem.EnforceAfterLateDays}!</color>");
            else sb.Append($"Hạn nộp: hết ngày {b.dueDay}");
            var t = UIKit.Label(row.transform, sb.ToString(), 20);
            t.Fit(20, 12);
            t.Size(flexW: 1);
            if (!b.paid)
            {
                var bill = b;
                long due = TaxSystem.AmountDue(b, S.day);
                var pay = UIKit.Button(row.transform, b.Total == 0 && due == 0 ? "Nộp tờ khai" : $"Khai & nộp\n{Finance.Money(due)}",
                    () => { if (TaxSystem.Pay(gm, bill)) gm.TutorialSignal("taxpay"); }, late > 0 ? Pal.Red : Pal.Green, 22);
                pay.Size(w: 260);
                if (taxPayBtn == null) taxPayBtn = pay;
            }
        }

        public void ShowDayReport(Action onContinue)
        {
            reportTab = S.day % 30 == 0 ? 2 : S.day % 7 == 0 ? 1 : 0;
            string title = $"KẾT THÚC NGÀY {S.day}" + (S.day % 30 == 0 ? " — TỔNG KẾT THÁNG" : S.day % 7 == 0 ? " — TỔNG KẾT TUẦN" : "");
            OpenModal(title, body =>
            {
                Tabs(body, new[] { "Hôm nay", "7 ngày qua", "30 ngày qua", "Chương này" }, reportTab,
                    i => { reportTab = i; RebuildModal(); }, 0.91f);
                var area = UIKit.NewRect("Area", body).Anchors(0, 0.12f, 1, 0.9f);
                var list = UIKit.ScrollList(area, 6, 8);
                ((RectTransform)list.parent.parent).Fill();
                var (from, t) = ReportRange(reportTab == 3 ? 3 : reportTab);
                var txt = UIKit.Label(list, Finance.ReportText(S, Finance.Build(S, from, S.day), t), 23, Pal.Text, TextAnchor.UpperLeft);
                txt.Flow();
                var ok = UIKit.Button(body, "TIẾP TỤC  >>", () => CloseModal(true), Pal.Orange, 32);
                ((RectTransform)ok.transform).Anchors(0.35f, 0.005f, 0.65f, 0.11f);
            }, false, onContinue);
        }
    }
}
