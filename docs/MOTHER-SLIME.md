# Slime Mẹ — thiết kế được duyệt 06/11/2026

Trạng thái: **đã triển khai, kiểm chứng và publish mốc 1–4 (`7712dc3`, 06–07/11)**. Luật dưới đây thay phần Slime Mẹ cũ trong BOSS-TACTICS.md khi `EnemyType.mother=true`. Mọi thời gian, phần trăm là số khởi điểm, cần đo lại; không nới ngưỡng cân bằng.

## Ý tưởng

Bà mẹ quá thương con, nhận nhầm mèo là con mình: dịu dàng nhưng đáng sợ, muốn ôm, quấn khăn và kéo mèo về tổ. Hình ảnh chỉ dùng trứng, nôi, khăn, nơ; không dùng hình ảnh sinh nở.

## Nhận diện

- Thân thạch hồng trong suốt hình chuông, mép dưới là váy nhầy bốn múi. Bỏ vương miện, thay bằng nơ lớn. Hai tay thạch ôm bụng.
- Dấu nhận diện: 5–6 trứng nhấp nhô trong bụng, cùng một nhịp tim sáng lên.
- Hitbox giữ nguyên, chỉ đổi hình. Art vector/procedural.
- Animation riêng: thở và nhịp tim, trườn bằng múi váy, cúi đẻ ổ, gồng riêng từng đòn, choáng thì nơ xoắn, trúng đòn thì trứng rung. Chết: ôm tổ, ngủ thiếp rồi tan thành bong bóng và tim.

## Luật chung

- Combat thuộc C#, Python không tham gia. Pause/menu/hướng dẫn đứng hình; Khiên và Nộ theo luật chung.
- Đòn `air` trúng mèo đang cầm/bay khi thực sự chạm: chỉ mất máu, không tuột tay, không đổi vận tốc, không choáng, không Dizzy.
- Mỗi đòn có báo, khóa, thực hiện, hồi phục; túi đòn theo seed, tránh lặp ngay. Choáng/Kẹp gắp hoặc vào mech ngắt đòn, không có cú đánh trễ.
- Miễn debuff khi vào mech, trong mech và chuyển phase như Slime Nén/Slime biến dị.
- **Dây chỉ đứt bằng Kéo cắt** (hoặc hạ con giữ dây); mèo bay/được kéo xuyên dây không cắt gì. Không mech nào bắt buộc có Kéo cắt, vì Viễn chinh có thể không mang và kỹ năng có hồi chiêu.
- Giữ bản sắc: ổ trứng, con về hồi máu, dây hồng cứu con ở phase 1–2; ổ/con không hoạt động trong mech. Lứa cuối 20% bỏ, cú lao tích giận thành đòn Ôm chầm.

## Ba phase, 9 đòn

| Phase | Hình | Đòn |
|---|---|---|
| 1. Ấp ủ (100–66%) | Hiền, bụng đầy trứng | **Vỗ về:** hai tay vỗ lệch nhịp vào hai điểm đã khóa. **Trứng lo xa:** ba trứng bay theo cung, vỡ thành vũng làm chậm. **Ôm chầm:** báo làn, lao tới, hai tay khép lại. |
| 2. Bảo vệ tổ (66–33%) | Mắt gắt, nơ dựng, bụng căng | **Bầy hộ tống:** con nhảy vào vùng báo; hết con thì nhả thạch đạn. **Dây nôi quất:** hai dây quét cao và thấp, có khe né. **Tràn tổ:** sóng nhầy lan hai bên kèm vỏ trứng lăn. |
| 3. Tổ vỡ (thanh dỗ) | Bụng xẹp, nơ rách, ít trứng; dịu dần theo thanh dỗ | **Nơ bung:** ruy băng xòe nan quạt có khe né. **Mưa vỏ trứng:** hai đợt có vòng báo, luôn chừa đường an toàn. **Ba cú ôm:** nhảy ôm vào ba điểm khóa. |

## Debuff Quấn khăn (từ phase 2)

Tách khỏi túi đòn. Mèo dưới sàn bị quấn khăn hồng, chạy ×0,75, dây kéo trượt dần về phía mẹ. Gỡ bằng 3 click vào khăn, Kéo cắt, hoặc tự hết sau 4 giây. Khiên chặn, Nộ giải. Mèo đang cầm/bay không bị áp; kéo trượt không đổi physics thả/ném; Pause đóng băng.

## Mech 1 (66%) — Trứng treo

Mẹ dựng khiên, treo 5 trứng bằng dây, có trứng cao và trứng thấp, đung đưa như con lắc. Bế mèo tới đánh để trứng rơi, rồi đánh vỡ trước khi nở. Kéo cắt là đường tắt: cắt dây thì trứng rơi ngay. Hết 40 s: trứng còn lại nở thành slime hợp thể, mẹ hồi 5% HP mỗi trứng, rồi sang phase 2.

## Mech 2 (33%) — Trả con về nôi

4 con hoảng chạy khắp sân. Bế mèo chạm con để con bám vào, rồi đưa lên nôi treo; dây quất làm con rơi lại. Đủ 4 thì mẹ dịu xuống, lộ yếu rồi sang phase 3. Hết 45 s: phạt 30% max HP mèo, rồi sang phase 3. Va chạm con chỉ là logic trận, không đổi physics kéo/ném.

## Phase 3 — Thanh dỗ

Sau Mech 2, thanh máu đổi thành Thanh dỗ màu hồng 0→100%, có vạch 33/66/100. Mẹ không mất máu nữa, muốn thắng phải dỗ đầy.

- Nhát vũ khí và kỹ năng trúng mẹ cộng dỗ theo sát thương, hiện tim thay số damage. Thanh đầy tương đương 33% max HP. Độc, đốt, execute không cộng.
- Ngừng đẻ ổ và dây cứu; con còn sống vẫn đánh. Khi giải ngưỡng thanh đứng yên, mẹ không ra đòn thường.
- Mỗi ngưỡng chỉ kích hoạt một lần, trừ 100%.

| Ngưỡng | Sự kiện | Cách giải | Thời gian |
|---|---|---|---|
| 33% | Bài hát ru | Bế mèo chạm 5 nốt tim trôi trên không, tránh nốt chói đỏ | 18 s |
| 66% | Vuốt lưng | Mèo đứng trong vòng tim dưới sàn (đổi chỗ sau mỗi lần vuốt), người chơi rê chuột vuốt theo mũi tên trên lưng mẹ đủ 3 lần | 15 s |
| 100% | Trứng cuối | Sóng nhịp tim lan toàn sân, nhấc mèo né; sóng qua thì bụng mở 1,8 s, mèo vỗ đủ 5 nhát | 30 s |

- Giải được ở 33%/66%: đòn chậm ×0,8 đến hết phase, thưởng thêm 10% thanh.
- Trượt (phương án A, người dùng chọn): thanh giảm 15%, mẹ òa khóc thành sóng nhẹ có báo trước, rồi dỗ tiếp tới **ngưỡng sau**, không lặp sự kiện vừa trượt.
- Trứng cuối bắt buộc giải: trượt thì thanh giảm 15%, dỗ đầy lại rồi giải lại.
- Giải xong Trứng cuối: mẹ ngủ thiếp rồi tan, tính là hạ boss bình thường, đủ thưởng. Không đụng luật ngủ của mèo.
- Không có đòn phạt lớn khi trượt; chỉ mất tiến độ.

## Mốc triển khai

0. Bỏ cắt dây bằng mèo ở mọi slime; đổi chỗ Kéo cắt (Đầm nhầy) và Bình xịt (Slime Nén); tăng rulesVersion các chế độ có slime xích. Kẹp gắp cắt xích giữ nguyên.
1. Hình và animation (có ảnh render cho người dùng xem).
2. Phase và 9 đòn, Quấn khăn.
3. Hai mech và thanh dỗ.
4. Cân bằng, bot, Sổ tay, i18n, tài liệu.

## Cần kiểm tra trước khi báo xong

- Chặn mọi nguồn damage ở thanh dỗ (thẻ, app, độc, execute); kết thúc trận đúng thưởng.
- Va chạm con khi bế, Vuốt lưng không kích hoạt drag, không phụ thuộc DPI/rung chuột; Pause dừng tiến độ.
- Layout chịu resize/DPI; timer theo simulation time, dọn sạch khi trận kết thúc.
- Đầm nhầy và `y_mother_nest` so bảng cân bằng trước/sau, perf `crowd-late`.

## Ánh xạ code và số liệu triển khai #67

- `PetBattle.Mother.cs`: state riêng `MotherBoss`, ba túi đòn theo seed trận/ID boss, không lặp ngay qua ranh túi. `MotherZone` dùng chung cảnh báo/va chạm, đạn cung quét tương đối cả mèo lẫn projectile; không đổi physics cửa sổ. Chuyển mech/choáng/Kẹp gắp hủy đòn chờ. Boss miễn debuff trong mọi mech, con và ổ đứng yên trong mech.
- Phase 1–2 dùng lại `BroodSpec`, ổ và dây cứu con; không có Lứa cuối 20% hoặc giận gọi cú lao cũ. Đòn mở báo cơ sở 1,1 s, khoảng hồi theo `bossTactics.opening` (3,5 s); thành công sự kiện dỗ làm nhịp chậm ×0,8. Bầy hộ tống dùng tối đa ba con sống, thiếu con thì bắn thạch thay vào các vị trí còn thiếu.
- Mech trứng: một nhát làm rơi, một nhát tiếp làm vỡ; dùng hồi vũ khí hiện tại, Kéo cắt chỉ làm rơi. Hết giờ mỗi trứng còn lại gọi một con hợp thể và hồi 5% HP, trong giới hạn summonCap. Mech nôi chỉ mang một con mỗi lượt; dây báo chỗ cũ, quất trúng làm rơi con nhưng không ép thả mèo.
- Thanh dỗ nhận hit trực tiếp Weapon/Combo/Bomb/Tool; chặn Poison/App/Reflect/Echo/Link/Splash/Throw, không cộng execute. HP giữ nguyên. Hát ru có năm nốt tốt và ba nốt đỏ, mỗi nốt đỏ ×0,3 damage mẹ. Vuốt lưng dùng hành trình có hướng trên nửa bề ngang mẹ, không tính rung nhỏ hoặc tích hành trình ngoài vùng. Mèo tự đi tới vòng mới; người chơi có thể bế đặt mèo vào vòng. Trứng cuối nhịp 4,2 s: báo 1 s, sóng 0,35 s, mở 1,8 s; năm nhát cộng dồn trong một lượt 30 s. Thắng ngủ/tan 2,5 s rồi gọi luồng hạ boss/thưởng chung.
- Khăn tồn tại 4 s, ba click gỡ, kéo sàn 32 DIP/s, hệ số chạy ×0,75; Khiên chặn áp, Nộ giải. Tự thử lại mỗi 12 s sau lần đầu ở phase 2. Chuyển mech/kết thúc trận dọn khăn.
- Art `PetBattleView.Mother.cs`, HUD và sky overlay dùng simulation time; không thêm sprite, Python, dependency hay timer độc lập. Resize map cả đích đòn, dây, trứng, con, nôi và vòng; đổi chiều cao chặn vật thể ra ngoài màn và xóa mẫu vuốt cũ.
- Cân bằng: HP khởi điểm 650 → 600 do boss mới có ba phase và nhiều khoảng giải mech; damage26/threat/summonCap/ngưỡng test giữ nguyên. Kỷ lục: Đầm nhầy6, Bãi rác17, Vô tận24; tuyến Slime/chương1/ZIP giữ nguyên.
- Test tự động: `BattleMotherTests`, `MotherPuzzlePlayer`, `MotherVisualChecks`; số đo và kết quả thật ghi ở PROGRESS/Issue_fixxed #67 khi đóng gói. Chưa có nghiệm thu chơi tay/DPI/nhiều màn hình.

Đối chứng và bảng cân bằng: [BALANCE-MOTHER-2026-11-06.md](BALANCE-MOTHER-2026-11-06.md).


## Polish hình và motion #68 — 07/11 (code/publish `4557dbd`)

`PetBattleView.MotherPose.cs` tính pose thuần theo simulation time; thân/nguồn tay/nơ/bụng dùng cùng tọa độ và scale. `MotherBoss` chỉ thêm actionAge/warn/end, poseMove, modeAge/previousMode, velocity phục vụ art; `MotherToken.DoneAge` chỉ dùng fade/pop. Không trường nào được dùng quyết định hit, nhịp đòn, damage, seed hay chuyển phase.

Mỗi nhóm đòn có lấy đà/phát/thu riêng, mô mềm và vải có quán tính. `PetBattleView.MotherEffects.cs` nối vai→tay, knot→ruy băng, bụng→trứng; shell có crack, tan thành vụn/vũng hồng kem (chỉ vũng owner Mother dùng material này trong Traps). Cảnh báo vẫn đúng footprint mô phỏng, effect không đổi hitbox. Nôi/egg swing dùng cùng vị trí thật; core bụng mở/khép theo cửa1,8s, fade không kéo dài damage. Chuyển mech và thu đòn dùng envelope thay cắt pose tức thời; mọi thứ dừng theo Pause/guide/menu, không timer riêng.

Review9đòn×6frame trên nền sáng/tối: `artifacts/mother-polish-render/mother-motion-0.png` tới9đòn (`...-8.png`). Kiểm tra `MotherMotionChecks`: sampling không mutate combat, continuity warn/release/recovery, emission đúng pose, Pause. So dòng mô phỏng với #67 giữ nguyên; không tăng rulesVersion. Chưa có nghiệm thu chơi tay/DPI/nhiều màn hình.
