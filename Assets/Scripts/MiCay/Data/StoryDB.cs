using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiCay
{
    public static class StoryDB
    {
        public static readonly Dictionary<string, CharacterDef> Characters = new Dictionary<string, CharacterDef>
        {
            { "narr", new CharacterDef("narr", "", "Dẫn truyện", new Color(0.25f, 0.25f, 0.3f)) },
            { "anh",  new CharacterDef("anh",  "Ánh", "Chủ tiệm (bạn)", new Color(0.93f, 0.45f, 0.15f)) },
            { "me",   new CharacterDef("me",   "Mẹ", "Mẹ của Ánh", new Color(0.88f, 0.42f, 0.6f)) },
            { "ba",   new CharacterDef("ba",   "Bố", "Bố của Ánh", new Color(0.45f, 0.38f, 0.3f)) },
            { "nam",  new CharacterDef("nam",  "Bà Năm", "Chủ nhà cho thuê mặt bằng", new Color(0.55f, 0.35f, 0.7f)) },
            { "lan",  new CharacterDef("lan",  "Chị Lan", "Cán bộ thuế phường", new Color(0.18f, 0.45f, 0.8f)) },
            { "tu",   new CharacterDef("tu",   "Chú Tư", "Mối hàng chợ đầu mối Long Biên", new Color(0.25f, 0.6f, 0.3f)) },
            { "vy",   new CharacterDef("vy",   "Vy", "Nhân viên", new Color(0.1f, 0.6f, 0.6f)) },
            { "khai", new CharacterDef("khai", "Anh Khải", "Chủ nợ", new Color(0.6f, 0.12f, 0.12f)) },
            { "tuan", new CharacterDef("tuan", "Tuấn", "Chủ quán Mì Cay Đại Hàn", new Color(0.4f, 0.4f, 0.45f)) },
            { "na",   new CharacterDef("na",   "Bé Na", "Khách quen, học sinh", new Color(0.95f, 0.6f, 0.2f)) },
            { "sau",  new CharacterDef("sau",  "Chú Sáu", "Khách quen, chạy xe ôm", new Color(0.5f, 0.45f, 0.2f)) },
            { "linh", new CharacterDef("linh", "Linh Ăn Cay", "TikToker ẩm thực", new Color(0.85f, 0.2f, 0.45f)) },
            { "congan", new CharacterDef("congan", "Anh Hùng", "Công an phường", new Color(0.2f, 0.4f, 0.25f), "khach_06") },
            { "attp",   new CharacterDef("attp",   "Chị Thu", "Thanh tra vệ sinh an toàn thực phẩm", new Color(0.3f, 0.55f, 0.6f), "khach_10") },
            { "pccc",   new CharacterDef("pccc",   "Anh Dũng", "Cảnh sát PCCC", new Color(0.75f, 0.2f, 0.15f), "khach_04") },
            { "bank",   new CharacterDef("bank",   "Chị Hà", "Nhân viên tín dụng ngân hàng", new Color(0.15f, 0.35f, 0.65f), "khach_07") },
            { "la",     new CharacterDef("la",     "Người lạ", "???", new Color(0.3f, 0.3f, 0.3f), "khach_12") },
            { "chuba",  new CharacterDef("chuba",  "Chú Ba", "Bạn làm ăn cũ của bố", new Color(0.45f, 0.3f, 0.2f), "khach_03") },
            { "thoxua", new CharacterDef("thoxua", "Chú Bảy", "Thợ sửa bếp", new Color(0.4f, 0.35f, 0.3f), "khach_11") },
        };

        public static CharacterDef Char(string id) => Characters.TryGetValue(id, out var c) ? c : Characters["narr"];

        static DialogueLine L(string who, string text) => new DialogueLine(who, text);

        static DayEvent Ev(int day, EventAction action, long amount, int days, string param, params DialogueLine[] lines) =>
            new DayEvent { day = day, atEnd = false, action = action, amount = amount, days = days, param = param, lines = lines };

        static DayEvent EvEnd(int day, EventAction action, long amount, int days, string param, params DialogueLine[] lines) =>
            new DayEvent { day = day, atEnd = true, action = action, amount = amount, days = days, param = param, lines = lines };

        static DayEvent Talk(int day, bool atEnd, params DialogueLine[] lines) =>
            new DayEvent { day = day, atEnd = atEnd, action = EventAction.None, lines = lines };

        public static Debt MakeDebt(string id, int today)
        {
            switch (id)
            {
                case "me":
                    return new Debt { id = "me", kind = Debt.Family, name = "Tiền mẹ cho mượn", creditor = "Mẹ", remaining = 20_000_000, dueDay = 15 };
                case "no_bo":
                    return new Debt
                    {
                        id = "no_bo", name = "Nợ cũ của bố", creditor = "Anh Khải", remaining = 8_000_000,
                        interestPerPeriod = 0.03f, periodDays = 4, installment = 3_000_000, nextDueDay = today + 3, mandatory = true,
                    };
                case "chu_ba":
                    return new Debt
                    {
                        id = "chu_ba", name = "Khoản vay cũ của chú Ba", creditor = "Chú Ba", remaining = 15_000_000,
                        interestPerPeriod = 0.02f, periodDays = 4, installment = 5_500_000, nextDueDay = today + 3, mandatory = true,
                    };
                case "bao_lanh":
                    return new Debt
                    {
                        id = "bao_lanh", name = "Nợ bảo lãnh cho bạn của bố", creditor = "Anh Khải", remaining = 25_000_000,
                        interestPerPeriod = 0.03f, periodDays = 4, installment = 9_000_000, nextDueDay = today + 3, mandatory = true,
                    };
                case "no_lon":
                    return new Debt
                    {
                        id = "no_lon", name = "Món nợ của chi nhánh đối tác", creditor = "Anh Khải", remaining = 60_000_000,
                        interestPerPeriod = 0.05f, periodDays = 4, installment = 20_000_000, nextDueDay = today + 3, mandatory = true,
                    };
            }
            return null;
        }

        public static Debt MakeShark(long amount, int today) => new Debt
        {
            id = "shark_" + today, kind = Debt.Shark, name = "Vay nóng (tín dụng đen)", creditor = "Nhóm cho vay nóng",
            remaining = amount * 3 / 2, interestPerPeriod = 0.1f, periodDays = 3, installment = amount / 2,
            nextDueDay = today + 2, mandatory = true,
        };

        public static readonly List<ChapterDef> Chapters = BuildChapters();

        public static ChapterDef GetChapter(int n) => Chapters[Mathf.Clamp(n, 1, Chapters.Count) - 1];

        public static readonly ChapterDef FreeMode = BuildFreeMode();

        static ChapterDef BuildFreeMode()
        {
            var c = Base(50, "Kinh doanh tự do", "Quán mở cửa mãi mãi", 7);
            c.title = "Kinh doanh tự do";
            return c;
        }

        public static int ChapterOfDay(int day)
        {
            int start = 1;
            foreach (var ch in Chapters)
            {
                if (day < start + ch.days) return ch.number;
                start += ch.days;
            }
            return Chapters.Count;
        }

        static ChapterDef Base(int n, string title, string subtitle, int days)
        {
            var c = new ChapterDef
            {
                number = n, title = $"Chương {n}: {title}", subtitle = subtitle, days = days,
                patienceMul = n <= 5 ? 1.15f : n <= 15 ? 1.05f : 1f,
                rentPerDay = (long)Math.Round((120_000 + 4_000.0 * (n - 1)) / 10_000.0) * 10_000,
                utilityPerDay = (long)Math.Round((80_000 + 2_000.0 * (n - 1)) / 10_000.0) * 10_000,
                forceAboveThreshold = n >= 11,
                taxMethod = n >= 21 ? TaxMethod.ProfitPercent : TaxMethod.RevenuePercent,
                pitRate = n >= 21 ? 0.15f : 0.015f,
            };
            c.fail = n >= 46
                ? new[] { L("anh", "Chưa được... Phải tính kỹ từng đồng, từng tô mì. Làm lại!") }
                : new[] { L("me", "Không sao đâu con. Mình làm lại chương này, lần này tính toán kỹ hơn.") };
            return c;
        }

        static string Money(long v) => Finance.Money(v);

        static List<ChapterDef> BuildChapters()
        {
            var list = new List<ChapterDef>();
            ChapterDef c;

            c = Base(1, "Tô mì đầu tiên", "Khởi nghiệp với 20 triệu của mẹ", 5);
            c.intro = new[]
            {
                L("narr", "Hà Nội, năm 2026. Trong một con ngõ nhỏ trên phố Thái Hà, quận Đống Đa..."),
                L("anh", "Sau 3 năm làm văn phòng, cuối cùng mình cũng có quán của riêng mình: TIỆM MỲ CAY CỦA CÚN IÊU!"),
                L("me", "Đây là 20 triệu mẹ dành dụm, mẹ không lấy lãi. Con cầm làm vốn, nhưng ráng xoay trả mẹ trước hết ngày 15 nha — đúng hôm đó mẹ cần một khoản cho việc riêng của mẹ."),
                L("nam", "Mặt bằng cô tính con 120 nghìn một ngày, điện nước gas chừng 80 nghìn nữa. Cuối ngày cô thu, sòng phẳng nha."),
                L("tu", "Chú là Tư, mối hàng ở chợ đầu mối Long Biên. Bữa nay khai trương chú chở hàng tới liền. Từ lúc mở cửa trở đi, cháu ĐẶT HÀNG thì phải chờ chú giao 1 phút đó nha. Gấp thì chạy chợ mua lẻ, 5 giây là có nhưng đắt hơn, lại mất tiền xe ôm."),
                L("lan", "Chị là Lan, cán bộ thuế phường. Từ 2026 hộ kinh doanh TỰ KHAI, TỰ NỘP. Cứ 7 ngày một kỳ, hạn nộp 3 ngày sau khi hết kỳ. Trễ là bị phạt nặng đó em."),
                L("anh", "Dạ em nhớ rồi! Bắt đầu thôi!"),
            };
            c.outro = new[] { L("me", "Tuần đầu mà bán được vậy là giỏi lắm rồi con."), L("na", "Chị ơi mai em rủ cả lớp tới nha!") };
            c.events.Add(EvEnd(3, EventAction.ReputationChange, 10, 0, null,
                L("na", "Chị ơi, mì cấp 7 cay xé lưỡi luôn á! Mai em rủ cả lớp tới ăn!"), L("anh", "Cảm ơn em nha! (Uy tín quán tăng)")));
            list.Add(c);

            c = Base(2, "Khách quen", "Giữ chân khách bằng tô mì đúng vị", 5);

            c.intro = new[]
            {
                L("sau", "Ê cháu, trưa nào chú cũng ghé. Nhớ giữ đúng vị nha, đổi vị là chú bỏ đó!"),
                L("anh", "Từ hôm nay phải ĐẶT HÀNG TRƯỚC với chú Tư. Thịt bò, nước dùng chỉ để được 2 ngày, đặt dư là bỏ phí."),
                L("narr", "MẸO: đặt sỉ từ buổi sáng, trước khi mở cửa (hàng về sau 1 phút). Mua lẻ 5 giây là có nhưng đắt hơn và mất 30 nghìn xe ôm mỗi lần. Mở cửa mà tủ trống thì khách bỏ về, ráng chịu!"),
            };
            c.outro = new[] { L("sau", "Được đó cháu. Mà trưa chú muốn ăn đĩa cơm, quán có bán không?") };
            c.events.Add(EvEnd(2, EventAction.SpillBroth, 150_000, 0, null,
                L("anh", "Á! Trượt chân ở bếp, cả nồi nước lẩu đổ lênh láng ra sàn, bỏng cả tay rồi..."),
                L("narr", "Toàn bộ nước lẩu trong tủ phải bỏ. Sáng mai nhớ ĐẶT nước lẩu sớm, không thì mở cửa sẽ không có nước dùng mà nấu.")));
            list.Add(c);

            c = Base(3, "Thêm món cơm", "Cơm trưa văn phòng cho dân phố Thái Hà", 6);

            c.intro = new[]
            {
                L("anh", "Từ hôm nay quán bán thêm cơm sườn, cơm sườn nướng, cơm tấm sườn bì chả!"),
                L("tu", "Sườn cốt lết chú lấy ở lò mổ, nhưng sườn tươi chỉ để được 2 ngày thôi nha."),
                L("narr", "Vào mục THỰC ĐƠN để bật/tắt món và nâng cấp bếp, bàn."),
            };
            c.outro = new[] { L("sau", "Đĩa cơm sườn này ăn trưa là chuẩn bài, dân văn phòng quanh đây khen nức nở!") };
            c.events.Add(Talk(4, false, L("tu", "Dạo này heo lên giá. Cháu đặt hàng vừa đủ thôi, để lâu hư là lỗ đó.")));
            list.Add(c);

            c = Base(4, "Cô nhân viên đầu tiên", "Thêm người, thêm chi phí", 6);
            c.goalReputation = 3.4f;
            c.intro = new[]
            {
                L("vy", "Dạ em chào chị, em là Vy, sinh viên năm hai. Em xin làm bán thời gian ạ. Lương em xin 180 nghìn một ngày."),
                L("anh", "Được, em đi làm luôn nhé! (Có Vy: khách kiên nhẫn hơn 15%. Vy lo bưng món ra các khu bên ngoài, rảnh tay thì phụ chị ở sân trước)"),
            };
            c.outro = new[] { L("vy", "Chị Ánh ơi, mai là hết kỳ thuế đó, chị nhớ nộp nha!") };
            c.events.Add(Ev(1, EventAction.HireStaff, 180_000, 0, null));
            list.Add(c);

            c = Base(5, "Quên hạn thuế", "Lỡ một ngày là mất cả tháng lời", 6);
            c.goalNoOverdueTax = true;
            c.intro = new[]
            {
                L("lan", "Ánh ơi, kỳ thuế vừa rồi em chưa nộp tờ khai đó. Trễ 1 ngày ở đây là trễ gần 2 tuần ngoài đời!"),
                L("lan", "Trễ 6 ngày thực là phạt 2,5 triệu, để quá lâu là cưỡng chế, tịch thu tài sản. Tái phạm nhiều lần là ĐI TÙ vì tội trốn thuế."),
                L("anh", "Dạ em vô mục THUẾ nộp ngay ạ!"),
            };
            c.outro = new[] { L("lan", "Tốt lắm, quán em khai nộp đúng hạn. Cứ giữ vậy nha.") };
            c.events.Add(EvEnd(3, EventAction.Loss, 300_000, 0, "Thay vòi nước bồn rửa",
                L("anh", "Ối, vòi nước bồn rửa bát bể toác, nước phun tung tóe cả bếp!"),
                L("narr", "Gọi thợ tới thay vòi mới hết 300 nghìn. Đồ dùng trong bếp nên kiểm tra thường xuyên.")));
            list.Add(c);

            c = Base(6, "Món nợ của bố", "Trả nợ đúng hạn hoặc mất quán", 9);
            c.goalDebtCleared = "no_bo";
            c.goalText = "Trả hết \"Nợ cũ của bố\" (8 triệu + lãi 3%/kỳ) trong 9 ngày. Mỗi 4 ngày phải trả ít nhất 3 triệu. Trễ 2 kỳ là mất quán!";
            c.intro = new[]
            {
                L("ba", "Ánh à... bố xin lỗi con. Hồi trước bố vay anh Khải 8 triệu để sửa xe chạy hàng, rồi làm ăn thua lỗ..."),
                L("khai", "Tôi không làm khó cô. Cứ 4 ngày trả tôi 3 triệu, lãi 3% mỗi kỳ trên số còn lại. Trễ hai kỳ thì cái quán này coi như của tôi."),
                L("anh", "Anh yên tâm, em sẽ trả đủ, đúng hạn. (Vào SỔ SÁCH > tab NỢ để trả)"),
            };
            c.outro = new[] { L("khai", "Cô sòng phẳng, tôi nể."), L("ba", "Con gái bố giỏi quá...") };
            c.events.Add(Ev(1, EventAction.AddDebt, 0, 0, "no_bo"));
            c.events.Add(Talk(4, false, L("khai", "Hôm nay tới hạn kỳ đầu rồi nha. Cuối ngày tôi qua lấy 3 triệu.")));
            list.Add(c);

            c = Base(7, "Trộm đột nhập", "Một đêm mất ngủ", 6);

            c.intro = new[] { L("congan", "Dạo này khu này có mấy vụ trộm đột nhập ban đêm. Bà con nhớ khóa cửa kỹ, đừng để nhiều tiền mặt trong quán.") };
            c.outro = new[] { L("congan", "Bên anh đã bắt được đối tượng. Chú ý gửi tiền ngân hàng, đừng giữ nhiều tiền mặt nha.") };
            c.events.Add(Ev(2, EventAction.Theft, 3_000_000, 0, null,
                L("vy", "Chị Ánh ơi! Sáng ra cửa cuốn bị cạy, ngăn kéo tiền trống trơn, tủ đông cũng mất một nửa thịt!"),
                L("congan", "Anh đã lập biên bản. Em kiểm tra lại hàng hóa, đặt hàng bù để còn bán.")));
            list.Add(c);

            c = Base(8, "Mùa mưa", "Mưa chiều nào phố Thái Hà cũng ngập", 6);

            c.intro = new[] { L("tu", "Mưa ngập đường, xe hàng của chú đi chậm lắm. Cháu nhớ đặt hàng sớm hơn nha.") };
            c.outro = new[] { L("sau", "Mưa gió mà quán vẫn mở, chú phục cháu.") };
            c.events.Add(Ev(1, EventAction.SupplierDelay, 150, 6, null));
            c.events.Add(Ev(2, EventAction.CustomerMod, 75, 3, null, L("narr", "Mưa lớn suốt 3 ngày, khách vắng hơn khoảng 25%.")));
            c.events.Add(EvEnd(4, EventAction.ClosedDay, 0, 0, null,
                L("vy", "Chị ơi, nước ngập tới đầu gối rồi. Mai chắc phải đóng cửa một bữa..."), L("anh", "Thôi đành nghỉ một ngày dọn dẹp vậy.")));
            list.Add(c);

            c = Base(9, "Hàng trôi nổi", "Rẻ thì ai cũng thích, nhưng...", 6);
            c.goalReputation = 3.5f;
            c.intro = new[] { L("narr", "Một người đàn ông lạ chạy xe ba gác ghé quán...") };
            c.outro = new[] { L("attp", "Quán giữ vệ sinh tốt. Nhớ luôn lấy hàng có hóa đơn, nguồn gốc rõ ràng.") };
            c.events.Add(Ev(1, EventAction.ShadySupplier, 0, 0, null,
                L("la", "Thịt bò, tôm, mực đây, rẻ bằng nửa giá chợ! Không cần hóa đơn chứng từ gì hết, lấy không em?")));
            c.events.Add(Ev(4, EventAction.HealthInspection, 6_000_000, 0, null,
                L("attp", "Chào em, đoàn kiểm tra vệ sinh an toàn thực phẩm của quận. Cho chị xem nguồn gốc nguyên liệu của quán.")));
            list.Add(c);

            c = Base(10, "Mì cay lên mạng", "Clip TikTok 2 triệu lượt xem", 6);
            c.patienceMul = 0.95f;
            c.intro = new[]
            {
                L("linh", "Hello cả nhà! Hôm nay Linh thử thách mì cay CẤP 7 của tiệm nhỏ trong ngõ Thái Hà nè... ÔI CAY QUÁ!"),
                L("vy", "Chị Ánh ơi! Clip của chị Linh lên xu hướng rồi!"),
                L("anh", "Vậy là sắp đông lắm đây. Phải đặt hàng nhiều hơn, nâng cấp thêm bếp, thêm bàn!"),
            };
            c.outro = new[] { L("linh", "Linh sẽ quay lại! Quán xứng đáng được biết tới nhiều hơn."), L("lan", "Doanh thu tăng thì thuế cũng tăng nha em...") };
            c.events.Add(Ev(1, EventAction.CustomerMod, 135, 4, null));
            list.Add(c);

            c = Base(11, "Vượt ngưỡng 1 tỷ", "Từ giờ kỳ nào cũng phải nộp thuế", 6);
            c.goalNoOverdueTax = true;
            c.intro = new[]
            {
                L("lan", "Cơ quan thuế xác định doanh thu cả năm của quán đã vượt 1 tỷ, nên từ giờ MỌI kỳ đều phát sinh thuế: GTGT 3% doanh thu và TNCN 1,5% trên phần vượt ngưỡng."),
                L("anh", "Dạ, vậy phải để dành tiền thuế ra riêng mới được."),
            };
            c.outro = new[] { L("lan", "Em khai nộp đầy đủ, chị ghi nhận.") };
            list.Add(c);

            c = Base(12, "Bếp hư", "Cái bếp gas cũ đã tới tuổi", 6);

            c.intro = new[] { L("vy", "Chị ơi, bếp số 1 lửa cứ phụt phụt, có mùi gas nữa!") };
            c.outro = new[] { L("thoxua", "Bếp tốt thì phải bảo dưỡng định kỳ nha cô.") };
            c.events.Add(Ev(2, EventAction.StoveBroken, 1_500_000, 4, null,
                L("thoxua", "Van gas với béc bếp hư hết rồi. Sửa liền hết 1,5 triệu. Không sửa thì bếp số 1 nghỉ 4 ngày, mà để vậy nguy hiểm lắm.")));
            list.Add(c);

            c = Base(13, "Cuộc gọi lạ", "Giả danh cán bộ thuế", 6);

            c.intro = new[] { L("narr", "Điện thoại reo, một số máy lạ...") };
            c.outro = new[] { L("lan", "Cơ quan thuế KHÔNG BAO GIỜ gọi điện yêu cầu chuyển tiền vào tài khoản cá nhân. Có gì em cứ lên phường hỏi chị.") };
            c.events.Add(Ev(2, EventAction.Scam, 4_000_000, 0, "Chuyển 4 triệu",
                L("la", "Alo, tôi là cán bộ Chi cục Thuế. Hộ kinh doanh của chị đang nợ thuế, phải chuyển ngay 4 triệu vào tài khoản này để tránh bị khóa mã số thuế trong hôm nay!"),
                L("anh", "(Ủa... sao lại chuyển vào tài khoản cá nhân? Mà mình nộp thuế đầy đủ rồi mà...)")));
            list.Add(c);

            c = Base(14, "Chủ nhà tăng giá", "Bà Năm muốn tăng tiền mặt bằng", 6);

            c.intro = new[] { L("nam", "Con ơi, thấy quán đông khách cô mừng. Mà giá cả lên quá, cô tăng tiền mặt bằng thêm 60 nghìn một ngày nghen.") };
            c.outro = new[] { L("nam", "Con làm ăn đàng hoàng, cô yên tâm cho thuê lâu dài.") };
            c.events.Add(Ev(1, EventAction.RentChange, c.rentPerDay + 60_000, 0, null));
            list.Add(c);

            c = Base(15, "Đối thủ bên kia đường", "Mì Cay Đại Hàn giảm giá 30%", 7);
            c.goalReputation = 3.8f;
            c.intro = new[]
            {
                L("tuan", "Chào hàng xóm. Tôi là Tuấn, chủ Mì Cay Đại Hàn. Khai trương giảm 30%, khách bên cô chắc qua bên tôi hết đấy."),
                L("narr", "GỢI Ý: có thể giảm giá 10% trong THỰC ĐƠN để kéo khách, hoặc giữ chất lượng: nấu đúng độ cay, phục vụ nhanh."),
            };
            c.outro = new[] { L("tuan", "Tôi phải công nhận... mì của cô ngon hơn.") };
            c.events.Add(Ev(1, EventAction.CustomerMod, 75, 4, null));
            c.events.Add(EvEnd(5, EventAction.ReputationChange, 10, 0, null, L("na", "Bên kia rẻ mà mì nhạt lắm chị ơi, tụi em quay về đây nè!")));
            list.Add(c);

            c = Base(16, "Vy xin tăng lương", "Người làm tốt thì phải giữ", 6);

            c.intro = new[] { L("vy", "Chị Ánh... em làm cũng lâu rồi, quán đông hơn trước nhiều. Em xin tăng lương lên 250 nghìn một ngày được không chị?") };
            c.outro = new[] { L("vy", "Em sẽ cố gắng hơn nữa!") };
            c.events.Add(Ev(1, EventAction.StaffRaise, 250_000, 0, null));
            list.Add(c);

            c = Base(17, "Kiểm tra PCCC", "An toàn cháy nổ quán ăn", 6);

            c.intro = new[] { L("pccc", "Quán ăn dùng bếp gas phải có bình chữa cháy, cảm biến khói và lối thoát hiểm. Tuần này bên anh đi kiểm tra.") };
            c.outro = new[] { L("pccc", "An toàn là trên hết nha em.") };
            c.events.Add(Ev(3, EventAction.FireInspection, 2_000_000, 0, null,
                L("pccc", "Quán em còn thiếu bình chữa cháy và cảm biến khói. Trang bị đủ hết khoảng 2 triệu. Không trang bị thì phải lập biên bản xử phạt.")));
            list.Add(c);

            c = Base(18, "Mùa lễ đông khách", "Khách đông, việc nhiều, rủi ro cũng nhiều", 6);

            c.intro = new[] { L("narr", "Dịp lễ 2/9, phố xá đông vui. Khách kéo tới quán nườm nượp.") };
            c.outro = new[] { L("anh", "Lễ này bận muốn xỉu mà vui!") };
            c.events.Add(Ev(1, EventAction.CustomerMod, 125, 3, null));
            c.events.Add(EvEnd(2, EventAction.Loss, 500_000, 0, "Mua lại bát đĩa bị vỡ",
                L("anh", "Đông khách quá, bưng vội trượt tay làm vỡ cả chồng bát đĩa..."),
                L("narr", "Mua lại bát đĩa mới hết 500 nghìn.")));
            list.Add(c);

            c = Base(19, "Mẹ bệnh", "Gia đình là trên hết", 7);

            c.goalDebtCleared = "*shark";
            c.intro = new[] { L("ba", "Ánh ơi, mẹ con nhập viện rồi, bác sĩ nói phải mổ gấp...") };
            c.outro = new[] { L("me", "Mẹ khỏe rồi, con đừng lo. Con làm ăn vất vả quá...") };
            c.events.Add(Ev(2, EventAction.PersonalExpense, 7_000_000, 0, "Viện phí cho mẹ",
                L("anh", "Viện phí, tiền mổ hết 7 triệu. Phải lấy tiền quán ra lo cho mẹ trước đã.")));
            c.events.Add(Ev(3, EventAction.LoanShark, 10_000_000, 0, null,
                L("la", "Nghe nói em đang kẹt tiền? Anh cho vay nóng 10 triệu, không cần thế chấp, giải ngân liền!"),
                L("la", "Mỗi 3 ngày góp 5 triệu, tổng trả 15 triệu thôi. Lấy không em?")));
            list.Add(c);

            c = Base(20, "Lễ hội ẩm thực", "Gian hàng ở phố đi bộ Hồ Gươm", 6);
            c.intro = new[] { L("linh", "Lễ hội ẩm thực đường phố tuần này nè! Linh giới thiệu quán mình với ban tổ chức rồi đó!") };
            c.outro = new[] { L("linh", "Gian hàng của quán là đông nhất lễ hội luôn!") };
            c.events.Add(Ev(1, EventAction.CustomerMod, 140, 4, null));
            list.Add(c);

            c = Base(21, "Thuế theo lợi nhuận", "Sổ sách phải rõ ràng từng đồng", 6);
            c.goalNoOverdueTax = true;
            c.intro = new[]
            {
                L("lan", "Quán em giờ nộp TNCN theo LỢI NHUẬN: 15% x (doanh thu − chi phí hợp lý), cộng GTGT 3% doanh thu."),
                L("lan", "Chi phí phải có sổ sách. Tiền phạt, tiền mất trộm KHÔNG được tính là chi phí đâu nha."),
            };
            c.outro = new[] { L("lan", "Sổ sách quán em rõ ràng lắm.") };
            list.Add(c);

            c = Base(22, "Nhà cung cấp trục trặc", "Xe hàng của chú Tư bị hư", 6);

            c.intro = new[] { L("tu", "Xe tải của chú hư hộp số, phải thuê xe ngoài nên hàng về chậm gấp đôi. Cháu đặt sớm giùm chú nha.") };
            c.outro = new[] { L("tu", "Xe sửa xong rồi, cảm ơn cháu thông cảm.") };
            c.events.Add(Ev(1, EventAction.SupplierDelay, 200, 5, null));
            list.Add(c);

            c = Base(23, "Bão giá", "Nguyên liệu tăng giá 30%", 7);

            c.intro = new[]
            {
                L("tu", "Giá heo, giá bò lên hết rồi cháu. Tuần này hàng nào cũng tăng chừng 30%."),
                L("narr", "GỢI Ý: tăng giá bán 10% trong THỰC ĐƠN, hoặc tạm ngưng món dùng nguyên liệu đắt."),
            };
            c.outro = new[] { L("tu", "Giá về lại bình thường rồi, mừng ghê.") };
            c.events.Add(Ev(1, EventAction.PriceSpike, 130, 7, null));
            list.Add(c);

            c = Base(24, "Cúp điện", "Lịch cắt điện luân phiên", 6);

            c.intro = new[] { L("narr", "Điện lực thông báo cắt điện luân phiên để sửa chữa đường dây trong tuần.") };
            c.outro = new[] { L("anh", "Có điện lại rồi! Phải mua máy phát dự phòng thôi.") };
            c.events.Add(Ev(2, EventAction.CustomerMod, 60, 2, null, L("vy", "Cúp điện cả buổi chiều, quán tối thui, khách ngại vào...")));
            c.events.Add(Ev(5, EventAction.Expense, 800_000, 0, "Thuê máy phát điện", L("anh", "Thuê máy phát điện cho hôm nay hết 800 nghìn.")));
            list.Add(c);

            c = Base(25, "Khoản vay của chú Ba", "Món nợ cũ từ thời bố còn buôn bán", 9);
            c.goalDebtCleared = "chu_ba";
            c.goalText = "Trả hết khoản vay cũ của chú Ba (15 triệu + lãi 2%/kỳ) trong 9 ngày, mỗi 4 ngày ít nhất 5,5 triệu.";
            c.intro = new[]
            {
                L("chuba", "Ánh à, chú là Ba, bạn làm ăn với bố cháu. Hồi trước bố cháu vay chú 15 triệu, giấy tờ đây. Giờ chú cần tiền gấp."),
                L("ba", "Đúng là bố có vay... Bố xin lỗi con lần nữa."),
            };
            c.outro = new[] { L("chuba", "Cháu giỏi hơn bố cháu hồi trẻ nhiều.") };
            c.events.Add(Ev(1, EventAction.AddDebt, 0, 0, "chu_ba"));
            list.Add(c);

            c = Base(26, "Hóa đơn điện tử", "Quy định mới cho hộ kinh doanh", 6);

            c.intro = new[] { L("lan", "Hộ kinh doanh doanh thu trên 1 tỷ phải dùng HÓA ĐƠN ĐIỆN TỬ có kết nối máy tính tiền với cơ quan thuế. Cuối tuần này đoàn sẽ đi kiểm tra.") };
            c.outro = new[] { L("lan", "Quán em chấp hành tốt.") };
            c.events.Add(Ev(1, EventAction.EInvoice, 2_000_000, 0, null,
                L("narr", "Phần mềm hóa đơn điện tử + máy tính tiền kết nối cơ quan thuế: khoảng 2 triệu.")));
            c.events.Add(Ev(5, EventAction.TaxAudit, 0, 0, null, L("lan", "Chào em, đoàn kiểm tra việc khai thuế và sử dụng hóa đơn điện tử.")));
            list.Add(c);

            c = Base(27, "Lấn chiếm vỉa hè", "Đợt ra quân lập lại trật tự đô thị", 6);

            c.intro = new[] { L("congan", "Phường ra quân lập lại trật tự vỉa hè. Quán nào kê bàn ra vỉa hè mà không có giấy phép là xử phạt đó em.") };
            c.outro = new[] { L("congan", "Quán em chấp hành tốt.") };
            c.events.Add(Ev(1, EventAction.Sidewalk, 1_000_000, 0, null,
                L("narr", "Quán có hơn 10 bàn thì phải kê ra vỉa hè. Xin giấy phép tạm sử dụng một phần vỉa hè: 1 triệu. Không xin mà có hơn 10 bàn sẽ bị phạt.")));
            list.Add(c);

            c = Base(28, "Khách phản ánh", "Một bài đăng bóc phốt", 6);
            c.goalReputation = 3.8f;
            c.intro = new[] { L("vy", "Chị ơi, có người đăng bài nói ăn quán mình bị đau bụng, cả ngàn lượt chia sẻ rồi!") };
            c.outro = new[] { L("linh", "Linh đã ghé kiểm chứng: quán sạch sẽ, mì ngon. Bài bóc phốt kia là sai sự thật!") };
            c.events.Add(Ev(1, EventAction.ReputationChange, -50, 0, null));
            c.events.Add(Ev(1, EventAction.CustomerMod, 80, 3, null));
            c.events.Add(Ev(3, EventAction.HealthInspection, 8_000_000, 0, null, L("attp", "Có phản ánh về vệ sinh thực phẩm nên đoàn xuống kiểm tra đột xuất.")));
            list.Add(c);

            c = Base(29, "Trộm lần hai", "Lần này chúng nhắm vào tủ đông", 6);

            c.intro = new[] { L("congan", "Băng trộm chuyên đột nhập quán ăn lại xuất hiện. Em lắp camera, gửi tiền vào ngân hàng nha.") };
            c.outro = new[] { L("congan", "Đã triệt phá băng trộm. Cảm ơn em đã cung cấp hình ảnh camera.") };
            c.events.Add(Ev(1, EventAction.Invest, 1_200_000, 0, "Lắp camera an ninh", L("anh", "Lắp camera an ninh hết 1,2 triệu. Thôi, của đi thay người.")));
            c.events.Add(Ev(3, EventAction.Theft, 8_000_000, 0, null, L("vy", "Chị ơi! Lại bị trộm nữa rồi! Ổ khóa bị cắt, mất tiền với cả tủ đông!")));
            list.Add(c);

            c = Base(30, "Mở rộng quán", "Thuê thêm mặt bằng bên cạnh", 7);
            c.intro = new[]
            {
                L("nam", "Căn bên cạnh trống, cô cho con thuê luôn để mở rộng. Tiền mặt bằng tăng thêm 80 nghìn một ngày."),
                L("anh", "Thêm chỗ thì phải thêm bàn, thêm bếp. Cố lên!"),
            };
            c.outro = new[] { L("vy", "Quán mình giờ to gấp đôi rồi chị ơi!") };
            c.events.Add(Ev(1, EventAction.RentChange, c.rentPerDay + 80_000, 0, null));
            c.events.Add(Ev(1, EventAction.CustomerMod, 115, 7, null));
            list.Add(c);

            c = Base(31, "Món mới, khách mới", "Thực đơn đầy đủ nhất từ trước tới nay", 6);
            c.intro = new[] { L("anh", "Thêm mì tương đen, cơm ba chỉ rim, cơm chiên kim chi. Khách giới trẻ chắc sẽ thích!") };
            c.outro = new[] { L("na", "Mì tương đen ngon xỉu chị ơi!") };
            c.events.Add(Ev(1, EventAction.CustomerMod, 120, 3, null));
            list.Add(c);

            c = Base(32, "Bố nằm viện", "Tai nạn giao thông", 6);

            c.intro = new[] { L("me", "Ánh ơi, bố con bị xe tông trên đường đi chợ, đang nằm ở bệnh viện Bạch Mai...") };
            c.outro = new[] { L("ba", "Bố khỏe rồi, cảm ơn con đã lo cho bố.") };
            c.events.Add(Ev(1, EventAction.PersonalExpense, 12_000_000, 0, "Viện phí cho bố", L("anh", "Viện phí hết 12 triệu. Bảo hiểm chỉ chi trả một phần...")));
            c.events.Add(Ev(2, EventAction.StaffAway, 0, 2, null, L("vy", "Chị lo cho bác đi, mà... em bị sốt xuất huyết, phải nghỉ 2 ngày...")));
            list.Add(c);

            c = Base(33, "Cúp nước", "Đường ống chính bị vỡ", 6);

            c.intro = new[] { L("narr", "Đường ống nước chính của khu phố bị vỡ. Công ty cấp nước thông báo cúp nước để sửa.") };
            c.outro = new[] { L("anh", "Có nước lại rồi. Phải lắp bồn chứa dự phòng thôi.") };
            c.events.Add(EvEnd(2, EventAction.ClosedDay, 0, 0, null, L("vy", "Mai cúp nước cả ngày, không có nước nấu mì, rửa chén. Mình phải đóng cửa thôi chị.")));
            c.events.Add(Ev(4, EventAction.Invest, 1_500_000, 0, "Lắp bồn nước dự phòng", L("anh", "Lắp bồn chứa nước dự phòng hết 1,5 triệu.")));
            list.Add(c);

            c = Base(34, "Thanh tra thuế đột xuất", "Rà soát toàn bộ các kỳ đã khai", 6);
            c.goalNoOverdueTax = true;
            c.intro = new[] { L("lan", "Tuần này có đoàn thanh tra thuế của thành phố về rà soát hộ kinh doanh doanh thu lớn. Em chuẩn bị sổ sách nha.") };
            c.outro = new[] { L("lan", "Đoàn đánh giá quán em tuân thủ tốt.") };
            c.events.Add(Ev(3, EventAction.TaxAudit, 0, 0, null, L("lan", "Đoàn thanh tra đã tới. Cho chị xem các kỳ khai thuế và hóa đơn.")));
            list.Add(c);

            c = Base(35, "Hàng trôi nổi lần hai", "Cám dỗ khi giá cả leo thang", 6);
            c.goalReputation = 4.0f;
            c.intro = new[] { L("tu", "Dạo này giá lên, chú nghe nhiều quán lấy hàng trôi nổi lắm. Cháu cẩn thận đó.") };
            c.outro = new[] { L("attp", "Quán vẫn giữ được chất lượng, rất tốt.") };
            c.events.Add(Ev(1, EventAction.PriceSpike, 115, 6, null));
            c.events.Add(Ev(2, EventAction.ShadySupplier, 0, 0, null, L("la", "Hàng đông lạnh nhập lậu nè, rẻ hơn 60%. Mấy quán lớn lấy hết đó, em lấy không?")));
            c.events.Add(Ev(5, EventAction.HealthInspection, 15_000_000, 0, null, L("attp", "Đoàn liên ngành kiểm tra nguồn gốc thực phẩm đông lạnh trên địa bàn.")));
            list.Add(c);

            c = Base(36, "Nhân viên nghỉ đột ngột", "Vy về quê chịu tang", 6);

            c.intro = new[] { L("vy", "Chị ơi... ông nội em mất, em phải về quê 4 ngày. Em xin lỗi chị...") };
            c.outro = new[] { L("vy", "Em lên rồi nè chị. Cảm ơn chị đã cho em nghỉ.") };
            c.events.Add(Ev(1, EventAction.StaffAway, 0, 4, null));
            list.Add(c);

            c = Base(37, "Đường đang sửa", "Rào chắn trước cửa quán suốt 5 ngày", 7);

            c.intro = new[] { L("narr", "Quận sửa đường, rào chắn chắn ngang trước cửa quán. Khách phải đi bộ vào...") };
            c.outro = new[] { L("sau", "Đường mới láng o, chạy xe tới quán sướng ghê!") };
            c.events.Add(Ev(1, EventAction.CustomerMod, 70, 5, null));
            list.Add(c);

            c = Base(38, "Đòi nợ thuê", "Khoản bảo lãnh của bố", 9);
            c.goalDebtCleared = "bao_lanh";
            c.goalText = "Trả hết nợ bảo lãnh 25 triệu (lãi 3%/kỳ) trong 9 ngày, mỗi 4 ngày ít nhất 9 triệu. Trễ 2 kỳ là mất quán!";
            c.intro = new[]
            {
                L("khai", "Lại là tôi đây. Bố cô đứng ra bảo lãnh cho ông bạn vay 25 triệu, giờ ông ta bỏ trốn rồi. Bố cô phải trả."),
                L("ba", "Bố ngu quá... Bố tin người quá..."),
                L("anh", "Thôi bố đừng tự trách. Anh Khải, em sẽ trả. Nhưng đừng cho người tới quán làm phiền khách."),
            };
            c.outro = new[] { L("khai", "Cô là người đàng hoàng. Từ giờ tôi không tới làm phiền nữa.") };
            c.events.Add(Ev(1, EventAction.AddDebt, 0, 0, "bao_lanh"));
            list.Add(c);

            c = Base(39, "Giá gas tăng", "Điện, nước, gas đều tăng", 6);

            c.intro = new[] { L("nam", "Giá gas, giá điện lên hết rồi con. Tiền điện nước gas giờ chắc phải thêm 100 nghìn một ngày.") };
            c.outro = new[] { L("anh", "Chi phí cố định cao thì phải bán nhiều hơn thôi.") };
            c.events.Add(Ev(1, EventAction.UtilityChange, c.utilityPerDay + 100_000, 0, null));
            list.Add(c);

            c = Base(40, "Giải thưởng quán ngon", "Cuộc bình chọn của người Hà Nội", 7);
            c.goalReputation = 4.1f;
            c.intro = new[] { L("linh", "Quán được đề cử giải \"Quán ngon bình dân Hà Nội 2026\"! Giám khảo sẽ ghé ăn bí mật đó!") };
            c.outro = new[] { L("linh", "TIỆM MỲ CAY CỦA CÚN IÊU đoạt giải rồi!!!") };
            c.events.Add(Ev(1, EventAction.CustomerMod, 120, 7, null));
            list.Add(c);

            c = Base(41, "Lừa đảo đặt tiệc", "Một đơn hàng quá hời", 6);

            c.intro = new[] { L("narr", "Một khách gọi điện đặt tiệc 50 suất cho công ty...") };
            c.outro = new[] { L("congan", "Đường dây giả danh đặt tiệc vừa bị triệt phá. Ai cũng cẩn thận nha.") };
            c.events.Add(Ev(2, EventAction.Scam, 6_000_000, 0, "Chuyển 6 triệu đặt cọc",
                L("la", "Chị ơi, công ty em đặt 50 suất. Nhưng sếp em muốn uống rượu vang ở chỗ quen, chị chuyển giúp 6 triệu đặt rượu trước, mai em thanh toán gộp nha!"),
                L("anh", "(Đơn to quá... nhưng sao lại bắt mình ứng tiền mua rượu?)")));
            list.Add(c);

            c = Base(42, "Mùa Tết", "Cao điểm trước Tết, vắng tanh sau Tết", 7);
            c.intro = new[] { L("me", "Tết tới rồi, con nhớ để dành tiền lì xì cho mấy đứa nhỏ nha."), L("vy", "Trước Tết khách đông lắm, nhưng mùng 1 mùng 2 thì vắng hoe à anh.") };
            c.outro = new[] { L("me", "Năm nay nhà mình ăn Tết đủ đầy rồi con.") };
            c.events.Add(Ev(1, EventAction.CustomerMod, 150, 4, null));
            c.events.Add(Ev(5, EventAction.CustomerMod, 55, 3, null));
            c.events.Add(Ev(5, EventAction.PersonalExpense, 3_000_000, 0, "Lì xì, quà Tết", L("anh", "Lì xì, quà Tết cho gia đình hết 3 triệu.")));
            list.Add(c);

            c = Base(43, "Chủ nhà đòi lại mặt bằng", "Đặt cọc hay tăng giá", 7);

            c.intro = new[] { L("nam", "Con trai cô muốn lấy lại mặt bằng mở tiệm. Con muốn thuê tiếp thì đặt cọc thêm 15 triệu, hoặc cô tăng tiền thuê lên 1,5 lần.") };
            c.outro = new[] { L("nam", "Ừ, vậy con cứ thuê tiếp nha.") };
            c.events.Add(Ev(1, EventAction.Expense, 0, 0, "deposit"));
            list.Add(c);

            c = Base(44, "Bão lũ", "Mưa lớn kỷ lục, cả thành phố ngập", 7);

            c.intro = new[] { L("narr", "Bão số 9 đổ bộ, mưa lớn kỷ lục. Đường về chợ đầu mối ngập, rau thịt khan hiếm.") };
            c.outro = new[] { L("tu", "Cháu trụ được qua mùa bão là giỏi. Nhiều quán quanh đây đóng cửa rồi.") };
            c.events.Add(Ev(1, EventAction.PriceSpike, 125, 7, null));
            c.events.Add(Ev(1, EventAction.SupplierDelay, 180, 7, null));
            c.events.Add(Ev(2, EventAction.CustomerMod, 70, 4, null));
            c.events.Add(EvEnd(3, EventAction.ClosedDay, 0, 0, null, L("vy", "Nước ngập vào tận bếp rồi chị ơi! Mai phải đóng cửa dọn dẹp...")));
            list.Add(c);

            c = Base(45, "Chuẩn bị chi nhánh", "Gom vốn mở quán thứ hai", 8);
            c.goalReputation = 4.1f;
            c.intro = new[]
            {
                L("anh", "Con muốn mở chi nhánh thứ hai ở Cầu Giấy, gần Đại học Quốc gia trên đường Xuân Thủy."),
                L("tuan", "Nếu cô mở chi nhánh, tôi muốn hợp tác. Tôi lo bếp, cô lo công thức."),
                L("narr", "Từ chương sau là 5 thử thách cuối cùng, RẤT KHÓ. Hãy nâng cấp tối đa bếp, quầy, bàn và để dành tiền dự phòng!"),
            };
            c.outro = new[] { L("tuan", "Chi nhánh mới sẵn sàng rồi. Nhưng... có vài chuyện không hay đang tới.") };
            list.Add(c);

            c = Base(46, "Bão giá kép", "Giá tăng 45%, chợ thiếu hàng, xe hàng chậm", 8);
            c.hard = true;
            c.ingredientPriceMul = 1.45f; c.shortageChance = 0.3f; c.orderDelayMul = 1.8f; c.rentPerDay += 60_000;
            c.intro = new[]
            {
                L("tu", "Lần này căng thật rồi cháu. Giá tăng gần một nửa, xe hàng kẹt, chợ lẻ thì hết hàng liên tục."),
                L("anh", "Phải đặt hàng thật chính xác, tăng giá bán, tắt món lỗ. Không được phí một vắt mì nào."),
            };
            c.outro = new[] { L("tu", "Qua được trận này thì cháu không sợ gì nữa.") };
            list.Add(c);

            c = Base(47, "Thanh tra liên ngành", "Thuế, vệ sinh, PCCC cùng lúc", 8);
            c.hard = true;
            c.goalNoOverdueTax = true; c.goalReputation = 4.4f; c.patienceMul = 0.9f;
            c.intro = new[] { L("lan", "Tuần này đoàn liên ngành thuế, vệ sinh an toàn thực phẩm, PCCC sẽ kiểm tra toàn diện các quán lớn. Chuẩn bị kỹ nha em.") };
            c.outro = new[] { L("lan", "Quán em đạt tất cả các tiêu chí. Chị rất tự hào.") };
            c.events.Add(Ev(2, EventAction.TaxAudit, 0, 0, null, L("lan", "Đoàn thanh tra thuế đã tới.")));
            c.events.Add(Ev(4, EventAction.HealthInspection, 20_000_000, 0, null, L("attp", "Đoàn kiểm tra vệ sinh an toàn thực phẩm.")));
            c.events.Add(Ev(6, EventAction.FireInspection, 4_000_000, 0, null, L("pccc", "Kiểm tra PCCC quán quy mô lớn: cần hệ thống báo cháy tự động, khoảng 4 triệu.")));
            list.Add(c);

            c = Base(48, "Món nợ khổng lồ", "Chi nhánh đối tác phá sản, bạn phải gánh", 10);
            c.hard = true; c.goalDebtCleared = "no_lon";
            c.goalText = "Trả hết 60 triệu nợ (lãi 5%/kỳ) trong 10 ngày, mỗi 4 ngày ít nhất 20 triệu. Trễ 2 kỳ là mất quán!";
            c.intro = new[]
            {
                L("tuan", "Ánh... chi nhánh Cầu Giấy lỗ nặng, tôi vay anh Khải 60 triệu để cầm cự mà giờ không trả nổi. Cô đứng tên đồng sở hữu..."),
                L("khai", "Hợp đồng ghi rõ cô chịu trách nhiệm liên đới. 4 ngày 20 triệu, lãi 5% mỗi kỳ. Trễ hai kỳ tôi lấy cả hai quán."),
                L("anh", "...Được. Tôi sẽ trả. (Có thể vay ngân hàng trong SỔ SÁCH > NGÂN HÀNG, nhưng phải trả đúng hạn!)"),
            };
            c.outro = new[] { L("khai", "Tôi chưa thấy ai gánh nổi món nợ này. Cô là người đầu tiên."), L("tuan", "Tôi nợ cô cả đời...") };
            c.events.Add(Ev(1, EventAction.AddDebt, 0, 0, "no_lon"));
            c.events.Add(Ev(5, EventAction.Theft, 15_000_000, 0, null, L("vy", "Chị ơi... trộm lại vào nữa rồi. Đúng lúc khó khăn nhất...")));
            list.Add(c);

            c = Base(49, "Cạnh tranh khốc liệt", "Chuỗi mì cay lớn mở ngay đầu ngõ", 8);
            c.hard = true;
            c.customerRate *= 0.8f; c.patienceMul = 0.85f; c.goalReputation = 4.4f;
            c.intro = new[]
            {
                L("tuan", "Chuỗi \"Mì Cay Seoul Express\" 200 chi nhánh vừa mở ngay đầu ngõ, giảm giá 50% cả tháng."),
                L("anh", "Không đấu giá được với họ. Chỉ còn cách: nhanh hơn, ngon hơn, đúng vị hơn."),
            };
            c.outro = new[] { L("sau", "Mấy chuỗi lớn sao bằng tô mì của cháu được!") };
            c.events.Add(EvEnd(3, EventAction.ReputationChange, -30, 0, null, L("vy", "Bên kia thuê người đánh giá 1 sao quán mình trên mạng...")));
            list.Add(c);

            c = Base(50, "Giấc mơ chuỗi mì cay", "Thử thách cuối cùng", 10);
            c.hard = true;
            c.goalReputation = 4.5f; c.goalNoOverdueTax = true; c.patienceMul = 0.9f; c.ingredientPriceMul = 1.15f;
            c.intro = new[]
            {
                L("narr", "Nhà đầu tư muốn rót vốn biến TIỆM MỲ CAY CỦA CÚN IÊU thành chuỗi toàn quốc. Họ sẽ theo dõi quán 10 ngày."),
                L("me", "Con đi xa lắm rồi. Mẹ tin con."),
                L("anh", "10 ngày cuối cùng. Từ một tô mì đầu tiên... tới đây rồi!"),
            };
            c.outro = new[]
            {
                L("narr", "Nhà đầu tư ký hợp đồng. TIỆM MỲ CAY CỦA CÚN IÊU chính thức trở thành chuỗi với 10 chi nhánh khắp Hà Nội."),
                L("vy", "Em được làm quản lý chi nhánh thật hả chị? Em cảm ơn chị!"),
                L("lan", "Hộ kinh doanh nhỏ ngày nào giờ đã thành doanh nghiệp. Chị tự hào về em."),
                L("anh", "Từ một tô mì đầu tiên... cảm ơn tất cả mọi người."),
                L("narr", "HẾT 50 CHƯƠNG. Quán vẫn mở cửa mỗi ngày — tiếp tục kinh doanh tự do, không bao giờ kết thúc!"),
            };
            c.events.Add(Ev(3, EventAction.StoveBroken, 3_000_000, 3, null, L("thoxua", "Bếp số 1 nứt mặt gang rồi. Thay mới 3 triệu, không thay thì nghỉ 3 ngày.")));
            c.events.Add(Ev(6, EventAction.HealthInspection, 25_000_000, 0, null, L("attp", "Nhà đầu tư yêu cầu kiểm tra vệ sinh độc lập.")));
            list.Add(c);

            foreach (var ch in list)
                if (string.IsNullOrEmpty(ch.goalText)) ch.goalText = GoalTextFor(ch);
            return list;
        }

        static string GoalTextFor(ChapterDef c)
        {
            var parts = new List<string>();
            if (c.goalDebtCleared == "*shark") parts.Add("không còn nợ vay nóng");
            if (c.goalReputation.HasValue) parts.Add($"uy tín ≥ {c.goalReputation.Value:0.0}");
            if (c.goalNoOverdueTax) parts.Add("không có kỳ thuế nào quá hạn");
            string head = parts.Count > 0 ? "Khi hết chương: " + string.Join(", ", parts) + ". " : "";
            return head + $"Còn lại cứ bán thật nhiều, lời được bao nhiêu hưởng bấy nhiêu — chỉ cần trụ vững {c.days} ngày, đừng để phá sản (tiền mặt âm 3 ngày liền).";
        }

        public static DayEvent RandomFreeEvent(System.Random rng, GameState s)
        {
            long scale = 1 + s.day / 100;
            switch (rng.Next(9))
            {
                case 0: return Ev(0, EventAction.Theft, 10_000_000 * scale, 0, null, L("vy", "Chị ơi, quán bị trộm đột nhập đêm qua!"));
                case 1: return Ev(0, EventAction.StoveBroken, 2_500_000, 3, null, L("thoxua", "Bếp số 1 hư rồi, sửa liền thì tốn tiền, không thì nghỉ 3 ngày."));
                case 2: return Ev(0, EventAction.CustomerMod, 70, 2, null, L("narr", "Mưa bão 2 ngày, khách vắng hẳn."));
                case 3: return Ev(0, EventAction.PriceSpike, 125, 3, null, L("tu", "Giá hàng tăng mấy bữa nay cháu ơi."));
                case 4: return Ev(0, EventAction.Scam, 5_000_000 * scale, 0, "Chuyển tiền", L("la", "Alo, bên điện lực đây, quán nợ tiền điện, chuyển khoản ngay kẻo bị cắt điện trong 2 tiếng!"));
                case 5: return Ev(0, EventAction.HealthInspection, 20_000_000, 0, null, L("attp", "Đoàn kiểm tra vệ sinh an toàn thực phẩm định kỳ."));
                case 6: return Ev(0, EventAction.TaxAudit, 0, 0, null, L("lan", "Đoàn thanh tra thuế kiểm tra định kỳ."));
                case 7: return Ev(0, EventAction.CustomerMod, 140, 2, null, L("linh", "Linh vừa đăng clip review quán lần nữa nè!"));
                default: return Ev(0, EventAction.ShadySupplier, 0, 0, null, L("la", "Hàng rẻ đây, không cần hóa đơn, lấy không?"));
            }
        }
    }
}
