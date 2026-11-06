# Cân bằng Slime Mẹ — 06/11/2026 (#67)

Đối chứng HEAD sạch `59567c2` (controller mẹ cũ, HP650) và controller mới HP600/damage26, một nhát làm rơi + một nhát làm vỡ trứng. Giữ threat, giới hạn quân, ngưỡng nghiệm thu, các boss khác. Trước: Adventure 221 check đạt (`mother-balance-before.log`); Viễn chinh 82 check đạt trên worktree HEAD (`mother-expedition-before.log`). Sau: Mother/Adventure/Expedition 340 check đạt (`mother-verified-suites.log`). Đây là mô phỏng, chưa nghiệm thu chơi tay.

Bot mới đọc vị trí đã báo, bế ở tốc độ ngang tối đa700DIP/s/dọc600DIP/s, dùng hồi vũ khí thật để giải trứng/Trứng cuối; không trừ HP trực tiếp hay bỏ qua mech. Simple né vùng báo, casual chỉ giải mech; idle không giải nên không thể hạ boss mới. Mọi ngưỡng cũ giữ nguyên.

## Đầm nhầy — simple, 60 seed, mang build từ các màn trước

| Vũ khí | Độ khó | Thắng trước | Thắng sau | Giây trước | Giây sau | HP trước | HP sau |
|---|---|---:|---:|---:|---:|---:|---:|
| sword | Normal | 60/60 | 60/60 | 89 | 153 | 78% | 75% |
| sword | Hard | 39/60 | 33/60 | 103 | 178 | 50% | 65% |
| hammer | Normal | 59/60 | 53/60 | 96 | 173 | 70% | 73% |
| hammer | Hard | 28/60 | 25/60 | 114 | 204 | 40% | 68% |
| yarn | Normal | 60/60 | 51/60 | 110 | 184 | 86% | 68% |
| yarn | Hard | 51/60 | 22/60 | 127 | 225 | 67% | 71% |
| glove | Normal | 60/60 | 59/60 | 86 | 145 | 79% | 75% |
| glove | Hard | 49/60 | 44/60 | 95 | 163 | 58% | 65% |

## Bãi rác — Normal/simple/tham lam, 40 seed

Số liệu cho toàn tuyến, không chỉ trận Tổ Slime Mẹ; bot không mua đồ hoặc chơi thay người. Kỷ lục mới Bãi rác17/Đầm nhầy6/Vô tận24.

| Vũ khí | Thắng trước | Thắng sau | Tổng giây trước | Tổng giây sau |
|---|---:|---:|---:|---:|
| sword | 21/40 | 22/40 | 357 | 386 |
| hammer | 18/40 | 18/40 | 392 | 435 |
| yarn | 21/40 | 21/40 | 393 | 422 |
| glove | 28/40 | 27/40 | 344 | 370 |

Búa sát ngưỡng18/40, cần chơi tay. Chín đòn và các khoảng giải mech làm trận dài hơn trước; không dùng việc qua mô phỏng để khẳng định độ khó/mỹ thuật đã được duyệt. Các lần thử số liệu không đạt đã giữ ở artifacts/mother-balance*.log; không nới tiêu chuẩn để qua.

## Kiểm chứng tổng thể

- Full `scripts/test.ps1 -Ui` chạy một lần: Python58+i18n5 đạt, C#3748 đạt/9FAIL ban đầu. Đã sửa ba assertion rulesVersion và một nhóm khóa dịch cũ; bốn check registry đạt khi chạy ToolingTests với quyền ghi HKCU test tạm (24 check đạt). Lỗi cân bằng Búa đã sửa ở số nhát trứng. Kiểm lại Mother/Adventure/Expedition340 và metadata232 đều đạt. Không bỏ test lỗi để publish.
- WPF1496 đạt trên desktop tương tác; sandbox trước đó không capture được chuột. Đã xem render trước/sau thân mẹ, trứng treo, phase3, nơ/dây, cửa bụng/vòng vuốt trên nền sáng/tối. Có37 ảnh mother ở artifacts/mother-render, gồm chín windup thật. Không đồng nghĩa đã chơi thật hoặc thử DPI/nhiều màn hình.
- Build/publish/smoke/perf cuối được ghi thêm ở Issue_fixxed/PROGRESS sau đóng gói.
