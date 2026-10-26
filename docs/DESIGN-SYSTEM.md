# Design System — Cozy Desktop Adventure

Chuẩn thiết kế project, được người dùng duyệt ngày 2026-10-24. Chủ đề: **Một thế giới nhỏ trên bàn làm việc**.

> Mèo là bạn đồng hành sống trên desktop; cửa sổ, đồ vật và những sự cố máy tính trở thành chất liệu cho cuộc phiêu lưu của nó.

## Trạng thái và nguồn chuẩn

Đây là chuẩn cho thiết kế hiện tại và tính năng tương lai, không xác nhận mọi màn hình đã được rà trực quan. Chuẩn được áp dụng dần khi sửa đúng thành phần; không yêu cầu viết lại UI. Lịch sử từng đợt thay đổi ghi ở PROGRESS.md, không ghi ở đây.

- Token màu/font/cỡ chữ/bo góc thực thi: [PetUi.cs](../src/DesktopPet/PetUi.cs) (bảng màu sáng + `PetUi.Game` cho bảng game tối).
- Control: [PetControlStyle.cs](../src/DesktopPet/PetControlStyle.cs); menu: [PetMenuStyle.cs](../src/DesktopPet/PetMenuStyle.cs), [PetTrayMenuStyle.cs](../src/DesktopPet/PetTrayMenuStyle.cs).
- Minh họa dùng chung: [PetIllustration.cs](../src/DesktopPet/PetIllustration.cs) (motif nhỏ), [PetScene.cs](../src/DesktopPet/PetScene.cs) (cảnh banner).
- Quy tắc hành vi/ngoại lệ mới nhất: [AGENTS.md](../AGENTS.md). Theme không thay sleep lock, input, combat, pause, physics hoặc quy tắc đã duyệt.
- Khi thêm token, cập nhật code và tài liệu trong cùng mốc. Không copy bảng màu riêng cho mỗi cửa sổ, không dùng màu có sẵn của WPF (`Brushes.Coral`, `Brushes.Red`…) cho UI.

## Ba đặc tính

**Ấm áp:** chữ rõ, màu dịu, mèo đồng hành không gây áp lực phải chăm liên tục.
**Tinh nghịch:** hình tròn, đồ vật nảy, biểu cảm và sự cố desktop hài hước có nhịp nghỉ.
**Phiêu lưu:** trang bị, khám phá, chiến đấu, mang kỷ vật về nơi sống.

Cozy là cách thể hiện, không bắt combat dễ. Cảnh báo phải dễ đọc dù boss/elite mạnh. Không biến app thành giao diện neon/cyberpunk hoặc fantasy nặng tách khỏi mèo.

## Palette

App có hai nền: **kem** (cài đặt, menu, trạng thái, nhật ký, hộp thoại) và **game tối** (sảnh, bản đồ, chuẩn bị, thẻ, túi đồ, kết quả, HUD). Chọn màu chữ theo nền đang đứng, không mang màu của nền này sang nền kia.

### Nền kem (PetUi)

| Token | Màu | Vai trò | Tương phản đo được |
| --- | --- | --- | --- |
| Surface | #FFF8F1 | Nền bảng | |
| CardBack | #FFFFFF | Thẻ nội dung | |
| Stage | #FBEFE3 | Nền preview mèo | |
| Ink | #4A3628 | Chữ chính, nét nâu của art | |
| Muted | #7D6656 | Chữ phụ | 5.1 trên Surface, 4.75 trên Stage |
| Accent | #C2561F | Nền nút chính, công tắc bật, icon | chữ trắng trên Accent 4.52 |
| AccentText (= AccentHoverHex) | #A84816 | Chữ màu nhấn: link, số nổi bật | 5.8 trên trắng/kem |
| AccentBright | #E8834F | Trang trí, biểu đồ, hover viền; **không dùng cho chữ** | 2.6 |
| AccentSoft / AccentSofterHex | #FCE9DB / #F8D9C4 | Nền nhấn nhẹ, nền icon menu, tab chọn | |
| ControlEdgeHex | #A38670 | Viền ô nhập, combo box, rãnh công tắc tắt | 3.4 trên trắng, 3.2 trên kem |
| Line / ControlLineHex | #EEDFD0 / #E2CDB9 | Phân cách, thanh cuộn, trang trí; **không làm viền duy nhất của control** | 1.5 |
| Care | #327354 | Hồi phục, nghỉ, trạng thái, cảm xúc | ≥ 4.5 trên kem |
| Mystery | #7953A1 | Nhật ký, bí ẩn, nhiễm độc | ≥ 4.5 |
| Danger | #B94444 | Nguy hiểm, thoát, laser | ≥ 4.5 |
| Reward | #976412 | Phần thưởng, elite, chiến đấu, cho ăn | ≥ 4.5 |

Accent làm chữ trên kem chỉ đạt 4.3:1, nên chữ dùng AccentText. Care/Mystery/Danger/Reward là màu mực cho nền sáng; không dùng chúng làm chữ trên nền game tối. UiChecks (VisualPolishChecks) khóa các tỷ lệ semantic, AccentText và ControlEdge.

### Nền game tối (PetUi.Game)

| Token | Vai trò |
| --- | --- |
| Panel (#E8302520), HudTop | Nền bảng game và HUD (nâu ấm gần đen, không đen tuyền) |
| Rim, HudRim | Viền kem quanh khung game |
| Soft (#F1E4D6) | Chữ phụ trên nền tối (≈ 9:1) |
| Gold (#FFD35C), RareGold, Warning (#FFC86A) | Nhấn vàng, độ hiếm, cảnh báo nhẹ; GoldInk là chữ đặt trên nền vàng |
| Chosen (= Accent), Armed, Progress (= AccentBright) | Chip/tab đang chọn, kỹ năng sẵn sàng, thanh tiến độ, nút chính |
| ChosenCard (#73C2561F) + ChosenRim (= AccentBright) | Thẻ lớn đang chọn (vũ khí, nâng cấp, lựa chọn Viễn chinh): cam trong suốt + viền cam + ✓, không tô kín |
| CardBack, CardRim, RareBack, ButtonBack, Stage, BarBack, Locked, Ahead | Lớp kem trong suốt chồng lên nền tối |
| Shadow(alpha) | Hố bóng nâu ấm |

Chữ chính trên nền tối là trắng; chữ phụ dùng Soft; số liệu/nhấn dùng Gold. Màu riêng của art trận (AppArt, slime, hiệu ứng) giữ trong file art tương ứng.

### Màu icon menu

Icon menu có nền AccentSoft; màu lấy qua `PetUi.IconColor(glyph)` theo nhóm: chăm sóc/cảm xúc = Care, nhật ký = Mystery, chiến đấu/cho ăn = Reward, thoát = Danger, còn lại = Accent. Chỉ truyền màu riêng khi cần nghĩa khác nhóm (ví dụ laser = Danger, yên lặng = Mystery), và luôn là token. Mục bật/tắt có icon (Yên lặng = trăng, Âm thanh = loa): khi bật, nền icon đậm hơn và dấu ✓ hiện ở cuối dòng; mục bật/tắt không icon giữ dấu ✓ ở cột icon.

### Màu của art

Artwork (mèo, PetRenderer, PetScene, PetIllustration, kỷ vật, quái) có palette riêng: `PetUi.Rose` #F3A1A0, `PetUi.Mint` #9BD2AE, `PetUi.Lilac` #C5ADE5 (chỉ tô hình, không làm chữ), gỗ #B4815E/#6D4E40, Game.Gold.

Nét viền: cảnh/icon trên nền kem dùng Ink #4A3628; art vẽ trên desktop và trong bảng game (chiến trường, slime, icon thẻ/túi đồ/chuẩn bị, app giả, kỷ vật, bong bóng thoại, hiệu ứng cảm xúc) dùng `PetUi.ArtInk` #3A2C1F — nâu gần đen, đậm hơn Ink để giữ viền trên mọi hình nền. Bóng/viền chữ/màn che bán trong suốt dùng `ShadeColor` #2A1F16 với alpha. Không dùng mực chàm/tím/xanh rêu cho viền.

Màu tín hiệu trận giữ độ rực để đọc trên mọi hình nền: cảnh báo đỏ #E53E3E, hồi máu xanh lá, Đẩy xanh dương, bom len hồng, vàng combo/khiên. Ba nhóm thẻ: thủ #7CC0EE, công #F49272, tốc #9AD884 (thanh, viền, huy hiệu túi đồ); vòng icon thẻ nâng cấp dùng Mint/AccentBright/Lilac. Hiệu ứng cảm xúc trong PetRenderer (vàng, đỏ cà chua, hồng, xanh giọt nước) là màu art có viền, không phải token UI.

### Không chỉ dựa vào màu

Không mã hóa trạng thái chỉ bằng màu: stun có sao, Push bị chặn có biểu tượng/nhãn, elite có huy hiệu, lỗi có mô tả. Chữ nhỏ ≥ 4.5:1 trên nền thật; viền/focus/icon quan trọng ≥ 3:1. Overlay trên wallpaper dùng nền/viền hai tông, không chỉ shadow mờ.

## Typography, khoảng cách và hình khối

Giữ font hiện có: Segoe UI, Yu Gothic UI; icon Segoe Fluent Icons, Segoe MDL2 Assets. Kiểm tra đủ dấu tiếng Việt và tiếng Nhật, không dùng emoji hệ điều hành làm art chính của quái hoặc trạng thái chiến đấu.

Cỡ (DIP): Caption 11, Small 12, Body 13, Title 14, Heading 16, Display 20. Nội dung quan trọng dùng Body trở lên; caption chỉ cho thông tin phụ, không ép chữ nhỏ để vừa panel. Code mới dùng hằng PetUi, không viết số cỡ chữ trực tiếp.

Bo góc control/thẻ: Small 6, Card 12, Pill 16, `Game.ButtonRadius` 12. Art vẽ tay (icon, huy hiệu, thanh nhỏ) được dùng bán kính riêng theo hình. Khoảng cách: dùng bội của 4 (4/8/12/16/24 DIP) — quy ước, chưa có hằng số trong code. Co giãn/wrap/scroll theo nội dung thay vì cắt nhãn. Giữ focus bàn phím và trạng thái disabled rõ.

## Mẫu thành phần

| Thành phần | Chuẩn |
| --- | --- |
| Nút | `PrimaryButton` (Accent) cho một hành động chính mỗi vùng quyết định; `SecondaryButton`/`LinkButton` cho phụ; có hover/pressed/focus/disabled; nhãn là động từ rõ |
| Ô nhập/combo/công tắc | Style chung trong PetControlStyle; viền ControlEdge, hover AccentBright, focus Accent/Ink |
| Thẻ trang bị/nâng cấp | Hình đại diện, tên, tác dụng chính và đánh đổi; chọn có viền + dấu chọn, không chỉ màu; chỉ số là chi tiết phụ |
| HUD | HP, trạng thái và kỹ năng dễ đọc nhanh; ít văn bản lặp; giá trị cập nhật không làm panel nhảy kích thước; không thêm hình trang trí vào từng dòng |
| Tooltip | Giải thích tác dụng, điều kiện và hạn chế; không che cảnh báo hoặc nút cần bấm |
| Thông báo | Nhẹ cho thông tin thường; rõ cho lỗi/nguy hiểm; không spam banner/cue; lỗi thật không diễn đạt thành thành công |
| Bảng game | `CreateGamePanel` (bản đồ, prep, kết quả): nội dung cuộn, nút thao tác cố định ngoài scroll; kiểm tra màn hình thấp 480 DIP |
| Kết quả | Kết quả trận và phần thưởng trước, thống kê sau; tiếp tục/chơi lại/đổi trang bị/về desktop dễ tìm theo flow đang có |
| Cài đặt/menu | Dùng style chung, văn bản thực dụng, không đổi tên chức năng thành ẩn dụ khó hiểu |

Không tự thêm confirm, màn trung gian hoặc đổi navigation chỉ để hợp theme.

## Art và minh họa

- Giữ vector/procedural WPF: silhouette tròn rõ ở kích thước thật, nét nâu đậm, bóng nhẹ, chi tiết vừa đủ. Không đổi sang sprite hoặc engine khác.
- Mèo là tâm điểm. Trang bị và quái dùng cùng nét, độ đơn giản và chất liệu gỗ/vải/giấy/kim loại hoạt hình.
- **PetIllustration**: 28 motif nhỏ trên ô 64, nét Ink, màu Rose/Mint/Lilac/Gold/gỗ, nền đĩa AccentSoft (`Backdrop = false` khi hình nằm trên nút tròn có sẵn, ví dụ nút bản đồ Viễn chinh; `Motif` đổi tại chỗ). Gán theo chức năng, mỗi chỗ một nghĩa:
  - Thẻ mục Cài đặt/Tùy chỉnh (26 DIP cạnh tiêu đề): Hồ sơ = Cat, Ngoại hình = Palette, Thói quen = Clock, Kỷ vật = Gem, Bong bóng chữ = Bubble, Trí nhớ = Book, Khi nào phát = Note, Âm lượng = Speaker, Ứng dụng = Gear, Cập nhật = Gift, Nghỉ giải lao = Cup.
  - Phần thưởng ở sảnh: Phụ kiện Phiêu lưu = Crown, Kỷ vật Viễn chinh = Gem, Vô tận = Moon, Kỷ lục = Trophy.
  - Bản đồ Phiêu lưu (mốc dưới màn): màn đầu Flag, màn cuối (trùm) Crown, giữa lần lượt Sword/Shield/Mushroom. Bản đồ Viễn chinh (trong nút): giao tranh Sword, tinh anh Spark, trùm Crown, nghỉ Campfire, sự kiện Scroll, chưa lộ Question; dòng tỉ lệ quay vẫn dùng ký tự.
  - Trống: Box (túi đồ chưa có thẻ = Cat + Box; chưa có di vật; chưa có phần thưởng). Hộp thoại: thông tin = Cat, câu hỏi = Question, cảnh báo = Warning.
  - Trạng thái, nhật ký (Heart/Fish/Ball/Spark/Clock/Book), welcome, kết quả giữ như trước.
- Menu khay (WinForms) dùng cùng huy hiệu glyph của menu chuột phải, vẽ thành ảnh 48 px (`PetTrayMenuStyle.Icon`), hiện 20 DIP theo DPI của menu; mục bật/tắt (Tạm dừng, AI) chỉ có dấu ✓.
- **PetScene**: 12 cảnh banner 76–104 DIP trong vùng nội dung cuộn, chọn theo chức năng; mỗi cảnh có nền và đồ vật riêng, không lặp một bố cục: Home (phòng ban ngày, màn hình + cuộn len), Adventure (đồi xanh, slime, rương), Expedition (bản đồ + la bàn + ba lô, đêm), Infinity (cổng vô tận + slime, đêm), Workshop (bảng treo kiếm/búa/khiên, bàn thợ, đèn treo), Diary (giá sách, bàn có sổ mở, cửa sổ hoàng hôn), Speech (bãi cỏ, cây, bong bóng thoại), Sound (sân khấu rèm, đèn rọi, mèo đeo tai nghe), Settings (bảng trượt, bánh răng, đồng hồ), Treasure (rương mở, xu, cúp, đá quý), Victory (bục ba bậc, cúp, cờ, giấy màu), Rest (đêm, trăng khuyết, mèo cuộn tròn ngủ trên đệm, ZZZ, đèn ngủ). Giữ bố cục chính ở giữa vì thẻ mode crop cảnh: Adventure đặt slime/mèo/rương trong x 152–328 của khung 480.
- Scene/illustration là trang trí: không text, không nút, không nhận input, không timer; DrawingGroup frozen, cache theo loại.
- Mỗi bảng tối đa một banner; không đặt banner ngay cạnh preview/chân dung mèo (Tùy chỉnh, Túi đồ, trang Phần thưởng của sảnh không có banner riêng). Bảng game (CreateGamePanel) co banner theo màn hình của mèo: 104 DIP; màn hình còn < 700 DIP thì 56 DIP; < 540 DIP thì ẩn (PlaceGamePanel).
- Tiêu đề bảng game căn trái, không huy hiệu mode (banner đã nói mode).
- Mèo trong PetScene mặc coat và phụ kiện của pet (`PetScene.Coat`, `PetScene.Accessory`; PetWindow đặt khi tải, khi Lưu cài đặt và khi đổi đồ ngày lễ): lông, mõm, tai/điểm màu, bàn chân, mắt màu, hoa văn tam thể/mướp lấy từ CoatPalette; phụ kiện vẽ bằng chính dữ liệu accessories.json của PetRenderer, đổi tỷ lệ 0,75 vào đầu mèo nhỏ (cả mèo ngồi và mèo cuộn ngủ). Cache theo (cảnh, coat, phụ kiện).
- Vùng đã có art riêng tiếp tục dùng art đó: ModeArt, WeaponIcon, UpgradeIcon (64 DIP trên huy hiệu 86), SynergyIcon, BagIcon, KeepsakeArt, AppArt.
- Vai trò slime nhận ra từ hình dáng/phụ kiện, không chỉ recolor. Elite thêm một dấu hiệu rõ, không phủ hiệu ứng che windup.
- App giả là đồ chơi hình cửa sổ/icon máy tính; giữ quy tắc không dùng app/logo thật, không đọc nội dung desktop. Phân biệt cảnh báo game với lỗi Windows thật.
- ZZZ, sao stun, số damage và dấu miễn có viền/nền tương phản. Kiểm tra thứ tự lớp giữa cửa sổ mèo/chiến trường; không để thân mèo che chữ như các lỗi đã ghi ở Issue_fixxed.md.
- Art không tự đổi hitbox, input region hoặc physics. Giữ vùng trong suốt xuyên chuột và vùng tương tác đã được duyệt cho từng vật thể.

## Motion và âm thanh

Sinh hoạt: nhẹ, có anticipation và nhịp nghỉ. Tương tác: nảy/lắc có trọng lượng và giảm dần. Combat: chuẩn bị dễ đọc → impact rõ → recovery. UI: gọn, không làm cả bảng nảy liên tục; không đổi timing chiến đấu theo timing UI.

Giữ cue hoạt hình hiện có: meow cách điệu, pop/boing, gỗ/kim loại nhẹ; elite/boss có lực hơn nhưng cùng chất liệu. Không beep hệ thống hay mèo thật. Giữ volume/mute/preview/cooldown và giới hạn chồng âm hiện có. Hiệu ứng trang trí giảm được nếu gây rối; không loại bỏ cảnh báo cần thiết. Không thêm timer vĩnh viễn hay allocation mỗi frame chỉ cho trang trí; reuse/freeze tài nguyên theo hệ hiện có.

## Một thế giới, nhiều chế độ

| Khu vực | Cách thể hiện | Cảnh/nền |
| --- | --- | --- |
| Pet Mode | Cuộc sống thường ngày trên bàn làm việc | Nền kem, PetScene Home |
| Sảnh | Góc chuẩn bị của nhà thám hiểm nhỏ | Bảng game tối, banner Home |
| Adventure | Các câu chuyện trong thế giới desktop | Đồi xanh ban ngày (Adventure) |
| Viễn chinh | Chọn đường, khám phá, thu thập đồ lạ | Bản đồ cuộn ban đêm (Expedition) |
| Vô tận | Giữ desktop trước những đợt hỗn loạn | Cổng năng lượng tím (Infinity) |
| Kỷ vật/nhà mèo tương lai | Dấu vết chuyến đi và nơi sinh hoạt | KeepsakeArt |

Vùng tương lai có thể là Khu vườn cửa sổ, Kho lưu trữ thất lạc, Xưởng đồ chơi. Đây là ví dụ chủ đề, chưa là roadmap được giao. Không bắt người chỉ dùng Pet Mode phải chiến đấu, không đổi tên mode hoặc thêm gameplay trong một mốc theme.

## Checklist cho người/AI phát triển

Luôn làm:
1. Đọc chuẩn này và AGENTS; tìm token/thành phần có sẵn trước khi tạo style mới. Chọn màu theo nền (kem hay game tối).
2. Giải thích phạm vi UI cần đổi; tách visual khỏi gameplay, giữ thay đổi chưa commit của người khác.
3. Kiểm tra normal/hover/pressed/focus/disabled/selected, nhãn vi/en/ja dài, overflow và keyboard.
4. Xem ảnh render (`PET_RENDER_REVIEW=<thư mục>` khi chạy UiChecks) trước/sau; build/test và publish theo AGENTS. Tài liệu-only không publish binary.

Khi đụng overlay, cửa sổ trận hoặc animation:
5. Thử nền sáng/tối/nhiều màu, kích thước thật, DPI 100/125/150/200%, nhiều màn hình và z-order. Không báo đã kiểm tra nếu chưa làm.
6. Kiểm tra pause/fullscreen/drag/sleep; không tự sửa các ngoại lệ đã duyệt theo ý theme.

Ảnh render khác với nghiệm thu desktop/DPI thật; ghi rõ cái nào đã làm.

## Việc tiếp theo để chuẩn hóa dần

- Ghi chú bản đồ Viễn chinh và dòng cộng hưởng HUD giữ chữ theo ngôn ngữ lúc tạo; đổi ngôn ngữ giữa chuyến/trận thì còn chữ cũ tới khi nội dung đổi.
- Màu riêng còn lại trong PetBattleView (tín hiệu, kỹ năng), PetBattleAppArt (app giả) và PetRenderer (hiệu ứng cảm xúc) là màu art có chủ đích; chỉ gom khi sửa đúng phần đó.
- Kiểm kê màu hardcode còn lại (Inventory, BattleUpgrade, GamePanel, BattleHud) để gom vào PetUi.Game hoặc file art tương ứng.
- Thêm hằng khoảng cách nếu nhiều màn mới cần cùng nhịp.
- Chưa có cuộc rà trực quan toàn app trên desktop/DPI thật.
