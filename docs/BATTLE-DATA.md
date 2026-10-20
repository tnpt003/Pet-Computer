# Tùy chỉnh dữ liệu Battle Mode (battle.json)

Áp dụng từ 2026-10-19, `schemaVersion` 1. Không cần sửa code hay build lại exe để thêm biến thể quái, vũ khí hoặc đổi các đợt quái.

## File nằm ở đâu

| File | Vai trò |
| --- | --- |
| `src/DesktopPet/Assets/battle.json` | Dữ liệu gốc, nhúng vào exe. Người dùng không sửa file này. |
| `%LOCALAPPDATA%\DesktopPet\battle.json` | File của người dùng, cùng thư mục với `preferences.json` (hoặc `PET_DATA_DIRECTORY` nếu có đặt). Không bắt buộc phải có. |
| `docs/examples/battle.json` | Ví dụ thêm `stone_slime`. Test tự động cũng đọc file này nên ví dụ luôn hợp lệ. |

File của người dùng được đọc lại **mỗi lần mở bảng chuẩn bị** (Bắt đầu trận hoặc Chơi lại). Khi bấm Sẵn sàng, trận chụp lại dữ liệu đang có; sửa file giữa trận không ảnh hưởng trận đang chạy, chỉ áp dụng cho lần chuẩn bị sau.

## Cách gộp

Chỉ cần ghi những gì muốn đổi. File của người dùng được gộp đè lên dữ liệu gốc theo quy tắc sau:

- `"schemaVersion": 1` là **bắt buộc**. Thiếu hoặc khác 1 thì cả file bị bỏ qua.
- Số và chuỗi ở cấp trên cùng (ví dụ `"catMaxHp": 120`) thay giá trị gốc.
- Object (`easy`, `hard`) gộp theo từng khóa; ví dụ `"easy": { "enemyHp": 0.5 }` chỉ đổi đúng một số đó.
- `enemies` và `weapons` gộp **theo `id`**:
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

## Đợt quái (`waves`) và luật trận

- `waves`: tối đa 30 đợt. Mỗi đợt có `groups`, mỗi group gồm `enemy` (id) và `count` (1–50). Các group ra quái theo thứ tự; tối đa `maxAlive` con cùng lúc. Đợt nào cũng có thể có boss.
- Luật trận ở cấp trên cùng: `catMaxHp`, `maxAlive`, `spawnInterval`, `firstWaveDelay`, `waveBreakSeconds`, `waveHeal`, `landingGraceSeconds` (0–5), `rescueGraceCooldownSeconds` (0–60), kỹ năng (`pushCooldown`, `healAmount`…), combo (`comboHits` 2–6…).
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

Kỹ năng (`BattleSkills`) chưa chuyển sang dữ liệu trong đợt này.

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
| `armorBreak` | 0.05–3 | Phá giáp: đòn combo × (1 + amount). Xuyên khả năng chặn của slime đá sẽ có ở mốc 3. (0.3) |
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
