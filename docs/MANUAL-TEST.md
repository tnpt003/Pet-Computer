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
