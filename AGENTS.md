# Desktop Pet (Windows) - Quy tắc cho AI agent
Trao đổi bằng tiếng Việt. Thư mục: C:\Project VS\Pet Computer
C# / WPF (.NET 10) + Python AI service. Nhân vật: mèo hoạt hình.

Theme chính thức: **Cozy Desktop Adventure — Một thế giới nhỏ trên bàn làm việc**. Khi sửa UI/art/motion/sound, đọc [docs/DESIGN-SYSTEM.md](docs/DESIGN-SYSTEM.md), tái dùng PetUi/style chung; không thay gameplay chỉ để hợp theme.

## QUY TRÌNH BẮT BUỘC CHO MỖI YÊU CẦU (tự động, không cần tôi nhắc)
1. ĐỌC: PROGRESS.md, docs và code liên quan. Đối chiếu với code thật vì ghi chú có thể đã cũ.
2. GIẢI THÍCH: nêu ngắn gọn nguyên nhân/cách làm, danh sách file sẽ đổi, rủi ro đến hành vi hiện có. KHÔNG code, KHÔNG sửa file ở bước này.
3. CHỜ: dừng lại, hỏi tôi xác nhận. Chỉ code khi tôi trả lời "ok", "làm đi" hoặc tương tự. Nếu tôi chỉnh kế hoạch thì giải thích lại.
4. LÀM: thay đổi nhỏ, đúng phạm vi, giữ rõ trách nhiệm C#/Python.
5. KIỂM TRA: chạy build/test phù hợp. Nếu đụng behavior/input, kiểm tra: sleep lock, wake, drag, throw, emotion priority, local fallback. Không khẳng định đã kiểm tra trực quan hoặc nhiều màn hình nếu chưa làm thật.
6. GHI NHỚ: cập nhật PROGRESS.md (đã làm, kết quả test thật, việc dở, điểm cần test tay) và docs nếu hành vi đổi. Việc tôi nghĩ ra nhưng chưa làm thì thêm vào "Đang dở và việc tiếp theo".
7. BÁO CÁO: đã đổi gì, kết quả test, điểm tôi cần test thủ công.
Ngoại lệ: nếu tôi nói "làm luôn" hoặc "không cần hỏi" thì bỏ qua bước 2-3 cho yêu cầu đó.
Nếu hội thoại quá dài và bạn bắt đầu quên hoặc lặp lỗi, hãy tự đề nghị tôi mở phiên mới sau khi cập nhật PROGRESS.md.

## Quy tắc commit và publish — người dùng cập nhật 2026-10-20

- Sau mỗi mốc code hoàn chỉnh, chạy build/test phù hợp, cập nhật tài liệu, chủ động commit và publish rồi chạy scripts/check-publish.ps1. Không cần hỏi lại quyền commit/publish cho công việc đã được giao. Không chỉ để bản .dirty rồi báo hoàn tất.
- Commit trước khi publish để artifact truy vết được source. Chỉ stage thay đổi thuộc mốc của mình; giữ nguyên thay đổi chưa commit không liên quan, không reset hoặc gom tất cả bằng git add . thiếu kiểm tra.
- Nếu cần thay bản đang chạy: đóng đúng process pet của project bằng cách thoát bình thường, publish/smoke rồi mở lại nếu trước đó pet đang chạy. Không kill Python/app không liên quan và không ghi đè dữ liệu người dùng.
- Nếu test/build/publish bị lỗi hoặc môi trường chặn: sửa trong phạm vi hoặc báo rõ bước còn thiếu; không tuyên bố đã phát hành thành công, không bỏ test để ép publish.
- Mốc chỉ sửa tài liệu: commit tài liệu; không cần rebuild/publish binary không đổi. Review không có thay đổi file thì không tạo commit rỗng.
- Commit là local; publish là đóng gói Windows bằng script project. Không suy ra quyền git push, tạo remote release, upload hoặc deploy ra ngoài. Báo commit hash, kết quả test/smoke và đường dẫn artifact sau mỗi mốc code.
- Quy tắc này thay các chỉ dẫn cũ cấm tự commit/publish/restart phục vụ publish trong tài liệu bàn giao. Không thay phạm vi tính năng hoặc yêu cầu xác nhận thiết kế ngoài phạm vi đã giao.

## Phân vai
- C#: window, rendering, animation, input, physics, âm thanh, tray, vòng đời Python, timeout/lỗi/fallback.
- Python: chỉ quyết định hành vi cấp cao, tính cách, mood, memory. KHÔNG di chuyển window, KHÔNG physics.
- Python tắt hoặc lỗi thì mèo vẫn chạy bằng hành vi local.
- IPC: JSON-lines qua stdin/stdout, giữ nguyên trừ khi có lý do rõ ràng.
- Chưa tích hợp LLM, voice, computer vision. Hạn chế dependency.

## Nguyên tắc
- Mở rộng project hiện có; không xây lại, không đổi framework, không đổi sang sprite (giữ artwork vector/procedural).
- Không làm mất hành vi đã có. Chỉ sửa file liên quan, không refactor ngoài yêu cầu.
- Window: transparent, borderless, always-on-top, không có taskbar button; có tray và menu chuột phải.

## Bất biến dễ bị hỏng (luôn giữ)
Ngoại lệ nào có link tới docs/APPROVED-EXCEPTIONS.md: trước khi sửa vùng đó PHẢI đọc bản đầy đủ ở file này (có hiệu lực như AGENTS.md). Duyệt thêm thì ghi bản đầy đủ vào file đó, ở đây chỉ sửa tóm tắt.
- Drag: chỉ bắt đầu khi di chuyển quá ~6 DIPs; có dangling/pendulum; thả có velocity, gravity, bounce; pickup mượt, không flicker.
- Dizzy: chỉ khi drag nhanh/mạnh vượt ngưỡng cao; swing nhẹ không gây Dizzy; có hysteresis.
- Emotion (Happy, Angry, Sad, Surprised, Sleepy, Dizzy): có ưu tiên/cooldown, không chồng trạng thái xung đột, không phá dragging.
- Sleep: dùng tư thế ngủ; khóa mọi hành vi/emotion tự động; cursor, AI, background không được đánh thức; tự thức sau SleepDurationSeconds (~10s); waking ~1.2s rồi Idle; left-click thức ngay và vẫn drag được; right-click thức ngay và mở menu; Pause đóng băng cả sleep timer; wake cooldown ~30s.
- Ngoại lệ đã duyệt (2026-10-13): "away nap" khi người dùng không chạm máy ≥5 phút: giữ sleep timer, thức khi có input hệ thống trở lại (waking 1.2s + cooldown 30s). Ngủ thường giữ nguyên quy tắc trên.
- Ngoại lệ đã duyệt (2026-10-13): menu "Làm ngay" cho động tác chạy ngay (bỏ qua cooldown/bond/xác suất/Quiet, không cắt drag/bay/sleep lock/waking); công tắc "Tự điều khiển cảm xúc" (chỉ trong phiên) tắt cảm xúc tự động + cooldown, giữ cảm xúc người dùng chọn, báo Python là Neutral. Không có Chóng mặt/Buồn ngủ bấm tay.
- Ngoại lệ đã duyệt (2026-10-15): Quiet không chặn chơi bóng/đèn laser do người dùng bật (Quiet chỉ chặn hành vi tự động); bật Quiet giữa chừng vẫn dừng trò chơi; app fullscreen vẫn chặn. Cooldown cho ăn 20s và cách mờ menu khi mèo chưa rảnh giữ nguyên.
- Ngoại lệ đã duyệt (2026-10-15): "đi trên cửa sổ" (mặc định bật, tắt trong Cài đặt → Mèo): mép trên cửa sổ app nhìn thấy được là mặt đất; mèo đáp khi rơi từ trên xuống, đi theo khi cửa sổ bị kéo, tự nhảy (gờ thấp) hoặc trèo cạnh (cửa sổ cao chạm sàn), rơi khi cửa sổ mất. Chỉ đọc hình chữ nhật/kiểu/z-order cửa sổ, không đọc nội dung. Ngủ trên gờ mà gờ mất thì rơi trong tư thế ngủ và vẫn ngủ. Physics vẫn ở C#; Python chỉ thêm "Climbing" vào BUSY_STATES.
- Ngoại lệ đã duyệt (2026-10-16, bổ sung 23–24/10, 26/10): Battle Mode (tùy chọn, mặc định tắt, không tự bật lại): trong trận C# (PetBattle.cs) sở hữu hành vi/di chuyển của mèo, chặn hành vi/cảm xúc tự động, ngủ, away nap, AI, đồ chơi, gờ cửa sổ; drag/throw giữ nguyên, mèo không nhận sát thương khi đang cầm/bay; mèo đang choáng không nhấc được, lắc trong trận không gây Dizzy; trận dừng (Pause/menu/chọn thẻ/túi đồ) đóng băng cả mèo và không nhấc được; (1.5) mèo đang cầm chỉ đánh được quái trên không (slime bay), đạn dính của slime bay (bay khắp màn hình) gây sát thương cả khi mèo đang cầm, mèo đang bay không tự đánh, cú ném chỉ tác động quái có tương tác ném riêng; con trỏ tương tác với slime trong khung trận; Python không tham gia combat. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Battle Mode.
- Ngoại lệ đã duyệt (2026-10-22): kệ kỷ vật là đồ vật có physics (C#, PetKeepsakeMotion.cs + PetWindow.Keepsake.cs): cầm/lắc/ném/nảy; rơi hoặc ném trúng mèo thì choáng Bonk ~2,2 s (KHÔNG phải Dizzy; mèo ngủ thì bị đánh thức như click rồi choáng); đứng trên gờ cửa sổ; chỉ đệm slime có ngủ/nảy bạt nhún; mèo tự đẩy kệ là hành vi tự động; Pause đóng băng kệ. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Kệ kỷ vật có physics.
- Âm thanh: giữ cue phong cách hoạt hình (assets CC0 đã xử lý, không thay bằng beep hay tiếng mèo thật); auto sound có cooldown, không overlap; preview chạy dù auto bị mute, volume 0 thì im; không sửa trực tiếp buffer dùng chung.
- DPI/monitor: PerMonitorV2; đổi DPI phải đồng bộ position/velocity/drag offset và bỏ throw samples cũ; vị trí lưu theo monitor-relative, fallback primary screen.
- FPS: active ~60, sleeping ~30, paused ~10.
- Single-instance: mở EXE lần hai sẽ mở controls của mèo đang chạy.
- Ngoại lệ đã duyệt (2026-10-23, bổ sung 24/10): app trong trận (không trận tự do): icon app GIẢ tự vẽ (không dùng tên/logo/màu app thật, không đọc hay nhận diện app thật), mỗi icon một cửa sổ nhỏ topmost; virus và app độc (vòng loading, Cập nhật bắt buộc, virus.exe); tắt được bằng ô "App trong trận"; logic C# PetBattle.Apps.cs, số liệu battle.json `apps`; Python không tham gia. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → App trong trận.
- Ngoại lệ đã duyệt (2026-10-24): "Hướng dẫn phiêu lưu" (chương 1 Phiêu lưu, từ 1.5 cả chương 2; C# PetWindow.BattleGuide.cs): trận ĐỨNG HÌNH nhưng KHÔNG phải Pause, vẫn nhấc/kéo mèo, bấm kỹ năng/icon/app độc được; Pause/menu/fullscreen ẩn hướng dẫn. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Hướng dẫn phiêu lưu.

## File quan trọng
README.md, docs/AI.md, docs/WINDOWS.md, src/DesktopPet/ (App.xaml.cs, PetWindow*.cs, PetModel.cs, PetRenderer.cs, PetPreferences.cs, PetSounds.cs, AI/), ai/, tests/DesktopPet.Tests/, tests/DesktopPet.UiChecks/, scripts/.

## Lệnh (chạy từ C:\Project VS\Pet Computer)
Build:   & .\.tools\dotnet\dotnet.exe build .\src\DesktopPet\DesktopPet.csproj -c Release
Test:    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Ui
Publish: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
Smoke:   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-publish.ps1
SDK/Python local: .tools\dotnet\dotnet.exe, .tools\python\python.exe
Release: artifacts\win-x64\DesktopPet.exe (khi chia sẻ phải copy cả folder win-x64).
Preferences: %LOCALAPPDATA%\DesktopPet\preferences.json. KHÔNG ghi đè khi test.
Khi publish: đóng đúng process của project bình thường; không kill toàn bộ Python hay ứng dụng không liên quan.
