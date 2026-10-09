# Tiến độ Desktop Pet

Cập nhật: 2026-10-08 (Asia/Saigon).
Project: C:\Project VS\Pet Computer.

## Kết quả test gần nhất
- scripts/test.ps1 -Ui: 201 C# checks, 107 WPF checks, 20 Python tests đạt. Log: test-motion.log. Đợt tính năng trước đạt 181 C#/101 WPF/20 Python (test-features.log).
- Publish Release self-contained thành công: artifacts/win-x64/DesktopPet.exe.
- scripts/check-publish.ps1 đạt: khởi động từ thư mục khác, bundled .NET/Python, mở lần hai, đóng ứng dụng và Python sạch.
- Smoke test dùng PET_DATA_DIRECTORY riêng dưới artifacts/smoke-data/<run-id>, không ghi preferences hoặc memory của người dùng.
- Đã xem ảnh render Grooming và Stretching trong artifacts/review; không thấy cắt hình ở các khung đã xem. Chưa xác nhận độ mượt bằng thao tác thực tế.
- Lịch sử: bản bàn giao trước đạt 143 C#/87 WPF/16 Python; đợt ưu tiên tiếp theo đạt 156 C#/90 WPF/16 Python. Không dùng số liệu cũ thay kết quả hiện tại.

## Đã làm gần đây
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
- Không còn hạng mục triển khai nào trong năm mục đã duyệt.
- Chờ người dùng nghe và thao tác thử để tinh chỉnh nhịp chơi/âm thanh nếu cần.
- Không tích hợp LLM, voice hay computer vision; không thay framework hoặc hệ thống rendering/physics.

## Cần test thủ công
- Motion mới: xem trực tiếp đi/chậm lại/quay đầu, chuẩn bị nhảy/tiếp đất, ngồi/ngủ/thức và kéo khi đang chuyển tư thế. Ảnh render không thay thế kiểm tra độ mượt theo thời gian.
- Nghe chín preview, tiếng chạm bóng/ăn xong/thức dậy; đánh giá độ dễ chịu, tương phản âm lượng và tần suất. Test PCM không xác nhận chất lượng nghe.
- Chơi bóng với Balanced/Playful/Curious/Calm; kiểm tra độ tự nhiên của quan sát/hạ người/đánh/nghỉ.
- Quan sát liếm chân/rửa mặt/duỗi người; thử ngắt bằng click và drag, Pause/Resume; test tự động đã kiểm tra state lock nhưng cảm nhận chuyển động cần thao tác thật.
- Dùng Behavior tuning, Save/đóng không Save/Restore defaults; khởi động lại để kiểm tra trải nghiệm thực tế.
- Mixed-DPI nhiều màn hình vật lý, drag/throw qua màn hình, ngắt/kết nối và khôi phục vị trí/favorite spot: chưa thực hiện trên nhiều màn hình vật lý.
