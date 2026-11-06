# Slime Nén — thiết kế lại 06/11

Luật hiện hành của boss `zip`. Người dùng duyệt ngày 2026-11-06 (Issue #54–#56). Cập nhật sau chơi thử: khóa đứng yên, mech đánh khóa/ghép thư mục/tay kéo. Code: `PetBattle.Zip.cs` (giai đoạn, mech), `PetBattle.ZipMoves.cs` (đòn), `PetBattle.ZipDebuff.cs` (debuff), `PetBattle.ZipPuzzles.cs` (mục tiêu và đòn đẩy riêng); vẽ `PetBattleView.Zip.cs`, `PetBattleView.ZipMoves.cs`, `PetRenderer.cs`; test `BattleZipTests`, `BattleZipMoveTests`, `ZipProbeTests`, `ZipVisualChecks`. Số liệu ở `enemies[zip].zip` trong battle.json. Combat thuộc C#, Python không tham gia.

## Ba giai đoạn

| Giai đoạn | Thân | Đòn thường | Mech cuối giai đoạn |
|---|---|---|---|
| 1. Nén chặt (`4 KB`, ×1,1) | Kiện vuông, hai đai, khóa đóng | Kẹp kiện, Bắn khóa kéo, Cuộn đóng gói | 66%: Tháo chốt kiện |
| 2. Giải nén (`2 MB`, ×2) | Khóa hé, giấy bung | Cây thư mục, Tệp truy đuổi, Giải nén lỗi | 33%: Sắp xếp thư mục |
| 3. Quá tải (`9 GB`, ×2,9) | Căng phồng, khóa toác | Tràn bộ nhớ, Ghi đè, Xé archive | 1 HP: Bom nén sinh tử |

## Chọn đòn

- Túi đòn trộn bằng seed trận (`BattleRandom.Zip`). Đủ ba đòn hợp lệ mới trộn lại, không lặp ngay đòn vừa dùng.
- Đòn hợp lệ theo khoảng cách (`minRange`–`maxRange`). Mèo đang bế hoặc bay chỉ gặp đòn có `air`. Chỉ còn đúng đòn vừa dùng hợp lệ thì chờ 2,5 giây mới được lặp.
- Nhịp mỗi đòn: báo trước → khóa → ra đòn → hồi phục. Sau khi khóa, đòn đã báo không đổi hướng, ngoại trừ Tệp truy đuổi bám có giới hạn quay theo bổ sung #57.
- Choáng, Kẹp gắp hoặc khiên mech ngắt đòn ngay và xóa các điểm đang chờ, không có cú đánh trễ.
- Chạm ngưỡng HP thì hủy đòn và vào mech. Sang giai đoạn mới thì túi đòn làm lại.

## Đòn thường

| Đòn | Cách đánh | Cách né |
|---|---|---|
| Kẹp kiện | Trượt tới điểm chốt, hai nửa kiện ép lại | Ra khỏi khe, hoặc nâng mèo cao hơn 90 DIP |
| Bắn khóa kéo (`air`) | Đường ngắm theo mèo, khóa rồi phóng tới điểm chốt và thu về, trúng tối đa 1 lần | Rời điểm chốt sau khi khóa, tránh đường về |
| Cuộn đóng gói | Lăn3 lượt theo X mèo, báo đầu1,1 s/hai lượt sau0,6 s; chốt hướng khi lao, mỗi lượt một làn cảnh báo | Nâng cao hơn thân boss, hoặc ra khỏi dải |
| Cây thư mục | Tối đa 6 nhánh đánh số, nổ theo thứ tự, bỏ ngẫu nhiên 1 nhánh | Đứng vào khoảng giữa các nhánh, hoặc nâng cao hơn 120 DIP |
| Tệp truy đuổi (`air`) | Ba tệp bay cong rồi bám mèo với tốc độ quay tối đa 2,8 rad/s (khoảng 160°/s), giữ tốc độ và tuổi thọ 4 giây | Đổi hướng nhanh hoặc kéo ra khỏi quỹ đạo; bế/bounce không miễn trúng |
| Giải nén lỗi | Ba file giống nhau, một bom ngẫu nhiên; rê chuột mới lộ bom và thời gian còn lại | Soi từng file; bom đã soi giữ hiển thị. Đánh vỡ bom hoặc né vòng; đồng hồ chạy cả khi ẩn |
| Tràn bộ nhớ | Trượt tới điểm chốt, giấy lan từ hai mép, kết thúc bằng cú bung ngoài khoảng vàng | Ở trong khoảng vàng 190 DIP, hoặc nâng cao hơn 170 DIP |
| Ghi đè (`air`) | Đánh dấu 3 vị trí liên tiếp của mèo, kể cả trên cao, rồi đánh lần lượt | Rời điểm đã đánh dấu |
| Xé archive | Thân tách đôi nối bằng khóa kéo, quét ra 380 DIP rồi nhập lại, trúng tối đa 1 lần mỗi lượt | Ra ngoài tầm quét, hoặc nâng cao hơn 120 DIP |

## Ba mech — bản người dùng chốt sau chơi thử 06/11

- **66% Tháo chốt kiện:** boss nhảy ra giữa và giữ khiên. Hai ổ khóa ở hai rìa, cách mép 65 DIP để mèo có chỗ đứng đánh. Đánh khóa sáng **12 lần tổng cộng**, cứ 3 đòn đổi bên (trái → phải → trái → phải). Chỉ nhát vũ khí trực tiếp tính, damage lớn vẫn một lượt; kỹ năng/đòn lan không cộng. Mỗi 2,8 giây boss nhún và kéo căng đai trong 0,8 giây, đầu đai chạy từ boss về rìa phía khóa mở trong 0,35 giây rồi thu về 0,2 giây. Chỉ đầu đai quét thực sự chạm mèo mới gây 8 damage cơ sở và choáng 0,5 giây, tối đa một lần/lượt; đuôi và lượt thu không gây damage. Nhấc cao né được; vùng báo chỉ là vạch sàn nhẹ, không phủ thành tường. Đủ 3 đòn đổi khóa thì ngắt cảnh báo đang chuẩn bị. Đủ 12 mở khiên, sang phase 2 và choáng 2,2 giây, nhận ×1,5 trong khoảng phản công giới hạn 6% HP tối đa. Không tự bỏ qua mech.
- **33% Sắp xếp thư mục:** ba file và ba thư mục đích có ký hiệu tròn/vuông/dấu chéo. Đứng phía đối diện rồi đánh file để đẩy 36 DIP về thư mục cùng ký hiệu; cách đích ≤40 DIP thì ghép xong. File/đích chừa chỗ hai bên, file bị giới hạn trong vùng cách mép tối đa 120 DIP. Kỹ năng/đòn lan không đẩy hoặc phá file. Mỗi 3,5 giây cây thư mục đánh dấu vị trí mèo, chốt ngay, báo 1 giây rồi mọc, gây 12 damage cơ sở và choáng 0,6 giây; cây không chắn hoặc đẩy file. Ghép đủ ba thì hết khiên, sang phase 3, boss choáng 2,2 giây.
- **1 HP Bom nén sinh tử:** chỉ một lần khi HP xuống ≤1. Tất cả nguồn damage bị chặn ở đúng **1 HP**, kể cả damage vượt ngưỡng và độc; boss lập tức hủy đòn và dựng khiên. Sinh sáu tay kéo độc lập theo seed, ba trên không và ba dưới đất, chừa mép và giới hạn chiều cao theo vùng trận. Mỗi tay chịu đúng một nhát vũ khí trực tiếp; mèo đang cầm đánh tay trên không, mèo dưới sàn đánh tay dưới đất. Kéo/ném xuyên và Kéo cắt không giải thay. Phá đủ sáu: boss xì hơi 1,6 giây rồi bị hạ, không cần gây damage lần cuối. Mỗi 14 giây chưa giải xong thì nổ sàn theo damage/radius cũ, cao 170 DIP; giữ khiên và các tay chưa phá, không tạo lứa mới hoặc mở lại phase thường.

## Bổ sung sửa sau chơi thử — Issue #57

- Vị trí file và thư mục đích được chọn theo seed trong ba làn độc lập, nằm ngoài thân boss (tính cả kích thước đang lớn tới) và chừa mép. Hai đầu của mỗi làn cùng một phía boss; đẩy sai cũng bị giữ trong làn, không phải đẩy xuyên boss. Nếu chưa đủ chỗ, boss nhảy sát mép rồi mới đặt file; đồng hồ cây bắt đầu sau bố trí. Boss giữ vị trí trong lúc giải. Đã kiểm tra layout 480/640/1600 DIP qua 12 seed mỗi cỡ.
- Vào bất kỳ mech nào: hủy điểm chờ và dọn đạn giấy/gói bom thường còn lại của chính boss. `ClearPackets` chỉ dọn `ZipProp.None`, không dọn Belt/File/Tab. Kể cả một bom nổ trễ cũng không xóa file giải đố; cây trong mech vẫn gây damage/choáng như trước.
- Riêng giấy truy đuổi: quét đoạn đường đạn tương đối với chuyển động mèo qua hộp thân ngang ±30 DIP, từ chân −50 tới đầu +38 DIP, cộng bán kính giấy. Dùng tư thế thật cả khi bế, rơi hoặc bounce; không dùng mỗi tâm. Trúng khi bế/bay không làm tuột tay, không đổi vận tốc, không choáng. Khiên/Nộ/Pause giữ luật cũ. Đạn của slime khác giữ hình học cũ.
- Visual riêng, không dùng `Slam` chấn động chung cho Slime Nén: Kẹp kiện = hai nửa kiện/đai; Khóa kéo = rãnh răng khóa và đầu kéo; Cuộn = băng đóng kiện/cuộn băng; Cây = nhánh thư mục; Giấy = giấy gấp/nhàu; Giải nén lỗi = file lỗi/mảnh giấy gạch đỏ; Tràn bộ nhớ = lớp giấy; Ghi đè = khung file đóng dấu; Xé archive = mép giấy/răng khóa rách; đẩy riêng = luồng giấy; bom cuối = bung đai/phun giấy. Cảnh báo hình chữ nhật/làn vẫn bao đúng vùng trúng, không đổi hitbox theo hiệu ứng.
- HP vẫn 1100; records: c2_zip_slime 7, Vô tận 15, Bãi rác 8. Không nới ngưỡng cân bằng.

## Đòn đẩy riêng phase 2–3

Tách khỏi túi đòn, hồi 12 giây thời gian hợp lệ (lần đầu 5 giây sau chuyển phase), báo 0,9 giây. Chỉ bắt đầu khi mèo dưới sàn và giữa các đòn, không trong mech, Khóa ZIP, khoảng an toàn sau khóa hoặc lúc boss choáng. Đẩy mèo dưới sàn về rìa phía xa boss trong 0,8 giây rồi choáng 1,2 giây; **không gây damage**. Boss nghỉ tới hết đẩy/choáng và thêm 0,8 giây. Khiên/Nộ/miễn đẩy và ân hạn đáp chặn; không tuột tay hoặc đổi cú ném. Nhấc mèo thoát đẩy thì không có choáng trễ. Pause đóng băng cả báo trước và đẩy.

## Hình và tương tác

- Khóa ZIP là ổ khóa lớn có quai, thân và lỗ chìa gắn đai quanh mèo.
- Cây thư mục có ba mẫu phân cấp (tầng, lệch nhánh, xòe), chọn bằng seed cho mỗi cây; có thư mục gốc/con và đường nối vuông góc. Hình nằm trong vùng trúng đã báo.
- Giải nén lỗi: đồng hồ bắt đầu ngay khi file ra sân, không chờ soi; soi bom mới hiện màu/rung, vòng nổ và số giây còn lại. Đã soi thì giữ hiện khi chuột rời đi.

## Hai debuff

Tách khỏi túi đòn, không báo trước, không đồng thời. Lần đầu sau 8 giây, sau đó cách 10–16 giây. Đồng hồ dừng trong mech. Bị Khiên chặn thì vẫn tính lượt.

- **Nén:** hình mèo 75% (cửa sổ và vùng click giữ nguyên), vũ khí ×0,75, chạy ×0,8, kéo dài 5 giây. Kỹ năng và icon app không bị giảm. Không có thoát sớm.
- **Khóa ZIP:** không bế lên hoặc tự di chuyển được, vẫn đánh được. Mèo tự đánh ổ khóa trên người dù không có quái gần, đủ 5 nhát chạm theo nhịp vũ khí thì mở; không đánh quái, tích Nộ hoặc combo trong lúc này. Cũng mở bằng 3 click hoặc tự hết sau 6 giây; Pause dừng cả nhịp tự đánh và timer. Mỗi nhát có vũ khí hướng vào ổ khóa, khóa rung/chớp và thêm vết nứt.
  - Chỉ áp khi mèo dưới sàn và không choáng, boss không ở trong đòn đã báo, không có đạn, điểm chờ hay gói nổ.
  - Boss không mở đòn mới trong lúc Khóa và thêm 0,8 giây sau đó.
  - Mech bắt đầu thì Khóa tự mở, vì mech cần tay.
- Khiên chặn áp debuff. Nộ giải và miễn cả hai. Trận kết thúc hoặc boss chết thì xóa hết.

## Boss đánh mèo đang bế

Chỉ áp dụng cho Slime Nén. Slime Mẹ và Vua biến dị giữ luật cũ.

- Đòn chỉ trúng khi hộp đòn thực sự chạm hộp mèo (`CatInVolume`), áp dụng như nhau cho mèo đang bế và mèo đang bay sau ném.
- Đòn sát sàn né được bằng cách nâng cao; đòn `air` nhắm vị trí thật của mèo. Ân hạn lúc đáp vẫn bảo vệ.
- Trúng đòn khi đang bế hoặc bay: mất máu, không tuột tay, không đổi cú ném, không choáng, không Dizzy. Khiên và Pause giữ vai trò cũ.

## Số đo HP trước đổi mech (lịch sử, không đại diện bản #56)

Lưu ý #56: probe cũ chưa tắt app dù mô tả ghi không app; bản này truyền `apps:false`. Không dùng bảng cũ làm đối chứng có kiểm soát cho bản mới. Cân bằng toàn chương/Viễn chinh vẫn chạy với app theo luật màn.

`maxHp` 1100 (trước là 900). Đây là mô phỏng, không phải chơi thật.

**Phương pháp:** `ZipProbeTests` với `PET_ZIP_PROBE=1`, 8 seed mỗi dòng, chỉ trận boss, bot biết né, đè, cắt, kéo, mở khóa và dùng Nộ. Tính riêng thời gian đánh được và thời gian boss bất tử. "Vòng" là số đòn đã dùng chia 3.

**Giới hạn:** bot phản ứng đều và không sai sót kiểu người thật, không có thẻ di vật, chưa có app; ba mức build là `none`, `mid` (2 sharp + 2 fast_paws), `strong` (3 + 3 + 1 thick_fur).

Normal (threat 1,4, boss 1540 HP), mỗi ô là số vòng đòn:

| Vũ khí | Build | Thắng | Giây | Giai đoạn 1 | Giai đoạn 2 | Giai đoạn 3 |
|---|---|---|---|---|---|---|
| Kiếm | none | 7/8 | 90 | 2,1 | 1,0 | 2,0 |
| Kiếm | mid | 8/8 | 61 | 1,4 | 0,6 | 1,0 |
| Kiếm | strong | 8/8 | 47 | 1,0 | 0,3 | 0,8 |
| Búa | mid | 7/8 | 73 | 1,6 | 0,8 | 1,4 |
| Cuộn len | mid | 6/8 | 95 | 2,1 | 2,0 | 1,6 |
| Găng | mid | 8/8 | 64 | 1,4 | 0,8 | 1,2 |

Thời gian bất tử mỗi trận khoảng 7 + 2,5 + 3,3 giây. DPS thực của Kiếm mid: 30 / 66 / 30.

**Chưa đạt mục tiêu 1–2 vòng:** giai đoạn 2 chỉ đạt 0,6–0,8 vòng với build mid (Normal), 0,3–0,4 với build strong; Hard đạt khoảng 1,0 với mid. Nguyên nhân là boss to, đứng gần mèo nên DPS giai đoạn này cao gấp đôi, cộng với khoảng phản công. Chưa nâng HP thêm vì Cuộn len không thẻ ở Normal đã chỉ thắng 4/8.

Mô phỏng chương 2 màn cuối, bot cũ (không tự né, 60 seed): Normal simple 55–57/60, Hard simple 36–43/60. Ngưỡng cân bằng không bị nới.


## Bổ sung #58 — đai di chuyển và tự phá khóa

Damage đai dùng cùng đầu quét với renderer, quét đoạn từ vị trí frame trước tới frame hiện tại (rộng ±12 DIP, cao 65 DIP), không đánh cả nửa sân ngay khi hết báo. Pause và Screenshot giữ đứng cả đầu đai; màn/khóa đổi lượt hủy đai như trước. Boss gồng theo tiến độ báo trước, phần cuộn đai nằm ngoài mặt boss. Khóa ZIP hủy nhát đánh quái đang dở, ưu tiên nhát tự đánh khóa, đếm tại `HitTime`; 3 click/Nộ/kết thúc trận hủy nhát đang chờ để không có hit trễ. Vũ khí quá chậm vẫn có timeout 6 giây. HP 1100; records c2_zip_slime 8, Vô tận 16, Bãi rác 9.

Đổi vùng trận: FromX/ToX/X/Radius của đai và marker được ánh xạ cùng vị trí boss, giữ tiến độ và cờ đã trúng; không dùng đoạn quét từ tọa độ màn cũ. Đã kiểm bằng unit test, chưa xác nhận DPI hoặc nhiều màn hình thật.

## Bổ sung #59 — cây toàn chiều cao và nhịp đẩy (người dùng duyệt 06/11)

Cây thư mục mech 1 HP: lần đầu báo sau 2 s, mỗi 4,5 s chốt X mèo, báo 1 s, dải rộng 100 DIP từ sàn tới SkyHeight tồn tại 0,8 s; damage cơ sở 10 tối đa một lần/cây, choáng sàn 0,4 s. Bế/bay chỉ mất máu, không tuột tay/đổi vận tốc/choáng. Không chặn chuột hoặc phá tay kéo; đủ sáu tay dọn cả cây đang chờ. Đẩy riêng phase 2–3 đặt đồng hồ 5 s khi sang phase, hồi 12 s sau khi ra đòn; chỉ mở lúc mèo sàn và boss giữa đòn, ưu tiên trước đòn thường. Nhấc khi báo vẫn né được và tính lượt; giữ 0 damage/báo 0,9 s/đẩy 0,8 s/choáng cuối 1,2 s. Pause đóng băng tất cả. HP 1100, records 9/17/10.

## Bổ sung #60 — phạt hết giờ mech cuối (người dùng duyệt 06/11)

Mech cuối Slime Nén vẫn chỉ mở một lần ở 1 HP. Hết 14 s: nổ toàn vùng trận, damage cơ sở = CatMaxHp × bomb.hpShare (0,8), không nhân threat/damage boss, không né bằng độ cao hoặc ân hạn đáp; Khiên/vỏ rùa/Nộ và giảm damage giữ hiệu lực. Nếu đòn xuyên phòng vệ và mèo sống: bế/bay/ném/bounce bị ngắt, tuột tay, VX=0, hủy VY hướng lên, rơi theo trọng lực không nảy (kể cả đệm); cấm nhấc trong lúc rơi. Đáp sàn mới choáng đúng 5 s, không tạo ân hạn cứu hoặc Cú đáp địa chấn. Mèo sàn giữ choáng cũ của bomb.catStun. Nộ giải trạng thái bất lợi; không ảnh hưởng physics thả/ném khác. Vụ nổ chỉ hit một lần, hút file/khóa rung 2 s cuối, bung răng ZIP và 72 file/thư mục từ boss ra toàn màn hình có trận trong 0,9 s, không chặn chuột. Hết giờ dọn cây và toàn bộ tay cũ, reset số tay đã phá; sống qua hiệu ứng thì sinh mới 6 tay random theo seed (3 cao/3 đất) và bắt đầu đủ 14 s, cây chạy lại sau 2 s. Lặp cho tới giải đủ sáu hoặc mèo chết. Pause/menu đóng băng timer/effect/rơi. HP boss1100; records c2_zip_slime10/Endless18/Bãi rác11.

## Bổ sung #61 — Cuộn đóng gói truy đuổi3 lượt (06/11, người dùng duyệt)

Slime Nén phase1 Cuộn đóng gói lăn3 lượt: lần đầu báo1,1 s, hai lượt sau báo0,6 s. Trong gồng cập nhật hướng theo X mèo; khi lao chốt hướng, không quay giữa lượt. Giữ tốc độ360/tầm460/damage12 cơ sở, mỗi lượt chỉ trúng một lần; hết chỗ ở mép mới đổi sang hướng có chỗ. Đủ3 mới hồi phục; choáng/vào mech hủy chuỗi. Timer công khai ZipSeconds/ZipLeft đồng bộ gồng lăn thay vì đứng ở giá trị lúc mở đòn. King và mutant_king bật kingFormations và rallyTargets2; king_c1 giữ nguyên. Mỗi mech chọn ngẫu nhiên theo seed một bộ3 khác loại từ Pháo đài/Bạt nhún/Đội trống/Pháo kích (bốn tổ hợp3-trong-4); tổng9–10 quân, không timer/bầy nhập/bomber chồng hồi máu, báo0,6 s và xuất hiện đủ cùng frame; không chờ maxAlive, giới hạn SummonCap10. Quân theo vai trò hỗ trợ/cản/đánh/áp sát, không tạo đợt bù liên tục. Đủ hạ quân của phase thì khiên hạ. King thường nay có3 pha khiên75/50/25%; Vua biến dị giữ ngưỡng cũ, crown links/rain/tactics. Mỗi buff chọn2 con chưa tinh anh khác nhau trong quân phase, báo hai đường vương miện; mục tiêu chết thì chọn bù hợp lệ, ít hơn2 thì buff số còn lại; đẩy/choáng ngắt cả2. Giữ nhịp và sức buff cũ; Pause đóng băng. HP không đổi: ZIP1100/king480/mutant1000. Records ZIP11/Endless19/Bãi rác12; tuyến Viễn chinh chương1 giữ8.
