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
- `enemies`, `weapons`, `upgrades`, `synergies` và `skills` gộp **theo `id`**:
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
| `marks` | `none`, `speed`, `cracks` | Họa tiết: vệt gió / vết nứt |
| `pushFactor`, `stunFactor` | 0–3 | Hệ số bị đẩy lùi / thời gian choáng (1 = như slime thường) |
| `boss` | true/false | Có vương miện, thanh HP riêng, banner BOSS |
| `slam` | true/false | Đập đất trúng cả vùng, có cảnh báo đỏ trên sàn |
| `slamDamage` | 0–1000 (chỉ khi `slam`) | Sát thương của cú dậm (nhân độ khó/threat như `damage`); 0 = bằng `damage`. Bản gốc 2026-10-24: hai Vua 22 (đòn `damage` 30 giữ nguyên) |
| `summonAt`, `summonCount`, `summonType` | 0–1; 1–6; id | Dưới tỷ lệ HP này gọi quái phụ (một lần). Quái được gọi không được tự gọi thêm. |
| `enrageAt`, `enragedSpeed`, `enragedWindup` | 0–1; 0.1–400; 0.1–10 | Dưới tỷ lệ HP này thì nổi giận: nhanh hơn, gồng ngắn hơn |

JSON chỉ chọn được những khả năng code đã hỗ trợ. Khóa lạ như `"flying": true` sẽ bị từ chối. Muốn có khả năng mới (bay, bắn đạn…) phải viết code controller/renderer trước.

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
- Cả 5 kỹ năng mới đã có tác dụng: Đèn laser, Quạt từ M05 (mục "Đèn laser và Quạt"); Kẹp gắp, Kéo cắt, Bình xịt nước từ M06 (mục "Kẹp gắp, Kéo cắt, Bình xịt nước"). Một kỹ năng mới mở khi đã thắng màn có `"unlocksSkill": "<id>"` (dữ liệu gốc chưa màn nào có; chương 2 thêm ở M21); chưa mở thì hiện ô khóa, không chọn được. Riêng **trận tự do** mang được cả 10 kỹ năng (người dùng chốt 2026-10-28, tới khi có chương 2). Cờ `Working` trong code vẫn còn cho kỹ năng sau này chưa có tác dụng.
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

Từ 2026-10-20 battle.json có khóa tùy chọn `campaign`: các chương, mỗi chương là danh sách màn chơi theo thứ tự. Thắng một màn thì mở màn kế tiếp trong chương; màn đầu luôn mở.

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

- Thẻ tiện ích = thẻ không có `group` (trừ `heal`): `utilityChance` (cấp trên cùng, 0–1, gốc 0.25) là tỉ lệ một thẻ tiện ích chiếm một ô ngẫu nhiên trong lượt mời (luồng random riêng `BattleRandom.Utility`); chúng vẫn lấp chỗ khi nhóm hết thẻ. Thẻ app không bao giờ được mời khi trận không có app.
- Mỗi nhóm được tối đa **hai** thẻ hiếm (trước là một): khi thẻ hiếm thay chỗ thì bốc ngẫu nhiên trong các thẻ hiếm còn lấy được của nhóm.
- Bản gốc thêm: Sức mạnh Móng vuốt + Kết liễu (hiếm); Tốc độ Nhịp dài + Lướt gió (hiếm); Phòng thủ Hồi phục + Kiên cường; tiện ích Tay nhanh, Sạc pin, Hút năng lượng, Bom to, Khiên dày. Giới hạn Vô tận: Sắc bén 5, Nhanh chân 3, Ra đòn nhanh 4, Lông dày 3, Tim to 4, Móng vuốt 3 (Đẩy mạnh giữ 2).

### Nhóm thẻ, synergy và build cộng dồn (Combat Depth mốc 2)

- `group`: `defense`, `power` hoặc `speed` (bỏ trống = không thuộc nhóm). Thẻ `heal` không được có nhóm. Mỗi lần lấy một thẻ có nhóm = 1 điểm nhóm đó. Bản gốc: Defense = Lông dày + Tim to (`big_heart`, +20 HP tối đa, tối đa 2), Power = Sắc bén + Đẩy mạnh, Speed = Nhanh chân + Ra đòn nhanh.
- Luật đưa thẻ: mỗi nhóm còn thẻ chưa chạm `maxStacks` góp 1 thẻ ngẫu nhiên (thứ tự Defense, Power, Speed); nhóm hết thẻ thì thẻ khác lấp chỗ. Mèo mất máu thì thẻ hồi máu thay thẻ không nhóm hoặc thẻ của nhóm build đang ít điểm nhất; HP đầy không bao giờ có thẻ hồi máu.
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
| `tether` (dây, Q4) | Tự buộc dây vào đồng minh gần nhất trong `range` (không phải boss, không phải con giữ dây khác, chưa bị ai buộc); gánh `share` (0.05–0.95, mặc định 0.5) mọi sát thương đồng minh nhận; đi sát ngay sau đồng minh, phía xa mèo | `share` | `CutLink` (ném mèo xuyên dây, Kéo cắt: M03/M06) hoặc hạ con giữ dây: đồng minh choáng `breakStun` s. Dây giãn quá `range` × 1,5 thì tự đứt, không thưởng. Sau `relink` s mới buộc lại |
| `rhythm` (nhịp, Q3) | Mỗi `beat` s (0.2–3, mặc định 0.8) một nhịp; nhịp thứ `beats` (2–8, mặc định 4) mọi quái trong `range` (cả nó) đang đánh tới mèo được thì **gồng cùng lúc**, bỏ qua lượt ra đòn và hồi chiêu | `beat`, `beats` | Bị choáng (Đẩy, búa…) thì đếm lại từ 0. Không gồng khi mèo đang được cầm hay đang `landingGrace`; không áp dụng cho boss, quái hỗ trợ, con đang cưỡi, con đang hoảng |
| `ride` (cưỡi, Q6) | Cưỡi lên quái loại `mount` (bắt buộc, quái đã biết, không phải boss, không tự cưỡi) trong `range`, chưa bị ai cưỡi; được chở đi, vẽ trên lưng; mèo không nhắm và không click chọn được con đang cưỡi (đánh con bị cưỡi). Cách mèo `dropRange` (20–400, mặc định 90) thì nhảy xuống, không choáng | `mount`, `dropRange` | Hạ con bị cưỡi hoặc `CutLink`: con cưỡi rơi tại chỗ, choáng `breakStun` s |
| `merge` (nhập, Q5/Q7) | `count` (2–6, mặc định 3) con cùng loại đứng trong `range` của nhau liên tục `mergeSeconds` (0.5–10, mặc định 2) s thì nhập thành một quái `into` (bắt buộc, quái đã biết, không phải boss, không tự nhập), máu = tổng máu hiện tại, máu tối đa = tổng máu tối đa. Không tính là hạ quái, không kết thúc đợt | `count`, `mergeSeconds`, `into` | Bị choáng (Đẩy…) thì đồng hồ của con đó về 0; tách xa nhau cũng vậy |
| `aura` (hào quang, Q8) | Đồng minh trong `range` đi nhanh ×`speed` (1–3, mặc định 1.3); lao (dash) không nhanh thêm | `speed` | Hạ nó: mọi quái khác (trừ boss) **hoảng** `breakStun` s: chạy ra xa mèo, không đánh |

- **Tối đa 2 loại liên kết mỗi trận** (`EnemyLinks.MaxKinds`): lúc nạp dữ liệu, mỗi danh sách đợt (trận tự do, màn Phiêu lưu, Viễn chinh) mà tính cả quái bốc ngẫu nhiên, quân boss gọi, quái được gọi, `mount`, `into` có hơn 2 loại `link` thì file bị từ chối. Lúc chạy (đợt sinh tự động như Vô tận) hai loại đến trước được dùng, quái mang loại thứ 3 đánh như quái thường.
- **Tắt** một liên kết trong battle.json của người dùng: `"enemies": [ { "id": "chain_slime", "link": { "enabled": false } } ]` (gộp với file gốc, phần còn lại giữ nguyên).
- Hình: dây nhầy xanh võng nhẹ từ con giữ dây tới đồng minh; vòng vàng đứt nét trên sàn quanh con hào quang; vòng cam chấm trên sàn quanh con nhịp và các chấm nhịp trên đầu (nhịp ngay trước khi gồng chuyển đỏ và nháy); thanh tím đếm ngược trên con sắp nhập; giọt mồ hôi xanh trên con đang hoảng. Chỉ vẽ nét viền, không tô nền, nên click vào khoảng trống vẫn xuyên xuống desktop.
- Pause/menu/chọn thẻ đóng băng mọi đồng hồ liên kết (như cả trận). Không dùng luồng ngẫu nhiên nào, nên seed cũ cho đúng trận cũ.
- Sự kiện mới cho âm thanh/hiệu ứng sau này: `LinkFormed`, `LinkCut`, `LinkSnapped`, `LinkBeat`, `LinkStrike`, `Merged`, `Dismounted`, `Panicked` (chưa gắn âm thanh ở M01).
- Đã bỏ loại `bounce` khỏi thiết kế (người dùng duyệt 2026-10-28): Q2 lò xo là bẫy, không có liên kết riêng.

## Quái trên không và tương tác ném (`hover`, `throwHit`) (bản 1.5, mốc M03, 2026-10-28)

Khung cho ngoại lệ Battle Mode 1.5 (docs/APPROVED-EXCEPTIONS.md → Battle Mode). Code: `src/DesktopPet.Core/PetBattle.Held.cs`, test `tests/DesktopPet.Tests/BattleHeldTests.cs` (quái giả). **battle.json gốc chưa có quái nào dùng hai khóa này**: mọi trận hiện tại chạy y như trước (mô phỏng cân bằng giống hệt từng dòng). Quái thật dùng ở M14 (slime bay), M15 (slime băng, bám tường); cắt dây bằng ném mèo làm cùng Q4 (M10).

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
- **Được mang ở đâu:** theo người dùng chốt 2026-10-28, **trận tự do** mang được mọi kỹ năng đã chạy (Laser, Quạt) ngay; Phiêu lưu, Viễn chinh, Vô tận vẫn chỉ mở bằng `unlocksSkill` (chương 2, M21). Lựa chọn lưu chung `BattleLoadout`: chế độ khác đọc bộ có Laser/Quạt chưa mở thành 5 kỹ năng cũ.
- **Hướng dẫn đứng hình:** bước `laser` và `fan` (kiểu "làm") chỉ chạy trong màn có `unlocksSkill` đúng kỹ năng đó và khi trận mang kỹ năng ấy: sáng ô, chọn xong thì sáng khoảng sàn quanh mèo; dùng xong là hết bước. Chương 1 không có màn như vậy nên không đổi. Màn dạy tự cho mượn kỹ năng: làm cùng chương 2 (M21).
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
- **Được mang ở đâu:** như Laser/Quạt: trận tự do mang ngay; Phiêu lưu, Viễn chinh, Vô tận chờ `unlocksSkill` (chương 2, M21).
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
  "id": "endless", "rulesVersion": 4, "unlockAfter": "c1_slime_king", "upgradeEvery": 2, "bossEvery": 10, "boss": "king_c1",
  "lateBoss": "king", "lateBossFrom": 30, "bossBonusPick": true, "allSynergiesAt": 48,
  "eliteChancePerWave": 0.007, "eliteChanceMax": 0.45, "moreElitesEvery": 20, "moreElitesMax": 2,
  "firstCount": 3, "countPerWave": 0.35, "maxCount": 14, "threatPerWave": 0.06, "maxAlive": 5, "eliteChance": 0.12,
  "kinds": [ { "enemy": "slime", "weight": 50 }, { "enemy": "runner", "weight": 25, "from": 3 }, { "enemy": "mender", "weight": 15, "from": 5 }, { "enemy": "rock", "weight": 15, "from": 7 } ]
}
```

- Một trận không bao giờ thắng. Đợt n có `firstCount` + `countPerWave` × (n − 1) quái (làm tròn xuống, 1 đến `maxCount`), bốc theo `kinds` (mỗi loại có từ đợt `from`, theo `weight`; phải có ít nhất một loại từ đợt 1; không được là trùm), threat 1 + `threatPerWave` × (n − 1).
- Mỗi đợt thứ `bossEvery` (0 = không có) chỉ có `boss` (phải là quái trùm); từ đợt `lateBossFrom` (1–500, gốc 30) có thêm `lateBoss` (trùm, tùy chọn) cùng đợt. Sau mỗi `upgradeEvery` đợt dọn xong (0 = không có thẻ) người chơi chọn 1 thẻ, giữ tới hết lượt; `bossBonusPick` true: dọn xong đợt trùm thì chọn liền 2 thẻ.
- (2026-10-24) Tinh anh tăng dần: tỉ lệ = `eliteChance` + `eliteChancePerWave` (0–0.2) × (n − 1), tối đa `eliteChanceMax` (0–1, 0 = không trần); cứ `moreElitesEvery` đợt (0–200, 0 = không) được thêm một tinh anh sống cùng lúc so với `maxElites`, thêm tối đa `moreElitesMax` (0–4). `allSynergiesAt` (1–500, tùy chọn): từ đợt này mọi synergy (cả tầng chỉ Vô tận) tự bật, kể cả build lệch nhóm.
- Bản gốc từ 2026-10-24 (rulesVersion 4): `upgradeEvery` 2, `boss` `king_c1` (Vua chương 1, có khiên và phong tinh anh), `lateBoss` `king` từ đợt 30, `bossBonusPick` true, `allSynergiesAt` 48, tinh anh 12% + 0,7%/đợt tối đa 45%, thêm một tinh anh sống mỗi 20 đợt (tối đa 3 con). Tới đợt 48 có 24 lượt thẻ thường + 4 lượt thưởng sau trùm = 28 lượt, đủ 27 điểm cho ba nhóm × 9 nếu chia đều; không đều thì lưới an toàn ở đợt 48 bật nốt.
- Hết lượt khi mèo thua hoặc người chơi bấm Kết thúc (cả hai đều có bảng kết quả và tính kỷ lục). Đóng app giữa chừng thì mất lượt, không lưu gì.
- Kỷ lục: đợt xa nhất và số slime hạ nhiều nhất, theo vũ khí + độ khó, trong battle-records.json (khóa `endless|r<rulesVersion>|<vũ khí>|<độ khó>`, thêm `|custom` khi dùng battle.json riêng). Đổi luật nhiều thì tăng `rulesVersion` để kỷ lục cũ đứng riêng.
- `unlockAfter`: màn Phiêu lưu phải thắng trước (không có màn đó trong dữ liệu thì mở luôn). `maxAlive`, `eliteChance`: như của màn.
- `milestones` (2026-10-22): phần thưởng khi **vượt qua** đợt `wave` (bất kỳ vũ khí/độ khó; dữ liệu tùy chỉnh không tính), nhận một lần và giữ mãi. Mỗi mục: `id` (dùng chung không gian id với kỷ vật Viễn chinh, không được trùng), `wave` (tăng dần, tối đa 500), `kind` = `accessory` (kèm `accessory`: phụ kiện có `reward` trong accessories.json) hoặc kỷ vật trưng bày `hourglass`, `orb`, `cushion`; `nameVi`/`nameEn` 1–32 ký tự; tối đa 12 mục. Gốc: đợt 10 Vòng cổ ∞, 20 Đồng hồ cát vô tận, 30 Vành sao, 40 Quả cầu tinh vân, 50 Áo choàng thiên hà. Đã nhận hay chưa tính từ kỷ lục (`MostCleared`, kỷ lục cũ dùng đợt xa nhất − 1) của mọi `rulesVersion`.

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
