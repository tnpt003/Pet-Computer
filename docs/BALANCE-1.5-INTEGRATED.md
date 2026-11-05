# Đối chứng bản tích hợp 1.5 — 2026-11-06

**Mô phỏng, không phải chơi thật.** Đối chứng gameplay `0172091` (Slime Nén mới đã hoàn tất), bản sau ghép thêm hai boss và sửa các lỗi chuẩn hóa. Cả hai dùng cùng 60 seed cố định cho toàn chương 2, đủ bốn vũ khí, Easy/Normal/Hard và các bot đang có. Chapter 1 giữ mẫu 20; Vô tận vẫn 8 lượt/20 phút. Mọi tỷ lệ yêu cầu/ngưỡng chênh giữ nguyên.

Checkout đối chứng `artifacts/standardize-baseline`: code/data giữ `0172091`, chỉ đổi cùng cỡ mẫu trong test harness. Log `artifacts/standardize-before-60.log` và `artifacts/standardize-balance-60.log`. Bảng lịch sử 20 seed/nhánh riêng không so trực tiếp số lượt thắng với bảng này.

## Những hàng chính đã đổi

Bot simple, thời gian/HP chỉ tính các lượt thắng. Build được mang qua các màn nên thay màn trước có thể đổi kết quả màn sau.

| Màn / vũ khí / mức | Trước: thắng; giây; HP | Tích hợp: thắng; giây; HP |
|---|---|---|
| Đầm nhầy / kiếm / Easy | 60/60; 70; 90% | 60/60; 72; 93% |
| Đầm nhầy / kiếm / Normal | 60/60; 82; 80% | 60/60; 86; 81% |
| Đầm nhầy / kiếm / Hard | 44/60; 93; 54% | 43/60; 97; 56% |
| Đầm nhầy / búa / Easy | 60/60; 73; 90% | 60/60; 75; 93% |
| Đầm nhầy / búa / Normal | 60/60; 88; 75% | 60/60; 92; 78% |
| Đầm nhầy / búa / Hard | 40/60; 102; 46% | 36/60; 111; 50% |
| Đầm nhầy / len / Hard | 58/60; 113; 82% | 52/60; 122; 70% |
| Đầm nhầy / găng / Hard | 50/60; 90; 57% | 53/60; 94; 65% |
| Slime Nén / kiếm / Hard | 28/60; 128; 70% | 29/60; 127; 70% |
| Slime Nén / búa / Hard | 36/60; 133; 64% | 38/60; 132; 61% |
| Slime Nén / găng / Hard | 40/60; 117; 66% | 42/60; 117; 64% |

Không kết luận mọi vũ khí/mức đều khó hơn: Slime Mẹ làm kiếm/búa/len Hard thắng ít hơn, nhưng găng thắng nhiều hơn. Bot chưa tự thả đè và kéo tay kéo Slime Nén; đây là mô hình hạn chế, không được gọi là đánh giá đầy đủ kỹ năng người chơi.

## Các hàng sát ngưỡng

- Thư mục ma, len Normal: **42/60 = 70% ở cả hai bản**, đạt tối thiểu 60% người dùng duyệt. Vẫn yếu hơn kiếm, giữ nguyên luật cờ hiệu.
- Thư mục ma, len Hard: đối chứng **15/60 = 25%**, bản tích hợp **14/60 ≈23%**; kiếm cùng mẫu **42/60 =70%**. Chênh bản tích hợp khoảng 46,7 điểm, đạt giới hạn 50 điểm đã duyệt nhưng còn sát. 20 seed từng cho len 25%/kiếm 80% và FAIL; đã tăng cỡ mẫu cho toàn chương, không nới giới hạn để ép đạt.
- Đầm nhầy len Normal: vẫn thắng 60/60 nhưng HP còn 94% →90%; không dùng HP khi thắng một mình để suy ra tỷ lệ khó.
- Tất cả ngưỡng AdventureBalanceTests và EndlessMutationTests đạt trong phép đo chốt này. Gợi ý kỹ năng Thư mục ma vẫn `laser/push/shield`; thử thay push bằng bomb làm chênh kiếm/búa tệ hơn nên đã bỏ, không đưa vào bản tích hợp.

## Vô tận

| Vũ khí | Trung vị đợt trước | Trung vị đợt tích hợp |
|---|---|---|
| Kiếm | 29 | 29 |
| Búa | 27 | 27 |
| Len | 25 | 25 |
| Găng | 29 | 29 |

Dãy búa có thay đổi dù trung vị giữ 27; không coi trung vị bằng nhau là toàn bộ gameplay không đổi. Nhiều lượt chết trước đợt 30, nên không đủ chứng cứ để kết luận riêng Vua biến dị cân bằng. Phải thử trận riêng và trận Vô tận thật.

## Kiểm tra tích hợp và nghiệm thu

Kết quả full test, artifact, smoke và perf chốt ghi ở PROGRESS.md và STANDARDIZATION-1.5.md. Không dùng kết quả nhánh riêng làm chứng cứ cho main mới. Còn chơi tay ba boss, đủ bốn vũ khí, cửa sổ/DPI/màn phụ và nhịp khựng trong trận đông; chưa nghiệm thu release 1.5.0.

Bản chốt ac93f41: Python 58 + 5, C# 3170, WPF 1418 đạt, publish sạch và smoke 3/3 PASS. Perf crowd-late 34 s: FPS trung vị 51,8, thấp nhất 44,7, gap p95 62 ms/max 128 ms, tối đa 20 quái. Dưới mục tiêu ~57; cần profile/đo dài hơn trước M30. Không so trực tiếp với 59,5 FPS của nhánh riêng (khác source và lượng quái).
