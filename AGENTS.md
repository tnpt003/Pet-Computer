# Desktop Pet (Windows) - Quy tắc cho AI agent
Trao đổi bằng tiếng Việt. Thư mục: C:\Project VS\Pet Computer
C# / WPF (.NET 10) + Python AI service. Nhân vật: mèo hoạt hình.

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
- Drag: chỉ bắt đầu khi di chuyển quá ~6 DIPs; có dangling/pendulum; thả có velocity, gravity, bounce; pickup mượt, không flicker.
- Dizzy: chỉ khi drag nhanh/mạnh vượt ngưỡng cao; swing nhẹ không gây Dizzy; có hysteresis.
- Emotion (Happy, Angry, Sad, Surprised, Sleepy, Dizzy): có ưu tiên/cooldown, không chồng trạng thái xung đột, không phá dragging.
- Sleep: dùng tư thế ngủ; khóa mọi hành vi/emotion tự động; cursor, AI, background không được đánh thức; tự thức sau SleepDurationSeconds (~10s); waking ~1.2s rồi Idle; left-click thức ngay và vẫn drag được; right-click thức ngay và mở menu; Pause đóng băng cả sleep timer; wake cooldown ~30s.
- Ngoại lệ đã duyệt (2026-10-13): "away nap" khi người dùng không chạm máy ≥5 phút: giữ sleep timer, thức khi có input hệ thống trở lại (waking 1.2s + cooldown 30s). Ngủ thường giữ nguyên quy tắc trên.
- Ngoại lệ đã duyệt (2026-10-13): menu "Làm ngay" cho động tác chạy ngay (bỏ qua cooldown/bond/xác suất/Quiet, không cắt drag/bay/sleep lock/waking); công tắc "Tự điều khiển cảm xúc" (chỉ trong phiên) tắt cảm xúc tự động + cooldown, giữ cảm xúc người dùng chọn, báo Python là Neutral. Không có Chóng mặt/Buồn ngủ bấm tay.
- Âm thanh: giữ cue phong cách hoạt hình (assets CC0 đã xử lý, không thay bằng beep hay tiếng mèo thật); auto sound có cooldown, không overlap; preview chạy dù auto bị mute, volume 0 thì im; không sửa trực tiếp buffer dùng chung.
- DPI/monitor: PerMonitorV2; đổi DPI phải đồng bộ position/velocity/drag offset và bỏ throw samples cũ; vị trí lưu theo monitor-relative, fallback primary screen.
- FPS: active ~60, sleeping ~30, paused ~10.
- Single-instance: mở EXE lần hai sẽ mở controls của mèo đang chạy.

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
