# Tiến độ Desktop Pet

Cập nhật: 2026-10-26 (Asia/Saigon). Lịch sử từng đợt: docs/archive/PROGRESS-2026-10.md (đến 20/10), docs/archive/PROGRESS-2026-10-21-to-26.md (21–26/10, đến bản 1.4.4).
Project: C:\Project VS\Pet Computer. Đã dùng git (nhánh main), mỗi đợt một commit; tác giả cục bộ tnpt003.
GitHub: https://github.com/tnpt003/Pet-Computer (origin/main). Không commit artifacts/, .tools/, bin/obj, *.log.
Quy ước file này: chỉ giữ trạng thái hiện tại, các đợt đang làm, việc dở và test tay. Sau mỗi lần release, chuyển các mục đợt đã xong sang docs/archive/ (giữ file dưới ~20 KB).

## Trạng thái hiện tại
- Bản phát hành mới nhất: **1.4.4** (2026-10-26, tag v1.4.4, commit e8f97d4). GitHub Actions success, Release "Desktop Pet 1.4.4" là Latest, `releases/latest/download/releases.win.json` trả về 1.4.4. Bản trên máy: artifacts\win-x64 (1.4.4+e8f97d4), ZIP artifacts\DesktopPet-1.4.4-e8f97d4-win-x64.zip.
- Test thật lần cuối (2026-10-26, trước khi gắn tag): scripts/test.ps1 -Ui exit 0: Python đạt, C# 1538, WPF 1027/1027; check-publish 3/3; pack.ps1 trên máy đạt (delta 1.4.3 → 1.4.4).
- Nội dung chính đã có: mèo vector (cảm xúc, ngủ/away nap, kéo/ném/Dizzy, đi trên cửa sổ, đồ chơi, kệ kỷ vật có physics, AI Python có fallback local), Battle Mode (Phiêu lưu chương 1 + Hướng dẫn phiêu lưu, Viễn chinh, Vô tận, trận tự do, app trong trận + app độc, thẻ/di vật/cộng hưởng), giao diện Cozy Desktop Adventure (tranh minh họa, icon), vi/en/ja, bộ cài Velopack tự cập nhật.
- Ngoại lệ đã duyệt dạng đầy đủ: docs/APPROVED-EXCEPTIONS.md (AGENTS.md chỉ còn tóm tắt).

## Đang dở và việc tiếp theo
- Chưa làm, chờ người dùng: việc E của visual combat (slime nhún khi đáp/đi, mép dưới có khối); trang "Sổ tay" trong Sảnh (đề xuất, chưa duyệt).
- Tiếng Nhật: người dùng xác nhận bản dịch ổn (24/10). Trạng thái AI (`LocalizeAiStatus`) vẫn tiếng Anh như debug. Nút/hộp thoại của Velopack không dịch được.
- App trong trận: chỉnh số liệu `apps` trong battle.json sau khi chơi thật. Ý tưởng chưa duyệt: đổi vùng lưới icon trong Cài đặt, cửa sổ app thật làm bục đứng (cần duyệt ngoại lệ đọc cửa sổ).
- Cài đặt/cập nhật chưa thử thật: khởi động lại máy, gỡ có xóa dữ liệu, thứ tự thoát trước khi gỡ (01ded63). Có thể sau: bộ cài MSI (`vpk pack --msi`) nếu muốn chọn thư mục cài; ký code (SignPath Foundation) khi muốn chia sẻ rộng, SmartScreen hiện còn cảnh báo.
- Vô tận: chỉnh `endless` trong battle.json sau khi chơi thật (đợt nào bắt đầu khó, Vua đợt 10/20 có quá mạnh không vì threat cộng dồn). Có thể làm sau: mô phỏng cân bằng như AdventureBalanceTests; phần thưởng mốc đợt (đã nêu, người dùng chưa chọn).
- Cân bằng (tùy người dùng, đã chơi thật và thấy ổn): nếu muốn chỉnh thì sửa battle.json — `kindGacha.weights`, giá viên vàng 12%, `rerollCost` 8%, `winHeal`/`stepThreat`/`retreatCost`/`restHeal`, số liệu búa, buff nền quái (hướng đã đo: bỏ buff nền giữ elite), `rescueGraceMinLiftSeconds`. Chiến thuật quái a–d mỗi cái tắt được bằng battle.json.
- Adventure: Mũ slime hơi nhỏ trên đầu mèo (chỉnh nếu người dùng muốn). Thẻ trạng thái: có thể thêm "câu nói/ý nghĩ gần nhất" nếu người dùng muốn.
- Ghi chú bảo trì: câu nhắc nghỉ dự phòng trong C# (PetBreaks, vi/en/ja) trùng 3 câu `break` đầu của ai/lines.json — sửa giọng văn thì sửa cả hai. Câu không phải câu chào do Python chọn đúng lúc mèo vừa ngủ vẫn bị bỏ. Fullscreen chỉ xét màn hình có mèo; app bị nhận nhầm thì xem log "Fullscreen app detected". Nếu ném vẫn giật: cân nhắc vòng lặp active theo CompositionTarget.Rendering. Bảng Tết/Trung thu tới 2050 (scratch script thuật toán âm lịch không nằm trong repo; nối tiếp thì dùng lại thuật toán Hồ Ngọc Đức UTC+7). Build có cảnh báo CS0649 `PetWindow.cursorProbe` (chỉ test gán qua reflection, vô hại).
- Ngày kỷ niệm đầu tiên của người dùng là 2026-10-13 → lần đầu thấy được 2027-10-13.
- Đã bỏ theo người dùng (2026-10-22): cho ăn bỏ cooldown/xếp hàng và mèo no từ chối ăn; đo RAM chạy dài; phím tắt toàn cục cho Làm ngay; dạy mèo nói (cách 2) không làm trong đợt này. Bỏ (2026-10-24, trước tạm hoãn từ 16/10): tùy chọn away nap 5/10/15 phút; tốc độ đói/chán, khoảng cách bong bóng trong Behavior tuning. LLM trò chuyện chỉ khi người dùng đổi quy tắc AGENTS.md.
- **Hướng mở rộng mới (người dùng 23/10): mèo nói tiếng người** — đọc to câu trong bong bóng (vi/en/ja). Chưa duyệt thiết kế; cần đổi quy tắc AGENTS.md "chưa tích hợp voice". Các cách A líu lo hoạt hình / B giọng Windows / C tạo sẵn / D neural offline, quy tắc im lặng (Quiet, fullscreen, ngủ, trận, Pause), mốc V1–V3 và câu hỏi cho người dùng: docs/NEXT-STEPS.md mục "Hướng mở rộng mới: mèo nói tiếng người".
- Đề xuất quy trình (2026-10-26, người dùng đồng ý dọn tài liệu): mỗi bản chốt danh sách tính năng trước; mỗi ý tưởng kèm tiêu chí "xong"; test tay chia smoke ngắn mỗi release + mục mới của bản; dọn PROGRESS sau release. Mẫu prompt theo quy trình này: docs/PROMPTS.md. Chưa làm: danh sách smoke ngắn trong MANUAL-TEST.md.
- Backlog khác: docs/NEXT-STEPS.md → "Backlog".

## Cần test thủ công
- 2026-10-22: người dùng đã test tay toàn bộ docs/MANUAL-TEST.md (đến bản b5fa2eb), passed. 2026-10-24: người dùng đã test tay các mục thêm sau 22/10 (tới 1.4.3). Mục mới sau ngày này thêm vào cuối MANUAL-TEST.md với ô `[ ]`.
- Còn chờ test tay (mục cuối docs/MANUAL-TEST.md, chưa xem trong trận thật hay DPI thật): Đồng hồ cát chảy, Tắt app trong trận, App độc (vòng loading, cập nhật bắt buộc, virus.exe), Cân bằng trận 2026-10-24 (mốc 1–3, mốc 4), Hướng dẫn phiêu lưu, Visual Polish 2026-10-25, Minh họa lớn 2026-10-25, Icon toàn app 2026-10-26, Visual combat 2026-10-26.
- Release 1.4.4: máy đã cài 1.4.3 nhận cập nhật qua khay (Cập nhật lên bản 1.4.4); trang Release có ghi chú vi/en/ja.
