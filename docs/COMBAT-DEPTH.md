# Combat Depth — Build & Enemy Roles

Bàn giao 2026-10-20 (Asia/Saigon). Trạng thái: chưa triển khai; người dùng yêu cầu AI khác kiểm tra và lập kế hoạch từ code thật. Đọc AGENTS.md/PROGRESS.md trước. Giữ luồng chơi lại hiện có (người dùng xác nhận trơn tru); không xây lại Adventure hoặc engine C#/Python.

## Yêu cầu người dùng

- Màn 1–4 quá dễ; nâng cấp chưa tạo quyết định, cần combo giữa các thẻ. Ví dụ 3 thẻ Defense kích hoạt Giáp gai.
- Support chỉ đánh mèo khi hết đồng minh; giữ khoảng cách, hỗ trợ sau các slime khác.
- Slime nhanh gây stun khi va trúng một lần.
- Slime đá nhiều HP hơn, có xác suất miễn nhiễm sát thương.
- King đạt ngưỡng HP thì lui xa, triệu hồi trợ chiến và bất tử đến khi các slime trợ chiến chết hết.
- Mèo stun dùng biểu cảm/emoji choáng khoảng **1,5 giây**.

## Stun mèo — chi tiết bàn giao

Dùng biểu cảm choáng và sao xoay quanh đầu trong art style vector hiện có, rõ trên nền sáng/tối. `stunDurationSeconds = 1.5` cấu hình được.

Trong stun, dừng tự di chuyển/tấn công nhưng không khóa mouse capture/drag/throw/gravity/bounce. Kéo không tự xóa stun; khi hết thời gian chỉ giao quyền di chuyển lại cho combat nếu mèo tiếp đất và không còn trạng thái chặn khác. Pause đóng băng timer và hiệu ứng stun.

Tái sử dụng phần hình ảnh Dizzy nếu phù hợp, nhưng timer/trạng thái stun combat tách riêng Dizzy do lắc chuột. Giữ nguyên ngưỡng/hysteresis của pickup dizzy. Không áp stun lên mèo đang được cầm/bay; không cộng stun mỗi frame chồng va chạm. Đề xuất khoảng kháng stun ngắn sau hết stun để tránh khóa liên tục; thời lượng kháng cần cân bằng. End/replay/reset không để stun sót sang Pet Mode. Khi drag-dizzy và combat stun cùng tồn tại, chọn một presentation rõ ràng, không nhân đôi vòng sao hay làm mất thông tin timer.

## Thiết kế đề xuất cần kiểm chứng/chốt

### Build từ các nhóm thẻ

- Defense: giảm damage nhận/tăng max HP/hồi phục; mốc 3 điểm nhóm → Giáp gai phản một phần damage cận chiến thực nhận.
- Power: damage/knockback; mốc 3 → Phá giáp (đòn combo xuyên một phần phòng thủ).
- Speed: move/attack speed; mốc 3 → Dư ảnh (sau số đòn trúng cấu hình, thêm đòn phụ).
- Chỉ Defense 3 → Giáp gai là ví dụ người dùng nêu; tên/cơ chế Power/Speed và con số là đề xuất, không phải tính năng đã được xác nhận hay đã có.
- Mỗi thẻ giữ tác dụng riêng; HUD báo Defense 2/3, thẻ báo nếu lựa chọn kích hoạt synergy. Lặp thẻ có giới hạn có thể tăng điểm nhóm; cần chốt luật stack, cap và mốc mỗi synergy.
- Thẻ hồi HP tức thời đề xuất không cộng nhóm: đổi an toàn trước mắt lấy tiến độ build. Không đưa ra thẻ vô tác dụng khi full HP.
- Nếu reset build mỗi màn thì màn có build phải có đủ lượt chọn để đạt mốc 3. Kiểm tra wave/offer hiện có, ưu tiên ≥3 lần chọn ở màn 2–5 hoặc phương án tương đương được thống nhất; không đổi sang carry build qua màn âm thầm.
- Đảm bảo pool/offer tạo cơ hội hoàn thành build, không chỉ số học đạt 3 nhưng xác suất quá thấp. Không tự thêm reroll/shop.
- Damage pipeline phân biệt direct/reflect/secondary: Giáp gai không phản khi damage=0/bất tử; không reflect loop. Dư ảnh không tự kích hoạt chính nó vô hạn. Chốt Phá giáp tương tác với random block của đá; không xuyên khiên pha King.
- Modifier chỉ trong lượt, không sửa definition chung; snapshot/loadout, pause, chọn thẻ và reset/replay giữ nguyên luật.

### Support

Có đồng minh hợp lệ thì giữ sau tuyến trước, chọn đồng minh cần hồi trong range/cooldown/budget, tránh áp sát mèo. Không kite vô hạn: retreat có giới hạn và cooldown, xử lý mép màn hình rõ. Khi chỉ còn support, cần luật chuyển sang chiến đấu tránh hai support chờ nhau mãi; đề xuất tính đồng minh có khả năng giao chiến làm điều kiện hỗ trợ. Giữ các giới hạn hồi máu hiện có, không hồi quái chết/quá max.

### Slime nhanh

Đề xuất windup có cảnh báo → dash → recover. Mỗi dash chỉ gây hit/stun một lần khi thật sự va mèo; không gây damage/stun mỗi frame overlap. Cho né hoặc ngắt dash bằng kỹ năng; xác định interaction stun/knockback/shield/grace. Không tự stun mèo đang bất tử do drag/flight. Kiểm tra collision tốc độ cao để không xuyên qua mèo giữa hai tick.

### Slime đá

Tăng HP trong dữ liệu, random block cấu hình được. Đề xuất giới hạn chuỗi block liên tiếp/hồi chiêu để không gây cảm giác quái bất tử ngẫu nhiên; hiển thị BLOCK và hiệu ứng giáp rõ. Chốt loại damage nào bị block, cách Power phá giáp đối phó. RNG injectable/seed trong test, không roll mỗi frame, không đồng nhất random block với invulnerability của King.

### King

Ngưỡng HP → retreat có animation trong work area → summon → shield/invulnerable → hết viện binh → quay lại đánh. Chốt thời điểm bật khiên và quy tắc xử lý damage trong transition, không teleport hoặc âm thầm hồi HP.

Theo dõi ID nhóm viện binh của chính pha đó. Không để support/creep không liên quan giữ khiên mãi. Nếu spawn thiếu vì cap, phải có cách hoàn tất danh sách hoặc kết thúc pha an toàn; không deadlock. Mỗi ngưỡng kích hoạt một lần; đòn mạnh vượt nhiều ngưỡng phải có policy queue/ưu tiên, không summon chồng pha. Khi khiên bật, mèo tự ưu tiên mục tiêu có thể bị đánh; marker/cảnh báo giải thích boss bất tử. Khi viện binh cuối chết, khiên tắt đúng một lần. Kiểm tra survival/end/pause và mất monitor giữa pha.

## Cân bằng màn

Màn 1 dễ học nhưng cần thao tác; màn 2 giới thiệu dash/stun; màn 3 có đá tuyến trước; màn 4 support sau đội hình; màn 5 tổng hợp với King. Đây là hướng đội hình, phải đối chiếu objective hiện tại, không bỏ survival stage hoặc đổi unlock/reward tùy tiện. Làm capability trước, sau đó cân bằng, không tăng mạnh HP/damage/speed mọi quái cùng lúc.

Mô phỏng kiếm/búa × độ khó × build × policy hỗ trợ nhiều seed. Đo win rate, thời gian, HP, số stun/block, uptime shield boss và tỉ lệ kích hoạt synergy. Bổ sung playtest thật vì người dùng đã thấy 1–4 dễ dù test trước đạt; không giữ nguyên tiêu chí cũ nếu chúng khuyến khích trận quá dễ. Chốt mục tiêu mới với người dùng, không tuyên bố balance từ mô phỏng là đủ.

## Mốc triển khai đề xuất

1. Inspect và chốt schema/damage pipeline, số lần chọn thẻ, luật tương tác bất tử/stun/block/reflect và boss phases.
2. Nhóm thẻ/synergy + test reset/proc chain; minh họa UI tiến độ build.
3. Vai trò support/dash/đá/King + unit và WPF tests; làm từng capability nhưng tích hợp chung.
4. Đội hình, cân bằng, playtest và tài liệu. Giữ UI chơi lại ổn định.

Không tự coi tất cả thiết kế gợi ý là đã duyệt. Người dùng hiện yêu cầu bàn giao để AI kiểm tra; giải thích kế hoạch theo AGENTS.md trước khi code. Khi có mốc code hoàn chỉnh: test → commit → publish → smoke theo quy tắc người dùng, không hỏi lại quyền các bước đó. Không push/upload.

## Kiểm thử bắt buộc nếu triển khai

- Stun đúng 1,5s theo simulation, pause đóng băng, một dash một stun, immunity window, kéo/thả vẫn hoạt động, hết stun giữa flight không ép grounded; Dizzy pickup không hồi quy.
- Synergy đạt ngưỡng một lần, stack đúng, reset lượt, không reflect/proc recursion, thứ tự damage/armor/block/shield nhất quán.
- Support chọn mục tiêu và retreat biên, hai support sống cuối; đá RNG deterministic/giới hạn block.
- King nhiều ngưỡng một hit, spawn cap, viện binh chết bởi bom/reflect, last add chết, không còn mục tiêu đánh được, pause/end giữa pha; không bất tử vĩnh viễn.
- Regression toàn bộ Adventure/Pet Mode/save/records/upgrade UI. Khi đổi luật ảnh hưởng kỷ lục, tăng rulesVersion phù hợp, giữ legacy/progress/reward cũ.
- Cập nhật BATTLE-DATA.md, MANUAL-TEST.md, PROGRESS.md theo kết quả thật; test dùng dữ liệu riêng. Ghi rõ chưa kiểm chứng DPI/desktop/âm thanh nếu chưa làm.
