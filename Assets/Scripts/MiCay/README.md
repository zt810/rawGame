# Tiệm mỳ cay của Cún iêu (Unity 6, 2D, màn hình ngang)

Bối cảnh: một tiệm mỳ cay trong ngõ phố Thái Hà, Đống Đa, Hà Nội.

## Chạy thử
Mở scene bất kỳ (ví dụ `Assets/Scenes/SampleScene.unity`) rồi bấm **Play**.
`GameManager` tự khởi tạo qua `[RuntimeInitializeOnLoadMethod]`, tự dựng toàn bộ UI bằng code,
nên không cần kéo thả gì vào scene. Trong Game view nên chọn tỉ lệ 1920x1080 (16:9) hoặc 2400x1080.

Menu `Mì Cay > Xóa dữ liệu lưu` để chơi lại từ đầu. Bản lưu của phiên bản 7 chương cũ không dùng được, game tự bắt đầu lại.

## Dữ liệu, đồ họa, font
- **Món ăn & nguyên liệu**: `Assets/Resources/Data/recipes.json`, `ingredients.json`. Sửa giá, thêm món không cần biên dịch lại.
  Mỗi nguyên liệu có `icon`/`color`/`color2` để công cụ vẽ biết vẽ hình gì.
- **Đồ họa pixel art**: `python Tools/gen_art.py` vẽ lại toàn bộ PNG vào `Assets/Resources/Art/`
  (nhân vật, chân dung, món, nguyên liệu, mặt tiền quán, khung UI). Thêm món vào JSON rồi chạy lại là có icon.
  Ảnh được nhập kiểu pixel (lọc Point, không nén) nhờ `Editor/PixelArtImporter.cs`; nếu ảnh bị mờ: menu `Mì Cay > Nhập lại đồ họa pixel`.
- **Âm thanh**: `python Tools/gen_sfx.py` tạo 18 hiệu ứng 8-bit vào `Assets/Resources/Audio/`. Thay bằng âm thanh thật: chép file cùng tên đè lên.
  Gọi `Sfx.Play("ten")` trong code; bật/tắt bằng nút loa trên thanh trên cùng (lưu trong PlayerPrefs).
- **Khu ăn uống**: 4 khu (quán chính, khu mở rộng, sân sau, khu vỉa hè), mỗi khu rộng một khung nhìn, vuốt ngang hoặc bấm mũi tên để chuyển khu;
  8 bàn/khu, tối đa 32 bàn (`CustomerSystem.Areas`, `MaxTables`, `TableX/TableY`). Khách đi vào từ mép khu có bàn của mình.
  Chỗ chưa có bàn hiện bàn mờ, chỗ kế tiếp có nút "+ Thêm bàn" (`MainUI.BuildGhostTable`).
- **Bếp**: tối đa 9 bếp (`GameManager.StoveTiers`) và 6 máy pha (`GameManager.BarTiers`, máy mua pha cùng lúc 2 ly); đồ cũ nâng cấp tối đa 2 lần, mua đồ mới thì được hỏi bán đồ kém nhất theo giá khấu hao (`GameManager.Resale`); 1 hàng khi ≤ 6 ô, 2 hàng khi ≤ 12 ô, 3 hàng khi nhiều hơn (ô gọn: hình bên trái, chữ bên phải).
- **Topping**: nguyên liệu có `topping` (chương bắt đầu), `toppingFor` (Noodle/Rice/Drink), `toppingPrice` trong `ingredients.json`;
  món có `"toppings": true` trong `recipes.json` thì khách có thể gọi thêm topping từ ngày 2 (tỉ lệ 20% + 0,8%/chương, tối đa 45%; từ chương 5 có thể gọi 2 topping). Mỗi ngày chỉ có vài loại topping "hot" (2 loại + 1 loại mỗi 8 chương, tối đa 6), báo trước trong ghi chú buổi sáng. Phải cho thêm đúng topping vào nồi, thiếu là hỏng món.
- **Ngày → đêm**: cảnh quán phủ màu theo giờ (10h-16h trong, 16h-18h hoàng hôn, 18h-20h chạng vạng, sau 20h là đêm) — `MainUI.SkyTint`.
- **Hướng dẫn theo từng bài** (`GameManager.Lessons`, `UI/TutorialPointer.cs`): ngày 1 học bán hàng cơ bản (nhập hàng → nấu → giao món → nút Công thức);
  các ngày sau học dần: đặt hàng phải chờ (ngày 2), sổ sách & thuế (ngày 2), tốc độ/tạm dừng (lúc mở cửa ngày 2), thực đơn & nâng cấp (ngày 3), các khu bàn (ngày 4);
  và các bài "khi cần": lần đầu phải nộp thuế, trả nợ theo kỳ, tiền mặt dưới 3 triệu, khách gọi topping, mở khóa ghép nhanh.
  Mỗi sáng tối đa 1 bài giới thiệu; trong bài ngón tay chỉ nút cần bấm, các nút khác bị khóa. Bài đã học lưu trong `GameState.tutDone`.
- **Chủ tiệm**: Ánh (nữ), mã nhân vật `anh`. Người bưng món đi ngang ra mép quầy rồi mới tới bàn; khách nhận món ngồi ăn ~5 giây (`CustomerGroup.EatSeconds`) rồi đi bộ ra mép khu; khách bỏ về cũng đi bộ ra, thông báo ở thanh chat dưới đáy.
- **Font**: VT323 (giấy phép SIL OFL, xem `Assets/Resources/Fonts/VT323-OFL.txt`), đủ dấu tiếng Việt. Chữ tự co cho vừa khung (`UIKit.Fit`).

## Cấu trúc
| Thư mục | Nội dung |
|---|---|
| `Data/Models.cs` | Kiểu dữ liệu: nguyên liệu, công thức, chương, sự kiện, hội thoại |
| `Data/GameDB.cs` | Nạp 62 nguyên liệu (20 loại gọi thêm làm topping được), 33 món (14 mì, 7 cơm, 12 đồ uống) từ JSON |
| `Data/StoryDB.cs` | 19 nhân vật, 50 chương (5 phần), tình huống theo ngày, khoản nợ, sự cố ngẫu nhiên khi kinh doanh tự do |
| `Core/GameState.cs` | Dữ liệu được lưu (JSON): tiền, kho theo lô, sổ thu chi, thống kê ngày, kỳ thuế, nợ |
| `Core/GameManager.cs` | Vòng lặp ngày: Sáng → Mở cửa 10h–21h → Cuối ngày; chương, hướng dẫn ngày 1, tình huống có lựa chọn, đi tù & làm lại, lưu/tải, nâng cấp |
| `Systems/Economy.cs` | Kho (FIFO, hết hạn), Chợ (đặt hàng / mua lẻ có thời gian chờ), Sổ sách & báo cáo, Pháp lý (điểm vi phạm, tịch thu), Thuế, Nợ, Ngân hàng |
| `Systems/Gameplay.cs` | Khách hàng (gọi món, kiên nhẫn, đánh giá), Bếp (ghép nguyên liệu, nấu, giao) |
| `UI/*` | UI pixel art dựng bằng code: HUD, cảnh quán, bếp, các bảng, hội thoại, `Art.cs` nạp ảnh |

## Các thông số cân bằng chính
- `GameManager.DayLengthSeconds` (360s = 6 phút / ngày), `StartCapital` (20 triệu, mẹ cho mượn), `PrisonLoan` (15 triệu vay ngân hàng sau khi ra tù)
- `IngredientDef.OrderSeconds`: thời gian chờ hàng đặt theo loại (đồ uống 60s → hải sản 120s); `Market.RetailDelaySeconds`: mua lẻ chờ 20s. Ngày 1 hàng có ngay (`Market.Instant`)
- `CustomerSystem.TrySpawn`: kiên nhẫn cơ bản 62s + 10s/món (trước là 70 + 12), nhân hệ số chương, nhân viên, x2 khi đang hướng dẫn
- `CustomerSystem.Tick`: khoảng cách khách tới cơ bản là 13s, nhân với hệ số chương, tình huống, uy tín, giá bán và giờ cao điểm
- `KitchenSystem.MasteryForQuickFill`: nấu 3 lần thì mở khóa ghép nhanh; món có yêu cầu riêng của khách (bớt nguyên liệu, không đá/đường, thêm topping) thì không ghép nhanh được
- Không có mục tiêu tiền: chương chỉ yêu cầu trụ vững (không phá sản) và các yêu cầu cốt truyện (trả nợ, không nợ thuế quá hạn, uy tín) — `StoryDB.GoalTextFor`
- `StoryDB.Base(n)`: lượng khách (0,85 → 1,54), tiền mặt bằng, điện nước, luật thuế tăng dần theo chương
- `TaxSystem`: kỳ 7 ngày, hạn nộp +3 ngày, 1 ngày trễ ≈ 13 ngày thực, cưỡng chế sau 7 ngày trễ, bị cưỡng chế lần 3 = đi tù
- `Legal`: đủ 6 điểm vi phạm = đi tù; 10 ngày không vi phạm giảm 1 điểm; cưỡng chế thì tịch thu kho rồi thiết bị, vẫn thiếu = đi tù
- `BankSystem`: vay 7 ngày (lãi 2%) hoặc 14 ngày (4,5%), hạn mức tăng theo chương và uy tín; quá hạn 3 ngày = cưỡng chế + 3 điểm vi phạm

## Tình huống (EventAction)
Trộm đột nhập, đổ nồi nước lẩu, vỡ bát đĩa, hỏng vòi nước, bếp hư (chọn sửa hay để hỏng), mưa/lũ/cúp điện/cúp nước, nhà cung cấp giao chậm,
bão giá, nhân viên nghỉ/xin tăng lương, chủ nhà tăng giá/đòi đặt cọc, hàng trôi nổi + kiểm tra vệ sinh ATTP, kiểm tra PCCC,
hóa đơn điện tử, lấn chiếm vỉa hè, lừa đảo giả danh cán bộ thuế/đặt tiệc, tín dụng đen, viện phí gia đình, đòi nợ cũ có kỳ hạn,
thanh tra thuế. Thêm tình huống: khai báo `Ev(...)`/`EvEnd(...)` trong `StoryDB.BuildChapters()`, xử lý trong `GameManager.ApplyEvent`.

## Thuế (mô phỏng quy định hộ kinh doanh từ 2026)
- Bỏ thuế khoán và lệ phí môn bài; hộ kinh doanh tự khai, tự nộp (NQ 198/2025/QH15).
- Ngưỡng doanh thu không chịu thuế GTGT/TNCN là 1 tỷ/năm (NĐ 68/2026, sửa bởi NĐ 141/2026), nhưng vẫn phải nộp tờ khai.
- Ăn uống: GTGT 3% doanh thu; TNCN 1,5% × (doanh thu − ngưỡng); từ chương 11 luôn coi là vượt ngưỡng; từ chương 21 tính 15% × (doanh thu − chi phí).
- Tiền chậm nộp 0,03%/ngày (Luật QLT 2019, Đ.59). Phạt chậm nộp hồ sơ 2,5–12,5 triệu (NĐ 125/2020, cá nhân = 1/2 mức tổ chức).
  Trốn thuế bị phạt 1–3 lần số thuế, kèm cưỡng chế, tịch thu tài sản nếu thiếu tiền và đình chỉ kinh doanh 1 ngày; tái phạm lần 3 bị truy cứu hình sự.

Giá món và nguyên liệu là giá tham khảo tổng hợp từ menu/đánh giá trên Foody, GrabFood và giá chợ. Đây không phải bảng giá chính thức của quán nào.
