# Mở rộng Battle Mode

Cập nhật: 2026-11-05, theo code bản 1.5. Dành cho phiên làm việc thêm nội dung mới. Đường dẫn và tên kiểu là của lúc viết; đối chiếu bằng `graphify query` trước khi sửa.

Luật không được phá: AGENTS.md, docs/APPROVED-EXCEPTIONS.md. Khóa dữ liệu: docs/BATTLE-DATA.md. Quy trình sửa: skill `battle-change`.

## Chọn cách rẻ nhất

| Muốn thêm | Chỉ dữ liệu được không | Khi nào cần code |
|---|---|---|
| Biến thể quái, đổi số liệu | Được | Không |
| Đội hình | Được | Không |
| Màn, chương | Được | Chương có phông mới |
| Tuyến Viễn chinh, vùng, sự kiện cùng loại | Được | Loại sự kiện mới |
| Chặng Vô tận | Được | Không |
| Thẻ, di vật, đột biến | Được nếu dùng `effect` có sẵn | `effect` mới |
| Vũ khí | Được nếu dùng `style` có sẵn | Kiểu vẽ mới |
| Cơ chế quái mới | Không | Luôn cần |
| Kỹ năng, app, app độc | Không | Luôn cần |
| Kiểu màn, chế độ chơi | Không | Luôn cần |

## Việc chung cho mọi nội dung mới

Mẫu chốt thiết kế: **vai trò/áp lực → dấu báo → cách đối phó → cơ hội phản công → giới hạn/điều kiện ngắt → tiêu chí kiểm tra**. Giữ ID Q/S/B/K… hiện có; ghi rõ đã duyệt, đã code, đã test tự động, đã xem render, đã chơi tay. Bảng luật tương tác và ngân sách quân/hiệu ứng: [STANDARDIZATION-1.5.md](STANDARDIZATION-1.5.md). Hình học cảnh báo lấy từ cùng nguồn với combat; boss nâng cấp dùng `BossZone`/`BossWaveZone`/`BossRainZone`, không chép số sang renderer.

1. **Dữ liệu:** thêm khóa thì sửa kiểu dữ liệu, `Validate` và fallback C#. `BattleData` bỏ cả file nếu có một lỗi; `BattleDataTests` fail khi battle.json lệch fallback. Tên, mẹo và mô tả có giới hạn độ dài, vượt là cả file bị từ chối.
2. **Chữ:** vi trong code/dữ liệu, `...En`, bản dịch `src/DesktopPet/Assets/i18n/ja.json` (câu tiếng Anh là khóa).
3. **Trạng thái trận:** mọi thứ mới dừng khi Pause, menu, chọn thẻ, túi đồ, hướng dẫn đứng hình; chịu được mèo đang cầm, đang bay, đang choáng; kết thúc trận thì dọn sạch.
4. **Kỷ lục:** đổi luật màn đã phát hành thì tăng `rulesVersion`. Viễn chinh: thêm số cũ vào `keepRuns` để chuyến đang đi tiếp tục được.
5. **Ngẫu nhiên:** dùng seed của trận, luồng riêng cho thứ mới để bảng mô phỏng cũ không đổi.
6. **Giới hạn:** tối đa 2 loại liên kết và 2 loại quái dùng chuột mỗi trận (kiểm tra lúc nạp).
7. **Kiểm tra:** test mới bằng quái giả, UiChecks có ảnh render, `scripts/test.ps1 -Ui`, so `AdventureBalanceTests`, thêm quái thì đo `scripts/perf.ps1 -Battle crowd-late`.
8. **Tài liệu:** docs/BATTLE-DATA.md, mục test tay trong docs/MANUAL-TEST.md, CHANGELOG, rồi skill `ship-milestone`.

## Quái

**Chỉ ghép khóa có sẵn:** thêm vào `enemies` (hoặc file `battle.json` của người dùng), có `tipVi`/`tipEn`/`counters` cho Sổ tay. Đưa vào đợt, chặng hoặc đội hình. `binSpit: false` nếu Thùng Rác không được nhả nó. Quảng cáo chỉ thả loại có trong `apps.popupKinds`. Slime hẹn giờ chỉ gọi đội trong `clock.callFormations`.

**Cơ chế mới:** lấy slime nhím làm mẫu, mỗi chỗ dưới đây đều phải xem:

| Việc | Chỗ |
|---|---|
| Khóa, `Validate`, các điều kiện loại trừ giữa khóa | `PetBattleData.cs` |
| Trạng thái trên `Slime`, sự kiện `BattleEventKind` | `PetBattleSettings.cs` |
| Luật | `PetBattle.<Tên>.cs` mới, gọi từ `PetBattle.Enemies.cs`; khởi tạo trong `PetBattle.Waves.cs` |
| Mục tiêu mèo chọn, sát thương | `PetBattle.cs` |
| Vẽ | `PetBattleView.<Tên>.cs` mới; `marks` phải có trong `EnemyMarks`, được xử lý trong `PetBattleView.cs` |
| Banner, âm thanh | `PetWindow.BattleFeedback.cs` |
| Bước hướng dẫn | `PetWindow.BattleGuide.cs` |
| Trận có giỏ cá | điều kiện loại trừ trong `PetBattle.Basket.cs` |

**Dễ quên:**

- Quái dùng chuột: thêm vào `EnemyType.UsesMouse`.
- Quái bay hoặc lên cao: `OutOfHand`, cửa sổ riêng trong `PetWindow.BattleSky.cs`; kỹ năng và app phải biết xử lý nó.
- Quái miễn hoặc đổi đòn: kiểm tra Bom, độc, phản đòn, Kẹp gắp, Kéo cắt, Bình xịt, Quạt, Laser, app.
- Liên kết mới: thêm vào `EnemyLinks.All`, có công tắc tắt.

## Kỹ năng

Thêm kỹ năng thì bảng chọn thành 5 trong N, HUD không đổi (`BattleSkills.Carry`).

| Việc | Chỗ |
|---|---|
| Enum, thứ tự `All` (ở cuối để giữ HUD), cách nhắm `SkillAim` | `BattleSkill` trong `PetBattleSettings.cs`; `BattleSkills` trong `PetBattleData.cs` |
| Tên, mô tả, fallback | `BattleSkills.BuiltIn`; `skills` trong battle.json |
| Số liệu | `BattleSettings` + `Validate` + battle.json |
| Luật | `PetBattle.Tools.cs` hoặc file riêng; đi qua `ToolHit` |
| Icon, ô hồi | `PetBattleHud.cs` |
| Click ngoài dải trận | `PetWindow.BattleAim.cs`, `PetWindow.BattleSky.cs` |
| Hướng dẫn | `PetWindow.BattleGuide.cs` |
| Bot mô phỏng | `AdventureBalanceTests` |
| Mở khóa, thẻ riêng | `unlocksSkill`, `suggestSkills` của màn; thẻ có `skill` |

## Vũ khí

Dữ liệu trong `weapons`. Kiểu vẽ mới: thêm vào `WeaponStyles`, vẽ trong `PetRenderer.cs` (cả icon thẻ). Đo cân bằng riêng như Cuộn len, Găng đấm.

## App và app độc

| Việc | Chỗ |
|---|---|
| Icon hỗ trợ | `BattleAppKind`, `LaunchKind` (`PetBattleSettings.cs`); `PetBattle.Apps.cs`; `PetBattleAppArt.cs`; `PetDesktopAppViews.cs`; bộ chọn trong `PetWindow.BattlePrep.cs` |
| App độc | Cả **hai** danh sách `MalwareCue.Kinds` (`PetBattleData.cs`) và `MalwareIds` (`PetBattle.Book.cs`), chưa có test bắt lệch; `PetBattle.Malware2.cs`; `PetMalwareIcon.cs`; `PetWindow.CustomFight.cs`; `PetMonsterBook.cs` |
| Có cửa sổ riêng | Mẫu `PetWindow.BattleCaptcha.cs`: ẩn khi Pause, menu, fullscreen |
| Giới hạn chế độ | `newAppsFrom`, `maxMalware` |

App vẫn là icon tự vẽ, không dùng tên, logo, màu app thật.

## Chương, màn, kiểu màn

- **Chương:** thêm vào `campaign.chapters`, có `unlockAfter`. Có `skillChoice` thì có mục chọn kỹ năng. Phông mới: thêm vào `ChapterSpec.Scenes`, `PetSceneKind` trong `PetScene.cs`, mốc bản đồ ở `PetWindow.Adventure.cs`, phông trận ở `PetWindow.BattleScenery.cs`.
- **Màn:** `objective` nằm trong `StageObjectives`. Hướng dẫn đứng hình của chương mới cần duyệt theo mục Hướng dẫn phiêu lưu trong docs/APPROVED-EXCEPTIONS.md.
- **Kiểu màn mới:** mẫu giỏ cá (`basket`, `PetBattle.Basket.cs`, `PetBattleView.Basket.cs`): khóa màn, điều kiện thua riêng, HUD, kỷ lục.

## Viễn chinh, Vô tận

- **Tuyến:** `expedition.routes` dùng chung luật HP, sự kiện, di vật, cửa hàng; `zones` là vùng; kỷ lục riêng.
- **Sự kiện loại mới, di vật `effect` mới:** `PetExpedition.cs` (`RelicSpec.Effects`), bảng chọn ở `PetWindow.Expedition.cs`, file lưu expedition.json có `version`.
- **Chặng Vô tận:** `endless.eras`; `formations` riêng của chặng ghi đè pool chung, `malwareKinds` lọc app độc ngẫu nhiên (vẫn chịu `newAppsFrom`). Bỏ khóa/null giữ fallback cũ. Giới hạn 2 loại liên kết/quái chuột xét cả pool quái lẻ và thành viên đội hình trong chặng. Xem [phân bố #70](CONTENT-MODES-1.5-2026-11-07.md).
- **Đột biến:** `endless.mutations`; `effect` mới ở `PetBattle.Mutations.cs`.

## Chế độ chơi mới

Tham số `loot` và `stage` của `BattleSim`, luồng mở trận trong `PetWindow.Battle.cs`, nút ở `PetWindow.BattleHub.cs`. Quyết định rõ trước khi làm: có kỷ lục không, có tính Sổ tay và thành tựu không, có đồ rơi, app, thẻ không, Python vẫn không tham gia.

## Sau 1.5

Danh sách ý tưởng: docs/UPDATE-1.5.md → Backlog sau 1.5; docs/UPDATE-1.5.1.md (chưa chốt). Việc ngoài trận (đồ vật, mini-game) không theo file này: giữ phân vai C#/Python và các bất biến trong AGENTS.md.
