# Tiến độ Desktop Pet

Cập nhật: 2026-10-13 (Asia/Saigon).
Project: C:\Project VS\Pet Computer. Đã dùng git (nhánh main), mỗi đợt một commit; tác giả cục bộ tnpt003.
GitHub: https://github.com/tnpt003/Pet-Computer (origin/main). Không commit artifacts/, .tools/, bin/obj, *.log.

## Kết quả test gần nhất (2026-10-13, đợt bộ lông/phụ kiện + nhắc nghỉ + ngữ cảnh máy)
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
- Đề xuất còn lại từ danh sách 2026-10-13: 4 (giữ câu bị chặn, không lỡ câu chào), 5 (thêm động tác vector: nhồi bột, lăn ngửa, đuổi đuôi…), 6 (chơi với con trỏ kiểu laser), 7 (Nhật ký Mochi), 8 (mèo đi trên cửa sổ, đợt riêng), 9 (dạy mèo nói). Gợi ý tiếp: 5 + 7.
- Away nap: nếu xem video không fullscreen mà không chạm chuột 5 phút thì mèo cũng ngủ (chấp nhận được); có thể thêm tùy chọn thời gian 5/10/15 phút nếu thấy sớm.
- Fullscreen: chỉ xét màn hình đang có mèo; app "borderless fullscreen" không caption sẽ được tính (đúng ý). Nếu có app bị nhận nhầm, xem log "Fullscreen app detected" và cân nhắc danh sách class bỏ qua.
- Câu nhắc nghỉ dự phòng trong C# (PetBreaks) trùng nội dung 3 câu đầu của lines.json; sửa giọng văn ở lines.json thì nên sửa cả đó.
- Cách 2 (dạy mèo nói: người dùng gõ câu theo tình huống, mèo học câu nào được thích) và cách 3 (LLM trò chuyện có trí nhớ) chưa làm.
- Câu do Python chọn nhưng C# không hiện (ví dụ mèo vừa đi ngủ) thì bị bỏ; có thể cho C# giữ chờ vài giây nếu thấy mất câu chào quá thường.
- Người dùng nên đọc lại ai/lines.json để chỉnh giọng văn.
- Tinh chỉnh nhịp nhu cầu/bong bóng sau khi người dùng dùng thật (tốc độ đói 25/giờ, chán 2/phút, khoảng cách bong bóng 90s).
- Ý tưởng chưa làm: giai đoạn 3 (ngữ cảnh máy: idle toàn hệ thống, app fullscreen → yên lặng); LLM bong bóng thoại (cần đổi quy tắc AGENTS.md). Có thể thêm 3 tốc độ nhu cầu vào Behavior tuning.
- Cửa sổ quả bóng (ToyBall) vẫn dời bằng Left rồi Top, chưa nội suy; nếu bóng còn giật thì áp dụng cách tương tự.
- Nếu vẫn thấy giật khi ném sau bản này: cân nhắc chuyển vòng lặp active sang CompositionTarget.Rendering (đồng bộ vsync); probe PowerShell cho kết quả không ổn định nên chưa dùng.
- Không còn hạng mục nào khác trong 11 đề xuất đã duyệt.
- Chờ người dùng nghe và thao tác thử để tinh chỉnh nhịp chơi/âm thanh nếu cần.
- Không tích hợp LLM, voice hay computer vision; không thay framework hoặc hệ thống rendering/physics.

## Cần test thủ công
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
