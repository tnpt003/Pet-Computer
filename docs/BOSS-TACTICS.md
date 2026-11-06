# Slime Mẹ và Vua Slime biến dị — cơ chế nâng cấp

Thiết kế người dùng duyệt 2026-11-05, triển khai trên nhánh `feature/mother-mutant-mechanics` từ `7e423b5`. Không chứa thay đổi Slime Nén ở phiên khác. Không có mầm biến dị hoặc slime con biến dị.

**Tích hợp 06/11:** đã ghép với Slime Nén mới vào main. Phần mô tả nhánh riêng ở trên là lịch sử; luật chung và kết quả bản tích hợp theo [STANDARDIZATION-1.5.md](STANDARDIZATION-1.5.md).

## Slime Mẹ: tổ nhầy và đường tiếp tế

- Giữ HP 650, giới hạn 6 con và tốc độ chuyển máu 20 HP/s. `brood.every` 10 giây, nổi giận 6,5 giây: đồng hồ chỉ chạy lúc boss không đang dùng chiêu đặc biệt, choáng hay bật khiên. Vòng báo ổ đúng vị trí sắp xuất hiện.
- Ổ ủ là mục tiêu có 80 HP, hình ba trứng hồng, thanh máu và số giây. Tối đa 2 ổ; trên 60% máu đặt 1 ổ, từ 60% đặt 2 ổ hai phía, nở lệch 1,2 giây. Ổ xuất hiện xong mới đếm 2,5 giây ủ. Bị choáng/Kẹp gắp thì ngừng ủ. Phá ổ ngăn bầy xuất hiện.
- Một ổ nở tối đa 3 slime hợp thể, vẫn chịu trần 6 con. Vỏ biến mất không tính là hạ quái; để lại nhầy 5 giây, bán kính 65 DIP, tốc độ mèo dưới sàn ×0,55. Quạt làm khô như vũng thường.
- Mẹ gọi con do mình sinh có ≤50% máu quay về. Khổng lồ hợp thành vẫn tự quay về như trước. Mẹ đầy máu thì con đứng chờ, không tiêu hao máu vô ích. Mẹ chết thì con còn sống trở lại đánh bình thường.
- Con đang về có ≤50% máu, trong 360 DIP: mẹ nối dây cứu trong tối đa 6 giây, hồi dây 14 giây. Con nhận một nửa sát thương, mẹ đứng giữ dây. Dây hồng cùng đường cong với hit test; Kéo cắt hoặc kéo/ném mèo xuyên dây ở ≥300 DIP/s cắt được. Đứt dây: cả hai choáng 2 giây, con ngừng về và mẹ hoãn lứa sau ít nhất 4 giây. Hạ con khi đang nối dây cũng khiến mẹ khựng và hoãn đẻ.
- Con đang về đứng sát mẹ che phía mình: mẹ nhận ×0,6 sát thương từ hướng đó, không miễn đòn. Đổi phía hoặc hạ con để phá che chắn.
- Mẹ luân phiên gọi con về và vỗ bụng gọi tối đa 3 con đang tự do nhảy vào vùng đã báo. Bầy bay 0,7 giây, đáp quanh tâm báo ±65 DIP, vùng trúng 42 DIP, gây ×0,7 sát thương của từng con; đáp xong nằm choáng 1,2 giây. Bế mèo né được; choáng/Kẹp gắp ngắt cú nhảy.
- Mỗi ổ/con bị hạ tích 1 giận. Đủ 3 thì chiêu kế là cú lao: báo 1,1 giây, lao tối đa 220 DIP trong 0,45 giây; chỉ trúng một lần, đẩy mèo dưới sàn 80 DIP trong 0,25 giây nếu đòn xuyên khiên và mèo không miễn đẩy. Né hụt thì mẹ vẫn phải nghỉ.
- ≤20% máu: một lần duy nhất mất 6% máu tối đa (không tự chết) để đặt lứa cuối, vẫn chịu giới hạn ổ/con. Hết chỗ thì chờ; chỉ đánh dấu đã dùng và trừ máu khi đặt được ít nhất một ổ.

## Vua biến dị: cơ thể bất ổn và nhầy chiếm sàn

- Giữ HP 1000, các pha khiên và quân tinh anh/xích/trống hiện có. Khi khiên tắt, chuỗi riêng lần lượt: tay quét → mưa → sóng. Từ 60% máu bổ sung hút nhầy → phình nổ vào chuỗi.
- Tay quét: báo phía tay vươn, đánh vùng tâm cách vua 125 DIP, bán kính 125, sát thương ×0,85. Phình nổ: vòng bán kính 160, sát thương ×1,1 và đẩy 65 DIP nếu xuyên khiên/không miễn đẩy.
- Mưa hai nhịp: cả hai đợt giọt hiện vòng từ đầu, vị trí cố định quanh nơi mèo đứng lúc vua bắt đầu chiêu; nhịp sau lệch nửa `rain.spacing` và rơi chậm hơn 0,8 giây. Không bám theo mèo sau khi đã báo. Giữ số liệu sát thương, kích thước và thời gian vũng của `rain`.
- Sóng sát sàn: tối đa 7 điểm cách nhau 65 DIP, lan theo hướng báo trước; mỗi điểm có vòng riêng, trễ 0,18 giây so với điểm trước, vùng trúng 38 DIP, sát thương ×0,65. Không quay đầu, không tạo thêm vũng.
- Hút nhầy: dừng 2,4 giây, nối dòng vàng từ vũng của chính vua trong 650 DIP. Kéo cắt/kéo mèo cắt dòng dẫn ngăn vũng đó bị hút trong lần này. Mỗi vũng hút thành công tăng giáp bằng 2,5% HP tối đa, tổng không quá 10%; giáp hấp thụ lượng sát thương hữu hạn, không phải pha bất tử. Không hút vũng Slime Mẹ hoặc slime khác.
- Mỗi chiêu xong nghỉ 1,25 giây; sau mỗi 4 chiêu, quá tải vương miện: bỏ giáp tái tạo, nghỉ 3,5 giây và nhận ×1,4 sát thương. Pha khiên gọi quân vẫn giữ luật phải hạ quân, không tự bỏ qua.
- ≤25% máu ghép từng cặp: mưa → sóng, các chiêu khác → phình nổ. Chiêu thứ hai vẫn báo trước đầy đủ, sau mỗi cặp bắt buộc nghỉ. Khi nổi giận giảm thời gian chờ giữa các lượt xuống ×0,75.

## Dữ liệu, bất biến và kiểm tra

`EnemyType.bossTactics` là opt-in, không đổi boss chương 1 hoặc Slime Nén. Mặc định C#: `every=5` (2–30), `warning=1.1` (0,7–3), `opening=3.5` (2–8), `nestHp=80` (10–200), `incubation=2.5` (2–10). Hình học tune được: `sweepReach=125` (30–400), `sweepRadius=125` (15–300), `burstRadius=160` (30–400), `jumpRadius=42` (15–100), `waveRadius=38` (15–100), `waveSpacing=65` (30–150). Chỉ dùng cho boss có đúng một trong `brood`/`rain`. `BossZone`/`BossWaveZone`/`BossRainZone` dùng chung cho simulation, cảnh báo và bot; các hằng luật còn lại trong PetBattle.BossTactics.cs.

Chiêu mới không gây sát thương lên mèo đang cầm/bay. Giữ Khiên, Nộ, ân hạn hạ mèo, sleep/wake/drag/throw bên ngoài trận và fallback Python. Toàn bộ đồng hồ dùng thời gian simulation; Pause/menu/hướng dẫn đứng hình không chạy simulation. Chụp Màn Hình đóng băng cả chiêu và đạn; choáng ngắt chiêu đang báo. Đổi vùng màn hình ánh xạ cả điểm đầu và điểm đích. Kết thúc trận dọn chiêu/dây/đạn. Không thêm RNG, dependency hoặc ảnh sprite.

Kỷ lục bản tích hợp: Đầm nhầy chương 2 r4, Slime Nén r4, Vô tận r12, tuyến Bãi rác r5 (giữ chuyến luật 1/2/3/4). Nhánh hai boss riêng trước đó là Vô tận 11/tuyến 2 r4. Chương 1 và tuyến Slime giữ nguyên.

Test: `BossTacticsTests` kiểm tra ổ/giới hạn nở, choáng ổ, hồi máu, cắt dây, lứa cuối, cú lao, mưa hai nhịp, hút/cắt dòng dẫn, giới hạn giáp, ngắt chiêu, né khi bế, đổi vùng và cặp chiêu cuối. `BattleBossM18Tests` giữ cơ chế cũ khi không bật opt-in. `BroodVisualChecks` xuất ảnh trứng/dây/tay quét/giáp và mưa.

## Bổ sung #61 — đội hình và song phong tinh anh cho vua ngoài chương1 (06/11)

Slime Nén phase1 Cuộn đóng gói lăn3 lượt: lần đầu báo1,1 s, hai lượt sau báo0,6 s. Trong gồng cập nhật hướng theo X mèo; khi lao chốt hướng, không quay giữa lượt. Giữ tốc độ360/tầm460/damage12 cơ sở, mỗi lượt chỉ trúng một lần; hết chỗ ở mép mới đổi sang hướng có chỗ. Đủ3 mới hồi phục; choáng/vào mech hủy chuỗi. Timer công khai ZipSeconds/ZipLeft đồng bộ gồng lăn thay vì đứng ở giá trị lúc mở đòn. King và mutant_king bật kingFormations và rallyTargets2; king_c1 giữ nguyên. Mỗi mech chọn ngẫu nhiên theo seed một bộ3 khác loại từ Pháo đài/Bạt nhún/Đội trống/Pháo kích (bốn tổ hợp3-trong-4); tổng9–10 quân, không timer/bầy nhập/bomber chồng hồi máu, báo0,6 s và xuất hiện đủ cùng frame; không chờ maxAlive, giới hạn SummonCap10. Quân theo vai trò hỗ trợ/cản/đánh/áp sát, không tạo đợt bù liên tục. Đủ hạ quân của phase thì khiên hạ. King thường nay có3 pha khiên75/50/25%; Vua biến dị giữ ngưỡng cũ, crown links/rain/tactics. Mỗi buff chọn2 con chưa tinh anh khác nhau trong quân phase, báo hai đường vương miện; mục tiêu chết thì chọn bù hợp lệ, ít hơn2 thì buff số còn lại; đẩy/choáng ngắt cả2. Giữ nhịp và sức buff cũ; Pause đóng băng. HP không đổi: ZIP1100/king480/mutant1000. Records ZIP11/Endless19/Bãi rác12; tuyến Viễn chinh chương1 giữ8.
