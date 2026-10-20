# Checklist test thủ công

Dùng cho những gì test tự động không thay được: cảm giác chuyển động, âm thanh, giọng văn, nhiều màn hình.
Chạy bản publish (`artifacts\win-x64\DesktopPet.exe`). Mở **Behavior debug view** (menu chuột phải → Khác) để xem trạng thái.
Tick `[x]` khi đạt; ghi lỗi ngay dưới mục kèm ngày. Mục mới chưa test thì thêm vào đúng nhóm.

## Ưu tiên 1 — Bất biến (hỏng là lỗi nặng)

### Kéo, ném, nhấc lên
- [ ] Bấm giữ rồi di chuột < ~6 px: không vào kéo. Di xa hơn: nhấc lên mượt, không nháy.
- [ ] Khi kéo mèo đung đưa như con lắc; lắc nhẹ không chóng mặt, lắc mạnh lâu mới chóng mặt, thả ra hết dần.
- [ ] Ném mạnh/nhẹ: bay, rơi, nảy tự nhiên. Debug view dòng Frame ~60 fps, longest gap < ~35 ms (khoảng cách thật dài nhất giữa 2 khung trong cửa sổ 1 s; từ 2026-10-18 không còn bị chặn ở 100 ms nên khựng dài sẽ hiện đúng).
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
- [ ] Mặc định tắt; mở lại app không tự vào trận. Menu chuột phải → Chế độ chiến đấu → Bắt đầu trận → bảng chuẩn bị → Sẵn sàng. (Đổi 2026-10-19: khi mèo ngủ/đang kéo/trên cửa sổ/đang chơi, bảng chuẩn bị vẫn mở nhưng nút Sẵn sàng mờ kèm lý do; chỉ Pause/fullscreen làm mục Bắt đầu mờ.) Mèo không bị đánh thức/ép.
- [ ] Click xuyên: trong trận, click/gõ vào app bên dưới ở khoảng trống của dải sàn và quanh HUD vẫn được; con trỏ đang gõ ở app khác không bị mất focus khi click slime/HUD; không có cửa sổ mới trên taskbar/Alt+Tab.
- [ ] Mèo tự đi tới slime gần nhất, vung kiếm, slime trúng đòn nhấp trắng; slime phồng lên + dấu "!" đỏ trước khi đánh; hạ slime thì bụi trắng, không máu.
- [ ] Click slime: mũi tên vàng + vòng vàng, mèo đổi sang đánh con đó; con đó chết thì marker mất và mèo tự chọn con khác. Xem marker/HP/HUD trên nền desktop sáng và tối.
- [ ] Đẩy quái: bấm nút → nút xanh, slime có vòng xanh đứt, con trỏ dấu cộng; click slime → bị đẩy lùi + choáng, mục tiêu không đổi; nút hiện đếm ngược ~5 s; bấm lại nút hoặc chuột phải dải sàn để hủy chọn.
- [ ] Kiếm: đúng tay, đổi hướng khi mèo quay đầu; khi vung, khi bị kéo (đung đưa, chóng mặt), khi ném/bay, khi thua (ngồi, sao quay) và thắng (giơ kiếm) không bị cắt mép, không che mặt khó chịu.
- [ ] Kéo cứu nguy: bấm giữ mèo khi slime đang "!": slime đứng yên giữ nguyên "!" (đổi 2026-10-18: không hủy đòn nữa); thả mèo ra xa ngoài tầm thì đòn trượt, thả lại chỗ cũ (khi hết bảo vệ) thì bị đánh; kéo/đung đưa/chóng mặt/ném như cũ; khi đang cầm HP không giảm, không ra thêm slime, đợt không chuyển; thả xuống thì sau khi chạm đất mèo mới đánh tiếp, ~1.5 s đầu không bị đánh.
- [ ] Pause/Resume trên HUD và trong menu; mở menu giữa trận thì trận dừng, đóng menu thì chạy tiếp; trận đã Pause thì đóng menu vẫn Pause. Pause ở khay: mọi thứ đứng yên, bỏ Pause chạy tiếp không "giật" sát thương.
- [ ] (2026-10-18) Ném mèo lên cao rồi bấm Tạm dừng trên HUD khi mèo đang bay: mèo đứng yên giữa không trung (không rơi tiếp), slime/bom/lấp lánh Cổ vũ/khiên cũng đứng yên; bấm Tiếp tục: mèo bay tiếp từ đúng chỗ đó, không giật/nhảy. Trong lúc dừng vẫn kéo được mèo (đung đưa bình thường); thả ra thì mèo đứng yên ở chỗ thả đến khi Tiếp tục mới rơi. Mở menu chuột phải lúc mèo đang bay: mèo cũng đứng yên tới khi đóng menu.
- [ ] (2026-10-18, đổi 2026-10-19) Vũ khí: chọn Búa trên bảng chuẩn bị rồi Sẵn sàng (thoát/mở lại mèo vẫn nhớ; menu chuột phải không còn mục Vũ khí). Trận dùng búa: mèo cầm búa gỗ, vung chậm và nặng, mỗi nhát slime bị đẩy lùi một chút (boss/slime đá lùi ít); HUD ghi "Mochi · Búa"; bảng kết quả ghi "Thường · Búa". Búa có bị cắt khi vung/khi bế/khi ngồi choáng không, hai hướng. So với kiếm: có khác lối chơi thật không, búa có quá mạnh/quá yếu không (chỉnh mục "hammer" trong "weapons" của battle.json, hoặc thử trước bằng file tùy chỉnh — docs/BATTLE-DATA.md).
- [ ] (2026-10-18, đổi 2026-10-19) Hướng dẫn trận đầu: lần đầu mở bảng chuẩn bị thấy bảng 4 thẻ (chưa có trận, chưa có slime); Xong/Bỏ qua đóng nó, bảng chuẩn bị vẫn còn; bấm Sẵn sàng khi hướng dẫn đang mở thì hướng dẫn đóng và trận chạy ngay; lần sau không tự hiện; nút Hướng dẫn trên bảng chuẩn bị mở lại được; con trỏ đang gõ ở app khác không mất focus; chữ dễ hiểu, đúng tiếng Việt/English; đọc rõ trên nền sáng/tối.
- [ ] (2026-10-18) Phản hồi khi bế mèo lúc slime đang "!": dấu "!" có vòng đỏ nhấp nháy, trên sàn có vòng đỏ đứt nét quanh slime; dòng chữ HUD ghi "slime đang giữ đòn: thả mèo ra ngoài vòng đỏ"; thả ngoài vòng: đòn trượt. Thả xuống có bảo vệ: HUD ghi "Được bảo vệ sau khi tiếp đất: còn N giây". Vòng có rõ không, có rối mắt không.
- [ ] (2026-10-18) Pause mèo ở khay giữa trận: dải sàn + HUD vẫn hiện nhưng bấm ô kỹ năng/click slime không có tác dụng (ô mờ); bấm Kết thúc trên HUD vẫn kết thúc trận. Đã Tạm dừng trận rồi Pause khay, bỏ Pause khay: trận vẫn dừng tới khi bấm Tiếp tục.
- [ ] (2026-10-18) Tạm dừng → mở menu → đóng menu: vẫn dừng. Tạm dừng rồi mở video fullscreen, thoát: vẫn dừng, phải bấm Tiếp tục. Mèo ở màn hình khác (nếu có) rồi bấm nút Tạm dừng trên HUD: không bị chạy tiếp; đưa mèo về vẫn dừng tới khi bấm Tiếp tục.
- [ ] (2026-10-18) Nhấc/thả liên tục tại chỗ (mỗi lần ~0.5 s): chỉ lần thả đầu có ~1.5 s an toàn; sau đó slime vẫn đánh trúng (đòn đang gồng được giữ qua các lần nhấc, không bị hủy). Cứu đúng lúc (nhấc khi thấy "!", thả xa ~2 thân mèo) vẫn né được. Thử cả với Vua Slime (vùng đỏ rộng hơn). Cảm giác có công bằng, có bị đánh "bất ngờ" lúc vừa thả không. Khi đang bế, dòng chữ dưới HUD ghi "kỹ năng chờ mèo xuống" và "thả xuống được 1.5 giây an toàn" hoặc "thả lúc này không có thêm bảo vệ (còn N giây)". Click thường vào mèo (không kéo): HUD không nháy, không được bảo vệ.
- [ ] (2026-10-18) Chọn Bom len/Đẩy rồi kéo mèo: lựa chọn bị hủy ngay, ô kỹ năng mờ, số giây hồi chiêu đứng yên khi đang bế/bay, chạy lại khi mèo chạm đất. Thả bom rồi nhấc mèo ngay: bom vẫn nổ. Bật Khiên/Cổ vũ rồi bế mèo lâu: hiệu ứng vẫn hết đúng giờ. Thắng trận có lúc Tạm dừng lâu: thời gian trên bảng kết quả không tính lúc dừng.
- [ ] Fullscreen (video/game) trên màn hình có mèo: trận dừng, dải sàn + HUD ẩn; thoát fullscreen thì hiện lại ở trạng thái dừng, bấm Tiếp tục mới chạy.
- [ ] Thắng 3 đợt: "Chiến thắng!" rồi tự đóng; thua: mèo ngồi choáng rồi tự đóng; sau đó mèo vẫn bình thường (không buồn/giận, ăn/chơi được, AI chạy lại). Bắt đầu/kết thúc trận nhiều lần liên tiếp, thoát app giữa trận: không còn dải sàn/HUD sót lại.
- [ ] Âm thanh: "vút" khi vung kiếm/đẩy, "bộp" khi slime trúng đòn, "bụp" khi slime biến mất; giật mình khi bị đánh, vui khi hết đợt/thắng, cáu khi thua; nghe có dễ chịu, có quá dày/quá to không; tắt Âm thanh/volume 0 thì im.
- [ ] Hết đợt 1 và 2: HUD ghi "+15 HP" và thanh HP tăng; đợt cuối không hồi.
- [ ] Bảng kết quả sau khi thắng/thua (đổi 2026-10-19, xem mục Trang bị bên dưới): bấm "Kết thúc" giữa trận thì không có bảng.
- [ ] Thông báo từ chối: mở menu khi mèo rảnh, đợi mèo bị kéo/ngủ rồi mới bấm "Bắt đầu trận" (khó tái hiện): thông báo nhỏ trên đầu mèo, click xuyên, tự mất sau 2.5 s, không có hộp thoại.
- [ ] DPI 125–150% và màn hình tọa độ âm/màn hình phụ (nếu có): dải sàn nằm đúng đáy vùng làm việc, slime đúng sàn, HUD giữa phía trên. Đổi vị trí taskbar hoặc DPI của màn hình đang đánh giữa trận: chiến trường dời theo. (Mèo sang màn hình khác: xem mục Nhiều màn hình bên dưới.)
- [ ] Tắt AI ở khay rồi chơi một trận: chạy bình thường.
- [ ] HUD kiểu game: chân dung mèo đúng bộ lông/phụ kiện; thanh HP đổi màu xanh/vàng/đỏ, vệt cam chạy theo khi mất máu; chấm đợt + vương miện; banner "ĐỢT n"/"BOSS!"/"CHIẾN THẮNG!"; rê chuột lên ô kỹ năng thấy mô tả ở dòng gợi ý dưới HUD; HUD không che việc đang làm quá nhiều.
- [ ] 5 kỹ năng: Đẩy; Cá hồi máu (+25 xanh bay lên trên mèo, đầy máu thì không tốn); Cổ vũ (lấp lánh vàng, đánh nhanh hơn rõ); Bom len (chọn ô → click slime, cuộn len rơi rồi nổ, trúng cả quái gần đó); Khiên (bong bóng quanh mèo, slime đánh không mất máu). Vòng quét hồi chiêu + số giây; bấm lại ô hoặc chuột phải để hủy chọn.
- [ ] Mèo chạy nhanh hơn trong trận (bước chân nhanh, không trượt); hết trận đi bộ lại bình thường.
- [ ] Đợt 2–3 có Slime nhanh (xanh dương, nhỏ, có vệt gió) và Slime đá (xám, to, vết nứt, khó đẩy): nhìn rõ trên nền sáng/tối; độ khó có hợp lý không.
- [ ] Nhiều màn hình (nếu có): ném/kéo mèo sang màn hình khác giữa trận: trận dừng, có thông báo cạnh mèo, chiến trường + HUD vẫn ở màn hình cũ; đưa mèo về: trận chạy tiếp. Bấm Tạm dừng khi mèo ở màn hình khác rồi đưa về: vẫn dừng.
- [ ] (2026-10-18, cần 2 màn hình) Đang đánh ở màn hình phụ, rút cáp/tắt màn hình phụ (hoặc Win+P → chỉ màn hình chính): trong ~1 s dải sàn + HUD chuyển sang màn hình còn lại, slime nằm trong sàn mới, HP/đợt/hồi chiêu giữ nguyên, trận dừng kèm thông báo "Màn hình chiến trường đã ngắt…", bấm Tiếp tục thì đánh tiếp. Thử cả lúc đang kéo mèo (mèo không bị dời, vẫn kéo tiếp được), màn hình tọa độ âm và DPI khác nhau. Log có dòng "Battle monitor … is gone".
- [ ] Nhấc mèo khi có quái: quái bò theo mèo dọc sàn nhưng không gồng/đánh; thả xuống có 1.5 s an toàn (tối đa 1 lần mỗi ~5 s mèo ở dưới đất).
- [ ] Đẩy theo hướng: chọn Đẩy, rê lên nửa trái/phải slime thấy mũi tên xanh đổi hướng; click nửa trái → bay sang phải, nửa phải → sang trái (kể cả về phía mèo).
- [ ] Rê chuột lần lượt qua 5 ô kỹ năng và nút Tạm dừng/Kết thúc: dòng chữ dưới HUD đổi đúng theo ô đang rê, rời ra thì trở lại câu gợi ý; HUD không co giãn/nhảy khi đổi chữ.
- [ ] Combo: để mèo đánh trúng liền 3 nhát: nhát 3 có vệt kiếm vàng, chùm sao vàng + chữ "COMBO!", số sát thương gấp đôi, slime bị hất lùi (boss/slime đá lùi ít); nhấc mèo giữa chuỗi thì nhát sau lại là nhát thường. Combo có quá mạnh/quá dễ không.
- [ ] Chuỗi trên HUD: đánh trúng 1 nhát thì cạnh tên mèo hiện "×1", nhát 2 hiện "×2" màu vàng, nhát 3 (combo) hoặc trượt/nghỉ lâu/nhấc mèo thì chữ biến mất; HUD không xê dịch. Có dễ nhận ra không.
- [ ] Kết quả trận sang Python (bật Mèo biết nói): thắng/thua xong một trận, mèo nói một câu về trận (kỷ lục mới > thắng > thua) khi đứng yên; Nhật ký có dòng "Chiến đấu: thắng · thua · combo"; bấm Kết thúc giữa trận không tính.
- [ ] HUD né con trỏ: rê chuột lên chân dung/thanh máu/dòng chữ của HUD: HUD mờ hẳn, click được cửa sổ app bên dưới; rê sang ô kỹ năng hoặc nút Tạm dừng/Kết thúc: HUD rõ lại, bấm được, chữ mô tả vẫn hiện; rời HUD: bình thường. Thử cả DPI 125–150% nếu có.
- [ ] Độ khó (đổi 2026-10-19: chọn trên bảng chuẩn bị, nút đang chọn màu xanh): thoát và mở lại mèo vẫn nhớ mức đã bấm Sẵn sàng. Dễ: slime ít máu (số HP trên thanh boss ~300), đánh nhẹ, chậm hơn; Khó: ngược lại (boss ~520 HP). Cảm giác 3 mức có khác rõ và hợp lý không.
- [ ] Kỷ lục (đổi 2026-10-19: tách theo vũ khí + độ khó): thắng/thua vài trận ở một mức: bảng kết quả ghi vũ khí · độ khó, dòng vàng "Kỷ lục mới" khi phá kỷ lục, dòng "Kỷ lục (mức): nhanh nhất … · nhiều nhất … slime · thắng … lần"; đổi mức khác thì kỷ lục riêng. Thoát và mở lại mèo: kỷ lục còn. Bấm "Kết thúc" giữa trận: không tính.
- [ ] Hiệu năng/mượt: slime và hiệu ứng chạy 30 khung/giây (mèo vẫn 60): nhìn có giật không; khi Tạm dừng, debug view (dòng Frame) và Task Manager thấy CPU gần như không đổi so với lúc không chơi. Lúc boss gồng vẫn click được app bên dưới vùng đỏ.
- [ ] Boss Vua Slime ở đợt cuối: to, vương miện; vùng đỏ trên sàn khi gồng; dưới nửa máu gọi 2 slime; dưới 30% đổi màu đỏ, nhanh hơn; Đẩy chỉ xê dịch ít; hạ boss thì bụi lớn, bảng kết quả ghi "Đã hạ Vua Slime". Thử trên màn hình hẹp/DPI cao xem boss có bị cắt đầu không.

#### Bảng chuẩn bị và dữ liệu tùy chỉnh (2026-10-19)
- [ ] Menu chuột phải → Chế độ chiến đấu chỉ còn Bắt đầu trận (khi chưa đánh) / Tạm dừng / Kết thúc. Bắt đầu trận: bảng "Chuẩn bị trận" hiện ở giữa phía trên màn hình có mèo; chưa có dải sàn, slime, HUD.
- [ ] Bảng chuẩn bị: bấm Kiếm/Búa và Dễ/Thường/Khó: nút đang chọn đổi màu, dòng gợi ý vũ khí đổi theo; đang gõ ở app khác (Notepad) rồi bấm các nút: con trỏ gõ không mất focus, phím gõ không vào bảng; không có cửa sổ mới trên taskbar/Alt+Tab. Đọc rõ trên nền sáng/tối, tiếng Việt/English.
- [ ] Hủy: bảng đóng, mèo đi lại bình thường sau ~2 s, không có kết quả/kỷ lục; mở lại thấy lựa chọn cũ (lựa chọn chỉ lưu khi bấm Sẵn sàng).
- [ ] Mở bảng khi mèo đang ngủ: mèo ngủ tiếp (không bị đánh thức), Sẵn sàng mờ + "Chưa thể bắt đầu: Mèo đang ngủ."; mèo tự thức thì nút sáng lại. Kéo mèo khi bảng đang mở: nút mờ, thả xuống thì sáng. Đang mở bảng: mèo đứng yên, không tự đi/ngủ/nói; để máy ~5 phút: mèo không ngủ "away nap".
- [ ] Bấm Sẵn sàng nhanh 2 lần (double click): chỉ một trận, một dải sàn, một HUD. Trận dùng đúng vũ khí/độ khó đã chọn (HUD ghi tên vũ khí, bảng kết quả ghi độ khó).
- [ ] Pause ở khay khi bảng đang mở: Sẵn sàng mờ; bỏ Pause: sáng lại. Mở video fullscreen: bảng ẩn; thoát fullscreen: bảng hiện lại. Màn hình DPI 125–150%: bảng đúng vị trí, chữ không bị cắt.
- [ ] Dữ liệu tùy chỉnh: chép docs/examples/battle.json vào %LOCALAPPDATA%\DesktopPet\battle.json, thêm "waves" có stone_slime (theo ghi chú trong file), mở bảng chuẩn bị: có dòng "Đang dùng dữ liệu trận tùy chỉnh"; vào trận gặp Slime đá tảng (xám, to, nứt). Sửa file thành sai (ví dụ "maxHp": -5) rồi mở lại bảng: dòng vàng báo lỗi kèm tên khóa sai, trận dùng dữ liệu gốc, mèo không crash. Sửa file giữa trận: trận đang chạy không đổi. Xóa file khi xong.

#### Trang bị và phong cách chiến đấu (2026-10-19, đợt 15)
- [ ] Bảng chuẩn bị mới: bên trái mèo (đúng bộ lông/phụ kiện) cầm vũ khí đang chọn; bên phải 2 thẻ Kiếm/Búa có hình, mô tả, 4 thanh chỉ số kèm số; thẻ đang chọn có viền trắng + dấu ✓; độ khó có dấu ✓; "Cách chơi" mở hướng dẫn; Đóng / Sẵn sàng. Đổi vũ khí: mèo xem trước đổi ngay. Chữ đọc rõ trên nền sáng/tối, tiếng Việt/English, DPI 125–150% không bị cắt.
- [ ] Xem thử: mèo xem trước vung một nhát đúng nhịp (kiếm nhanh; búa giơ lên, khuỵu và rung một chút rồi đập mạnh), có dấu đòn (tia trắng / bụi dưới sàn), có tiếng vút nếu âm thanh đang bật (tắt âm thì im). Mèo thật không động đậy, không có trận. Bấm Xem thử rồi Đóng ngay: không còn gì chạy.
- [ ] Trong trận: kiếm trúng ra tia trắng, búa trúng ra bụi + vòng trên sàn và slime lùi xa hơn; búa giơ lâu nên slime chạy ra khỏi tầm thì nhát đập trượt. Nhát chém/đập trong hình có trùng lúc slime bị trúng không. Kiếm có thật sự hợp lối đánh áp sát/combo, búa hợp lối giữ khoảng cách không; búa có quá mạnh/yếu không (số liệu mới: tầm 76, đẩy 170, chạm đích 0.45 s, vung 0.7 s).
- [ ] Bảng kết quả: hình mèo thắng (giơ vũ khí) / thua (ngồi choáng), "Vũ khí · Độ khó", số slime + thời gian, HP nhỏ, kỷ lục của đúng vũ khí + độ khó. Không tự đóng nữa. "Chơi lại": trận mới cùng vũ khí/độ khó ngay (bấm nhanh 2 lần vẫn 1 trận); "Đổi trang bị": bảng chuẩn bị với lựa chọn vừa dùng; "Về desktop": đóng. Fullscreen/đưa mèo về: bảng đóng.
- [ ] Kỷ lục cũ: máy đã có battle-records.json từ trước (chép dự phòng file trước khi thử): sau trận đầu bảng kết quả có dòng mờ "Trước phân loại trang bị (mức): …" giữ số cũ; kỷ lục kiếm và búa tính riêng từ 0. Chơi với file battle.json tùy chỉnh: kỷ lục ghi "dữ liệu tùy chỉnh", tách khỏi kỷ lục thường.

#### Phiêu lưu — mốc 2 (2026-10-20)
Nên chép dự phòng %LOCALAPPDATA%\DesktopPet\adventure-progress.json (nếu đã có) trước khi thử.
- [ ] Chuột phải → Chế độ chiến đấu: có "Phiêu lưu" ở trên "Bắt đầu trận". Bấm Phiêu lưu: bản đồ ở giữa phía trên màn hình, có 5 nút màn nối bằng đường. Lần đầu chỉ màn 1 xanh, các màn khác xám có ổ khóa. Gõ phím ở app khác vẫn bình thường (bản đồ không lấy focus), không có nút trên taskbar. Chữ không bị cắt ở tiếng Việt/English, DPI 125–150%.
- [ ] Click màn bị khóa: dòng "Chưa mở — thắng màn … trước", nút Vào màn mờ. Click màn 1 → Vào màn: bảng chuẩn bị có tiêu đề "Màn 1 · Làm quen" + mục tiêu, nút bên trái ghi "Bản đồ". Bấm Bản đồ: quay lại bản đồ.
- [ ] Màn 1: tối đa 2 slime cùng lúc, 2 đợt. Thắng: bảng kết quả ghi "Màn 1 · Làm quen · Vũ khí · Độ khó", dòng vàng "Đã mở: Màn 2 · Giải vây", nút chính "Màn tiếp", bên dưới là Chơi lại / Bản đồ / Về desktop. Màn tiếp mở bảng chuẩn bị màn 2 với vũ khí vừa dùng.
- [ ] Thua một màn (có thể bỏ mặc mèo ở độ khó Khó): không mở màn mới, nút chính là "Chơi lại". Thua màn đã thắng trước đó: màn đó vẫn được tính là đã thắng.
- [ ] Kết thúc trận giữa chừng (HUD → Kết thúc, hoặc thoát app): không có bảng kết quả, không tính gì. Thoát app rồi mở lại → Phiêu lưu: các màn đã thắng vẫn vàng, màn kế vẫn mở, không thắng trùng.
- [ ] Đi hết 5 màn tới Vua Slime: thắng màn 5 thì có dòng "Hoàn thành chương!" và không có nút Màn tiếp. Bấm Bản đồ: cả 5 màn vàng, có số lần thắng và thời gian nhanh nhất.
- [ ] Trận tự do (Bắt đầu trận) vẫn như cũ; kỷ lục của trận tự do không đổi sau khi chơi Phiêu lưu.
- [ ] Mở bản đồ khi mèo đang ngủ: mèo vẫn ngủ. Để máy yên 5 phút khi bản đồ mở: mèo không ngủ away nap. Mở app fullscreen: bản đồ ẩn, đóng app thì hiện lại.
- [ ] Có file %LOCALAPPDATA%\DesktopPet\battle.json tùy chỉnh: bản đồ ghi rõ "không lưu tiến trình và kỷ lục"; thắng màn không mở màn sau.

#### Phiêu lưu — mốc 3: nâng cấp và sinh tồn (2026-10-20)
- [ ] Màn 2 (Giải vây): dọn xong đợt 1 thì hiện bảng "Chọn một nâng cấp cho lượt này" phía trên sàn, 3 thẻ có hình + tên + tác dụng + "Đã có x/y". Trận đứng yên (slime, đồng hồ, kỹ năng). Gõ phím ở app khác vẫn bình thường. Click một thẻ (viền trắng) rồi Xác nhận: bảng đóng, trận chạy tiếp, HUD có dòng vàng nhỏ ghi nâng cấp. Bấm Xác nhận nhanh 2 lần: chỉ lấy 1.
- [ ] Hình 6 thẻ (khiên, kiếm, đồng hồ, mũi tên tốc độ, mũi tên đẩy, con cá) nhìn rõ trên nền tối; chữ tiếng Việt không bị cắt ở DPI 125–150%.
- [ ] Đang chọn thẻ mà bấm Tạm dừng trên HUD rồi Xác nhận: trận vẫn tạm dừng cho tới khi bấm Tiếp tục. Mở app fullscreen lúc đang chọn: bảng ẩn, đóng app thì hiện lại. Bấm Kết thúc lúc đang chọn: bảng đóng.
- [ ] Nhanh chân: mèo chạy nhanh hơn thấy rõ; Cá nướng không xuất hiện khi HP đầy. Chơi lại màn: không còn nâng cấp cũ. Màn 1 không có thẻ. Trận tự do không có thẻ.
- [ ] Màn 3 (Cầm cự): HUD ghi "Sống sót: còn 1:15" đếm ngược; bế mèo thì đồng hồ dừng; hết giờ mà còn HP thì thắng; bảng kết quả không có "thắng nhanh nhất". Màn có khó quá/dễ quá không (75 giây, 3 đợt lặp lại)?
- [ ] Bảng kết quả màn có dòng "Nâng cấp lượt này: …".

#### Phiêu lưu — mốc 4: quái hồi máu, Vua Slime theo pha, phần thưởng (2026-10-20)
- [ ] Màn 4: slime thảo dược (xanh ngọc, dấu "+" trắng). Khi nó gồng: dấu "+" xanh nhấp nháy trên đầu nó, vòng xanh đứt nét dưới con được hồi; sau đó con kia hiện dấu cộng bay lên và "+15". Banner "Quái hồi máu! Hạ nó trước" chỉ một lần. Đẩy nó lúc đang gồng: không hồi. Màn có thắng được không, có kéo dài vô tận không.
- [ ] Màn 5: Vua Slime gọi 2 slime ở ~75% máu, 1 slime thảo dược ở ~50%, slime nhanh ở ~40%, nổi giận ở ~30%; mỗi pha có banner, không gọi lại; không bao giờ quá 4 quân của nó cùng lúc. Trận tự do: Vua Slime vẫn như cũ.
- [ ] Thắng màn 3: bảng kết quả có dòng vàng "Phần thưởng mới: Mũ slime — đội trong Cài đặt → Mèo"; mèo không tự đội. Cài đặt → Mèo → Phụ kiện: có Mũ slime (trước đó không có), chọn → Lưu: mèo đội, xem khi đi/ngủ/kéo/trèo. Thắng màn 5: Vương miện Vua Slime. Thắng lại: không có dòng phần thưởng nữa. Bản đồ ghi "Phần thưởng: … ✓ đã nhận".
- [ ] Bấm Kết thúc (HUD hoặc menu) giữa trận màn: về bản đồ đúng màn đó, không tính gì. Trận tự do: Kết thúc về desktop như cũ.
- [ ] Đã thắng màn 3 bằng bản trước (8b93d25/9225d77): mở bản đồ là có Mũ slime trong Cài đặt (nhận bù).

#### Phiêu lưu — mốc 5: mẹo theo màn, cảnh báo lưu, bản 1.1.0-beta (2026-10-20)
- [ ] Lần đầu vào màn 2–5: hiện thẻ "Mẹo · Màn N · …" phía trên sàn, trận đứng yên tới khi bấm Xong/Bỏ qua; lần sau không hiện nữa. Bảng chuẩn bị của màn có link "Mẹo màn" mở lại thẻ.
- [ ] HUD ghi "Tên mèo · Vũ khí · Màn N"; lúc bắt đầu có banner tên màn.
- [ ] Giới thiệu (Khác → Giới thiệu) ghi 1.1.0-beta.
- [ ] (Tùy chọn) Đặt adventure-progress.json chỉ đọc (Properties → Read-only) rồi thắng một màn: bảng kết quả và bản đồ báo không lưu được; app không lỗi. Bỏ chỉ đọc sau khi thử.

#### Combat Depth — mốc 1: slime nhanh lao tới, mèo choáng 1,5 giây (2026-10-20)
- [ ] Màn 2 hoặc trận tự do: slime nhanh dừng cách mèo một đoạn, gồng (dấu "!" + vạch đỏ đứt nét có mũi tên trên sàn), rồi lao thẳng theo vạch, có vệt gió phía sau. Vạch rõ trên nền sáng và tối.
- [ ] Bị lao trúng: mất máu một lần, sao vàng xoay trên đầu + mắt chéo khoảng 1,5 giây; mèo đứng yên, không chém; sau đó đánh tiếp bình thường. Cú lao kế ngay sau đó (≤3 giây) chỉ mất máu, không choáng lại.
- [ ] Đang choáng: kéo mèo lên được ngay (không cần đợi), sao vẫn còn khi cầm; lắc mạnh khi đang choáng chỉ có **một** vòng sao; thả/ném rơi như thường; hết choáng khi đang bay thì mèo rơi xuống rồi mới đánh tiếp.
- [ ] Pause (HUD/menu) khi đang choáng: sao đứng yên, tiếp tục thì choáng chạy nốt phần còn lại. Bật Khiên trước cú lao: không mất máu, không choáng. Đẩy slime nhanh lúc nó gồng: không lao.
- [ ] Lắc mèo ngoài trận (Pet Mode): Chóng mặt vẫn như cũ (lắc nhẹ không chóng mặt). Kết thúc trận khi mèo đang choáng: về desktop không còn sao.

#### Combat Depth — mốc 2: nhóm thẻ, synergy, build cộng dồn (2026-10-20)
- [ ] Màn 2: mỗi lần chọn có 1 thẻ Phòng thủ, 1 Sức mạnh, 1 Tốc độ; dưới mô tả có dòng "Phòng thủ 0 → 1/3". Bị mất máu: Cá nướng thay một thẻ (không phải nhóm bạn đang theo nhiều nhất). Tiêu đề ghi "giữ sang màn sau khi thắng". Thẻ Tim to có hình trái tim, chọn xong HP tối đa +20 và thanh máu HUD đúng.
- [ ] HUD ghi "Phòng thủ 1/3 · …"; chọn thẻ thứ 3 cùng nhóm: thẻ đó ghi vàng "Mở Giáp gai!" trước khi chọn, chọn xong banner "★ GIÁP GAI!" và HUD có "★ Giáp gai".
- [ ] Giáp gai: quái đánh trúng mèo hiện số sát thương trên chính nó. Phá giáp: đòn combo (vệt vàng) mạnh hơn. Dư ảnh: cứ 4 nhát có thêm một tia sáng nhỏ và số sát thương phụ.
- [ ] Thắng màn 2 → bảng chuẩn bị màn 3 ghi "Build mang vào màn: …" đúng; vào màn 3 HUD đã có điểm nhóm từ đầu. Thua màn 3 rồi chơi lại: vẫn bắt đầu với build của màn 2 (không cộng thẻ của lượt thua). Màn 3 chỉ cho chọn tối đa 3 lần.
- [ ] Thắng lại màn 2 với build khác → màn 3 nhận build mới. Đóng app giữa chương rồi mở lại: build vẫn còn. Trận tự do: không có thẻ, không có build.

#### Combat Depth — mốc 3: đá chặn đòn, support đứng sau, King bật khiên (2026-10-20)
- [ ] Slime đá (màn 2–4 và trận tự do): khoảng 1/3 nhát kiếm/búa hiện "CHẶN" + mảnh đá xám, không mất máu, không bị đẩy; không bao giờ chặn 3 nhát liền. Bom len luôn trúng. Có Phá giáp (3 thẻ Sức mạnh): đòn combo vàng không bao giờ bị chặn. Chữ "CHẶN" đọc rõ trên nền sáng và tối.
- [ ] Slime thảo dược (màn 4–5): khi còn slime khác thì nó đứng phía sau, không gồng đánh mèo, vẫn hồi máu cho đồng đội; mèo đuổi thì nó lùi chậm và bị dồn ở mép màn hình. Hạ hết đồng đội thì nó tự đánh mèo. Click nó để mèo đánh nó trước vẫn được.
- [ ] Màn 5: Vua Slime ở 70% máu bật bong bóng khiên vàng, banner "… bật khiên! Hạ hết quân", chạy về mép xa mèo rồi gọi 2 slime. Một đòn lớn (bom) không làm máu tụt qua 70%. Đánh/bom vào nó hiện "BẤT TỬ", thanh máu HUD vàng "(khiên: hạ hết quân!)". Mèo tự đánh quân, không đứng đánh boss. Hạ hết quân → "KHIÊN VỠ!" một lần, boss quay lại đánh. 40%: khiên lần 2, gọi slime thảo dược + 2 slime nhanh. 30%: nổi giận.
- [ ] Trong khiên: kéo mèo lại sát boss thì nó vẫn dậm (vùng đỏ). Pause/menu giữa lúc boss chạy về mép: mọi thứ đứng yên. Thua giữa pha khiên: trận kết thúc bình thường, về Pet Mode không còn gì lạ. Chơi trên màn hình phụ/DPI khác: boss chạy đúng mép của màn hình đó.
- [ ] Âm thanh: bật khiên/khiên vỡ/chặn đòn dùng cue sẵn có, không dày hay chói.

#### Enemy Director — mốc 1: đội hình, threat, sinh tồn liên tục (2026-10-20)
- [ ] Màn 1: đợt 2 luôn có ít nhất một slime nhanh (vạch lao đỏ). Màn 2: có nhiều slime đá hơn trước.
- [ ] Màn 3 (Cầm cự): quái ra liên tục tới hết 75 giây, không có "Đợt 2/3"; không bao giờ quá 4 con cùng lúc. Bảng thẻ hiện ở khoảng giây 20, 40 và 60 của đồng hồ, đồng hồ đứng yên khi đang chọn. Quái cuối màn trâu hơn đầu màn.
- [ ] Màn 4: slime thảo dược và 3 slime đá hiện cùng lúc, sát nhau ở một mép, thảo dược đứng phía sau; nếu sàn còn quái cũ thì cả nhóm chờ, không hiện lẻ. Nhóm không hiện đè lên mèo.
- [ ] Log (`%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log`) có dòng "Battle started: … seed N." mỗi trận.
- [ ] Kỷ lục cũ của 5 màn không bị xóa nhưng không so với luật mới (rulesVersion tăng). Build đã lưu vẫn mang được vào màn sau.

#### Enemy Director — mốc 2: vai trò quái, chiến thuật (2026-10-20)
- [ ] Slime thảo dược: có mũ lá và túi thảo dược, không còn dấu + trên thân. Lá sáng và có hạt sáng bay tới đồng minh khi gồng hồi; hết lượng hồi thì lá cụp xỉn. Đẹp/đọc rõ trên nền sáng và tối.
- [ ] Đồng minh của nó tụt dưới ~1/3 máu ở xa: nó co người trong vòng lá, có dấu xanh ở điểm đến, rồi hiện ra sau đồng minh. Đẩy lúc đang co người thì hủy. Không teleport liên tục.
- [ ] Slime đá: thân rộng/thấp, khiên đá ở phía mặt (khiên mới: xem mục mốc 3), nứt thêm khi yếu. Mèo không tự đi xuyên qua đá; click quái phía sau thì mèo vẫn đánh đá trước (vòng xám dưới chân đá). Bế mèo thả ra sau đá thì đánh thẳng mục tiêu. Thả mèo đè lên đá: mèo bước ra mép gần, không nhảy xa. Đánh từ sau lưng đá không bao giờ "CHẶN"; đá cần một chút thời gian quay lại.
- [ ] Không thấy quá 2 quái cùng gồng một lúc; các đòn không trúng cùng một khung hình.
- [ ] Màn 4: một slime đá đứng chắn giữa mèo và slime thảo dược; ném mèo sang phía kia thì đá đổi chỗ sau một nhịp.
- [ ] Màn 5 đợt 1: mũi tên đỏ + banner "Quái tới từ phía sau!" rồi slime nhanh ra ở mép đó sau ~1 s. Đứng sát mép đó thì quái ra ở mép kia. Cầm cự sau giây 45: quái ra luân phiên hai mép, có mũi tên.
- [ ] Vua Slime khi bật khiên: vương miện có vòng sáng, banner "ra lệnh xung phong", một quân chạy nhanh có vệt vàng. Đẩy Vua lúc vương miện sáng thì hủy. Khiên vỡ: Vua choáng ~2 s (sao quanh đầu), đánh được.

#### Enemy Director — mốc 3: tinh anh, thẻ hiếm, cân bằng (2026-10-20)
- [ ] Màn 3–5 ở Thường/Khó: thỉnh thoảng có quái viền vàng + ngôi sao vàng ở góc lưng (không quá 1 con cùng lúc); lần đầu mỗi loại có banner "★ … tinh anh: …". Rõ trên nền sáng và tối, không che thanh máu/mũ lá/giáp. Màn 1–2 và mức Dễ không bao giờ có.
- [ ] Slime nhanh tinh anh gồng lâu hơn thấy rõ rồi lao nhanh hơn; vẫn né/Đẩy kịp. Slime đá tinh anh chặn chắc đòn đầu phía trước ("CHẶN"); Bom len vẫn trúng. Slime thảo dược tinh anh hồi lần đầu nhiều hơn (+30 thay vì +15).
- [ ] Thẻ hiếm: chơi vài lượt màn 2–5, đôi khi có thẻ viền vàng "★ Thẻ hiếm · một lần mỗi build" thay thẻ cùng nhóm. Đã lấy rồi thì không hiện lại. HUD và bảng chuẩn bị màn sau ghi "◆ <tên thẻ>".
- [ ] Mai rùa: đòn đầu trúng mèo không mất máu (mảnh đá xám ở mèo), ~12 s sau lại chặn được một đòn; bế mèo thì không hồi nhanh hơn. Đòn mở màn: nhát đầu vào mỗi quái số sát thương gấp đôi. Chạy đà: mèo chạy một quãng tới quái rồi chém, số to hơn và có vệt vàng combo; đứng yên chém thì bình thường.
- [ ] Slime đá cầm khiên đá hình huy hiệu (đầu phẳng, đáy nhọn, có bàn tay) ở phía mặt, không còn giống cái loa/vòi. Màn 4: slime thảo dược đứng sau 3 con đá vẫn thấy rõ (vẽ trên cùng, đá đè lên nó hơi mờ); click vào nó chọn đúng nó. Nền sáng và tối.
- [ ] Vua Slime (màn 5) ở 70%/40% máu: nhún một nhịp rồi nhảy vòng cung (có bóng dưới sàn) về mép đối diện nửa sân nó đứng; khi mèo ở nửa đó thì về mép kia, không bao giờ đáp cạnh mèo. Thử cả mèo trái và mèo phải. Đáp có bụi, không mất máu.
- [ ] Sau khi đáp: vòng tím có "!" ở chỗ quân sắp ra (~0,6 s), quân ra đúng các vòng: thảo dược sát Vua, đá đứng trước nó, slime nhanh ngoài cùng. Vương miện sáng thì có tia vàng nối tới con sắp được tăng tốc.
- [ ] Slime thảo dược hồi nhiều thấy rõ (màn 4–5 khoảng +60 mỗi lần); không cắt thì quái khó hạ hẳn, nên hạ nó trước hoặc Đẩy lúc lá sáng.
- [ ] Chiến thuật (màn 2–5): slime thảo dược gồng hồi thì con được hồi có vòng lá xanh, đánh vào hiện "MIỄN"; Đẩy slime thảo dược thì hết miễn ngay; mèo tự chuyển sang con khác. Mèo áp sát slime thảo dược thì nó lùi nhanh hơn nhưng vẫn đuổi kịp; nó đứng sau con đá vệ sĩ. Slime thường yếu (dưới ~1/3 máu) có mũi tên lùi, đi về chỗ slime thảo dược rồi quay lại. Slime nhanh lao xong chạy ra xa rồi mới quay lại. Có đá chắn trước mặt thì một slime thường (cung cam trên đầu) vòng ra sau lưng mèo. Có banner giải thích lần đầu; thấy rõ trên nền sáng/tối; không thấy cảnh bị kẹp không lối thoát.
- [ ] Cảm nhận độ khó mới: Dễ màn 1–2 vẫn thoải mái; Thường màn 2–5 phải dùng kỹ năng/bế mèo mới thắng; Khó là thử thách thật. Ghi vào "Nhật ký chơi thử" (mô phỏng không thay chơi thật).

### Nhật ký chơi thử Battle Mode (beta, thêm 2026-10-18)
Chơi 3–5 trận mỗi độ khó trên bản publish, ghi 1 dòng/trận. Phiên bản: xem Khác → Giới thiệu (ví dụ `1.0.0 (18cb227)`). Chưa có dòng nào là chưa chơi thử; số mô phỏng trong PROGRESS.md không thay cho bảng này.

| Ngày | Phiên bản | Màn (hoặc "tự do") | Vũ khí | Độ khó | Thời lượng | Thắng/thua | HP cuối | Nâng cấp đã chọn | Số lần cứu nguy | Kỹ năng đã dùng | Vấn đề thấy |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | | | | | | | |

Sau vài trận, trả lời: đòn quái sau khi thả mèo có dễ hiểu/công bằng không; 5 kỹ năng có vai trò rõ hay chỉ bấm hết khi hồi xong; HUD/click xuyên/focus có làm phiền app bên dưới không; âm thanh khi đánh lâu có dày/to quá không; cảnh báo có rõ khi nhiều quái/boss không; các mức khó khác nhau ở quyết định hay chỉ dài hơn.

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
