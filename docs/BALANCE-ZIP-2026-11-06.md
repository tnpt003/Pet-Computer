# Slime Nén sau chơi thử — đối chứng 06/11, Issue #56

Đây là mô phỏng, chưa chơi thật. **Chưa đủ điều kiện publish:** ba ngưỡng còn FAIL ở bản HP 1100. Không thay ngưỡng test.

## Cách đo

- Trước: source HEAD `aa627fd`, xuất bằng `git archive` vào `artifacts/zip-baseline-src`, chạy `AdventureBalanceTests` (221 check đạt; `artifacts/zip-before-balance.log`). Không sửa source chính hoặc dữ liệu người dùng để đo.
- Sau: `AdventureBalanceTests`, `ExpeditionTests`, `ZipProbeTests`; `artifacts/zip-balance-final.log`: 290 check đạt, 3 FAIL.
- Cùng 60 seed cho chương 2, 40 seed cho Viễn chinh, giữ build/đường đi/thẻ/ngưỡng có sẵn. Bot mới biết đánh khóa, ghép file, đánh tay kéo và soi file bom. Tốc đi dùng số liệu trận; đổi phía đẩy file dùng thao tác cầm thật trong mô phỏng. Bot cơ bản giữ chính sách né bằng kỹ năng, chỉ nhấc khi giải mech; probe riêng biết nhấc né. Vì mech và cách giải đã đổi, đây là đối chứng kết quả, không phải cùng chuỗi input.
- Probe riêng trước đây ghi “không app” nhưng constructor vẫn bật app độc: bản mới dùng `apps:false` đúng phạm vi. Probe đạt Kiếm thường Normal 4/4; không dùng số đo probe cũ làm đối chứng có kiểm soát.

## Màn 5 chương 2, người chơi cơ bản

| Vũ khí | Độ khó | Trước thắng/60 | Sau thắng/60 | Giây thắng trước → sau | HP còn trước → sau |
|---|---|---:|---:|---:|---:|
| Kiếm | Normal | 55 | 51 | 116 → 138 | 73% → 80% |
| Búa | Normal | 56 | 51 | 121 → 157 | 70% → 79% |
| Cuộn len | Normal | 57 | 43 | 132 → 158 | 65% → 77% |
| Găng | Normal | 57 | 52 | 113 → 131 | 72% → 84% |
| Kiếm | Hard | 39 | 32 | 127 → 157 | 60% → 69% |
| Búa | Hard | 38 | 33 | 137 → 180 | 64% → 76% |
| Cuộn len | Hard | 36 | 21 | 158 → 200 | 64% → 67% |
| Găng | Hard | 43 | 39 | 121 → 143 | 68% → 80% |

Các ngưỡng còn FAIL:

1. Cuộn len ở boss Normal: 43/60 = 72%, cần ≥80%.
2. Viễn chinh Lối Bãi rác Normal, đường tham lam, Kiếm: 15/40, cần ≥20/40.
3. Cùng tuyến/đường, Cuộn len: 13/40, cần ≥20/40.

Các ngưỡng khác đạt, gồm chênh vũ khí, thời gian trận, Kiếm Normal ≥80%, HP cuối Kiếm Normal ≤90% (80%), người chơi không thao tác không thắng boss (0%). Kết quả thắng và HP chỉ lấy những lượt thắng nên không suy ra trận đã dễ hơn chỉ từ HP cao.

## Điểm chờ quyết định

Đã gửi người dùng đề xuất **thử HP 1100 → 1000**, giữ mech và ngưỡng test rồi đo lại. **Chưa được duyệt, chưa áp dụng.** Nếu giữ HP 1100 thì vẫn giữ rõ ba FAIL này; không publish bằng cách bỏ suite hoặc nới ngưỡng.

## Kiểm chứng kỹ thuật

- Python: 58 hành vi + 5 i18n đạt ở lượt đầy đủ; i18n chạy lại sau sửa Sổ tay đạt 5/5.
- C#: lượt đầy đủ sau sửa code 89 suite, 3222 đạt/1 FAIL (`zip-csharp-verified.log`); các suite tính năng đạt. Sau sửa mô hình bot, chỉ chạy lại ba suite cân bằng: 290 đạt/3 FAIL như trên.
- WPF: 1431 check đạt (`zip-wpf-verified.log`); đã mở ảnh ổ khóa, ba cây thư mục và cảnh báo lăn trên nền sáng/tối. Chưa chơi trận thật, thử DPI hoặc nhiều màn hình.
- Bản đang chạy và artifact publish cũ chưa bị thay. Chờ cân bằng/duyệt bổ sung rồi mới commit mốc hoàn chỉnh, publish và smoke.
