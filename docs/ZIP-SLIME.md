# Slime Nén — thiết kế lại 06/11

Luật hiện hành của boss `zip`. Người dùng duyệt ngày 2026-11-06 (Issue #54, #55). Code: `PetBattle.Zip.cs` (giai đoạn, mech), `PetBattle.ZipMoves.cs` (đòn), `PetBattle.ZipDebuff.cs` (debuff); vẽ `PetBattleView.Zip.cs`, `PetBattleView.ZipMoves.cs`, `PetRenderer.cs`; test `BattleZipTests`, `BattleZipMoveTests`, `ZipProbeTests`, `ZipVisualChecks`. Số liệu ở `enemies[zip].zip` trong battle.json. Combat thuộc C#, Python không tham gia.

## Ba giai đoạn

| Giai đoạn | Thân | Đòn thường | Mech cuối giai đoạn |
|---|---|---|---|
| 1. Nén chặt (`4 KB`, ×1,1) | Kiện vuông, hai đai, khóa đóng | Kẹp kiện, Bắn khóa kéo, Cuộn đóng gói | 66%: Bung đai |
| 2. Giải nén (`2 MB`, ×2) | Khóa hé, giấy bung | Cây thư mục, Tệp truy đuổi, Giải nén lỗi | 33%: Giải nén kẹt |
| 3. Quá tải (`9 GB`, ×2,9) | Căng phồng, khóa toác | Tràn bộ nhớ, Ghi đè, Xé archive | 20%: Bom nén |

## Chọn đòn

- Túi đòn trộn bằng seed trận (`BattleRandom.Zip`). Đủ ba đòn hợp lệ mới trộn lại, không lặp ngay đòn vừa dùng.
- Đòn hợp lệ theo khoảng cách (`minRange`–`maxRange`). Mèo đang bế hoặc bay chỉ gặp đòn có `air`. Chỉ còn đúng đòn vừa dùng hợp lệ thì chờ 2,5 giây mới được lặp.
- Nhịp mỗi đòn: báo trước → khóa → ra đòn → hồi phục. Sau khi khóa, đòn đã báo không đổi hướng.
- Choáng, Kẹp gắp hoặc khiên mech ngắt đòn ngay và xóa các điểm đang chờ, không có cú đánh trễ.
- Chạm ngưỡng HP thì hủy đòn và vào mech. Sang giai đoạn mới thì túi đòn làm lại.

## Đòn thường

| Đòn | Cách đánh | Cách né |
|---|---|---|
| Kẹp kiện | Trượt tới điểm chốt, hai nửa kiện ép lại | Ra khỏi khe, hoặc nâng mèo cao hơn 90 DIP |
| Bắn khóa kéo (`air`) | Đường ngắm theo mèo, khóa rồi phóng tới điểm chốt và thu về, trúng tối đa 1 lần | Rời điểm chốt sau khi khóa, tránh đường về |
| Cuộn đóng gói | Lăn qua, dừng 0,45 giây, lăn ngược về chỗ cũ; dải báo cả hai lượt | Nâng cao hơn thân boss, hoặc ra khỏi dải |
| Cây thư mục | Tối đa 6 nhánh đánh số, nổ theo thứ tự, bỏ ngẫu nhiên 1 nhánh | Đứng vào khoảng giữa các nhánh, hoặc nâng cao hơn 120 DIP |
| Tệp truy đuổi (`air`) | Hai tệp bay cong, chỉnh hướng đúng 1 lần lúc vòng khóa hiện | Đổi chỗ sau tín hiệu khóa |
| Giải nén lỗi | Ba gói ra sân, một gói đỏ rung và có vòng nổ | Đánh vỡ gói đỏ (hai gói còn lại tan), hoặc ra khỏi vòng |
| Tràn bộ nhớ | Trượt tới điểm chốt, giấy lan từ hai mép, kết thúc bằng cú bung ngoài khoảng vàng | Ở trong khoảng vàng 190 DIP, hoặc nâng cao hơn 170 DIP |
| Ghi đè (`air`) | Đánh dấu 3 vị trí liên tiếp của mèo, kể cả trên cao, rồi đánh lần lượt | Rời điểm đã đánh dấu |
| Xé archive | Thân tách đôi nối bằng khóa kéo, quét ra 380 DIP rồi nhập lại, trúng tối đa 1 lần mỗi lượt | Ra ngoài tầm quét, hoặc nâng cao hơn 120 DIP |

## Ba mech

- **66% Bung đai:** khóa kẹt, boss trượt (không lăn) tới điểm vàng đã chốt từ đầu rồi dừng 2,2 giây. Chỉ thả mèo đè lúc đã dừng mới tính; mỗi lần bung một đai, 2 lần thì boss choáng 2,2 giây và nhận ×1,5 sát thương. Hết 4 lượt trượt thì khóa tự tuột, không thưởng.
- **33% Giải nén kẹt:** ba `zip_packet` nối luồng về boss, boss có khiên. Cắt luồng (kéo/ném mèo ≥300 DIP/giây, Kéo cắt) hoặc đánh vỡ gói. Cắt hai luồng trong 0,5 giây thì boss choáng thêm 1,5 giây. Không còn dùng đội Pháo đài.
- **20% Bom nén:** giữ cơ chế cũ, thêm mặt đồng hồ. 14 giây, kéo tay kéo 3 lần hoặc Kéo cắt thì boss xì hơi rồi choáng 3 giây, nhận ×1,5. Hết giờ thì nổ sàn, vùng trúng cao 170 DIP, nạp lại sau 14 giây.
- Khoảng phản công sau Bung đai và Bom nén bị giới hạn (`opening` 6% HP tối đa), để build mạnh không bỏ qua cả giai đoạn sau.

## Hai debuff

Tách khỏi túi đòn, không báo trước, không đồng thời. Lần đầu sau 8 giây, sau đó cách 10–16 giây. Đồng hồ dừng trong mech. Bị Khiên chặn thì vẫn tính lượt.

- **Nén:** hình mèo 75% (cửa sổ và vùng click giữ nguyên), vũ khí ×0,75, chạy ×0,8, kéo dài 5 giây. Kỹ năng và icon app không bị giảm. Không có thoát sớm.
- **Khóa ZIP:** không bế lên được, vẫn đi và đánh được. Mở bằng 3 click, hoặc 5 nhát vung kể cả trượt, hoặc tự hết sau 6 giây.
  - Chỉ áp khi mèo dưới sàn và không choáng, boss không ở trong đòn đã báo, không có đạn, điểm chờ hay gói nổ.
  - Boss không mở đòn mới trong lúc Khóa và thêm 0,8 giây sau đó.
  - Mech bắt đầu thì Khóa tự mở, vì mech cần tay.
- Khiên chặn áp debuff. Nộ giải và miễn cả hai. Trận kết thúc hoặc boss chết thì xóa hết.

## Boss đánh mèo đang bế

Chỉ áp dụng cho Slime Nén. Slime Mẹ và Vua biến dị giữ luật cũ.

- Đòn chỉ trúng khi hộp đòn thực sự chạm hộp mèo (`CatInVolume`), áp dụng như nhau cho mèo đang bế và mèo đang bay sau ném.
- Đòn sát sàn né được bằng cách nâng cao; đòn `air` nhắm vị trí thật của mèo. Ân hạn lúc đáp vẫn bảo vệ.
- Trúng đòn khi đang bế hoặc bay: mất máu, không tuột tay, không đổi cú ném, không choáng, không Dizzy. Khiên và Pause giữ vai trò cũ.

## Số đo HP

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
