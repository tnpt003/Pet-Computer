# Tùy chỉnh dữ liệu Battle Mode (battle.json)

Áp dụng từ 2026-10-19, `schemaVersion` 1. Không cần sửa code hay build lại exe để thêm biến thể quái, vũ khí hoặc đổi các đợt quái.

## File nằm ở đâu

| File | Vai trò |
| --- | --- |
| `src/DesktopPet/Assets/battle.json` | Dữ liệu gốc, nhúng vào exe. Người dùng không sửa file này. |
| `%LOCALAPPDATA%\DesktopPet\battle.json` | File của người dùng, cùng thư mục với `preferences.json` (hoặc `PET_DATA_DIRECTORY` nếu có đặt). Không bắt buộc phải có. |
| `docs/examples/battle.json` | Ví dụ thêm `stone_slime`. Test tự động cũng đọc file này nên ví dụ luôn hợp lệ. |

Ngôn ngữ khác (tiếng Nhật): tên/mô tả hiển thị tra bản dịch theo đúng câu `...En` trong `src/DesktopPet/Assets/i18n/ja.json` (cũng như coats.json, accessories.json). Câu `...En` không có trong bảng (ví dụ tên tự thêm trong file của người dùng) thì hiện tiếng Anh. Đổi câu tiếng Anh trong dữ liệu gốc thì đổi khóa trong ja.json (`scripts/i18n.py` liệt kê câu thiếu bản dịch).

File của người dùng được đọc lại **mỗi lần mở sảnh chiến đấu, bản đồ hoặc bảng chuẩn bị** (cả Chơi lại). Khi bấm Sẵn sàng, trận chụp lại dữ liệu đang có; sửa file giữa trận không ảnh hưởng trận đang chạy, chỉ áp dụng cho lần chuẩn bị sau.

## Cách gộp

Chỉ cần ghi những gì muốn đổi. File của người dùng được gộp đè lên dữ liệu gốc theo quy tắc sau:

- `"schemaVersion": 1` là **bắt buộc**. Thiếu hoặc khác 1 thì cả file bị bỏ qua.
- Số và chuỗi ở cấp trên cùng (ví dụ `"catMaxHp": 120`) thay giá trị gốc.
- Object (`easy`, `hard`) gộp theo từng khóa; ví dụ `"easy": { "enemyHp": 0.5 }` chỉ đổi đúng một số đó.
- `enemies`, `weapons`, `upgrades`, `synergies`, `skills` và `formations` gộp **theo `id`**:
  - `id` đã có: chỉ đổi các khóa được ghi (ví dụ `{ "id": "slime", "maxHp": 60 }`).
  - `id` mới: thêm vào cuối danh sách.
  - Mỗi mục phải có `id`, không được trùng trong file.
  - Không xóa được quái/vũ khí gốc, nhưng có thể bỏ chúng khỏi `waves`.
- `waves` nếu có thì **thay toàn bộ** danh sách đợt.
- Tên khóa không phân biệt hoa/thường. Được viết chú thích `//` và để dấu phẩy cuối.

Sau khi gộp, toàn bộ dữ liệu được kiểm tra. **Chỉ cần một lỗi là cả file bị bỏ qua**, không bao giờ chạy dữ liệu "nửa đúng":

- Mèo vẫn chạy bằng dữ liệu gốc.
- Log ghi lý do.
- Bảng chuẩn bị hiện dòng vàng kèm khóa bị lỗi, ví dụ `enemies[slime].maxHp must be 1–100000`.

Khi dùng được file tùy chỉnh, bảng chuẩn bị ghi "Đang dùng dữ liệu trận tùy chỉnh".

## Quái (`enemies`): mẫu dùng chung

Mỗi mục là một **mẫu quái** dùng chung cho mọi con cùng loại. Mỗi con trong trận giữ HP, vị trí, trạng thái và hồi chiêu riêng; trong trận không có gì sửa vào mẫu.

| Khóa | Giới hạn | Ý nghĩa |
| --- | --- | --- |
| `id` | `a-z 0-9 _ -`, tối đa 32 ký tự | ID cố định; `waves` và `summonType` dùng ID này |
| `family` | dạng như id, mặc định `slime` | Nhóm quái |
| `renderer` | chỉ `slime` | Cách vẽ đã có trong code |
| `nameVi`, `nameEn` | tối đa 32 ký tự | Tên hiển thị |
| `maxHp` | 1–100000 | Máu |
| `speed` | 0–400 | Tốc độ đi (DIP/giây) |
| `damage` | 0–1000 | Sát thương mỗi đòn |
| `range` | 10–400 | Tầm đánh (tính từ tâm tới tâm) |
| `windup` | 0.1–10 | Thời gian gồng có dấu "!" trước khi đánh (giây) |
| `attackCooldown` | 0.1–30 | Thời gian giữa hai đòn |
| `recover` | 0–10 | Thời gian nghỉ sau đòn |
| `scale` | 0.5–2.3 | Kích thước **vẽ** so với slime thường |
| `hitScale` | 0.3–2.3, mặc định = `scale` | Kích thước **trúng đòn** (tầm kiếm, vùng nổ bom) |
| `color`, `angryColor` | `#RRGGBB` | Màu thường / màu khi nổi giận |
| `marks` | `none`, `speed`, `cracks`, `leaf`, `cross`, `spout`, `spring` | Họa tiết: vệt gió / vết nứt / lá thảo dược / dấu "+" cũ / vòi phun (1.5 M09) / lò xo (1.5 M09) |
| `binSpit` | true/false (mặc định true) | (1.5 M09) Thùng Rác nhiễm virus được nhả loại này (khi `spitElite`). Quái 1.5 đặt `false` để không lọt vào trận chưa dùng chúng |
| `pushFactor`, `stunFactor` | 0–3 | Hệ số bị đẩy lùi / thời gian choáng (1 = như slime thường) |
| `boss` | true/false | Có vương miện, thanh HP riêng, banner BOSS |
| `slam` | true/false | Đập đất trúng cả vùng, có cảnh báo đỏ trên sàn |
| `slamDamage` | 0–1000 (chỉ khi `slam`) | Sát thương của cú dậm (nhân độ khó/threat như `damage`); 0 = bằng `damage`. Bản gốc 2026-10-24: hai Vua 22 (đòn `damage` 30 giữ nguyên) |
| `summonAt`, `summonCount`, `summonType` | 0–1; 1–6; id | Dưới tỷ lệ HP này gọi quái phụ (một lần). Quái được gọi không được tự gọi thêm. |
| `enrageAt`, `enragedSpeed`, `enragedWindup` | 0–1; 0.1–400; 0.1–10 | Dưới tỷ lệ HP này thì nổi giận: nhanh hơn, gồng ngắn hơn |

JSON chỉ chọn được những khả năng code đã hỗ trợ (từ 1.5 có thêm `link`, `hover`, `throwHit`, `spit`, `spring`, xem các mục cuối). Khóa lạ như `"flying": true` sẽ bị từ chối. Muốn có khả năng mới (bay, bắn đạn…) phải viết code controller/renderer trước.

## Vũ khí (`weapons`): mẫu dùng chung

Thứ tự trong danh sách là thứ tự trên bảng chuẩn bị; vũ khí đầu tiên là mặc định. Diễn biến một lần vung và hồi chiêu thuộc về từng trận, không nằm trong mẫu.

| Khóa | Giới hạn | Ý nghĩa |
| --- | --- | --- |
| `id` | như id quái | Được lưu trong `preferences.json` (`BattleWeapon`) |
| `nameVi`, `nameEn` | tối đa 32 ký tự (trống thì hiện id) | Tên |
| `hintVi`, `hintEn` | tối đa 80 ký tự | Dòng gợi ý dưới nút chọn |
| `style` | `sword`, `hammer`, `yarn`, `glove` | Hình vẽ cầm tay và kiểu vung (búa: giơ cao, gồng, đập; cuộn len: quất rộng; găng: đấm thẳng, không có vệt chém) |
| `hitEffect` | `spark` hoặc `thud`, mặc định theo style | Dấu đòn trúng: tia trắng / bụi và vòng trên sàn |
| `damage` | 0–1000 | Sát thương mỗi nhát |
| `range` | 10–300 | Tầm đánh |
| `attackCooldown` | 0.1–10 | Thời gian từ nhát này tới nhát sau |
| `swingDuration` | 0.05–5 | Độ dài một lần vung |
| `hitTime` | lớn hơn 0 và nhỏ hơn `swingDuration` | Thời điểm duy nhất trong lần vung gây sát thương |
| `knock` | 0–1000 | Mỗi nhát đẩy lùi slime (DIP/giây × `pushFactor`); 0 = chỉ combo mới đẩy |
| `stunChance`, `stunSeconds` | 0–1, 0–3 s (mặc định 0.15, 0.8) | (2026-10-24) Tỉ lệ một nhát trúng làm choáng quái `stunSeconds` × `stunFactor` của nó, hủy đòn đang gồng. Không choáng trùm, tinh anh miễn Đẩy, quái đang lao/hồi máu/nhảy. Bản gốc: búa 0.15 / 0.8 s, kiếm 0 |
| `sweep` | true/false (mặc định false) | (1.5 M08) Mỗi nhát trúng còn trúng mọi slime khác trong tầm phía trước mèo (nguyên đòn thường, bị đá chặn như đòn lan); combo không lan thêm. Bản gốc: cuộn len |
| `pull` | 0–1000 DIP/giây | (1.5 M08) Nhát trúng kéo slime **đứng xa** (loại `support`, ví dụ slime thảo dược; slime phun Q1 ở M09) về phía mèo, dừng ngay cạnh mèo; không kéo boss, tinh anh miễn Đẩy, slime dính. Bản gốc: cuộn len 420 |
| `comboHits` | 2–6 (trống: theo `comboHits` của trận) | (1.5 M08) Số nhát liền để thành combo của vũ khí này. Bản gốc: găng 5 |
| `launch`, `launchStun` | 0–1500 DIP/giây, 0–3 s | (1.5 M08) Nhát combo hất tung quái lên (bay vòng cung như Kẹp gắp thả, không va ai), khi bay quái không đánh và mèo không nhắm nó; đáp đất thì choáng `launchStun`. Không hất boss, đá, quái lớn hơn `grabberMaxScale`, tinh anh miễn Đẩy, quái bay/cưỡi/dính. Bản gốc: găng 560 / 0,8 s |

Bốn vũ khí gốc (1.5 M08 thêm hai cái cuối, chọn được ở mọi bảng chuẩn bị; kỷ lục tách theo vũ khí như trước):

| Vũ khí | Sát thương | Tầm | Hồi | Đặc điểm |
| --- | --- | --- | --- | --- |
| Kiếm `sword` | 25 | 76 | 0,75 s | combo 3 nhát |
| Búa `hammer` | 45 | 76 | 1,35 s | đẩy lùi mỗi nhát, 15% choáng |
| Cuộn len `yarn` | 14 | 114 | 0,8 s | quét mọi slime trong tầm, kéo slime đứng xa, không choáng |
| Găng đấm `glove` | 14 | 70 | 0,38 s | combo 5 nhát, nhát cuối hất tung (choáng 0,8 s khi đáp) |

Mô phỏng cân bằng chương 1 (AdventureBalanceTests, người chơi "simple", 20 seed): cuộn len và găng thắng trong khoảng 30 điểm so với kiếm ở mọi màn và độ khó, ≥ 80% ở Normal; bảng kiếm/búa không đổi. Găng ở Hard yếu hơn kiếm rõ nhất (màn 4–5: 55–60% so với 80–85%). Số lần hất tung thực tế ít (1–4 mỗi trận) vì chuỗi 5 nhát hay đứt khi đổi mục tiêu.

Nếu `id` đã lưu trong preferences không còn trong dữ liệu (ví dụ file bị xóa), mèo dùng vũ khí đầu tiên. Preferences cũ ghi `"Sword"`/`"Hammer"` vẫn khớp với `sword`/`hammer`.

## Kỹ năng (`skills`) (2026-10-22)

Tên và mô tả của 10 kỹ năng (HUD, túi đồ, bảng chuẩn bị). Gộp theo `id` như `weapons`: ghi `{ "id": "heal", "nameEn": "Fish snack" }` là đổi đúng tên tiếng Anh của Cá hồi máu; file tùy chỉnh trước 1.5 chỉ có 5 kỹ năng vẫn đọc được (5 kỹ năng mới giữ chữ gốc). Số liệu vẫn ở cấp trên cùng (`pushCooldown`, `healAmount`…); thứ tự trên HUD, kỹ năng nào phải nhắm (Đẩy, Bom) và tác dụng nằm trong app, không đổi bằng dữ liệu.

| Khóa | Giới hạn | Ý nghĩa |
| --- | --- | --- |
| `id` | `push`, `heal`, `cheer`, `bomb`, `shield`, `laser`, `grabber`, `scissors`, `fan`, `spray` | Kỹ năng nào; danh sách gộp xong phải có đủ cả 10, không trùng |
| `nameVi`, `nameEn` | 1–32 ký tự | Tên |
| `descVi`, `descEn` | 1–220 ký tự | Mô tả khi rê chuột; `{khóa}` được thay bằng số cùng tên ở cấp trên cùng của battle.json, ví dụ `{pushCooldown}` → `3` (không phân biệt hoa thường; khóa không có thì file bị từ chối) |

### Bộ kỹ năng mang theo (1.5 M02, 2026-10-28)

- Mỗi trận mang **5 trong 10** kỹ năng; HUD có đúng 5 ô theo thứ tự cố định ở bảng trên (5 kỹ năng cũ trước, rồi `laser`, `grabber`, `scissors`, `fan`, `spray`). Mặc định là 5 kỹ năng cũ.
- Bảng chuẩn bị có mục "Kỹ năng mang theo (n/5)": 10 ô icon, bấm ô đã chọn để bỏ, bấm ô còn mở để thêm khi chưa đủ 5; Sẵn sàng chỉ chạy khi đủ 5. Lựa chọn lưu trong preferences (`BattleLoadout`, null = mặc định); id không còn hợp lệ hoặc kỹ năng chưa mở thì đọc như mặc định.
- Có mục chọn ở trận tự do, Vô tận, chuẩn bị chuyến Viễn chinh (bộ kỹ năng giữ suốt chuyến, lưu trong chuyến: `Skills`, null = mặc định) và màn thuộc chương có `"skillChoice": true`. **Chương 1 không có `skillChoice`**: luôn 5 kỹ năng cũ, mục chọn ẩn.
- Cả 5 kỹ năng mới đã có tác dụng: Đèn laser, Quạt từ M05 (mục "Đèn laser và Quạt"); Kẹp gắp, Kéo cắt, Bình xịt nước từ M06 (mục "Kẹp gắp, Kéo cắt, Bình xịt nước"). Một kỹ năng mới mở khi đã thắng màn có `"unlocksSkill": "<id>"` (từ M21: 5 màn chương 2, mục "Chương 2"); chưa mở thì hiện ô khóa, không chọn được, trừ ở chính màn mở nó (màn đó **cho mượn**, xem "Chương 2"). Riêng **trận tự do** mang được cả 10 kỹ năng (người dùng chốt 2026-10-28; giữ nguyên sau M21). Cờ `Working` trong code vẫn còn cho kỹ năng sau này chưa có tác dụng.
- Kỹ năng không mang theo: ô không có trên HUD, không dùng được. Thẻ có `"skill": "<id>"` (gốc: Bom to → `bomb`, Khiên dày → `shield`) và di vật có `"skill"` (gốc: Chuông phản đòn → `shield`, Túi cứu trợ → `heal`) chỉ được đưa khi bộ mang theo có kỹ năng đó. Thẻ/di vật đã có từ trước vẫn giữ.
- Kỷ lục không tách theo bộ kỹ năng.
- **Đổi kỹ năng ở điểm nghỉ Viễn chinh (M06):** khi điểm nghỉ đang mở và số kỹ năng đã mở nhiều hơn 5, khung lựa chọn có nút "Đổi kỹ năng": hiện các kỹ năng đã mở (đang mang có dấu ✓), bỏ/chọn cho đủ 5 rồi "Giữ bộ này" (hoặc "Hủy"). Miễn phí, đổi bao nhiêu lần cũng được, **không thay** lựa chọn hồi máu/lấy thẻ của điểm nghỉ (vẫn phải chọn một trong hai). Thẻ đang được điểm nghỉ đưa mà gắn với kỹ năng vừa bỏ thì rút khỏi lựa chọn (không bốc lại thẻ khác). Chuyến lưu bộ mới (`Skills`); trận sau mang bộ mới. Chỉ có 5 kỹ năng cũ mở (hiện tại, tới khi có chương 2) thì nút không hiện.

## Đợt quái (`waves`) và luật trận

- `waves`: tối đa 30 đợt. Mỗi đợt có `groups`, mỗi group gồm `enemy` (id) và `count` (1–50). Các group ra quái theo thứ tự; tối đa `maxAlive` con cùng lúc. Đợt nào cũng có thể có boss.
- Luật trận ở cấp trên cùng: `catMaxHp`, `maxAlive`, `spawnInterval`, `firstWaveDelay`, `waveBreakSeconds`, `waveHeal`, `landingGraceSeconds` (0–5), `rescueGraceCooldownSeconds` (0–60), `rescueGraceMinLiftSeconds` (0–2, mặc định 0.3: nhấc ngắn hơn thì thả xuống không được bảo vệ), kỹ năng (`pushCooldown`, `healAmount`…), combo (`comboHits` 2–6…; `utilityChance` 0–1 ở mục thẻ; `comboSplash` 0–2 và `comboSplashRadius` 0–300 DIPs từ 2026-10-24: đòn combo gây thêm `comboSplash` × sát thương của nó cho mọi quái khác trong `comboSplashRadius` DIPs tính từ mép mục tiêu, có thể bị đá chặn như combo, Phá giáp xuyên được; gốc 0.6 và 60; từ 2026-10-24 chỉ quái nằm trong tầm vung: phía trước mèo, mép gần cách tâm mèo ≤ tầm vũ khí + 14 DIPs).
- Độ khó `easy`/`hard` có `enemyHp`, `enemyDamage`, `enemySpeed`, mỗi số trong khoảng 0.25–3. Mức Thường dùng nguyên số liệu.
- Xem file gốc để biết đủ các khóa và giá trị mặc định.

## Ví dụ

Thêm một biến thể slime (giống `docs/examples/battle.json`):

```json
{
  "schemaVersion": 1,
  "enemies": [
    { "id": "stone_slime", "family": "slime", "renderer": "slime",
      "nameVi": "Slime đá tảng", "nameEn": "Stone slime",
      "maxHp": 120, "speed": 22, "damage": 14, "range": 70,
      "scale": 1.35, "color": "#8F8A80", "marks": "cracks",
      "pushFactor": 0.35, "stunFactor": 0.5 }
  ]
}
```

Quái mới **không tự xuất hiện** trong các đợt gốc. Muốn gặp nó, thêm `waves` (danh sách này thay toàn bộ đợt):

```json
  "waves": [
    { "groups": [ { "enemy": "slime", "count": 2 }, { "enemy": "stone_slime", "count": 1 } ] },
    { "groups": [ { "enemy": "king", "count": 1 } ] }
  ]
```

Thêm một vũ khí:

```json
{ "schemaVersion": 1,
  "weapons": [ { "id": "spear", "nameVi": "Giáo", "nameEn": "Spear", "style": "sword",
                 "hintVi": "tầm xa, đánh vừa", "hintEn": "long reach, medium hits",
                 "damage": 18, "range": 110, "attackCooldown": 0.9, "swingDuration": 0.4, "hitTime": 0.2, "knock": 0 } ] }
```

Chỉ làm slime thường yếu đi:

```json
{ "schemaVersion": 1, "enemies": [ { "id": "slime", "maxHp": 35 } ] }
```

## Ánh xạ sang code

| Khái niệm | Trong code |
| --- | --- |
| EnemyDefinition | `EnemyType` (`PetBattleData.cs`) |
| EnemyInstance | `Slime` (`PetBattle.cs`) |
| EnemyController | `BattleSim` |
| EnemyRenderer | `BattleFieldView` (`PetBattleView.cs`) |
| WeaponDefinition | `WeaponSpec` |
| WaveDefinition | `WaveSpec` / `WaveGroup` |
| BattleRules | `BattleSettings` |
| Nạp, gộp và kiểm tra dữ liệu | `BattleData` (`Load`, `ParseWithOverride`, `Validate`) |
| SkillDefinition | `SkillSpec` (tên, mô tả) + `BattleSkills` (thứ tự, nhắm, hồi chiêu, tác dụng trong `BattleSim.Use`) |

## Kỷ lục (battle-records.json)

Từ 2026-10-19 (`Version` 2), kỷ lục được tách theo **id vũ khí + độ khó** (ví dụ `hammer|Normal`). Trận chơi khi đang dùng file battle.json tùy chỉnh được ghi riêng dưới khóa có thêm `|custom`, vì cùng một id có thể đang mang số liệu khác. Đây là kỷ lục cá nhân trên máy, không phải bảng xếp hạng.

Kỷ lục của file cũ (trước khi tách theo vũ khí) được giữ nguyên trong `Easy`/`Normal`/`Hard` và hiển thị là "Trước phân loại trang bị"; chúng không bị gán cho vũ khí nào. Xóa một vũ khí khỏi dữ liệu không làm mất kỷ lục của nó.


## Phiêu lưu (`campaign`)

Từ 2026-10-20 battle.json có khóa tùy chọn `campaign`: các chương, mỗi chương là danh sách màn chơi theo thứ tự. Thắng một màn thì mở màn kế tiếp trong chương; màn đầu luôn mở, trừ chương có `unlockAfter` (mục "Chương 2").

```jsonc
"campaign": {
  "chapters": [
    {
      "id": "slime_invasion", "nameVi": "Cuộc xâm nhập của Slime", "nameEn": "The Slime Invasion",
      "stages": [
        {
          "id": "c1_first_steps", "nameVi": "Làm quen", "nameEn": "First steps",
          "goalVi": "…", "goalEn": "…",       // tối đa 160 ký tự, hiện trên bản đồ và bảng chuẩn bị
          "objective": "clear",              // hiện chỉ có "clear": dọn hết các đợt
          "maxAlive": 2,                     // 1–10, số quái cùng lúc (bỏ trống: dùng maxAlive chung)
          "rulesVersion": 1,                 // tăng khi đổi màn nhiều để kỷ lục cũ không bị so với luật mới
          "waves": [ { "groups": [ { "enemy": "slime", "count": 2 } ] } ]
        }
      ]
    }
  ]
}
```

- id chương và id màn theo cùng quy tắc với id quái (chữ thường, số, `_`, `-`, tối đa 32). id màn **không được trùng trong toàn bộ campaign**, vì tiến trình lưu theo id màn. Đổi id thì màn đó được coi là màn mới.
- Mỗi chương có 1–12 màn. Đợt quái của màn được kiểm tra như `waves` chung: quái phải có trong `enemies`, tối đa 30 đợt, mỗi nhóm 1–50 con.
- Khóa sai chính tả, objective lạ, id trùng hoặc quái không tồn tại đều làm cả file bị bỏ, giống các phần khác.
- Cách gộp: `campaign` được gộp theo từng khóa, còn mảng `chapters` nếu có trong file của bạn sẽ **thay toàn bộ** danh sách chương gốc (không gộp theo id). File không có `campaign` thì giữ chương gốc.
- Chơi Phiêu lưu khi đang dùng battle.json tùy chỉnh vẫn được, nhưng **không lưu tiến trình và kỷ lục**. Như vậy file tùy chỉnh không thể mở màn hay ghi đè thành tích của dữ liệu gốc.

Tiến trình nằm ở `%LOCALAPPDATA%\DesktopPet\adventure-progress.json` (`Version` 1): màn đã thắng, kỷ lục theo `màn|r<rulesVersion>|vũ khí|độ khó`, các lượt đã tính (chống tính hai lần). Mỗi lần lưu, file cũ được giữ thành `.bak`. Kỷ lục Phiêu lưu tách hẳn khỏi battle-records.json của trận tự do.

## Nâng cấp giữa các đợt (`upgrades`) và mục tiêu sinh tồn

Màn có `"upgrades": true` sẽ cho chọn thẻ sau mỗi đợt đã dọn (khi còn đợt sau). Danh sách thẻ nằm ở `upgrades`, gộp theo `id` giống `enemies`/`weapons`:

```jsonc
"upgrades": [
  { "id": "sharp", "nameVi": "Sắc bén", "nameEn": "Sharp edge", "descVi": "+20% sát thương", "descEn": "+20% damage",
    "effect": "damage", "amount": 0.2, "maxStacks": 3 }
]
```

| effect | amount | Ý nghĩa |
| --- | --- | --- |
| `damage` | 0.01–2 (phần) | sát thương vũ khí × (1 + tổng) |
| `moveSpeed` | 0.01–1 | tốc độ chạy × (1 + tổng) |
| `attackSpeed` | 0.01–0.5 | thời gian giữa hai nhát × (1 − tổng), không dưới 30% |
| `knock` | 1–500 (DIP/s) | cộng thêm vào lực đẩy lùi mỗi nhát |
| `heal` | 1–1000 (HP) | hồi ngay; không được đưa ra khi HP đầy |
| `armor` | 0.01–0.5 | sát thương nhận vào × (1 − tổng); thẻ và synergy cộng lại giảm tối đa 60% (từ 2026-10-24; trước là 70%) |
| `maxHp` | 1–500 (HP) | cộng HP tối đa; lúc chọn hồi luôn đúng lượng đó (Combat Depth mốc 2) |

`skill` (1.5, tùy chọn): id một kỹ năng; thẻ chỉ được đưa khi trận mang kỹ năng đó. `maxStacks` 0–10 (0 là không giới hạn). `endlessMaxStacks` (2026-10-24, tùy chọn, 1–10, không nhỏ hơn `maxStacks`, không cho thẻ hiếm hay thẻ không giới hạn): giới hạn riêng trong Vô tận. Tên tối đa 32 ký tự, mô tả tối đa 80. Effect lạ, amount ngoài khoảng, id trùng hoặc danh sách rỗng đều làm cả file bị bỏ.

Effect mới (2026-10-24):

| effect | amount | Ý nghĩa (bản gốc) |
| --- | --- | --- |
| `crit` | 0.01–1 | Móng vuốt: tỉ lệ một nhát ×2 sát thương (0.1, tối đa 2, Vô tận 3) |
| `execute` | 0.1–3 | Kết liễu (hiếm, Sức mạnh): + amount sát thương lên quái dưới 30% máu (0.5) |
| `comboWindow` | 0.05–2 s | Nhịp dài: thêm thời gian nối combo (0.3, tối đa 2) |
| `landingRush` | 0.05–2 | Lướt gió (hiếm, Tốc độ): được thả xuống sau khi nhấc ≥ `rescueGraceMinLiftSeconds` thì 2 s đánh nhanh + amount (0.3) |
| `regen` | 0.05–10 HP/s | Hồi phục: hồi máu khi đang đánh (không khi đang cầm, không lúc nghỉ đợt) (0.5, tối đa 2) |
| `stunCut` | 0.05–0.9 | Kiên cường: choáng trên mèo × (1 − tổng), ít nhất 10% (0.25, tối đa 2) |
| `skillCooldown` | 0.01–0.5 | Tay nhanh (tiện ích): hồi chiêu mọi kỹ năng × (1 − tổng), ít nhất 50% (0.1, tối đa 3) |
| `appCharge` | 0.01–0.5 | Sạc pin (tiện ích, chỉ trận có app): thời gian sạc icon × (1 − tổng), ít nhất 30% (0.08, tối đa 3) |
| `killCharge` | 0.005–0.2 | Hút năng lượng (tiện ích, chỉ trận có app): cộng vào `killCharge` (0.01, tối đa 3) |
| `bomb` | 0.05–2 | Bom to (tiện ích): bán kính và sát thương Bom len × (1 + tổng) (0.2, tối đa 2) |
| `shieldTime` | 0.2–5 s | Khiên dày (tiện ích): Khiên kéo dài thêm (1, tối đa 2) |

- Thẻ tiện ích = thẻ không có `group` (trừ `heal`) **hoặc thẻ nhóm Mưu mẹo** (`trick`, từ 1.5 M25: 5 thẻ tiện ích gốc đều vào nhóm này): `utilityChance` (cấp trên cùng, 0–1, gốc 0.25, từ M25 là 0.35) là tỉ lệ một thẻ như vậy chiếm một ô ngẫu nhiên trong lượt mời (luồng random riêng `BattleRandom.Utility`); chúng vẫn lấp chỗ khi nhóm hết thẻ. Thẻ app không bao giờ được mời khi trận không có app.
- Mỗi nhóm được tối đa **hai** thẻ hiếm (trước là một): khi thẻ hiếm thay chỗ thì bốc ngẫu nhiên trong các thẻ hiếm còn lấy được của nhóm.
- Bản gốc thêm: Sức mạnh Móng vuốt + Kết liễu (hiếm); Tốc độ Nhịp dài + Lướt gió (hiếm); Phòng thủ Hồi phục + Kiên cường; tiện ích Tay nhanh, Sạc pin, Hút năng lượng, Bom to, Khiên dày. Giới hạn Vô tận: Sắc bén 5, Nhanh chân 3, Ra đòn nhanh 4, Lông dày 3, Tim to 4, Móng vuốt 3 (Đẩy mạnh giữ 2).

### Nhóm thẻ, synergy và build cộng dồn (Combat Depth mốc 2)

- `group`: `defense`, `power`, `speed` hoặc `trick` (Mưu mẹo, từ 1.5 M25; bỏ trống = không thuộc nhóm). Thẻ `heal` không được có nhóm. Mỗi lần lấy một thẻ có nhóm = 1 điểm nhóm đó. Bản gốc: Defense = Lông dày + Tim to (`big_heart`, +20 HP tối đa, tối đa 2), Power = Sắc bén + Đẩy mạnh, Speed = Nhanh chân + Ra đòn nhanh.
- Luật đưa thẻ: mỗi nhóm còn thẻ chưa chạm `maxStacks` góp 1 thẻ ngẫu nhiên (thứ tự Defense, Power, Speed; Mưu mẹo không có ô riêng mà vào qua ô tiện ích, xem "Nhóm Mưu mẹo"); nhóm hết thẻ thì thẻ khác lấp chỗ. Mèo mất máu thì thẻ hồi máu thay thẻ không nhóm hoặc thẻ của nhóm build đang ít điểm nhất; HP đầy không bao giờ có thẻ hồi máu.
- `synergies` (gộp theo `id`): `{ "id", "group", "at": 1–10, "effect", "amount", "every", "armor": 0–0.5, "nameVi/En" ≤ 32, "descVi/En" ≤ 80 }`. `armor` (2026-10-24): bật synergy thì giảm thêm chừng đó sát thương nhận vào (cộng với thẻ `armor`, chung trần 60%); bản gốc Giáp gai 0.1. Đạt `at` điểm của nhóm thì bật vĩnh viễn trong build đó, banner "★ TÊN!" một lần.

| effect | amount | Ý nghĩa (bản gốc) |
| --- | --- | --- |
| `thorns` | 0.05–2 | Giáp gai: phần sát thương mèo **thực nhận** (sau giáp) dội lại quái vừa đánh. Khiên chặn thì không dội. (0.35) |
| `armorBreak` | 0.05–3 | Phá giáp: đòn combo × (1 + amount) và xuyên khả năng chặn đòn (`blockChance`, Combat Depth mốc 3). (0.3) |
| `echo` | 0.1–2, `every` 2–10 | Dư ảnh: cứ `every` nhát trúng thì thêm một đòn amount × sát thương nhát đó. (0.5, mỗi 4 nhát) |

Đòn dội lại và đòn dư ảnh không kích hoạt thêm bất kỳ hiệu ứng build nào (không có vòng lặp).

Tầng chỉ có ở Vô tận (2026-10-24, `"endlessOnly": true`; ở chế độ khác không bật và không hiện trên HUD/túi đồ/thẻ). Synergy cùng effect bật sau thay cái trước (Dư ảnh+ thay Dư ảnh).

| effect | amount | Ý nghĩa (bản gốc) |
| --- | --- | --- |
| `lastStand` | 1 | Bất khuất (Phòng thủ 6): mỗi đợt một lần, đòn làm mèo về 0 HP chỉ để còn 1 HP |
| `autoShield` | 5–120 s | Pháo đài (Phòng thủ 9): HP dưới 30% thì Khiên tự bật, mỗi amount giây một lần (30) |
| `comboStun` | 0.1–3 s | Chấn động (Sức mạnh 6): đòn combo trúng làm choáng mục tiêu × `stunFactor` (không trùm, không tinh anh miễn Đẩy) (0.6) |
| `finisher` | 0.01–0.5 | Hành quyết (Sức mạnh 9): đòn trúng mà quái thường (không trùm) còn dưới amount máu thì hạ luôn (0.1) |
| `rush` | 0.05–1 | Gió lốc (Tốc độ 6): sau đòn combo, 3 s chạy và đánh nhanh + amount (0.2) |
| `echo` (Dư ảnh+) | như `echo` | Tốc độ 9: cứ 3 đòn trúng thêm một đòn 75% |
- Build cộng dồn (Phiêu lưu): thắng màn N lưu build lúc kết thúc (không lưu thẻ hồi máu) vào `adventure-progress.json` (`Builds`, file phiên bản 2). Màn N+1, lần đầu hay chơi lại, luôn bắt đầu bằng build đã lưu của màn N; màn đầu chương bắt đầu trống. Thua không đổi build đã lưu; thắng lại màn cũ thì thay build lưu của màn đó. Chơi bằng dữ liệu tùy chỉnh thì không lưu gì. Trận tự do không có build. Thẻ trong build mà dữ liệu không còn thì bị bỏ qua; số lần vượt `maxStacks` bị cắt.
- `maxUpgrades` (1–30) trên một màn: số lần chọn thẻ tối đa trong một lượt (màn 3 gốc: 3). Không đặt: mỗi đợt dọn xong mà còn đợt sau thì có một lần chọn.

Màn sinh tồn: `"objective": "survive", "surviveSeconds": 10–600`. Các đợt ra theo thứ tự, hết đợt thì đợt cuối lặp lại cho tới hết giờ. Đồng hồ dừng khi mèo đang được cầm. `surviveSeconds` trên màn "clear" là lỗi.

## Quái hỗ trợ, pha của boss, phần thưởng (mốc 4)

- Quái hồi máu: `healAmount` (0–1000, 0 = không hồi), `healRange` (10–600), `healCooldown` (0.5–60), `healWindup` (0.2–5), `healBudget` (1–100000: tổng máu tối đa con đó có thể hồi). `marks: "cross"` vẽ dấu "+" trên thân.
- Pha của boss: `phases: [ { "at": 0.5, "summon": "mender", "count": 1 }, { "at": 0.3, "enrage": true } ]` (1–6 pha; `at` trong khoảng 0–1; mỗi pha phải gọi quân hoặc nổi giận; `count` 1–6). `summonCap` 1–10 là số quân được gọi còn sống cùng lúc. Quân được gọi không được là quái biết gọi quân. Có pha nổi giận thì cần `enragedSpeed`/`enragedWindup`.
- Phần thưởng màn: `"reward": "<id phụ kiện>"`, phụ kiện đó phải có `"reward"` trong accessories.json. Chỉ nhận một lần, chỉ với dữ liệu gốc.
- (1.5) `"unlocksSkill": "<id kỹ năng>"`: thắng màn này thì kỹ năng đó mở ở bảng chuẩn bị (nếu kỹ năng đã có tác dụng). Chương có `"skillChoice": true` thì màn của nó cho chọn bộ kỹ năng. Xem "Bộ kỹ năng mang theo".
- (2026-10-24, Hướng dẫn phiêu lưu) `tipVi`/`tipEn` (tối đa 400 ký tự, cần cả hai): màn này dạy gì, mở từ bảng chuẩn bị ("Màn này dạy gì"), không tự bật nữa.
- `"malware": false`: màn không có app độc và không có icon bị nhiễm (icon app vẫn giúp mèo). Gốc: màn 2.
- `"malwarePlan": [ { "at": 6, "kind": "ads" }, … ]` (1–12 mục, màn có app độc): gửi đúng lúc (giây của trận; màn sống sót: giây của đồng hồ, bế mèo thì đồng hồ dừng), mỗi mục một lần; `kind` là `ads`, `loading`, `update`, `virus` (virus.exe) hoặc `infect` (slime bắn virus vào icon). Chưa gửi được (không có slime, không chỗ đặt tường, không còn icon sạch) thì chờ và thử lại. Có kế hoạch thì app độc ngẫu nhiên chờ tới `"malwareFrom"` giây (0–600) và đợt không tự bốc virus nhiễm icon. Gốc: màn 3 quảng cáo 6, loading 14, cập nhật 27, virus.exe 34, nhiễm 47, ngẫu nhiên từ 50.
- Nhóm quái (hoặc thành viên squad) có `"elite": true`: mọi con của nhóm ra là tinh anh, bất kể `eliteChance`, độ khó (cả Dễ), `maxElites` hay ngân sách; loại quái phải có `"elite"`. Gốc: màn 4 có một tinh anh mỗi loại (slime, slime nhanh, slime đá, slime thảo dược) và `eliteChance` 0; màn 3 không còn tinh anh.

## Lao tới và choáng (Combat Depth mốc 1)

- Quái lao tới: `"dash": true` thay đòn thường của loại đó. Khi mèo trong `dashRange` (20–600) quái gồng `windup` giây, vạch lao màu đỏ hiện trên sàn theo hướng đã chốt lúc bắt đầu gồng; rồi lao `dashDistance` (20–800) DIPs với tốc độ `dashSpeed` (50–1500, × tốc độ độ khó). Va vào mèo trên đường lao: trúng **một lần** mỗi cú lao (`damage`, × giáp) và choáng mèo. Va chạm được kiểm tra trên cả đoạn đường mỗi frame, nên lao rất nhanh cũng không xuyên qua mèo. Mặc định: 170 / 230 / 420. Slime nhanh (`runner`) có sẵn `dash`.
- Choáng (cấp trên cùng): `stunDurationSeconds` (0–5, mặc định 1.5; 0 = chỉ mất máu, không choáng) và `stunImmunitySeconds` (0–30, mặc định 3: sau khi hết choáng không bị choáng lại trong khoảng này).
- Luật: mèo choáng đứng yên, không chém (nhát đang chém bị hủy); người dùng vẫn nhấc/kéo/ném được, kéo không xóa choáng và đồng hồ choáng vẫn chạy khi đang cầm. Pause, menu, chọn thẻ đóng băng nó. Khiên chặn cả máu lẫn choáng; mèo đang cầm/bay hoặc đang được bảo vệ sau khi thả thì cú lao trượt. Đẩy quái lúc gồng hoặc lúc lao sẽ hủy cú lao. Hết trận (thắng/thua/kết thúc) không để lại choáng.

## Chặn đòn, quái hỗ trợ đứng sau, pha khiên của boss (Combat Depth mốc 3)

- Chặn đòn: `blockChance` (0–0.9, mặc định 0 = không chặn) và `blockMaxStreak` (1–10, mặc định 2). Mỗi nhát vũ khí (thường, combo, Dư ảnh) trúng quái này được tung xác suất **một lần** (random của trận, test dùng seed được); bị chặn thì 0 sát thương, không đẩy lùi, hiện "CHẶN"/"BLOCK" và mảnh đá xám. Chặn liền `blockMaxStreak` lần thì nhát sau chắc chắn trúng. Không bao giờ bị chặn: bom len, Giáp gai, đòn combo khi có Phá giáp. Nhát bị chặn vẫn tính vào chuỗi combo. Bản gốc: slime đá 150 HP, 0.3 / 2. Trận tự do cũng có slime đá nên cũng bị ảnh hưởng (kỷ lục trận tự do không có rulesVersion).
- Hỗ trợ đứng sau: `"support": true`, `keepBack` (0–600, mặc định 110). Còn đồng minh biết đánh (quái không phải support, kể cả boss và con đang hiện ra) thì quái này **không bao giờ đánh mèo**: nó đứng phía của mình so với mèo, sau đồng minh gần mèo nhất ở phía đó `keepBack` DIPs (không có đồng minh phía đó thì cách mèo `keepBack`), đi bằng tốc độ của nó (chậm hơn mèo chạy nên mèo đuổi kịp; tới mép sân thì đứng yên). Lúc lùi nó đi xuyên qua quái khác. Vẫn hồi máu như cũ. Hết đồng minh biết đánh (kể cả khi chỉ còn 2 support) thì đánh như slime thường. Bản gốc: slime thảo dược.
- Pha khiên của boss: thêm `"shield": true` vào một pha; `summons: [ { "enemy", "count" } ]` (1–4 nhóm, mỗi nhóm 1–6) gọi nhiều loại quân trong một pha (cùng với `summon`/`count`, tổng tối đa 8). Pha khiên bắt buộc phải gọi quân. Luật:
  - Một đòn không bao giờ đưa HP boss xuống dưới mức của pha khiên kế tiếp: HP dừng ở đó, sát thương dư mất. Pha khiên bật ngay, các pha thấp hơn chờ tới khi khiên tắt.
  - Khiên bật: boss bỏ đòn đang gồng, chạy về mép sân **xa mèo** với `retreatSpeed` (10–600, mặc định 240), tới nơi hoặc sau `retreatSeconds` (0.2–10, mặc định 3) thì gọi quân của pha ở phía còn trống, trong giới hạn `summonCap`.
  - Trong khiên: boss không mất máu (kiếm, bom đều hiện "BẤT TỬ"/"IMMUNE"), Giáp gai không dội lên nó, slime thảo dược không hồi cho nó. Nó đứng giữ mép, không đi tới mèo nhưng vẫn dậm nếu mèo lại gần. Mèo tự bỏ qua boss để đánh quân; người dùng click boss thì boss vẫn được đánh dấu, mèo đánh nó lại khi khiên tắt. HUD: thanh máu boss màu vàng, ghi "(khiên: hạ hết quân!)".
  - Khiên tắt **đúng một lần** khi quân cuối cùng **của chính pha đó** bị hạ (đòn, bom hay Giáp gai đều được); quân của pha khác không giữ khiên. Không gọi được con nào (đầy `summonCap`) thì khiên tắt ngay sau khi chạy tới mép, không kẹt mãi. Pause/menu/chọn thẻ đóng băng cả pha.
  - Bản gốc Vua Slime chương 1 (`king_c1`, 600 HP): khiên ở 75% (2 slime + 1 slime nhanh), 50% (thảo dược + đá + slime nhanh) và 25% (thảo dược + đá + 2 slime, kèm nổi giận). Vua Slime của trận tự do (`king`) giữ nguyên.

## Đội hình, threat và seed (Enemy Director mốc 1)

- Nhóm trong `groups` có ba dạng (chỉ chọn một):
  - Thường: `{ "enemy": "slime", "count": 2 }`.
  - Bốc theo trọng số: `{ "pick": { "slime": 1, "runner": 1 }, "count": 3, "atLeast": { "runner": 1 } }`. Mỗi con trong `count` được bốc **một lần** lúc đợt bắt đầu (trọng số 0–100, 1–8 loại, không có boss). `atLeast` bảo đảm tối thiểu, đổi chỗ những con không bắt buộc ở vị trí ngẫu nhiên; tổng `atLeast` ≤ `count`.
  - Đội: `{ "squad": [ { "enemy": "mender", "count": 1 }, { "enemy": "rock", "count": 3 } ], "count": 1 }`. Cả đội (2–8 con, không boss) hiện cùng một khung hình ở một mép, support ở phía mép (sau), quái đánh được ở phía mèo, cách mèo ít nhất 90 DIPs khi đủ chỗ (chật thì khép sát lại, tối đa còn 1/3 khoảng cách). Đội chờ tới khi sàn còn chỗ cho **cả đội** (tính `maxAlive`); các nhóm sau chờ sau nó. Validate: đội lớn hơn `maxAlive` của màn bị từ chối.
- `threat` trên một đợt (0.5–3) và trên một màn (0.5–3): quái spawn có HP × threat, sát thương × (1 + (threat − 1) × `threatDamageShare`) (mặc định 0.5, sát thương tăng chậm hơn), nhân thêm hệ số độ khó, tính **một lần** khi quái hiện. Không đổi thời gian gồng/choáng. Trận tự do không có threat. Số đã nhân được làm tròn; Thường với threat 1 giữ nguyên số gốc.
- Màn sinh tồn: đợt cuối có thể là `"stream": { "weights": {...}, "interval": 2.6, "intervalEnd": 1.4 }`: không bao giờ hết đợt, mỗi khi sàn còn chỗ (dưới `maxAlive`) thì bốc một loại theo trọng số, khoảng cách giữa hai lần spawn giảm dần từ `interval` (đầu màn) tới `intervalEnd` (cuối giờ). Chỉ được ở đợt cuối của màn `survive` và không có `groups`.
  - `threatGrowth` (0–2, chỉ màn sinh tồn): threat cộng thêm mỗi phút đã trụ được.
  - `upgradeAt` (1–10 mốc tăng dần, nằm trong `surviveSeconds`, chỉ màn sinh tồn có `upgrades`): đưa thẻ ở các giây đó của đồng hồ thay vì sau mỗi đợt; đồng hồ dừng khi chọn; vẫn tôn trọng `maxUpgrades`.
- Seed: mỗi trận có một seed (log ghi `Battle started: Adventure stage … r<rulesVersion>, <độ khó>, <vũ khí>, seed N.`). Seed tách thành các luồng riêng: spawn (loại bốc + mép xuất hiện), thẻ, chặn đòn của đá; thêm lượt bốc ở luồng này không đổi luồng khác. `BattleSim(..., seed: N)` tái hiện lại trận (cùng thao tác).
- Chương 1 hiện tại (rulesVersion màn 1–5: 2/6/7/7/7):
  - Làm quen: đợt 1 chỉ slime; đợt 2 bốc 3 con slime 3 : nhanh 1, luôn có ít nhất 1 slime nhanh.
  - Giải vây: mỗi đợt 1–2 slime đá, phần còn lại bốc thường/nhanh 1 : 1; đợt 3 threat 1.1.
  - Cầm cự: một stream nhanh 2 : thường 1 : đá 1, 2.6 s → 1.4 s, `threatGrowth` 0.3, thẻ ở giây 20/40/60.
  - Cắt viện trợ: `maxAlive` 5; đợt 1 bốc 3 con thường/nhanh; đợt 2 và 3 có đội 1 slime thảo dược + 3 slime đá.
  - Vua Slime: đợt 2 là đội thảo dược + đá + slime; đợt 3 là Vua một mình (quân của Vua thuộc các pha khiên).

## Vai trò quái và chiến thuật (Enemy Director mốc 2)

- Luật chung (`battle.json` gốc):
  - `maxAttackers` (0–10, mặc định 2; 0 = không giới hạn): tối đa bấy nhiêu quái cùng gồng/lao một lúc.
  - `attackStagger` (0–3 s, mặc định 0.3): lần gồng mới cách lần trước ít nhất bấy nhiêu. Boss không phải chờ lượt (nhưng vẫn tính là đang đánh).
  - `incomingWarning` (0–5 s, mặc định 1): mũi tên đỏ ở mép trước khi quái phía sau xuất hiện.
- Nhóm trong đợt có `"side"`: `"a"` = mép xa mèo lúc đợt bắt đầu, `"b"` = mép còn lại, có mũi tên cảnh báo trước. Mèo đang đứng ở mép "b" (gần hơn `spawnMinDistance`) thì quái ra ở mép kia, không cảnh báo, không bao giờ đè mèo. Stream sinh tồn có `pincerAfter` (giây đồng hồ, 0 = không): từ lúc đó mỗi lần spawn đổi mép, có cảnh báo. Trong lúc cảnh báo không có spawn khác; bế mèo/Pause đóng băng cảnh báo.
- Quái:
  - `blocker`: mèo tự đi trên sàn không đi xuyên qua; mục tiêu ở sau thì mèo đánh nó trước (marker mục tiêu giữ nguyên, quái chắn có vòng xám đứt nét dưới chân). Bế mèo thả ra sau vẫn được. Mèo bị thả chồng lên quái chắn thì tự bước ra mép gần nhất (không dịch chuyển xa). Quái chắn chỉ giữ khoảng cách với quái chắn khác, quái thường lách qua được nó.
  - `blockFrontOnly`: chỉ chặn đòn từ phía nó đang quay mặt; đòn từ sau luôn trúng.
  - `turnSeconds` (0–3): thời gian quay mặt sang hướng mèo; trong lúc đó không đi, không gồng.
  - `farHealBelow` (0–0.9, chỉ quái support có hồi máu; thay `teleportBelow` từ 2026-10-21): không có đồng minh bị thương trong tầm hồi mà có đồng minh biết đánh dưới tỉ lệ HP này ở bất cứ đâu trên sân → hồi cho con yếu nhất như lần hồi thường (cùng lượng, cùng `healBudget`, cùng `healGuard`), nhưng gồng `farHealWindup` s (0.2–5, mặc định 1.2) và có vệt lá xanh đứt nét nối tới con đó. Hồi chiêu riêng `farHealCooldown` (1–60 s, mặc định 8), vẫn phải hết `healCooldown`. Support đứng yên tại chỗ. Đẩy/choáng hủy và tính trọn cả hai hồi chiêu. Không hồi xa cho support khác; đồng minh chết trước khi xong thì không mất lượng hồi. Banner lần đầu mỗi trận.
  - Support xuất hiện lẻ (không trong đội, không có `side`): đứng cạnh đồng minh biết đánh xa mèo nhất, phía xa mèo.
  - Vệ sĩ: mỗi support còn sống được giao quái chắn gần nhất; vệ sĩ đứng giữa mèo và support (cách support `keepBack` của support, gần hơn khi mèo sát), vẫn đánh mèo trong tầm. Vai trò tính lại 8 lần/giây theo vị trí mèo lúc đó (có độ trễ, không đoán trước).
  - Boss `rallyEvery` (cần pha khiên): khi khiên bật và quân của pha đã ra, cứ `rallyEvery` s vương miện sáng `rallyCharge` s rồi một quân chưa là tinh anh, **bốc ngẫu nhiên** trong quân của pha (từ 2026-10-24, cả slime thảo dược; trước đó là quân đánh gần mèo nhất, không bao giờ là support), **thành tinh anh vĩnh viễn** (từ 2026-10-21, thay cho tăng tốc): viền vàng + sao, affix tinh anh riêng của loại đó nếu có, +`rallyHp` HP tối đa (0–3, mặc định 0.5; cộng luôn vào HP hiện tại) và sát thương ×`rallyDamage` (1–3, mặc định 1.25). Không tính vào `maxElites`. Đẩy boss lúc vương miện sáng thì hủy lệnh. Mọi quân đánh được đã là tinh anh thì vương miện không sáng nữa. Banner "★ … thành tinh anh!".
  - Boss `shieldBreakStun` (0–5): khiên vỡ thì boss choáng bấy nhiêu giây, vẫn nhận sát thương.
  - Quân boss gọi ra không xuất hiện trong vòng 90 DIPs quanh mèo khi còn chỗ.
  - `marks: "leaf"`: slime thảo dược có mũ lá + túi thảo dược (không còn dấu + trên thân); lá sáng khi gồng hồi, hạt sáng bay tới đồng minh; hết lượng hồi thì lá cụp, xỉn màu. `"cross"` vẫn hợp lệ cho dữ liệu cũ.
- Bản gốc: slime đá `blocker`, `blockFrontOnly`, `turnSeconds` 0.6 (2 từ 2026-10-24), `hitScale` 1.5 (vẽ rộng/thấp, cầm khiên đá hình huy hiệu ở phía mặt, thêm vết nứt dưới 50% HP); slime thảo dược `leaf`, `farHealBelow` 0.35 / 1.2 s / 8 s (trước đây `teleportBelow`); Vua Slime chương 1 `rallyEvery` 10, `rallyCharge` 0.8, `rallyHp` 0.5, `rallyDamage` 1.25, `shieldBreakStun` 2 (trước đây `rallyEvery` 7 + tăng tốc ×1.4 trong 3 s). Màn 5 đợt 1: đá + slime từ mép xa, 2 slime nhanh từ phía sau. Cầm cự: hai phía từ giây 45. rulesVersion màn 1–5: 3/7/8/8/8 (chia lượt ra đòn áp cho mọi màn).

## Ngẫu nhiên có giới hạn: tinh anh, ngân sách, thẻ hiếm (Enemy Director mốc 3)

- Ngân sách: mỗi quái có `cost` (0–10, mặc định 1; bản gốc slime/nhanh 1, đá/thảo dược 2).
  - Đợt có `budget` (0–100, 0 = không giới hạn): nhóm cố định và đội luôn ra đủ; các con bốc theo trọng số chia phần còn lại (con bốc ra mà không vừa, có chừa chỗ cho các lượt bốc sau, thì đổi thành loại rẻ nhất của nhóm đó; `atLeast` thắng ngân sách). Phần ngân sách còn dư trả cho tinh anh.
  - Stream có `budget`: tổng `cost` của quái đang trên sàn (do đợt gửi ra, kể cả phụ phí tinh anh) không vượt số này; loại bốc ra không vừa thì đổi thành loại rẻ nhất, vẫn không vừa thì chờ. Validate: ngân sách dưới loại rẻ nhất của stream bị từ chối (stream sẽ không bao giờ spawn).
- Tinh anh: quái có `"elite": { ... }` (không cho boss) có thể thành tinh anh khi một đợt/stream gửi ra (không bao giờ là quân boss gọi): màn có `eliteChance` (0–1), không ở Dễ, dưới `maxElites` (0–5, mặc định 1) con tinh anh còn sống, còn ngân sách cho `elite.cost` (mặc định 1). Tung **một lần** lúc spawn bằng luồng ngẫu nhiên riêng (affix), nên tinh anh không đổi loại quái hay chỗ xuất hiện. Nhận diện: viền vàng quanh thân và ngôi sao vàng ở góc trên phía lưng; lần đầu mỗi loại trong trận có banner "★ … tinh anh: <descVi>".
  - `windupAdd` (0–3 s, chỉ quái lao): gồng lâu hơn. `dashSpeed` (1–3, chỉ quái lao): lao nhanh hơn ×.
  - `sureBlocks` (0–5, chỉ quái biết chặn): bấy nhiêu đòn phía trước đầu tiên chắc chắn bị chặn (không tung xác suất); bom, Giáp gai, combo có Phá giáp và đòn từ sau lưng vẫn trúng như cũ và không làm mất lượt chặn chắc chắn.
  - `firstHeal` (1–5, chỉ quái hồi máu): lần hồi đầu tiên trúng mạnh × (vẫn trong `healBudget`).
  - Chỉ số (từ 1.2.0-beta, nhân **một lần** lúc thành tinh anh, sau độ khó/threat, làm tròn như khi spawn; cũng áp cho quân boss phong tinh anh): `hp` (1–3, thêm HP tối đa và HP hiện tại tương ứng), `damage` (1–3), `attackCooldown` (0.2–1: chỉ thời gian chờ giữa hai đòn, gồng/hồi sức giữ nguyên), `healCooldown` (0.2–1) và `healBudget` (1–3) chỉ cho quái hồi máu (không đổi lượng mỗi lần hồi hay HealPower).
  - `pushImmune` (true/false): kỹ năng Đẩy không đẩy, không choáng, không ngắt gồng/hồi máu của nó; kỹ năng vẫn tốn hồi chiêu; banner "… tinh anh miễn Đẩy!" lần đầu mỗi trận. Đòn của mèo (combo, búa, thẻ Đẩy mạnh) cũng không đẩy lùi nó nhưng vẫn gây sát thương; bom vẫn như cũ.
  - `descVi`/`descEn` (≤ 60 ký tự). Affix không đổi gì thì bị từ chối.
  - Bản gốc (1.2.0-beta): slime thường `hp` 1.2, `attackCooldown` 0.65; slime nhanh gồng +0.3 s, lao ×1.4, `damage` 1.2; slime đá 1 lần chặn chắc chắn, `hp` 1.3, `pushImmune`; slime thảo dược lần hồi đầu ×2, `healCooldown` 0.85, `healBudget` 1.3. Màn 5 `eliteChance` 0.3 (màn 3 bỏ từ 2026-10-24; màn 4 đặt tinh anh bằng `"elite": true`), `maxElites` 1.
- Thẻ hiếm: thẻ có `"rare": true` (phải có `group`, `maxStacks` 1; mỗi nhóm tối đa một thẻ hiếm). Khi đưa thẻ, thẻ của mỗi nhóm có xác suất `rareChance` (0–1, mặc định 0.15) bị thay bằng thẻ hiếm của nhóm đó (nếu build chưa có). Tung bằng luồng ngẫu nhiên riêng, nên các thẻ thường vẫn như khi không có thẻ hiếm. Thẻ hiếm không bao giờ là thẻ lấp chỗ trống; tính 1 điểm cho nhóm; được mang sang màn sau như thẻ khác. Thẻ có viền vàng và dòng "★ Thẻ hiếm · một lần mỗi build"; HUD/bảng chuẩn bị ghi "◆ <tên>".
  - `shell` (Mai rùa, Phòng thủ; `amount` 3–60 s): đòn tiếp theo trúng mèo bị chặn trọn (không mất máu, không choáng), rồi mọc lại sau `amount` giây mèo đứng dưới sàn (không hồi khi đang bế). Khiên bong bóng vẫn được tính trước.
  - `opener` (Đòn mở màn, Sức mạnh; `amount` 0.1–3): nhát vũ khí đầu tiên trúng mỗi quái gây thêm `amount` (1 = ×2). Nhát bị chặn không tính là đã mở. Dư ảnh lặp lại nhát chém không kèm thưởng này.
  - `momentum` (Chạy đà, Tốc độ; `amount` 0.1–3): nhát chém sau khi mèo tự chạy ≥ 140 DIPs trên sàn (kể từ nhát trước) gây thêm `amount` (bản gốc +80%). Bế/kéo mèo không tính là chạy.
- Vẽ (2026-10-20, sau phản hồi người dùng): quái chắn vẽ ở lớp sau cùng, quái hỗ trợ trên cùng (click chỗ chồng nhau chọn con trên cùng); quái chắn đè lên quái hỗ trợ thì mờ đi một chút; mỗi quái có viền tối mảnh ngoài viền trắng để tách khối khi chồng nhau. Khoảng cách giữa các quái không đổi.
- Cân bằng chương 1 (mô phỏng, không phải chơi thật): Dễ `enemyHp` 0.65, `enemyDamage` 0.5 (trước đây 0.75/0.6); `threat` màn 2–5: 1.6 / 1.7 / 1.8 / 1.6; ngân sách stream màn 3 là 7, các đợt màn 4–5 chừa đúng chỗ cho một tinh anh. rulesVersion màn 1–5: 4/8/9/9/9.

## Vua nhảy về mép, báo trước quân gọi ra, hồi máu theo threat (sau Enemy Director mốc 3)

- Mép của pha khiên (mọi boss có `shield`): mép **đối diện nửa sân Vua đang đứng** (Vua ở nửa phải → mép trái); nếu mèo cũng đang ở nửa có mép đó thì dùng mép còn lại, để Vua không bao giờ đáp cạnh mèo. Mép khóa suốt pha.
- `retreatLeap` (cần pha khiên): thay cho đoạn chạy, Vua nhún `leapCrouch` s (0.1–2; Đẩy/choáng lúc nhún chỉ làm nó chờ) rồi bay theo vòng cung `leapSeconds` s (0.3–3) tới mép, cao tới 80 DIPs (thấp hơn khi nhảy ngắn), bóng ở lại dưới sàn; đáp có bụi, không gây sát thương. Suốt cú nhảy vẫn bất tử; Pause/menu/chọn thẻ đóng băng nó; quái khác không bị nó đẩy khi nó đang bay. `retreatSpeed`/`retreatSeconds` chỉ dùng khi không có `retreatLeap`.
- `summonWarning` (0–3 s, cần pha khiên): quân của pha được báo trước bằng vòng tím đứt nét có dấu "!" ở đúng chỗ chúng sẽ hiện; khiên giữ nguyên trong lúc báo. Hết trận giữa lúc báo thì quân không ra.
- Quân của pha khiên xếp theo vai trò từ Vua ra phía mèo: quái hỗ trợ sát Vua, quái chắn, quái thường, quái lao ngoài cùng; vẫn không đè mèo (≥ 90 DIPs) và tôn trọng `summonCap`. Đá trong quân gọi ra làm vệ sĩ cho slime thảo dược như ở đợt thường.
- Lệnh phong tinh anh: con được chọn lúc vương miện bắt đầu sáng (gần mèo nhất, không phải hỗ trợ, chưa là tinh anh); trong lúc sáng có tia vàng đứt nét nối vương miện với nó và vòng vàng dưới chân nó. Con đó chết trước khi hết sáng thì lệnh đi tới con gần mèo nhất còn lại.
- Hồi máu của quái hỗ trợ: lượng hồi và `healBudget` nhân cùng hệ số máu quái (độ khó × threat lúc spawn), làm tròn; Thường threat 1 giữ nguyên số.
- Bản gốc: Vua chương 1 `retreatLeap` (nhún 0.35 s, bay 0.9 s), `summonWarning` 0.6; ba pha khiên 75/50/25% (xem trên; trước 2026-10-21 là 70%: 2 slime + 1 slime nhanh và 40%: thảo dược + đá + 2 slime nhanh). Slime thảo dược hồi 35 mỗi 3.5 s (gồng 0.9 s), tổng 210. Màn 5 `threat` 1.75. rulesVersion màn 4–5: 10. Vua trận tự do giữ nguyên.

## Chiến thuật quái (sau Enemy Director mốc 3, mỗi hành vi một công tắc)

Mỗi khóa tắt được riêng trong battle.json của người dùng (ví dụ `"enemies": [ { "id": "runner", "hitAndRun": 0 } ]`).
- `healGuard` (quái hồi máu): trong lúc gồng hồi, đồng minh được hồi **miễn mọi sát thương** (kiếm, búa, combo, bom, Giáp gai, Dư ảnh): vòng lá xanh quanh nó, trúng đòn hiện "MIỄN"/"IMMUNE" xanh. Đẩy/choáng quái hồi máu thì hết miễn ngay. Không bao giờ cho boss. `healGuardRepeat` (0–10 s, mặc định 2): cùng một đồng minh chỉ được miễn lại sau bấy nhiêu giây kể từ lúc lần hồi đó lẽ ra xong. Mèo tự bỏ qua quái đang miễn nếu còn quái khác (mục tiêu click vẫn giữ). Banner lần đầu mỗi trận.
- `fleeSpeed` (1–4, quái hỗ trợ): khi đang lùi xa mèo, tốc độ ×1 nếu mèo cách ≥ 200 DIPs, tăng dần tới ×`fleeSpeed` khi mèo cách ≤ 60 DIPs. Khi đi lại gần mèo không tăng.
- `hideBehindGuard` (quái hỗ trợ): đứng `keepBack` sau **con đá vệ sĩ của nó** thay vì sau đồng minh gần mèo nhất. Không có đá vệ sĩ thì vẫn đứng `keepBack` sau đồng minh biết đánh gần mèo nhất ở phía nó (slime, slime nhanh…).
- `hitAndRun` (0–600 DIPs, quái lao): lao xong (hồi xong) thì chạy thẳng ra xa mèo bấy nhiêu DIPs (tới mép thì thôi) rồi mới quay lại.
- Bản gốc: slime thảo dược `healGuard` (2 s), `fleeSpeed` 2.5 (28 → 70 DIPs/s, mèo chạy ~130), `hideBehindGuard`; slime nhanh `hitAndRun` 150. Các hành vi này cũng có trong trận tự do (slime và slime nhanh dùng chung).

## Gọn lại chiến thuật quái (2026-10-21)

- Người dùng chốt sau khi xem sơ đồ quái: slime thường bỏ "lùi về chỗ hồi" (`fallBackBelow`) và "vòng ra sau" (`flank`, `flankSpeed`, `maxFlankers`), chỉ còn đi tới, gồng, đánh. Lý do: mèo chạy ~130 DIPs/s và chém ~33 sát thương/s, nên cả hai chiến thuật bắt slime đi bộ qua tầm đánh 76 DIPs và gần như luôn chết giữa đường.
- Slime thảo dược bỏ dịch chuyển, thay bằng hồi máu xa (`farHealBelow`, xem mục vai trò quái ở trên). Không có đá thì nó vẫn nấp sau đồng minh biết đánh gần mèo nhất (hành vi có sẵn, nay có test).
- Vua Slime chương 1: 600 HP (trước 450), ba pha khiên 75/50/25% (pha cuối kèm nổi giận), lệnh của vương miện phong quân thành tinh anh (`rallyHp`, `rallyDamage`) thay cho tăng tốc, mỗi 10 s.
- File battle.json **của người dùng** còn các khóa cũ (`fallBackBelow`, `flank`, `flankSpeed`, `maxFlankers`, `teleportBelow`, `teleportWindup`, `teleportCooldown`, `rallySeconds`, `rallySpeed`) vẫn dùng được: các khóa đó bị bỏ qua và ghi một dòng log, phần còn lại của file vẫn áp dụng. File gốc nhúng trong exe không được có các khóa này.
- rulesVersion màn 1–5: 4/10/11/12/12.

## Liên kết quái (`link`) (bản 1.5, mốc M01, 2026-10-28)

Khung cho quái liên kết của 1.5 (docs/UPDATE-1.5.md). Code: `src/DesktopPet.Core/PetBattle.Links.cs` (luật), `src/DesktopPet/PetBattleView.Links.cs` (vẽ), test `tests/DesktopPet.Tests/BattleLinkTests.cs` (quái giả). **battle.json gốc chưa có quái nào dùng `link`**: chương 1 và mọi chế độ hiện tại chạy y như trước (mô phỏng cân bằng giống hệt từng dòng). Quái thật dùng khung này làm ở M09–M12.

```json
{ "id": "chain_slime", "link": { "kind": "tether", "range": 200, "share": 0.5, "breakStun": 1, "relink": 6 } }
```

Mỗi quái có tối đa một `link`. Khóa chung: `kind`, `enabled` (mặc định `true`), `range` (20–800 DIPs, mặc định 200), `breakStun` (0–5 s, mặc định 1), `relink` (0–60 s, mặc định 6). Boss không có `link`.

| `kind` | Tác dụng | Khóa riêng | Cách phá / thưởng |
|---|---|---|---|
| `tether` (dây, Q4) | Tự buộc dây vào đồng minh gần nhất trong `range` (không phải boss, không phải con giữ dây khác; ưu tiên con chưa bị ai buộc, từ M12 không còn thì con mới có 1 dây, dây thứ hai gánh phần của nó trên phần còn lại); gánh `share` (0.05–0.95, mặc định 0.5; tinh anh `eliteShare` nếu có) mọi sát thương đồng minh nhận; đi sát ngay sau đồng minh, phía xa mèo | `share`, `eliteShare`, `cutSpeed` | `CutLink` (mèo đang cầm/ném xuyên dây nhanh ≥ `cutSpeed`: M10; Kéo cắt: M06) hoặc hạ con giữ dây: đồng minh choáng `breakStun` s. Dây giãn quá `range` × 1,5 thì tự đứt, không thưởng. Sau `relink` s mới buộc lại |
| `rhythm` (nhịp, Q3) | Mỗi `beat` s (0.2–3, mặc định 0.8) một nhịp; nhịp thứ `beats` (2–8, mặc định 4; tinh anh `eliteBeats` nếu có) mọi quái trong `range` (cả nó) đang đánh tới mèo được thì **gồng cùng lúc**, bỏ qua lượt ra đòn và hồi chiêu | `beat`, `beats`, `eliteBeats` | Bị choáng (Đẩy, búa…) thì đếm lại từ 0. Không gồng khi mèo đang được cầm hay đang `landingGrace`; không áp dụng cho boss, quái hỗ trợ, con đang cưỡi, con đang hoảng |
| `ride` (cưỡi, Q6) | Cưỡi lên quái loại `mount` (bắt buộc, quái đã biết, không phải boss, không tự cưỡi) trong `range`, chưa bị ai cưỡi; được chở đi, vẽ trên lưng; mèo không nhắm và không click chọn được con đang cưỡi (đánh con bị cưỡi). Cách mèo `dropRange` (20–400, mặc định 90) thì nhảy xuống, không choáng | `mount`, `dropRange` | Hạ con bị cưỡi hoặc `CutLink`: con cưỡi rơi tại chỗ, choáng `breakStun` s |
| `merge` (nhập, Q5/Q7) | `count` (2–6, mặc định 3) con cùng loại đứng trong `range` của nhau liên tục `mergeSeconds` (0.5–10, mặc định 2) s thì nhập thành một quái `into` (bắt buộc, quái đã biết, không phải boss, không tự nhập), máu = tổng máu hiện tại, máu tối đa = tổng máu tối đa. Không tính là hạ quái, không kết thúc đợt | `count`, `mergeSeconds`, `into` | Bị choáng (Đẩy…) thì đồng hồ của con đó về 0; tách xa nhau cũng vậy |
| `aura` (hào quang, Q8) | Đồng minh trong `range` đi nhanh ×`speed` (1–3, mặc định 1.3; tinh anh `eliteSpeed` nếu có, M12); lao (dash) không nhanh thêm | `speed`, `eliteSpeed` | Hạ nó: mọi quái khác (trừ boss) **hoảng** `breakStun` s: chạy ra xa mèo, không đánh |

- **Tối đa 2 loại liên kết mỗi trận** (`EnemyLinks.MaxKinds`): lúc nạp dữ liệu, mỗi danh sách đợt (trận tự do, màn Phiêu lưu, Viễn chinh) mà tính cả quái bốc ngẫu nhiên, quân boss gọi, quái được gọi, `mount`, `into` có hơn 2 loại `link` thì file bị từ chối. Lúc chạy (đợt sinh tự động như Vô tận) hai loại đến trước được dùng, quái mang loại thứ 3 đánh như quái thường.
- **Tắt** một liên kết trong battle.json của người dùng: `"enemies": [ { "id": "chain_slime", "link": { "enabled": false } } ]` (gộp với file gốc, phần còn lại giữ nguyên).
- Hình: dây nhầy xanh võng nhẹ từ con giữ dây tới đồng minh; vòng vàng đứt nét trên sàn quanh con hào quang; vòng cam chấm trên sàn quanh con nhịp và các chấm nhịp trên đầu (nhịp ngay trước khi gồng chuyển đỏ và nháy); thanh tím đếm ngược trên con sắp nhập; giọt mồ hôi xanh trên con đang hoảng. Chỉ vẽ nét viền, không tô nền, nên click vào khoảng trống vẫn xuyên xuống desktop.
- Pause/menu/chọn thẻ đóng băng mọi đồng hồ liên kết (như cả trận). Không dùng luồng ngẫu nhiên nào, nên seed cũ cho đúng trận cũ.
- Sự kiện mới cho âm thanh/hiệu ứng sau này: `LinkFormed`, `LinkCut`, `LinkSnapped`, `LinkBeat`, `LinkStrike`, `Merged`, `Dismounted`, `Panicked` (chưa gắn âm thanh ở M01).
- Đã bỏ loại `bounce` khỏi thiết kế (người dùng duyệt 2026-10-28): Q2 lò xo là bẫy, không có liên kết riêng.

## Quái trên không và tương tác ném (`hover`, `throwHit`) (bản 1.5, mốc M03, 2026-10-28)

Khung cho ngoại lệ Battle Mode 1.5 (docs/APPROVED-EXCEPTIONS.md → Battle Mode). Code: `src/DesktopPet.Core/PetBattle.Held.cs`, test `tests/DesktopPet.Tests/BattleHeldTests.cs` (quái giả). **battle.json gốc chưa có quái nào dùng hai khóa này**: mọi trận hiện tại chạy y như trước (mô phỏng cân bằng giống hệt từng dòng). Quái thật dùng ở M14 (slime bay), M15 (slime băng, bám tường); cắt dây bằng mèo đang cầm/ném làm ở M10 (mục Slime nhạc trưởng và Slime xích).

```json
{ "id": "flyer", "hover": 160 }
{ "id": "ice_slime", "throwHit": { "minSpeed": 600, "damage": 30, "stun": 1.5 } }
```

- **`hover`** (0–2000 DIPs, mặc định 0): quái lơ lửng, đáy thân cách sàn chừng đó. Mèo dưới sàn không nhắm, không click chọn được, đòn lan của combo không trúng nó; nó không đánh gần mèo và không chen chỗ quái dưới sàn. Không dùng cho boss, quái chắn, lao, Slam hay quái có `link`.
- **Mèo đang cầm tự vung vuốt** (chỉ khi đã kéo quá ngưỡng ~6 DIPs, không phải chỉ nhấn): quái `hover` gần nhất có mép thân cách tâm mèo ≤ tầm vũ khí như dưới sàn (`range` − nửa bề rộng slime). Là đòn thường của vũ khí: tính thẻ tăng sát thương, Cổ vũ, Liều lĩnh, Móng vuốt (chí mạng), Kết liễu; **không** combo, Mở màn, Đà chạy, phản đòn, Dư ảnh, đánh lan hay đẩy lùi. Hồi chiêu như đòn thường (có Cổ vũ, vòng loading), đồng hồ riêng nên đòn dưới sàn không đổi. Thả mèo trước lúc trúng thì đòn hủy. Quái dưới sàn không bao giờ bị mèo đang cầm đánh.
- **`throwHit`**: mèo **đang bay** sau cú ném (không phải đang cầm) đi xuyên thân quái (nở thêm nửa bề rộng mèo) với tốc độ ≥ `minSpeed` (100–5000 DIPs/s, mặc định 600) thì quái mất `damage` (0–1000, không bị chặn) và choáng `stun` s (0–5, theo `stunFactor`; boss/tinh anh miễn Đẩy không choáng). Mỗi quái một lần mỗi cú ném; xét theo đoạn bay giữa hai khung hình nên ném nhanh không lọt qua. Phải có `damage` hoặc `stun`. Quái không có `throwHit` không bị cú ném ảnh hưởng.
- Mèo vẫn không nhận sát thương khi đang cầm/bay. Pause/menu/chọn thẻ/hướng dẫn đứng hình: không vung, không va chạm. Physics kéo/ném không đổi: PetWindow chỉ đọc vị trí/vận tốc (`CatPose`).
- Sự kiện: `HeldHit`, `ThrowHit` (Value: id quái; chưa gắn âm thanh riêng, đòn khi cầm dùng tiếng trúng đòn thường). Hình: vệt chém hiện cả khi mèo đang bị nhấc. Vẽ quái bay là việc của M14 (cửa sổ riêng).

## Hiệu ứng trên quái: độc, đóng băng, dính (`onHit`) (bản 1.5, mốc M04, 2026-10-28)

Code: `src/DesktopPet.Core/PetBattle.Status.cs` (luật), `src/DesktopPet/PetBattleView.Status.cs` (vẽ), test `tests/DesktopPet.Tests/BattleStatusTests.cs` (quái giả) và ảnh UiChecks. **battle.json gốc chưa có vũ khí hay thẻ nào gây hiệu ứng**: mọi trận hiện tại chạy y như trước (mô phỏng cân bằng giống hệt từng dòng). Bình xịt (M06), vũ khí mới (M08) và thẻ Mưu mẹo (J1) sẽ dùng khung này; trong code là `BattleSim.ApplyStatus(quái, loại, giây, power)`.

```json
"weapons": [ { "id": "fang", "onHit": { "status": "poison", "chance": 0.3, "seconds": 3, "power": 6 } } ]
"upgrades": [ { "id": "glue_paw", "effect": "onHit", "amount": 0.2, "onHit": { "status": "stick", "seconds": 1.2 },
  "nameVi": "Vuốt keo", "nameEn": "Glue paw", "descVi": "20% đòn trúng làm quái dính 1,2 s", "descEn": "20% of hits stick an enemy 1.2 s" } ]
```

- **`onHit`** trên vũ khí hoặc thẻ: mỗi đòn vũ khí **trúng** (dưới sàn, combo, hay mèo đang cầm đánh quái trên không; không tính đòn lan, Dư ảnh, phản đòn, bom, app) gieo `chance` (0,01–1, mặc định 1) để gây hiệu ứng. Thẻ: `"effect": "onHit"`, **`amount` là tỉ lệ** (0,01–1), không ghi `chance` trong object; lấy lại cùng thẻ thì cộng tỉ lệ (tối đa 1). Mỗi nguồn gieo riêng trên luồng ngẫu nhiên riêng (`BattleRandom.Status`), nên các lần gieo khác của trận không đổi.
- **Độc** (`poison`): `seconds` 0,5–20, `power` 2–200 máu/giây (× sát thương của build lúc gây). Trừ mỗi 0,5 s, số trừ màu tím nhỏ (số < 1 không hiện). Tối đa **3 lớp**, mỗi lớp cộng thêm `power`; trúng lại thì thời gian lấy giá trị dài hơn (lớp thứ 4 chỉ làm dài thời gian). Không bị đá chặn, không đẩy lùi/choáng, không kích hoạt thẻ cộng thêm (Mở màn, Dư ảnh, Gai…). Quái chết vì độc vẫn tính là mèo hạ. Boss khiên/quái đang được hồi máu bảo vệ: không mất máu, thời gian vẫn chạy, không hiện "MIỄN".
- **Đóng băng** (`chill`): `seconds` 0,5–10, `power` 0,05–0,9 = chậm bao nhiêu (0,5: còn nửa tốc độ). Đi, lao, gồng đòn, hồi đòn, gồng hồi máu và thời gian chờ giữa các đòn đều chậm theo. Không cộng dồn: giữ mức chậm mạnh hơn và thời gian dài hơn.
- **Dính** (`stick`): `seconds` 0,2–5, không có `power`. Quái đứng yên: không đi, không lao, không bị đẩy lùi, quái khác không đẩy nó được (chúng tự dạt ra); vẫn quay mặt và đánh mèo trong tầm. Không cộng dồn (giữ thời gian dài hơn); hết dính thì miễn dính 1,5 s.
- **Boss**: nhận nửa độc, nửa mức chậm, dính nửa thời gian. Không gây hiệu ứng lên quái đang ra sân, đã bị hạ, đang trong pha khiên, đang nhảy; quái đang cưỡi không bị dính. Bị hạ thì mất hết hiệu ứng.
- Thời gian chỉ chạy khi trận chạy: Pause, menu, chọn thẻ, túi đồ, hướng dẫn đứng hình đều đóng băng hiệu ứng.
- Hình: độc phủ tím và bọt nổi lên; đóng băng phủ sương xanh nhạt và tinh thể băng; dính có vũng keo màu hổ phách dưới chân và sợi keo hai bên. Trên đầu mỗi hiệu ứng một huy hiệu tròn có vòng đếm thời gian còn lại (đẩy chữ tinh anh và mũi tên chọn lên cao hơn). Sự kiện `StatusApplied` (Value: loại), chưa gắn âm thanh.

## Đèn laser và Quạt (bản 1.5, mốc M05, 2026-10-28)

Code: `src/DesktopPet.Core/PetBattle.Tools.cs` (luật), `src/DesktopPet/PetBattleView.Tools.cs` (vẽ), test `tests/DesktopPet.Tests/BattleToolsTests.cs` (quái giả), ảnh UiChecks và bước hướng dẫn trong TutorialChecks. Số liệu ở cấp trên cùng của battle.json:

| Khóa | Giới hạn | Gốc | Ý nghĩa |
| --- | --- | --- | --- |
| `laserCooldown` | 1–120 s | 14 | Hồi chiêu Đèn laser |
| `laserSeconds` | 0,5–15 s | 4 | Chấm đỏ nằm bao lâu |
| `laserLureRange` | 20–2000 DIPs | 300 | Quái cách chấm trong khoảng này thì bị dụ |
| `laserDps` | 0–100 | 3 | Máu/giây quái đứng trên chấm mất |
| `fanSteamSeconds` | 0,5–30 s | 3 | Thổi liên tục được bao lâu (thanh hơi) |
| `fanRefillSeconds` | 1–120 s | 6 | Cạn hơi thì chờ bao lâu (là hồi chiêu của Quạt); khi chưa cạn, hơi cũng hồi với nhịp này |
| `fanRange` | 20–1000 DIPs | 200 | Tầm gió quanh con trỏ |
| `fanSpeed` | 0–1500 DIPs/s | 220 | Tốc độ gió đẩy (× `pushFactor` của quái) |
| `fanBumpDamage` | 0–100 | 4 | Sát thương khi quái bị thổi va quái khác hoặc va mép |

- **Đèn laser** (chọn ô rồi click **một chỗ** trên dải trận, kể cả trúng slime): chấm đỏ đứng yên `laserSeconds`. Quái trong `laserLureRange` mà tự đi được (không phải boss, không cưỡi, không bay, không bị dính, không đang gồng hồi máu, không trong pha khiên) đi về chấm thay vì về mèo và **không gồng đòn mới**; đòn đã gồng vẫn đánh. Quái đứng trên chấm mất `laserDps` máu mỗi giây (trừ mỗi 0,5 s). Có `LaserShows(x, nửa rộng)` cho slime bóng ma/ảo ảnh (M13).
- **Quạt** (chọn ô rồi **giữ chuột** trên dải trận): gió thổi quái trong `fanRange` ra xa con trỏ, càng gần càng mạnh (ở mép tầm còn 25%), nhân `pushFactor`; tinh anh miễn Đẩy chỉ bị 20%; boss, quái dính, quái cưỡi, quái bay không bị thổi. Quái bị thổi đủ nhanh va vào quái khác (không cùng bay theo) hoặc vào mép/tường "Cập nhật" thì cả hai mất `fanBumpDamage`, mỗi con tối đa một lần mỗi giây. Quái có nhịp trống (liên kết `rhythm`) bị gió thổi thì mất đếm nhịp. Rê chuột khi giữ thì gió đi theo. Thả chuột thì ngừng nhưng **Quạt vẫn được chọn**; cạn hơi thì tự cất và chờ `fanRefillSeconds`. Vòng quét trên ô Quạt là phần hơi đã dùng. Làm khô vũng/tách bầy sẽ có cùng quái thật (M09/M11).
- **Sát thương** của cả hai là sát thương kỹ năng: không bị đá chặn, có Kết liễu, quái chết tính là mèo hạ; boss nhận 50%. Không kích hoạt thẻ cộng thêm khác.
- **Luật chung như kỹ năng cũ:** đang cầm mèo thì kỹ năng chờ (Quạt ngừng, hơi không hồi; chấm laser đã đặt vẫn chạy hết giờ như Cổ vũ); nhấc mèo thật thì bỏ chọn. Pause, menu, chọn thẻ, túi đồ, hướng dẫn đứng hình đóng băng chấm và hơi. Kết thúc trận xóa chấm và gió. Trên dải trận: dấu × quảng cáo và app độc nhận click trước; quảng cáo che phần sau nó. Click trúng mèo luôn là nhấc mèo (mèo là cửa sổ riêng).
- **Được mang ở đâu:** theo người dùng chốt 2026-10-28, **trận tự do** mang được mọi kỹ năng đã chạy (Laser, Quạt) ngay; Phiêu lưu, Viễn chinh, Vô tận mở bằng `unlocksSkill` (chương 2 từ M21; màn mở thì cho mượn). Lựa chọn lưu chung `BattleLoadout`: chế độ khác đọc bộ có Laser/Quạt chưa mở thành 5 kỹ năng cũ.
- **Hướng dẫn đứng hình:** bước `laser` và `fan` (kiểu "làm") chỉ chạy trong màn có `unlocksSkill` đúng kỹ năng đó và khi trận mang kỹ năng ấy: sáng ô, chọn xong thì sáng khoảng sàn quanh mèo; dùng xong là hết bước. Chương 1 không có màn như vậy nên không đổi. Từ M21 màn dạy tự cho mượn kỹ năng (mục "Chương 2").
- Hình: chấm đỏ có quầng sáng và vòng đếm ngược, đốm đỏ nhấp nhô trên đầu quái bị dụ; Quạt là cánh quạt xanh quay ở chỗ con trỏ và vệt gió chạy ra hai bên. Âm thanh dùng cue có sẵn (`LaserOn` lấp lánh, `FanOn` vút, `FanBump` cộc, `FanSpent` keng). Icon ô giữ bản vẽ từ M02.

## Kẹp gắp, Kéo cắt, Bình xịt nước (bản 1.5, mốc M06, 2026-10-28)

Code: `src/DesktopPet.Core/PetBattle.Grabber.cs` (Kẹp gắp), `src/DesktopPet.Core/PetBattle.Tools.cs` (Kéo cắt, Bình xịt), `src/DesktopPet/PetBattleView.Tools.cs` (vẽ), test `tests/DesktopPet.Tests/BattleHandToolsTests.cs` (quái giả, cả đổi kỹ năng ở điểm nghỉ), ảnh UiChecks, bước hướng dẫn trong TutorialChecks, điểm nghỉ trong ExpeditionChecks. Số liệu ở cấp trên cùng của battle.json:

| Khóa | Giới hạn | Gốc | Ý nghĩa |
| --- | --- | --- | --- |
| `grabberCooldown` | 1–120 s | 12 | Hồi chiêu Kẹp gắp (tính từ lúc thả) |
| `grabberHoldSeconds` | 0,5–15 s | 3 | Giữ slime lâu nhất; quá thì slime tự tuột (rơi, không tính ném) |
| `grabberMaxScale` | 0,5–2,3 | 1,3 | Loại quái có `scale` lớn hơn thì không gắp được |
| `grabberHitDamage` | 0–100 | 8 | Sát thương khi slime bị ném trúng slime khác (cả hai) hoặc trúng mép (nó) |
| `grabberStun` | 0–5 s | 1 | Choáng của con bị trúng, và của con bị ném khi đáp |
| `grabberThrowSpeed` | 50–1500 DIPs/s | 300 | Tốc độ con trỏ lúc thả để tính là "ném"; chậm hơn thì chỉ thả rơi |
| `scissorsCooldown` | 1–120 s | 8 | Hồi chiêu Kéo cắt |
| `scissorsDamage` | 0–100 | 6 | Sát thương mỗi slime nằm trên nét cắt |
| `scissorsMinLength` | 10–400 DIPs | 40 | Nét ngắn hơn thì không tính (kỹ năng vẫn được chọn) |
| `sprayTankSeconds` | 0,5–30 s | 4 | Xịt liên tục được bao lâu (thanh nước) |
| `sprayRefillSeconds` | 1–120 s | 8 | Cạn nước thì chờ bao lâu (là hồi chiêu); khi chưa cạn, nước cũng hồi với nhịp này |
| `sprayRange` | 10–500 DIPs | 70 | Tầm xịt quanh con trỏ (tính tới thân slime) |
| `sprayDps` | 0–100 | 2 | Máu/giây slime bị xịt mất (trừ mỗi 0,5 s) |
| `sprayChill` | 0,05–0,9 | 0,35 | Slime bị xịt chậm đi bao nhiêu (hiệu ứng đóng băng của M04) |
| `spraySoakSeconds` | 0,5–10 s | 1,5 | Ướt (và đóng băng) bao lâu sau lần xịt cuối |

- **Kẹp gắp** (chọn ô rồi **nhấn giữ một slime** và kéo): chỉ gắp slime nhỏ (`scale` ≤ `grabberMaxScale`), không gắp **đá** (`blocker`), **boss**, quái bay hay quái đang nhảy. Slime bị gắp treo theo con trỏ (cao tối đa 100 DIPs trên sàn), không đi, không đánh (đòn đang gồng, hồi máu đang gồng bị hủy), mèo không nhắm nó, không chen ai; đang cưỡi thì rơi khỏi thú cưỡi, đang cõng thì con cưỡi rơi xuống; dây nhầy (liên kết `tether`) vẫn giữ. **Thả chuột:** con trỏ đang vung đủ nhanh (`grabberThrowSpeed`) thì slime bị **ném** theo hướng và tốc độ đó (luôn bật lên một chút để bay thành vòng cung), rơi theo trọng lực; trúng slime khác đầu tiên trên đường bay thì **cả hai mất `grabberHitDamage`** (boss nhận 50%), con kia choáng `grabberStun` giây, con bị ném rơi thẳng xuống; trúng mép/tường "Cập nhật" thì chỉ nó mất máu; đáp đất thì nó choáng `grabberStun` giây. Thả chậm thì slime chỉ rơi xuống, không ai mất máu. Hồi chiêu bắt đầu lúc thả. Kéo S5/S8 xuống, lôi S6 lên làm cùng các quái đó (M14/M15).
- **Kéo cắt** (chọn ô rồi **kéo chuột thành một nét** trên dải trận, thả chuột là cắt): nét dài ít nhất `scissorsMinLength` DIPs; mọi dây nhầy (`tether`) nét đi qua bị cắt (như `CutLink`: đồng minh choáng `breakStun`, có thưởng); mọi slime có thân nằm trên nét mất `scissorsDamage` một lần. Nét ngắn hơn không làm gì và Kéo cắt vẫn được chọn. Dây được coi như hình vẽ (từ thân tới thân, võng ở giữa, không thấp hơn mặt sàn), nét chỉ cần đi gần dây vài DIPs. Chọc vỡ bong bóng S2 làm cùng quái đó (M13).
- **Bình xịt nước** (chọn ô rồi **giữ chuột**, như Quạt): slime trong `sprayRange` quanh con trỏ bị **ướt** (vẽ nhỏ lại ~14%, giọt nước chảy trên thân), bị đóng băng `sprayChill` (hiệu ứng M04, boss một nửa) và mất `sprayDps` máu/giây; hết xịt thì khô sau `spraySoakSeconds`. Rê chuột khi giữ thì vòi đi theo, kể cả lên cao (tầm tính theo cả chiều dọc). Thả chuột thì ngừng nhưng vẫn được chọn; cạn nước thì tự cất và chờ `sprayRefillSeconds`; vòng quét trên ô là nước đã dùng. "Ướt" (`Wet`) là móc cho băng mềm (S7, M15) và gai nhím xẹp (S9, M16).
- **Luật chung** như Đèn laser và Quạt: sát thương kỹ năng (không bị đá chặn, có Kết liễu, quái chết tính là mèo hạ, boss 50%); đang cầm mèo thì kỹ năng chờ (Kẹp gắp thả slime ra không tính ném, Bình xịt ngừng); nhấc mèo thật thì bỏ chọn; Pause, menu, chọn thẻ, túi đồ, hướng dẫn đứng hình đóng băng (slime đang gắp đứng yên giữa không trung). Kết thúc trận hoặc dải trận đổi chỗ thì thả mọi slime xuống. Click trúng mèo luôn là nhấc mèo (mèo là cửa sổ riêng); thứ tự trên dải trận như cũ: dấu × quảng cáo, app độc, rồi kỹ năng.
- **Được mang ở đâu:** như Laser/Quạt: trận tự do mang ngay; Phiêu lưu, Viễn chinh, Vô tận mở bằng `unlocksSkill` (chương 2 từ M21).
- **Hướng dẫn đứng hình:** bước `grabber`, `scissors`, `spray` chỉ chạy ở màn có `unlocksSkill` đúng kỹ năng đó và khi trận mang nó (như `laser`/`fan`).
- Hình: càng kẹp kim loại trên dây thả từ mép trên dải trận, slime bị gắp hơi kéo dài, bóng dưới sàn nhỏ dần theo độ cao; nét cắt là đường đứt nét trắng viền tối với cây kéo mở ở đầu con trỏ; Bình xịt là bình xanh vòi vàng, vòng chấm xanh là tầm xịt, giọt nước bắn ra. Âm thanh dùng cue có sẵn (`Grabbed` bóp, `GrabReleased` vút, `GrabHit` cộc, `Snipped` vút, `SprayOn` bóp, `SpraySpent` keng). Icon ô giữ bản vẽ từ M02.

## Mèo nổi giận (bản 1.5, mốc M07, 2026-10-28)

Tư thế trận, **không phải** cảm xúc Angry (cảm xúc vẫn bị khóa trong trận như cũ). Code: `src/DesktopPet.Core/PetBattle.Rage.cs` (luật), thanh nộ trong `src/DesktopPet/PetBattleHud.cs`, hình trong `src/DesktopPet/PetRenderer.cs` (+ bảng màu đỏ `CoatPalette.Enraged`), test `tests/DesktopPet.Tests/BattleRageTests.cs` (quái giả) và UiChecks (HUD, ảnh). Số liệu ở cấp trên cùng của battle.json:

| Khóa | Giới hạn | Gốc | Ý nghĩa |
| --- | --- | --- | --- |
| `rageSeconds` | 1–30 s | 6 | Nổi giận bao lâu (thời gian trận) |
| `rageHitGain` | 0–1 | 0,05 | Thanh nộ tăng mỗi nhát mèo tự đánh trúng (cả nhát khi đang cầm); 20 nhát là đầy |
| `rageHurtGain` | 0–1 | 0,012 | Thanh nộ tăng mỗi HP mèo mất (Khiên/Mai rùa chặn thì không tính) |
| `rageScale` | 1–1,4 | 1,3 | Hình mèo to bao nhiêu (chỉ hình vẽ) |
| `rageKnock` | 0–1000 DIPs/s | 220 | Đẩy lùi ít nhất của mỗi nhát khi nổi giận (× `pushFactor` của quái) |
| `rageKnockFactor` | 1–5 | 1,5 | Đẩy lùi thường (combo, búa) nhân bao nhiêu khi nổi giận |

- **Thanh nộ** nằm dưới thanh HP trên HUD, mỗi trận bắt đầu rỗng (Viễn chinh: mỗi bước một trận mới). Đầy dần khi mèo đánh trúng và khi mèo mất máu; không tăng khi đang nổi giận. Bom, kỹ năng, app, nhát vọng, đòn lan không cộng. `rageHitGain` và `rageHurtGain` cùng 0 thì thanh không bao giờ đầy.
- **Đầy thì sáng lên** (viền vàng, nhấp nháy), lần đầu mỗi trận có banner "Thanh nộ đầy! Bấm vào thanh để mèo nổi giận". **Không bao giờ tự bật**: người dùng click vào thanh. Click chỉ có tác dụng khi trận đang chạy, mèo đang ở dưới sàn (không bị cầm) và **không đang choáng** (đang choáng thì thanh vẫn đầy, chờ hết choáng; nổi giận không cắt ngắn cú choáng đang có). Pause, menu, chọn thẻ, túi đồ đều chặn click.
- **Trong `rageSeconds` giây:** mỗi nhát mèo tự đánh trúng còn trúng **mọi slime khác trong tầm** vũ khí (+14 DIPs) ở phía mèo đang vung hoặc chồng lên mèo, mỗi con nhận nguyên đòn thường (không thêm chí mạng/Mở màn/Đà chạy), bị đá chặn như đòn lan của combo; combo lúc này không lan thêm. Mọi nhát trúng đều đẩy lùi (`rageKnock`, mạnh hơn ×`rageKnockFactor`), tinh anh miễn Đẩy vẫn đứng yên. **Không quái hay app nào làm mèo choáng** (vẫn mất máu bình thường). Mèo đang cầm vẫn chỉ đánh quái trên không; nổi giận thì nhát đó trúng mọi quái trên không trong tầm.
- **Thời gian:** chạy theo thời gian trận: Pause, menu, chọn thẻ, túi đồ, hướng dẫn đứng hình đóng băng; mèo đang bị cầm thì vẫn trôi (như Cổ vũ). Hết giờ, thua/thắng hoặc kết thúc trận thì mèo về cỡ cũ.
- **Hình:** mèo to dần lên ×`rageScale` trong 0,25 s đầu, nhỏ lại trong 0,25 s cuối, quanh chân; lông ánh đỏ (bảng màu đỏ của chính bộ lông đang chọn), quầng đỏ nhấp nháy sau lưng. Đang bị cầm thì chỉ to thêm 30% phần đó (quanh gáy) để kiếm khi lắc không ra khỏi cửa sổ. **Cửa sổ mèo, vùng trúng đòn trên sàn (`CatHalfWidth`) và cách kéo không đổi**; vì cửa sổ mèo nhận click theo điểm ảnh có vẽ, phần hình to ra cũng nhấc mèo được. Thanh nộ khi nổi giận là thời gian còn lại, màu đỏ tươi.
- Âm thanh dùng cue có sẵn (`RageFull` lấp lánh, `Raged` phấn khích); banner "MÈO NỔI GIẬN!". Không có bước hướng dẫn riêng (rê chuột lên thanh xem mô tả). Python không tham gia.

## Slime phun và Slime lò xo (`spit`, `spring`) (bản 1.5, mốc M09, 2026-10-28)

Q1 và Q2 của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetBattle.Spit.cs`, `src/DesktopPet.Core/PetBattle.Spring.cs` (luật), `src/DesktopPet/PetBattleView.Traps.cs` (vẽ), test `tests/DesktopPet.Tests/BattleSpitSpringTests.cs` và ảnh UiChecks (`TrapVisualChecks`, `enemy-spit-spring.png`). **Hai quái có trong battle.json gốc (`spitter`, `springer`) nhưng chưa trận nào dùng**: chương 1, trận tự do, Viễn chinh, Vô tận không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng), Thùng Rác không nhả chúng (`binSpit: false`). Chương 2 (M21) và Vô tận (M24) sẽ đưa vào. Muốn thử ngay: chép `docs/examples/battle-m09.json` thành `%LOCALAPPDATA%\DesktopPet\battle.json` rồi mở Trận tự do (dữ liệu tùy chỉnh: không kỷ lục, không thưởng).

```json
{ "id": "spitter", "support": true, "spit": { "range": 380, "minRange": 90, "windup": 1.0, "flight": 0.8, "radius": 42, "cooldown": 4, "puddleSeconds": 6, "puddleRadius": 48, "dashDistance": 1.5, "dashSpeed": 1.4 } }
{ "id": "springer", "spring": { "radius": 60, "compress": 0.8, "stun": 0.8, "rest": 4, "flattenHits": 2, "flatSeconds": 3, "creepRange": 120 } }
```

**Slime phun (`spit`)** — quái hỗ trợ (`support`: đứng sau đồng đội, Cuộn len kéo được), 40 HP, đánh gần yếu.

| Khóa | Giới hạn | Gốc | Ý nghĩa |
| --- | --- | --- | --- |
| `range`, `minRange` | 100–900, 0–400 DIPs (`minRange` < `range`) | 380, 90 | Mèo cách trong khoảng này thì phun |
| `windup` | 0,3–5 s | 1 | Gồng trước khi phun; vòng cam hiện ngay ở **chỗ mèo đứng lúc bắt đầu gồng** |
| `flight` | 0,2–3 s | 0,8 | Giọt bay vòng cung bao lâu |
| `radius` | 10–200 DIPs | 42 | Giọt rơi trúng mèo nếu tâm mèo trong khoảng này |
| `cooldown` | 0,5–30 s | 4 | Hồi giữa hai lần phun (con mới ra sân chờ một nửa) |
| `puddleSeconds`, `puddleRadius` | 0–30 s, 10–200 DIPs | 6, 48 | Vũng nằm bao lâu (0: không có vũng), rộng bao nhiêu |
| `dashDistance`, `dashSpeed` | 1–3 | 1,5, 1,4 | Quái lao đi qua vũng: quãng lao ×, tốc độ lao × (mỗi cú lao một lần) |
| `eliteDrops`, `eliteSpread`, `elitePuddle` | 1–3, 0–300 DIPs, 1–3 | 2, 70, 1,5 | Tinh anh: phun mấy giọt (giọt sau xa hơn về phía bên kia mèo), vũng lâu × |

- Giọt rơi trúng mèo **đang ở dưới sàn** (không bị nhấc, không trong `landingGrace`): mất máu bằng `damage` của slime phun (nhân độ khó/threat như đòn thường; Gai phản lại một phần nếu slime phun còn sống), **không choáng**. Khiên bong bóng và Mai rùa chặn. Mèo đã đi khỏi vòng hoặc đang được nhấc: trượt, vũng vẫn thành.
- Không bắt đầu phun khi người dùng đang cầm mèo, trong `landingGrace`, khi bị Đèn laser dụ hay đang hoảng. Đẩy hoặc choáng trong lúc gồng thì hủy cú phun. Gồng phun không chiếm lượt ra đòn (`maxAttackers`) của quái khác.
- Vũng: tối đa 6 vũng cùng lúc (vũng thứ 7 làm khô vũng cũ nhất); giọt rơi trùng vũng cũ thì làm mới vũng đó. Quạt thổi tới (trong `fanRange` + bán kính vũng) thì vũng khô ngay. Vũng **không** phải loại `link`, không tính vào giới hạn 2 loại liên kết.
- Tinh anh (`elite.hp` 1,2): phun 2 giọt, vũng lâu ×1,5.

**Slime lò xo (`spring`)** — bẫy nằm trên sàn, 70 HP, thân dẹt (`hitScale` 1,3 vì vẽ rộng hơn 20%). Không bao giờ đánh gần hay gồng đòn (kể cả nhịp trống `rhythm`).

| Khóa | Giới hạn | Gốc | Ý nghĩa |
| --- | --- | --- | --- |
| `radius` | 20–200 DIPs | 60 | Vòng vàng trên sàn, luôn hiện |
| `compress` | 0,3–3 s | 0,8 | Mèo dưới sàn bước vào vòng thì nén chừng này (vòng đỏ, kẽo kẹt) rồi bật |
| `stun` | 0–3 s | 0,8 | Mèo còn trong vòng lúc bật: mất `damage` và choáng chừng này (có Kiên cường, miễn choáng sau choáng, nổi giận, Khiên, Mai rùa như đòn khác) |
| `rest` | 0,5–20 s | 4 | Nghỉ sau khi bật (dù trúng hay không) |
| `flattenHits`, `flatSeconds` | 1–10, 0,5–15 s | 2, 3 | Trúng chừng này đòn vũ khí/bom (thường, combo, lan, Dư ảnh, bom; không tính độc, kỹ năng, app) thì bẹp, nằm im không bật |
| `creepRange` | 0–1500 DIPs (0: không trượt) | 120 | Mèo xa hơn chừng này thì trượt chậm (`speed`) lại gần, tới đó thì nằm im; đang nén hay bẹp thì không trượt |
| `eliteRadius`, `eliteRest`, `eliteHits` | 1–2, 0,5–20 s, 1–10 | 1,35, 2,5, 3 | Tinh anh: vòng to ×, nghỉ ngắn hơn, cần nhiều đòn hơn mới bẹp |

- Mèo đang được cầm không bao giờ kích bẫy và không bị bật trúng; nhấc mèo ra trong lúc nén là né được (thả lại vào vòng thì có `landingGrace` như thường). Đẩy/choáng/Kẹp gắp trong lúc nén: lò xo xả hơi, chuyển sang nghỉ. Physics kéo/ném của mèo không đổi.
- Không chen chỗ với quái khác (quái khác đứng lên được "tấm thảm"), nên mạnh khi đi cùng đội hình dồn mèo vào vòng.
- `creepRange` gốc là 120 (kế hoạch nói ~250): mèo tự đánh đứng cách mục tiêu ~83 DIPs, lớn hơn vòng 60, nên nằm im ở 250 thì bẫy gần như không bao giờ bật; 120 đủ gần để mèo bước vào vòng khi đánh các con khác.

- Pause, menu, chọn thẻ, túi đồ, hướng dẫn đứng hình đóng băng giọt đang bay, vũng, đồng hồ nén/nghỉ/bẹp. Kết thúc trận xóa giọt và vũng. Đổi vùng trận (DPI, màn hình) giữ đúng vị trí tương đối.
- Hình: slime phun màu tím nhạt có vòi trên đầu, gồng thì má phồng và miệng vòi cam; vòng cam đứt nét trên sàn đầy dần; giọt bay vòng cung với bóng tròn lớn dần ở chỗ rơi; vũng là viền tím bóng có mũi tên chạy hai chiều, mờ dần khi sắp khô. Lò xo là tấm thảm vàng có cuộn lò xo và đệm đỏ trên lưng; vòng vàng đứt nét (sẵn sàng), đỏ đầy dần (đang nén), xám chấm (nghỉ/bẹp); bật thì thân giãn cao lên. Chỉ vẽ nét viền trên sàn, nên click chỗ trống vẫn xuyên xuống desktop.
- Banner lần đầu mỗi trận: "Slime phun! Nhấc mèo khỏi vòng cam", "Vũng nhầy làm slime lao xa hơn! Dùng Quạt làm khô", "Lò xo đang nén! Nhấc mèo ra khỏi vòng", "Lò xo bẹp rồi! Nó nằm im một lúc"; tinh anh có banner tinh anh sẵn có. Âm thanh dùng cue có sẵn (`SpitAimed`/`GooLanded` bóp, `PuddleBoost` vút, `PuddleDried` bụp, `SpringSqueezed` keng, `SpringBounced` cộc, `SpringFlattened` bóp). Sự kiện mới: `SpitAimed`, `GooLanded`, `PuddleBoost`, `PuddleDried`, `SpringSqueezed`, `SpringBounced`, `SpringFlattened`; hiệu ứng `Splat`.
- Bước hướng dẫn đứng hình chương 2 có từ M21 (mục "Chương 2"); trang Sổ tay quái ở M27. Python không tham gia.

## Slime nhạc trưởng và Slime xích (`drummer`, `chain`) (bản 1.5, mốc M10, 2026-10-28)

Q3 và Q4 của docs/UPDATE-1.5.md, dùng khung liên kết M01 (mục Liên kết quái). Code: `src/DesktopPet.Core/PetBattle.Links.cs` (nhịp/dây, `Beats`/`Share` theo tinh anh), `src/DesktopPet.Core/PetBattle.Held.cs` (mèo cắt dây), `src/DesktopPet/PetBattleView.Links.cs` (vẽ), test `tests/DesktopPet.Tests/BattleBandTests.cs` và ảnh UiChecks (`BandVisualChecks`, `enemy-drummer-chain.png`). **Hai quái có trong battle.json gốc nhưng chưa trận nào dùng** (`binSpit: false`): chương 1, trận tự do, Viễn chinh, Vô tận không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng). Chương 2 (M21) và Vô tận (M24) sẽ đưa vào. Muốn thử ngay: chép `docs/examples/battle-m10.json` thành `%LOCALAPPDATA%\DesktopPet\battle.json` rồi mở Trận tự do (dữ liệu tùy chỉnh: không kỷ lục, không thưởng).

```json
{ "id": "drummer", "link": { "kind": "rhythm", "range": 220, "beat": 0.8, "beats": 4, "eliteBeats": 3 } }
{ "id": "chain", "link": { "kind": "tether", "range": 200, "share": 0.5, "breakStun": 1, "relink": 6, "cutSpeed": 300, "eliteShare": 0.7 } }
```

- **Slime nhạc trưởng** (60 HP, đánh gần như slime thường, màu cam, ôm trống đỏ phía trước, dùi gõ xuống mỗi nhịp): chấm nhịp trên đầu, vòng cam chấm trên sàn bán kính 220. Mỗi 0,8 s một nhịp; nhịp thứ 4 mọi quái trong vòng (cả nó) đang với tới mèo **gồng đánh cùng lúc**, bỏ qua lượt ra đòn và hồi chiêu. Đẩy, choáng (búa, Kẹp gắp…) hoặc Quạt thổi trúng thì đếm lại từ 0; Khiên bong bóng chặn cả loạt. Tinh anh (`elite.hp` 1,2): đếm 3 nhịp là đánh.
- **Slime xích** (36 HP, nhỏ, xanh lá, hai mắt xích thép trên đầu): tự nối dây nhầy vào đồng minh gần nhất trong 200, gánh 50% mọi sát thương đồng minh nhận, đi sát sau đồng minh. Cắt dây: Kéo cắt, hoặc **mèo đang cầm (đã kéo quá ngưỡng ~6 DIPs) hay đang bay sau cú ném đi xuyên dây** với tốc độ ≥ `cutSpeed` (100–5000 DIPs/s, gốc 300): đồng minh choáng 1 s, 6 s sau slime xích mới nối lại. Hạ slime xích cũng làm đồng minh choáng. Tinh anh (`elite.hp` 1,3): gánh `eliteShare` (0.05–0.95, gốc 0,7).
- Cắt dây bằng mèo: xét đoạn đường của tâm mèo giữa hai khung hình (ném nhanh không lọt qua) với dây như được vẽ, tâm mèo cách dây ≤ 12 DIPs; mỗi dây đứt một lần; mèo chỉ nhấn (chưa kéo quá ngưỡng), kéo chậm hay bay cao qua dây thì không cắt. Physics kéo/ném không đổi (chỉ đọc vị trí/vận tốc), mèo vẫn không nhận sát thương khi đang cầm/bay. Pause/menu/chọn thẻ/hướng dẫn đứng hình: không cắt. Đếm riêng `CatRopeCuts` (Kéo cắt vẫn đếm `RopesCut`).
- Dây dài nằm trên vạch sàn thay vì võng xuống dưới sàn (sửa hình M01; vùng cắt của Kéo cắt và mèo vốn đã tính như vậy).
- Banner lần đầu mỗi trận: "Nhạc trưởng đếm nhịp! Đẩy nó trước nhịp cuối", "Dây nhầy! Ném hoặc kéo mèo xuyên dây, hay dùng Kéo cắt", "Đứt dây! Đồng đội choáng" (vi/en/ja). Âm thanh dùng cue có sẵn: `LinkBeat` gõ nhẹ (`ball_tap`, hạng thấp nhất), `LinkStrike` keng, `LinkFormed` bóp, `LinkCut` bụp, `LinkSnapped` bụp nhỏ.
- Bước hướng dẫn đứng hình chương 2 có từ M21 (mục "Chương 2"); trang Sổ tay quái ở M27. Python không tham gia.

## Slime hợp thể, Slime bom, Slime tách đôi (`fuser`, `bomber`, `splitter`) (bản 1.5, mốc M11, 2026-10-28)

Q5, Q6, Q7 của docs/UPDATE-1.5.md, dùng liên kết `merge` và `ride` của M01 (mục Liên kết quái) cùng hai khóa mới `fuse`, `split` và cờ `afterHeal`. Code: `src/DesktopPet.Core/PetBattle.Fuse.cs` (bom), `src/DesktopPet.Core/PetBattle.Split.cs` (tách đôi), `src/DesktopPet.Core/PetBattle.Links.cs` (nhập, cưỡi), `src/DesktopPet/PetBattleView.Swarm.cs` (vẽ), test `tests/DesktopPet.Tests/BattleSwarmTests.cs` và ảnh UiChecks (`SwarmVisualChecks`, `enemy-swarm.png`). **Năm quái có trong battle.json gốc nhưng chưa trận nào dùng** (`binSpit: false`): chương 1, trận tự do, Viễn chinh, Vô tận không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng). Chương 2 (M21), đội hình "Bầy nhập"/"Kỵ binh" (M12) và Vô tận (M24) sẽ đưa vào. Muốn thử ngay: chép `docs/examples/battle-m11.json` thành `%LOCALAPPDATA%\DesktopPet\battle.json` rồi mở Trận tự do (dữ liệu tùy chỉnh: không kỷ lục, không thưởng). File này dùng `merge` + `ride` nên không trộn được với dây/nhịp của M10 (tối đa 2 loại liên kết mỗi trận).

```json
{ "id": "fuser", "link": { "kind": "merge", "range": 70, "count": 3, "mergeSeconds": 2, "into": "giant" } }
{ "id": "bomber", "link": { "kind": "ride", "mount": "runner", "range": 160, "dropRange": 120, "breakStun": 1 },
  "fuse": { "range": 70, "seconds": 1.5, "radius": 90, "stun": 0.6, "enemyDamage": 20, "dropFuse": 0.8, "eliteSeconds": 1.1, "eliteRadius": 1.3 } }
{ "id": "splitter", "split": { "into": "splitling", "count": 2, "eliteCount": 3, "health": 0.6 } }
{ "id": "splitling", "link": { "kind": "merge", "range": 120, "count": 2, "mergeSeconds": 1.5, "into": "splitter", "afterHeal": true } }
```

- **Slime hợp thể** (`fuser`, 26 HP, nhỏ 0,7, hồng, một râu trên đầu; ra theo bầy 3 con bằng `squad`): 3 con đứng trong 70 DIPs của nhau liên tục 2 s (thanh tím đếm ngược) thì thành **Slime khổng lồ** (`giant`: to 1,6, chậm 22, đánh 20, ba râu và lông mày nặng), máu bằng tổng máu hiện tại, máu tối đa bằng tổng. Đẩy, choáng hay Kẹp gắp làm đồng hồ con đó về 0; Quạt thổi tách bầy; Bom len khi đang chụm. Khổng lồ quá to cho Kẹp gắp (`scale` > 1,3). Tinh anh hợp thể: máu ×1,3.
- **Slime bom** (`bomber`, 30 HP, xám xanh, nắp thép và ngòi trên đầu): cưỡi slime nhanh (`runner`) ở xa mèo, cách mèo 120 thì nhảy xuống. Cách mèo dưới sàn ≤ `fuse.range` thì dừng, châm ngòi `seconds` (tia lửa chạy dọc ngòi, vòng nổ trên sàn đỏ dần), rồi nổ: mèo dưới sàn trong `radius` mất `damage` của nó (22, nhân độ khó/threat như đòn thường) và choáng `stun` s; mọi slime khác trong vòng (không tính slime trên không) mất `enemyDamage` (không bị đá chặn, tính là mèo hạ). Bom tự nổ thì biến mất, **không tính là mèo hạ**. Không đánh gần, không vào nhịp trống.
  - Hạ bom trước khi châm: xì, không nổ (tính là mèo hạ). Hạ bom khi ngòi đang cháy: nổ tại chỗ.
  - Hạ con ngựa giữa đường (hoặc cắt dây cưỡi): bom rơi, choáng `breakStun` s, châm ngòi ngắn `dropFuse` s và nổ tại chỗ rơi.
  - Đẩy bom đang cháy ngòi: bom văng đi, nổ ở chỗ dừng. Kẹp gắp ném bom (đủ nhanh): nổ ở chỗ trúng slime khác/mép hoặc chỗ đáp.
  - Mèo đang được cầm, đang trong `landingGrace`: không bị nổ trúng; Khiên bong bóng, Mai rùa chặn như đòn khác. Không châm ngòi khi người dùng đang cầm mèo hay trong `landingGrace`.
  - Tinh anh (`elite.hp` 1,2): ngòi `eliteSeconds`, vòng nổ ×`eliteRadius`.

| Khóa `fuse` | Giới hạn | Gốc |
| --- | --- | --- |
| `range` | 20–400 DIPs | 70 |
| `seconds`, `dropFuse`, `eliteSeconds` | 0,3–5 s, 0,2–5 s, 0,3–5 s | 1,5, 0,8, 1,1 |
| `radius` | 20–300 DIPs | 90 |
| `stun` | 0–3 s | 0,6 |
| `enemyDamage` | 0–1000 | 20 |
| `eliteRadius` | 1–2 | 1,3 |

  `fuse` không dành cho boss, đá chặn, quái lao, quái dậm, quái trên không, quái hỗ trợ, quái phun, lò xo hay quái hồi máu.
- **Slime tách đôi** (`splitter`, 70 HP, nâu caramel, đường nứt zigzag giữa thân): bị hạ thì vỡ thành `count` **Slime nửa** (`splitling`, 35 HP tối đa, ra với `health` × máu tối đa, băng dán trên đầu); tinh anh (`elite.hp` 1,2) vỡ thành `eliteCount`. Nửa không tách tiếp (`split.into` phải là quái đã biết, không phải boss, không tự tách). Nửa luôn ra kể cả khi sân đã đủ `maxAlive` (quái mới chờ chỗ như thường); nửa của quân boss gọi giữ khiên của pha đó. Giới hạn: `count`/`eliteCount` 2–4, `health` 0,1–1.
  - Nhập lại (`afterHeal`, chỉ cho `merge`): chỉ những nửa đã được slime thảo dược hồi máu từ lúc ra mới tính; 2 nửa như vậy đứng trong 120 DIPs liên tục 1,5 s thì nhập lại thành Slime tách đôi (máu bằng tổng). Hạ thảo dược trước, hoặc Bom len cả 2 nửa. Con mẹ nhập lại bị hạ thì tách tiếp (ngân sách hồi của thảo dược có hạn).
- Pause, menu, chọn thẻ, túi đồ, hướng dẫn đứng hình đóng băng ngòi, đồng hồ nhập. Kết thúc trận tắt mọi ngòi. Không dùng luồng ngẫu nhiên mới, nên seed cũ cho đúng trận cũ.
- Banner lần đầu mỗi trận (vi/en/ja): "Slime nhập thành khổng lồ! Lần sau Đẩy hoặc Quạt tách bầy ra", "Slime bom cưỡi slime nhanh! Hạ con ngựa giữa đường", "Bom châm ngòi! Nhấc mèo ra khỏi vòng đỏ", "Slime tách đôi! Đừng để thảo dược hồi cho hai nửa", "Hai nửa nhập lại! Hạ slime thảo dược trước". Âm thanh dùng cue có sẵn: `Merged` bóp, `Dismounted` vút, `FuseLit` keng, `Blasted` bụp to, `SlimeSplit` bóp. Sự kiện mới: `FuseLit`, `Blasted`, `SlimeSplit`.
- Đo perf: `scripts/perf.ps1 -Battle crowd-new` giữ sàn đầy 16 quái 1.5 (hợp thể, bom cưỡi, tách đôi, phun, lò xo, thảo dược) bằng số liệu gốc.
- Bước hướng dẫn đứng hình chương 2 có từ M21 (mục "Chương 2"); trang Sổ tay quái ở M27. Python không tham gia.

## Slime cờ hiệu và đội hình phối hợp (`flagbearer`, `formations`) (bản 1.5, mốc M12, 2026-10-29)

Q8 và 6 đội hình phối hợp của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetBattleDirector.cs` (bốc đội hình), `src/DesktopPet.Core/PetBattle.Waves.cs` (sự kiện `FormationArrived`), `src/DesktopPet.Core/PetBattle.Links.cs` (hào quang tinh anh, 2 dây trên một đồng minh), `src/DesktopPet/PetBattleView.Flag.cs` (vẽ), test `tests/DesktopPet.Tests/BattleFormationTests.cs` và ảnh UiChecks (`FlagVisualChecks`, `enemy-flag.png`). **Slime cờ hiệu và 6 đội hình có trong battle.json gốc nhưng chưa trận nào dùng** (`binSpit: false`): chương 1, trận tự do, Viễn chinh, Vô tận không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng). Chương 2 (M21) và Vô tận (M24) sẽ đưa vào. Muốn thử ngay: chép `docs/examples/battle-m12.json` thành `%LOCALAPPDATA%\DesktopPet\battle.json` rồi mở Trận tự do (dữ liệu tùy chỉnh: không kỷ lục, không thưởng).

```json
{ "id": "flagbearer", "support": true, "keepBack": 120, "link": { "kind": "aura", "range": 180, "speed": 1.3, "eliteSpeed": 1.45, "breakStun": 2 } }
"formations": [ { "id": "fortress", "nameVi": "Pháo đài", "nameEn": "Fortress", "tipVi": "…", "tipEn": "…", "minThreat": 0,
  "parts": [ { "squad": [ { "enemy": "spitter", "count": 1 }, { "enemy": "mender", "count": 1 }, { "enemy": "rock", "count": 1 } ] } ] } ]
{ "groups": [ { "formation": { "fortress": 2, "drums": 1 }, "count": 1 } ] }
```

- **Slime cờ hiệu** (`flagbearer`, 50 HP, xanh dương, cán cờ sau lưng, cờ đuôi nheo vàng sọc đỏ bay về phía lưng): quái hỗ trợ (đứng `keepBack` sau đồng đội, không đánh khi còn đồng đội biết đánh; Cuộn len kéo được). Hào quang vòng 180 (vòng vàng đứt nét): đồng minh trong vòng đi nhanh ×1,3 và có hai vạch vàng sau lưng; tinh anh (`elite.hp` 1,2) ×1,45 (`eliteSpeed`, 1–3, chỉ cho `aura`). Hạ nó: mọi quái khác (trừ boss) hoảng 2 s (chạy ra xa mèo, không đánh, giọt mồ hôi xanh). Cách phá: hạ nó trước; Đèn laser dụ quái ra khỏi vòng.
- **Đội hình** (`formations`, gộp theo `id`): `id`, `nameVi`/`nameEn` (≤ 32 ký tự), `tipVi`/`tipEn` (đi cùng nhau, ≤ 80), `minThreat` (0–3), `parts` (1–4 phần; mỗi phần là nhóm thường `enemy`/`count` 1–6 hoặc `squad` 2–8 con như ở đợt, có `elite`, `side`). Tối đa 12 quái mỗi đội hình, không boss.
- **Nhóm đội hình trong đợt**: `{ "formation": { id: trọng số }, "count": 1–4, "side"? }` (không đi cùng `pick`, `squad`, `atLeast`, `elite`). Lúc đợt bắt đầu, Director bốc `count` lần, mỗi lần một đội hình theo trọng số trong số đội hình có `minThreat` ≤ threat hiện tại (không có: các đội hình có `minThreat` nhỏ nhất) và vừa phần ngân sách còn lại của đợt (giá = tổng `cost` thành viên, chưa tính phụ phí tinh anh); không đội hình nào vừa thì lấy đội hình rẻ nhất. Nhóm cố định và đội luôn trừ ngân sách trước, đội hình trừ tiếp, quái bốc `pick` chia phần còn lại. Mỗi lần bốc dùng một lượt luồng spawn, chỉ ở đợt có nhóm đội hình (seed cũ cho đúng trận cũ).
- Các phần của một đội hình ra lần lượt (mỗi phần chờ sàn đủ chỗ như đội): từ mép xa mèo (hoặc mép của `side` nhóm), phần `"side": "b"` ra ở mép còn lại sau mũi tên đỏ. Phần đầu tiên ra thì banner "Đội hình <tên>! <gợi ý>" (lần đầu mỗi đội hình mỗi trận, vi/en/ja) và cue lấp lánh.
- Kiểm tra lúc nạp: phần nào lớn hơn `maxAlive` của trận/màn thì bị từ chối; luật tối đa 2 loại liên kết mỗi trận tính cả mọi thành viên của mọi đội hình có thể bốc.
- 6 đội hình gốc:

| `id` | Tên | Thành phần | `minThreat` | Giá |
|---|---|---|---|---|
| `fortress` | Pháo đài | slime phun (sau cùng), thảo dược, đá (phía mèo) | 0 | 6 |
| `cavalry` | Kỵ binh | 2 slime bom cưỡi 2 slime nhanh; rồi 1 slime phun | 1,4 | 8 |
| `drums` | Đội trống | nhạc trưởng (sau) + 3 slime | 0 | 5 |
| `assembly` | Dây chuyền | 2 slime xích + 1 slime đá **tinh anh**; cả hai dây nối vào đá, đá chỉ nhận 25% sát thương khi đủ 2 dây | 1,2 | 6 |
| `trampoline` | Bạt nhún | slime + slime nhanh từ phía trước; lò xo từ mép sau lưng mèo (`side: "b"`) | 0 | 4 |
| `swarms` | Bầy nhập | (thảo dược + 3 hợp thể) rồi (3 hợp thể) | 1,6 | 8 |

- Liên kết: Kỵ binh `ride`, Đội trống `rhythm`, Dây chuyền `tether`, Bầy nhập `merge`; một trận chỉ trộn được tối đa 2 loại (kể cả slime cờ hiệu `aura`).
- **Hai dây trên một đồng minh** (người dùng chọn 2026-10-29, cho Dây chuyền): slime xích ưu tiên đồng minh chưa có dây; không còn thì nối vào con mới có 1 dây (tối đa 2, `EnemyLinks.MaxTethers`). Dây thứ hai đứng sau dây thứ nhất và gánh phần của nó trên phần còn lại (50% rồi 50% của nửa còn lại: đồng minh nhận 25%). Cắt dây nào thì đồng minh choáng như cũ.
- Banner slime cờ hiệu lần đầu: "Slime cờ hiệu! Quái trong vòng vàng chạy nhanh hơn. Hạ nó trước"; hoảng: "Mất cờ! Cả đội hoảng loạn" (cue ngạc nhiên). Trang Sổ tay quái cho đội hình làm ở M27. Python không tham gia.
- Perf: chưa đo riêng (kịch bản `crowd-new` đã dùng 2 loại liên kết nên không thêm được hào quang); phần vẽ mới là vài nét mỗi slime.

## Quái dùng chuột: bóng ma, bong bóng, ảo ảnh, dỗi (`ghost`, `bubble`, `mirage`, `sulk`) (bản 1.5, mốc M13, 2026-10-29)

S1–S4 của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetBattle.Tricks.cs` (luật), `src/DesktopPet/PetBattleView.Tricks.cs` (vẽ), `PetWindow.Battle.cs` (`BattlePointer`: đưa vị trí chuột vào trận; click chọc bong bóng), test `tests/DesktopPet.Tests/BattleTrickTests.cs` và ảnh UiChecks (`TrickVisualChecks`, `enemy-tricks.png`; kiểm tra đọc chuột trong `BattleChecks`). **Bốn quái có trong battle.json gốc nhưng chưa trận nào dùng** (`binSpit: false`): chương 1, trận tự do, Viễn chinh, Vô tận không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng, 225/225). Chương 2 (M21) và Vô tận (M24) sẽ đưa vào. Muốn thử: chép `docs/examples/battle-m13.json` (bóng ma + bong bóng) hoặc `docs/examples/battle-m13b.json` (ảo ảnh + dỗi) thành `%LOCALAPPDATA%\DesktopPet\battle.json` rồi mở Trận tự do.

```json
{ "id": "phantom", "ghost": { "visible": 2.5, "hidden": 2.5, "reveal": 2, "laserRange": 160, "eliteVisible": 1.5 } }
{ "id": "bubble", "bubble": { "pops": 3, "elitePops": 5, "regrow": 6, "stun": 1.5, "lift": 12 } }
{ "id": "mirage", "mirage": { "copies": 2, "eliteCopies": 3, "reveal": 2, "laserRange": 160 } }
{ "id": "sulky", "sulk": { "calm": 1800, "eliteCalm": 2800, "idle": 1, "fade": 0.15, "grow": 0.1, "maxGrow": 1.6 } }
```

- **Đọc chuột:** mỗi khung hình (không Pause, không hướng dẫn đứng hình) cửa sổ đưa vị trí con trỏ vào trận **chỉ khi con trỏ nằm trong dải trận** (cửa sổ dải dưới đáy màn hình có trận), chỉ vị trí (x, độ cao trên sàn), không đọc gì khác. Đang cầm mèo thì coi như không có chuột. "Trên quái" = trong thân như vẽ cộng lề 10 DIPs (lề click giống `SlimeAt`). Không quái nào làm mèo đổi cảm xúc hay ngủ (cảm xúc tự động vẫn bị chặn trong trận); Python không tham gia.
- **Slime bóng ma** (`phantom`, id không dùng `ghost` vì nhiều test cũ dùng tên đó làm quái không tồn tại; 40 HP, xanh nhạt hơi trong, miệng "o", lọn khói trên đầu): hiện `visible` s rồi ẩn `hidden` s. Đang ẩn: gần như trong suốt (viền đứt nét trắng mờ lắc lư), mèo không nhắm, không click chọn, không Đẩy/Bom ngắm vào được, Kẹp gắp không gắp; đòn nào trúng cũng "TRƯỢT" (trừ độc đã ngấm); không gồng đòn, trôi xuyên qua mèo sang phía bên kia (tốc độ ×1,3). Rê chuột lên nó (hoặc chấm laser trong `laserRange`) thì hiện ra ngay, sáng vàng nhẹ, giữ hiện khi chuột còn trên nó và thêm `reveal` s sau khi rời. Không ẩn giữa lúc đang gồng/lao. Tinh anh: mỗi lần hiện chỉ `eliteVisible` s.
- **Slime bong bóng** (`bubble`, 34 HP, xanh da trời): lơ lửng trong bong bóng cao `lift` DIPs; bong bóng chặn mọi đòn (kể cả độc), nó vẫn đánh bình thường. Click vào nó (không chọn skill nào) là chọc: mỗi lần một vết nứt; đủ `pops` lần (tinh anh `elitePops`) thì vỡ: rơi xuống choáng `stun` s (× `stunFactor`), đánh được. Một nét Kéo cắt qua nó cũng làm vỡ (không mất máu). Bong bóng mọc lại sau `regrow` s. Mèo bỏ qua nó khi còn quái khác để đánh. Có skill nhắm quái đang chọn (Đẩy, Bom…) thì click dùng skill như cũ (ưu tiên: mèo → skill → quái).
- **Slime ảo ảnh** (`mirage`, 36 HP, tím, viên đá tím trên trán): ra kèm `copies` bản giả (tinh anh `eliteCopies`, cũng có viền vàng). Bản giả giống hệt, hiện máu của bản thật, đi và gồng đòn như thật nhưng đòn của nó không gây gì (chữ "TRƯỢT" ở mèo). Bất kỳ đòn nào trúng bản giả (kể cả Đẩy, Kẹp gắp) thì nó tan, không tính là mèo hạ, không vào kỷ lục; bản thật bị hạ thì mọi bản giả tan. Bản giả không tính vào số quái trên sàn (`maxAlive`), ngân sách đợt, số tinh anh, không bị thảo dược hồi, không được dây xích/cưỡi chọn, không bắn virus. Bình thường không bản nào có bóng; rê chuột lên một bản (hoặc laser trong `laserRange`) thì `reveal` s: bản giả nhòe tím, trong suốt; bản thật có bóng đậm rộng dưới chân.
- **Slime dỗi** (`sulky`, hồng, chau mày, má hồng, bĩu môi): mọi đòn không trừ máu, mỗi đòn làm nó phồng thêm `grow` (tối đa ×`maxGrow`, chữ hồng "DỖI!"), đòn của nó mạnh theo độ phồng; mỗi đòn cũng làm tụt 10% độ dỗ. Rê chuột qua lại trên nó: quãng đường chuột đi trên nó cộng dồn (mỗi khung tối đa 80 DIPs, nhảy chuột không tính), đủ `calm` DIPs (tinh anh `eliteCalm`) thì hết dỗi: cười, vẫy tay, đi khỏi và mờ dần, tim bay lên; **tính là mèo hạ**. Đang dỗ thì vừa xẹp lại. Thanh hồng trên đầu là độ dỗ (thay thanh máu). Không dỗ quá `idle` s thì dỗi lại `fade` mỗi giây. Mèo bỏ qua nó khi còn quái khác.
- Kiểm tra lúc nạp: mỗi loại quái tối đa một trong bốn khóa; không cho boss, đá chắn, quái bay, lò xo, bom, tách đôi, có liên kết; ảo ảnh không cho quái hỗ trợ/hồi máu; dỗi không cho quái hồi máu. **Mỗi trận tối đa 2 loại quái dùng chuột** (`BattleData.MaxMouseKinds`, tính mọi loại các đợt có thể đưa ra như luật liên kết; áp cho mọi trận; từ M24 Vô tận xét theo từng chặng và từng đợt trùm).
- Sự kiện mới: `GhostFaded`, `GhostRevealed`, `GhostMissed`, `BubbleBlocked`, `BubblePoked`, `BubblePopped`, `BubbleRegrown`, `MirageCast`, `MirageDispelled`, `SulkPuffed`, `SulkPetted`, `SulkCalmed` (cue có sẵn); hiệu ứng `Miss`, `Heart`. Banner lần đầu mỗi trận (vi/en/ja): bóng ma ẩn, bong bóng chặn đòn, bong bóng vỡ, ảo ảnh có bản giả, slime dỗi không mất máu, hết dỗi.
- Số liệu là ước lượng, chỉnh sau khi chơi thật (nhất là `calm`: người dùng rê nhanh thì ~1–2 s).

## Slime bay và slime đào hang (`fly`, `burrow`) (bản 1.5, mốc M14, 2026-10-29)

S5 và S6 của docs/UPDATE-1.5.md, theo ngoại lệ Battle Mode đã duyệt 2026-10-26 (docs/APPROVED-EXCEPTIONS.md). Code: `src/DesktopPet.Core/PetBattle.Fly.cs`, `PetBattle.Burrow.cs` (luật), `src/DesktopPet/PetBattleView.Sky.cs` (vẽ), `PetWindow.BattleSky.cs` (cửa sổ riêng của slime bay, canvas đạn), test `tests/DesktopPet.Tests/BattleSkyTests.cs`, ảnh UiChecks (`SkyVisualChecks`: `enemy-sky.png`, `enemy-flyer.png`, `enemy-sky-shots.png`; cửa sổ thật trong `BattleChecks`). **Hai quái có trong battle.json gốc nhưng chưa trận nào dùng** (`binSpit: false`): chương 1, trận tự do, Viễn chinh, Vô tận không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng, 225/225). Muốn thử: chép `docs/examples/battle-m14.json` thành `%LOCALAPPDATA%\DesktopPet\battle.json` rồi mở Trận tự do.

```json
{ "id": "flyer", "fly": { "flySpeed": 150, "move": 5, "minHeight": 220, "cooldown": 3.5, "aim": 0.8, "shotSpeed": 520, "radius": 30,
  "slow": 0.6, "slowSeconds": 2, "eliteShots": 2, "heldHits": 3, "eliteHeldHits": 4, "heldWindow": 4, "downed": 3, "downedDamage": 1.5, "rise": 1 } }
{ "id": "burrower", "burrow": { "speed": 1.4, "crack": 1, "eliteCrack": 0.7, "radius": 55, "emerge": 0.5, "stun": 0.8,
  "above": 3, "eliteAbove": 2, "dig": 0.6, "rest": 4, "pinned": 3 } }
```

- **Slime bay** (`flyer`, 44 HP, xanh bạc hà, hai cánh trắng): bay khắp màn hình có trận, chân thân cao từ `minHeight` DIPs trên sàn tới gần mép trên màn hình, mỗi ~`move` s đổi chỗ (`flySpeed` DIPs/s), né HUD, bảng thẻ, túi đồ và các hàng icon app. Cửa sổ cho trận biết màn hình cao bao nhiêu (`SetSky`); chỗ mới bốc bằng luồng ngẫu nhiên riêng `BattleRandom.Sky` (trận không có slime bay không đổi). Mèo dưới sàn không nhắm, Bom (rơi ở sàn) không trúng, không bị dây xích/nhịp trống/cờ kéo vào, không chạm tường "Cập nhật". Vẽ trong **cửa sổ nhỏ topmost riêng** như icon app (theo DPI của màn hình trận, chỉ màn có trận; ẩn khi app khác fullscreen, khi Pause/menu); chỉ phần hình nhận chuột, và click chỉ có tác dụng khi đang chọn Đẩy hoặc Kẹp gắp. Trên dải trận chỉ còn bóng mờ dưới nó.
- **Đạn dính:** mỗi `cooldown` s nó đứng yên ngắm `aim` s (vòng đỏ quanh nó, đường đứt nét đỏ và vòng ngắm co lại ở mèo), rồi bắn giọt nhầy bay thẳng tới chỗ mèo lúc bắn (`shotSpeed`), bay quá thêm ~1,2 s; trúng khi đi qua trong `radius` DIPs quanh tâm mèo (tính theo đoạn đường mỗi khung). Trúng thì mèo mất máu như đòn của nó (Gai phản lại được), **kể cả khi đang cầm và trong `landingGrace`**; Khiên bong bóng, Mai rùa chặn; không bao giờ choáng mèo, không làm rơi mèo khỏi tay. Mèo dưới sàn trúng đạn thì chậm ×`slow` trong `slowSeconds` (gộp vào `LagFactor`: chạy và vung chậm lại). Đang cầm mèo kéo đi chỗ khác thì né được. Tinh anh bắn `eliteShots` giọt liền nhau (cách 0,3 s). Đạn và vòng ngắm vẽ trên một canvas xuyên chuột phủ vùng làm việc của màn hình trận, chỉ hiện khi có đạn hoặc đang ngắm.
- **Làm rơi:** `heldHits` nhát vuốt khi cầm mèo trong `heldWindow` s (tinh anh `eliteHeldHits`), Đẩy (click vào nó khi chọn Đẩy), Kẹp gắp (click vào nó: **giật xuống ngay**, không treo theo con trỏ, tiêu Kẹp gắp), hoặc bất kỳ cú choáng nào. Nó rơi theo trọng lực, nằm choáng `downed` s dưới sàn (cánh cụp, đánh được như slime thường, nhận ×`downedDamage` mọi đòn; Đẩy lúc này không rút ngắn choáng), rồi gồng `rise` s (vẫn đánh được, cánh vỗ nhanh) và bay lên chỗ mới.
- **Slime đào hang** (`burrower`, 46 HP, nâu, mũi hồng, hai vuốt): ra khỏi đất ~1 s rồi đào xuống (`dig` s). Dưới đất chỉ thấy ụ đất di chuyển: không đòn/Bom/độc mới nào trúng ("TRƯỢT"), mèo không nhắm, không Đẩy/chọn được, không gồng đòn; đi tới chỗ mèo với tốc độ ×`speed`. Nghỉ dưới đất ít nhất `rest` s, rồi khi tới dưới mèo đang đứng sàn (không khi mèo đang cầm/bay hay trong `landingGrace`) thì hiện **vòng nứt** bán kính `radius` ở chỗ mèo, đầy đỏ dần trong `crack` s (tinh anh `eliteCrack`), rồi trồi lên ở đó: mèo dưới sàn còn trong vòng mất máu và choáng `stun` s (Khiên, Mai rùa, nộ, miễn choáng như mọi đòn). Lên rồi nó đánh như slime thường `above` s (tinh anh `eliteAbove`) rồi đào xuống lại.
- **Thả đè:** mèo được thả sau khi nhấc thật mà đáp trong vòng nứt lúc vòng còn hiện hoặc trong `emerge` s nó đang trồi thì nó kẹt ngoài: choáng `pinned` s, mèo không bị trúng. Chỉ slime đào hang phản ứng với mèo rơi. **Kẹp gắp** bấm vào ụ đất thì lôi nó lên và gắp như slime nhỏ; sau đó nó ở ngoài thêm `pinned` s.
- Kiểm tra lúc nạp: một loại không vừa bay vừa đào; không cho boss, đá chắn, lao, nện, `hover`, hỗ trợ, hồi máu, liên kết, phun, lò xo, bom, tách đôi, quái dùng chuột; số liệu trong giới hạn.
- Sự kiện mới: `FlyAimed`, `FlyShot`, `FlyShotHit` (Value 1: mèo đang cầm), `FlyerKnocked` (Value 1: Kẹp gắp), `FlyerDowned`, `FlyerRose`, `BurrowCracked`, `BurrowEmerged` (Value 1: trúng mèo), `BurrowPinned`, `BurrowPulled`, `BurrowDug` (cue có sẵn). Banner lần đầu mỗi trận (vi/en/ja): slime bay ngắm mèo, slime bay rơi, sàn nứt, đè trúng.
- Chưa làm: Captcha (A2) chưa có nên luật "Captcha không ra khi có slime bay" để làm cùng A2; Sổ tay quái cho hai quái ở M27 (hướng dẫn chương 2 có từ M21). Số liệu là ước lượng, chỉnh sau khi chơi thật. Perf: chưa đo; canvas đạn phủ cả vùng làm việc chỉ hiện khi có đạn/đang ngắm.

## Slime băng và slime bám tường (`ice`, `cling`) (bản 1.5, mốc M15, 2026-10-29)

S7 và S8 của docs/UPDATE-1.5.md, dùng khóa `throwHit` của M03 (cú ném chỉ tác động quái có tương tác ném riêng, ngoại lệ Battle Mode đã duyệt 2026-10-26). Code: `src/DesktopPet.Core/PetBattle.Wall.cs` (luật), `src/DesktopPet/PetBattleView.Wall.cs` (vẽ), test `tests/DesktopPet.Tests/BattleWallTests.cs`, ảnh UiChecks (`WallVisualChecks`: `enemy-wall.png`). **Hai quái có trong battle.json gốc nhưng chưa trận nào dùng** (`binSpit: false`): chương 1, trận tự do, Viễn chinh, Vô tận không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng, 232/232). Muốn thử: chép `docs/examples/battle-m15.json` thành `%LOCALAPPDATA%\DesktopPet\battle.json` rồi mở Trận tự do.

```json
{ "id": "icy", "throwHit": { "minSpeed": 650, "damage": 12, "stun": 1.5 },
  "ice": { "armor": 0.15, "wetSpeed": 400, "regrow": 10, "eliteRegrow": 6 } }
{ "id": "clinger", "throwHit": { "minSpeed": 450, "damage": 8 },
  "cling": { "height": 60, "spacing": 46, "climb": 90, "cooldown": 3, "aim": 0.9, "flight": 0.5, "radius": 36,
             "eliteSpikes": 2, "eliteSpread": 60, "downed": 2.5, "ground": 4 } }
```

- **Slime băng** (`icy`, 50 HP, xanh băng, vỏ băng trong suốt có viền xanh): vào trận có vỏ băng. Còn vỏ thì đòn vũ khí của mèo (vung thường, combo, lan, dư ảnh; cả khi đang cầm) chỉ còn ×`armor` ("xước"); Bom, kỹ năng, app, độc, cú ném không bị vỏ giảm. Còn vỏ thì mèo ưu tiên đánh slime khác (như bong bóng/dỗi). Mèo **đang bay sau cú ném** đi xuyên nó với tốc độ ≥ `throwHit.minSpeed` thì vỡ vỏ, rồi `throwHit` tác động như thường (mất 12, choáng 1,5 s). Đang ướt vì Bình xịt (vỏ vẽ đứt nét, có giọt nhỏ) thì chỉ cần ≥ `wetSpeed`. Vỡ rồi chỉ còn vài mảnh băng trên đầu, lớn dần; sau `regrow` s (tinh anh `eliteRegrow`; 0: không mọc lại) vỏ mọc lại. Pause đóng băng.
- **Slime bám tường** (`clinger`, 40 HP, xanh ô liu, hàng gai trên lưng, chân có vuốt): ra trận thì đi tới mép trái/phải gần nhất của dải trận (không đánh dọc đường) rồi leo lên (`climb` DIPs/s) tới khi chân cao `height` DIPs (con thứ hai cùng mép cao thêm `spacing`, tối đa 110). Trên mép nó nghiêng vào tường, có bóng dưới sàn, là quái trên không: mèo dưới sàn không nhắm/chọn, Quạt/Laser/đòn lan không tới; mèo đang cầm đưa lại gần thì tự vung vuốt (như `hover`). Mỗi `cooldown` s nó ngắm `aim` s (vòng đỏ đầy dần ở chỗ mèo trên sàn) rồi ném gai theo đường cong tới đó, rơi sau `flight` s: mèo **dưới sàn** trong `radius` mất máu như đòn của nó, không choáng; nhấc mèo, `landingGrace`, Khiên, Mai rùa né/chặn như giọt của slime phun. Tinh anh ném `eliteSpikes` gai cách nhau `eliteSpread` quanh chỗ mèo.
- **Làm rơi:** mèo đang bay sau cú ném đi xuyên nó với tốc độ ≥ `throwHit.minSpeed` (ném mèo vào mép tường chỗ nó bám; nó còn mất `throwHit.damage`), Đẩy (click vào nó khi chọn Đẩy), Kẹp gắp (click vào nó: **giật xuống ngay**, không treo theo con trỏ, tiêu Kẹp gắp) hoặc bất kỳ cú choáng nào. Nó rơi, nằm choáng `downed` s dưới sàn, đánh nhau dưới sàn như slime thường `ground` s rồi đi leo lại. Bom rơi sát mép vẫn trúng nó (Bom tính theo chiều ngang).
- Kiểm tra lúc nạp: một loại không vừa băng vừa bám tường; hai khóa này phải có `throwHit`; không cho boss, đá chắn, lao, nện, `hover`, hỗ trợ, hồi máu, liên kết, phun, lò xo, bom, tách đôi, quái dùng chuột, bay, đào hang; `wetSpeed` ≤ `throwHit.minSpeed`; `cling.height` 20–110; số liệu trong giới hạn.
- Sự kiện mới: `IceScratched`, `IceShattered`, `IceRegrown`, `ClingClimbed`, `ClingAimed`, `ClingShot`, `ClingKnocked` (Value 1: Kẹp gắp), `ClingLanded`; gai rơi là `GooLanded` (Value 1: trúng mèo). Cue có sẵn. Banner lần đầu mỗi trận (vi/en/ja): vũ khí chỉ xước, vỡ băng, slime bám tường leo lên mép, slime bám tường rơi.
- Chưa làm: Sổ tay quái cho hai quái ở M27 (hướng dẫn chương 2 có từ M21). Số liệu (`armor`, ngưỡng tốc độ ném, độ cao bám) là ước lượng, chỉnh sau khi chơi thật: chưa đo tốc độ ném thực tế của người dùng khi nhắm vào mép màn hình.

## Slime nhím, Slime hẹn giờ, Slime trộm (`quills`, `clock`, `steal`) (bản 1.5, mốc M16, 2026-10-29)

S9, S10, S11 của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetBattle.Quills.cs` (luật), `src/DesktopPet/PetBattleView.Quills.cs` (vẽ), test `tests/DesktopPet.Tests/BattleQuillsTests.cs`, ảnh UiChecks (`QuillsVisualChecks`: `enemy-quills.png`). **Ba quái có trong battle.json gốc nhưng chưa trận nào dùng** (`binSpit: false`): chương 1, trận tự do, Viễn chinh, Vô tận không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng, 232/232). Muốn thử: chép `docs/examples/battle-m16.json` thành `%LOCALAPPDATA%\DesktopPet\battle.json` rồi mở Trận tự do.

```json
{ "id": "hedgehog", "quills": { "flat": 3, "warn": 0.5, "puffed": 2.5, "elitePuffed": 4, "armor": 0.5, "prick": 5 } }
{ "id": "timer", "clock": { "seconds": 20, "eliteSeconds": 15, "stunned": 0.3, "calls": 2, "call": [ "slime", "slime", "runner" ] } }
{ "id": "thief", "damage": 0, "steal": { "minCharge": 0.3, "flee": 1.5, "eliteFlee": 1.9, "linger": 6 } }
```

- **Slime nhím** (`hedgehog`, 55 HP, nâu đất, hàng gai trên lưng): gai nằm `flat` s, dựng dần `warn` s (gai rung, báo trước), rồi xù `puffed` s (tinh anh `elitePuffed`), lặp lại. Đang xù thì đòn vũ khí của mèo (vung, combo, lan; không tính dư ảnh) chỉ còn ×`armor` và mèo bị gai đâm mất `prick` máu mỗi nhát, không choáng; Khiên, Mai rùa chặn như đòn thường; mèo nổi giận đánh lan trúng nhiều nhím thì bị đâm nhiều lần. Bom, kỹ năng, app, độc không bị giảm, không bị đâm lại. Đang xù thì mèo ưu tiên đánh slime khác. Ướt vì Bình xịt: gai cụp xuống ngay (`QuillsSoaked`) và nằm tới khi khô.
- **Slime hẹn giờ** (`timer`, 45 HP, xanh bạc hà, đồng hồ báo thức nhỏ trên đầu): đồng hồ chạy `seconds` s từ lúc vào trận (tinh anh `eliteSeconds`), vòng xanh vơi dần, dưới 5 s thì đỏ. Đang choáng (Đẩy, ném, Kẹp gắp, búa…) thì đồng hồ chạy ×`stunned` (vòng xanh nhạt). Hết giờ: reng, gọi nhóm `call` (1–4 quái đã biết, không boss, không phải slime hẹn giờ), báo bằng mũi tên ở mép sau lưng mèo, ra khi sàn còn chỗ (`maxAlive`); đợt chỉ xong khi hạ hết nhóm đó. Quân được gọi đánh dấu `SummonedBy = -1` nên không ăn ngân sách đợt. Đồng hồ chạy lại, tối đa `calls` lần (1–5). Hạ nó trước thì không gọi nữa; nhóm đã gọi mà chưa ra vẫn ra. Đồng hồ vẫn chạy khi mèo đang được cầm (nhóm gọi chờ như các đợt). Đợt mới hoặc trận kết thúc thì bỏ các lần gọi còn chờ.
- **Slime trộm** (`thief`, 35 HP, tím xám, mặt nạ đen): không bao giờ làm mèo mất máu (`damage` 0). Lượt gồng cạnh mèo là móc túi: lấy năng lượng của icon app sạch nhiều năng lượng nhất (≥ `minCharge`, chỉ trận có app), không thì lấy kỹ năng đang sẵn có thời gian hồi dài nhất (ô kỹ năng phải hồi lại từ đầu). Không có gì để lấy thì nó chỉ đi theo mèo, không gồng. Lấy xong thì đeo túi trên lưng (tia sét vàng: năng lượng app; sao xanh: kỹ năng), chạy ×`flee` tốc độ (tinh anh `eliteFlee`) về mép xa mèo, đi xuyên qua slime khác; tới mép thì đứng `linger` s (vòng đỏ đầy dần trên túi; đang choáng hoặc chạy theo Laser thì không đếm) rồi trốn mất: biến khỏi trận, không tính là mèo hạ, thứ bị lấy mất luôn. Hạ nó trước thì trả lại: icon được cộng lại phần năng lượng (icon bị nhiễm trong lúc đó thì không), ô kỹ năng hết thời gian hồi. Đẩy hoặc choáng lúc nó gồng thì không lấy được gì; Laser dụ, Bình xịt làm chậm như mọi slime.
- Kiểm tra lúc nạp: một loại chỉ có một trong ba khóa; không cho boss, đá chắn, lao, nện, `hover`, hỗ trợ, hồi máu, liên kết, phun, lò xo, bom, tách đôi, quái dùng chuột, bay, đào hang, băng, bám tường, gọi quân (`summonAt`, `phases`); `clock.call` chỉ gồm quái đã biết, không boss, không slime hẹn giờ; số liệu trong giới hạn.
- Sự kiện mới: `QuillsRising`, `QuillsUp`, `QuillsDown`, `QuillsSoaked`, `QuillsPricked`, `ClockStarted`, `ClockRang`, `ClockCalled`, `ThiefStole` (Value 1: kỹ năng, 0: app), `ThiefReturned`, `ThiefEscaped`. Cue có sẵn. Banner (vi/en/ja): nhím xù gai và bị gai đâm, slime hẹn giờ ra trận và reng (lần đầu mỗi trận); trộm lấy đồ, lấy lại, trốn mất (mỗi lần).
- Chưa làm: Sổ tay quái cho ba quái ở M27 (hướng dẫn chương 2 có từ M21, trừ slime hẹn giờ chưa vào chương nào); kiểu màn Đuổi trộm (T2) để sau 1.5. Số liệu (`prick`, thời gian xù, 20 s, `linger`) là ước lượng, chỉnh sau khi chơi thật.

## Slime Nén (boss `zip`: `roll`, pha `roped`/`pack`, `still`) (bản 1.5, mốc M17, 2026-10-29)

B1 của docs/UPDATE-1.5.md, trùm chương 2. Code: `src/DesktopPet.Core/PetBattle.Zip.cs` (luật), nối vào pha khiên có sẵn ở `PetBattle.Boss.cs`; vẽ `src/DesktopPet/PetBattleView.Zip.cs`; test `tests/DesktopPet.Tests/BattleZipTests.cs`, ảnh UiChecks (`ZipVisualChecks`: `enemy-zip.png`, `enemy-zip-roll.png`). **Boss và mảnh file có trong battle.json gốc nhưng chưa trận nào dùng** (`binSpit: false`; chương 2 ở M21, Viễn chinh tuyến 2 ở M23, Vô tận ở M24 đưa vào): chương 1, trận tự do, Viễn chinh, Vô tận không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng, 217/217). Muốn thử: chép `docs/examples/battle-m17.json` thành `%LOCALAPPDATA%\DesktopPet\battle.json`, mở Trận tự do và mang Kéo cắt.

```json
{ "id": "zip", "boss": true, "maxHp": 900, "summonCap": 10, "ropeCutSpeed": 300, "ropeCutStun": 1,
  "roll": { "every": 7, "enragedEvery": 4.5, "minRange": 140, "range": 520, "windup": 1.1, "speed": 360, "distance": 460, "damage": 24, "stun": 0.9 },
  "phases": [
    { "at": 0.7, "shield": true, "roped": true, "formation": "fortress" },
    { "at": 0.5, "shield": true, "roped": true, "formation": "cavalry" },
    { "at": 0.25, "shield": true, "pack": true, "enrage": true, "summons": [ { "enemy": "zipshard", "count": 4 } ] } ] }
{ "id": "zipshard", "maxHp": 40, "speed": 0, "damage": 0, "scale": 0.6, "still": true }
```

- **Pha 1, lăn đè** (`roll`, chỉ cho boss): mèo dưới sàn cách `minRange`–`range` (tâm tới tâm), đã qua `every` s từ lần lăn trước (nổi giận: `enragedEvery`; lần đầu sau nửa `every`) và hết hồi đòn thường thì boss gồng `windup` s (dải đỏ trên sàn chỉ hướng lăn, "!" trên đầu, không có vòng nện), rồi lăn `distance` DIPs với tốc độ `speed` theo hướng đã khóa, mặc mèo làm gì. Mèo dưới sàn nằm trên đường lăn (xét cả đoạn đường trong khung hình như cú lao) mất `damage` (tính theo hệ số như đòn thường của nó) và choáng `stun` s, mỗi lần lăn tối đa một lần; nhấc mèo lên (đã kéo quá ngưỡng) thì né; `landingGrace`, Khiên, Mai rùa như đòn thường. Gần hơn `minRange` thì nện như Vua Slime. Không lăn khi đang bật khiên (cả lúc nén). Hình: thân lăn xoay quanh tâm, nâng lên vừa đủ để không lún xuống sàn.
- **Pha 2, giải nén** (`roped` + `formation`): pha khiên như Vua Slime (dừng đòn ở vạch, bật khiên, nhảy về mép xa mèo, vòng tím báo chỗ ra), quân là thành viên của đội hình đã đặt tên (sau các quân `summon`/`summons` nếu có), banner "Đội hình …!" như đội hình của M12. Mỗi quân ra trận nối **dây nhầy về boss** (`Slime.RopedTo`). Còn dây nào thì boss không mất máu (kể cả Bom). Cắt dây: mèo đang cầm (quá ngưỡng kéo) hoặc đang bay sau cú ném đi xuyên dây với tốc độ ≥ `ropeCutSpeed` (xét theo đoạn đường giữa hai khung hình, như dây Slime xích), hoặc một nét Kéo cắt đi qua dây; quân đó choáng `ropeCutStun` s. Hạ quân thì dây của nó cũng mất. Hết dây thì khiên vỡ (choáng `shieldBreakStun` s), các quân đã đứt dây vẫn đánh tiếp. Kéo chậm, mới nhấn chưa kéo, Pause không cắt. Dây về boss không tính vào giới hạn 2 loại liên kết mỗi trận và không chia sát thương. `summonCap` 10 để đội hình sau vẫn ra đủ khi quân đội hình trước còn sống.
- **Pha 3, tự nén** (`pack`): boss bật khiên **tại chỗ** (không chạy về mép), vẽ nhỏ lại ×0,72 với hai đai quanh thân, các quân của pha (mảnh file) ra hai bên sát nó (bên nào hết sàn thì dồn sang bên kia), không tính `summonCap`. Khiên vỡ khi phá hết mảnh. Lúc nén boss không đi, không lăn, nhưng vẫn nện mèo đứng sát. Pha này có `enrage`: sau khi vỡ khiên boss nhanh hơn và lăn dày hơn.
- **`still`** (mảnh file): không đi, không đánh, không bị dồn chỗ (`Separate` bỏ qua); vẫn bị đẩy lùi, Đẩy, Kẹp gắp như slime thường. Chỉ cho quái thường (không boss, không đòn đặc biệt, không liên kết, không khóa 1.5 nào).
- Kiểm tra lúc nạp: `roll` chỉ cho boss, `minRange` < `range`, số liệu trong giới hạn; `formation` phải là đội hình đã biết; `roped`/`pack` chỉ cho pha khiên, không cùng lúc; tổng quân một pha (kể cả thành viên đội hình) tối đa 8; `ropeCutSpeed` 100–5000, `ropeCutStun` 0–5. Thành viên đội hình của pha boss tính vào luật "quái được gọi không gọi tiếp", giới hạn liên kết và quái dùng chuột như mọi quân gọi.
- Sự kiện mới: `RollAimed`, `Rolled`, `RollHit`, `BossRoped`, `BossRopeCut` (Value: id quân), `BossRopesGone`, `BossPacked`; đội hình của pha boss phát `FormationArrived`. Cue có sẵn. Banner (vi/en/ja): pha giải nén "… bung file! Còn dây thì nó không mất máu…", pha nén "… tự nén lại! Phá hết mảnh file…", mỗi lần cắt dây "Đứt dây! Còn N dây", lần lăn đầu mỗi trận "… sắp lăn! Nhấc mèo lên để né".
- Chưa làm: tranh/bản đồ chương 2 (M22), Sổ tay quái (M27), (chương 2 và hướng dẫn cho boss có từ M21). Số liệu (HP 900, lăn 24 + choáng 0,9 s, 4 mảnh 40 HP) là ước lượng, chốt bằng mô phỏng cân bằng chương 2 ở M21.

## Slime Mẹ và Vua Slime biến dị (boss `mother`: `brood`; boss `mutant_king`: `crownLinks`, `rain`) (bản 1.5, mốc M18, 2026-10-29)

B2 (boss phụ chương 2) và B3 (Vô tận, Viễn chinh) của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetBattle.Brood.cs`, `src/DesktopPet.Core/PetBattle.Mutant.cs` (luật; móc vào hợp thể ở `PetBattle.Links.cs`, phong tinh anh ở `PetBattle.Boss.cs`, giọt nhầy ở `PetBattle.Spit.cs`); vẽ `src/DesktopPet/PetBattleView.Brood.cs`; test `tests/DesktopPet.Tests/BattleBossM18Tests.cs`, ảnh UiChecks (`BroodVisualChecks`: `enemy-brood.png`, `enemy-rain.png`). **Hai boss có trong battle.json gốc nhưng chưa trận nào dùng** (`binSpit: false`; chương 2 ở M21, Viễn chinh tuyến 2 ở M23, vòng boss Vô tận ở M24): mọi trận hiện có không đổi (bảng mô phỏng cân bằng giống hệt bản trước từng dòng, 335/335). Muốn thử: chép `docs/examples/battle-m18.json` (Slime Mẹ) hoặc `docs/examples/battle-m18b.json` (Vua biến dị) thành `%LOCALAPPDATA%\DesktopPet\battle.json` rồi mở Trận tự do. Hai boss không chung một trận được: hợp thể + dây xích + nhịp trống là 3 loại liên kết, vượt giới hạn 2.

```json
{ "id": "mother", "boss": true, "maxHp": 650, "summonCap": 6,
  "brood": { "enemy": "fuser", "count": 3, "every": 10, "enragedEvery": 6.5, "warning": 0.6, "feedRate": 20 },
  "phases": [ { "at": 0.3, "enrage": true } ] }
{ "id": "mutant_king", "boss": true, "maxHp": 1000, "summonCap": 5, "rallyEvery": 9,
  "crownLinks": [ { "kind": "tether", "range": 220, "share": 0.5, "breakStun": 1, "relink": 6, "cutSpeed": 300 },
                  { "kind": "rhythm", "range": 220, "beat": 0.8, "beats": 4 } ],
  "rain": { "every": 12, "drops": 4, "spacing": 90, "warning": 1, "radius": 32, "damage": 14, "height": 150, "puddleSeconds": 5 },
  "phases": [ … ] }
```

Vua biến dị giữ pha khiên, nhảy về mép, vòng tím, phong tinh anh như Vua Slime chương 1: 3 pha khiên ở 75% / 50% / 25% (pha cuối nổi giận), mỗi pha gọi 3–4 quân trong đó có 1 slime phun.

- **Đẻ bầy** (`brood`, chỉ cho boss): cứ `every` s (nổi giận: `enragedEvery`; lần đầu sau 40% `every`), khi khiên tắt, không choáng, không đang nhảy, boss báo bằng vòng tím trên sàn `warning` s rồi đẻ `count` con `enemy` xếp sát nhau **ở phía mèo** (nên chụm ngay: cơ hội cho Bom len, Đẩy, Quạt). Tối đa `summonCap` quân của boss còn sống; hết chỗ thì 1 s sau thử lại. `enemy` phải là quái có liên kết `merge`, không phải boss, không gọi quân.
- **Về cho bú:** bầy của boss nhập lại theo luật hợp thể (M11); con nhập thành (Slime khổng lồ) là quân của boss (`SummonedBy`) và có `Slime.Nursing`: **không đánh mèo**, đi về chỗ boss (tim hồng trên đầu). Tới sát boss thì chuyển `feedRate` HP/s từ máu của nó sang boss (không quá máu tối đa; đường nét đứt hồng, tim bay), tới khi cạn máu thì biến mất (`Escaped`, không tính là mèo hạ, sự kiện `NurseFed` một lần với tổng HP đã cho). Bị đẩy ra xa thì đi lại bú tiếp. Mèo hạ nó giữa đường hoặc lúc đang bú thì boss không nhận phần còn lại (tính là mèo hạ). Đẩy, choáng, đèn laser, hoảng loạn giữ chân nó như mọi quái. Boss mất thì nó thôi bú và đánh như thường. Slime hợp thể không do boss đẻ (đợt thường, đội hình) không bú.
- **Liên kết vương miện** (`crownLinks`, cần `rallyEvery`): mỗi lần phong tinh anh, boss nối thêm một liên kết cho quân vừa thành tinh anh, lần lượt theo danh sách: `tether` (một quân khác của boss, gần nhất trong `range`, chưa có liên kết, cầm dây nối vào tinh anh và gánh `share` sát thương của nó; cắt như dây Slime xích) hoặc `rhythm` (tinh anh thành nhạc trưởng: nhịp `beats` thì mọi quái trong `range` cùng gồng). Trận đã đủ 2 loại liên kết khác thì bỏ qua loại đó, thử loại kế; không loại nào vừa thì không nối. Hai loại của vương miện tính vào giới hạn 2 loại mỗi trận khi nạp dữ liệu.
- **Mưa nhầy** (`rain`, chỉ cho boss): cứ `every` s (lần đầu sau nửa `every`), khi khiên tắt, không choáng, `drops` giọt rơi thẳng từ `height` DIPs xuống quanh mèo (giọt đầu đúng chỗ mèo, các giọt sau cách `spacing` lần lượt hai bên), mỗi giọt có vòng cam trên sàn `warning` s trước. Giọt rơi như giọt Slime phun: mèo dưới sàn trong `radius` mất `damage` (tính theo hệ số như đòn của boss), không choáng; nhấc mèo, `landingGrace`, Khiên bong bóng thì không trúng. `puddleSeconds` > 0: để lại vũng tăng tốc như Slime phun (cỡ `radius` × 1,3, Quạt làm khô).
- Kiểm tra lúc nạp: `brood`/`crownLinks`/`rain` chỉ cho boss; `brood.count` 1–6, `every` 2–60, `warning` 0–3, `feedRate` 0–500; `crownLinks` 1–4 mục, chỉ `tether`/`rhythm`, số liệu như liên kết thường; `rain.drops` 1–8, `warning` 0,3–5, `radius` 10–200, `height` 40–400, `puddleSeconds` 0–30. Dấu vẽ mới `marks`: `mother`, `mutant`.
- Sự kiện mới: `BroodWarned`, `BroodLaid` (Value: số con), `NurseHeading`, `NurseFed` (Value: HP đã cho), `CrownLinked` (Value: id quân cầm liên kết), `RainCalled`; giọt mưa rơi là `GooLanded`. Cue có sẵn. Banner (vi/en/ja): lần đẻ đầu "… sắp đẻ bầy!…", con đầu tiên về bú "Slime khổng lồ về cho … bú!…", mỗi lần bú xong "… được hồi N máu", mỗi lần vương miện nối dây hoặc trao trống, lần mưa đầu "… gọi mưa nhầy!…".
- Slime Mẹ có trong chương 2 từ M21 (cuối màn 4); Vua biến dị chưa vào trận gốc nào. Từ M23a Slime Mẹ có ở tinh anh "Tổ Slime Mẹ" của Viễn chinh tuyến 2. Từ M24 Slime Nén và Vua biến dị có trong vòng trùm Vô tận. Chưa làm: Sổ tay quái (M27). Số liệu (HP 650/1000, bú 20 HP/s, mưa 14) là ước lượng, chốt bằng mô phỏng ở M21/M29.

## Đồ rơi và xu (`drops`) (bản 1.5, mốc M19, 2026-10-29)

M4, M5 của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetBattle.Drops.cs` (luật), móc vào `Defeat`/`Finish`/`SetArea` ở `PetBattle.cs`, xu của chuyến ở `PetExpedition.cs` (`ExpeditionRun.Coins`); vẽ và bắt click `src/DesktopPet/PetBattleView.Drops.cs`; test `tests/DesktopPet.Tests/BattleDropTests.cs`, UiChecks `DropVisualChecks` (ảnh `drops.png`) và phần xu trong `ExpeditionChecks`.

```json
"drops": {
  "chance": 0.2, "eliteChance": 0.6, "bossDrops": 3, "life": 8, "fade": 1.5, "maxOnFloor": 6,
  "weights": { "fish": 3, "coin": 5, "potion": 2 },
  "fishHeal": 10, "coin": 1, "eliteCoin": 2, "bossCoin": 3
}
```

- **Chế độ nào có đồ rơi** (tham số `loot` của `BattleSim`, cửa sổ chọn lúc bắt đầu trận): Viễn chinh: cá, xu, bình. Vô tận: cá và bình (không xu). **Chương 1 Phiêu lưu và Trận tự do: không có gì** (chương 1 và rulesVersion của nó giữ nguyên). Chương 2 (từ M21, chương có `"drops": true`): cá và bình. Không có khóa `drops` trong dữ liệu thì không trận nào có đồ rơi.
- **Rơi:** slime bị mèo hạ (qua `Defeat`, kể cả hạ bằng độc/bom/app) rơi một món với xác suất `chance` (tinh anh: `eliteChance`), loại bốc theo `weights` (xu bị bỏ ở chỗ không có xu). Boss rơi `bossDrops` đồng xu, mỗi đồng `bossCoin` (chỉ ở Viễn chinh). Bản giả của ảo ảnh, bom tự nổ, trộm trốn, Slime khổng lồ bú xong không rơi (không phải mèo hạ). Tối đa `maxOnFloor` món trên sàn, món mới thay món cũ nhất.
- **Ngẫu nhiên riêng:** đồ rơi dùng luồng `BattleRandom.Drops` (12) của seed trận, nên trận không ai nhặt đồ chơi y hệt trận không có đồ rơi (bảng mô phỏng cân bằng không đổi, 240/240 dòng so với HEAD cũ).
- **Nhặt:** click trúng món (vùng nhận chuột rộng hơn hình một chút). Thứ tự click trong dải trận: mèo → kỹ năng đang chọn → **đồ rơi** → quái → icon app; quảng cáo che thì không nhặt được món phía sau. Cá: +`fishHeal` HP (không quá máu tối đa). Bình: kỹ năng mang theo đang hồi lâu nhất sẵn sàng ngay (không kỹ năng nào đang hồi thì không có tác dụng). Xu: +`coin` (tinh anh `eliteCoin`) vào xu của trận. Không nhặt được khi Pause, đang chọn thẻ, Hướng dẫn phiêu lưu đang đứng hình, hay trận đã kết thúc.
- **Tan:** món nằm `life` giây thời gian trận rồi biến mất; `fade` giây cuối mờ dần, 0,6 s cuối nhấp nháy. Đồng hồ chỉ chạy trong `Step`: Pause, chọn thẻ, hướng dẫn đứng hình đóng băng nó.
- **Kết thúc trận:** thắng thì gom luôn xu còn nằm trên sàn; mọi món còn lại biến mất. Thua thì xu của trận không được tính.
- **Xu trong chuyến (M5):** trận Viễn chinh thắng cộng `BattleSim.Coins` vào `ExpeditionRun.Coins` (lưu trong expedition.json; file cũ không có thì là 0). Rời trận hoặc thua thì không cộng; chuyến kết thúc là hết xu, không thành tiền tệ vĩnh viễn. HUD trận Viễn chinh hiện "Xu N" (xu chuyến + trận này), bản đồ hiện "N xu", kết quả trận hiện "Nhặt được N xu · chuyến có M xu". Tiêu ở cửa hàng mèo (từ M23a, xem "Tuyến Viễn chinh và cửa hàng mèo").
- Kiểm tra lúc nạp: `chance`, `eliteChance` 0–1; `bossDrops` 0–10; `life` 2–60; `fade` 0–`life`; `maxOnFloor` 1–20; `fishHeal` 0–200; `coin`, `eliteCoin`, `bossCoin` 1–100; `weights` ít nhất một loại, chỉ `fish`/`coin`/`potion`, mỗi trọng số 0–100.
- Sự kiện mới: `DropAppeared`, `DropFaded` (Value: id món), `DropPicked` (Value: loại); nhặt dùng cue `sparkle` có sẵn.
- rulesVersion của Viễn chinh tăng 6 → 7 ở M23a (cửa hàng; chuyến r6 đang đi vẫn đi tiếp); của Vô tận tăng 5 → 6 ở M24. Thẻ Mưu mẹo về đồ rơi (Mũi thính, Cá béo) có từ M25 (xem "Nhóm Mưu mẹo"); số liệu là ước lượng, chốt khi chơi thật/M29.

## Bảo vệ giỏ cá (`basket` của màn) (bản 1.5, mốc M20, 2026-10-29)

T1 của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetBattle.Basket.cs` (luật), móc vào `AddSlime` (`MarkTaker`), bước `Moving` của quái (`TakeFish`), `PickTarget` (ưu tiên con tha cá), `Defeat`/`Finish`/`SetArea` ở `PetBattle.cs`; kỷ lục ở `PetBattleRecords.cs` (`MostFish`) và `PetAdventure.cs` (`Apply(..., fishKept)`); vẽ và bắt click `src/DesktopPet/PetBattleView.Basket.cs`; test `tests/DesktopPet.Tests/BattleBasketTests.cs`, UiChecks `BasketVisualChecks` (ảnh `basket.png`).

```json
"basket": { "fish": 5, "at": 0.5, "every": 2, "grab": 1.2, "carry": 0.8, "back": 5 }
```

- Khóa của **màn** (`StageSpec`, cùng chỗ `objective`), dùng được với màn `clear` và `survive`. Không có khóa thì màn chơi y như trước. **Chưa màn gốc nào dùng** (chương 2 ở M21); thử bằng `docs/examples/battle-m20.json` (một chương thay chương 1 khi file còn đó: màn 5 cá ba đợt, màn sống sót 60 giây 4 cá).
- **Giỏ:** đứng ở `at` (0–1 bề ngang dải trận; mặc định giữa) với `fish` con cá. HUD hiện "Cá N/M" (cá chưa bị mang đi hẳn / tổng); bản đồ và bảng chuẩn bị ghi "· giữ giỏ M cá".
- **Slime tha cá:** cứ `every` con slime "thường" ra trận (đếm theo thứ tự ra, cả quân gọi thêm) thì một con là slime tha cá. Chỉ slime không có việc riêng mới tha: không boss, hỗ trợ (thảo dược), đá, mảnh file, quái bay/trên không, quái có liên kết, phun, lò xo, bom, đào hang, bám tường, trộm, bóng ma, bong bóng, ảo ảnh, dỗi, băng, nhím, hẹn giờ (`BattleSim.MayTakeFish`). Trong luật hiện có: slime thường, slime nhanh, slime tách đôi và nửa của nó, slime khổng lồ.
- Slime tha cá bỏ qua mèo: đi tới con cá rơi gần nhất trên sàn, không có thì tới giỏ (còn cá); đứng `grab` giây (cá rơi: một nửa; vòng vàng đầy dần) thì lấy một con, giơ cá trên đầu và chạy ×`carry` về mép xa phía bên kia mèo. Tới mép thì biến mất cùng con cá: **mất một cá**, không tính là mèo hạ (như trộm trốn). Không còn cá để lấy thì nó đánh mèo như slime thường. Đang tha cá thì không chen chỗ với slime khác.
- **Làm rơi cá:** slime đang tha bị hạ, choáng (Đẩy, búa, kỹ năng…) hoặc bị Kẹp gắp thì cá rơi tại chỗ. Cá rơi nằm trên sàn có vòng trắng đếm ngược: click (thứ tự click: mèo → kỹ năng đang chọn → đồ rơi → **cá rơi** → quái → icon) thì về giỏ ngay; để yên `back` giây thì tự về giỏ; slime tha cá khác có thể nhặt nó trước (nhanh hơn lấy từ giỏ). Pause/chọn thẻ/hướng dẫn đứng hình: đồng hồ cá rơi đứng yên, không click được.
- **Mèo ưu tiên** con đang tha cá (gần mèo nhất trong số đó), trừ khi người dùng đã chọn quái khác.
- **Thua khi mất hết cá**, dù mèo còn máu (`FishGone`); mèo đang được cầm thì slime vẫn tha cá. Thua vì hết máu thì như cũ. **Thắng:** cá đang rơi trên sàn hoặc còn trên tay slime (màn sống sót hết giờ) được tính là giữ được.
- **Kết quả/kỷ lục:** kết quả trận ghi "giữ được N/M cá" (thắng) hoặc "mất N/M cá" (thua); thua vì mất cá có tiêu đề "Thua rồi… slime tha hết cá". Kỷ lục màn (`BattleRecord.MostFish`, theo khóa kỷ lục của màn: rulesVersion, vũ khí, độ khó, không app) giữ **số cá nhiều nhất trong một trận thắng**; thua không bao giờ ghi cá, không tính thắng, không có "thắng nhanh nhất". Tin "Kỷ lục mới của màn: giữ được nhiều cá nhất!" khi vượt; bản đồ ghi "· giữ nhiều nhất N cá". File kỷ lục cũ không có trường này đọc là 0.
- Kiểm tra lúc nạp: `fish` 1–20, `at` 0,15–0,85, `every` 1–6, `grab` 0,2–5, `carry` 0,3–2, `back` 1–30.
- Sự kiện mới: `FishTaken` (Value: id slime), `FishDropped` (Value: id cá), `FishReturned` (Value: 1 click, 0 tự về), `FishLost` (Value: cá còn lại). Cue có sẵn (`swoosh`, `pop`, `satisfied`, `annoyed`). Banner (vi/en/ja): lần đầu mỗi trận "Slime tha cá!…", "Cá rơi! Click vào cá để trả về giỏ"; mỗi lần mất cá "Mất một con cá! Còn N".
- Không dùng luồng ngẫu nhiên nào; bảng mô phỏng cân bằng giống hệt HEAD cũ (240/240 dòng).
- Từ M21 có trong chương 2 (màn 3 "Giữ giỏ cá", kèm bước hướng dẫn); từ M23a ở vùng Kho cá của Viễn chinh tuyến 2 (màn "hạ hết quái" có giỏ: mất hết cá vẫn thua). Số liệu là ước lượng.

## Chương 2 "Bãi rác Tải về" (bản 1.5, mốc M21, 2026-10-29)

C1 của docs/UPDATE-1.5.md. Dữ liệu: chương `download_dump` trong `campaign` của battle.json. Luật: `src/DesktopPet.Core/PetAdventure.cs` (khóa chương, mượn kỹ năng, đồ rơi theo chương), `PetBattleData.cs` (khóa mới), `PetBattle.Apps.cs` (`maxMalware` của màn). Giao diện: `PetWindow.Adventure.cs` (bản đồ nhiều chương), `PetWindow.BattlePrep.cs` (kỹ năng mượn, gợi ý mang), `PetWindow.Battle.cs` (bộ kỹ năng và đồ rơi lúc vào trận), `PetWindow.BattleGuide.cs` (bước hướng dẫn chương 2). Test: `tests/DesktopPet.Tests/ChapterTwoTests.cs`, `AdventureBalanceTests.RunChapter2`, UiChecks trong `TutorialChecks`.

**Khóa mới**
- Chương: `"unlockAfter": "<id màn>"`: màn đầu của chương chỉ mở khi đã thắng màn đó (phải là màn của chương **trước**; sai thì cả file bị bỏ). `"drops": true`: slime trong chương rơi cá và bình (không có xu; số liệu ở khóa `drops` chung).
- Màn: `"maxMalware": 1–4` (chỉ màn có malware): số loại app độc cùng lúc thay cho `apps.maxMalware` (chương 2: 4, chương 1 giữ 2). `"suggestSkills": [ … ]` (1–5 id khác nhau): dòng "Gợi ý mang: …" trên bản đồ và bảng chuẩn bị.

**Luật**
- Thắng Vua Slime (`c1_slime_king`) thì mở chương 2; khung kết quả ghi "Hoàn thành chương! Đã mở chương 2: …", nút chính thành "Chương tiếp". Màn đã thắng thì luôn mở. Chương 2 bắt đầu build rỗng (build chỉ mang trong một chương).
- **Mượn kỹ năng:** màn có `unlocksSkill` mà kỹ năng đó chưa mở thì cho mượn: trận luôn mang nó; bộ đang chọn chưa có thì nó thay **ô cuối** (theo thứ tự HUD; với 5 kỹ năng cũ là Khiên). Ở bảng chuẩn bị ô mượn không bỏ được, 4 ô kia đổi tự do; bộ có kỹ năng mượn **không lưu** thành lựa chọn (Chơi lại cũng mượn lại). Thắng màn thì kỹ năng mở hẳn cho Phiêu lưu, Viễn chinh, Vô tận.
- Bản đồ mở ở chương mở mới nhất (hoặc chương của màn được chọn); nút ‹ › cạnh tiêu đề đổi chương (xem được chương còn khóa; màn đầu ghi "Chưa mở — thắng Vua Slime (chương 1) để mở chương này"). Sảnh và túi đồ theo chương mở mới nhất; trang Phần thưởng liệt kê kỷ lục của mọi chương đã mở.

**5 màn** (`rulesVersion` 1; số liệu chốt bằng mô phỏng, chưa chơi thật)

| Màn | id | Kiểu | Quái mới | Cho mượn / mở | threat |
|---|---|---|---|---|---|
| 1 Cổng tải về | `c2_download_gate` | dọn 3 đợt | phun, lò xo, hợp thể | Quạt | 1,9 |
| 2 Thư mục ma | `c2_ghost_folder` | dọn 3 đợt | bóng ma, ảo ảnh, nhạc trưởng, cờ hiệu | Đèn laser | 2,2 |
| 3 Giữ giỏ cá | `c2_fish_basket` | sống sót 75 s, giỏ 5 cá (`every` 3, `at` 0,3) | bom cưỡi, đào hang, bay, trộm | Kẹp gắp | 2,2 (+0,5/phút) |
| 4 Đầm nhầy | `c2_slime_swamp` | dọn 4 đợt, cuối là Slime Mẹ | nhím, băng, xích, tách đôi | Bình xịt nước | 1,95 |
| 5 Slime Nén | `c2_zip_slime` | dọn 3 đợt, cuối là Slime Nén | bong bóng, bám tường | Kéo cắt | 1,45 |

Mỗi trận ≤ 2 loại liên kết và ≤ 2 loại quái dùng chuột (kiểm tra lúc nạp). Slime dỗi, hẹn giờ, Vua biến dị không vào chương 2 (để Viễn chinh tuyến 2 / Vô tận). Tranh, bản đồ, phông riêng và phần thưởng chương 2: mục "Hình và phần thưởng chương 2" (M22).

**Hướng dẫn đứng hình** (như chương 1, mặc định bật): 5 bước kỹ năng có sẵn (`fan`, `laser`, `grabber`, `spray`, `scissors`) chạy ở màn cho mượn; thêm bước "gặp" (xem, Hiểu rồi) lần đầu mỗi loại quái mới lộ ra, sáng đúng chỗ vẽ (slime bay trong cửa sổ riêng, slime bám tường/bong bóng ở trên cao): `spitter`, `springer`, `fuser`, `drops` (món rơi đầu tiên), `phantom`, `mirage`, `drummer`, `flag`, `basket`, `bomber`, `flyer`, `thief`, `hedgehog`, `icy`, `chain`, `splitter`, `mother`, `clinger`, `zip`, `ropes`, `pack`; và bước "làm": `fish_back` (click cá rơi về giỏ), `crack` (nhấc mèo khi có vòng nứt), `bubble` (chọc vỡ bong bóng), `roll` (nhấc mèo trước cú lăn). Từ 2026-10-30 (sau M26) thêm 5 bước app mới, chạy ở **màn nào có `newAppsFrom`** (cả 5 màn chương 2, vì app độc ra ngẫu nhiên), mỗi bước một lần mỗi lượt: `screenshot` (icon Chụp Màn Hình đầy lần đầu, bấm nó hoặc Hiểu rồi), `dazzle` (xem: Chụp Màn Hình bị nhiễm làm mèo lóa; sáng mèo và icon), `captcha` (sáng cửa sổ Captcha, xong khi bấm hết ô mèo), `battery` (sáng dải Pin yếu, xong khi Cắm sạc), `cursor` (sáng lớp Con trỏ đơ, xong khi Tiếp tục). Trận đứng hình nên đồng hồ Captcha và Con trỏ đơ cũng dừng; ô Captcha, Cắm sạc, Tiếp tục và icon vẫn bấm được.

**Mô phỏng cân bằng** (`AdventureBalanceTests.RunChapter2`, 20 seed, chơi cả chương theo thứ tự, mang build, bot mang theo "Gợi ý mang" và nhặt đồ rơi; bot "simple"/"casual" còn biết rê chuột lên bóng ma/ảo ảnh, chọc bong bóng, trả cá về giỏ, dùng Laser/Quạt/Xịt/Kéo cắt theo luật đơn giản, Đẩy slime bay/bám tường; không dùng Kẹp gắp, không nhấc mèo). Đạt: Thường, simple ≥ 80% mọi màn (19–20/20); trung bình ≤ 4 phút (dài nhất ~104 s); bot đứng im ≤ 30% ở màn 2–5 mức Thường; simple còn ≤ 90% máu ở màn 3–5 (màn 2 cho tới 95%: bot soi bóng ma và đặt laser tức thì, tăng threat chỉ làm bot đứng im về 0%; casual còn ~82%); kiếm và búa chênh ≤ 30 điểm, **trừ Slime Nén mức Khó cho tới 40 điểm** (búa luôn kém ~35 điểm ở mọi threat đã thử 1,7 → 1,45; xem lại sau khi chơi thật). Bảng chương 1 giống hệt HEAD cũ (195/195 dòng).

## Hình và phần thưởng chương 2 (bản 1.5, mốc M22, 2026-10-29)

C2, C3 của docs/UPDATE-1.5.md. Chỉ là hình và phần thưởng: luật trận, quái, số liệu, `rulesVersion` và kỷ lục không đổi (bảng mô phỏng cân bằng không bị ảnh hưởng).

**Khóa mới**
- Chương: `"scene": "dump"` (chỉ có giá trị này; khác thì cả file bị bỏ): hình riêng của chương. Không có (chương 1): đồng cỏ như cũ, không phông trận.
- Màn: `"keepsake": { "id", "kind", "nameVi", "nameEn" }` (tùy chọn, `tier` nếu `kind` là `cup`): một kỷ vật trưng bày được (`plush`, `cushion`, `cup`, `hourglass`, `orb`; không `accessory`), trao **một lần** lần đầu thắng màn, cùng lúc với `reward`, lưu chung danh sách `rewards` của adventure-progress.json. id theo luật id của battle.json (chữ thường), không trùng kỷ vật khác của Phiêu lưu, Viễn chinh, Vô tận. Màn của Viễn chinh không có `keepsake`.
- `kind` kỷ vật mới `plush`: thú bông Slime Nén (vẽ trong PetKeepsakeArt.cs, rộng 36).

**Chương 2 (`download_dump`) dùng**
- `"scene": "dump"`: bản đồ (banner tranh "Bãi rác Tải về": đống thư mục, trang giấy, mũi tên tải về, thanh tiến trình trên trời, mèo giữa slime xanh và Slime Nén, giỏ cá; đổi chương bằng ‹ › thì banner đổi theo), mốc dưới mỗi màn (tải về, thư mục, cá, trang giấy, vương miện; chương 1 giữ cờ, kiếm, khiên, nấm, vương miện), thẻ Phiêu lưu ở Sảnh (tranh của chương mở mới nhất), và **phông trận**: trận của màn chương 2 có một cửa sổ riêng xuyên chuột nằm ngay dưới dải trận (cùng chỗ, cùng kích thước): hai đống thư mục + trang giấy ở hai đầu, biển tải về cạnh đống trái, vài trang giấy rải trên sàn; giữa dải không có gì ngoài trang giấy sát sàn. Mọi click đi xuyên xuống cửa sổ bên dưới; slime vẽ đè lên phông; mèo vẫn ở trên cùng. Vẽ một lần, ẩn cùng dải trận khi có app fullscreen, đóng khi hết trận. Chương 1, Viễn chinh, Vô tận, trận tự do không có phông.
- Màn 3 Giữ giỏ cá: `"reward": "FolderHat"` — phụ kiện **Mũ thư mục** (accessories.json, `reward: "download_dump"`, lớp đầu): đội trong Tùy chỉnh → Phụ kiện.
- Màn 5 Slime Nén: `"keepsake": { "id": "zip_plush", "kind": "plush", ... }` — **Thú bông Slime Nén**: kỷ vật trên góc trưng bày như các món khác (cầm, lắc, ném, rơi, nảy, đứng trên gờ cửa sổ, choáng Bonk khi rơi trúng mèo — luật kệ kỷ vật có physics, docs/APPROVED-EXCEPTIONS.md; không có ngủ/nảy như đệm slime). Chọn trong Tùy chỉnh hoặc menu chuột phải món đồ; kết quả trận ghi "Kỷ vật mới: Thú bông Slime Nén (ở góc trưng bày)". Trang Phần thưởng có mục mới "Kỷ vật Phiêu lưu".

Code: `ChapterSpec.Scene`, `StageSpec.Keepsake`, `Adventure.Keepsakes`/`GrantRewards` (PetAdventure.cs), `PetScene.cs` (`PetSceneKind.Dump`), `PetIllustration.cs` (`Folder`, `Download`, `File`), `PetWindow.BattleScenery.cs` (phông trận), `PetKeepsakeArt.cs` (thú bông), `PetWindow.Keepsake.cs`/`PetWindow.Rewards.cs`. Test: `tests/DesktopPet.Tests/ChapterRewardTests.cs`, UiChecks `ChapterArtChecks` (ảnh `scene-dump.png`, `landmarks-dump.png`, `keepsake-plush.png`, `accessory-folder-hat.png`, `battle-scenery-dump.png`, `trophies-plush.png`).

## Tuyến Viễn chinh và cửa hàng mèo (`routes`, `zones`, `shop`, `keepRuns`) (bản 1.5, mốc M23a, 2026-10-29)

- **Tuyến (`expedition.routes`):** mỗi tuyến có `id`, tên, `rulesVersion`, `unlockAfter`, bản đồ (`steps` hoặc `kindGacha`), `fights`/`elites`/`boss` (id trận không trùng giữa các tuyến), `zones`, `scene`. Luật HP (`retreatCost`, `winHeal`, `stepThreat`, `restHeal`, `maxRelics`, `eliteRelics`), sự kiện, di vật, máy viên nang và cửa hàng lấy chung của `expedition`; tuyến tự khai các khóa đó thì bị từ chối. Tuyến không có kỷ vật. Tối đa 4 tuyến thêm. Code đọc qua `ExpeditionSpec.All` (tuyến 1 là chính `expedition`).
- **Tuyến 2 "Lối Bãi rác" (`dump_trail`):** mở khi thắng `c2_zip_slime`; 6 bậc (máy quay bậc như tuyến 1, thêm ô cửa hàng) rồi trùm `y_zip` "Slime Nén trở lại"; phông trận `dump` như chương 2. Ba vùng, mỗi vùng 2 bậc: Đầm nhầy (vũng nhầy, ao băng, bùn tách đôi; tinh anh Tổ Slime Mẹ), Kho cá (thùng cá và cướp cá giữ giỏ cá ở dạng "hạ hết quái", đường hầm cá; tinh anh Vụ trộm cá có giỏ 6 cá), Mê cung thư mục (thư mục ẩn, sảnh trống, tường thư mục; tinh anh Pháo đài thư mục dùng đội hình Pháo đài rồi Kỵ binh, Diễu hành bóng ma). Mọi trận `maxMalware` 4.
- **Vùng (`zones`):** theo thứ tự bậc, `steps` mỗi vùng cộng lại bằng số bậc trước trùm; bậc nào chỉ rút giao tranh/tinh anh trong `fights`/`elites` của vùng đó (`ExpeditionSpec.Pool(kind, step)`). Bản đồ ghi "Vùng: A → B → C" trước chuyến và tên vùng trên dòng trạng thái.
- **Chọn tuyến:** trên bản đồ Viễn chinh khi chưa có chuyến, một chip mỗi tuyến (tuyến khóa mờ, tooltip nói thắng màn nào để mở; chỉ hiện khi có hơn một tuyến). Chỉ một chuyến tại một lúc; chuyến đang đi luôn ở tuyến của nó (`ExpeditionRun.Expedition` = id tuyến). Tuyến chọn chỉ nhớ trong phiên. Kỷ lục theo khóa `tuyến|r<rulesVersion>|vũ khí|độ khó`, mỗi tuyến riêng. Sảnh: "Đang có chuyến · <tuyến> · Bậc…"; kỷ vật đếm theo tuyến 1.
- **Cửa hàng mèo (`shop`, loại ô `shop`):** không ở bậc 1, mỗi bậc tối đa một ô (như nghỉ). Vào ô thì rút hàng một lần (luồng ngẫu nhiên của ô, như nghỉ/sự kiện) và giữ trong chuyến: bữa cá (+`heal` HP tối đa), `cards` thẻ thường (lấy từ lượt chọn thẻ như ở điểm nghỉ), `rares` thẻ hiếm còn lấy được (thẻ có `skill` chỉ khi mang kỹ năng đó), `relics` di vật (khi chuyến còn chỗ). Giá bằng xu chuyến: `healPrice` 2, `cardPrice` 3, `rarePrice` 5, `relicPrice` 6. Mỗi món mua một lần, mua bao nhiêu tùy xu, rồi "Đi tiếp" qua bậc. Bữa cá bị từ chối khi đầy máu. Mỗi lần mua ghi vào nhật ký chuyến (Túi đồ) kèm giá. Kiểm tra: giá 1–99, `heal` (0, 1], `cards` 0–4, `rares` 0–2, `relics` 0–2; máy quay có trọng số `shop` hoặc `steps` có ô shop mà không có `shop` thì lỗi.
- **Chuyến cũ (`keepRuns`):** tuyến 1 lên rulesVersion 7 (`keepRuns: [6]`): chuyến bắt đầu dưới luật 6 vẫn đi tiếp tới hết, các bậc chưa quay của nó không ra cửa hàng, kết thúc tính vào kỷ lục r6. Chuyến luật 5 trở xuống vẫn kết thúc như cũ. `keepRuns` chỉ nhận số nhỏ hơn `rulesVersion`.
- **File:** expedition.json lên version 5 (lựa chọn đang mở có `Wares`/`Sold`, nhật ký có `Coins`); bản cũ đọc file mới ở chế độ chỉ đọc. Chuyến ở tuyến mà dữ liệu không còn thì kết thúc khi mở bản đồ.
- **Mô phỏng cân bằng (không phải chơi thật, ExpeditionTests):** tuyến 2, Normal, người chơi đơn giản thắng 29–33/40 (tuyến 1: 34–39/40); Hard 9–12/40. Số liệu trận, giá và trọng số `shop` (tuyến 1: 12, tuyến 2: 14) là ước lượng; xu thật phụ thuộc người chơi click nhặt, chốt khi chơi thật/M29.
- Sự kiện và di vật mới: mốc M23b (mục kế tiếp).

## Sự kiện và di vật mới của Viễn chinh (bản 1.5, mốc M23b, 2026-10-29)

Có ở cả hai tuyến (tuyến lấy chung `events`/`relics` của `expedition`). `rulesVersion` không đổi (M23a và M23b là một đợt đổi luật của bản 1.5 chưa phát hành, kỷ lục vẫn r7 / tuyến 2 r1). expedition.json lên version 6 (`Forged`, `WeaponBonus`, `Cursed`, `CurseThreat`, `CurseElite` của chuyến; bản trước đọc file mới ở chế độ chỉ đọc).

**Sự kiện** (`events`, loại mới, không cần `relics`):
- `forge` **Lò rèn** (`coins` 4, `cost` 0.15, `amount` 0.15, `levels` 2): trả `coins` xu **hoặc** `cost` HP tối đa để vũ khí chém mạnh hơn `amount` đến hết chuyến; tối đa `levels` lần mỗi chuyến (lần sau bị từ chối "đã rèn tối đa"); đã rèn đủ thì máy quay không bốc Lò rèn nữa (trừ khi không còn sự kiện nào khác). Thưởng chỉ nhân vào nhát chém của vũ khí (cả khi mèo bị cầm), không nhân app, độc hay kỹ năng.
- `curse` **Lời nguyền** (`cards` 2, `amount` 0.25, `battles` 2, `eliteChance` 0.15): chọn 1 trong `cards` thẻ hiếm còn lấy được (thẻ có `skill` chỉ khi mang kỹ năng đó) hoặc đi tiếp; lấy thì `battles` trận kế (giao tranh, tinh anh hay trùm) quái mạnh hơn `amount` (nhân vào threat) và tỉ lệ tinh anh +`eliteChance` (cả trận vốn không có tinh anh; độ khó Dễ vẫn không có tinh anh). Mỗi trận thắng trừ một; rời trận không trừ. Bị nguyền thêm lần nữa thì lấy số lớn hơn, không cộng dồn.
- `lost` **Mèo bị lạc** (`cost` 0.1, `coins` 3, `cards` 3): mất `cost` HP tối đa, nhận `coins` xu và chọn 1 trong tối đa `cards` thẻ (lượt đưa thẻ như ở điểm nghỉ), hoặc đi tiếp. Không đủ máu (phải còn ≥ 1 HP) thì nút bị khóa.
- Kiểm tra lúc nạp: `coins` 1–99 chỉ cho forge/lost, `amount` (0, 1] chỉ cho forge/curse, `levels` 1–5 chỉ forge, `battles` 1–5 chỉ curse, `eliteChance` 0–1 chỉ curse, `cards` 1–3 chỉ curse/lost, forge/lost cần `cost` > 0.
- Bản đồ: Lò rèn có hai nút "Rèn: −4 xu" / "Rèn: −N HP" và dòng "Đã rèn x/2 lần · chuyến có N xu"; Lời nguyền liệt kê thẻ hiếm, nút "Lấy thẻ, chịu lời nguyền"; Mèo bị lạc liệt kê thẻ, nút "Giúp: −N HP, +3 xu, lấy thẻ đã chọn". Dòng di vật ghi "⚒ Rèn ×n: +X% chém" và "☠ Lời nguyền: còn n trận"; chi tiết trận bị nguyền ghi "☠ lời nguyền: dễ ra tinh anh hơn". Túi đồ có dòng cảnh báo lời nguyền/lò rèn, nhật ký chuyến ghi từng lựa chọn (nhật ký `Coins`: xu đã trả, số âm là xu nhận).

**Di vật** (`relics`, `effect` mới; tối đa 12 di vật, gốc có 10):

| `effect` | Gốc | Được | Giá |
|---|---|---|---|
| `magnet` Nam châm xu | `amount` 0.5, `penalty` 5 | tỉ lệ rơi đồ ×(1 + `amount`) | đồ rơi nằm sàn tối đa `penalty` giây (2–30; 0 = không đổi; không ngắn hơn `fade`) |
| `thorns` Vòng gai | `amount` 0.3, `penalty` 0.1 | slime **đánh gần hoặc lao** trúng mèo chịu lại `amount` phần máu mèo thật sự mất (cộng với cộng hưởng Gai nếu có) | nhát chém −`penalty` |
| `venom` Răng nanh độc | `onHit` độc 3 s, 3 máu/s, `penalty` 0.1 | mỗi nhát chém trúng gây `onHit` (luật độc M04) | thời gian hồi đòn ×(1 + `penalty`) |
| `frost` Viên đá lạnh | `onHit` chậm 30% 2 s, `penalty` 0.15 | mỗi nhát chém trúng gây `onHit` (đóng băng) | nhát chém −`penalty` |
| `battery` Pin dự phòng | `amount` 0.2 | thời gian hồi kỹ năng ×(1 − `amount`) (nhân riêng, sau thẻ) | không rơi bình |
| `helmet` Mũ bảo hiểm | `penalty` 0.1 | không bị choáng bởi cú lăn (Slime Nén), lò xo bật, slime đào hang trồi lên (vẫn mất máu); lao, bom, chuông app vẫn choáng | tốc chạy −`penalty` |

- `onHit` chỉ cho `venom` (phải là `poison`) và `frost` (phải là `chill`), kiểm tra như `onHit` của vũ khí. `amount` của thorns/battery ≤ 0.9; `penalty` của thorns/venom/frost/helmet ≤ 0.9, magnet ≤ 30.
- Cắt sát thương chém của thorns/frost nhân riêng với thẻ (tối thiểu ×0,1), không đổi sát thương app/độc.

**Mô phỏng (ExpeditionEventTests, ExpeditionTests; không phải chơi thật):** mỗi di vật mới giữ từ đầu chuyến, kiếm + búa, Normal, người chơi đơn giản, đường tham: tuyến 1 không di vật thắng 22/24, các di vật 19–22/24 (Mũ bảo hiểm thấp nhất vì tuyến 1 ít đòn lăn/lò xo/đào hang); tuyến 2 không di vật 20/24, di vật 20–23/24. Bảng cân bằng chung sau khi thêm sự kiện: tuyến 1 Normal đơn giản an toàn 39/40, tham 32–34/40; tuyến 2 Normal 31–35/40, Hard 11–13/40. Số liệu là ước lượng, chốt sau khi chơi thật/M29.

## Viễn chinh (`expedition`) (2026-10-21)

Một chuyến đi gồm nhiều bậc, mỗi bậc chọn một điểm, bậc cuối là trùm. HP và build của mèo đi tiếp từ bậc này sang bậc sau; chuyến mới bắt đầu lại từ đầu. Code: `src/DesktopPet.Core/PetExpedition.cs` (luật, không WPF), `PetWindow.Expedition.cs` (bản đồ, kết quả).

```json
"expedition": {
  "id": "slime_trail", "nameVi": "Đường mòn Slime", "nameEn": "The Slime Trail",
  "rulesVersion": 5, "unlockAfter": "c1_slime_king", "retreatCost": 0, "winHeal": 0.05, "stepThreat": 0.1,
  "restHeal": 0.3, "maxRelics": 3, "eliteRelics": 2,
  "kindGacha": { "steps": 5, "minLanes": 2, "maxLanes": 3, "weights": { "fight": 45, "event": 20, "elite": 20, "rest": 15 }, "rerollCost": 0.08 },
  "capsules": { "freeHeal": 0.1, "freeCard": 0.5, "goldCost": 0.12, "goldHeal": 0.1, "relic": 40, "rare": 30, "cardHeal": 30 },
  "events": [ { "id": "slime_chest", "kind": "chest", "heal": 0.1, "cost": 0.15, "nameVi": "...", "nameEn": "...", "descVi": "...", "descEn": "..." } ],
  "relics": [ { "id": "counter_bell", "effect": "counter", "amount": 0.6, "penalty": 3, "nameVi": "...", "nameEn": "...", "descVi": "...", "descEn": "..." } ],
  "fights": [ { "id": "x_meadow", ... như một màn Phiêu lưu ... } ],
  "elites": [ ... ],
  "boss": { "id": "x_king", ... }
}
```

- `kindGacha` (2026-10-22, dữ liệu gốc dùng cái này thay `steps`): bản đồ lúc đầu chỉ lộ trùm; khi chuyến tới một bậc, người chơi bấm **Quay máy** và bậc đó được bốc `minLanes`–`maxLanes` điểm (1–3) theo `weights` (`fight`, `elite`, `rest`, `event`; 0–1000, `fight` phải > 0). Luật luôn giữ: bậc 1 không có tinh anh/nghỉ; mỗi bậc tối đa 1 nghỉ và không chỉ toàn tinh anh; bậc ngay trước trùm luôn có 1 điểm nghỉ; không quá số trận/sự kiện trong kho. `steps` (1–10): số bậc trước trùm. Bốc từ seed của chuyến (seed riêng cho mỗi bậc), lưu ngay vào chuyến (`Kinds`, `Map`), nên quay lần hai, đóng/mở app hay quay lúc khác đều ra đúng kết quả đó. `rerollCost` (0–0,5, 2026-10-22, gốc 0,08): bấm **Quay lại** để bốc lại bậc đang đứng, mỗi bậc tối đa 1 lần, trước khi vào/ghé điểm nào của bậc đó, tốn chừng ấy phần HP tối đa (không cho quay nếu HP không đủ để còn sống); kết quả quay lại cũng bốc từ seed (luồng riêng) và được lưu, ghi vào nhật ký chuyến; 0 = tắt.
- `capsules` (2026-10-22, tùy chọn): máy viên nang sau mỗi trận **giao tranh** thắng (không sau tinh anh — đã có di vật — và không sau trùm). Cả hai viên được bốc lúc thắng, lưu trong chuyến, chỉ lộ khi mở. Viên thường miễn phí: `freeCard` (0–1) xác suất là 1 thẻ thường từ lượt đưa thẻ của build, còn lại hồi `freeHeal` HP tối đa. Viên vàng tốn `goldCost` (0–0,9) HP tối đa (phải còn ≥ 1 HP): theo trọng số `relic` (1 di vật chưa có, chỉ khi còn chỗ), `rare` (1 thẻ hiếm chưa đầy), `cardHeal` (1 thẻ + `goldHeal` HP tối đa). Có thể bỏ qua.
- `steps`: loại điểm cố định của từng bậc (trước trùm), 1–3 điểm mỗi bậc, theo thứ tự làn. Dùng `steps` **hoặc** `kindGacha`, không cả hai; file của bạn đưa `steps` thì thay `kindGacha` của dữ liệu gốc (và ngược lại); `"capsules": null` tắt máy viên nang. Loại: `fight` (giao tranh), `elite` (tinh anh), `rest` (nghỉ chân, tối đa 1 mỗi bậc), `event` (sự kiện, bốc từ `events`, các sự kiện cùng bậc khác nhau). Tối đa 10 bậc.
- `rest` (mốc 3): không có trận; chọn hồi `restHeal` (0–1) HP tối đa, hoặc lấy 1 trong tối đa 3 thẻ (cùng luật đưa thẻ như trong trận, không có thẻ hồi máu ngay).
- `events` (mốc 3, tối đa 12): `kind` là `swap` (bỏ 1 thẻ của build lấy 1 trong 3 thẻ mới, hoặc đi tiếp), `chest` (an toàn: hồi `heal` HP tối đa; liều: mất `cost` HP tối đa, nhận 1 di vật ngẫu nhiên chưa có), `shrine` (mất `cost` HP tối đa để chọn 1 trong 2 di vật hiện rõ, hoặc đi tiếp). `heal` 0–1, `cost` 0–0,9; cái giá phải trả được mà mèo còn ≥ 1 HP, không thì nút bị khóa. `chest`/`shrine` cần có `relics`.
- `relics` (mốc 3, tối đa 12): chỉ có trong chuyến đi, BattleSim áp như một modifier riêng (không sửa thẻ, kỹ năng hay luật chung; Phiêu lưu và trận tự do không có di vật). `effect`:
  - `counter`: Khiên bong bóng hoặc Mai rùa chặn một đòn → nhát chém trúng kế tiếp +`amount`; Khiên hồi lâu hơn `penalty` giây (0–30).
  - `relief`: Cá hồi máu hồi thêm `amount`; hồi chiêu Cá dài thêm `penalty` (tỉ lệ).
  - `reckless`: HP dưới `below` HP tối đa thì +`amount` sát thương; luôn nhận thêm `penalty` sát thương.
  - `light`: +`amount` tốc chạy, −`extra` thời gian hồi đòn (0–0,5); −`penalty` HP tối đa (0–50).
  - `skill` (1.5, tùy chọn): id một kỹ năng; di vật chỉ được đưa cho chuyến mang kỹ năng đó.
- `keepsakes` (mốc 4, tối đa 12): mỗi món cho một lần, khi thắng cả chuyến ở độ khó `difficulty` trở lên (`exact: true`: chỉ đúng độ khó đó). `kind`: `cup` (cúp trên góc trưng bày, `tier` là `bronze`/`silver`/`gold`), `cushion` (đệm slime trên góc trưng bày), `accessory` (`accessory` là id một phụ kiện có `reward` trong accessories.json; nhận rồi thì Cài đặt cho đội). Cho cùng lần lưu với kết quả chuyến nên không bao giờ cho hai lần; battle.json tùy chỉnh không cho kỷ vật. Góc trưng bày vẽ theo dữ liệu gốc của app.
- `maxRelics` (0–6): số di vật giữ cùng lúc tối đa; đủ thì không còn được đưa di vật. `eliteRelics` (0–3): thắng tinh anh được chọn 1 trong ngần ấy di vật (hoặc bỏ qua) trên bản đồ trước khi đi tiếp.
- Lựa chọn ở điểm nghỉ/sự kiện/thưởng tinh anh được bốc một lần từ seed của chuyến và lưu trong chuyến (`Pending`), nên đóng/mở bản đồ hay mở lại app không bốc lại; mỗi lựa chọn chỉ áp dụng một lần.
- `fights`, `elites`, `boss`: các trận (encounter) viết như một màn Phiêu lưu (`waves`, `threat`, `maxAlive`, `eliteChance`, `upgrades`, `maxUpgrades`…) nhưng chỉ là màn `clear`, không có `reward` hay `tipVi/tipEn`. `boss` phải có một đợt có boss. Mỗi bậc cần đủ trận trong kho của loại đó để hai điểm cùng bậc khác nhau.
- id trận không được trùng nhau và không trùng id màn Phiêu lưu. Chuyến đã lưu ghi id trận, nên đổi id là trận mới.
- `rulesVersion`: tăng khi đổi luật đủ nhiều để chuyến đang lưu không nên đi tiếp và kỷ lục cũ không nên so. Chuyến đang lưu không khớp (khác `id`/`rulesVersion`, bản đồ khác hình dạng, id trận không còn) sẽ kết thúc khi mở bản đồ, kèm một dòng báo.
- `unlockAfter`: màn Phiêu lưu phải thắng trước. Nếu dữ liệu không còn màn đó (ví dụ battle.json của bạn thay cả `campaign`), Viễn chinh mở luôn.
- `retreatCost` (0–1): rời trận của một bậc (Kết thúc trận, đóng app, crash, đặt lại vị trí mèo) thì bậc đó vẫn chờ đánh lại với cùng seed, nhưng mèo mất phần này của HP tối đa. HP về 0 thì chuyến kết thúc (thua). Trận đang đánh khi đóng app được tính lúc mở bản đồ lần sau. Bản gốc từ 2026-10-23 là `0` (người dùng: thoát Battle Mode thì giữ tiến trình để chơi tiếp): rời trận không mất máu, chuyến giữ nguyên, bậc đánh lại từ đầu.
- `winHeal` (0–1): thắng một bậc thì hồi phần này của HP tối đa (ngoài hồi giữa các đợt như thường).
- `stepThreat` (0–0,5): quái mỗi bậc mạnh hơn bậc trước chừng này (bậc 1 ×1, bậc 2 ×1,1…), nhân với `threat` của trận.
- Bản đồ được bốc một lần từ seed của chuyến khi bắt đầu và được lưu lại; mỗi điểm có seed trận riêng suy ra từ seed chuyến, nên đóng/mở bản đồ hay tiếp tục sau khi mở lại app không đổi bản đồ hay trận.
- Lưu ở `expedition.json` (phiên bản 2 từ mốc 3: thêm di vật và lựa chọn đang mở; phiên bản 3 từ mốc 4: thêm `Keepsakes`; phiên bản 4 từ 2026-10-22: `Kinds` của chuyến, `Free`/`Gold` của máy viên nang; bản app cũ chỉ đọc, không ghi đè) (cạnh preferences.json, tách khỏi `adventure-progress.json`): chuyến đang đi, kỷ lục theo `expedition|r<rulesVersion>|vũ khí|độ khó` (số chuyến, số chuyến thắng, đi xa nhất, nhanh nhất theo thời gian đánh), các chuyến đã tính. Lưu an toàn như Phiêu lưu (.tmp, .bak, file của bản mới hơn chỉ đọc). Chơi với battle.json tùy chỉnh thì chuyến đi chỉ giữ trong bộ nhớ, không ghi file.
- Cách gộp: như `campaign`, khóa được gộp từng khóa; các mảng (`steps`, `fights`, `elites`, `events`, `relics`, `keepsakes`) trong file của bạn thay toàn bộ mảng gốc.
- Log khi bắt đầu trận: `Battle started: Expedition step N <id trận>, threat ×…, <độ khó>, <vũ khí>, seed …`.

## Vô tận (`endless`) (2026-10-22)

```jsonc
"endless": {
  "id": "endless", "rulesVersion": 6, "unlockAfter": "c1_slime_king", "upgradeEvery": 2, "bossEvery": 10, "boss": "king_c1",
  "bosses": [ "king_c1", "zip", "mutant_king" ], "lateBoss": "king", "lateBossFrom": 40, "bossBonusPick": true, "allSynergiesAt": 48,
  "eliteChancePerWave": 0.007, "eliteChanceMax": 0.45, "moreElitesEvery": 20, "moreElitesMax": 2,
  "firstCount": 3, "countPerWave": 0.35, "maxCount": 14, "threatPerWave": 0.06, "maxAlive": 5, "eliteChance": 0.12,
  "kinds": [ { "enemy": "slime", "weight": 50 }, { "enemy": "runner", "weight": 25, "from": 3 }, { "enemy": "mender", "weight": 15, "from": 5 }, { "enemy": "rock", "weight": 15, "from": 7 } ]
}
```

- Một trận không bao giờ thắng. Đợt n có `firstCount` + `countPerWave` × (n − 1) quái (làm tròn xuống, 1 đến `maxCount`), bốc theo `kinds` (mỗi loại có từ đợt `from`, theo `weight`; phải có ít nhất một loại từ đợt 1; không được là trùm), threat 1 + `threatPerWave` × (n − 1).
- Mỗi đợt thứ `bossEvery` (0 = không có) chỉ có `boss` (phải là quái trùm; từ M24 có `bosses` thì theo vòng, xem mục Chặng, vòng trùm và Đột biến); từ đợt `lateBossFrom` (1–500, gốc 30) có thêm `lateBoss` (trùm, tùy chọn) cùng đợt. Sau mỗi `upgradeEvery` đợt dọn xong (0 = không có thẻ) người chơi chọn 1 thẻ, giữ tới hết lượt; `bossBonusPick` true: dọn xong đợt trùm thì chọn liền 2 thẻ.
- (2026-10-24) Tinh anh tăng dần: tỉ lệ = `eliteChance` + `eliteChancePerWave` (0–0.2) × (n − 1), tối đa `eliteChanceMax` (0–1, 0 = không trần); cứ `moreElitesEvery` đợt (0–200, 0 = không) được thêm một tinh anh sống cùng lúc so với `maxElites`, thêm tối đa `moreElitesMax` (0–4). `allSynergiesAt` (1–500, tùy chọn): từ đợt này mọi synergy (cả tầng chỉ Vô tận) tự bật, kể cả build lệch nhóm.
- Bản gốc từ 2026-10-24 (rulesVersion 4): `upgradeEvery` 2, `boss` `king_c1` (Vua chương 1, có khiên và phong tinh anh), `lateBoss` `king` từ đợt 30, `bossBonusPick` true, `allSynergiesAt` 48, tinh anh 12% + 0,7%/đợt tối đa 45%, thêm một tinh anh sống mỗi 20 đợt (tối đa 3 con). Tới đợt 48 có 24 lượt thẻ thường + 4 lượt thưởng sau trùm = 28 lượt, đủ 27 điểm cho ba nhóm × 9 nếu chia đều; không đều thì lưới an toàn ở đợt 48 bật nốt.
- Hết lượt khi mèo thua hoặc người chơi bấm Kết thúc (cả hai đều có bảng kết quả và tính kỷ lục). Đóng app giữa chừng thì mất lượt, không lưu gì.
- Kỷ lục: đợt xa nhất và số slime hạ nhiều nhất, theo vũ khí + độ khó, trong battle-records.json (khóa `endless|r<rulesVersion>|<vũ khí>|<độ khó>`, thêm `|custom` khi dùng battle.json riêng). Đổi luật nhiều thì tăng `rulesVersion` để kỷ lục cũ đứng riêng.
- `unlockAfter`: màn Phiêu lưu phải thắng trước (không có màn đó trong dữ liệu thì mở luôn). `maxAlive`, `eliteChance`: như của màn.
- `milestones` (2026-10-22): phần thưởng khi **vượt qua** đợt `wave` (bất kỳ vũ khí/độ khó; dữ liệu tùy chỉnh không tính), nhận một lần và giữ mãi. Mỗi mục: `id` (dùng chung không gian id với kỷ vật Viễn chinh, không được trùng), `wave` (tăng dần, tối đa 500), `kind` = `accessory` (kèm `accessory`: phụ kiện có `reward` trong accessories.json) hoặc kỷ vật trưng bày `hourglass`, `orb`, `cushion`; `nameVi`/`nameEn` 1–32 ký tự; tối đa 12 mục. Gốc: đợt 10 Vòng cổ ∞, 20 Đồng hồ cát vô tận, 30 Vành sao, 40 Quả cầu tinh vân, 50 Áo choàng thiên hà. Đã nhận hay chưa tính từ kỷ lục (`MostCleared`, kỷ lục cũ dùng đợt xa nhất − 1) của mọi `rulesVersion`.

## Chặng, vòng trùm và Đột biến của Vô tận (`bosses`, `eras`, `mutations`) (bản 1.5, mốc M24, 2026-10-29)

E1, E2 của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetEndless.cs` (dữ liệu, kiểm tra lúc nạp), `src/DesktopPet.Core/PetBattle.Mutations.cs` (luật trong trận); bảng chọn dùng chung bảng thẻ (`PetWindow.BattleUpgrade.cs`), dòng HUD ở `PetBattleHud.cs`, banner ở `PetWindow.BattleFeedback.cs`. Test `tests/DesktopPet.Tests/EndlessMutationTests.cs`, UiChecks cuối `EndlessChecks` (ảnh `endless-mutation-vi/en/ja.png`).

```jsonc
"endless": {
  "rulesVersion": 6, "boss": "king_c1", "bosses": [ "king_c1", "zip", "mutant_king" ], "lateBoss": "king", "lateBossFrom": 40,
  "erasFrom": 15,
  "eras": [ { "nameVi": "Đầm nhầy", "nameEn": "Goo marsh", "kinds": [ { "enemy": "spitter", "weight": 20 }, … ] }, … ],
  "mutations": { "offer": 3, "list": [
    { "id": "glass_cat", "effect": "glass", "amount": 0.3, "reward": "damage", "rewardAmount": 0.3,
      "nameVi": "…", "nameEn": "…", "descVi": "…", "descEn": "…", "rewardVi": "…", "rewardEn": "…" }, … ] }
}
```

- **Vòng trùm** (`bosses`, 1–6 boss, tùy chọn; không có thì `boss` mọi lần): đợt trùm thứ k dùng boss thứ k theo vòng. Gốc: đợt 10 Vua chương 1, 20 Slime Nén, 30 Vua biến dị, 40 lại Vua chương 1… `lateBossFrom` gốc dời 30 → 40, nên vòng đầu mỗi trùm đánh một mình.
- **Chặng** (`eras`, tối đa 12, mỗi chặng 1–8 quái không phải boss, không có `from`, tên vi/en 1–32 ký tự): chặng đầu bắt đầu ở đợt `erasFrom` (gốc 15), các chặng sau bắt đầu ngay sau mỗi đợt trùm (dài `bossEvery` đợt), hết danh sách thì quay lại chặng đầu. Trong chặng, quái của chặng **thêm vào** bốc thăm của `kinds` (cộng trọng số nếu trùng); đợt trùm vẫn chỉ có boss. Chặng mới có banner "Chặng mới: … Slime lạ kéo tới" ở đợt đầu. Gốc (quái mới ~38–40% trọng số mỗi chặng): Đầm nhầy 15–20 (phun, lò xo, băng, tách đôi), Ban nhạc slime 21–30 (nhạc trưởng, xích, nhím, bóng ma, bay), Bãi rác 31–40 (hợp thể, bom, đào hang, bong bóng, bám tường, hẹn giờ), Mê cung ảo ảnh 41–50 (cờ hiệu, ảo ảnh, dỗi, trộm, phun, nhím), từ 51 lặp lại.
- **Giới hạn 2 loại liên kết, 2 loại quái chuột** (như mọi trận) xét lúc nạp cho: bốc thăm `kinds` riêng, từng chặng cộng `kinds`, từng boss của vòng (kèm `lateBoss`). Lúc chạy, đầu mỗi đợt Vô tận sàn trống nên trận quên các loại liên kết đã gặp (mỗi chặng có 2 loại của nó; luật "đến trước được trước" vẫn áp trong một đợt).
- **Đột biến** (`mutations`, cần `bossEvery` > 0): dọn xong mỗi đợt trùm, sau các thẻ của đợt đó, bảng thẻ hiện `offer` (1–3) luật lạ bốc ngẫu nhiên (luồng ngẫu nhiên riêng, không đổi thẻ/quái), không có luật vừa xong; **bắt buộc chọn một** (như thẻ, không có Bỏ qua). Luật giữ tới khi dọn xong đợt trùm kế tiếp, rồi trả thưởng (banner "★ Vượt đột biến …"); thua hoặc Kết thúc giữa chừng thì không có thưởng. Luật không phải thẻ của build (không tính nhóm, không vào Túi đồ thẻ). HUD ghi "Đột biến …: còn N đợt".
  - `effect` (bất lợi, `amount`): `swift` quái nhanh hơn (0,05–1), `crowd` thêm quái mỗi đợt (0–1, làm tròn lên) và `extra` con trên sàn (0–4, chỉ cho crowd), `elite` cộng tỉ lệ tinh anh (0,05–1, vẫn theo trần số tinh anh sống), `glass` mèo nhận thêm sát thương (0,05–1), `hungry` không rơi cá và không hồi máu giữa đợt (không có amount), `lazy` kỹ năng hồi chậm hơn (0,05–2).
  - `reward` (`rewardAmount`): `drops` đồ rơi nhiều hơn trong lúc giữ luật (0,1–3), `damage` mèo gây thêm sát thương trong lúc giữ (0,05–1), `maxHp` HP tối đa đến hết lượt khi xong (5–60, hồi luôn phần đó), `rare` một bảng 3 thẻ hiếm khi xong (hết thẻ hiếm thì bảng thường), `heal` hồi đầy khi xong, `none`. `cards` 0–3: thêm lượt thẻ khi xong. Thứ tự sau đợt trùm: thẻ của đợt (và thẻ thưởng trùm) → thẻ hiếm → thẻ thêm → đột biến mới.
  - Kiểm tra lúc nạp: `id` chữ thường 1–32, không trùng nhau hay trùng id thẻ; tên 1–32, mô tả/thưởng 1–80 ký tự; danh sách có ít nhất `offer` + 1 luật, tối đa 12; `none` phải có `cards`.
  - Gốc: Slime tăng tốc (+25% → đồ rơi ×2 trong lúc đó), Đông nghẹt (+30% quái, +2 trên sàn → +1 lượt thẻ), Toàn tinh anh (+25% → chọn 1 thẻ hiếm), Mèo thủy tinh (nhận +30% → gây +30% trong lúc đó), Nhịn đói (→ +20 HP tối đa), Kỹ năng lười (hồi chậm 40% → hồi đầy + 1 lượt thẻ).
- Sự kiện mới: `EraStarted` (Value: chỉ số chặng), `MutationStarted`, `MutationEnded` (Value: chỉ số luật). Bảng đột biến dùng `UpgradeOffered`/`UpgradeChosen` như thẻ.
- rulesVersion Vô tận 5 → 6: kỷ lục cũ đứng riêng (khóa `endless|r5|…`); mốc phần thưởng vẫn tính kỷ lục của mọi phiên bản.
- Mô phỏng (không phải chơi thật; bot "simple" của AdventureBalanceTests, Normal, kiếm, 8 lượt, tối đa 20 phút): luật cũ đợt 30 30 30 30 30 33 38 38 (trung vị 30), luật mới 20 30 30 31 31 32 34 35 (trung vị 31). Số liệu chặng và đột biến là ước lượng, chốt khi chơi thật/M29.

## Nhóm Mưu mẹo (`trick`) (bản 1.5, mốc M25, 2026-10-30)

J1 của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetBattle.Trickery.cs` (hiệu ứng thẻ, cú đáp, synergy), mời thẻ ở `PetBattle.Upgrades.cs`, nhóm ở `BuildGroups` (`PetBattleData.cs`); màu nhóm #EE9AD6 (thanh, viền icon thẻ, huy hiệu túi đồ). Test `tests/DesktopPet.Tests/BattleTrickeryTests.cs`, UiChecks (`UpgradeChecks`: thẻ Mưu mẹo trong bảng chọn; `EnemyVisualChecks`: icon 9 thẻ mới; túi đồ Vô tận 12 huy hiệu).

- **Nhóm thứ 4** `trick` "Mưu mẹo / Trickery" (kỹ năng, bom, app, đồ rơi). `BuildGroups.All` có 4 nhóm (HUD, túi đồ, bảng chuẩn bị), `BuildGroups.Offered` chỉ 3 nhóm cũ: mỗi lượt mời vẫn 3 thẻ, mỗi nhóm cũ một ô; thẻ Mưu mẹo vào **ô tiện ích** (`utilityChance` 0.35) và lấp chỗ khi nhóm khác hết thẻ. Trong ô tiện ích, thẻ hiếm Mưu mẹo thay thẻ thường với `rareChance` (cùng luồng `Utility`). Mỗi nhóm tối đa 2 thẻ hiếm như cũ.
- **Thẻ của kỹ năng** (`skill`: chỉ mời khi trận mang kỹ năng đó) và **thẻ đồ rơi** (chỉ mời ở trận có đồ rơi: Viễn chinh, Vô tận). Thẻ hiếm và Đột biến "Toàn tinh anh" theo cùng điều kiện.

| id | effect | amount (khoảng) | Gốc |
| --- | --- | --- | --- |
| `quick_hands`, `power_bank`, `energy_drain`, `big_bomb`, `thick_bubble` | như cũ | như cũ | 5 thẻ tiện ích cũ, nay `group: trick` |
| `long_laser` (laser) | `laserTime` | 0.2–5 s | Laser bền: chấm sáng thêm 1,5 s (tối đa 2) |
| `turbo_fan` (fan) | `fanPower` | 0.05–1 | Quạt turbo: tầm và lực gió +25% |
| `firm_grip` (grabber) | `grabberThrow` | 0.05–2 | Kẹp chắc: cú ném +50% sát thương và thời gian choáng |
| `sharp_scissors` (scissors) | `scissorsDamage` | 0.05–2 | Kéo sắc: +40% sát thương |
| `big_tank` (spray) | `sprayTime` | 0.2–5 s | Bình to: thêm 1,5 s nước |
| `keen_nose` | `dropChance` | 0.05–2 | Mũi thính: tỉ lệ rơi đồ ×1,3 (nhân cùng Nam châm xu, đột biến) |
| `fat_fish` | `fishHeal` | 1–100 HP | Cá béo: cá rơi ra hồi thêm 10 HP |
| `ground_quake` (hiếm) | `landingQuake` | 1–100 | Cú đáp địa chấn: 12 sát thương |
| `sleight` (hiếm) | `skillRefund` | 0.05–1 | Ảo thuật: 25% lần dùng kỹ năng chỉ hồi một nửa |

- **Cú đáp địa chấn** (ý của người dùng, ngoại lệ Battle Mode (3) trong docs/APPROVED-EXCEPTIONS.md): mèo được thả hoặc ném rồi đáp sau khi nhấc ≥ `rescueGraceMinLiftSeconds` → sóng bán kính `BattleSim.QuakeRadius` 110 DIPs quanh chỗ đáp: `amount` sát thương như đòn công cụ (boss một nửa, Kết liễu cộng) và đẩy nhẹ (160 × `pushFactor`; boss và tinh anh miễn Đẩy không bị đẩy). Không trúng quái trên không, bám tường, đang đào hang, trong kẹp gắp hay đang nhảy. Hồi `QuakeCooldown` 6 s (đếm theo thời gian trận). Hình dùng hiệu ứng Slam có sẵn, cue `pop`; sự kiện `Quaked` (Value: số quái trúng).
- **Ảo thuật**: mỗi lần kỹ năng bắt đầu hồi (mọi loại, cả quạt/bình xịt khi hết hơi) bốc trên luồng random riêng `BattleRandom.Tricks`; trúng thì thời gian hồi còn một nửa, sự kiện `SkillRefunded`, cue `sparkle`. Kẻ trộm lấy kỹ năng không tính.
- **Cộng hưởng** (3 ở mọi chế độ; 6 và 9 chỉ Vô tận, `endlessOnly`):

| effect | amount | Ý nghĩa (gốc) |
| --- | --- | --- |
| `skillRush` | 0.05–1 | Phối hợp (3): dùng kỹ năng (lúc nó tác dụng: bấm, nhắm trúng, bắt đầu giữ quạt/bình, nhấc slime, cắt) → 3 s đòn của mèo (cả đòn khi đang cầm) +amount sát thương (0.2) |
| `killRecharge` | 0.1–5 s | Nạp nhanh (6): mỗi quái bị mèo hạ, mọi kỹ năng mang theo đang hồi bớt amount giây (0.5) |
| `skillFeast` | 10–300 s | Đại tiệc (9): cứ amount giây đánh nhau (không khi mèo bị cầm), kỹ năng mang theo còn hồi lâu nhất sẵn sàng ngay; không kỹ năng nào đang hồi thì đồng hồ chờ đầy (40) |

- Build cũ đã lưu có thẻ tiện ích giờ tính điểm Mưu mẹo (3 thẻ bật ngay Phối hợp).
- rulesVersion: Vô tận 6 → 7; Viễn chinh tuyến 1 7 → 8 (`keepRuns: [6, 7]`, khóa mới `shopFrom: 7` = phiên bản đầu có cửa hàng, chuyến r7 đang đi vẫn có cửa hàng, r6 thì không), tuyến 2 1 → 2 (`keepRuns: [1]`). Chương Phiêu lưu không đổi.
- Bot mô phỏng (AdventureBalanceTests) xếp thẻ Mưu mẹo như thẻ tiện ích cũ (cuối cùng) nên bảng đo như trước; check "không màn nào quá 4 phút" từ M25 không tính bot đứng yên (màn 5, búa, Dễ: trước 1/20 trận thắng trong 277 s, sau 2/20 trong 252 s). Mô phỏng sau M25: Vô tận trung vị đợt 31 (như trước); Viễn chinh Normal bot simple thắng 39/35/38/32 trên 40 (trước 39/34/39/32). Số liệu thẻ là ước lượng, chốt khi chơi thật/M29.

## App mới: Chụp màn hình, Captcha, Pin yếu, Con trỏ đơ (`newAppsFrom`) (bản 1.5, mốc M26, 2026-10-30)

A1–A4 của docs/UPDATE-1.5.md, theo ngoại lệ "App trong trận" (docs/APPROVED-EXCEPTIONS.md). Code: `src/DesktopPet.Core/PetBattle.Malware2.cs` (logic), `PetBattle.Apps.cs` (icon thứ 6, rút thăm), cửa sổ Captcha `src/DesktopPet/PetWindow.BattleCaptcha.cs`, lớp phủ HUD trong `PetBattleHud.cs`, hình trên dải trận `PetBattleView.Snap.cs`. Test `tests/DesktopPet.Tests/NewAppTests.cs`, UiChecks `NewAppVisualChecks.cs` và khối "1.5 M26" trong `BattleChecks.cs`.

- **Chỉ ở trận có `"newAppsFrom"`** (khóa của màn/trận; 0–1000, 0 = không bao giờ; Vô tận: `endless.newAppsFrom`, 0–100). Gốc: 5 màn chương 2 và 14 trận Lối Bãi rác = 1, Vô tận = 15. Chương 1, tuyến Viễn chinh đầu và trận tự do không có: ở đó trọng số app độc mới coi là 0 nên rút thăm y như trước (bảng mô phỏng chương 1 và tuyến 1 giống hệt từng dòng bản M25).
- **Chụp Màn Hình (icon thứ 6, `screenshot` trong `apps.list`):** sạc 75 s, bắt đầu 30%. Bấm khi đầy (và có slime) thì tia chớp bay xuống; tới nơi mọi quái (cả trùm) **đứng hình `shotFreeze` s**: không đi, không đánh, đòn đang gồng dừng giữa chừng, đạn/goo đang bay dừng, đợt chưa sinh quái mới, slime không ném app độc/virus. Mèo vẫn đánh, bom/kỹ năng vẫn trúng, độc vẫn ngấm, quái vẫn bị hạ. Dải trận chớp trắng 0,35 s, quái đứng hình giữ nguyên dáng lúc chớp, có 4 góc khung ngắm. Nhiễm virus: cứ `dazzleEvery` s chớp vào mắt mèo, mèo **lóa `dazzleSeconds` s** (vuốt trượt, hiện MISS; mèo đang bế, đang được bảo vệ sau khi thả thì không trúng; Khiên chặn).
- **Captcha (`captchaWeight`):** cửa sổ giả "Xác minh bạn không phải robot" rộng `captchaShare` bề ngang sàn (190–520 DIPs), cao 150 DIPs, đứng trên sàn ở chỗ mèo lúc nó bật ra (không đi theo). Là cửa sổ topmost riêng, được đưa lên trên cửa sổ mèo (trừ lúc đang cầm/ném mèo: mèo đang cầm ở phía trước, thả xuống rồi Captcha lại đè lên): chỗ nó che thì không bấm được mèo/slime; mèo ra khỏi chỗ che thì nhấc bình thường; slime sau nó vẫn đánh. Lưới 3×3 hình tự vẽ (mèo, slime, cá, cuộn len, file, chuông), `captchaCatsMin`–`captchaCatsMax` ô có mèo. Bấm ô mèo: ô xanh có dấu tích; đủ hết thì đóng. Bấm ô khác: xáo lại (mất các ô đã chọn). Tự đóng sau `captchaSeconds` s (đồng hồ dừng khi đang cầm mèo). **Không bật ra khi đang có slime bay** (lịch màn chờ). Pause/menu/fullscreen ẩn nó.
- **Pin yếu (`batteryWeight`):** dải đỏ "Pin yếu" trên dải ô kỹ năng HUD; kỹ năng hồi chiêu bằng `batteryRate` tốc độ thường tới khi bấm **Cắm sạc**.
- **Con trỏ đơ (`cursorWeight`):** lớp phủ đồng hồ cát "Không phản hồi… N" che 5 ô kỹ năng: không dùng, không chọn được kỹ năng (kỹ năng đang chọn bị cất, quạt/bình xịt dừng) trong `cursorSeconds` s hoặc tới khi bấm **Tiếp tục**. App và thanh nộ vẫn dùng được.
- Như app độc khác: tính trong `maxMalware` (chương 2: 4 loại cùng lúc), chờ khi đang cầm mèo, dọn xong đợt thì hết; bấm được cả khi đang cầm mèo. Lịch `malwarePlan` có thêm `captcha`, `battery`, `cursor`. Danh sách `apps.list` của file tự sửa cũ thiếu `screenshot` vẫn hợp lệ (dùng tên gốc).
- rulesVersion: màn chương 2 → 2, Lối Bãi rác → 3 (`keepRuns` [1, 2]: chuyến cũ đi tiếp), Vô tận → 8 (kỷ lục cũ đứng riêng).

| Khóa (`apps`) | Khoảng | Gốc |
| --- | --- | --- |
| `shotFreeze`, `dazzleEvery`, `dazzleSeconds` | 0,2–10 s, 1–120 s, 0–5 s | 2, 11, 1.5 |
| `captchaWeight`, `captchaSeconds`, `captchaShare`, `captchaCatsMin`, `captchaCatsMax` | 0–100, 3–60 s, 0,15–0,6, 1–8, ≥ min và ≤ 8 | 1, 15, 0.33, 2, 4 |
| `batteryWeight`, `batteryRate` | 0–100, 0,1–1 | 1, 0.4 |
| `cursorWeight`, `cursorSeconds` | 0–100, 1–30 s | 1, 5 |

- Hướng dẫn phiêu lưu: 5 bước `screenshot`, `dazzle`, `captcha`, `battery`, `cursor` ở mọi màn có `newAppsFrom` (mục "Chương 2").
- Mô phỏng (2026-10-30, so với M25; bot không bấm Captcha/Cắm sạc/Tiếp tục): chương 2 và Lối Bãi rác khó hơn chút (vd. Lối Bãi rác kiếm Thường simple safe 33 → 31/40 chuyến thắng), Vô tận trung vị giữ 31 đợt.

## Sổ tay quái (`tipVi`/`tipEn`/`counters`/`page` của quái) (bản 1.5, mốc M27, 2026-10-30)

L1 của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetMonsterBook.cs` (sổ, file), `PetBattle.Book.cs` (trận ghi gì), trang trong Sảnh `src/DesktopPet/PetWindow.MonsterBook.cs`. Test `tests/DesktopPet.Tests/MonsterBookTests.cs`, UiChecks khối "1.5 M27" trong `BagChecks.cs`. Python không tham gia; không cần ngoại lệ mới (chỉ là trang xem trong Sảnh).

- **Trang:** mỗi loại quái có `tipVi`/`tipEn` (cần cả hai, 1–200 ký tự) có một trang; `counters` (tối đa 3 id kỹ năng khác nhau) là "Kỹ năng hợp". `page`: dùng chung trang của loại khác (có tip, không có `page`); khi đó loại này không có tip riêng. Gốc: 24 slime + 4 trùm (Vua Slime, Slime Nén, Slime Mẹ, Vua biến dị); `king_c1` (Vua chương 1) dùng trang `king`; Slime nửa và Mảnh file không có trang (chỉ ra từ quái khác). 6 đội hình (`formations`) mỗi cái một trang, dùng `tipVi`/`tipEn` có sẵn của đội hình.
- **Gặp:** trận ghi loại quái khi có một con đứng trên sàn (sinh xong, không tính bản giả của slime ảo ảnh), số con mèo hạ theo loại (như bộ đếm hạ của trận: tự nổ, trộm trốn, quái tha cá thoát không tính), đội hình khi nó tới (cả đội hình của boss). Lúc hết trận (thắng, thua, hoặc rời trận giữa chừng) cửa sổ cộng vào sổ một lần; mọi chế độ (Phiêu lưu, Viễn chinh, Vô tận, trận tự do). Trận chạy bằng battle.json tự sửa không ghi gì. Số hạ cộng vào trang (Vua chương 1 cộng vào trang Vua Slime).
- **File:** `%LOCALAPPDATA%\DesktopPet\monster-book.json` (version 1): `met`, `formations`, `defeated`, `seen` (trang Sảnh đã hiện), `seeded`. Ghi kiểu .tmp + .bak như adventure-progress; file hỏng thì đọc .bak hoặc bắt đầu lại; file của bản mới hơn chỉ đọc, không ghi đè. Chỉ ghi khi có gì thay đổi.
- **Dữ liệu cũ:** lần đầu mở sổ (chưa có file), các màn Phiêu lưu đã thắng tính là đã gặp quái của màn đó (quái ghi trực tiếp trong đợt và thành viên đội hình; số hạ 0), coi như đã xem (không hiện "Mới").
- **Sảnh:** nút "Sổ tay quái" (kèm "· N mới") cạnh "Phần thưởng"; bấm thì sổ thế chỗ các thẻ chế độ, nút thành "‹ Sảnh" và nút Phần thưởng ẩn (một trang mỗi lúc). Trên cùng là trang đang chọn: hình thường và tinh anh (vẽ bằng chính hàm vẽ của dải trận, chụp một lần rồi giữ), tên, "Trùm", cách khắc chế, "Tinh anh: …" (mô tả affix), "Kỹ năng hợp: …", "Đã hạ: N"; trang đội hình: hình các thành viên, gợi ý, "Thành viên: …". Dưới là ba nhóm ô Slime / Trùm / Đội hình phối hợp có "đã mở/tổng", cuộn khi dài. Ô chưa gặp là bóng đen có "?" và tên "???", không bấm được; ô đã gặp có tên, "× N" (số đã hạ), nhãn "Mới" nếu chưa xem. Mở sổ là đánh dấu đã xem các trang mới.
- **Kết quả trận:** trận mở trang mới thì có dòng vàng "Sổ tay quái, trang mới: …".
- Luật trận, rulesVersion, kỷ lục và bảng mô phỏng không đổi (chỉ thêm ghi nhận).

## Thành tựu, lông và phụ kiện thưởng (`achievements.json`) (bản 1.5, mốc M28, 2026-10-30)

L2, L3 của docs/UPDATE-1.5.md. Dữ liệu `src/DesktopPet/Assets/achievements.json` (nhúng vào app như coats.json, không nằm trong battle.json nên battle.json tự sửa không đổi được). Code: `src/DesktopPet.Core/PetAchievements.cs` (đọc file, số liệu từ trận `AchievementStats`, sổ `AchievementBook`), cửa sổ `src/DesktopPet/PetWindow.Achievements.cs`. Test `tests/DesktopPet.Tests/AchievementTests.cs`, UiChecks khối "1.5 M28" trong `BagChecks.cs`. Python không tham gia; không cần ngoại lệ mới.

- **Một thành tựu:** `id`, `name`/`desc` (`vi`, `en`; ja tra theo chữ tiếng Anh), `stat`, `goal` (1–1.000.000), `scope` (`total`: cộng dồn mọi trận; `battle`: đạt trong một trận), `reward` tùy chọn. File sai (stat lạ, goal 0, scope lạ, id trùng, thiếu mô tả, reward không hợp lệ) thì bỏ cả file, app vẫn chạy (không có thành tựu).
- **Số liệu trận** (đọc lúc trận hết hoặc bị rời giữa chừng): `wins` (thắng), `defeated` (số hạ), `bosses` (hạ trùm), `combos`, `flawless` (thắng mà `HurtTaken` = 0: chưa mất máu lần nào, khiên/mai rùa chặn thì không tính là mất), `clutch` (thắng khi máu ≤ 15%), `hardWin` (thắng ở Khó), `quickWin` (thắng trong ≤ 120 s đánh), `heldHits`, `throwHits`, `ropesCut` (cắt dây bằng mèo + kéo + dây trùm), `rages`, `bubbles`, `ghosts`, `iceBreaks`, `fullBasket` (thắng màn giỏ cá còn đủ cá), `coins`, `endlessWaves` (đợt vượt trong một lượt Vô tận), `popups`, `quakeHits`. Bộ đếm mới duy nhất trong trận là `BattleSim.HurtTaken` (cộng ở LandHit), không đổi luật trận.
- **20 thành tựu gốc**, 6 cái có thưởng: Thợ săn slime (500 slime) → phụ kiện Khăn buộc trán; Huyền thoại slime (3000) → lông Vàng kim; Mèo chiến binh (50 trận thắng) → lông Nửa đêm; Khắc tinh của trùm (hạ trùm 10 trận) → Huy chương anh hùng; Không trầy xước → lông Bạc hà; Giỏ cá đầy ắp → Vòng cổ cá nhỏ. 14 cái còn lại chỉ có huy hiệu.
- **Lông/phụ kiện thưởng:** coats.json và accessories.json có `"reward": "achievement"`; mỗi món chỉ một thành tựu trao (kiểm khi đọc file). Cài đặt → Tùy chỉnh chỉ hiện món đã nhận (hoặc đang mặc sẵn). Bộ lông đầu tiên (Cam, mặc định) không được là phần thưởng. Mười bộ lông cũ và phụ kiện cũ không đổi.
- **Ghi nhận:** mọi chế độ (Phiêu lưu, Viễn chinh, Vô tận, trận tự do), mỗi trận một lần; trận rời giữa chừng vẫn cộng số liệu (không có thắng). Trận chạy bằng battle.json tự sửa không ghi gì. Không tính ngược trận cũ (bắt đầu từ 0 ở bản này).
- **File:** `%LOCALAPPDATA%\DesktopPet\achievements.json` (version 1): `Totals`, `Best` (trận tốt nhất từng stat), `Earned`. Ghi .tmp + .bak như monster-book; file hỏng đọc .bak hoặc bắt đầu lại; số âm/không hữu hạn bị bỏ; file của bản mới hơn chỉ đọc, không ghi đè. Chỉ ghi khi có gì thay đổi.
- **Hiển thị:** Sảnh → Phần thưởng có mục đầu "Thành tựu · đạt/tổng", ô hai cột: hình phần thưởng trên mèo (lông hoặc phụ kiện) hoặc cúp, tên, mô tả, tiến độ "x/goal" hoặc "Đã đạt", phần thưởng; mới đạt có nhãn "Mới" và được đếm vào nút "Phần thưởng · N mới" (đánh dấu đã xem trong `RewardsSeen` của adventure-progress dưới dạng `a:<id>`). Kết quả trận có dòng vàng "★ Thành tựu: … · mở khóa …".
- Luật trận, rulesVersion, kỷ lục và bảng mô phỏng không đổi.

## Trận tự chọn (bản 1.5, mốc M29a, 2026-10-30)

F1 của docs/UPDATE-1.5.md. Code: `src/DesktopPet.Core/PetCustomFight.cs` (preset `CustomFight`, dựng trận `CustomFights.Build`, khóa và giới hạn, file `CustomFightBook`), app theo bộ trong `PetBattle.Apps.cs` (`StageSpec.CustomApps`), app đã gặp trong `PetBattle.Book.cs`/`PetMonsterBook.cs`; cửa sổ `src/DesktopPet/PetWindow.CustomFight.cs`. Test `tests/DesktopPet.Tests/CustomFightTests.cs`, UiChecks `CustomFightChecks.cs`. Không có khóa mới trong battle.json; Python không tham gia; không cần ngoại lệ mới.

- **Vào:** Sảnh chiến đấu → nút "Trận tự chọn" (cạnh Phần thưởng, Sổ tay quái) mở bảng chuẩn bị của trận tự do. Bảng có thêm mục "Trận tự chọn": chip bộ (Mặc định, rồi các bộ của người dùng), một dòng tóm tắt, nút "Dựng trận…". Vũ khí, độ khó, 5 kỹ năng chọn ở bảng chuẩn bị như trước (trận tự chọn mang được cả 10 kỹ năng như trận tự do). "Đóng" vẫn về desktop như trước.
- **Bộ Mặc định** là đúng trận tự do cũ (không có stage: 4 đợt `waves` gốc, không app, không thẻ, kỷ lục và thành tựu như trước). Không sửa được; "+ Bộ mới" chép nó thành bộ của người dùng (slime 2, slime nhanh 2, slime đá 1, Vua Slime, chỉ giữ những gì Sổ tay đã gặp).
- **Bảng dựng trận** (thay chỗ bảng chuẩn bị tới khi bấm "Xong"; mỗi thay đổi lưu ngay): ô quái (hình như Sổ tay; bấm: Tắt → Ít → Vừa → Nhiều = trọng số 0/1/2/3), trùm cuối (không có, hoặc một trong Vua Slime, Slime Nén, Slime Mẹ, Vua biến dị; trang Vua đánh bằng Vua chương 1 `king_c1`), số đợt 1–10, quái đợt đầu 2–12 (+1 mỗi đợt), tối đa trên sàn 2–8, tinh anh 0/10/25/50% (Dễ không có tinh anh như mọi chế độ), sức mạnh quái (threat) ×0,6–×2,0 bước 0,2, thẻ giữa đợt bật/tắt, 6 icon app bật/tắt, 8 app độc (quảng cáo, vòng loading, cập nhật bắt buộc, virus.exe, virus vào icon, Captcha, Pin yếu, Con trỏ đơ) mỗi loại Tắt/Ít/Vừa/Nhiều. Tối đa 6 bộ, tên tự đặt "Bộ 1…6" (chưa đổi tên được: bảng không nhận bàn phím).
- **Khóa theo Sổ tay quái:** quái/trùm chưa gặp là bóng đen "???", bấm chỉ báo "chưa gặp". App: Sổ tay ghi thêm icon và app độc đã gặp (`Apps` trong monster-book.json, version vẫn 1). Lần đầu suy ra từ các màn Phiêu lưu đã thắng (`AppsSeeded`): màn có app → 5 icon cũ; app độc trong `malwarePlan` của màn; màn không có plan mà threat cao nhất ≥ `malwareMinThreat` → 4 app độc ngẫu nhiên cũ, ≥ `infectMinThreat` → virus vào icon; màn có `newAppsFrom` → Chụp Màn Hình + 3 app độc mới. Sau đó trận nào cũng ghi icon có trên màn hình và app độc đã tung ra (không với battle.json tự sửa).
- **Giới hạn:** tối đa 2 loại liên kết và 2 loại quái dùng chuột mỗi trận, tính cả những gì trùm/quái kéo theo (`BattleData.Reach`, dùng chung với kiểm tra battle.json). Chọn thêm loại vượt giới hạn bị từ chối và ghi lý do (ví dụ Vua biến dị đã có dây xích + nhịp trống nên không thêm slime hợp thể được). Bộ sửa tay vượt giới hạn thì không dựng được trận.
- **Dựng trận** (`CustomFights.Build`): mỗi đợt một nhóm `pick` theo trọng số đã chọn, `count` = quái đợt đầu + số thứ tự đợt; trùm là một đợt riêng cuối cùng; `maxAlive`, `eliteChance`, `threat`, `upgrades` theo bộ. Bộ không có quái nào và không trùm: Sẵn sàng báo "chưa có quái hay trùm nào".
- **App trong trận tự chọn:** chỉ khi có ít nhất 1 icon. Chỉ các icon đã chọn (Chụp Màn Hình là icon thường ở đây, không cần `newAppsFrom`). App độc đã chọn ra ở mọi threat (bỏ `malwareMinThreat`), trọng số = mức chọn; khoảng chờ nhân ×1,5 (cao nhất là Ít), ×1 (Vừa), ×0,6 (Nhiều); tối đa `maxMalware` loại cùng lúc như thường. Virus vào icon: mỗi đợt 35% / 75% / 100% (bỏ `infectMinThreat`); tắt thì tinh anh cũng không bắn virus. Không có hướng dẫn phiêu lưu, không đồ rơi, không phông chương.
- **Không lưu gì ngoài Sổ tay:** bộ tự chọn không vào kỷ lục trận tự do, không tính thành tựu, không đụng tiến độ Phiêu lưu; kết quả ghi "Trận tự chọn · <tên bộ>" và "không lưu kỷ lục, không tính thành tựu". "Chơi lại" dựng lại đúng bộ đó; "Kết thúc" về desktop. Sổ tay quái vẫn ghi quái gặp/hạ.
- **File:** `%LOCALAPPDATA%\DesktopPet\custom-fights.json` (version 1): `Selected` (null = Mặc định), `Presets`. Ghi .tmp + .bak; file hỏng đọc .bak hoặc bắt đầu trống; số ngoài giới hạn được kẹp lại, id trùng/`default` bị bỏ, bộ đang chọn không còn thì về Mặc định; file của bản mới hơn chỉ đọc, không ghi đè.
- Luật các chế độ khác, rulesVersion và bảng mô phỏng không đổi (so bản M28: 358/358 dòng giống hệt).

## App trên màn hình (`apps`) (2026-10-23)

Một "desktop giả" trong trận Phiêu lưu, Viễn chinh và Vô tận (trận tự do không có): 5 icon app do game tự vẽ (không dùng tên, logo hay màu của app thật) đứng trên các ô lưới kiểu desktop phía trên dải chiến trường, ở vị trí ngẫu nhiên theo seed. Logic ở `PetBattle.Apps.cs`; hình ở `PetBattleAppArt.cs`, `PetDesktopAppViews.cs` (icon, đường bay), `PetBattleView.Apps.cs` (dải chiến trường); cửa sổ ở `PetWindow.BattleApps.cs`. Python không tham gia.

- **Lưới:** cột ô rộng ~84 DIPs trải hết bề ngang màn hình của trận, tối đa 3 hàng (cao 108 DIPs) ngay trên dải chiến trường (màn hình thấp thì ít hàng hơn); ô bị HUD, bảng chọn thẻ hoặc túi đồ (khi đang mở) che thì không dùng — icon đang ở đó tự sang ô trống. Đổi độ phân giải/taskbar/Scale của Windows giữa trận: lưới tính lại, cửa sổ icon và lớp đường bay đặt lại theo pixel mới. Mỗi icon là một cửa sổ nhỏ nổi trên cùng, chỉ phần hình nhận chuột. Con trỏ ở xa (≥ 280 DIPs) thì icon mờ còn 50% (icon đã sạc ≥ 75%, bị nhiễm ≥ 85%), lại gần (≤ 110) thì rõ hẳn. Rê chuột lên icon: khung chọn kiểu desktop + ghi chú bên cạnh (tên, tác dụng hoặc tác hại khi nhiễm, % sạc / số lần còn phải quét).
- **Sạc:** mỗi icon đầy sau `charge` giây trận (đếm khi mèo không bị cầm/bay), bắt đầu ở `start` của nó (không ghi thì `startCharge`; gốc: Tab 0.6, Bão Thông Báo 0.4, còn lại 0.2 → Tab sẵn sau ~20 s, kể cả màn ngắn). Mỗi trận (mỗi màn, mỗi bước Viễn chinh) sạc lại từ đầu; trong một trận (cả lượt Vô tận) sạc chạy tiếp qua các đợt. mỗi quái bị hạ cộng `killCharge` cho mọi icon sạch. Đang sạc: lớp tối phủ phần chưa đầy; đầy: quầng vàng, sao lấp lánh, nhún nhẹ, tên màu vàng. Bấm icon đầy thì trừ hết sạc và thứ nó gửi **bay theo đường vòng từ icon xuống** (0,45 s), chạm đích mới có tác dụng:
  - `tabs` Tab Lốc Xoáy: 3 tab bay xuống rồi xoay quanh mèo `tabSeconds` giây, mỗi `tabEvery` giây gây `tabDamage` (× sát thương build) cho quái trong `tabRadius` DIPs; không đánh khi mèo bị nhấc.
  - `ping` Bão Thông Báo: `pingTargets` slime gần mèo nhất (0–20, 0 = mọi slime; gốc 4 từ 2026-10-24) mỗi con một chuông "99+" (bay lệch nhau một chút): `pingDamage` (× build) + choáng `pingStun` giây (không choáng quái đang lao/hồi máu/nhảy, tinh anh miễn Đẩy).
  - `endtask` End Task: cửa sổ "Không phản hồi" bay tới quái nhiều máu nhất (không tính trùm đang bật khiên, quái đang được hồi) và tắt nó; trùm/tinh anh mất `endTaskShare` máu tối đa.
  - `undo` Ctrl+Z: quả cầu mũi tên bay về mèo, trả lại máu mất trong `undoSeconds` giây trước lúc bấm. Chưa mất máu thì không dùng (giữ sạc).
  - `trash` Thùng Rác: `trashCount` quái thường gần mèo nhất (không trùm, không tinh anh) bị hút ngay, bay vào thùng (nắp mở).
  - Không dùng được khi mèo đang bị cầm/bay (giống kỹ năng); Pause/menu/fullscreen đóng băng tất cả; app fullscreen thì icon ẩn cùng trận. Sát thương từ app không bị đá chặn.
- **Dọn nhà:** từ đợt 2, mỗi đợt mới một icon sạch nhảy sang ô trống khác (bay vòng 0,5 s, đáp bẹp).
- **Virus:** khi `threat` hiện tại ≥ `infectMinThreat`, mỗi đợt có `infectChance` để 3–8 giây sau khi đợt bắt đầu, một slime bắn cục nhớt (0,7 s) vào một icon sạch (tối đa `maxInfected` icon cùng lúc). Tinh anh có affix `virusEvery` (5–120 s, 0 = không; gốc: slime thường tinh anh 25 s, kể cả khi được Vua phong) bắn chắc chắn: lần đầu ~3 s sau khi thành tinh anh, rồi mỗi `virusEvery` s, tối đa `eliteVirusPerWave` lần mỗi đợt cho mọi tinh anh cộng lại (đang đủ `maxInfected` thì thử lại sau 1 s). Slime đó đeo dấu virus trên đầu. Icon bị nhiễm: phủ nhớt, virus mặt cau ngồi ở góc, rung từng cơn, ngừng sạc, cứ `crawlEvery` giây bò sang ô kế bên, và phản chủ:
  - Tab → bật `adCount` quảng cáo (mỗi `popupEvery` s khi không còn cái nào), bay từ icon xuống những chỗ ngẫu nhiên trải khắp bề ngang sàn. Trong lúc Tab còn nhiễm, quảng cáo **khóa**: ổ khóa trên nút ×, bấm × không tắt được (sự kiện `PopupLocked`, banner lần đầu) và đồng hồ thả slime đứng yên. Gỡ Tab xong thì bấm × tắt được (cả khi đang cầm mèo); để `popupSeconds` s thì mỗi cái thả một `popupSpawn` (khi sàn còn chỗ) rồi biến mất. Dọn xong đợt thì quảng cáo tự đóng.
  - Bão Thông Báo → chuông đỏ rơi lên đầu mèo (`bellDamage`, mỗi `bellEvery` s; đi qua Khiên/Mai rùa/giáp như đòn thường, mèo đang bị nhấc hoặc vừa được thả thì trượt) và làm mèo **choáng `bellStun` s** nếu đòn lọt qua (luật miễn choáng 3 s như cú lao; đang choáng thì không bế được mèo).
  - End Task → "Không phản hồi" trên đầu mèo: choáng `freezeSeconds` (Khiên chặn; vẫn theo luật miễn choáng).
  - Ctrl+Z → hồi `mendShare` máu tối đa cho slime còn ít máu nhất (mỗi `mendEvery` s) và che chắn nó `mendGuard` s (miễn mọi sát thương, vòng xanh "MIỄN"; không áp cho trùm).
  - Thùng Rác → nhả một quái xuống sàn dưới icon (mỗi `spitEvery` s, khi sàn còn chỗ, mỗi lúc chỉ một con do thùng nhả còn sống). `spitElite` true: bốc ngẫu nhiên một loại có affix tinh anh (không trùm) và cho nó thành tinh anh luôn (không tính vào `maxElites`, có banner); false: nhả `popupSpawn` thường.
  - Gỡ: **bấm icon `scrubClicks` lần** (bọt xà phòng mỗi lần, được cả khi đang cầm mèo) hoặc **hạ con slime đeo dấu virus**. Sạch rồi thì lấp lánh, sạc tiếp từ chỗ cũ.
- **App độc (2026-10-24, bản 2 cùng ngày: xuất hiện ngẫu nhiên):** khi `threat` ≥ `malwareMinThreat`, trong lúc đánh (không tính lúc nghỉ giữa đợt, lúc cầm mèo, Pause) cứ mỗi `malwareEveryMin`–`malwareEveryMax` s (lần đầu sau nửa khoảng đó) bốc theo trọng số một loại **chưa có trên sân**: vòng loading (`loadingWeight`), cập nhật bắt buộc (`updateWeight`), virus.exe (`virusExeWeight`, chỉ khi còn icon sạch) hoặc quảng cáo (`adWeight`: `adCount` cái, mở ngay trên sàn, không khóa nếu Tab sạch). Đang có `maxMalware` loại trên sân thì lượt đó bỏ qua. Trọng số 0 = không bao giờ. Khóa cũ `infectWeight` (virus của đợt bốc thăm với app độc) đã bỏ: file riêng còn ghi thì bị bỏ qua kèm một dòng log. **Vòng loading:** một slime ném vòng ngay chỗ mèo đứng (một vòng mỗi lúc), mảng xanh trên sàn rộng `loadingRadius` DIPs mỗi bên, vòng xoay ở trên cao (chỗ mèo không che) có thanh thời gian. Mèo đứng trong vòng (trên sàn) chạy và vung vũ khí bằng `loadingSlow` tốc độ thường. Hết `loadingSeconds` s, hoặc dọn xong đợt thì vòng đóng; `loadingHold` > 0: **nhấn giữ** vòng xoay đủ `loadingHold` s thì đóng (vòng vàng đầy dần quanh vòng xoay; thả chuột hoặc trượt ra ngoài thì giữ lại từ đầu; lúc chưa giữ có vòng nét đứt nhấp nháy gợi ý), 0: bấm một lần là đóng. Cầm mèo lên thì vòng đứng yên và không làm chậm.
  **Cập nhật bắt buộc:** cửa sổ "CẬP NHẬT" (76×96 DIPs) rơi xuống sàn cách mèo 86 DIPs về phía slime gần nhất còn đủ chỗ (không đè lên slime nào; không có chỗ thì thành vòng loading). Nó là **tường**: slime (trừ trùm, trùm nhảy qua) không đi, không bị đẩy hay lao xuyên qua và không vung đòn vào mèo ở bên kia; mèo không đi qua, mục tiêu ở bên kia thì mèo đánh tường, mỗi nhát trúng cộng `updateHitSeconds` s vào tiến độ. Xong sau `updateSeconds` s, hoặc bấm nút **Nhắc sau** `updateClicks` lần, hoặc dọn xong đợt. Cầm mèo lên thì tường đứng yên; thả mèo vào giữa tường thì mèo bước ra.
  **virus.exe:** một slime thả file "virus.exe" vào một ô trống của lưới icon (mỗi lúc một đợt bùng phát, chỉ khi còn icon sạch); slime đó đeo dấu virus. Cứ `virusExeEvery` s file tự nhân đôi sang ô trống kề bên (không có thì ô trống bất kỳ); đủ `virusExeCopies` bản thì nhiễm một icon sạch (như virus thường, tôn trọng `maxInfected`: đang đủ thì chờ) rồi biến mất hết. Bấm một bản để xóa bản đó (được cả khi đang cầm mèo); hạ slime đã thả thì xóa hết; dọn xong đợt cũng hết. Cầm mèo lên thì file không nhân bản.
- Hết trận: không còn gì bay, virus, quảng cáo và app độc biến mất, icon mờ dần cùng phần kết. Random của app (vị trí, dọn nhà, virus) là luồng riêng của seed trận (`BattleRandom.Apps`).
- `list`: mỗi app một mục (`tabs`, `ping`, `endtask`, `undo`, `trash`, `popup`), đủ cả 6, không trùng: `nameVi`/`nameEn` 1–32 ký tự, `descVi`/`descEn` (tác dụng) và `badVi`/`badEn` (khi nhiễm) ≤ 120 ký tự (chữ thường, không thay `{khóa}`), `charge` 1–600 giây (popup phải là 0), `start` 0–1 (tùy chọn). File riêng ghi `list` thì thay cả danh sách.

| Khóa | Giới hạn | Gốc |
| --- | --- | --- |
| `startCharge`, `killCharge` | 0–1 | 0.2, 0.02 |
| `start` (trong `list`, tùy chọn) | 0–1 | tab 0.6, chuông 0.4 |
| `charge` (trong `list`) | 1–600 s | tab 40 (50 trước 2026-10-24), chuông 55, End Task 80, Ctrl+Z 90, thùng rác 70 |
| `tabSeconds`, `tabRadius`, `tabDamage`, `tabEvery` | 0.5–30 s, 10–300, 0–1000, 0.1–5 s | 6, 70, 14, 0.5 |
| `pingDamage`, `pingStun`, `pingTargets` | 0–1000, 0–5 s, 0–20 | 14, 1.5, 4 |
| `endTaskShare`, `undoSeconds`, `trashCount` | 0–1, 0.5–30 s, 1–5 | 0.25, 4, 2 |
| `infectMinThreat`, `infectChance`, `maxInfected`, `scrubClicks`, `crawlEvery`, `eliteVirusPerWave` | 0–100, 0–1, 0–5, 1–10, 1–120 s, 0–10 | 1.3, 0.75, 3, 5, 9, 2 (trước 2026-10-24: 1.5, 0.5, 1, 3) |
| `popupEvery`, `bellEvery`, `freezeEvery`, `mendEvery`, `spitEvery` | 1–120 s | 8, 10, 12, 7, 15 |
| `popupSeconds`, `popupSpawn`, `adCount`, `bellDamage`, `bellStun`, `freezeSeconds`, `mendShare`, `mendGuard`, `spitElite` | 2–60 s, id quái, 1–5, 0–1000, 0–5 s, 0–5 s, 0–1, 0–10 s, true/false | 8, `slime`, 3, 6, 1, 1.2, 0.15, 2, true |
| `malwareMinThreat`, `malwareEveryMin`, `malwareEveryMax`, `maxMalware` | 0–100, 3–300 s, ≥ min và ≤ 300 s, 1–4 | 1.3, 15, 30, 2 |
| `loadingWeight`, `updateWeight`, `virusExeWeight`, `adWeight` | 0–100 | 1, 1, 1, 1 |
| `virusExeEvery`, `virusExeCopies` | 1–60 s, 2–8 | 6, 4 |
| `updateSeconds`, `updateHitSeconds`, `updateClicks` | 2–30 s, 0–10 s, 1–10 | 8, 1.2, 4 |
| `loadingSeconds`, `loadingRadius`, `loadingSlow`, `loadingHold` | 1–30 s, 20–200 DIPs, 0.2–1, 0–10 s | 12, 85, 0.5, 2 |

- Cân bằng (mô phỏng, bot simple bấm app ngay khi đầy — trường hợp tốt nhất): số sạc trên giữ bot dưới ngưỡng "≤ 85% máu còn lại" ở màn 2–4 Thường. Bản sạc nhanh hơn (20–34 s) cho bot 90–96%.
- rulesVersion khi thêm app: màn 1–5: 6/12/14/15/15, Vô tận 2 (kỷ lục cũ đứng riêng; phần thưởng mốc Vô tận vẫn tính mọi rulesVersion). Viễn chinh giữ 5 để không bỏ chuyến đang đi dở.
- rulesVersion sau đợt cân bằng 2026-10-24 (kỹ năng, thẻ, app độc ngẫu nhiên, Vua chương 1 ở mọi chế độ): màn 1–5: 7/14/16/17/17, Vô tận 4. Viễn chinh vẫn giữ 5 (chuyến đang đi không bị kết thúc; kỷ lục Viễn chinh cũ và mới nằm chung).
- Cân bằng sau đợt 2026-10-24 (mô phỏng, Thường): bot "simple" (đỡ mọi đòn ngay, bấm app ngay khi đầy) kết thúc màn 2 và màn 4 với ~88% HP (ngưỡng nới thành 90%), bot "casual" 61–78% ở màn 2–5.
