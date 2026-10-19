# Adventure Update — Chương 1: Cuộc xâm nhập của Slime

Ngày thống nhất hướng: 2026-10-20 (Asia/Saigon).
Trạng thái (cập nhật 2026-10-20): **mốc 1 (inspect/thiết kế) và mốc 2 (lát cắt chơi được) đã làm**, có test tự động; chưa chơi thử thật. Mốc 3–5 chưa làm. Chương 1 hiện có đủ 5 màn nhưng đều là "dọn đợt" với quái sẵn có (Cầm cự chưa có sinh tồn, Cắt viện trợ chưa có quái hồi máu, chưa có thẻ nâng cấp và phần thưởng); chưa phải bản cập nhật hoàn chỉnh.

### Ánh xạ thiết kế → code (mốc 1–2)
| Khái niệm | Code |
| --- | --- |
| CampaignDefinition / ChapterDefinition / StageDefinition | `CampaignSpec` / `ChapterSpec` / `StageSpec` (`PetBattleData.cs`, battle.json `campaign`) |
| WaveDefinition | `WaveSpec` sẵn có; màn có `waves` riêng |
| CampaignController (luật mở màn, tính kết quả) | `Adventure` (`PetAdventure.cs`, không WPF) |
| Tiến trình lưu | `AdventureProgress` → adventure-progress.json (Version 1, .tmp + .bak, runId chống tính hai lần) |
| BattleSim | `BattleSim(..., stage)`: chỉ lấy đợt + maxAlive của màn; trận tự do không đổi |
| Map / Preparing / Fighting / Result | `PetWindow.Adventure.cs` (bản đồ), bảng chuẩn bị có `prepStage`, `battle.Stage`, bảng kết quả theo màn |
| Dữ liệu custom | chơi được, không lưu tiến trình/kỷ lục (ranked = false) |

## Mục tiêu bản cập nhật

Biến Battle Mode thành một hành trình hoàn chỉnh: chọn màn, chuẩn bị vũ khí, vượt thử thách, chọn nâng cấp trong màn, đánh boss, nhận phần thưởng và mở màn tiếp theo. Phát triển nội bộ theo các mốc, nhưng mục tiêu phát hành là một chương chơi được từ đầu đến cuối, không phải các tính năng rời rạc.

Luồng: Pet Mode → Phiêu lưu → bản đồ chương → chọn màn → chuẩn bị → chiến đấu/nâng cấp giữa đợt → kết quả/phần thưởng → màn tiếp hoặc bản đồ hoặc desktop.

Giữ Battle Mode/trận tự do đã có và dữ liệu cũ. Campaign gọi engine combat hiện tại; không xây lại WPF/render/input/physics hoặc chuyển combat sang Python. Pet Mode hoạt động độc lập, không phải đánh quái mới được chăm mèo.

## Phạm vi Chương 1

Một bản đồ 5 màn, một loại quái vai trò mới, một boss cuối chương có cơ chế rõ, 5–6 nâng cấp tạm thời và một nhóm phần thưởng ngoại hình nhỏ. Tái sử dụng kiếm/búa, skill, quái, HUD và bảng chuẩn bị/kết quả đã có.

| Màn | Mục tiêu thiết kế | Nội dung đề xuất |
| --- | --- | --- |
| 1. Làm quen | Học chọn mục tiêu và đánh | Dọn các đợt slime dễ, hướng dẫn theo tình huống, có thể bỏ qua |
| 2. Giải vây | Học kỹ năng/cứu mèo và chọn nâng cấp | Dọn quái hỗn hợp, cảnh báo rõ, không yêu cầu spam kéo |
| 3. Cầm cự | Biết lúc tấn công và lúc bảo toàn HP | Sống sót trong thời gian cấu hình; giới hạn quái và lịch spawn |
| 4. Cắt viện trợ | Chọn mục tiêu có ý nghĩa | Quái hỗ trợ hồi máu đồng đội; ưu tiên hạ nó trước |
| 5. Vua Slime | Kết hợp các kỹ năng đã học | Boss hiện có được phát triển thành trận cuối chương; đòn báo trước và gọi hỗ trợ theo pha |

Đây là cấu trúc nội dung để triển khai, không phải các con số balance đã được chơi thử. Chỉ hai loại objective bắt buộc trong chương: dọn đợt và sinh tồn. Bảo vệ vật thể để backlog, tránh thêm một hệ HP/AI mới cùng lúc. Không bắt buộc tạo một boss mới hoàn toàn nếu boss hiện tại đủ nền tảng.

## Nâng cấp giữa các đợt

- Dọn đợt, khi còn đợt tiếp: hiện tối đa 3 thẻ khác nhau hợp lệ, chọn 1 rồi xác nhận. Không hiện sau thua/thắng cuối hoặc end battle.
- Các hiệu ứng ban đầu: tăng damage, move speed, giảm attack cooldown, tăng knockback, hồi HP tức thời; thêm tối đa một hiệu ứng đơn giản nếu cần đủ lựa chọn.
- Chỉ có hiệu lực trong màn đang chơi; reset khi chơi lại/chuyển màn/end battle, không sửa definition dùng chung.
- Giới hạn cộng dồn, tránh lựa chọn vô tác dụng/không tương thích; fallback nếu còn ít hơn 3 thẻ. Không reroll/trả phí.
- Chọn thẻ đóng băng simulation và đồng hồ kỷ lục. Tiếp tục chỉ gỡ lý do chờ lựa chọn, không xóa pause app/user/fullscreen/menu.
- Bản đầu giữ auto-heal giữa wave hiện có trừ khi có quyết định cân bằng rõ; không âm thầm thay khi thêm thẻ hồi HP.

## Quái hỗ trợ và boss

- Một capability hồi máu đồng đội, với range/cooldown/cảnh báo rõ; không vượt max HP, không hồi quái chết, không tạo vòng hồi vô hạn không thể vượt.
- Dùng EnemyDefinition/instance/controller hiện có, không lớp riêng cho mỗi màu/size; dữ liệu chọn capability đã được C# triển khai.
- Boss có pha và trigger một lần theo ngưỡng, giới hạn summon, giữ pause/drag immunity và readability. Test tương tác với stun/knockback/combo.
- Chưa thêm quái bắn xa hoặc giáp vào cùng đợt. Hiệu ứng mới cùng art style và có giới hạn số lượng.

## Tiến trình và phần thưởng

- Lưu lâu dài: màn đã mở/hoàn thành, kết quả tốt nhất, hướng dẫn đã xem, phần thưởng đã nhận.
- Tạm thời: HP trong lượt, buff/nâng cấp, enemy/runtime state, cooldown. Bản đầu không lưu trận đang đánh; đóng ứng dụng thì lượt dang dở bỏ, mở lại ở bản đồ/checkpoint màn đã hoàn thành, không coi là thắng/thưởng.
- Thắng màn mở màn tiếp; thua/thoát không thu hồi unlock đã có. Chơi lại màn cũ được phép.
- Phần thưởng cosmetic (phụ kiện/ngoại hình) theo mốc hoàn thành; không tự thay đồ đang dùng và không khóa sinh hoạt Pet Mode. Không thêm chỉ số vĩnh viễn/currency/shop.
- Completion và reward phải idempotent: double click, reopen result hoặc AI restart không cộng hai lần. Lưu atomic, có schema version, fallback/backup phù hợp; không xóa kỷ lục battle hiện tại.
- Kỷ lục campaign tách stage ID + difficulty + weapon ID và phiên bản luật phù hợp; không so thành tích nâng cấp trong campaign với trận tự do cũ như cùng luật. Giữ legacy data.
- Không cho file custom tùy ý ghi đè thành tích official: chốt cách đánh dấu custom/non-ranked hoặc namespace riêng khi inspect hệ dữ liệu.

## Kiến trúc và lifecycle

CampaignDefinition → ChapterDefinition → StageDefinition → WaveDefinition. Tên khái niệm, bám conventions thật thay vì ép đổi tên toàn repo.

- Stage gồm ID, tên dịch, objective, wave/enemy references, upgrade policy, unlock/reward references. Validate ID trùng/tham chiếu thiếu/version sai/cycle unlock nếu có.
- CampaignController quản lý chọn màn, completion và tiến trình; BattleSim quản lý trận; C# sở hữu cả hai. Python chỉ nhận kết quả cấp cao nếu cần, không quyết định phần thưởng hay damage.
- Tách trạng thái Map/Preparing/Fighting/ChoosingUpgrade/Result và lý do pause khỏi animation/emotion mèo. Chỉ một trạng thái sở hữu hành vi ở một thời điểm.
- Dữ liệu cấu hình snapshot theo màn, không hot-reload giữa lượt. Preferences/loadout cũ fallback nếu ID không tồn tại. Tận dụng battle.json và docs/BATTLE-DATA.md hiện có, không duplicate registry.
- Mọi đường thoát đều dọn window/event/timer, trả Pet Mode hợp lệ, không ép Idle khi mèo đang cầm/bay. Giữ sleep lock, AI response invalidation, focus/click-through, DPI và monitor recovery.

## UI của một chương hoàn chỉnh

- Bản đồ trực quan 5 node, thể hiện locked/unlocked/completed, xem mục tiêu và phần thưởng trước khi vào.
- Tái sử dụng preview/thẻ vũ khí và độ khó ở chuẩn bị. Hướng dẫn hiện đúng lúc, có thể bỏ qua/xem lại; không làm timer chạy nền.
- Kết quả: thắng/thua, tiến trình, phần thưởng mới; Chơi lại / Màn tiếp (khi hợp lệ) / Bản đồ / Về desktop, bố cục rõ không dồn nhiều nút ngang hàng.
- Chọn nâng cấp dùng thẻ hình ảnh, tên và tác dụng ngắn. Không mất focus ngoài ý muốn, vi/en và DPI cao không cắt chữ.
- Giữ dữ liệu/âm thanh/hiệu ứng có nguồn rõ; không yêu cầu asset mới vượt phạm vi nếu vector hiện có đủ dùng.

## Các mốc triển khai nội bộ

1. **Inspect và thiết kế tích hợp:** đối chiếu AGENTS/PROGRESS/code, mapping file/state/data thật, chốt migration và objective. Giữ thay đổi chưa commit. Báo rõ phần đã có; không làm lại đợt trước.
2. **Một lát cắt chơi được:** map → màn 1 → chuẩn bị → combat → result → lưu completion → mở màn 2 → đóng/mở app vẫn đúng. Làm cả thất bại/hủy, không chỉ happy path.
3. **Nâng cấp và objectives:** thêm ChoosingUpgrade, modifier runtime và survival timer; test pause/reset/rules trước khi thêm nội dung còn lại.
4. **Hoàn thành nội dung chương:** quái hỗ trợ, boss theo pha, 5 màn và cosmetic reward; nối mọi đường điều hướng.
5. **Ổn định và nghiệm thu:** test tự động, playtest kiếm/búa × độ khó, kiểm tra DPI/focus/audio/máy sạch khi có điều kiện; tài liệu và version release.

Không coi từng mốc là đã hoàn thành bản cập nhật lớn. Có thể review theo mốc; không tự publish/commit/restart pet. Người dùng hiện yêu cầu gom roadmap, không phải chứng nhận đã triển khai hoặc cho phép phát hành.

## Tiêu chí hoàn thành Adventure Update

- Chơi từ màn 1 tới boss, nhận cosmetic, quay về desktop; đóng/mở app thấy đúng tiến trình và không thưởng trùng.
- Thắng, thua, hủy, chơi lại, đổi trang bị và chuyển màn có hành vi xác định; không rò window/timer/state, không giữ buff lượt trước.
- Test selection/stack/reset upgrades, objective thành công/thất bại, spawn cap, pause reasons chồng nhau, drag/throw/landing, save lỗi/migration và custom data.
- Regression Pet Mode ngủ/thức/cảm xúc/input/AI fallback vẫn đạt. Dùng dữ liệu thử riêng, không ghi đè preferences/progress người dùng.
- Playtest ghi version, weapon, difficulty, thời lượng, HP, thắng/thua và quyết định nâng cấp; không coi simulation là chơi desktop thật.
- Tài liệu schema/custom, hướng dẫn người dùng, MANUAL-TEST, PROGRESS và trạng thái roadmap cập nhật theo bằng chứng. Nếu chưa có phần cứng/khả năng test tay, ghi rõ còn thiếu, không đánh dấu toàn bộ release đã nghiệm thu.

## Backlog sau Chương 1

Chương/vùng tiếp theo, sổ quái đầy đủ, quái bắn xa/giáp, bảo vệ vật thể, nâng cấp khác, nhiều pet. Shop/chế tạo/căn cứ, inventory lớn, multiplayer, LLM/voice/CV không nằm trong bản này. Các đề xuất lẻ cũ chỉ lấy vào phạm vi chương khi được liệt kê ở trên.
