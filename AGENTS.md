# Desktop Pet (Windows) - Quy tắc cho AI agent
Trao đổi bằng tiếng Việt. Thư mục: C:\Project VS\Pet Computer
C# / WPF (.NET 10) + Python AI service. Nhân vật: mèo hoạt hình.

Theme chính thức: **Cozy Desktop Adventure — Một thế giới nhỏ trên bàn làm việc**. Khi sửa UI/art/motion/sound, đọc [docs/DESIGN-SYSTEM.md](docs/DESIGN-SYSTEM.md), tái dùng PetUi/style chung; không thay gameplay chỉ để hợp theme.

## QUY TRÌNH BẮT BUỘC CHO MỖI YÊU CẦU (tự động, không cần tôi nhắc)
1. ĐỌC: hai mục "Trạng thái hiện tại" và "Đang dở và việc tiếp theo" của PROGRESS.md, docs và code liên quan. Đối chiếu với code thật vì ghi chú có thể đã cũ.
2. GIẢI THÍCH: nêu ngắn gọn nguyên nhân/cách làm, danh sách file sẽ đổi, rủi ro đến hành vi hiện có. KHÔNG code, KHÔNG sửa file ở bước này.
3. CHỜ: dừng lại, hỏi tôi xác nhận. Chỉ code khi tôi trả lời "ok", "làm đi" hoặc tương tự. Nếu tôi chỉnh kế hoạch thì giải thích lại.
4. LÀM: thay đổi nhỏ, đúng phạm vi, giữ rõ trách nhiệm C#/Python.
5. KIỂM TRA: chạy build/test phù hợp. Nếu đụng behavior/input, kiểm tra: sleep lock, wake, drag, throw, emotion priority, local fallback. Không khẳng định đã kiểm tra trực quan hoặc nhiều màn hình nếu chưa làm thật.
6. GHI NHỚ: cập nhật PROGRESS.md (đã làm, kết quả test thật, việc dở, điểm cần test tay) và docs nếu hành vi đổi. Việc tôi nghĩ ra nhưng chưa làm thì thêm vào "Đang dở và việc tiếp theo".
7. BÁO CÁO: đã đổi gì, kết quả test, điểm tôi cần test thủ công.
Ngoại lệ: nếu tôi nói "làm luôn" hoặc "không cần hỏi" thì bỏ qua bước 2-3 cho yêu cầu đó.
Nếu hội thoại quá dài và bạn bắt đầu quên hoặc lặp lỗi, hãy tự đề nghị tôi mở phiên mới sau khi cập nhật PROGRESS.md.

## Đọc tiết kiệm
Không đọc cả file lớn (docs/BATTLE-DATA.md, docs/MANUAL-TEST.md, PetBattleData.cs, Issue_fixxed.md, CHANGELOG.md, docs/archive/); dùng graphify hoặc tìm kiếm rồi đọc đúng đoạn. Log test/publish chỉ đọc dòng tổng kết và FAIL. Mỗi mốc nên là một phiên chat. Quy trình và tiêu chuẩn test không giảm vì việc này.

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
- Ngoại lệ đã duyệt: "away nap" sau ≥5 phút không chạm máy. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Hành vi mèo.
- Ngoại lệ đã duyệt: menu "Làm ngay" và công tắc "Tự điều khiển cảm xúc". Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Hành vi mèo.
- Ngoại lệ đã duyệt: Quiet không chặn bóng/laser do người dùng bật. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Hành vi mèo.
- Ngoại lệ đã duyệt: "đi trên cửa sổ" (chỉ đọc hình chữ nhật/kiểu/z-order cửa sổ, không đọc nội dung; physics ở C#). Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Hành vi mèo.
- Ngoại lệ đã duyệt: Battle Mode (tùy chọn, mặc định tắt, không tự bật lại): trong trận C# sở hữu mèo, Python không tham gia; có luật riêng về cầm/ném/sát thương/choáng/Pause. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Battle Mode.
- Bổ sung đã duyệt 05/11: Slime Mẹ lao và Vua biến dị phình nổ đẩy mèo dưới sàn; dây cứu con/dòng hút nhầy cắt được. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Battle Mode; luật: docs/BOSS-TACTICS.md.
- Bổ sung đã duyệt 06/11, chỉ Slime Nén: đòn trúng mèo đang cầm/bay khi thực sự chạm (không tuột tay, không choáng); Khóa ZIP khóa đi/nhấc; mech đánh khóa 12 nhát, ghép thư mục và 6 tay kéo ở 1 HP; phase 2–3 có đẩy riêng 0 damage (bổ sung sau chơi thử 06/11). Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Battle Mode; luật: docs/ZIP-SLIME.md.
- Bổ sung #58 đã duyệt 06/11: đai Slime Nén đi từ boss và chỉ gây damage khi đầu quét chạm; mèo tự đánh Khóa ZIP đủ 5 nhát để mở, không cần quái gần. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Battle Mode.
- Bổ sung #57 đã duyệt 06/11: giấy Slime Nén bám có giới hạn quay, quét toàn thân khi bế/rơi/bounce; file mech theo làn tránh boss và không bị bom thường xóa. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Battle Mode; luật: docs/ZIP-SLIME.md.
- Ngoại lệ đã duyệt: kệ kỷ vật có physics; rơi/ném trúng mèo gây choáng Bonk, KHÔNG phải Dizzy. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Kệ kỷ vật có physics.
- Âm thanh: giữ cue phong cách hoạt hình (assets CC0 đã xử lý, không thay bằng beep hay tiếng mèo thật); auto sound có cooldown, không overlap; preview chạy dù auto bị mute, volume 0 thì im; không sửa trực tiếp buffer dùng chung.
- DPI/monitor: PerMonitorV2; đổi DPI phải đồng bộ position/velocity/drag offset và bỏ throw samples cũ; vị trí lưu theo monitor-relative, fallback primary screen.
- FPS: active ~60, sleeping ~30, paused ~10.
- Single-instance: mở EXE lần hai sẽ mở controls của mèo đang chạy.
- Ngoại lệ đã duyệt: app trong trận dùng icon GIẢ tự vẽ, không dùng tên/logo/màu app thật, không đọc hay nhận diện app thật. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → App trong trận.
- Ngoại lệ đã duyệt: "Hướng dẫn phiêu lưu" đứng hình trận nhưng KHÔNG phải Pause. Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Hướng dẫn phiêu lưu.

- Bổ sung đã duyệt: sửa sau chơi thử 1.5 (bộ app tối đa 5, slime lui xuyên hàng, bóng ma, khổng lồ). Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Sửa sau chơi thử 1.5.

## File quan trọng
README.md, docs/AI.md, docs/WINDOWS.md, src/DesktopPet/ (App.xaml.cs, PetWindow*.cs, PetModel.cs, PetRenderer.cs, PetPreferences.cs, PetSounds.cs, AI/), ai/, tests/DesktopPet.Tests/, tests/DesktopPet.UiChecks/, scripts/.

## Skill
Bốn skill quy trình: ship-milestone, cozy-ui, battle-change, run-pet. Bản chuẩn ở .claude/skills/ (Claude Code); .agents/skills/ chỉ trỏ tới đó cho Codex. Sửa nội dung ở .claude/skills/; đổi tên hoặc description thì sửa cả hai nơi.

## Lệnh (chạy từ C:\Project VS\Pet Computer)
Build:   & .\.tools\dotnet\dotnet.exe build .\src\DesktopPet\DesktopPet.csproj -c Release
Test:    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Ui
Publish: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
Smoke:   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-publish.ps1
SDK/Python local: .tools\dotnet\dotnet.exe, .tools\python\python.exe
Release: artifacts\win-x64\DesktopPet.exe (khi chia sẻ phải copy cả folder win-x64).
Preferences: %LOCALAPPDATA%\DesktopPet\preferences.json. KHÔNG ghi đè khi test.
Khi publish: đóng đúng process của project bình thường; không kill toàn bộ Python hay ứng dụng không liên quan.
