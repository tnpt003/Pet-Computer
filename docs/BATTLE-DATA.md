# Tùy chỉnh dữ liệu Battle Mode (battle.json)

Áp dụng từ 2026-10-19, `schemaVersion` 1. Không cần sửa code hay build lại exe để thêm biến thể quái, vũ khí hoặc đổi các đợt quái.

## File nằm ở đâu

| File | Vai trò |
| --- | --- |
| `src/DesktopPet/Assets/battle.json` | Dữ liệu gốc, nhúng vào exe. Người dùng không sửa file này. |
| `%LOCALAPPDATA%\DesktopPet\battle.json` | File của người dùng, cùng thư mục với `preferences.json` (hoặc `PET_DATA_DIRECTORY` nếu có đặt). Không bắt buộc phải có. |
| `docs/examples/battle.json` | Ví dụ thêm `stone_slime`. Test tự động cũng đọc file này nên ví dụ luôn hợp lệ. |

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
| `style` | `sword` hoặc `hammer` | Hình vẽ cầm tay và kiểu vung (búa: giơ cao, gồng, đập) |
| `hitEffect` | `spark` hoặc `thud`, mặc định theo style | Dấu đòn trúng: tia trắng / bụi và vòng trên sàn |
| `damage` | 0–1000 | Sát thương mỗi nhát |
| `range` | 10–300 | Tầm đánh |
| `attackCooldown` | 0.1–10 | Thời gian từ nhát này tới nhát sau |
| `swingDuration` | 0.05–5 | Độ dài một lần vung |
| `hitTime` | lớn hơn 0 và nhỏ hơn `swingDuration` | Thời điểm duy nhất trong lần vung gây sát thương |
| `knock` | 0–1000 | Mỗi nhát đẩy lùi slime (DIP/giây × `pushFactor`); 0 = chỉ combo mới đẩy |

Nếu `id` đã lưu trong preferences không còn trong dữ liệu (ví dụ file bị xóa), mèo dùng vũ khí đầu tiên. Preferences cũ ghi `"Sword"`/`"Hammer"` vẫn khớp với `sword`/`hammer`.

## Kỹ năng (`skills`) (2026-10-22)

Tên và mô tả của 5 kỹ năng trên HUD (và túi đồ). Gộp theo `id` như `weapons`: ghi `{ "id": "heal", "nameEn": "Fish snack" }` là đổi đúng tên tiếng Anh của Cá hồi máu. Số liệu vẫn ở cấp trên cùng (`pushCooldown`, `healAmount`…); thứ tự trên HUD, kỹ năng nào phải nhắm (Đẩy, Bom) và tác dụng nằm trong app, không đổi bằng dữ liệu.

| Khóa | Giới hạn | Ý nghĩa |
| --- | --- | --- |
| `id` | `push`, `heal`, `cheer`, `bomb`, `shield` | Kỹ năng nào; danh sách phải có đủ cả 5, không trùng |
| `nameVi`, `nameEn` | 1–32 ký tự | Tên |
| `descVi`, `descEn` | 1–220 ký tự | Mô tả khi rê chuột; `{khóa}` được thay bằng số cùng tên ở cấp trên cùng của battle.json, ví dụ `{pushCooldown}` → `5` (không phân biệt hoa thường; khóa không có thì file bị từ chối) |

## Đợt quái (`waves`) và luật trận

- `waves`: tối đa 30 đợt. Mỗi đợt có `groups`, mỗi group gồm `enemy` (id) và `count` (1–50). Các group ra quái theo thứ tự; tối đa `maxAlive` con cùng lúc. Đợt nào cũng có thể có boss.
- Luật trận ở cấp trên cùng: `catMaxHp`, `maxAlive`, `spawnInterval`, `firstWaveDelay`, `waveBreakSeconds`, `waveHeal`, `landingGraceSeconds` (0–5), `rescueGraceCooldownSeconds` (0–60), `rescueGraceMinLiftSeconds` (0–2, mặc định 0.3: nhấc ngắn hơn thì thả xuống không được bảo vệ), kỹ năng (`pushCooldown`, `healAmount`…), combo (`comboHits` 2–6…).
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
          "goalVi": "…", "goalEn": "…",       // tối đa 120 ký tự, hiện trên bản đồ và bảng chuẩn bị
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
| `armor` | 0.01–0.5 | sát thương nhận vào × (1 − tổng), không dưới 30% |
| `maxHp` | 1–500 (HP) | cộng HP tối đa; lúc chọn hồi luôn đúng lượng đó (Combat Depth mốc 2) |

`maxStacks` 0–10 (0 là không giới hạn). Tên tối đa 32 ký tự, mô tả tối đa 80. Effect lạ, amount ngoài khoảng, id trùng hoặc danh sách rỗng đều làm cả file bị bỏ.

### Nhóm thẻ, synergy và build cộng dồn (Combat Depth mốc 2)

- `group`: `defense`, `power` hoặc `speed` (bỏ trống = không thuộc nhóm). Thẻ `heal` không được có nhóm. Mỗi lần lấy một thẻ có nhóm = 1 điểm nhóm đó. Bản gốc: Defense = Lông dày + Tim to (`big_heart`, +20 HP tối đa, tối đa 2), Power = Sắc bén + Đẩy mạnh, Speed = Nhanh chân + Ra đòn nhanh.
- Luật đưa thẻ: mỗi nhóm còn thẻ chưa chạm `maxStacks` góp 1 thẻ ngẫu nhiên (thứ tự Defense, Power, Speed); nhóm hết thẻ thì thẻ khác lấp chỗ. Mèo mất máu thì thẻ hồi máu thay thẻ không nhóm hoặc thẻ của nhóm build đang ít điểm nhất; HP đầy không bao giờ có thẻ hồi máu.
- `synergies` (gộp theo `id`): `{ "id", "group", "at": 1–10, "effect", "amount", "every", "nameVi/En" ≤ 32, "descVi/En" ≤ 80 }`. Đạt `at` điểm của nhóm thì bật vĩnh viễn trong build đó, banner "★ TÊN!" một lần.

| effect | amount | Ý nghĩa (bản gốc) |
| --- | --- | --- |
| `thorns` | 0.05–2 | Giáp gai: phần sát thương mèo **thực nhận** (sau giáp) dội lại quái vừa đánh. Khiên chặn thì không dội. (0.35) |
| `armorBreak` | 0.05–3 | Phá giáp: đòn combo × (1 + amount) và xuyên khả năng chặn đòn (`blockChance`, Combat Depth mốc 3). (0.3) |
| `echo` | 0.1–2, `every` 2–10 | Dư ảnh: cứ `every` nhát trúng thì thêm một đòn amount × sát thương nhát đó. (0.5, mỗi 4 nhát) |

Đòn dội lại và đòn dư ảnh không kích hoạt thêm bất kỳ hiệu ứng build nào (không có vòng lặp).
- Build cộng dồn (Phiêu lưu): thắng màn N lưu build lúc kết thúc (không lưu thẻ hồi máu) vào `adventure-progress.json` (`Builds`, file phiên bản 2). Màn N+1, lần đầu hay chơi lại, luôn bắt đầu bằng build đã lưu của màn N; màn đầu chương bắt đầu trống. Thua không đổi build đã lưu; thắng lại màn cũ thì thay build lưu của màn đó. Chơi bằng dữ liệu tùy chỉnh thì không lưu gì. Trận tự do không có build. Thẻ trong build mà dữ liệu không còn thì bị bỏ qua; số lần vượt `maxStacks` bị cắt.
- `maxUpgrades` (1–30) trên một màn: số lần chọn thẻ tối đa trong một lượt (màn 3 gốc: 3). Không đặt: mỗi đợt dọn xong mà còn đợt sau thì có một lần chọn.

Màn sinh tồn: `"objective": "survive", "surviveSeconds": 10–600`. Các đợt ra theo thứ tự, hết đợt thì đợt cuối lặp lại cho tới hết giờ. Đồng hồ dừng khi mèo đang được cầm. `surviveSeconds` trên màn "clear" là lỗi.

## Quái hỗ trợ, pha của boss, phần thưởng (mốc 4)

- Quái hồi máu: `healAmount` (0–1000, 0 = không hồi), `healRange` (10–600), `healCooldown` (0.5–60), `healWindup` (0.2–5), `healBudget` (1–100000: tổng máu tối đa con đó có thể hồi). `marks: "cross"` vẽ dấu "+" trên thân.
- Pha của boss: `phases: [ { "at": 0.5, "summon": "mender", "count": 1 }, { "at": 0.3, "enrage": true } ]` (1–6 pha; `at` trong khoảng 0–1; mỗi pha phải gọi quân hoặc nổi giận; `count` 1–6). `summonCap` 1–10 là số quân được gọi còn sống cùng lúc. Quân được gọi không được là quái biết gọi quân. Có pha nổi giận thì cần `enragedSpeed`/`enragedWindup`.
- Phần thưởng màn: `"reward": "<id phụ kiện>"`, phụ kiện đó phải có `"reward"` trong accessories.json. Chỉ nhận một lần, chỉ với dữ liệu gốc.

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
  - Boss `rallyEvery` (cần pha khiên): khi khiên bật và quân của pha đã ra, cứ `rallyEvery` s vương miện sáng `rallyCharge` s rồi quân gần mèo nhất (không phải support, chưa là tinh anh) **thành tinh anh vĩnh viễn** (từ 2026-10-21, thay cho tăng tốc): viền vàng + sao, affix tinh anh riêng của loại đó nếu có, +`rallyHp` HP tối đa (0–3, mặc định 0.5; cộng luôn vào HP hiện tại) và sát thương ×`rallyDamage` (1–3, mặc định 1.25). Không tính vào `maxElites`. Đẩy boss lúc vương miện sáng thì hủy lệnh. Mọi quân đánh được đã là tinh anh thì vương miện không sáng nữa. Banner "★ … thành tinh anh!".
  - Boss `shieldBreakStun` (0–5): khiên vỡ thì boss choáng bấy nhiêu giây, vẫn nhận sát thương.
  - Quân boss gọi ra không xuất hiện trong vòng 90 DIPs quanh mèo khi còn chỗ.
  - `marks: "leaf"`: slime thảo dược có mũ lá + túi thảo dược (không còn dấu + trên thân); lá sáng khi gồng hồi, hạt sáng bay tới đồng minh; hết lượng hồi thì lá cụp, xỉn màu. `"cross"` vẫn hợp lệ cho dữ liệu cũ.
- Bản gốc: slime đá `blocker`, `blockFrontOnly`, `turnSeconds` 0.6, `hitScale` 1.5 (vẽ rộng/thấp, cầm khiên đá hình huy hiệu ở phía mặt, thêm vết nứt dưới 50% HP); slime thảo dược `leaf`, `farHealBelow` 0.35 / 1.2 s / 8 s (trước đây `teleportBelow`); Vua Slime chương 1 `rallyEvery` 10, `rallyCharge` 0.8, `rallyHp` 0.5, `rallyDamage` 1.25, `shieldBreakStun` 2 (trước đây `rallyEvery` 7 + tăng tốc ×1.4 trong 3 s). Màn 5 đợt 1: đá + slime từ mép xa, 2 slime nhanh từ phía sau. Cầm cự: hai phía từ giây 45. rulesVersion màn 1–5: 3/7/8/8/8 (chia lượt ra đòn áp cho mọi màn).

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
  - Bản gốc (1.2.0-beta): slime thường `hp` 1.2, `attackCooldown` 0.65; slime nhanh gồng +0.3 s, lao ×1.4, `damage` 1.2; slime đá 1 lần chặn chắc chắn, `hp` 1.3, `pushImmune`; slime thảo dược lần hồi đầu ×2, `healCooldown` 0.85, `healBudget` 1.3. Màn 3–5 `eliteChance` 0.3, `maxElites` 1.
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

## Viễn chinh (`expedition`) (2026-10-21)

Một chuyến đi gồm nhiều bậc, mỗi bậc chọn một điểm, bậc cuối là trùm. HP và build của mèo đi tiếp từ bậc này sang bậc sau; chuyến mới bắt đầu lại từ đầu. Code: `src/DesktopPet/PetExpedition.cs` (luật, không WPF), `PetWindow.Expedition.cs` (bản đồ, kết quả).

```json
"expedition": {
  "id": "slime_trail", "nameVi": "Đường mòn Slime", "nameEn": "The Slime Trail",
  "rulesVersion": 5, "unlockAfter": "c1_slime_king", "retreatCost": 0.25, "winHeal": 0.05, "stepThreat": 0.1,
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
- `keepsakes` (mốc 4, tối đa 12): mỗi món cho một lần, khi thắng cả chuyến ở độ khó `difficulty` trở lên (`exact: true`: chỉ đúng độ khó đó). `kind`: `cup` (cúp trên góc trưng bày, `tier` là `bronze`/`silver`/`gold`), `cushion` (đệm slime trên góc trưng bày), `accessory` (`accessory` là id một phụ kiện có `reward` trong accessories.json; nhận rồi thì Cài đặt cho đội). Cho cùng lần lưu với kết quả chuyến nên không bao giờ cho hai lần; battle.json tùy chỉnh không cho kỷ vật. Góc trưng bày vẽ theo dữ liệu gốc của app.
- `maxRelics` (0–6): số di vật giữ cùng lúc tối đa; đủ thì không còn được đưa di vật. `eliteRelics` (0–3): thắng tinh anh được chọn 1 trong ngần ấy di vật (hoặc bỏ qua) trên bản đồ trước khi đi tiếp.
- Lựa chọn ở điểm nghỉ/sự kiện/thưởng tinh anh được bốc một lần từ seed của chuyến và lưu trong chuyến (`Pending`), nên đóng/mở bản đồ hay mở lại app không bốc lại; mỗi lựa chọn chỉ áp dụng một lần.
- `fights`, `elites`, `boss`: các trận (encounter) viết như một màn Phiêu lưu (`waves`, `threat`, `maxAlive`, `eliteChance`, `upgrades`, `maxUpgrades`…) nhưng chỉ là màn `clear`, không có `reward` hay `tipVi/tipEn`. `boss` phải có một đợt có boss. Mỗi bậc cần đủ trận trong kho của loại đó để hai điểm cùng bậc khác nhau.
- id trận không được trùng nhau và không trùng id màn Phiêu lưu. Chuyến đã lưu ghi id trận, nên đổi id là trận mới.
- `rulesVersion`: tăng khi đổi luật đủ nhiều để chuyến đang lưu không nên đi tiếp và kỷ lục cũ không nên so. Chuyến đang lưu không khớp (khác `id`/`rulesVersion`, bản đồ khác hình dạng, id trận không còn) sẽ kết thúc khi mở bản đồ, kèm một dòng báo.
- `unlockAfter`: màn Phiêu lưu phải thắng trước. Nếu dữ liệu không còn màn đó (ví dụ battle.json của bạn thay cả `campaign`), Viễn chinh mở luôn.
- `retreatCost` (0–1): rời trận của một bậc (Kết thúc trận, đóng app, crash, đặt lại vị trí mèo) thì bậc đó vẫn chờ đánh lại với cùng seed, nhưng mèo mất phần này của HP tối đa. HP về 0 thì chuyến kết thúc (thua). Trận đang đánh khi đóng app được tính lúc mở bản đồ lần sau.
- `winHeal` (0–1): thắng một bậc thì hồi phần này của HP tối đa (ngoài hồi giữa các đợt như thường).
- `stepThreat` (0–0,5): quái mỗi bậc mạnh hơn bậc trước chừng này (bậc 1 ×1, bậc 2 ×1,1…), nhân với `threat` của trận.
- Bản đồ được bốc một lần từ seed của chuyến khi bắt đầu và được lưu lại; mỗi điểm có seed trận riêng suy ra từ seed chuyến, nên đóng/mở bản đồ hay tiếp tục sau khi mở lại app không đổi bản đồ hay trận.
- Lưu ở `expedition.json` (phiên bản 2 từ mốc 3: thêm di vật và lựa chọn đang mở; phiên bản 3 từ mốc 4: thêm `Keepsakes`; phiên bản 4 từ 2026-10-22: `Kinds` của chuyến, `Free`/`Gold` của máy viên nang; bản app cũ chỉ đọc, không ghi đè) (cạnh preferences.json, tách khỏi `adventure-progress.json`): chuyến đang đi, kỷ lục theo `expedition|r<rulesVersion>|vũ khí|độ khó` (số chuyến, số chuyến thắng, đi xa nhất, nhanh nhất theo thời gian đánh), các chuyến đã tính. Lưu an toàn như Phiêu lưu (.tmp, .bak, file của bản mới hơn chỉ đọc). Chơi với battle.json tùy chỉnh thì chuyến đi chỉ giữ trong bộ nhớ, không ghi file.
- Cách gộp: như `campaign`, khóa được gộp từng khóa; các mảng (`steps`, `fights`, `elites`, `events`, `relics`, `keepsakes`) trong file của bạn thay toàn bộ mảng gốc.
- Log khi bắt đầu trận: `Battle started: Expedition step N <id trận>, threat ×…, <độ khó>, <vũ khí>, seed …`.

## Vô tận (`endless`) (2026-10-22)

```jsonc
"endless": {
  "id": "endless", "rulesVersion": 1, "unlockAfter": "c1_slime_king", "upgradeEvery": 3, "bossEvery": 10, "boss": "king",
  "firstCount": 3, "countPerWave": 0.35, "maxCount": 14, "threatPerWave": 0.06, "maxAlive": 5, "eliteChance": 0.12,
  "kinds": [ { "enemy": "slime", "weight": 50 }, { "enemy": "runner", "weight": 25, "from": 3 }, { "enemy": "mender", "weight": 15, "from": 5 }, { "enemy": "rock", "weight": 15, "from": 7 } ]
}
```

- Một trận không bao giờ thắng. Đợt n có `firstCount` + `countPerWave` × (n − 1) quái (làm tròn xuống, 1 đến `maxCount`), bốc theo `kinds` (mỗi loại có từ đợt `from`, theo `weight`; phải có ít nhất một loại từ đợt 1; không được là trùm), threat 1 + `threatPerWave` × (n − 1).
- Mỗi đợt thứ `bossEvery` (0 = không có) chỉ có `boss` (phải là quái trùm). Sau mỗi `upgradeEvery` đợt dọn xong (0 = không có thẻ) người chơi chọn 1 thẻ, giữ tới hết lượt.
- Hết lượt khi mèo thua hoặc người chơi bấm Kết thúc (cả hai đều có bảng kết quả và tính kỷ lục). Đóng app giữa chừng thì mất lượt, không lưu gì.
- Kỷ lục: đợt xa nhất và số slime hạ nhiều nhất, theo vũ khí + độ khó, trong battle-records.json (khóa `endless|r<rulesVersion>|<vũ khí>|<độ khó>`, thêm `|custom` khi dùng battle.json riêng). Đổi luật nhiều thì tăng `rulesVersion` để kỷ lục cũ đứng riêng.
- `unlockAfter`: màn Phiêu lưu phải thắng trước (không có màn đó trong dữ liệu thì mở luôn). `maxAlive`, `eliteChance`: như của màn.
- `milestones` (2026-10-22): phần thưởng khi **vượt qua** đợt `wave` (bất kỳ vũ khí/độ khó; dữ liệu tùy chỉnh không tính), nhận một lần và giữ mãi. Mỗi mục: `id` (dùng chung không gian id với kỷ vật Viễn chinh, không được trùng), `wave` (tăng dần, tối đa 500), `kind` = `accessory` (kèm `accessory`: phụ kiện có `reward` trong accessories.json) hoặc kỷ vật trưng bày `hourglass`, `orb`, `cushion`; `nameVi`/`nameEn` 1–32 ký tự; tối đa 12 mục. Gốc: đợt 10 Vòng cổ ∞, 20 Đồng hồ cát vô tận, 30 Vành sao, 40 Quả cầu tinh vân, 50 Áo choàng thiên hà. Đã nhận hay chưa tính từ kỷ lục (`MostCleared`, kỷ lục cũ dùng đợt xa nhất − 1) của mọi `rulesVersion`.

## App trên màn hình (`apps`) (2026-10-23)

App "giả" do game tự vẽ (không dùng tên hay logo app thật), có trong trận Phiêu lưu, Viễn chinh và Vô tận (trận tự do không có). Logic ở `PetBattle.Apps.cs`, hình ở `PetBattleView.Apps.cs`; Python không tham gia.

- **App giúp mèo:** dọn xong một đợt (không phải đợt cuối) thì rơi 1 icon gần mèo (90–160 DIPs); hạ một quái thì rơi với xác suất `dropChance`. Icon lơ lửng trên dải chiến trường `life` giây (3 giây cuối nhấp nháy), tối đa `maxOnField` icon. Người chơi **bấm vào icon** để dùng; rê chuột lên icon thì hiện mô tả.
  - `tabs` Tab Lốc Xoáy: 3 tab xoay quanh mèo `tabSeconds` giây, mỗi `tabEvery` giây gây `tabDamage` (× sát thương của build) cho quái trong `tabRadius` DIPs (cộng nửa bề ngang quái). Không đánh khi mèo đang bị nhấc.
  - `ping` Bão Thông Báo: chuông "99+" rơi lên mọi quái: `pingDamage` (× build) và choáng `pingStun` giây (× hệ số choáng của loại quái); không choáng quái đang lao/hồi máu/nhảy, tinh anh miễn Đẩy.
  - `endtask` End Task: quái nhiều máu nhất (không tính trùm đang bật khiên, quái đang được hồi) bị tắt; trùm hoặc tinh anh chỉ mất `endTaskShare` máu tối đa (vẫn dừng ở mốc khiên như mọi đòn).
  - `undo` Ctrl+Z: hoàn lại máu mèo mất trong `undoSeconds` giây vừa qua. Chưa mất gì thì icon ở lại.
  - Sát thương từ app không bị đá chặn (như bom len). App không dùng được khi mèo đang bị cầm/bay (giống kỹ năng); khi đó icon cũng đứng thời gian.
- **Quảng cáo (quái):** từ khi `threat` hiện tại (màn × đợt × bậc Viễn chinh) ≥ `popupMinThreat`, mỗi đợt có `popupChance` bật 1 cửa sổ quảng cáo 2–6 giây sau khi đợt bắt đầu, đè lên một con quái. Click vào thân cửa sổ không trúng quái phía sau; bấm **×** để tắt (được cả khi đang cầm mèo). Để `popupSeconds` giây thì nó thả ra 1 con `popupSpawn` (nếu sàn còn chỗ). Dọn xong đợt thì quảng cáo tự đóng.
- Pause/menu/fullscreen đóng băng icon như mọi thứ trong trận; kết thúc trận xóa hết. Random của app là luồng riêng của seed trận (`BattleRandom.Apps`): thêm app không đổi quái/thẻ/chặn đòn của cùng seed.
- `list`: mỗi app một mục (`tabs`, `ping`, `endtask`, `undo`, `popup`), đủ cả 5, không trùng: `nameVi`/`nameEn` 1–32 ký tự, `descVi`/`descEn` ≤ 120 ký tự (mô tả là chữ thường, không thay `{khóa}`), `weight` 0–100 = tần suất rơi tương đối (popup không bao giờ rơi). File riêng ghi `list` thì thay cả danh sách.

| Khóa | Giới hạn | Gốc |
| --- | --- | --- |
| `dropChance` | 0–1 | 0.12 |
| `waveDrop` | true/false | true |
| `life` | 2–60 s | 10 |
| `maxOnField` | 1–4 | 2 |
| `tabSeconds`, `tabRadius`, `tabDamage`, `tabEvery` | 0.5–30 s, 10–300, 0–1000, 0.1–5 s | 6, 70, 14, 0.5 |
| `pingDamage`, `pingStun` | 0–1000, 0–5 s | 14, 1.5 |
| `endTaskShare` | 0–1 | 0.25 |
| `undoSeconds` | 0.5–30 | 4 |
| `popupMinThreat`, `popupChance`, `popupSeconds`, `popupSpawn` | 0–100, 0–1, 2–60 s, id quái | 1.5, 0.35, 8, `slime` |

- rulesVersion khi thêm app: màn 1–5: 6/12/14/15/15, Vô tận 2 (kỷ lục cũ đứng riêng; phần thưởng mốc Vô tận vẫn tính mọi rulesVersion). Viễn chinh giữ 5 để không bỏ chuyến đang đi dở.
