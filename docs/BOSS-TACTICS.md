# Slime Mẹ và Vua Slime biến dị — cơ chế nâng cấp

**Cập nhật 06/11:** phần Vua biến dị dưới đây là luật cũ cho cấu hình legacy, không còn dùng cho boss gốc `mutant_king`. Slime biến dị dùng `mutant: true`, luật ba phase và sân nhầy riêng ở [MUTANT-SLIME.md](MUTANT-SLIME.md). Slime Mẹ và các cấu hình opt-in cũ giữ nguyên.

Thiết kế người dùng duyệt 2026-11-05, triển khai trên nhánh `feature/mother-mutant-mechanics` từ `7e423b5`. Không chứa thay đổi Slime Nén ở phiên khác. Không có mầm biến dị hoặc slime con biến dị.

**Tích hợp 06/11:** đã ghép với Slime Nén mới vào main. Phần mô tả nhánh riêng ở trên là lịch sử; luật chung và kết quả bản tích hợp theo [STANDARDIZATION-1.5.md](STANDARDIZATION-1.5.md).

## Slime Mẹ — ba phase và Thanh dỗ (#67, 06/11)

Luật mới đầy đủ và số liệu ở [MOTHER-SLIME.md](MOTHER-SLIME.md). `mother:true` thay bộ gọi con/nhảy/lao/Lứa cuối cũ: ba phase, chín đòn theo seed, Quấn khăn; ngưỡng66% Trứng treo,33% Trả con về nôi, phase3 chuyển HP thành Thanh dỗ33/66/100. Đòn air chỉ gây mất máu khi thực sự chạm mèo bế/bay, không ép thả/choáng/đổi vận tốc. Dây chỉ đứt bằng Kéo cắt hoặc hạ con giữ dây. Không bắt buộc Kéo cắt để giải mech. Chuyển mech giải và miễn debuff; ổ/con đứng yên trong mech. Giữ Python và physics cửa sổ.

Các custom boss có brood/bossTactics nhưng không bật mother giữ controller cũ để tương thích. Phần bất biến chung phía dưới mô tả controller cũ; ngoại lệ mother/mutant/zip mới lấy từ tài liệu riêng và APPROVED-EXCEPTIONS.

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
