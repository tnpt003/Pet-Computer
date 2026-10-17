# Checklist test thủ công

Dùng cho những gì test tự động không thay được: cảm giác chuyển động, âm thanh, giọng văn, nhiều màn hình.
Chạy bản publish (`artifacts\win-x64\DesktopPet.exe`). Mở **Behavior debug view** (menu chuột phải → Khác) để xem trạng thái.
Tick `[x]` khi đạt; ghi lỗi ngay dưới mục kèm ngày. Mục mới chưa test thì thêm vào đúng nhóm.

## Ưu tiên 1 — Bất biến (hỏng là lỗi nặng)

### Kéo, ném, nhấc lên
- [ ] Bấm giữ rồi di chuột < ~6 px: không vào kéo. Di xa hơn: nhấc lên mượt, không nháy.
- [ ] Khi kéo mèo đung đưa như con lắc; lắc nhẹ không chóng mặt, lắc mạnh lâu mới chóng mặt, thả ra hết dần.
- [ ] Ném mạnh/nhẹ: bay, rơi, nảy tự nhiên. Debug view dòng Frame ~60 fps, max gap < ~35 ms.
- [ ] Kéo khi mèo đang chuyển tư thế (ngồi/ngủ/thức), đang làm động tác: ngắt ngay, không kẹt tư thế.

### Ngủ và thức
- [ ] Ngủ thường: đúng tư thế; rê chuột qua, AI, bong bóng không đánh thức; tự thức sau ~10 s; waking ~1.2 s rồi Idle; sau đó ~30 s không ngủ lại.
- [ ] Click trái mèo đang ngủ: thức ngay và vẫn kéo được. Chuột phải: thức ngay và mở menu.
- [ ] Pause khi đang ngủ: timer ngủ đứng yên; Resume chạy tiếp.
- [ ] Away nap: không chạm máy 5 phút → ngủ mãi; chạm chuột → thức (1.2 s) và chào "Sen về rồi!…" (bật Speech + AI). Click mèo đang away nap vẫn thức và kéo được. Pause trong lúc away nap.

### Cảm xúc
- [ ] Click dồn đến Giận: tai cụp, đuôi quất. Không đụng 3 phút: buồn vài giây rồi bình thường; click giữa lúc buồn thì hết ngay.
- [ ] Tự điều khiển cảm xúc: bật, chọn từng cảm xúc; vuốt/click dồn không đổi cảm xúc; để yên vài phút không buồn/ngủ tự động; kéo mạnh vẫn chóng mặt; tắt công tắc thì về bình thường ngay; mở lại app thì công tắc tắt.

### Không có Python
- [ ] Tắt "Mèo thông minh (AI)" ở khay: mèo vẫn đi, ngồi, ngủ, cảm xúc bình thường; bật lại thì AI chạy tiếp.

## Ưu tiên 2 — Tính năng mới

### Đi trên cửa sổ
- [ ] Để một cửa sổ không phóng to (VS Code, Explorer); ném/thả mèo lên mép trên: đáp đúng mép, đi qua lại, quay đầu ở mép.
- [ ] Kéo cửa sổ: mèo đi theo. Thu nhỏ/phóng to/đóng: mèo rơi.
- [ ] Cửa sổ thấp gần mèo: tự nhảy lên (chờ 1–2 phút). Cửa sổ cao chạm đáy: đi tới cạnh, trèo, lên đỉnh; click khi đang trèo: buông.
- [ ] Mèo ngủ trên cửa sổ rồi thu nhỏ cửa sổ: rơi mà vẫn ngủ.
- [ ] Chơi bóng/laser khi mèo trên cửa sổ: đi ra mép và nhảy xuống. Tắt "Mèo leo lên cửa sổ": mèo rơi.
- [ ] Chrome/VS Code (thanh tiêu đề tự vẽ): mép có lệch không. Xem dòng Windows trong debug view.

### Đèn laser và quả bóng
- [ ] Laser: rê chuột trên màn hình có mèo: nhìn, đuổi, dừng gần thì rình rồi vồ; rê cao thì chỉ nhìn; rê qua người mèo không tính vuốt; sang màn hình khác chấm ẩn; click/chuột phải mèo thì tắt; tự tắt sau 60 s; click app khác trong lúc chơi vẫn được.
- [ ] Bóng: nảy/lăn và khi click bóng còn giật/lệch không. Chơi bóng với Balanced/Playful/Curious/Calm xem quan sát/hạ người/đánh/nghỉ tự nhiên không.

### Quiet và fullscreen
- [ ] Bật Quiet, vuốt/cho ăn: có câu phản ứng (nếu bật Speech) nhưng không tự đi. Chơi bóng/laser vẫn chạy, hết trò thì đứng yên. Bật Quiet giữa lúc chơi: trò dừng.
- [ ] Video YouTube fullscreen, game, PowerPoint trình chiếu trên màn hình có mèo: đứng yên, im, không bong bóng, không bắt đầu được trò chơi; thoát thì bình thường. Maximize thường KHÔNG tính. Fullscreen ở màn hình phụ khi mèo ở màn hình chính: không tính.

### Làm ngay
- [ ] Thử từng động tác (cả khi Quiet); chuột phải lúc ngủ rồi chọn (thức 1.2 s rồi làm); Ngủ ngay sau khi vừa thức; click/kéo giữa lúc làm thì ngắt; Pause thì menu mờ.

### Giao diện
- [ ] Menu chuột phải: icon rõ, bo góc, bóng, nhóm hợp lý, menu con Làm ngay/Cảm xúc/Chỗ nghỉ/Khác.
- [ ] Cài đặt từ menu mèo và từ khay; đổi từng tab rồi Hủy (không đổi gì) và Lưu; tắt Âm thanh/Mèo biết nói/Nhắc nghỉ thì ô phụ mờ; Nghe thử; Esc đóng. Xem ở DPI 125–150%.
- [ ] Ngôn ngữ: English → Save: menu mèo, menu khay, tooltip khay, About, Nhật ký, Behavior tuning, hộp thoại Quên đều tiếng Anh; mèo nói tiếng Anh. Đổi lại Tiếng Việt. Debug view luôn tiếng Anh.
- [ ] Bộ lông/phụ kiện: đổi, Save, xem khi đi, ngủ, kéo, ném, ăn, vuốt; mèo đen (mắt/miệng rõ không); mũ khi kéo sát mép trên.
- [ ] Nhật ký (AI bật và tắt), đổi ngôn ngữ, rê lên biểu đồ xem tooltip; sau vài ngày xem biểu đồ bond/giờ chơi.
- [ ] Behavior tuning: Save / đóng không Save / Khôi phục mặc định; khởi động lại xem có giữ.
- [ ] Danh sách bộ lông (đọc từ coats.json): đủ 10, tên và chấm màu đúng ở cả tiếng Việt/English.
- [ ] Sinh nhật: chọn ngày + tháng = hôm nay → Lưu → thoát và mở lại mèo (Mèo biết nói + AI bật): câu chào là câu chúc sinh nhật; để lâu thỉnh thoảng nói thêm. Bỏ chọn tháng → Lưu → mở lại: chào bình thường. Mở lại Cài đặt thấy đúng ngày đã lưu.
- [ ] Phụ kiện mới (Mũ Noel, Mũ phù thủy, Hoa mai, Đèn lồng): chọn trong Cài đặt, xem khi đi, ngủ, kéo, ném, trèo cửa sổ; mũ không bị cắt ở mép trên.
- [ ] Tự đội đồ ngày lễ: đặt sinh nhật = hôm nay, mở lại mèo (AI bật): mèo đội mũ tiệc dù đã chọn phụ kiện khác; Cài đặt vẫn hiện phụ kiện đã chọn. Tắt ô → Lưu: về phụ kiện đã chọn. Tắt AI ở khay: bỏ mũ.
- [ ] Ngày lễ (khi tới dịp): Halloween 31/10, Noel 24–25/12, Tết, Trung thu: câu chào và câu nói vu vơ theo dịp.

### Chế độ chiến đấu (Battle Mode, 2026-10-16)
- [ ] Mặc định tắt; mở lại app không tự vào trận. Menu chuột phải → Chế độ chiến đấu → Bắt đầu trận. Khi mèo ngủ (chuột phải đánh thức nên thử bằng Pause/khay), đang kéo, đang trên cửa sổ, đang chơi bóng/laser: mục Bắt đầu mờ kèm lý do, mèo không bị đánh thức/ép.
- [ ] Click xuyên: trong trận, click/gõ vào app bên dưới ở khoảng trống của dải sàn và quanh HUD vẫn được; con trỏ đang gõ ở app khác không bị mất focus khi click slime/HUD; không có cửa sổ mới trên taskbar/Alt+Tab.
- [ ] Mèo tự đi tới slime gần nhất, vung kiếm, slime trúng đòn nhấp trắng; slime phồng lên + dấu "!" đỏ trước khi đánh; hạ slime thì bụi trắng, không máu.
- [ ] Click slime: mũi tên vàng + vòng vàng, mèo đổi sang đánh con đó; con đó chết thì marker mất và mèo tự chọn con khác. Xem marker/HP/HUD trên nền desktop sáng và tối.
- [ ] Đẩy quái: bấm nút → nút xanh, slime có vòng xanh đứt, con trỏ dấu cộng; click slime → bị đẩy lùi + choáng, mục tiêu không đổi; nút hiện đếm ngược ~5 s; bấm lại nút hoặc chuột phải dải sàn để hủy chọn.
- [ ] Kiếm: đúng tay, đổi hướng khi mèo quay đầu; khi vung, khi bị kéo (đung đưa, chóng mặt), khi ném/bay, khi thua (ngồi, sao quay) và thắng (giơ kiếm) không bị cắt mép, không che mặt khó chịu.
- [ ] Kéo cứu nguy: bấm giữ mèo khi slime đang "!": slime thôi đánh; kéo/đung đưa/chóng mặt/ném như cũ; khi đang cầm HP không giảm, không ra thêm slime, đợt không chuyển; thả xuống thì sau khi chạm đất mèo mới đánh tiếp, ~1.5 s đầu không bị đánh.
- [ ] Pause/Resume trên HUD và trong menu; mở menu giữa trận thì trận dừng, đóng menu thì chạy tiếp; trận đã Pause thì đóng menu vẫn Pause. Pause ở khay: mọi thứ đứng yên, bỏ Pause chạy tiếp không "giật" sát thương.
- [ ] Fullscreen (video/game) trên màn hình có mèo: trận dừng, dải sàn + HUD ẩn; thoát fullscreen thì hiện lại ở trạng thái dừng, bấm Tiếp tục mới chạy.
- [ ] Thắng 3 đợt: "Chiến thắng!" rồi tự đóng; thua: mèo ngồi choáng rồi tự đóng; sau đó mèo vẫn bình thường (không buồn/giận, ăn/chơi được, AI chạy lại). Bắt đầu/kết thúc trận nhiều lần liên tiếp, thoát app giữa trận: không còn dải sàn/HUD sót lại.
- [ ] Âm thanh: tiếng chạm khi trúng/đẩy, tiếng giật mình khi bị đánh, vui khi hết đợt/thắng, cáu khi thua; không dồn dập; tắt Âm thanh/volume 0 thì im.
- [ ] DPI 125–150% và màn hình tọa độ âm/màn hình phụ (nếu có): dải sàn nằm đúng đáy vùng làm việc, slime đúng sàn, HUD giữa phía trên; kéo mèo sang màn hình khác rồi thả: chiến trường chuyển theo. Đổi vị trí taskbar giữa trận.
- [ ] Tắt AI ở khay rồi chơi một trận: chạy bình thường.

## Ưu tiên 3 — Cảm nhận và dài hạn

### Động tác và nhịp
- [ ] Vuốt liên tục vài giây: ngồi nhồi bột rồi về bình thường; bond ≥ 60 có thể lăn ngửa.
- [ ] Để chạy lâu: thỉnh thoảng đuổi đuôi, cào móng, hắt xì, liếm chân, rửa mặt, duỗi người; không quá dày; click/kéo/Pause ngắt được.
- [ ] Đi/chậm lại/quay đầu, chuẩn bị nhảy/tiếp đất, ngồi/ngủ/thức trông mượt.
- [ ] Needs/Thought trong debug view: cho ăn khi đói, chơi bóng khi chán; bong bóng quá dày hay thưa.

### Mèo nói
- [ ] Tần suất Ít/Vừa/Nhiều có dễ chịu; câu sau vuốt/cho ăn/ném mạnh; câu cảm ơn hiện ngay sau khi ăn xong.
- [ ] Câu chào khi mở lại app; nếu mèo ngủ lúc mở thì chào sau khi thức.
- [ ] Bong bóng sát mép màn hình, trên màn hình phụ/DPI khác.
- [ ] Đọc lại `ai/lines.json` để chỉnh giọng văn.

### Âm thanh
- [ ] Nghe 9 preview, tiếng chạm bóng/ăn xong/thức dậy: dễ chịu, âm lượng, tần suất; không chồng tiếng.

### Nhắc nghỉ, tự học, khởi động
- [ ] Nhắc nghỉ 30 phút: dùng máy bình thường; xem dòng Break reminder; khi nhắc mèo nói + duỗi; tắt Speech vẫn nhắc; rời máy ≥5 phút thì reset.
- [ ] Sau vài ngày xem "Learned likes/dislikes"; thử nút Quên.
- [ ] Bật/tắt Khởi động cùng Windows (bản publish), đăng xuất/đăng nhập lại; About; mở thư mục logs.

### Bộ nhớ và log
- [ ] Để mèo chạy cả ngày, mở debug view xem dòng Memory: `heap` phải lên xuống chứ không tăng mãi; `committed` có thể lớn hơn heap.
- [ ] Đo dài: `scripts\perf.ps1 -Minutes 120 -Interval 10 -Label long` khi đang dùng máy bình thường.
- [ ] Log (`%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log`): mỗi dòng "started" có dòng "exited" đi kèm; nếu thiếu thì lần đó bị crash hoặc bị kill.

## Chưa làm được (cần phần cứng)
- [ ] Nhiều màn hình vật lý mixed-DPI: kéo/ném qua màn hình, ngắt/kết nối màn hình, khôi phục vị trí và chỗ nghỉ.
