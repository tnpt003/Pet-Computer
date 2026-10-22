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
- Ngoại lệ đã duyệt (2026-10-15): Quiet không chặn chơi bóng/đèn laser do người dùng bật (Quiet chỉ chặn hành vi tự động); bật Quiet giữa chừng vẫn dừng trò chơi; app fullscreen vẫn chặn. Cooldown cho ăn 20s và cách mờ menu khi mèo chưa rảnh giữ nguyên.
- Ngoại lệ đã duyệt (2026-10-15): "đi trên cửa sổ" (mặc định bật, tắt trong Cài đặt → Mèo): mép trên cửa sổ app nhìn thấy được là mặt đất; mèo đáp khi rơi từ trên xuống, đi theo khi cửa sổ bị kéo, tự nhảy (gờ thấp) hoặc trèo cạnh (cửa sổ cao chạm sàn), rơi khi cửa sổ mất. Chỉ đọc hình chữ nhật/kiểu/z-order cửa sổ, không đọc nội dung. Ngủ trên gờ mà gờ mất thì rơi trong tư thế ngủ và vẫn ngủ. Physics vẫn ở C#; Python chỉ thêm "Climbing" vào BUSY_STATES.
- Ngoại lệ đã duyệt (2026-10-16): Battle Mode (tùy chọn, mặc định tắt, không tự bật lại khi mở app): trong trận combat C# (PetBattle.cs) sở hữu hành vi/di chuyển của mèo; chặn hành vi tự động, ngủ, away nap, AI, bóng/laser/cho ăn, cảm xúc tự động và gờ cửa sổ; drag/throw/dizzy giữ nguyên, mèo không nhận sát thương khi đang cầm/bay; Pause/menu/fullscreen đóng băng trận; kết thúc trận trả mèo về bình thường. Python không tham gia combat.
- Ngoại lệ đã duyệt (2026-10-22): kệ kỷ vật là đồ vật có physics (C#, PetShelfMotion.cs + PetWindow.Trophies.cs): người dùng cầm (quá ~6 DIPs), lắc khi cầm, ném, rơi, nảy/nhún kiểu hoạt hình; click không kéo thì kệ "boing". Rơi trúng đầu mèo đang thức (đứng trên sàn/gờ, không bị cầm/bay/trèo) thì mèo choáng ~2,2 s (sao + mắt choáng như choáng trong trận; KHÔNG phải cảm xúc Dizzy, quy tắc Dizzy khi kéo giữ nguyên), kệ nảy sang một bên; rơi trúng mèo đang ngủ thì (duyệt 2026-10-22) mèo bị đánh thức như click (waking 1,2 s + wake cooldown 30 s) và choáng. Chỉ trưng bày MỘT kỷ vật (chọn trong Cài đặt → Mèo hoặc chuột phải món đồ, mặc định món xịn nhất), vẽ 1,2× (~nửa mèo). Món đồ đứng được trên mép trên cửa sổ (dùng ledge của mèo: cần "Mèo leo lên cửa sổ", cùng màn hình với mèo; đi theo cửa sổ, rơi khi cửa sổ mất). Ném ngang trúng người mèo cũng choáng. Mèo rảnh nhìn theo khi người dùng cầm món đồ; rơi mạnh sát mèo thì mèo Ngạc nhiên. Double-click món đồ gọi mèo ra ngắm (cả khi Quiet: người dùng yêu cầu). Bóng dội khỏi món đồ. Đệm slime đang trưng bày: thỉnh thoảng mèo tự đi tới ngủ trên đệm (giấc ngủ thường, theo mọi quy tắc ngủ; không khi Quiet/wake cooldown; chỉ vẽ nâng lên, physics không đổi). Mèo thỉnh thoảng tự đẩy kệ (hành vi tự động: chặn bởi Quiet/ngủ/fullscreen/trận/gờ). Kệ nằm yên sát mép (< 24 DIPs) sau khi thả/đẩy/nảy: mèo Ngạc nhiên (không khi tự điều khiển cảm xúc hoặc ngủ) rồi kệ biến về giữa. Kệ ở lại màn hình được thả, nhớ vị trí trong preferences; Pause đóng băng kệ; kệ không còn xuyên chuột (vùng trong suốt vẫn xuyên).
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
