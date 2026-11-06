# Đối chứng phân bố Viễn chinh/Vô tận #70 (07/11)

Đây là mô phỏng, không phải chơi thật. Gốc: checkout sạch `720a018` (sau #69). Giữ seed, bot, ngưỡng nghiệm thu và chỉ số boss. Chạy gốc `AdventureBalanceTests,EndlessMutationTests` và `ExpeditionTests`; bản cuối chạy full C# rồi kiểm lại `ExpeditionTests,EndlessMutationTests` sau điều chỉnh phân bố.

## Tỷ lệ thắng Viễn chinh

Đơn vị: thắng/40 chuyến, người chơi simple; mỗi ô là **an toàn / tham**. Thẻ ngoài trận nay có đúng ngữ cảnh app/đồ rơi, nên cả tuyến cơ bản cũng đổi lựa chọn build dù roster giữ nguyên.

| Tuyến | Vũ khí | Độ khó | Trước | Sau |
|---|---|---|---|---|
| slime_trail | sword | Normal | 38/40 / 35/40 | 38/40 / 35/40 |
| slime_trail | sword | Hard | 16/40 / 10/40 | 14/40 / 9/40 |
| slime_trail | hammer | Normal | 38/40 / 30/40 | 38/40 / 29/40 |
| slime_trail | hammer | Hard | 21/40 / 8/40 | 21/40 / 8/40 |
| slime_trail | yarn | Normal | 38/40 / 34/40 | 38/40 / 34/40 |
| slime_trail | yarn | Hard | 24/40 / 13/40 | 18/40 / 13/40 |
| slime_trail | glove | Normal | 30/40 / 33/40 | 30/40 / 32/40 |
| slime_trail | glove | Hard | 18/40 / 9/40 | 18/40 / 9/40 |
| dump_trail | sword | Normal | 29/40 / 23/40 | 29/40 / 22/40 |
| dump_trail | sword | Hard | 9/40 / 3/40 | 13/40 / 6/40 |
| dump_trail | hammer | Normal | 27/40 / 18/40 | 23/40 / 18/40 |
| dump_trail | hammer | Hard | 5/40 / 3/40 | 6/40 / 2/40 |
| dump_trail | yarn | Normal | 15/40 / 24/40 | 20/40 / 22/40 |
| dump_trail | yarn | Hard | 8/40 / 3/40 | 10/40 / 3/40 |
| dump_trail | glove | Normal | 33/40 / 25/40 | 32/40 / 24/40 |
| dump_trail | glove | Hard | 16/40 / 8/40 | 14/40 / 6/40 |

## Các dòng đổi tỷ lệ thắng

So 364 dòng tỷ lệ thắng (300 Phiêu lưu + 64 Viễn chinh): 31 dòng đổi, đều ở Viễn chinh. Đây là so cột thắng, không phải khẳng định mọi cột thời gian/HP giữ nguyên. Ngưỡng nghiệm thu không hạ; 118 check của hai bộ Viễn chinh/Vô tận cuối đạt. Kiếm/Cuộn len Bãi rác Normal đường tham đều 22/40, Búa giữ 18/40 ngay ngưỡng đã duyệt; cần chơi tay. Bố trí thêm Bạt nhún ở Vũng nhầy và đội trống/dây chuyền ở Sảnh trống từng làm hai dòng xuống 19/40 nên đã bỏ, giữ tổ hợp cũ ở hai trận này.

| Bảng | Vũ khí | Độ khó | Bot | Đường | Trước → sau |
|---|---|---|---|---|---|
| Expedition balance | sword | Normal | casual | safe | 37/40 → 36/40 |
| Expedition balance | sword | Hard | simple | safe | 16/40 → 14/40 |
| Expedition balance | sword | Hard | simple | greedy | 10/40 → 9/40 |
| Expedition balance | sword | Hard | casual | safe | 9/40 → 8/40 |
| Expedition balance | hammer | Normal | simple | greedy | 30/40 → 29/40 |
| Expedition balance | hammer | Normal | casual | safe | 33/40 → 34/40 |
| Expedition balance | hammer | Normal | casual | greedy | 30/40 → 29/40 |
| Expedition balance | hammer | Hard | casual | greedy | 2/40 → 3/40 |
| Expedition balance, 1.5 weapons | yarn | Hard | simple | safe | 24/40 → 18/40 |
| Expedition balance, 1.5 weapons | glove | Normal | simple | greedy | 33/40 → 32/40 |
| Expedition balance · dump_trail | sword | Normal | simple | greedy | 23/40 → 22/40 |
| Expedition balance · dump_trail | sword | Normal | casual | safe | 27/40 → 26/40 |
| Expedition balance · dump_trail | sword | Normal | casual | greedy | 20/40 → 21/40 |
| Expedition balance · dump_trail | sword | Hard | simple | safe | 9/40 → 13/40 |
| Expedition balance · dump_trail | sword | Hard | simple | greedy | 3/40 → 6/40 |
| Expedition balance · dump_trail | sword | Hard | casual | safe | 7/40 → 6/40 |
| Expedition balance · dump_trail | sword | Hard | casual | greedy | 2/40 → 4/40 |
| Expedition balance · dump_trail | hammer | Normal | simple | safe | 27/40 → 23/40 |
| Expedition balance · dump_trail | hammer | Normal | casual | safe | 26/40 → 27/40 |
| Expedition balance · dump_trail | hammer | Normal | casual | greedy | 18/40 → 17/40 |
| Expedition balance · dump_trail | hammer | Hard | simple | safe | 5/40 → 6/40 |
| Expedition balance · dump_trail | hammer | Hard | simple | greedy | 3/40 → 2/40 |
| Expedition balance · dump_trail | hammer | Hard | casual | safe | 5/40 → 3/40 |
| Expedition balance · dump_trail | hammer | Hard | casual | greedy | 4/40 → 1/40 |
| Expedition balance, 1.5 weapons · dump_trail | yarn | Normal | simple | safe | 15/40 → 20/40 |
| Expedition balance, 1.5 weapons · dump_trail | yarn | Normal | simple | greedy | 24/40 → 22/40 |
| Expedition balance, 1.5 weapons · dump_trail | yarn | Hard | simple | safe | 8/40 → 10/40 |
| Expedition balance, 1.5 weapons · dump_trail | glove | Normal | simple | safe | 33/40 → 32/40 |
| Expedition balance, 1.5 weapons · dump_trail | glove | Normal | simple | greedy | 25/40 → 24/40 |
| Expedition balance, 1.5 weapons · dump_trail | glove | Hard | simple | safe | 16/40 → 14/40 |
| Expedition balance, 1.5 weapons · dump_trail | glove | Hard | simple | greedy | 8/40 → 6/40 |

## Vô tận

Normal, bot simple, 8 seed 100–107, giới hạn 20 phút mô phỏng mỗi lượt. Trung vị lấy phần tử thứ 5 của 8 lượt đã sắp (giữ cách tính sẵn có).

| Vũ khí | Trước: các đợt đạt được | Trung vị trước | Sau: các đợt đạt được | Trung vị sau |
|---|---|---|---|---|
| Kiếm | 15 21 27 28 28 28 30 33 | 28 | 18 20 21 27 29 29 30 30 | 29 |
| Búa | 20 20 20 26 27 27 28 28 | 27 | 20 21 25 25 27 28 28 28 | 27 |
| Cuộn len | 18 18 20 20 24 24 25 25 | 24 | 18 18 20 23 25 25 26 27 | 25 |
| Găng đấm | 18 22 28 28 29 29 31 34 | 29 | 18 20 29 30 30 30 30 31 | 30 |

Đủ chín đội hình theo chặng; roster quái lẻ và luật boss/threat giữ nguyên. Trung vị chưa đo được cảm giác đấu Mẹ/Biến dị ở đợt 30–40; vẫn cần chơi tay. Chương 1/chương 2 không đổi bảng tỷ lệ thắng.

## Log và giới hạn kiểm tra

Log cục bộ (gitignore): `artifacts/mode15-before.log`, `mode15-expedition-before.log`, `mode15-tests.log`, `mode15-balance-final.log`, `mode15-balance-diff.txt`. Probe dùng dữ liệu riêng, không preferences thật. Full script chạy một lần: Python58/i18n5 đạt, C#3789 đạt/8FAIL; các FAIL đã được sửa hoặc kiểm lại đúng môi trường (ToolingTests registry ngoài sandbox), tổng bộ C#3797 check. Các bộ kiểm lại:314+24+118 check đạt (có phần chung).

WPF:1756 đạt/1FAIL `The backdrop is under the strip`; tái hiện cùng lỗi trên checkout HEAD sạch720a018. Thử đợi dispatcher cũng không khắc phục nên đã khôi phục test gốc, không tắt assertion hay sửa UI ngoài phạm vi. Build Release0error,7warning trong file không sửa. Chưa coi bộ kiểm tra tích hợp là sạch; việc z-order cần xử lý riêng. Kết quả đóng gói/smoke/perf ở Issue_fixxed#70.
