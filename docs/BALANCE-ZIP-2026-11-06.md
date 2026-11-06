# Slime Nén sau chơi thử — đối chứng 06/11, Issue #56–#57

## Kết quả mới nhất — sửa lỗi #57, HP 1100

Sau sửa file mech/bom còn sót, homing quét toàn thân và visual ZIP riêng: C# đầy đủ **3451 đạt**, WPF **1433 đạt**, Python **58 + 5 đạt**. Chạy lại riêng ZIP/slime bay sau các chỉnh cuối: **432 đạt**; ba suite cân bằng **293 đạt**. Không nới ngưỡng hoặc hạ HP; rulesVersion màn/Endless/Bãi rác = **7/15/8**.

Đối chứng trước là bản đã publish `8cd669f`, log `artifacts/zip-publish-tests.log`; sau là `artifacts/zip57-balance-final.log`. Cùng 60 seed chương 2 và 40 seed Viễn chinh, giữ bot/đường đi/build/ngưỡng. Luật truy đuổi và bố trí theo seed đã đổi, nên đây là kết quả toàn bản sửa, không gán riêng mức cải thiện cho một thay đổi.

| Vũ khí | Độ khó | Thắng trước → sau | Giây thắng trước → sau | HP còn trước → sau |
|---|---|---:|---:|---:|
| sword | Normal | 51 → 59/60 | 138 → 139 | 80% → 80% |
| hammer | Normal | 51 → 60/60 | 157 → 156 | 79% → 78% |
| yarn | Normal | 43 → 58/60 | 158 → 157 | 77% → 77% |
| glove | Normal | 52 → 60/60 | 131 → 131 | 84% → 84% |
| sword | Hard | 32 → 49/60 | 157 → 153 | 69% → 73% |
| hammer | Hard | 33 → 40/60 | 180 → 180 | 76% → 76% |
| yarn | Hard | 21 → 39/60 | 200 → 190 | 67% → 74% |
| glove | Hard | 39 → 51/60 | 143 → 141 | 80% → 76% |

Ba ngưỡng từng FAIL nay đạt: Cuộn len boss Normal **58/60 (97%) ≥80%**, Bãi rác tham lam Normal Kiếm **20/40 ≥20**, Cuộn len **22/40 ≥20**. Búa **21/40 ≥18**, Găng **30/40 ≥20**. Kiếm Viễn chinh còn sát ngưỡng; cần chơi thật trước khi coi là cân bằng cuối. Probe Kiếm thường Normal 4/4. Chưa chơi thật, thử DPI/multi-monitor hoặc đo perf boss mới; đã xem render sáng/tối của từng đòn và bố trí file/đích tránh boss.

## Lịch sử #56 và lượt thử HP 1000


Đây là mô phỏng, chưa chơi thật. **Cân bằng chưa đạt:** ba ngưỡng còn FAIL ở bản HP 1100. Người dùng đã yêu cầu publish bản này để chơi thử; không đổi ngưỡng test. Không thay ngưỡng test.

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

## Thử HP 1000 đã được duyệt

Người dùng duyệt thử **1100 → 1000** ngày 06/11. Chỉ đổi `enemies[zip].maxHp`, giữ nguyên mech, bot, seed và ngưỡng. Chạy lại ba suite `AdventureBalanceTests,ExpeditionTests,ZipProbeTests`: **289 check đạt, 4 FAIL**, log `artifacts/zip-hp1000-measure.log`.

| Tiêu chí | HP 1100 | HP 1000 | Ngưỡng |
|---|---:|---:|---:|
| Boss Normal, Cuộn len, 60 seed | 43/60 (72%) | 47/60 (78%) | ≥48/60 (80%) |
| Bãi rác tham lam Normal, Kiếm | 15/40 | 15/40 | ≥20/40 |
| Bãi rác tham lam Normal, Búa | 19/40 | 17/40 | ≥18/40 |
| Bãi rác tham lam Normal, Cuộn len | 13/40 | 13/40 | ≥20/40 |

Probe boss riêng Kiếm thường Normal vẫn đạt 4/4. HP thấp hơn không bảo đảm mọi seed tốt hơn: thời điểm đổi phase và dùng kỹ năng thay đổi. Đây vẫn là mô phỏng, chưa phải chơi thật.

**Kết luận lượt thử:** chưa chốt HP 1000 vì vẫn thiếu một thắng ở boss Cuộn len, không cải thiện hai ngưỡng Viễn chinh cũ và thêm một FAIL Búa. Đã trả đúng dòng dữ liệu về **1100**; `git diff -- src/DesktopPet/Assets/battle.json` trống. Ở lượt thử HP 1000 không thay ngưỡng, không publish hoặc thay mèo đang chạy. Việc tiếp theo là phân loại các seed thua ở từng màn/mech của tuyến Bãi rác trước khi đề xuất chỉnh thêm; không suy ra chỉ cần tiếp tục hạ HP boss.

## Kiểm chứng kỹ thuật

- Python: 58 hành vi + 5 i18n đạt ở lượt đầy đủ; i18n chạy lại sau sửa Sổ tay đạt 5/5.
- C#: lượt đầy đủ sau sửa code 89 suite, 3222 đạt/1 FAIL (`zip-csharp-verified.log`); các suite tính năng đạt. Sau sửa mô hình bot, chỉ chạy lại ba suite cân bằng: 290 đạt/3 FAIL như trên.
- WPF: 1431 check đạt (`zip-wpf-verified.log`); đã mở ảnh ổ khóa, ba cây thư mục và cảnh báo lăn trên nền sáng/tối. Chưa chơi trận thật, thử DPI hoặc nhiều màn hình.
- Sau khi xem kết quả thử, người dùng yêu cầu publish. Đã đóng gói sạch source `8cd669f` (code `29d83ab`), HP 1100; C# 3246 đạt/3 FAIL cân bằng, Python 58 + 5 đạt, WPF 1431 đạt; smoke 3/3 PASS, mở lại mèo. Artifact `artifacts/DesktopPet-1.4.4-8cd669f-win-x64.zip`. Giữ ba FAIL cân bằng để xử lý tiếp; không suy ra bản này đạt nghiệm thu cân bằng hoặc chơi thật.
