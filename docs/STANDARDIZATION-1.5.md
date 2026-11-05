# Chuẩn hóa và tích hợp 1.5 — 2026-11-06

Phạm vi đã duyệt: hợp nhất Slime Nén đã hoàn tất (`0172091`, code `6739762`) với hai boss (`91a49e9`, code `9477828`), review và sửa những điểm lệch logic/code/dữ liệu/tài liệu. Không thêm ý tưởng gameplay mới, không phát hành remote, không tự nâng version lên 1.5.0 trước M30.

## Nguồn chuẩn và trạng thái

| Nội dung | Nguồn chịu trách nhiệm |
|---|---|
| Danh sách Q/S/B/V/K/M/T/C/X/E/J/A/L/F, luật đang áp dụng | UPDATE-1.5.md |
| Số liệu cấu hình và validation | Assets/battle.json, PetBattleData.cs |
| Combat, vị trí/vận tốc trận, timer, ngẫu nhiên theo seed | DesktopPet.Core/PetBattle*.cs |
| Cửa sổ/input, quy tắc dừng trận và vẽ | PetWindow.Battle*.cs, PetBattleView*.cs |
| Ngoại lệ người dùng đã duyệt | APPROVED-EXCEPTIONS.md |
| Kết quả đã chạy, việc chưa xong | PROGRESS.md; lỗi review ở Issue_fixxed.md phần B |
| Ý tưởng chưa duyệt hoặc hoãn | UPDATE-1.5.md → Backlog sau 1.5; NEXT-STEPS.md |

Không gọi M30 hoàn tất chỉ vì code/test tự động đạt. Còn nghiệm thu thao tác thật, độ khó và màn hình thật. Phân vai Python không đổi: không combat, không physics; AI lỗi thì mèo dùng local fallback.

## Luật tương tác dùng chung

| Trạng thái | Đánh/gây sát thương | Nhấc và điều khiển | Đồng hồ |
|---|---|---|---|
| Mèo dưới sàn | Đánh tự động; nhận đòn nếu trong vùng và không được bảo vệ | Drag chỉ bắt đầu sau ngưỡng khoảng 6 DIP | Trận chạy |
| Mèo được cầm | Chỉ tự đánh quái trên không; đòn sàn và chiêu boss mới không trúng. Ngoại lệ: đạn dính slime bay | Cửa sổ sở hữu drag/pendulum | Trận chạy, giữ luật hồi kỹ năng hiện có |
| Mèo bay sau ném | Không tự đánh; chỉ tương tác ném có thiết kế riêng, gồm băng/tường/dây/tay kéo Slime Nén | C# physics, không do Python | Trận chạy |
| Mèo choáng | Không bắt đầu nhấc; hết choáng mới nhấc được | Không tự đổi thành Dizzy | Hết theo thời gian trận |
| Khiên bong bóng | Chặn toàn bộ cú đánh, cả sát thương và hiệu ứng kèm theo | Không đổi luật drag | Hết theo thời gian trận |
| Nộ | Vẫn nhận sát thương thường; giải và miễn hiệu ứng bất lợi, gồm choáng/chậm/đẩy lùi | Bật được khi choáng, không bật khi đang cầm | 5 giây trận; Pause đóng băng |
| Pause/menu/chọn thẻ/túi đồ/fullscreen | Không tiến combat | Khóa nhấc, đóng băng cả mèo/throw; Away có ngoại lệ riêng | Dừng theo battle pause reasons |
| Hướng dẫn đứng hình | Simulation đứng hình; chỉ tương tác đang dạy được dùng | Vẫn nhấc/kéo để làm theo hướng dẫn; không gọi đó là Pause | Không tiến simulation |
| Chụp Màn Hình | Quái/đạn/chiêu quái đứng hình, không làm quái trên cao rơi | Không khóa toàn bộ mèo như Pause | Đồng hồ của phần bị freeze dừng |

`BattleSim.Step(catHeld, lifted, pose)`: `catHeld` là mèo đang do cửa sổ điều khiển (cầm **hoặc bay**), không đồng nghĩa với `CatPose.InHand`. Tests phải truyền đúng cặp cờ để không tạo trạng thái bay giả trên sàn.

## Hình học cảnh báo và sát thương

- `BossDangerZone`, `BattleSim.BossZone`, `BossWaveZone`, `BossRainZone` là nguồn chung cho chiêu boss mới, renderer và bot. Không khai báo lại bán kính/vị trí vùng trúng trong renderer.
- `bossTactics` có các khóa tune: `sweepReach=125`, `sweepRadius=125`, `burstRadius=160`, `jumpRadius=42`, `waveRadius=38`, `waveSpacing=65`, bên cạnh 5 khóa thời gian/ổ đã có. C# có mặc định và Validate từng khóa. Đổi mặc định không tự nới tiêu chí test.
- Các hằng luật ít cần tune (7 điểm sóng, khoảng cách bầy nhảy, tốc độ cắt dây, thời gian bay) dùng chung từ BattleSim, không nhân bản ở phần vẽ.
- Vòng sóng trước đây bán kính 27 nhưng hit radius 38: đã sửa để cảnh báo bao đúng vùng trúng. Mưa đang gồng cũng hiển thị đủ vị trí hai nhịp từ cùng công thức với lúc tạo giọt.
- Slime Nén giữ nguồn số liệu `ZipSpec` và cơ chế riêng đã duyệt; không ép nó vào `BossTactics` chỉ để gom code.

## Giới hạn quân và hiệu ứng

| Giới hạn | Ý nghĩa thực tế |
|---|---|
| `maxAlive` của trận/màn | Điều kiện nhận đơn vị tiếp theo dùng `Alive + UnitRoom`; đội hình/bầy nhập ra trọn gói nên số quái thật có thể vượt cap. Không coi đây là trần tuyệt đối của số object |
| Slime hẹn giờ | Gọi đội hình có ngoại lệ vượt cap và giới hạn loại liên kết; hữu hạn theo `clock.calls`, không gọi lồng slime hẹn giờ |
| `summonCap` của boss | Hạn mức quân do chính boss sinh, tách khỏi cổng nhận đợt thường |
| Slime Mẹ | Tối đa 2 ổ và tổng 6 con/ổ còn sống, tính cả đang xuất hiện; nở không bỏ qua hạn mức. Lứa cuối chờ có chỗ, chỉ mất máu khi đặt được ổ |
| Vũng nhầy | Tối đa 6 toàn trận; vũng thường gần nhau vẫn gộp dù khác chủ. Nhầy của boss nâng cấp tách riêng để không hút nhầm nhầy thường |
| Hiệu ứng trang trí | Bị chặn bởi `MaxEffects` (mặc định C# 32). Không giảm cảnh báo gameplay chỉ để đạt FPS |
| Liên kết/con trỏ | Mặc định tối đa 2 loại liên kết, 2 loại quái dùng con trỏ, 2 xích trên một đồng minh; giữ ngoại lệ hẹn giờ đã duyệt |

`MalwareCue.DrawKinds` dùng chung cho trọng số app độc tùy chỉnh và ghi nhận Sổ tay, giữ nguyên thứ tự bốc cũ. `infect` có đường riêng; test kiểm tra tập tên và thứ tự, không thêm app độc mới.

## Tiêu chí thiết kế cho mỗi nội dung

Giữ ID đang có trong UPDATE-1.5 (không đánh lại số M/Q/S/B). Mỗi thay đổi phải trả lời sáu câu: vai trò/áp lực gì; dấu báo gì; cách đối phó; cơ hội phản công; giới hạn/điều kiện ngắt; chứng cứ kiểm tra để được gọi là xong. Kỹ năng phù hợp giúp dễ hơn, không trở thành khóa bắt buộc không có cách thay thế.

Đã làm, đã kiểm tra tự động, đã xem render và đã chơi tay là các trạng thái khác nhau. Đề xuất mới không trộn vào bảng luật hiện tại khi chưa duyệt. Không refactor toàn bộ state machine, framework hoặc renderer trong mốc này.

## Kiểm tra và kết quả

Bảng đo cùng phương pháp trước/sau, hàng sát ngưỡng và giới hạn bot: [BALANCE-1.5-INTEGRATED.md](BALANCE-1.5-INTEGRATED.md).

- Đối chứng sạch ở `artifacts/standardize-baseline`, commit `0172091`. Log cân bằng trước: `artifacts/standardize-before.log`; log bản tích hợp và bảng so sánh sẽ ghi trong PROGRESS khi chốt.
- `BossTacticsTests`: lứa cuối hết chỗ/được giải phóng chỗ; ranh giới warning/hit khi override số liệu; vị trí/radius sóng; ma trận floor/held/flying/Shield/Rage/Pause/Screenshot; Nộ ngăn và giải đẩy; vũng thường giữ luật gộp, vũng boss riêng.
- `BattleZipTests`: giữ đủ khóa vỏ/bung file/bom nén, thao tác mèo và Kéo. Các móc cắt tay kéo/cắt dây và validation giữ cả hai nhánh sau merge.
- Bot simple/casual biết dùng Khiên trước vùng chiêu mới và giọt sắp rơi, với thời gian phản ứng; vẫn **không** tự thả đè Slime Nén hay kéo tay kéo bằng mèo. Không gọi đó là bot chơi tối ưu hoặc nghiệm thu tay.
- Lần full đầu bắt lệch Cuộn len màn 2 Hard. Đã viết test tái hiện thay đổi gộp vũng ngoài phạm vi boss và khôi phục luật cũ; không tự nới ngưỡng 50 điểm đã duyệt. Đo lại là bắt buộc trước publish.
- Mẫu 20 seed vẫn còn lệch một lượt Cuộn len màn 2 (25% vs kiếm 80%). Đã giữ gợi ý kỹ năng gốc sau khi thử Bom làm các vũ khí khác lệch hơn; không đổi số liệu để ép đạt. Toàn chương 2 dùng 60 seed cố định cho mọi mức/vũ khí/bot và cả đối chứng, tất cả tỷ lệ yêu cầu/ngưỡng chênh giữ nguyên. Checkout đối chứng giữ gameplay 0172091, chỉ cùng thay cỡ mẫu trong test harness; ghi rõ 20/60, không trộn mẫu trong bảng.
- Mốc chỉ hoàn tất kỹ thuật khi build/test thích hợp đạt, commit trước publish, smoke artifact tích hợp đạt; ghi riêng FPS trung vị/thấp nhất và khoảng khựng. Không dùng số perf của nhánh riêng để đại diện bản tích hợp.

Chưa nghiệm thu chơi tay, DPI/multi-monitor, cài đặt/cập nhật từ 1.4.4. Các điểm này tiếp tục là điều kiện trước M30; chưa có tag, push hay release mới.
