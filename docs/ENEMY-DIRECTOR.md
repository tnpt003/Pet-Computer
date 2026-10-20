# Enemy Director & Tactical Formations — bàn giao có gacha

Ngày 2026-10-20 (Asia/Saigon). Người dùng yêu cầu ghi để AI khác triển khai. Lần này chỉ cập nhật tài liệu, chưa review/sửa/test code mới. Đọc AGENTS.md, PROGRESS.md, COMBAT-DEPTH.md, BATTLE-DATA.md và code thật trước khi lập kế hoạch; giữ phần đã triển khai, không xây lại engine. Kết quả các đợt trước không phải kiểm chứng của yêu cầu này.

## Tiến độ triển khai

- Người dùng chốt 2026-10-20 (sau khi Claude đối chiếu code): không làm posture (giữ chặn ngẫu nhiên + chặn đường, phải giết đá mới qua); đá chỉ chặn đòn phía trước, quay người ~0,6 s; support teleport (dưới 35% HP, gồng 0,6 s, hồi 8 s, không tới support khác, Đẩy hủy); King "Lệnh xung phong" (mỗi 7 s, vương miện sáng 0,8 s, +40% tốc độ chạy 3 s cho một quân, Đẩy King khi đang sáng thì hủy); King choáng 2 s khi khiên vỡ; Cầm cự spawn liên tục, threat tăng, thẻ ở giây 20/40/60; đánh hai phía có mũi tên cảnh báo 1 s (màn 5 đợt 1, Cầm cự sau 45 s); elite bộ nhỏ (nhanh gồng +0,3 s lao +40%, đá một lần chặn chắc chắn đầu tiên, support lần hồi đầu ×2; từ màn 3, không ở Dễ, tối đa 1 elite sống, tính vào ngân sách); thẻ hiếm mỗi nhóm 1 (Mai rùa / Đòn mở màn / Chạy đà; 15% thay thẻ thường cùng nhóm; cộng 1 điểm; tối đa 1).
- **Mốc ED1 (2026-10-20): xong** — seed + luồng ngẫu nhiên riêng, nhóm bốc theo trọng số có `atLeast`, đội (`squad`) spawn cùng lúc có giữ chỗ, `threat` đợt/màn + `threatDamageShare`, stream sinh tồn với `threatGrowth` và `upgradeAt`, đội hình 5 màn theo bảng dưới. Chi tiết: [BATTLE-DATA.md](BATTLE-DATA.md#đội-hình-threat-và-seed-enemy-director-mốc-1).
- **Mốc ED2 (2026-10-20): xong** — support mũ lá/túi thảo dược (bỏ dấu +), teleport, spawn cạnh đồng minh; đá chặn đường mèo, chỉ chặn đòn phía trước, quay người 0,6 s, hình rộng/thấp có giáp; vệ sĩ đá cho support; chia lượt ra đòn (2 con, cách 0,3 s); đánh hai phía có mũi tên; King lệnh xung phong + choáng 2 s khi vỡ khiên, quân không xuất hiện đè mèo. Chi tiết: [BATTLE-DATA.md](BATTLE-DATA.md#vai-trò-quái-và-chiến-thuật-enemy-director-mốc-2).
- **Mốc ED3 (2026-10-20): xong** — `cost`/`budget` cho đợt và stream, tinh anh (`elite`, `eliteChance`, `maxElites`, luồng affix riêng, viền vàng + sao, banner lần đầu), 3 thẻ hiếm (`rare`, `rareChance`, luồng rarity riêng): Mai rùa / Đòn mở màn / Chạy đà; cân bằng chương bằng mô phỏng. Chi tiết: [BATTLE-DATA.md](BATTLE-DATA.md#ngẫu-nhiên-có-giới-hạn-tinh-anh-ngân-sách-thẻ-hiếm-enemy-director-mốc-3). Mục tiêu "người chơi đơn giản thắng 60–85%, HP ≤ 60%" chưa đạt trong mô phỏng (xem PROGRESS); cần chơi thật để chốt.
- **Gọn lại (2026-10-21, người dùng chốt):** slime thường bỏ lùi về/vòng ra sau; slime thảo dược thay dịch chuyển bằng hồi máu xa (`farHealBelow`); Vua chương 1 600 HP, ba pha khiên 75/50/25%, lệnh của vương miện phong quân thành tinh anh thay cho tăng tốc. Chi tiết: [BATTLE-DATA.md](BATTLE-DATA.md#gọn-lại-chiến-thuật-quái-2026-10-21).

## Quan hệ với quyết định hiện tại

Tài liệu COMBAT-DEPTH.md ghi mốc 1–3 đã làm: stun 1,5s + kháng 3s; build cộng dồn qua màn lưu phiên bản 2; thẻ một lựa chọn mỗi nhóm và cá thay nhóm ít điểm nhất; đá block; King retreat/summon/shield. Giữ các quyết định đó trừ thay đổi mới được nêu rõ. Không quay lại reset build từng màn hoặc đổi kháng stun thành 1s chỉ vì đề xuất cũ. King có khiên vẫn dậm khi mèo gần là luật đã chốt.

Phạm vi mới gộp đội hình theo màn, vai trò quái, tạo hình support, đá chặn đường và ngẫu nhiên có kiểm soát. Các cơ chế còn hai phương án dưới đây phải được chốt trước khi code phần phụ thuộc, không âm thầm lựa chọn trái yêu cầu.

## Yêu cầu hành vi và ngoại hình

### King

- Vào mechanic chọn đầu work area xa mèo nhất (mèo trái → King phải), chừa biên đủ thân/hiệu ứng; khóa điểm retreat trong pha, không đổi hướng theo mỗi cú kéo.
- Viện binh spawn theo nhóm giữa mèo và boss, không đè mèo, tôn trọng cap. Shield liên kết đúng ID viện binh của pha. Giữ policy damage vượt ngưỡng đã chốt; không deadlock nếu spawn thiếu/hủy hoặc nhiều ngưỡng bị vượt.
- Hỗ trợ từ xa đề xuất: Lệnh xung phong, báo hiệu bằng vương miện sáng, rồi buff tốc độ di chuyển tạm cho một viện binh hợp lệ. Không buff stun/dash tức thì; không thêm cả heal/projectile/buff cùng lúc. Xác định điều kiện ngắt/đối phó và cooldown rõ.
- Đề xuất sau khi hết viện binh: King lộ sơ hở/choáng ngắn trước khi trở lại đánh. Con số và việc áp dụng cần cân bằng, không coi đã triển khai.

### Slime nhanh

- Hai con hit gần nhau chỉ tạo một stun 1,5s ở mèo; không cộng/reset stun timer. Guard ở status controller mèo và một hit mỗi dash ở quái.
- Giữ kháng stun 3s đã ghi ở mốc 1, trừ quyết định cân bằng mới. Damage từng hit là luật riêng, không tự bỏ damage của hit thứ hai.
- Có cảnh báo dash, recovery khi hụt, chịu ngắt theo luật; stun không khóa drag/throw/physics, pause đóng băng timer. Không tác động pickup Dizzy.

### Support: hình dáng và hành vi

- Bỏ dấu cộng gắn cố định trên thân (người dùng thấy xấu). Nhận diện bằng silhouette: slime thảo dược xanh ngọc, lá/mũ lá và túi thảo dược đơn giản. Không quá nhiều phụ kiện.
- Heal: lá phát sáng, hạt sáng truyền tới đồng minh; dấu + nếu dùng chỉ là effect thoáng qua. Hết budget hồi: lá cụp/ánh sáng tắt. Teleport có co người/vòng lá và tín hiệu vị trí đến.
- Spawn cạnh đồng minh, phía xa mèo; ở sau tuyến trước, không đánh mèo khi còn đồng minh giao chiến. Khi hết đồng minh hợp lệ thì tự vệ cận chiến, ngừng teleport giữa các support.
- Có thể teleport tới đồng minh HP thấp (gợi ý dưới 35%, cấu hình), đứng phía sau so với mèo. Teleport có windup, cooldown, đích hợp lệ và giới hạn; không hồi HP tự động chỉ vì teleport.
- Mục tiêu chết thì chọn lại; teleport sang đồng minh khác chỉ khi sẵn sàng, nếu không di chuyển bình thường. Không teleport vô hạn mỗi frame hoặc chạy trốn vô hạn ở biên.
- Heal có vận sức, đứng yên và cửa sổ để người chơi ngắt bằng kỹ năng hợp lệ. Giữ max HP, range/cooldown/budget, tránh hai support hỗ trợ nhau vô tận.

### Slime đá: tuyến trước

- HP cao hơn, block ngẫu nhiên, chặn mèo đi tự động xuyên qua trên mặt đất. Mèo chọn mục tiêu phía sau phải đánh vật cản trước; vẫn giữ target ưu tiên/marker và chỉ báo bị chắn.
- Người dùng yêu cầu phải giết đá mới đi qua; giữ làm mặc định. Kéo/thả mèo vòng sau vẫn cho phép, không phá input cứu nguy. Mèo bay không bị coi là đứng trên mặt sàn quái.
- Thân rộng/thấp, giáp phía trước, tư thế ghì chân, nứt khi yếu; hitbox khớp hình. Khi mèo ở sau, có thời gian quay người và cửa sổ dễ bị đánh. Block đề xuất chỉ phía trước.
- Không thêm stun/phản damage lên đá cùng lúc với HP/block/chặn đường.
- Phá thế thủ là lựa chọn thiết kế thêm: búa/Đẩy làm giảm posture, hết posture mất giáp ngắn. **Chưa chốt cho mèo vượt khi phá thế thủ** vì trái yêu cầu phải giết mới qua. Nếu muốn mở đường tạm phải xác nhận. Nếu thêm posture cần giảm/thay random block, không chồng mọi phòng thủ vô tội vạ.
- Nhiều đá không kẹp mèo vô hạn/đẩy ra màn hình; mèo tiếp đất chồng collider tách nhẹ tới vị trí hợp lệ, không teleport xa. Blocked AI luôn có hành động hợp lệ.

## Điều phối chiến thuật

Ưu tiên vệ sĩ bảo vệ support và nhịp tấn công xen kẽ:
- Một đá giữ giữa mèo và support thay vì mọi con đuổi mèo. Tính lại vai trò khi chết/đổi phía, có reaction delay; không đọc input tương lai.
- Cấp lượt chuẩn bị đòn theo nhóm để không tất cả hit một frame; khi con trước recovery con sau có thể lên. Không làm quái đứng chờ vô hạn hoặc khiến trận quá dễ.
- Đánh hai phía là biến thể encounter có cảnh báo: đá trước, nhanh phía đối diện. Độ khó giới hạn số threat cùng lúc; tránh ghép dash/stun/slam không có đường né.
- Tín hiệu trước hành động, thời gian phản ứng, sơ hở sau hành động là tiêu chí bắt buộc.

## Spawn/threat xuyên chương

Chỉ số cuối = chỉ số enemy × độ khó × threat của màn/đợt (và affix nếu có). HP và damage có đường cong riêng, damage tăng chậm hơn; tính một lần khi spawn. Mốc threat cố định theo tiến trình, replay không tự càng ngày càng khó. Không tăng duration stun theo threat.

| Màn | Đội hình yêu cầu |
| --- | --- |
| First Step | Đầu màn chỉ thường; đợt cuối có tỷ lệ nhanh, bảo đảm giới thiệu ít nhất một con trong lượt hướng dẫn nếu đủ chỗ |
| Break Through | Đá nhiều; thường và nhanh chia đều phần còn lại |
| Hold On | Một đợt spawn liên tục tới hết thời gian sinh tồn, nhanh nhiều; đá/thường chia đều phần còn lại |
| Cut the Support | Nhóm 1 support + 3 đá spawn cùng nhau, gần nhau; support phía sau |
| King | Pha/nhóm viện binh riêng, không trộn tùy ý spawn thường vào cơ chế shield |

Hold On tăng threat theo thời gian, có mốc chọn thẻ tối đa 3 lần theo quyết định hiện tại; tạm dừng đồng hồ khi chọn. Spawn vô hạn theo thời gian không phải unlimited alive. Reserve đủ 4 slot cho nhóm support/đá, nếu thiếu thì chờ, không spawn support lẻ. Cap phải tương thích formation, validate tránh nhóm không bao giờ spawn được.

## Gacha đã được đồng ý về hướng

Ngẫu nhiên trong gameplay, không tiền tệ/vé quay/gacha trả phí.

1. **Đội hình:** weighted tables theo stage + difficulty, ngân sách sức mạnh chung, giới hạn tổ hợp nguy hiểm. Không spawn loại chưa giới thiệu ở màn đó.
2. **Tinh anh:** mỗi quái tối đa một affix, roll một lần khi spawn, nhận diện bằng hình/effect. Tối đa số elite sống cùng lúc. Ví dụ: nhanh có windup dài hơn nhưng dash nhanh; đá thêm một lượt giáp; support lần heal đầu mạnh hơn. Đây là ví dụ cần chọn bộ nhỏ, không mặc định stack với tất cả buff khác. Tính chi phí affix trong spawn budget.
3. **Thẻ:** rarity và tăng cơ hội hoàn thành build, nhưng không phá luật một thẻ mỗi nhóm đã chốt. Nếu các nhóm đã luôn có mặt thì không thêm pity nhóm vô nghĩa; ưu tiên độ hữu ích trong nhóm, eligibility/cap và phân phối rarity. Cá vẫn theo luật hiện có. Thẻ hiếm khác cách dùng, không mạnh hơn mọi mặt. Nếu đổi offer policy phải nêu rõ và thống nhất trước.

Random block đá: roll đúng một lần mỗi hit hợp lệ, xác suất trần, giới hạn chuỗi, effect BLOCK; không roll mỗi frame/không roll reflect hay proc đệ quy. Giữ ngoại lệ bom/Giáp gai/combo Phá giáp đã chốt, trừ thay đổi được xác nhận. Nếu posture đưa vào thì chốt interaction trước.

Không gacha thời lượng stun, điều kiện hết khiên King, collision hit thực tế hoặc khóa tiến trình bắt buộc. RNG có seed injectable; nên tách stream spawn/affix/offer/block để animation hoặc thêm roll không thay toàn bộ trận. Ghi seed/rulesVersion để tái hiện; test nhiều seed, không chỉ seed đẹp. Kỷ lục custom/official và version luật giữ tách đúng.

## Chuẩn hóa và giảm tải

Tận dụng hệ hiện có, các tên sau chỉ mô tả trách nhiệm:
- SpawnDirector: budget/cap/nhịp và reservation formation.
- SpawnTable/Formation: trọng số và vị trí tương đối; ThreatScaling: tính chỉ số spawn.
- StatusController: stun/immunity/stack policy; BossPhase: threshold/retreat/summon IDs.
- Các capability support/dash/block cùng damage pipeline; không tự nhân rules ở nhiều UI handler.
- Target/formation decision theo sự kiện hoặc 5–10 Hz; cache mục tiêu/đích, movement/physics vẫn mượt theo timestep hiện có. Không thêm window/timer cho mỗi quái.
- Có cap enemy/effect và fallback khi không tìm được đích hợp lệ. Chưa thêm pooling/spatial index nếu chưa đo bottleneck. Đo CPU/frame-time/alloc ở cùng scenario trước/sau.

## Kế hoạch để AI tiếp nhận

1. Inspect phần Combat Depth đang làm, bảo toàn quyết định build cộng dồn, số thẻ, damage exemptions và stun immunity. Nêu các xung đột cần chốt (đặc biệt posture/vượt đá, offer rarity) trước khi sửa.
2. Chuẩn hóa spawn/threat/status + test; làm formation và tỷ lệ màn.
3. Support art/teleport/guardian, đá chắn đường, King hỗ trợ xa; giữ collision/pause/AI hợp lệ.
4. Gacha có giới hạn và cân bằng theo build/weapon/difficulty, nhiều seed; xem UI và playtest.

Tiêu chí: không double stun, không shield boss vĩnh viễn, không deadlock formation/collider/support; stage survival hết giờ đúng và chọn thẻ không chạy timer; custom schema/version lỗi fallback; RNG tái hiện; không rò timer/window qua replay. Regression Pet Mode và save/build/progress vẫn đạt. Thêm manual-test sáng/tối/DPI/dash/teleport/block/formation, không tuyên bố chơi thật khi chỉ mô phỏng.

Cập nhật PROGRESS, BATTLE-DATA, MANUAL-TEST theo kết quả. Mốc code hoàn chỉnh: test → commit → publish → smoke theo AGENTS; không tự push/upload. Yêu cầu hiện tại là bàn giao tài liệu, không phải bằng chứng những tính năng này đã code.
