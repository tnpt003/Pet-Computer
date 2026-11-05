# Đo cơ chế hai boss — 2026-11-05

Đây là số đo lịch sử của nhánh hai boss riêng. Từ 06/11 đã tích hợp với Slime Nén mới, sửa Nộ/đẩy và bảo toàn gộp vũng thường, bot biết vùng boss mới; không dùng bảng này làm số đo bản hiện tại. Xem STANDARDIZATION-1.5.md và PROGRESS.md.

So với commit nền `7e423b5`, nhánh `feature/mother-mutant-mechanics`. Dùng `AdventureBalanceTests` và `EndlessMutationTests`, cùng seed/bot. **Đây là mô phỏng, không phải chơi thật.** Không nới ngưỡng test để cho bản mới qua.

Ổ mẹ bản chốt: 80 HP ở threat 1 (nhân theo hệ số HP mẹ), 2,5 giây ủ sau khi xuất hiện. Bản thử đầu 40 HP/3,5 giây làm ổ quá dễ bị phá; đã tăng áp lực bằng ổ thay vì tăng HP/sát thương mẹ. Mẹ không mất lượt gọi khi sân không có con phù hợp.

## Đầm nhầy chương 2 — bot simple, 20 seed

| Vũ khí / mức | Trước: thắng, thời gian, HP còn khi thắng | Sau: thắng, thời gian, HP còn khi thắng |
|---|---|---|
| Kiếm / Easy | 20/20, 66 s, 91% | 20/20, 69 s, 93% |
| Kiếm / Normal | 20/20, 78 s, 82% | 20/20, 82 s, 87% |
| Kiếm / Hard | 17/20, 89 s, 57% | 16/20, 94 s, 66% |
| Búa / Easy | 20/20, 69 s, 93% | 20/20, 71 s, 94% |
| Búa / Normal | 20/20, 87 s, 78% | 20/20, 89 s, 81% |
| Búa / Hard | 14/20, 93 s, 45% | 12/20, 103 s, 49% |

Hard thắng ít hơn và trận lâu hơn. Easy/Normal có lượt thắng còn nhiều máu hơn bản cũ: không kết luận boss khó hơn ở mọi mức/vũ khí. HP trung bình chỉ tính các lượt thắng nên không so trực tiếp được với tỷ lệ sống toàn bộ lượt. Cần chơi thật để chỉnh tiếp nhịp bảo vệ/nhảy/đẻ mà không biến trận thành kéo dài vô hạn. Cuộn len Hard từng sát ngưỡng; kiểm tra của bộ mô phỏng vẫn đạt ở bản chốt.

## Vô tận

Mô phỏng 8 lượt, simple/Normal, chặn ở 20 phút. Bản chốt: kiếm trung vị đợt 29 (dãy 15/20/28/29/29/30/30/35), búa 27, len 23, găng 30. Mốc đối chứng luật cũ do suite dựng lại cho kiếm là 30; đây không phải phép đo riêng vua ở đợt 30 vì nhiều lượt chết trước khi gặp vua. Cần thử trực tiếp `docs/examples/battle-m18b.json` để đánh giá bộ chiêu của vua.

Log gốc trong worktree: `balance-before.log`, `boss-balance-final.log`, `boss-full-final.log` (không commit log). Các lượt Viễn chinh được chạy trong bộ đầy đủ; chưa có bảng đối chứng riêng tuyến Bãi rác trên commit nền, không khẳng định tỷ lệ thay đổi của tuyến.

## Kiểm tra và đóng gói

Commit code `9477828`. Python 58 + 5, C# 3093, WPF 1412 đạt. Ảnh render thực tế đã xem: `enemy-boss-tactics.png`, `enemy-rain.png` trên nền sáng/tối; số đếm ổ đã dùng huy hiệu nền tối cho đủ tương phản. Chưa chơi thật, chưa kiểm DPI/multi-monitor thật.

Publish sạch `1.4.4+9477828`, ba smoke đạt (runtime .NET/Python đi kèm, single-instance, thoát sạch cả app/Python). ZIP riêng `artifacts/DesktopPet-1.4.4-9477828-win-x64.zip` trong worktree. Bản vẫn mang số phiên bản project hiện tại, chưa phải release 1.5 hay bản đã gộp Slime Nén.

`scripts/perf.ps1 -Battle crowd-late -Minutes 0.5`: 34 giây frame stats, FPS trung vị 59,5, thấp nhất 25,8; gap p95 91 ms, lớn nhất 140 ms; trung bình 15,3/tối đa 17 quái. Có khựng, không coi là 60 FPS ổn định. Đây là kịch bản crowd-late chung, không phải phép đo riêng mọi chuỗi chiêu hai boss. Log `boss-perf.log`, CSV `artifacts/perf/20261105-235819-boss-tactics.csv`. Thử bằng dữ liệu riêng; mèo ở artifact thư mục gốc được thoát bình thường trước smoke/perf và mở lại sau đó.
