# Bàn giao review hiện tại và kế hoạch đợt tiếp theo

Cập nhật yêu cầu: 2026-10-19 (Asia/Saigon). Review kỹ thuật bên dưới ghi nhận ngày 18/10, sau Battle Mode đợt 11.
Workspace lúc review: C:\Project VS\Pet Computer; HEAD e45519d với thay đổi đợt 10–11 chưa commit.

## Dành cho AI tiếp nhận

Người dùng yêu cầu lưu review và kế hoạch ở đây để AI khác đọc và tiếp tục công việc. Đọc AGENTS.md, PROGRESS.md và code thật trước khi triển khai; không coi các mục đã sửa bên dưới là lỗi còn mở. Giữ nguyên thay đổi chưa commit của người dùng/Claude.

Lần bàn giao này chỉ cập nhật tài liệu. Yêu cầu triển khai hiện tại nằm ở mục ngày 2026-10-19 bên dưới và ưu tiên hơn backlog review cũ. Đọc AGENTS.md và giải thích kế hoạch trước khi triển khai; giữ nguyên source đang có và không tự publish/commit/restart.

## Ưu tiên hiện tại — yêu cầu người dùng ngày 2026-10-19

> Trạng thái (đợt 14, 2026-10-19, commit 8a29302, đã publish): đã triển khai A, B, C. Bảng chuẩn bị ở PetWindow.BattlePrep.cs. Dữ liệu tùy chỉnh ở %LOCALAPPDATA%\DesktopPet\battle.json (schema và ví dụ: [BATTLE-DATA.md](BATTLE-DATA.md), [examples/battle.json](examples/battle.json)). Test tự động: 55 Python + 614 C# đạt; WPF 379 đạt, 6 check cũ thất bại do chuột thật ở màn hình phụ (bản HEAD cũng thất bại y hệt). Chưa chơi thử trên desktop thật. Chi tiết trong PROGRESS.md.

Người dùng yêu cầu bàn giao để Claude triển khai luồng vào trận và khung dữ liệu mở rộng dưới đây. Phần này thay thế thứ tự ưu tiên cũ của review 18/10; các mục review phía dưới giữ làm backlog/thông tin lịch sử, không phải điều kiện phải hoàn tất trước luồng mới. Đọc code và giải thích kế hoạch trước khi sửa, giữ phạm vi đã yêu cầu. Không tự publish, commit, đóng/restart pet hoặc ghi đè thay đổi chưa commit.

### A. Luồng vào trận

Chuột phải → Battle Mode → Bắt đầu trận → Bảng chuẩn bị → Sẵn sàng → Chiến đấu → Kết quả.

- Không yêu cầu chọn vũ khí hoặc độ khó trong menu chuột phải trước khi bấm Bắt đầu trận.
- Bắt đầu trận mở phiên chuẩn bị, chưa spawn quái, chưa gây sát thương/chạy timer trận hoặc tính kỷ lục.
- Bảng chuẩn bị nhỏ chứa chọn vũ khí, chọn độ khó, hướng dẫn có thể bỏ qua/xem lại, nút Sẵn sàng và Hủy/Đóng. Nhớ lựa chọn hợp lệ trước đó để lần sau vào nhanh.
- Sẵn sàng mới bắt đầu simulation; kiểm tra lại điều kiện an toàn tại thời điểm bấm, không chỉ khi mở bảng. Chống double-click tạo hai trận/cửa sổ.
- Trong Preparing, quy định rõ quyền sở hữu hành vi: không để mèo tự ngủ/đi chơi hoặc Python ghi đè chuẩn bị. Không cưỡng ép cắt ngủ/drag/flight khi mở; nếu người dùng kéo mèo hoặc fullscreen/pause xuất hiện, chặn Sẵn sàng đến khi an toàn.
- Hủy/đóng chuẩn bị không tính thắng/thua, không cấp thưởng và trả Pet Mode đúng trạng thái; không để timer/window/event handler sót lại.
- Chơi lại từ bảng kết quả quay về chuẩn bị với lựa chọn cũ. Trong trận HUD chỉ tập trung HP/kỹ năng/pause/end; không đổi vũ khí giữa trận trong đợt này.
- Hướng dẫn phải nằm trong trải nghiệm Battle Mode, không thêm tầng menu chuột phải. Tôn trọng localization, DPI, focus và click-through. Chuẩn bị không nhận phím ngoài ý muốn từ app khác.

### B. Chuẩn hóa nội dung theo mẫu và instance

Mục tiêu: slime nhỏ/slime đá/slime vua đều là quái chung. Thêm biến thể cùng khả năng bằng dữ liệu, không viết lớp riêng cho từng biến thể. Phát triển từ battle.json, EnemyType, Slime/BattleSim và hệ render hiện có; tên dưới đây là trách nhiệm khái niệm, không ép đổi tên toàn bộ project.

- EnemyDefinition: ID, family, tên địa phương hóa, ngoại hình, stats, movement, attack, resistances, abilities.
- EnemyInstance: tham chiếu mẫu và trạng thái riêng mỗi con (HP, vị trí, cooldown, target, state). Không sửa mẫu dùng chung trong trận.
- EnemyController: thực thi khả năng được hỗ trợ; EnemyRenderer: vẽ theo renderer/style đã đăng ký. Không đưa UI/physics sang Python.
- WeaponDefinition: ID/tên, damage, range, cooldown/windup/hit timing, animation và hit effect; trạng thái swing/cooldown nằm ở runtime riêng.
- WaveDefinition: nhóm enemy ID, số lượng, lịch spawn/thời gian nghỉ.
- BattleRules: giới hạn quái, độ khó, cứu nguy và điều kiện kết thúc. SkillDefinition có thể theo cùng nguyên tắc nhưng không refactor toàn bộ kỹ năng nếu chưa cần.

Ví dụ khái niệm (không buộc thay nguyên schema đang có): stone_slime có family=slime, renderer=slime, scale=1.35, HP=120, moveSpeed=22, attack=melee, knockbackMultiplier=0.35 và stunDurationMultiplier=0.5. Slime nhỏ thay size/HP/speed; boss dùng khả năng summon/slam đã hỗ trợ. Các con số chỉ minh họa, không tự thay cân bằng hiện tại.

Quy tắc dữ liệu:
- schemaVersion, ID ổn định, validation ID trùng/tham chiếu thiếu/kiểu không hỗ trợ/NaN và giới hạn số liệu; lỗi có thông tin để sửa.
- Tách kích thước vẽ khỏi hitbox và attack range; mặc định tương thích để không âm thầm đổi combat cũ.
- Khả năng mới như bay/bắn đạn vẫn cần code controller/renderer; JSON chỉ chọn khả năng đã hỗ trợ, không chạy code tùy ý.
- Snapshot cấu hình khi bắt đầu trận; không đổi số liệu giữa trận khi người dùng sửa file.
- Cấu hình lỗi fallback an toàn, không crash startup hoặc để chạy dữ liệu nửa hợp lệ. Hỗ trợ dữ liệu/preferences cũ và ID lựa chọn cũ không còn tồn tại.
- Xác định rõ nơi custom: tài liệu schema và ví dụ. Ưu tiên bản built-in làm mặc định + file override JSON ngoài ở thư mục dữ liệu ứng dụng cho người dùng chỉnh, được validate khi tải; không đòi sửa/rebuild exe chỉ để đổi một biến thể. Đường dẫn cụ thể thống nhất với conventions của project và ghi trong docs.
- Không cần framework plugin hoặc editor đồ họa trong đợt này.

### C. Phạm vi triển khai và tiêu chí nghiệm thu

1. Inspect hệ vũ khí/quái hiện tại; giữ mọi nội dung đang có. Nếu chỉ có một vũ khí, vẫn tạo selection từ registry dữ liệu, không bịa thêm cả hệ trang bị. Thêm vũ khí thứ hai là đợt sau nếu người dùng yêu cầu.
2. Triển khai lifecycle chuẩn bị trận và UI, rồi nối lựa chọn vào combat hiện có. Giữ pause reasons, sleep lock, drag/throw/dizzy, AI fallback, kỷ lục và end battle.
3. Chuẩn hóa dữ liệu tăng dần. Chứng minh bằng fixture/ví dụ một biến thể slime mới nạp được mà không sửa controller; không tự chèn biến thể thử vào wave mặc định.
4. Test: mở/hủy/ready/replay, double click, trạng thái mèo đổi khi đang chuẩn bị, pause/fullscreen, resume/end; mỗi enemy có HP độc lập; dữ liệu lỗi/tham chiếu thiếu/phiên bản sai; override hợp lệ và fallback; lựa chọn vũ khí thực sự áp dụng vào trận và giữ snapshot.
5. Chạy regression phù hợp, cập nhật MANUAL-TEST.md, hướng dẫn custom và PROGRESS.md. Phân biệt probe/test với chơi desktop thật. Không dùng kết quả test cũ làm kết quả của đợt mới.

Các đề xuất review cũ (frame gap, playtest, nhận diện beta) vẫn có giá trị nhưng không ép người dùng hoàn tất theo thứ tự cũ trước khi làm nhiệm vụ trên. Shop/inventory, đổi vũ khí giữa đợt, pet pack tổng quát, LLM/voice/CV chưa thuộc phạm vi.

## Đánh giá và bằng chứng hiện tại

Project phù hợp dùng cá nhân/beta nhỏ; chưa đủ kiểm chứng để kết luận ổn định rộng rãi. Combat C# độc lập Python; có quái/boss, 5 kỹ năng, combo, độ khó, kỷ lục. Giữ engine WPF/vector hiện tại, Python bridge và fallback. Không xây lại ứng dụng.

Review độc lập sau đợt 11 đã chạy scripts/test.ps1 -Ui: **55 Python tests, 575 C# checks, 328 WPF checks đều đạt**, exit 0. Không phát hiện lỗi nghiêm trọng mới trong phần đã kiểm tra; không phải bảo đảm không còn lỗi. Lần ghi tài liệu này không chạy lại test và không thay source.

Chưa kiểm tra chơi thật, âm thanh trên loa, máy sạch hoặc mixed-DPI/nhiều màn hình vật lý trong lần review. Các mô phỏng/probe không thay thế những kiểm tra đó. Publish của Claude được ghi riêng trong PROGRESS.md; reviewer không publish/restart.

## Đã xử lý — không sửa lại nếu chưa có bằng chứng hồi quy

| Vấn đề cũ | Trạng thái sau đợt 11 |
| --- | --- |
| Pause ứng dụng vẫn dùng được Shield/chọn mục tiêu | Đã thêm BattlePause.App, đồng bộ SetPaused, chặn kỹ năng/target; có test giữ pause riêng của trận khi resume ứng dụng. |
| Nhấc/thả hủy mọi đòn quái dù grace có cooldown | Đã đổi: Windup giữ tiến độ khi mèo được cầm, tiếp tục khi mèo xuống. Có mô phỏng đo HP/số đòn cho slime và boss ở 3 độ khó. Cảm giác công bằng chưa được chơi thử xác nhận. |
| Test đẩy quái thất bại do spawn gần mép | Đã đặt quái ở vị trí xác định, thêm test biên riêng; suite review đạt. |
| Tài liệu bàn giao còn coi ba lỗi trên là chưa sửa | Đã cập nhật trong lần bàn giao này. |

Lịch sử: review đợt 10 từng đạt 55 Python/559 C#, WPF 322 đạt + 1 lỗi. Đó là kết quả cũ, không phải trạng thái test hiện tại.

## Ưu tiên 1 — Số đo giật khung hình chính xác

> Trạng thái (đợt 12, 2026-10-18, commit cb89351): đã sửa — RecordFrame nhận elapsed thật, simulation giữ dt chặn 0.1 s; có FrameChecks (gap giả 300 ms). Chưa đo thật khi idle/ngủ/battle/boss/pause trên desktop.

Điểm kiểm tra: src/DesktopPet/PetWindow.cs, Tick và RecordFrame; scripts/perf.ps1 nếu liên quan.

Phát hiện từ code: dt đã bị Math.Clamp(..., 0, 0.1) trước khi truyền vào RecordFrame. Khựng 300 ms có thể chỉ được ghi thành 100 ms. Đây là lỗi đo lường, chưa phải bằng chứng ứng dụng đang khựng.

Hướng thực hiện:
- Tách elapsed thực giữa các frame được xử lý khỏi dt giới hạn dùng cho simulation.
- RecordFrame đo elapsed thực; physics/combat vẫn giữ clamp và bước tích phân hiện có.
- Ghi rõ đơn vị, cửa sổ thống kê và ý nghĩa max gap; không thay timestep để làm đẹp số đo.
- Kiểm tra bằng thời gian giả: gap 300 ms phải báo khoảng 300 ms nhưng simulation vẫn không chạy bù vượt giới hạn.
- Đo khi idle/ngủ/battle/boss/pause với cùng điều kiện. Chưa kết luận RAM tăng là leak nếu chưa phân biệt heap sống và bộ nhớ GC giữ lại.

## Ưu tiên 2 — Hoàn thiện trải nghiệm trận đầu

> Trạng thái (đợt 12, commit 18cb227): đã làm hướng dẫn 4 thẻ (tự mở 1 lần, mở lại từ menu, trận dừng khi mở, không lấy focus) và phản hồi giữ đòn/bảo vệ trên sàn + HUD; có TutorialChecks. Chưa xem trực quan trên nền sáng/tối thật, chưa có playtest. Không đổi luật sát thương/cứu nguy.

### Hướng dẫn ngắn

- Hướng dẫn có thể bỏ qua và mở lại: click quái chọn mục tiêu; chọn kỹ năng rồi click quái; kéo mèo để cứu; giải thích cooldown/bảo vệ sau tiếp đất.
- Tôn trọng tiếng Việt/Anh, mute và focus; không bắt bàn phím khi người dùng đang gõ ứng dụng khác.
- Không spam hướng dẫn mỗi trận; nếu thêm preference thì default và load dữ liệu cũ phải an toàn.
- Hướng dẫn không tự bắt đầu trận khi mèo ngủ/đang kéo/bay và không gây trôi timer hoặc đòn đánh nền.

### Phản hồi nguy hiểm và trạng thái kỹ năng

- Làm rõ quái đang giữ tiến độ chuẩn bị đánh khi mèo được cầm. Thả gần quái có thể tiếp tục đòn sắp tới; người chơi cần hiểu qua cảnh báo.
- Phân biệt bảo vệ sau tiếp đất, thời gian chờ cấp bảo vệ lại và kỹ năng đang bị khóa do cầm/bay.
- Dùng HUD/effect hiện có, tương phản trên nền sáng/tối; tránh thêm cửa sổ lớn, hiệu ứng rối hoặc thông báo liên tục.
- Không tự đổi luật sát thương/cứu nguy nếu chưa có bằng chứng playtest và thống nhất thiết kế.

## Ưu tiên 3 — Playtest và kiểm chứng bản beta

> Trạng thái (đợt 12): CHƯA chơi thử thật. Đã thêm bảng Nhật ký chơi thử + câu hỏi vào docs/MANUAL-TEST.md cho người dùng điền.

Thực hiện 3–5 trận mỗi độ khó nếu có khả năng chơi thật. Ghi: phiên bản, độ khó, thời lượng, thắng/thua, HP cuối, số lần cứu, kỹ năng dùng và vấn đề quan sát. Không bịa dữ liệu nếu chỉ chạy simulation; ghi phần cần người dùng kiểm tra.

Câu hỏi cần trả lời:
- Đòn quái sau khi thả mèo có dễ hiểu/công bằng không?
- Năm kỹ năng có vai trò rõ, hay chỉ bấm tất cả khi hết cooldown?
- HUD/click-through/focus có ảnh hưởng ứng dụng bên dưới không?
- Âm thanh chiến đấu lâu có quá dày/to? Cảnh báo có rõ khi nhiều quái/boss?
- Các mức khó có tạo quyết định khác nhau hay chỉ kéo dài trận?

Kiểm tra thêm: pause giữa throw, menu/fullscreen, tắt Python, start/end nhiều lần, đóng app giữa trận, màn hình mất kết nối và mixed-DPI nếu có phần cứng. Dùng dữ liệu thử riêng, không ghi đè preferences/kỷ lục thật.

## Ưu tiên 4 — Nhận diện bản phát hành

> Trạng thái (đợt 12, commit facb54d): build ghi commit + ".dirty" khi có thay đổi chưa commit (About, log); ZIP đặt tên theo version + commit (+ thời gian nếu dirty). Chưa đổi số version, chưa changelog/tag, chưa thử máy sạch (cần người dùng quyết/làm).

Hiện csproj để Version=1.0.0, scripts/publish.ps1 đặt tên ZIP theo version và thay ZIP cùng tên. Có nhiều thay đổi chưa commit. Đây là rủi ro truy vết phiên bản, không phải lỗi combat.

- Khi chuẩn bị phát hành, thống nhất version beta và changelog; đưa version/mã commit vào thông tin build nếu phù hợp.
- Nếu build từ working tree có thay đổi, thể hiện trạng thái đó; mã commit một mình không nhận diện đủ nội dung chưa commit.
- Không ghi đè gói bản cũ chỉ vì cùng tên; có chiến lược tên artifact phân biệt phiên bản.
- Chốt commit/tag theo chỉ dẫn người dùng, không tự commit/publish trong nhiệm vụ review/tài liệu.
- Thử gói đầy đủ trên máy sạch: không cần Visual Studio, có .NET/Python/assets cần thiết; AI lỗi thì local fallback vẫn hoạt động.

## Tương lai — chưa nằm trong đợt triển khai này

> Trạng thái (đợt 13, 2026-10-18): đã làm hai vũ khí theo yêu cầu người dùng — Kiếm (giữ số cũ) và Búa (battle.json "hammer"), chọn trong menu, kỷ lục chung theo độ khó; có BattleWeaponTests + WeaponChecks và số đo mô phỏng trong PROGRESS.md. Chưa chơi thử thật.

Sau khi chốt trải nghiệm trận đầu, cân nhắc hai vũ khí: kiếm nhanh và búa chậm có đẩy lùi. Mục đích tạo lựa chọn chiến thuật mà không thêm nhiều nút. Chưa thêm inventory/shop, nhiều quái/kỹ năng, LLM/voice/CV lúc này.

Giữ logic combat độc lập WPF; khi mở rộng, tách theo trách nhiệm thay vì tiếp tục dồn vào PetWindow. Không refactor diện rộng chỉ vì file dài.

## Tiêu chí bàn giao sau khi thực hiện

- Liệt kê file thay đổi, hành vi mới và rủi ro; chạy kiểm thử phù hợp, giữ regression Pet Mode (sleep/wake/drag/throw/dizzy/emotion/AI fallback).
- Ghi kết quả thật, phân biệt unit test, WPF probe và desktop vật lý.
- Cập nhật PROGRESS.md, docs/MANUAL-TEST.md và trạng thái từng mục ở đây. Nếu chưa làm được playtest/máy sạch, ghi rõ chưa xác minh thay vì đánh dấu hoàn thành.
