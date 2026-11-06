# Slime Nén sau chơi thử — đối chứng 06/11, Issue #56–#62

## Kết quả mới nhất #62 — Tràn bộ nhớ toàn chiều cao, bản chơi thử còn1 FAIL

Publish sạch source `6ce2586`, smoke **3/3 PASS**, mở lại mèo; ZIP `artifacts/DesktopPet-1.4.4-6ce2586-win-x64.zip`. Perf crowd-late19 s median57/min46,8 FPS, gap p95/max141 ms, CPU avg3,27%, 14,8 quái trung bình/max17. Cảnh đo không có Overflow; chưa kết luận chi phí overlay mới hoặc cải thiện so với lượt trước. Logs zip62-publish.log/zip62-smoke.log/zip62-perf.log.

Người dùng yêu cầu sửa rồi publish, sau đó xác nhận thiết kế #62 và nhắc publish; gộp cả source#61 chưa phát hành. Không coi việc publish là nghiệm thu cân bằng. Giữ HP1100, warning1/strike2,2/recover1,6 s, damage26/13; không nới ngưỡng.

Đối chứng sourcebeecc78 (#61, m61-final-balance.log) → #62 (zip62-verified.log), cùng bot cân bằng chính/seed/build/vũ khí/đường đi. Chỉ bot probe dodge=true được cập nhật để bế ngang vào gap thay vì né bằng độ cao; không tác động bảng dưới. Full zip62-full-tests.log: Python58+i18n5, C#3686 đạt/2 FAIL. Sau cập nhật careful bot: probe và logic đạt, đo5 suite780 đạt/1 FAIL (Len16/40). WPF1454 đạt, đã mở ảnh full-warning/filling/peak sáng/tối. Chưa chơi trận thật/DPI/multi-monitor; perf overlay thật còn cần xem.

| Vũ khí | Độ khó | Thắng #61 → #62 | Giây #61 → #62 | HP #61 → #62 |
|---|---|---:|---:|---:|
| sword | Normal | 59/60 → 57/60 | 132 → 134 | 78% → 78% |
| sword | Hard | 48/60 → 42/60 | 147 → 155 | 64% → 62% |
| hammer | Normal | 57/60 → 57/60 | 152 → 156 | 77% → 69% |
| hammer | Hard | 46/60 → 40/60 | 173 → 182 | 66% → 57% |
| yarn | Normal | 57/60 → 55/60 | 154 → 161 | 79% → 70% |
| yarn | Hard | 38/60 → 30/60 | 185 → 191 | 63% → 56% |
| glove | Normal | 60/60 → 59/60 | 124 → 127 | 84% → 82% |
| glove | Hard | 55/60 → 52/60 | 135 → 139 | 72% → 65% |

Bãi rác tham lam Normal: sword 25/40→24/40, hammer 19/40→18/40, yarn 19/40→16/40, glove 26/40→22/40. **Cuộn len còn FAIL16/40 <20**, các ngưỡng khác đạt. Đây là mô phỏng với bot simple, không phải người chơi. Giữ nguyên kết quả FAIL để theo dõi chơi thử. RecordsZIP12/Endless20/Bãi rác13, tuyến chính8.

## Lịch sử #61 — lăn3 lượt/ba đội hình Vua, còn1 FAIL

Baseline source9fcbed2 (`zip60-balance-verified.log`) → #61 (`m61-final-balance.log`), cùng bot/seed/build/vũ khí/đường đi/ngưỡng/HP. Runtime mới gồm ZIP lăn3 lượt, timer gồng công khai được sửa, King/mutant ba đội hình/buff2; không gán mọi chênh lệch cho riêng một thay đổi. Không đổi bot để vượt ngưỡng.

| Vũ khí | Độ khó | Thắng trước → sau | Giây trước → sau | HP trước → sau |
|---|---|---:|---:|---:|
| sword | Normal | 60/60 → 59/60 | 133 → 132 | 77% → 78% |
| sword | Hard | 47/60 → 48/60 | 148 → 147 | 66% → 64% |
| hammer | Normal | 60/60 → 57/60 | 151 → 152 | 76% → 77% |
| hammer | Hard | 46/60 → 46/60 | 176 → 173 | 70% → 66% |
| yarn | Normal | 59/60 → 57/60 | 153 → 154 | 75% → 79% |
| yarn | Hard | 40/60 → 38/60 | 186 → 185 | 63% → 63% |
| glove | Normal | 60/60 → 60/60 | 125 → 124 | 84% → 84% |
| glove | Hard | 54/60 → 55/60 | 134 → 135 | 74% → 72% |

Bãi rác tham lam Normal: sword 22/40→25/40, hammer 21/40→19/40, yarn 22/40→19/40, glove 28/40→26/40. **FAIL Cuộn len19/40 <20/40**; các ngưỡng còn lại đạt. Tuyến này dùng bossZIP, không có King/mutant. Đã thử thay bộ hồi máu kép+bomber bằng Pháo đài/Bạt nhún/Đội trống và sửa clock gồng lăn; vẫn19/40. Không đổi damage/tầm/tốc độ/HP/ngưỡng; chưa coi là đạt cân bằng.

Full: Python58+i18n5, C#3630 đạt/9 FAIL cũ; sau xử lý test/dịch, core1013 đạt, build0 lỗi/7 cảnh báo cũ, WPF1450 đạt. Đo cuối4 suite547 đạt/1 FAIL. Chưa publish/smoke/perf bản này; mèo giữ9fcbed2, chờ duyệt phát hành chơi thử dù FAIL hoặc kế hoạch cân lại. Đã xem render hai đường buff sáng/tối, chưa chơi thật/DPI/multi-monitor. RecordsZIP11/Endless19/Bãi rác12; main route giữ8 vì vẫnking_c1.

## Lịch sử #60 — phạt timeout toàn sân, HP 1100

Publish sạch source `9fcbed2`, smoke3/3 PASS, mở lại mèo; ZIP `artifacts/DesktopPet-1.4.4-9fcbed2-win-x64.zip`. Perf `crowd-late`19 s: median48,6/min35,3 FPS, gap p95/max133 ms, CPU avg3,36%, 14,9 quái trung bình. Chưa đạt~60; đây không phải cảnh nổ ZIP và không có baseline perf cùng lượt, không kết luận là hồi quy của #60. Cần profile và thử burst thật; `zip60-perf.log`.

Lượt đầy đủ với bot cũ: C#3510 đạt/3 FAIL (Búa boss Normal72% <80%, Búa Bãi rác16/40 <18, Kiếm probe0/4). Không chỉnh HP/damage/clock/ngưỡng để né FAIL. Bot cũ né nổ bằng bế cao và đi bộ giữa tay sàn; đã đổi bot để bế chuyển giữa tay sàn ở700 DIP/s rồi đặt xuống mới đánh, không chờ bom bằng độ cao. Bot giả lập đáp ngay khi bị forced fall; đây là giới hạn mô phỏng, WPF có test rơi thật và đếm stun sau đáp.

Đối chứng **cùng bot mới** trên baseline source048862f (checkout riêng trong artifacts; bỏ duy nhất nhánh ZipForcedFall vì baseline chưa có API) và #60. Giữ seed/build/đường đi/vũ khí/ngưỡng/HP. Logs zip60-baseline-balance.log + zip60-baseline-expedition.log; sau zip60-balance-verified.log. Không so mức cải thiện của bot với bảng cũ như tác dụng của gameplay.

| Vũ khí | Độ khó | Thắng baseline → #60 | Giây baseline → #60 | HP baseline → #60 |
|---|---|---:|---:|---:|
| sword | Normal | 60/60 → 60/60 | 133 → 133 | 77% → 77% |
| sword | Hard | 47/60 → 47/60 | 148 → 148 | 66% → 66% |
| hammer | Normal | 60/60 → 60/60 | 151 → 151 | 76% → 76% |
| hammer | Hard | 46/60 → 46/60 | 176 → 176 | 70% → 70% |
| yarn | Normal | 59/60 → 59/60 | 153 → 153 | 75% → 75% |
| yarn | Hard | 40/60 → 40/60 | 186 → 186 | 63% → 63% |
| glove | Normal | 60/60 → 60/60 | 125 → 125 | 84% → 84% |
| glove | Hard | 54/60 → 54/60 | 134 → 134 | 74% → 74% |

Bãi rác tham lam Normal, cùng bot: sword 22/40→22/40, hammer 21/40→21/40, yarn 22/40→22/40, glove 28/40→28/40. Mọi ngưỡng đạt; cần tiếp tục chơi thử vì bot bế/đặt chính xác hơn người.

Python58+i18n5, ZIP436, WPF1448 đạt; đo lại cân bằng3 suite293 đạt. Đã xem render sáng/tối, chưa chơi thật/DPI/multi-monitor. Runtime C# và bridge window, không Python. Records10/18/11.

## Lịch sử #59 — cây mech cuối/đẩy riêng, HP 1100

Publish sạch source `048862f`, smoke **3/3 PASS**, đã mở lại mèo. ZIP `artifacts/DesktopPet-1.4.4-048862f-win-x64.zip`. Full C# 3484 đạt/6 FAIL test và môi trường, đã xử lý rồi kiểm lại suite ZIP/record 562 + Tooling24 đạt; Python58+i18n5, WPF1439 đạt. Chi tiết Issue_fixxed #59.

Đối chứng source 3b3682a (`zip58-full-tests.log`) với bản #59 (`zip59-full-tests.log`): giữ HP, bot, seed, đường đi và ngưỡng. Đây là mô phỏng, chưa chơi thật.

| Vũ khí | Độ khó | Thắng trước → sau | Giây trước → sau | HP trước → sau |
|---|---|---:|---:|---:|
| sword | Normal | 59/60 → 60/60 | 139 → 141 | 81% → 79% |
| sword | Hard | 48/60 → 47/60 | 153 → 157 | 74% → 71% |
| hammer | Normal | 60/60 → 59/60 | 156 → 160 | 77% → 79% |
| hammer | Hard | 43/60 → 46/60 | 179 → 186 | 77% → 76% |
| yarn | Normal | 58/60 → 59/60 | 157 → 161 | 79% → 77% |
| yarn | Hard | 41/60 → 40/60 | 189 → 194 | 72% → 70% |
| glove | Normal | 60/60 → 60/60 | 132 → 133 | 87% → 85% |
| glove | Hard | 53/60 → 53/60 | 141 → 142 | 76% → 78% |

Mọi ngưỡng cân bằng đạt. Bãi rác tham lam Normal: Kiếm 20→22/40 (≥20), Búa 22→20/40 (≥18), Cuộn len 23→22/40 (≥20), Găng 29→28/40 (≥20). Búa/Cuộn len còn gần ngưỡng, cần chơi thử. Records màn/Endless/Bãi rác 9/17/10.

## Lịch sử #58 — đai quét/tự phá khóa #58, HP 1100

Đã publish sạch source `3b3682a`, smoke **3/3 PASS**, mở lại mèo; ZIP `artifacts/DesktopPet-1.4.4-3b3682a-win-x64.zip`. Log `artifacts/zip58-publish-final.log`, `artifacts/zip58-smoke.log`.

C# đầy đủ **3467 đạt**, Python **58 + 5 đạt**, WPF **1437 đạt**; ZIP mục tiêu cuối **392 đạt** (gồm đổi vùng trận và tọa độ âm giữa đai đang bay, giữ tiến độ/không hit trễ) sau chỉnh test tự phá khóa với RageHitGain >0. Bộ đầy đủ `artifacts/zip58-full-tests.log`; WPF cuối `zip58-ui-verified.log`, ZIP cuối `zip58-area-tests.log`. Đã xem render gồng, hai vị trí đầu quét, thu đai và nhát tự đánh khóa trên nền sáng/tối; chưa chơi thật/DPI/multi-monitor.

Đối chứng trước là source đã publish `57c3055` (`artifacts/zip57-balance-final.log`), sau là `zip58-full-tests.log`. Cùng 60 seed chương 2 và 40 seed Viễn chinh, không đổi bot/build/đường đi/ngưỡng/HP; records màn/Endless/Bãi rác = 8/16/9. Đây là mô phỏng, không phải chơi thật.

| Vũ khí | Độ khó | Thắng trước → sau | Giây thắng trước → sau | HP còn trước → sau |
|---|---|---:|---:|---:|
| sword | Normal | 59 → 59/60 | 139 → 139 | 80% → 81% |
| hammer | Normal | 60 → 60/60 | 156 → 156 | 78% → 77% |
| yarn | Normal | 58 → 58/60 | 157 → 157 | 77% → 79% |
| glove | Normal | 60 → 60/60 | 131 → 132 | 84% → 87% |
| sword | Hard | 49 → 48/60 | 153 → 153 | 73% → 74% |
| hammer | Hard | 40 → 43/60 | 180 → 179 | 76% → 77% |
| yarn | Hard | 39 → 41/60 | 190 → 189 | 74% → 72% |
| glove | Hard | 51 → 53/60 | 141 → 141 | 76% → 76% |

Mọi ngưỡng tự động đạt. Bãi rác tham lam Normal: Kiếm **20/40** vẫn sát ngưỡng ≥20, Búa **22/40** ≥18, Cuộn len **23/40** ≥20, Găng **29/40** ≥20. Giữ rõ điểm sát ngưỡng để chơi thử; không nới tiêu chuẩn vì sửa lỗi.


## Kết quả #57, HP 1100

Đã publish sạch `57c3055`, smoke 3/3 PASS, mở lại mèo; ZIP `artifacts/DesktopPet-1.4.4-57c3055-win-x64.zip`. Đây vẫn là bản 1.5 đang chơi thử trong version 1.4.4, chưa nghiệm thu trận thật/DPI/multi-monitor.


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
