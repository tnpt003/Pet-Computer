---
name: battle-change
description: Sửa Battle Mode của Desktop Pet mà không phá luật đã duyệt hay cân bằng - quái, slime, boss, vũ khí, kỹ năng, thẻ, app trong trận, Phiêu lưu, Viễn chinh, Vô tận, số liệu battle.json. Dùng cả khi người dùng chỉ góp ý sau khi chơi thử như "quái này quá mạnh", "đánh không trúng", "khó quá", "bay xuyên qua", hoặc khi đụng PetBattle*.cs, PetBattleData.cs, PetExpedition.cs, PetEndless.cs.
---

# Sửa Battle Mode

Lỗi đắt nhất ở đây là sửa một chỗ làm lệch chỗ khác: một số liệu đổi cân bằng của cả chương, một luật cầm/ném phá ngoại lệ đã duyệt. Các bước dưới đây để phát hiện sớm việc đó.

## 1. Xác định loại thay đổi
- **Chỉ số liệu:** sửa `src/DesktopPet/Assets/battle.json`, ưu tiên cách này nếu đạt được yêu cầu.
- **Luật hoặc hành vi:** sửa code C#. Combat thuộc C#; Python không tham gia.
- **Có đụng mèo đang cầm/bay/ném, sát thương lên mèo, choáng, Pause, cửa sổ app:** đọc `docs/APPROVED-EXCEPTIONS.md` → Battle Mode trước. Hành vi ngoài các ngoại lệ đó cần người dùng duyệt, kể cả khi họ giao việc gần giống.

## 2. Đọc đúng đoạn
`docs/BATTLE-DATA.md` rất lớn: tìm heading `## ` theo tên khóa hoặc tên quái rồi chỉ đọc mục đó. Mục "Ánh xạ sang code" chỉ ra kiểu dữ liệu tương ứng. Thêm quái, kỹ năng, app, chương hay chế độ mới: đọc `docs/EXTENDING.md` trước. Luật hiện tại của 1.5 ở `docs/UPDATE-1.5.md`; lịch sử đợt C ở `docs/GOPY-C-2026-11-04.md`; số đo cân bằng ở `docs/BALANCE-1.5.md`.

## 3. Sửa
- Thêm khóa mới thì cập nhật cả kiểu dữ liệu, `Validate` và fallback C#. `BattleData` bỏ cả file nếu có một lỗi, và `BattleDataTests` sẽ fail nếu `battle.json` lệch số mặc định trong C#.
- Đổi màn nhiều thì tăng `rulesVersion` của màn hoặc chế độ đó, để kỷ lục cũ không bị so với luật mới.
- Giữ logic kiểm thử được, tách khỏi phần vẽ; dùng seed của trận thay vì ngẫu nhiên riêng.
- Mọi thứ mới phải dừng khi Pause và chịu được mèo đang cầm, đang bay, đang choáng.
- Tên và mô tả mới cần `...En`, bản dịch ja, và mục Sổ tay quái nếu là quái mới (skill `cozy-ui` có bước i18n).

## 4. Kiểm tra
- Lỗi người dùng báo: viết test tái hiện trước khi sửa nếu làm được.
- Khi đang sửa, chỉ chạy bộ liên quan: `PET_TEST_SUITES=Tên1,Tên2`.
- Đổi số liệu hoặc luật ảnh hưởng độ khó: so bảng mô phỏng `AdventureBalanceTests` trước và sau, báo những dòng đổi và màn sát ngưỡng. Đây là mô phỏng, không phải chơi thật.
- Muốn thử trận tùy chỉnh: dùng `docs/examples/battle-m*.json` trong thư mục dữ liệu riêng (skill `run-pet`), không ghi vào `%LOCALAPPDATA%\DesktopPet`.
- Trận đông quái hoặc thêm vẽ nặng: đo `scripts/perf.ps1 -Battle crowd-late`.

## 5. Khép việc
Cập nhật mục tương ứng trong `docs/BATTLE-DATA.md` và điểm test tay trong `docs/MANUAL-TEST.md`. Khi xong, dùng skill `ship-milestone`.
