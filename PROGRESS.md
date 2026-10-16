# Tiến độ Desktop Pet

Cập nhật: 2026-10-15 (Asia/Saigon).
Project: C:\Project VS\Pet Computer. Đã dùng git (nhánh main), mỗi đợt một commit; tác giả cục bộ tnpt003.
GitHub: https://github.com/tnpt003/Pet-Computer (origin/main). Không commit artifacts/, .tools/, bin/obj, *.log.

## Kết quả test gần nhất (2026-10-15, mèo đi trên cửa sổ)
- scripts/test.ps1 -Ui: 49 Python tests, 393 C# checks, 186 WPF checks đạt (exit 0). Publish + check-publish đạt (3/3). Đã đóng (WM_CLOSE đúng process artifacts\win-x64) và mở lại mèo của người dùng.
- So pixel: 156 ảnh render cũ giống hệt; tư thế trèo (2 hướng × 10 bộ lông × 8 phụ kiện) qua check mép; đã xem ảnh tư thế trèo.
- Chạy thật bản publish với PET_DATA_DIRECTORY riêng ~70 s: không lỗi, CPU ~8.8% một nhân, RAM 168 MB (lần đo trước ~6%, 200 MB; điều kiện khác nhau nên chưa kết luận). Lúc đó người dùng đang mở app phóng to (không phải gờ) nên CHƯA thấy mèo trèo/đứng trên cửa sổ thật. Ảnh chụp màn hình lúc đó đã xóa ngay (dính nội dung riêng).
- Chưa xem trực tiếp: nhảy/trèo/đứng/đi theo cửa sổ thật; Chrome/VS Code/Electron (thanh tiêu đề tự vẽ) có lệch vài px không; nhiều màn hình/DPI.

## Kết quả test đợt thêm bộ lông/phụ kiện + xem trước (2026-10-15)
- scripts/test.ps1 -Ui: 49 Python tests, 364 C# checks, 177 WPF checks đạt (exit 0). Publish + check-publish đạt (3/3). Đã đóng (WM_CLOSE đúng process artifacts\win-x64) và mở lại mèo của người dùng.
- So pixel: 156 ảnh render bộ lông/phụ kiện cũ (6×4 ở 6 tư thế + ảnh đơn) giống hệt bản trước. Check mép tự động đạt cho 10×8 ở mọi tư thế (gồm kéo có xoay).
- Đã xem ảnh: 4 bộ lông mới, 4 phụ kiện mới, ngủ (Xiêm + kính), kéo (tuxedo + vương miện), vui, quay trái; ô xem trước trong tab Mèo không bị cắt.
- Chưa xem trực tiếp: chuyển động ô xem trước trên máy thật; phụ kiện mới khi lăn ngửa/đuổi đuôi/nhồi bột (chỉ test mép ở 6 tư thế).

## Kết quả test đợt sắp xếp lại menu + cửa sổ cài đặt dạng tab (2026-10-15)
- scripts/test.ps1 -Ui: 49 Python tests, 364 C# checks, 175 WPF checks đạt (exit 0). Một lần chạy UiChecks lẻ trước đó có 3 check kéo thả FAIL (dùng con trỏ thật, như lần 2026-10-13); chạy lại test.ps1 2 lần đều đạt.
- Publish + check-publish đạt (3/3). Đã đóng (WM_CLOSE đúng process artifacts\win-x64) và mở lại mèo của người dùng.
- Đã xem ảnh render (PET_RENDER_REVIEW, file settings-{en,vi}-{0..3}.png, menu-{en,vi}.png): 4 tab cả 2 ngôn ngữ không bị cắt chữ; menu có icon, vạch ngăn đúng nhóm.
- Chưa xem trực tiếp: menu thật trên màn hình (DPI khác, menu con), cửa sổ cài đặt thật, menu khay.

## Kết quả test đợt sửa 3 lỗi từ review của Codex (2026-10-15)
- scripts/test.ps1 -Ui: 49 Python tests, 364 C# checks, 172 WPF checks đạt (exit 0). Publish + check-publish đạt (3/3). Đã đóng (WM_CLOSE đúng process) và mở lại mèo của người dùng.
- Đã kiểm tra test mới bắt được lỗi: chạy UiChecks trên src cũ (git stash) → 3 FAIL (thức dậy kéo dài 2.62s; câu ngôn ngữ cũ vẫn hiện); src mới đạt hết.
- Chưa xem trực tiếp: lời phản ứng/bong bóng trong Quiet khi dùng thật.

## Kết quả test đợt Quiet không chặn bóng/laser (2026-10-15)
- scripts/test.ps1 -Ui: 49 Python tests, 359 C# checks, 169 WPF checks đạt (exit 0). Publish + check-publish đạt (3/3). Đã đóng (WM_CLOSE đúng process) và mở lại mèo của người dùng.
- Chưa xem trực tiếp: bật Quiet rồi chơi bóng/laser thật.

## Kết quả test đợt chơi đèn laser (2026-10-15)
- scripts/test.ps1 -Ui: 49 Python tests, 359 C# checks, 167 WPF checks đạt (exit 0). Publish + check-publish đạt (3/3). Đã đóng (WM_CLOSE đúng process artifacts\win-x64) và mở lại mèo của người dùng.
- Test không di chuyển con trỏ thật: logic đuổi/rình/vồ test bằng LaserChase (C#); WPF test vòng đời (chấm click xuyên, vồ giữ trò chơi khi đang bay, click/ném/ngủ/hết 60s/Quiet).
- Chưa xem trực tiếp: chuyển động đuổi/vồ thật, chấm đỏ đi theo con trỏ, không tính vuốt khi rê qua mèo lúc chơi.

## Kết quả test đợt một ngôn ngữ chung cho cả app (2026-10-13)
- scripts/test.ps1 -Ui: 49 Python tests, 347 C# checks, 158 WPF checks đạt (exit 0). Lần chạy đầu có 3 check kéo thả FAIL (dùng con trỏ thật, có thể do chuột bị di chuyển lúc test); chạy lại 2 lần đều đạt.
- Publish đạt. check-publish FAIL 2 lần vì người dùng đang xem YouTube fullscreen trên màn hình chính: mèo vào chế độ yên lặng nên chưa xin quyết định AI, Python chưa khởi động trong 15s (log "Fullscreen app detected"). Đúng hành vi, không phải lỗi đợt này. Đã kiểm tra Python đóng gói (3.13) import được speech/service mới. 2026-10-15: chạy lại check-publish khi không có app fullscreen: đạt cả 3 bước (bundled .NET + Python, mở lần hai, đóng sạch). Đã push 1e7255d, dacf458, 6fa93d5.
- Đã đóng (WM_CLOSE đúng process artifacts\win-x64) và mở lại mèo của người dùng. Preferences của người dùng đang lưu Language = "vi" nên app hiện toàn tiếng Việt; chọn English trong cài đặt để chuyển (không tự ghi đè preferences).

## Kết quả test đợt menu Làm ngay + tự điều khiển cảm xúc (2026-10-13)
- scripts/test.ps1 -Ui: 48 Python tests, 347 C# checks, 155 WPF checks đạt. Publish + check-publish đạt. Đã đóng (WM_CLOSE đúng process artifacts\win-x64) và mở lại mèo của người dùng.
- Test WPF bắt được lỗi thật trong lúc làm: bấm Làm ngay khi mèo đang thức dậy làm behavior.Reset() kéo dài waking 1.2s → 2s; đã sửa (chỉ hủy hoạt động khi lệnh thực sự bắt đầu).
- Chưa xem trực tiếp: menu thật, cảm giác các động tác/cảm xúc bấm tay.

## Kết quả test đợt nhỏ: quả bóng + câu chào qua giấc ngủ (2026-10-13)
- scripts/test.ps1 -Ui: 48 Python tests, 324 C# checks, 148 WPF checks đạt. Publish + check-publish đạt. Đã đóng (WM_CLOSE đúng process artifacts\win-x64) và mở lại mèo của người dùng.
- Chưa xem trực tiếp: quả bóng còn giật không; câu chào sau khi mèo ngủ ngay lúc mở (khó tái hiện, test bằng check tự động).

## Kết quả test đợt 4+5+7: câu chờ, động tác mới, nhật ký (2026-10-13)
- scripts/test.ps1 -Ui: 48 Python tests, 317 C# checks, 146 WPF checks đạt (exit 0). Publish + check-publish đạt. Python mới chạy trên BẢN SAO ai-memory.json thật: giữ needs/habits, thêm first_seen/totals/bond_history, có diary. Đã đóng (WM_CLOSE) và mở lại mèo của người dùng.
- So pixel với lần render trước đợt: 41 ảnh trạng thái cũ + 151 ảnh bộ lông/phụ kiện giống hệt (trừ 2 ảnh bong bóng chữ vốn không cố định). Mọi tư thế cũ không đổi; chỉ biểu cảm Giận đổi có chủ đích (tai cụp, đuôi quất).
- Đã xem ảnh 5 động tác × 13 khung × 2 hướng × có/không mũ (Calico): không bị cắt (có check mép tự động); khung đầu của mỗi động tác giống hệt tư thế đứng (vào mượt). Sửa trong lúc làm: đuổi đuôi/lăn ngửa ban đầu ghi đè độ nhún gây giật khung đầu (test bắt được); cào móng chuyển sang phía không có đuôi cho dễ nhìn.
- Đã xem ảnh cửa sổ nhật ký (bản English, dữ liệu mẫu): chữ, biểu đồ bond 14 ngày, biểu đồ 24 giờ hiển thị đúng.
- Chưa xem trực tiếp chuyển động thật (nhồi bột khi vuốt, lăn ngửa khi bond ≥ 60) và chưa xem nhật ký với dữ liệu thật.

## Kết quả test đợt bộ lông/phụ kiện + nhắc nghỉ + ngữ cảnh máy (2026-10-13)
- scripts/test.ps1 -Ui: 45 Python tests, 291 C# checks (test.ps1 có thêm check tích hợp Python), 132 WPF checks đạt. Publish + check-publish đạt.
- So pixel: chạy UiChecks của commit trước (worktree tạm) và bản mới với PET_RENDER_REVIEW: 39/41 ảnh giống hệt; 2 ảnh khác là ảnh bong bóng chữ, và chạy bản cũ 2 lần cũng khác nhau (opacity/đuôi phụ thuộc thời gian thật), nên không phải lỗi renderer. Mèo cam mặc định giống pixel.
- Đã xem ảnh render 6 bộ lông × 4 phụ kiện ở các tư thế đứng, ngủ, vui, bay, kéo (pickup có xoay), quay trái: không bị cắt, mũ/nơ theo đầu. Có check tự động cho mép trong suốt.
- Chưa test thật: phát hiện fullscreen với game/video thật, away nap khi rời máy thật 5 phút, nhắc nghỉ sau 30–60 phút thật. Logic đã test bằng probe giả trong UiChecks.
- Đã đóng mèo của người dùng (WM_CLOSE đúng process artifacts\win-x64) để publish rồi mở lại bản mới với dữ liệu bình thường.

## Kết quả test đợt mèo biết nói + tự học (2026-10-10)
- scripts/test.ps1 -Ui: 259 C# checks, 119 WPF checks, 41 Python tests đạt. Publish + check-publish đạt.
- Đã xem ảnh bong bóng chữ (artifacts/speech-review/speech-vi.png, speech-en-long.png): dấu tiếng Việt đúng, xuống dòng, đuôi chỉ về mèo.
- Chạy thật bản publish với PET_DATA_DIRECTORY riêng: Python thật chọn câu, C# hiện bong bóng trên đầu mèo (ảnh live-hello.png: "Ngáp~ Mochi ngủ ngon ghê"). Lần đó câu chào bị lỡ vì mèo tình cờ đi ngủ ngay lúc mở (đúng quy tắc không nói khi ngủ).
- Đã đóng mèo của người dùng (WM_CLOSE) để publish rồi mở lại bản mới với dữ liệu bình thường.

## Kết quả test đợt AI nhu cầu (2026-10-09)
- scripts/test.ps1 -Ui: 238 C# checks, 114 WPF checks, 31 Python tests đạt. Publish + check-publish đạt.
- Đã xem ảnh render 4 bong bóng (artifacts/thought-review): rõ, không bị cắt. Chưa quan sát nhịp bong bóng/nhu cầu khi chạy lâu thật.
- Chạy service mới trên BẢN SAO ai-memory.json thật: giữ energy/decisions, thêm key needs mặc định. File gốc không bị đụng.
- Đã đóng mèo đang chạy (WM_CLOSE, đúng process artifacts\win-x64) để publish rồi mở lại bản mới.

## Kết quả test đợt trước (2026-10-09)
- scripts/test.ps1 -Ui: 211 C# checks, 110 WPF checks, 21 Python tests đạt. Test giờ chạy hết rồi báo danh sách FAIL ở cuối (không dừng ở lỗi đầu).
- Publish thành công (version 1.0.0) + zip artifacts/DesktopPet-1.0.0-win-x64.zip. check-publish.ps1 đạt (bundled runtime, mở lần hai, đóng sạch). Log của bản publish được ghi vào smoke-data/<run>/logs, không đụng %LOCALAPPDATA%.
- Refactor renderer: so 1569 frame render (mọi action × hướng × emotion × thời điểm, pickup, treat, play) giống hệt từng pixel với commit trước (công cụ tạm ở scratchpad, không nằm trong repo).
- Đo nhịp timer trên máy dev: DispatcherTimer 16 ms chỉ đạt ~37 FPS (khoảng 16/31 ms xen kẽ); cách mới đo được ~58 FPS, p90 ~20 ms (đo bằng script probe, không phải trong app thật).
- Lịch sử: 201 C#/107 WPF/20 Python (motion, 2026-10-08); 181/101/20 (features). Không dùng số liệu cũ thay kết quả hiện tại.

## Đã làm gần đây
- 2026-10-16 (người dùng chọn: Giao diện, Review Codex, Smoke test; 1b giữ cửa sổ riêng chỉ làm lại bố cục; tạm không làm "tinh chỉnh sau khi dùng thật"):
  - Smoke test: biến PET_CONTEXT_PROBE=off (PetWindow.Context.cs) làm contextProbe luôn trả (không fullscreen, idle 0); check-publish.ps1 đặt biến này. Chưa thử thật lúc đang mở app fullscreen.
  - Refactor điều kiện bắt đầu: PetActionRules.IsInFlight / HoldsBody / IsSettled thay danh sách lặp ở CanStartManual, CanPlay, PetSpeech.CanShow, PetThought.CanShow, điều kiện away nap. Hành vi giữ nguyên; CombinationTests thêm check so với điều kiện cũ cho mọi action × grounded × ngủ. Còn 2 chỗ tương tự chưa gom (PetBehavior dòng ~276, PetEmotions busy) vì danh sách khác nhau.
  - Bố cục: PetUi thêm Card/SectionTitle/ButtonBar/Ink. Tinh chỉnh hành vi: 3 thẻ (Tính cách, Đòi chú ý, Năng lượng mỗi giây), mỗi dòng tên + ô số + slider + gợi ý; thanh dưới: link Khôi phục mặc định, Hủy, Lưu (logic lưu/khôi phục/khởi động lại Python giữ nguyên). Nhật ký: tiêu đề + số ngày, 4 ô số (vuốt, treat, chơi bóng, thân thiết), thẻ biểu đồ thân thiết, giờ hay chơi, điều đã học; rộng 420. UiChecks tìm control qua cây logic (Descendants) và xuất tuning-window.png khi review.
  - Menu chuột phải: PetMenuStyle.cs (XAML parse lúc chạy, không thêm file build): panel trắng bo 8px, bóng mềm ở lớp riêng (chữ vẫn ClearType), hàng hover bo góc, cột icon cố định, dấu tick khi mục được chọn không có icon, nền xanh nhạt sau icon của mục được chọn, mũi tên menu con, mục bị mờ 45%. Menu khay (WinForms) không đổi. UiChecks: check style + xuất context-menu.png.
  - scripts/perf.ps1: lấy mẫu CPU/RAM/handle/thread của mèo và Python mỗi giây ra artifacts/perf/*.csv + tóm tắt; gắn chỉ đọc vào mèo đang chạy, nếu không có thì mở bản publish với dữ liệu riêng rồi đóng.
  - Đo thật (máy dev, 1 màn hình): mèo đang chạy ~7 giờ, 3 phút: CPU TB 0.94% p95 3.98%, private 276–296 MB, handles 675→683. Bản mới mở, 2 phút: CPU TB 0.7% p95 2.46%, private 118→135 MB. Python ~0% CPU, 11 MB. Chênh RAM ~120 MB vs ~290 MB sau nhiều giờ: cần chạy dài (perf.ps1 -Minutes 120+) để biết có rò bộ nhớ thật hay chỉ là GC.
  - Test thật: test.ps1 -Ui: 49 Python, 394 C#, 188 WPF đạt. Publish + check-publish.ps1 đạt; đã mở lại mèo bằng bản mới.
  - Cần test tay: menu chuột phải (hover, menu con mở/đóng, phím mũi tên/Esc, tick Yên lặng/Âm thanh/cảm xúc, mục mờ khi mèo chưa rảnh, gần mép màn hình); cửa sổ Tinh chỉnh và Nhật ký với dữ liệu thật; DPI 125/150% và nhiều màn hình chưa kiểm tra.
- 2026-10-15 (tư thế trèo đẹp hơn, người dùng duyệt sau khi đánh giá ảnh chụp: trước đó nhìn như mèo đứng lơ lửng vẫy tay):
  - PetRenderer.cs (chỉ khi Climbing): thân kéo dài (ScaleX 0.95, ScaleY 1.06), nghiêng 10° và lùi 8 DIPs để đầu chỉ chạm mép cửa sổ; 2 chân trước ở trên đầu, bám qua mép, vươn luân phiên, có 3 vạch móng; chân sau dựng đứng đạp vào cạnh cửa sổ luân phiên (không vẽ bàn chân đứng); đuôi rủ xuống phía ngoài, đung đưa nhẹ; mắt nhìn lên. Không đổi physics/Python.
  - GestureVisualChecks: check tư thế trèo (6 khung × 2 hướng × có/không mũ, nằm trong window, khác idle, có chuyển động); với PET_RENDER_REVIEW thì xuất thêm climbing-scene.png (mèo cạnh mép cửa sổ giả).
  - Test thật: scripts/test.ps1 393 C# check + Python đạt; UiChecks 187 WPF check đạt; build Release 0 warning.
  - Đã publish lại (17:19) + check-publish.ps1 đạt. Ảnh người dùng chụp trước đó (mèo không có chân trước) là bản publish cũ và mèo lúc đó không ở trạng thái Climbing (tư thế đứng thường).
  - Cần test tay: xem mèo trèo thật ở mép trái và mép phải cửa sổ; lúc chuyển từ trèo sang nhảy lên đỉnh có giật hình không; móng gần như không thấy trên cửa sổ nền tối.
- 2026-10-15 (mèo đi trên cửa sổ; người dùng chọn: trèo + nhảy, đi theo khi kéo cửa sổ, công tắc bật sẵn):
  - PetModel.cs: Ledge (mép trên nhìn thấy, ClimbLeft/ClimbRight), PetAction.Climbing, PetModel.LedgeId/ClimbId/ClimbSide, PetSettings.ClimbSpeed 110, LedgeMargin 8, LedgeBounceSpeed 400, ClimbOffset 30. PetPhysics.Step thêm ledges + mayLeaveLedge: đáp chỉ khi rơi từ trên, đứng trên gờ, quay đầu ở mép, rơi khi gờ mất/ra khỏi mép; leo (StepClimb) rồi nhảy nhẹ lên đỉnh; LeaveLedge giữ tư thế ngủ/thức; LetGo. Không có ledges thì physics như cũ (floor không nảy khi đang ngủ — trước đây mèo ngủ không bao giờ rơi).
  - PetLedges.cs (mới, thuần): Build (lọc, cắt phần bị che, cạnh trèo được), Support, Landing, Wall, PlanJump (60–380 DIPs cao, ≤260 ngang, đỉnh cao hơn mép 40), PlanClimb (≤700 DIPs).
  - PetWindow.Ledges.cs (mới): quét cửa sổ 0.3 s (EnumWindows z-order, DWM frame/cloaked, bỏ app mình, tool window, có chủ, không tiêu đề, phóng to, WS_EX_TRANSPARENT, desktop), mỗi khung đọc cửa sổ đang đứng để đi theo; tự lên (~mỗi 30–60 s × tính cách, nhảy nếu được, không thì đi tới cạnh rồi trèo), tự xuống sau 60–120 s (đi ra mép gần). Quiet/fullscreen/không rảnh thì không tự lên.
  - PetBehavior: StartLeap (dùng co người 0.16 s, vận tốc tính sẵn; Quiet từ chối; leap bị xóa khi có lệnh khác), Waking vẫn đếm 1.2 s khi đang rơi.
  - Tích hợp: bước sửa vị trí mỗi giây và DPI không kéo mèo trên gờ xuống (DPI thì về sàn), Reset/drag xóa gờ, CancelFeatureActivity làm mèo đang trèo buông ra, bóng chỉ đánh từ sàn, Nhớ chỗ này mờ khi trên cửa sổ, đi về chỗ nghỉ khi đứng ngay trên chỗ đó thì đi ra mép trước. Renderer: tư thế trèo (bản đầu; đã làm lại ở mục trên). PetEmotions busy thêm Climbing. Python BUSY_STATES thêm "Climbing".
  - Cài đặt → Mèo: "Mèo leo lên cửa sổ" (PetPreferences.ClimbWindows, mặc định true); tắt thì mèo rơi và ngừng đọc cửa sổ. Debug view thêm dòng Windows. AGENTS.md thêm ngoại lệ đã duyệt.
  - Test: LedgeTests.cs (C#, 29 check), LedgeChecks.cs (WPF với danh sách cửa sổ giả: ném lên, đi theo, bước sửa vị trí, cửa sổ mất, tự trèo, buông khi click, Quiet, tắt công tắc); UiChecks thay probe cửa sổ thật bằng danh sách rỗng; AppearanceVisualChecks thêm tư thế trèo 2 hướng.
- 2026-10-15 (bộ lông/phụ kiện + xem trước, người dùng duyệt danh sách và "cử động nhẹ"):
  - Bộ lông mới (PetAppearance.cs): GingerTabby (Mướp cam), Tuxedo (Mun cổ trắng: ngực/mõm/bàn chân trắng, mắt xanh vàng), RussianBlue (Xám xanh, mắt xanh lá), Siamese (Xiêm: tai, mặt, đuôi, bàn chân nâu, mắt xanh). CoatPalette thêm Ear/Muzzle/Paw (mặc định chính là brush Fur/Cream cũ nên bộ lông cũ không đổi pixel) và Swatch.
  - Phụ kiện mới (PetRenderer.cs): Scarf (khăn quàng sau đầu như vòng cổ, có vạt buông), Glasses (kính tròn trên mắt, mắt vẫn chớp/ngủ/cảm xúc), Flower (bông hoa chỗ nơ), Crown (vương miện thấp hơn mũ tiệc). PetPreferences.Coats/Accessories thêm tên mới; giá trị cũ giữ nguyên, bản EXE cũ đọc tên lạ thì về mặc định.
  - Tab Mèo: ô xem trước 140px (PetRenderer riêng + PetModel riêng, cắt lề trong suốt), đổi lựa chọn là đổi ngay; cử động idle (thở, đuôi, chớp mắt) bằng DispatcherTimer 33 ms chỉ khi tab Mèo đang mở, dừng khi đóng cửa sổ. Mèo thật chỉ đổi khi Lưu. Danh sách bộ lông có chấm màu (màu lông + chấm nhỏ màu vân/điểm). Cửa sổ cao tối đa 460.
  - Test: UiChecks (10 bộ lông, 8 phụ kiện, xem trước đổi theo lựa chọn + có chuyển động + không đổi mèo thật, đóng cửa sổ dừng timer); test giá trị không hợp lệ đổi "Crown" → "Cape" (Crown giờ hợp lệ). AppearanceVisualChecks tự phủ mọi tổ hợp mới.
- 2026-10-15 (giao diện gọn cho người dùng thường; người dùng "ok" mặc định: Khởi động cùng Windows vào Cài đặt, Debug vào "Khác", nhãn AI "Mèo thông minh (AI)", chưa làm ảnh xem trước mèo):
  - Menu chuột phải (PetWindow.Features.cs dựng toàn bộ; PetWindow.cs chỉ tạo ContextMenu): Cho ăn, Chơi bóng, Chơi đèn laser, Làm ngay ›, Cảm xúc › | Chế độ yên lặng (có tooltip), Âm thanh, Chỗ nghỉ yêu thích › | Nhật ký, Cài đặt…, Khác › (Gọi mèo về màn hình chính, Xem debug hành vi, Giới thiệu) | Thoát mèo. Icon từ font Segoe Fluent Icons/MDL2 có sẵn (PetUi.cs), mục có dấu tick không có icon. Handler và điều kiện mờ menu giữ nguyên.
  - PetWindow.Commands.cs: tách "Cảm xúc" (công tắc tự điều khiển + 5 cảm xúc) khỏi "Làm ngay" (12 động tác).
  - PetWindow.Settings.cs (mới, chuyển từ Features): cửa sổ "Cài đặt — {tên}" 460px, 4 tab Mèo/Lời nói/Âm thanh/Chung, nhãn trái-ô phải; Lưu (màu nhấn) + Hủy luôn ở đáy; tab tự cuộn khi màn nhỏ. Ô phụ thuộc mờ khi công tắc tắt (3 loại âm thanh, Mức độ nói, khoảng nhắc nghỉ). Nghe thử: 1 ô chọn 9 âm + nút Phát. Âm lượng hiện %. "Tinh chỉnh hành vi nâng cao…" và "Quên những gì mèo đã học…" thành link. Bỏ chữ "(Python AI)" khỏi nhãn. Khởi động cùng Windows trong tab Chung: đọc registry khi mở, chỉ ghi khi Lưu và giá trị đổi; cửa sổ test (không lưu preferences) không ghi registry.
  - Hành vi đổi: Khởi động cùng Windows không còn trong menu mèo (bấm Lưu mới có hiệu lực). Preferences giữ nguyên định dạng.
  - Khay: thêm "Cài đặt…", "Bật Python AI" → "Mèo thông minh (AI)"/"Smart pet (AI)", "Về màn hình chính" → "Gọi mèo về màn hình chính".
  - Nhật ký và Tinh chỉnh hành vi: chỉ đổi font Segoe UI 13 (Tinh chỉnh thêm nền sáng).
  - Test: UiChecks viết lại check menu (nhóm, 3 vạch ngăn, Khác, Thoát cuối), Làm ngay 12 + Cảm xúc 6, cài đặt qua PetSettingsForm (4 tab, Lưu/Hủy ngoài tab, ô mờ theo công tắc, Hủy không đổi gì); ảnh review menu + 4 tab × 2 ngôn ngữ khi có PET_RENDER_REVIEW. Docs: WINDOWS.md (mục mới "Menu and settings layout"), README, AI.md.
- 2026-10-15 (review của Codex, cả 3 điểm đã xác nhận đúng trong code):
  - Quiet: nhánh Quiet trong PetBehavior.Step vẫn đếm giờ và gọi DecisionRequested (2–6s); TryApply từ chối mọi lệnh khi Quiet (trước đây Quiet không gửi observation nào nên nhật ký/nhu cầu/câu phản ứng bị cũ). Hành vi đổi: trong Quiet mèo có câu phản ứng và bong bóng ý nghĩ (fullscreen vẫn chặn hiển thị).
  - Thức dậy: PetBehavior.Reset(pet) giữ bộ đếm khi đang Waking; CancelFeatureActivity và SetAiEnabled truyền pet. Sửa cả các đường Codex chưa nêu: click/chuột phải khi đang thức dậy, bấm Quiet, vào/thoát fullscreen.
  - Ngôn ngữ: RequestDecision ghi ngôn ngữ lúc gửi; phản hồi về khi ngôn ngữ đã đổi thì bỏ câu nói, vẫn nhận needs/diary.
  - Test: CombinationTests.cs (Quiet 60s vẫn gửi observation nhưng không di chuyển, TryApply từ chối, reset khi đang thức dậy), 3 check WPF (chuột phải + tắt AI + đường Quiet khi đang thức dậy; phản hồi ngôn ngữ cũ bị bỏ; câu đúng ngôn ngữ vẫn hiện).
- 2026-10-15 (Quiet, người dùng chọn phương án 2: chỉ phần Quiet): bóng và đèn laser bấm được và chạy trong Quiet (menu, ToggleToy, StartLaser, TickFeatures/TickLaser chỉ chặn khi context.Fullscreen). Quiet vẫn chặn tự đi về chỗ nghỉ và hành vi tự động; bật/tắt Quiet giữa chừng vẫn dừng trò chơi (CancelFeatureActivity như cũ). AGENTS.md thêm dòng ngoại lệ. Test: 2 check Quiet cũ đổi thành "vẫn chơi được", thêm check fullscreen vẫn chặn bóng/laser.
- 2026-10-15 (mục 6, chơi đèn laser): menu "Chơi đèn laser"/"Laser pointer" dưới Chơi bóng. PetWindow.Laser.cs: chấm đỏ (cửa sổ 24px click xuyên, không focus, bám đầu con trỏ, ẩn khi con trỏ ở màn hình khác); LaserChase (ToyBall.cs) Watch → Chase → Stalk (dừng ≥0.3s, cách < 70px) → Pounce (nhảy bằng JumpSpeed/WalkSpeed cũ) → Recover, nhịp theo tính cách; vồ trúng (cách < 45px) phát ball_tap. Chấm ở cao hơn đầu mèo >120px hoặc màn hình khác: chỉ nhìn. Hết sau 60s; hủy khi click/chuột phải/kéo/ném/ngủ/Pause/Quiet/fullscreen/Làm ngay/bóng/cho ăn. Trong lúc chơi không gửi con trỏ cho PetEmotions (không tính vuốt/giật mình), tắt thì ForgetCursor. Python nhận toy_started như bóng, không sửa Python.
- 2026-10-13 (một ngôn ngữ chung, người dùng chọn: giữ vi/en, mặc định English, debug view tiếng Anh):
  - Ô "Language / Ngôn ngữ" lên đầu cửa sổ cài đặt (English trước). Mọi chữ giao diện qua PetWindow.L(vi, en): menu chuột phải (đổi mỗi lần mở), menu tray + tooltip (App.xaml.cs, cập nhật khi mở menu và khi Save đổi ngôn ngữ qua LanguageChanged), cửa sổ cài đặt, Behavior tuning, hộp thoại, About, trạng thái AI (PetWindow.LocalizeAiStatus; debug view vẫn tiếng Anh), nút nghe thử âm thanh, tên tính cách/bộ lông/phụ kiện/tần suất (giá trị lưu không đổi).
  - PetPreferences: mặc định "en" (cả khi thiếu hoặc giá trị lạ). Đổi ngôn ngữ thì ẩn câu đang nói và bỏ câu chờ (không nói ngôn ngữ cũ).
  - Python: describe_learned theo observation.language (tên hành động + buổi/nhu cầu); câu đã học thuộc ngôn ngữ kia thì ghi tên tình huống ("a greeting"/"câu chào"). Protocol không đổi.
  - Giữ nguyên: tiêu đề cửa sổ mèo "{tên} — Desktop Pet" (smoke test dùng), tên "Desktop Pet".
  - Test: UiChecks quét mọi nhãn menu (cả menu con) ở cả 2 ngôn ngữ, không lẫn chữ ngôn ngữ kia; check trạng thái AI; cập nhật check cài đặt/nhật ký; SpeechTests mặc định en; 1 test Python mới + test learned cũ.
- 2026-10-13 (menu Làm ngay, người dùng chọn: công tắc chỉ tắt cảm xúc, không đứng yên; không có Chóng mặt bấm tay):
  - Menu chuột phải "Làm ngay"/"Do now" (theo ngôn ngữ): 12 động tác (Ngồi, Nhìn quanh, Nhảy, Ngủ, Vẫy chân, Liếm lông, Duỗi người, Nhồi bột, Lăn ngửa, Đuổi đuôi, Cào móng, Hắt xì). PetBehavior.StartManual/CanStartManual: bỏ qua cooldown, bond, xác suất, Quiet; thời lượng như cũ (Ngồi/Nhìn 4s); lệnh mới thay lệnh cũ; Ngủ bỏ qua wake cooldown rồi theo luật ngủ cũ. Không cắt drag/bay/sleep lock/waking: PetWindow.Commands.cs xếp hàng tối đa 3s (ví dụ chuột phải đánh thức → chờ 1.2s waking rồi làm). Không phải interaction (chỉ tăng revision), không ảnh hưởng needs/học.
  - Công tắc "Tự điều khiển cảm xúc" (chỉ trong phiên): PetEmotions.ManualMode tắt Happy khi vuốt, Angry khi click dồn, Surprised khi rê nhanh, đợt buồn, Sleepy tự ngủ, gợi ý AI; Perform giữ Bình thường/Vui/Giận/Buồn/Giật mình đến khi đổi; không chặn hành vi tự động (BlocksAutonomy = false); drag/ăn/bay/ngủ vẫn ưu tiên, Dizzy khi kéo mạnh vẫn có. Python nhận emotion Neutral (PetEmotions.Reported), trừ Sleepy khi ngủ. Tắt công tắc: xóa cảm xúc, đặt lại đồng hồ "lâu không được chú ý".
  - AGENTS.md thêm dòng ngoại lệ đã duyệt; docs/WINDOWS.md, docs/AI.md cập nhật. Test: ManualControlTests.cs (C#), 6 check WPF.
- 2026-10-13 (đợt nhỏ): đã push commit 2631fec lên GitHub.
  - Quả bóng: cửa sổ bóng dời bằng MoveWindow (1 lệnh SetWindowPos) thay vì Left rồi Top; không nội suy vì bóng cập nhật theo dt từng khung. Không đổi physics bóng/nhịp chơi.
  - Câu chào qua giấc ngủ: câu hello/welcome_back đang chờ không bị hủy khi mèo ngủ, chờ tối đa 30s (PetSpeech.GreetingPendingFor), hiện sau khi mèo thức (không hiện khi Sleeping/Waking nên không đánh thức mèo). Câu wake_up không thay câu chào; câu phản ứng mới vẫn thay. Câu khác vẫn bị hủy khi ngủ như cũ. Away nap dài hơn 30s thì câu chào hết hạn (đã có câu "back"); người dùng đã duyệt cách này (2026-10-13).
- 2026-10-13 (đợt 4+5+7):
  - Câu chờ (4): PetSpeech.Offer/TryShowPending/DropPending giữ 1 câu tối đa 10s khi mèo chưa nói được (ăn, bay, khoảng nghỉ 12s); câu mới thay câu cũ; ngủ/fullscreen/tắt Speech/hết hạn nhắc nghỉ thì bỏ. PetWindow.Speech: SpeechAllowed, TickPendingSpeech (mỗi khung), SpeechShown (meo, xóa bong bóng ý nghĩ, nhắc nghỉ).
  - Động tác (5): PetAction thêm Kneading, RollingOver, ChasingTail, Scratching, Sneezing (PetActionRules.IsGesture, thời lượng cố định 3.2/3.5/3/2.4/1s trong PetSettings). Nhồi bột: C#, vuốt 2 lần trong 10s (cooldown 20s), chạy cả Quiet/không Python. Python: ROLL_OVER (vừa được vuốt, bond ≥60, affection ≥60, cách ≥120s), CHASE_TAIL (tăng theo chán, nhóm riêng không chung với JUMP), SCRATCH (trọng số nhỏ). Local chọn thêm đuổi đuôi/cào móng/hắt xì (chung cooldown 45s với tự chăm sóc). Quiet/fullscreen chặn động tác tự làm. Renderer: tư thế mới + Giận cụp tai/quất đuôi; vào động tác theo envelope nên khung đầu giống tư thế cũ.
  - Nhật ký (7): Python lưu first_seen, totals, bond_history (30 ngày) và gửi decision.diary (days, since, totals, bond, 14 ngày bond, habits 24 giờ). C# PetWindow.Diary.cs: menu "Nhật ký của {tên}"/"{name}'s diary" theo ngôn ngữ, biểu đồ bond + biểu đồ giờ (tooltip), học được gì; AI tắt thì chỉ số liệu phiên.
  - Test mới: GestureTests.cs (C#), GestureVisualChecks.cs + LifeChecks (WPF), 3 test Python; ToolingTests cập nhật danh sách action bị hủy bởi input.
- 2026-10-13 (đợt 1+2+3: bộ lông/phụ kiện, nhắc nghỉ, ngữ cảnh máy; người dùng chọn 1a + 2a):
  - Bộ lông: Orange (mặc định, giống pixel bản cũ), Gray, Black (than chì, mắt hổ phách, viền đậm), White (mắt xanh), Calico (mảng cam/đen), Tabby (sọc "M" trán + lưng). Phụ kiện: None, Bell (vòng cổ chuông), Bow, Hat (mũ tiệc). PetAppearance.cs (CoatPalette, brush/pen frozen); PetRenderer lấy màu theo palette, vân/mảng clip trong viền; phụ kiện vẽ trong transform đầu. Cài đặt: nhóm Appearance. Preferences: Coat, Accessory (giá trị lạ → mặc định).
  - Nhắc nghỉ (mặc định tắt; 30/45/60 phút): PetBreaks.cs chỉ đếm khi có input hệ thống (GetLastInputInfo); vắng ≥5 phút = đã nghỉ, reset. Đến hạn và mèo nói được → observation break_due → Python chọn câu nhóm "break" (ưu tiên nhất, chạy cả khi tắt Speech); C# hiện rồi mèo duỗi người (nếu rảnh, không Quiet). Python tắt/không trả câu trong 15s → câu dự phòng trong C# (break:local:…). Không nhắc khi ngủ/kéo/bay/fullscreen (chờ). Preferences: BreakReminders, BreakMinutes. Cài đặt: nhóm Break reminders.
  - Ngữ cảnh máy (PetWindow.Context.cs, mỗi giây, Pause đóng băng): chỉ đọc hình chữ nhật/style/class cửa sổ foreground và thời điểm input cuối. Fullscreen (cửa sổ không caption phủ cả màn hình đang có mèo; bỏ qua desktop/taskbar/cửa sổ của app): như Quiet (không đổi Quiet đã lưu), không bong bóng chữ/ý nghĩ, không âm thanh (preview vẫn chạy), mèo vẫn hiện (lựa chọn 1a). Vắng ≥5 phút: away nap (2a), ngủ giữ timer (HoldSleep), thức khi có input lại (waking 1.2s + cooldown 30s), click vẫn thức ngay; Python được context.returned → câu nhóm "back"; đợt buồn ngắn bị hoãn khi quay lại (PetEmotions.UserReturned). AGENTS.md thêm dòng ngoại lệ away nap.
  - Protocol thêm (additive): break_due, context {fullscreen, away, returned}. Debug view thêm dòng Context và Break reminder. lines.json thêm nhóm back, break (vi/en, ≥5 câu).
  - Test mới: ContextTests.cs (31 check C#), AppearanceVisualChecks.cs + ContextChecks trong UiChecks (12 check WPF), 3 test Python. UiChecks thay probe ngữ cảnh bằng giá trị giả để chạy không người dùng không bị away nap.
- 2026-10-13 (rà soát tối ưu): đo thật mèo đang chạy: CPU ~6% một nhân (0.4% máy 16 nhân), RAM 200 MB ổn định 2 phút (không rò rỉ), Python ~0% CPU/12 MB. Sửa: cửa sổ cài đặt cao tối đa theo màn hình của mèo + cuộn được (trước 760px có thể tràn laptop 768p, khuất nút Save); nâng giới hạn đọc ai-memory.json 64 KB → 512 KB (vượt giới hạn sẽ âm thầm reset nhu cầu/bond/học). check-publish.ps1 tìm cửa sổ mèo theo tiêu đề "…Desktop Pet" (trước đây gửi WM_CLOSE nhầm vào bong bóng chữ khi mèo chào ngay lúc mở). Đã xóa ảnh chụp màn hình live-hello.png. Chưa làm (tùy chọn): 30 FPS khi đứng yên, chỉnh GC giảm RAM.
- Test 2026-10-13: 259 C#, 120 WPF, 42 Python đạt; publish + check-publish đạt.
- 2026-10-10 (mèo biết nói + tự học, cách 1): ai/lines.json (~16 tình huống × vi/en, mèo xưng tên {name}, gọi "sen"/"human"), ai/speech.py chọn câu chống lặp 20 câu, ai/learning.py học từ phản ứng (ngữ cảnh buổi × nhu cầu; vuốt/ăn +1, bóng +0.5, click +0.3, ném mạnh −1, giận −0.5; trọng số ×0.5–×2; phai nửa sau 7 ngày; tối đa 500 khóa). Service: sự kiện chờ tối đa 12s nếu mèo đang bay; câu nhu cầu cách 300/120/60s theo tần suất; nói vu vơ theo tần suất, không nói vu vơ ở Quiet. C#: PetSpeech (2.5–6s, cách ≥12s, không nói khi ngủ/kéo/bay/ăn, được nói khi giận), cửa sổ bong bóng riêng PetWindow.Speech.cs (click xuyên, không lấy focus, bám mèo, trong work area), lời nói được nhận cả khi lệnh bị bỏ do revision. Cài đặt: Speech group (bật/tắt, Tiếng Việt/English, Ít/Vừa/Nhiều, Quên những gì đã học). Debug view: Speech, Learned likes/dislikes. Preferences mới: Speech, Language, Chattiness, LearningReset. Đóng gói thêm speech.py, learning.py, lines.json.
- 2026-10-09 (AI giai đoạn 1 + bong bóng): Python có hunger/boredom/affection/bond (lưu trong ai-memory.json), thói quen theo giờ, chào khi quay lại (vắng ≥1h, bond ≥50). Bond giới hạn +3/−2 mỗi ngày. Đói ≥70 → đòi chú ý + bong bóng cá; chán ≥75 → đòi chơi + bong bóng bóng; chán ảnh hưởng trọng số JUMP/FOLLOW_MOUSE. C# đếm tương tác theo loại (PetInteractions) gửi qua observation.interactions; nhận thought/needs (sai thì bỏ qua, không loại quyết định). PetThought.cs quản lý bong bóng (2.5s, cooldown 20s/loại, 8s chung, không hiện khi ngủ/kéo/bay/ăn/giận/giật mình). Renderer vẽ bong bóng bên trái trên đầu. Debug view hiện needs + thought.
- 2026-10-09: Buồn ngắn: 3 phút người dùng không đụng → Sad ~6s rồi tự hết, cách nhau ≥3 phút; đồng hồ người dùng không bị reset khi mèo tự ngủ/dậy (trước đây Sad gần như không bao giờ hiện vì Sleepy 2 phút + reset khi thức). Chạm là hết buồn. Không đổi ngủ 2 phút.
- 2026-10-09: Glitch khi ném/đi: timer chạy nửa chu kỳ + bỏ tick sớm (nhịp đều ~60 FPS), nội suy vị trí hiển thị giữa 2 bước physics 1/120 s, dời cửa sổ bằng 1 lệnh SetWindowPos thay vì Left rồi Top. Reset/drag/DPI/khôi phục màn hình thì snap, không nội suy. Không đổi physics, lực ném, gravity, bounce, FPS mục tiêu (60/30/10). Debug view hiện FPS và max gap.
- 2026-10-09: Log file %LOCALAPPDATA%\DesktopPet\logs\desktoppet.log (≈1 MB + .old.log), ghi cả unhandled exception (chỉ log, không đổi hành vi). Sửa lỗi ký tự trong docs.
- 2026-10-09: PetActionRules (PetModel.cs) gom danh sách action từ chối lệnh AI (DecisionGuard + TryApply) và action hủy về Idle; Python dùng BUSY_STATES, có test so khớp hai bên. PetEmotions/CanPlay dùng tập khác nên giữ nguyên. Hằng số GroomingDuration/StretchingDuration/SelfCareCooldown/AttentionGestureDuration/AttentionCooldown trong PetSettings. Pen trong renderer được cache + freeze.
- 2026-10-09: PetRenderer.OnRender tách thành ComputePose/DrawCat/DrawBody/DrawHead/DrawFace/DrawDizzyOrbit/DrawOverlays/DrawTreat, giữ nguyên biểu thức và thứ tự vẽ.
- 2026-10-09: Menu mèo thêm "Start with Windows" (mặc định tắt, HKCU Run, đọc registry mỗi lần mở menu) và "About Desktop Pet" (version, thư mục data, trạng thái AI). Version 1.0.0 trong csproj; publish.ps1 tạo zip.
- 2026-10-09: Python nhận local_hour: đêm (22–6h) ít hoạt động, nhiều ngủ hơn; sáng (7–10h) năng động hơn. Chỉ là trọng số, C# vẫn giữ sleep lock/thời lượng/wake cooldown.
- 2026-10-08: hoàn tất năm cải tiến motion. PetMotion.cs chỉ giữ lịch sử trình diễn, không điều khiển vị trí hoặc lực.
- Chân/độ nhún theo vận tốc thực, giảm biên độ khi chậm lại. Yêu cầu đổi hướng được giữ khi đang giảm tốc, rồi tăng tốc ngược lại; hướng vẽ theo chiều di chuyển trước khi quay ngắn.
- Chuyển đứng/ngồi/ngủ/thức dùng RestBlend, giữ mốc chạm đất. Đầu/đuôi có độ trễ giới hạn và tự ổn định khi dừng; Pause đóng băng, pickup ưu tiên.
- PreparingJump co người 0.16 giây trước cú nhảy tự động, sau đó dùng lực nhảy cũ. Sleep/Quiet/click/drag hủy được; ném chuột không qua bước chuẩn bị.
- Kéo giãn nhẹ lúc bay và nén nhẹ 0.22 giây lúc chạm đất, chỉ đổi hình vẽ. Không đổi gravity, hệ số bounce, lực ném, ngưỡng kéo hoặc Dizzy.
- Thêm MotionTests.cs/MotionVisualChecks.cs; test vận tốc, đảo hướng, chuyển tư thế, hủy chuẩn bị nhảy, giữ bounce velocity và mép khung đều đạt. Đã xem ảnh tại artifacts/motion-review, không thấy clipping trong ảnh đã xem.
- Hoàn tất cả năm mục được duyệt: chơi bóng theo trình tự, tính cách rõ hơn, âm thanh theo tương tác, tự chăm sóc và giao diện chỉnh thông số.
- ToyPlaySequence: Observe → Crouch → Chase → Recover; Curious quan sát lâu, Playful chuẩn bị nhanh, Calm nghỉ lâu. Đuổi không tới bóng trong 6 giây thì nghỉ. Input/sleep/emotion bảo vệ sẽ hủy và xóa tư thế; Pause đóng băng.
- Grooming: liếm chân rồi rửa mặt trong 3.6 giây. Stretching: duỗi người trong 2.4 giây. Local chọn thỉnh thoảng, cooldown chung 45 giây, không đổi physics. Input ngắt ngay; sleep ưu tiên; Quiet không tự chăm sóc.
- Python thêm LOOK; Curious quan sát/follow lâu hơn, Calm nghỉ lâu và ít đòi chú ý hơn, Playful đi từng đợt ngắn hơn. Profile cũng ảnh hưởng lựa chọn local và nhịp chơi bóng.
- Thêm ball_tap, satisfied, wake; hiện có chín preview. Cues dùng recording CC0 đã có với xử lý cartoon, không tải asset hoặc dependency mới. Giữ volume, category, cooldown và không overlap; cue bị chặn không phát trễ.
- Name and personality… → Behavior tuning…: tám thông số được hỗ trợ trong ai/personality.json, slider + ô số, mô tả, kiểm tra hữu hạn/phạm vi và Restore defaults. Save lưu override trong preferences, gửi qua observation.tuning; không sửa JSON đóng gói.
- Restore defaults rồi Save xóa override và khởi động lại Python nếu đang bật, không reset state ngủ/thức. Không có override thì Python dùng file cấu hình. Sửa JSON nguồn cần publish lại; sửa file runtime cần restart AI.
- Thêm PersonalityTuning.cs, PetWindow.Personality.cs và FeatureExpansionTests.cs; cập nhật test Python/WPF và docs/AI.md, docs/WINDOWS.md.
- Sửa PROGRESS.md về UTF-8 tiếng Việt đúng dấu.
- Giữ thay đổi trước: giảm lặp local, vuốt ve nghiêng đầu/Happy, SEEK_ATTENTION nhìn/vẫy chân 2.4 giây với cooldown 45 giây, audio cache/cân bằng RMS và bỏ cue trễ.
- Giữ sleep lock, tự thức 10 giây, waking 1.2 giây, wake cooldown 30 giây, drag/throw/Dizzy, PerMonitorV2, vị trí theo monitor, single-instance, custom icon, treat/rest spot và local fallback.
- Lịch sử chuyển project: 2026-10-05 tạo AGENTS.md/PROGRESS.md tại ổ D khi thư mục đó trống; source hiện nằm tại ổ C, không cần khôi phục về D.

## Đang dở và việc tiếp theo
- Giao diện: menu khay (WinForms) vẫn kiểu Windows mặc định.
- Tạm hoãn theo người dùng (2026-10-16): lựa chọn away nap 5/10/15 phút; thêm tốc độ đói, tốc độ chán, khoảng cách bong bóng vào Behavior tuning (cần đưa THOUGHT_COOLDOWN ra personality.json).
- Hiệu năng: đã có scripts/perf.ps1; còn đo khi ngủ, kéo nhanh, chơi laser, chạy dài nhiều giờ (kiểm tra RAM ~290 MB sau 7 giờ so với ~130 MB lúc mới mở), nhiều màn hình/DPI.
- Phương án 1 chưa làm (người dùng chọn 2): bỏ cooldown cho ăn 20s; menu cho ăn/bóng/laser luôn bấm được và xếp hàng ≤3s khi mèo đang thức dậy/giận/giật mình (như Làm ngay). Ý tưởng khác: mèo no (đói < 10) thì từ chối ăn thay vì cooldown cố định.
- Đề xuất còn lại từ danh sách 2026-10-13: 8 (mèo đi trên cửa sổ, đợt riêng), 9 (dạy mèo nói). Đã làm 1–7.
- Nhật ký: người dùng cũ có first_seen = lần đầu chạy bản này (không có dữ liệu trước đó), nên "số ngày ở cùng" bắt đầu từ 2026-10-13.
- Away nap: nếu xem video không fullscreen mà không chạm chuột 5 phút thì mèo cũng ngủ (chấp nhận được); có thể thêm tùy chọn thời gian 5/10/15 phút nếu thấy sớm.
- Fullscreen: chỉ xét màn hình đang có mèo; app "borderless fullscreen" không caption sẽ được tính (đúng ý). Nếu có app bị nhận nhầm, xem log "Fullscreen app detected" và cân nhắc danh sách class bỏ qua.
- Câu nhắc nghỉ dự phòng trong C# (PetBreaks) trùng nội dung 3 câu đầu của lines.json; sửa giọng văn ở lines.json thì nên sửa cả đó.
- Cách 2 (dạy mèo nói: người dùng gõ câu theo tình huống, mèo học câu nào được thích) và cách 3 (LLM trò chuyện có trí nhớ) chưa làm.
- Câu khác (không phải câu chào) do Python chọn đúng lúc mèo vừa ngủ vẫn bị bỏ; chỉ câu chào được giữ qua giấc ngủ.
- Người dùng nên đọc lại ai/lines.json để chỉnh giọng văn.
- Tinh chỉnh nhịp nhu cầu/bong bóng sau khi người dùng dùng thật (tốc độ đói 25/giờ, chán 2/phút, khoảng cách bong bóng 90s).
- Ý tưởng chưa làm: LLM bong bóng thoại (cần đổi quy tắc AGENTS.md); phím tắt toàn cục cho Làm ngay; có thể thêm 3 tốc độ nhu cầu vào Behavior tuning. (Ngữ cảnh máy/fullscreen/away nap đã làm.)
- Quả bóng đã dời bằng 1 lệnh SetWindowPos; nếu vẫn giật thì xem tới CompositionTarget.Rendering (mục dưới).
- Nếu vẫn thấy giật khi ném sau bản này: cân nhắc chuyển vòng lặp active sang CompositionTarget.Rendering (đồng bộ vsync); probe PowerShell cho kết quả không ổn định nên chưa dùng.
- Không còn hạng mục nào khác trong 11 đề xuất đã duyệt.
- Chờ người dùng nghe và thao tác thử để tinh chỉnh nhịp chơi/âm thanh nếu cần.
- Không tích hợp LLM, voice hay computer vision; không thay framework hoặc hệ thống rendering/physics.

## Cần test thủ công
- Đi trên cửa sổ: để một cửa sổ (không phóng to) như VS Code/Explorer; ném/thả mèo lên mép trên: đáp đúng mép, đi qua lại, quay đầu ở mép; kéo cửa sổ: mèo đi theo; thu nhỏ/phóng to/đóng: mèo rơi. Cửa sổ thấp gần mèo: mèo tự nhảy lên (chờ 1–2 phút). Cửa sổ cao chạm đáy: mèo đi tới cạnh, trèo lên, nhảy lên đỉnh; click khi đang trèo: buông. Mèo ngủ trên cửa sổ rồi thu nhỏ cửa sổ: rơi mà vẫn ngủ. Chơi bóng/laser khi mèo trên cửa sổ: mèo đi ra mép và nhảy xuống. Tắt "Mèo leo lên cửa sổ": mèo rơi. Xem dòng Windows trong debug view. Thử Chrome/VS Code (thanh tiêu đề tự vẽ) xem có lệch không.
- Giao diện mới: chuột phải mèo xem menu (icon rõ, nhóm hợp lý, menu con Làm ngay/Cảm xúc/Chỗ nghỉ/Khác); mở Cài đặt từ menu mèo và từ khay; đổi từng tab rồi Hủy (không đổi gì) và Lưu; tắt Bật âm thanh/Mèo biết nói/Nhắc nghỉ thì ô phụ mờ; Nghe thử; bật/tắt Khởi động cùng Windows rồi Lưu (kiểm tra Task Manager → Startup); đổi ngôn ngữ; Esc đóng cửa sổ. Xem trên màn hình DPI 125–150%.
- Quiet + AI: bật Quiet, vuốt/cho ăn mèo: có câu phản ứng (nếu bật Speech) nhưng mèo không tự đi; mở Nhật ký sau vài phút Quiet: số liệu mới. Click để đánh thức rồi chuột phải ngay: mèo vẫn thức trong ~1.2s như thường.
- Quiet + trò chơi: bật Quiet, chọn Chơi bóng và Chơi đèn laser: chạy bình thường, hết trò thì mèo đứng yên (không tự đi). Bật Quiet giữa lúc đang chơi: trò dừng. Mở video fullscreen: không bắt đầu được.
- Đèn laser: bật từ menu, rê chuột trên màn hình có mèo: mèo nhìn, đuổi, dừng chuột gần mèo thì rình rồi vồ; rê lên cao thì mèo chỉ nhìn; rê qua người mèo không bị tính vuốt; sang màn hình khác thì chấm ẩn; click mèo/chuột phải thì tắt; tự tắt sau 60s. Click vào app khác trong lúc chơi vẫn được (chấm không chặn click).
- Ngôn ngữ: mở cài đặt, chọn English → Save; kiểm tra menu chuột phải (cả Làm ngay, Chỗ nghỉ), menu tray, tooltip khay, About, Nhật ký, Behavior tuning, hộp thoại Quên: toàn tiếng Anh; mèo nói tiếng Anh. Đổi lại Tiếng Việt: toàn tiếng Việt. Debug view luôn tiếng Anh.
- Làm ngay: thử từng động tác (cả khi bật Quiet); chuột phải lúc mèo đang ngủ rồi chọn động tác (mèo thức 1.2s rồi làm); chọn Ngủ ngay sau khi mèo vừa thức; click/kéo giữa lúc làm thì ngắt ngay; Pause thì menu mờ.
- Tự điều khiển cảm xúc: bật, chọn từng cảm xúc; vuốt/click dồn không đổi cảm xúc; để yên vài phút không buồn/ngủ tự động; kéo mạnh vẫn chóng mặt; tắt công tắc thì về bình thường ngay, không buồn/ngủ liền; mở lại app thì công tắc tắt.
- Quả bóng (menu chơi bóng): nhìn bóng nảy/lăn và khi click bóng: còn giật/lệch không.
- Câu chào: mở lại mèo vài lần (Speech + AI bật); câu "Chào sen" xuất hiện. Nếu mèo ngủ ngay lúc mở, câu chào phải hiện sau khi mèo thức (không đánh thức mèo sớm).
- Vuốt mèo liên tục vài giây: mèo ngồi nhồi bột (mắt lim dim) rồi về bình thường; trái tim/Happy vẫn như cũ. Khi bond ≥ 60 (xem debug view), vuốt xong có thể lăn ngửa. Click/kéo giữa lúc làm động tác thì ngắt ngay.
- Để mèo chạy lâu: thỉnh thoảng đuổi đuôi, cào móng, hắt xì; không quá dày. Clicking đến khi Giận: tai cụp, đuôi quất.
- Cho ăn rồi để Python trả câu trong lúc ăn: câu cảm ơn hiện ngay sau khi ăn xong.
- Mở nhật ký (menu chuột phải) với AI bật và tắt; đổi ngôn ngữ; rê chuột lên biểu đồ xem tooltip; sau vài ngày xem biểu đồ bond/giờ chơi.
- Bộ lông/phụ kiện: đổi trong Appearance, Save, xem trực tiếp khi đi, ngủ, kéo, ném, ăn treat, vuốt ve; đặc biệt mèo đen (mắt/miệng có rõ không) và mũ khi kéo sát mép trên màn hình.
- Nhắc nghỉ: bật 30 phút, dùng máy bình thường; xem debug view dòng Break reminder; khi nhắc mèo nói + duỗi người; tắt Speech vẫn nhắc; rời máy ≥5 phút thì đồng hồ reset.
- Fullscreen: mở video YouTube fullscreen (F11/nút fullscreen), game, PowerPoint trình chiếu trên màn hình có mèo: mèo đứng yên, im lặng, không bong bóng; thoát fullscreen thì bình thường. Cửa sổ maximize thường KHÔNG được tính. Thử fullscreen ở màn hình phụ khi mèo ở màn hình chính (không được tính).
- Away nap: không chạm máy 5 phút → mèo ngủ và ngủ mãi; chạm chuột → mèo thức (1.2s) và chào "Sen về rồi!…" (nếu bật Speech + AI). Click mèo đang away nap vẫn thức và kéo được. Pause trong lúc away nap.
- Mèo nói: tần suất có dễ chịu không (thử Ít/Vừa/Nhiều), đổi English, câu phản ứng sau vuốt/cho ăn/ném mạnh, bong bóng khi mèo ở sát mép màn hình và trên màn hình phụ/DPI khác.
- Tự học: sau vài ngày xem debug view "Learned likes/dislikes"; thử nút Quên.
- Để mèo chạy, mở Behavior debug view xem Needs/Thought thay đổi; vuốt ve (tim), cho ăn khi đói, chơi bóng khi chán. Đánh giá bong bóng có quá dày/thưa không.
- Không đụng mèo 3 phút: mèo buồn vài giây rồi bình thường; click giữa lúc buồn thì hết ngay.
- Ném mèo mạnh/nhẹ, nhìn khi bay và nảy: còn giật không? Mở Behavior debug view xem dòng Frame (kỳ vọng ~60 fps, max gap < ~35 ms). Kiểm tra drag, pickup, thả, sleep/wake không đổi cảm giác.
- Bật/tắt "Start with Windows" từ bản publish, đăng xuất/đăng nhập lại; xem About; mở thư mục logs.
- Motion mới: xem trực tiếp đi/chậm lại/quay đầu, chuẩn bị nhảy/tiếp đất, ngồi/ngủ/thức và kéo khi đang chuyển tư thế. Ảnh render không thay thế kiểm tra độ mượt theo thời gian.
- Nghe chín preview, tiếng chạm bóng/ăn xong/thức dậy; đánh giá độ dễ chịu, tương phản âm lượng và tần suất. Test PCM không xác nhận chất lượng nghe.
- Chơi bóng với Balanced/Playful/Curious/Calm; kiểm tra độ tự nhiên của quan sát/hạ người/đánh/nghỉ.
- Quan sát liếm chân/rửa mặt/duỗi người; thử ngắt bằng click và drag, Pause/Resume; test tự động đã kiểm tra state lock nhưng cảm nhận chuyển động cần thao tác thật.
- Dùng Behavior tuning, Save/đóng không Save/Restore defaults; khởi động lại để kiểm tra trải nghiệm thực tế.
- Mixed-DPI nhiều màn hình vật lý, drag/throw qua màn hình, ngắt/kết nối và khôi phục vị trí/favorite spot: chưa thực hiện trên nhiều màn hình vật lý.
