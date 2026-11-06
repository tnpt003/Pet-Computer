# Rà soát và chuẩn hóa 1.5 — 07/11/2026

Phạm vi: kiểm tra bản tích hợp sau #70, sửa sai lệch xác định được, đồng bộ tài liệu và lập điều kiện trước M30. Không thêm gameplay, không đổi HP/damage/threat, seed, rulesVersion hoặc ngưỡng cân bằng; không nâng Version/tag/release remote. HEAD gốc: `d641d64` sau gói code `79c9f1c`; commit của lượt rà được ghi ở PROGRESS.

## Nguồn luật

- UPDATE-1.5 là mục lục nội dung hiện hành; ZIP-SLIME, MOTHER-SLIME, MUTANT-SLIME sở hữu luật riêng từng boss. APPROVED-EXCEPTIONS lưu các lần duyệt; đoạn mới thay đoạn cũ trong đúng phạm vi, không cộng mọi luật lịch sử vào nhau.
- `Assets/battle.json` sở hữu số cấu hình; `PetBattleData.cs` sở hữu kiểu, fallback và validation. Core C# sở hữu combat/physics; WPF sở hữu input/cửa sổ/vẽ; Python chỉ quyết định hành vi ngoài trận và có local fallback.
- BOSS-TACTICS giữ mô tả controller legacy. Không dùng Lứa cuối, vương miện/gọi quân cũ cho `mother:true` hay `mutant:true`.
- Mỗi kết quả đo gắn commit, phương pháp, seed/cỡ mẫu và giới hạn. Đạt unit, xem render, smoke và chơi tay là bốn loại chứng cứ khác nhau.

## Phát hiện và hướng chuẩn hóa

| Ưu tiên | Phát hiện | Xử lý trong lượt rà |
|---|---|---|
| Cao | STANDARDIZATION còn miễn mọi chiêu boss khi bế/bay, tương tác mèo cắt dây/tay kéo ZIP và Lứa cuối Mẹ | Đồng bộ ngoại lệ hiện hành, tách controller legacy |
| Cao | UPDATE còn nói chỉ slime bay làm mèo đang cầm mất máu; trạng thái test và FPS lấy lần cũ | Sửa mô tả, gắn kết quả với phiên bản |
| Cao | WPF #70 lỗi lớp nền; lần chạy đối chứng mới đạt 1757 nên có tính gián đoạn | Kiểm tra thêm Topmost/fullscreen, giữ assertion z-order native |
| Cao | FPS crowd-late #70 median53,9/min30,9/gapmax173 ms dưới mục tiêu ~57 | Đo lại artifact sạch; không giảm cảnh báo/gameplay để ép FPS |
| Vừa | Kết quả test và cân bằng phân tán qua nhiều đợt | Bảng nghiệm thu bên dưới và liên kết số đo mới |
| Vừa | Cân bằng Búa Bãi rác Normal đường tham 18/40 đúng ngưỡng; bot chưa nghiệm thu hết Mẹ/Biến dị | Giữ số liệu/ngưỡng, chạy bộ cân bằng và ghi rõ giới hạn; người dùng chơi tay |
| Vừa | Mèo tự rớt khi bế đánh slime bay chưa tái hiện (#52) | Giữ log `Cat let go in battle`, chờ seed/log; chưa kết luận đã sửa |
| Vừa | Version vẫn 1.4.4 dù chứa nội dung 1.5 | Đúng trạng thái trước M30; chỉ nâng khi nghiệm thu release |

## Ma trận nghiệm thu bản tích hợp

| Khu vực | Kiểm tra tự động/đối chiếu | Phần cần nghiệm thu thật |
|---|---|---|
| Pet Mode | Physics, Sleep, EmotionPriority, AI/fallback và WPF input trong bộ đầy đủ | Sleep lock/wake, kéo nhẹ/mạnh, ném, ưu tiên cảm xúc, Python tắt/lỗi |
| Combat chung | Damage/Shield/Rage, Pause và Screenshot, lọc skill/app/thẻ, dữ liệu/fallback | Click mèo/kỹ năng/quái/icon, nhấc/choáng, Pause giữa cú ném |
| ZIP | BattleZip/ZipMove/ZipProbe và WPF ZIP | Ba lượt lăn, đai/khóa, file/cây, sáu tay và bom cuối |
| Mẹ | BattleMother, bot câu đố, MotherVisual/Motion | Chín đòn, Trứng treo, trả con, Thanh dỗ, Trứng cuối |
| Biến dị | BattleMutant, bot câu đố, MutantVisual | Bướu/ping pong, dải truy sát, ép rơi và bảo vệ, sáu cửa lõi |
| Phiêu lưu | AdventureContent/ContentLearning/Guide và cân bằng | Năm bài chương 2, đủ app đã chọn, đường mở khóa/phần thưởng |
| Viễn chinh/Vô tận | Expedition/EndlessMutation, pool/validation, thẻ ngoài trận | Tuyến khó, boss30–40, thẻ/di vật/app thật; cảm giác độ khó |
| Lưu trữ/kỷ lục | Tests chuyến cũ, keepRuns, rulesVersion, lỗi lưu | Dữ liệu từ 1.4.4, tiếp tục chuyến, không mất thiết lập |
| UI/vi/en/ja | WPF render/layout, i18n, Topmost/fullscreen | Màn hẹp, nền sáng/tối, DPI100/125/150/200%, màn tọa độ âm |
| Đóng gói | Build Release, publish sạch, smoke single-instance/Python/thoát | Bộ cài/cập nhật/gỡ từ 1.4.4, khởi động lại Windows |
| Hiệu năng | crowd-late, FPS và khoảng khựng | Trận boss thật, đo dài hơn và profile trước tối ưu |

Các bộ test được gọi bởi `scripts/test.ps1 -Ui`; không suy ra đã chạy chỉ vì liệt kê ở đây.

## Kết quả thực tế của lượt rà

- Build Release: 0 error, 7 warning có sẵn (6 member che member WPF và `cursorProbe` gán qua reflection trong test); không sửa các lớp không liên quan chỉ để xóa warning.
- Python 58 test và i18n 5 test đạt. C# **3797 check/93 suite đạt**, không FAIL. So **364 dòng tỷ lệ thắng với bản cuối #70: 0 dòng đổi**; không suy ra mọi cột thời gian/HP hay cảm giác chơi đều giống.
- WPF **1759 check đạt**, gồm hai check thêm về refresh Topmost và quay lại sau fullscreen. Lần đầy đủ đầu dừng WPF vì thư mục render chưa tạo; đã tạo và kiểm lại riêng WPF, không chạy lại C#/Python đã đạt. Lượt WPF thử trong sandbox gặp lỗi capture chuột, không dùng làm kết quả nghiệm thu; lượt cuối chạy trên desktop ngoài sandbox.
- Probe riêng trước sửa: refresh Topmost làm assertion nền dưới slime FAIL; sau sửa đạt. Dải trận nhận nền làm owner, Windows giữ thứ tự owned/owner; bỏ callback dispatcher sửa muộn. Không đổi input handlers, simulation, physics hoặc số liệu.
- Đã mở ảnh `battle-scenery-dump.png` trước/sau trên nền sáng/tối; SHA256 giống nhau, art không đổi. Kiểm tra z-order là native probe/WPF, ảnh bitmap không chứng minh desktop thực hay nhiều màn hình.
- Log cục bộ: `artifacts/standardize15-tests.log`, `standardize15-ui-final.log`, `standardize15-build.log`, `standardize15-zorder-repro.log`, `standardize15-zorder-fixed.log`, `standardize15-balance-diff.txt`; ảnh `artifacts/standardize15-render/`.
- Commit/publish/smoke và phép đo FPS trên artifact sạch: ghi ở PROGRESS và mục kết quả đóng gói khi chạy xong. Chưa nghiệm thu chơi tay/DPI/cập nhật từ 1.4.4.

## Điều kiện trước M30

1. Bộ tự động sạch; mọi FAIL còn lại phải có nguyên nhân và bằng chứng, không tắt assertion hoặc nới ngưỡng để đạt.
2. Đo hiệu năng trên đúng artifact sau commit; giữ cảnh báo gameplay, phân biệt phép đo ngắn và profile dài.
3. Người dùng nghiệm thu các hàng cột cuối theo MANUAL-TEST; bot không thay thao tác thật.
4. Rà câu mới tiếng Nhật, README/website/changelog và cập nhật từ 1.4.4; chỉ sau đó nâng 1.5.0 theo RELEASE.md.

Chưa thêm cơ chế/kỹ năng, không refactor state machine/renderer rộng. Chuẩn hóa tiếp sau 1.5: phát hiện tự động loại quái mới thiếu `UsesMouse`/điều kiện loại trừ và dữ liệu scene (backlog đã có trong UPDATE-1.5).
