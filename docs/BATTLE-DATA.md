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
| `style` | `sword` hoặc `hammer` | Hình vẽ cầm tay |
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
