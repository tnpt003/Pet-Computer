# Checklist test thủ công

Dùng cho những gì test tự động không thay được: cảm giác chuyển động, âm thanh, giọng văn, nhiều màn hình.
Chạy bản publish (`artifacts\win-x64\DesktopPet.exe`). Mở **Behavior debug view** (menu chuột phải → Khác) để xem trạng thái.
Tick `[x]` khi đạt; ghi lỗi ngay dưới mục kèm ngày. Mục mới chưa test thì thêm vào đúng nhóm.

> 2026-10-22: người dùng báo đã test tay toàn bộ các mục bên dưới (đến bản 1.3.0-beta+b5fa2eb), **passed**. Mục thêm sau ngày này để `[ ]`.

## Ưu tiên 1 — Bất biến (hỏng là lỗi nặng)

### Kéo, ném, nhấc lên
- [x] Bấm giữ rồi di chuột < ~6 px: không vào kéo. Di xa hơn: nhấc lên mượt, không nháy.
- [x] Khi kéo mèo đung đưa như con lắc; lắc nhẹ không chóng mặt, lắc mạnh lâu mới chóng mặt, thả ra hết dần.
- [x] Ném mạnh/nhẹ: bay, rơi, nảy tự nhiên. Debug view dòng Frame ~60 fps, longest gap < ~35 ms (khoảng cách thật dài nhất giữa 2 khung trong cửa sổ 1 s; từ 2026-10-18 không còn bị chặn ở 100 ms nên khựng dài sẽ hiện đúng).
- [x] Kéo khi mèo đang chuyển tư thế (ngồi/ngủ/thức), đang làm động tác: ngắt ngay, không kẹt tư thế.

### Ngủ và thức
- [x] Ngủ thường: đúng tư thế; rê chuột qua, AI, bong bóng không đánh thức; tự thức sau ~10 s; waking ~1.2 s rồi Idle; sau đó ~30 s không ngủ lại.
- [x] Click trái mèo đang ngủ: thức ngay và vẫn kéo được. Chuột phải: thức ngay và mở menu.
- [x] Pause khi đang ngủ: timer ngủ đứng yên; Resume chạy tiếp.
- [x] Away nap: không chạm máy 5 phút → ngủ mãi; chạm chuột → thức (1.2 s) và chào "Sen về rồi!…" (bật Speech + AI). Click mèo đang away nap vẫn thức và kéo được. Pause trong lúc away nap.

### Cảm xúc
- [x] Click dồn đến Giận: tai cụp, đuôi quất. Không đụng 3 phút: buồn vài giây rồi bình thường; click giữa lúc buồn thì hết ngay.
- [x] Tự điều khiển cảm xúc: bật, chọn từng cảm xúc; vuốt/click dồn không đổi cảm xúc; để yên vài phút không buồn/ngủ tự động; kéo mạnh vẫn chóng mặt; tắt công tắc thì về bình thường ngay; mở lại app thì công tắc tắt.

### Không có Python
- [x] Tắt "Mèo thông minh (AI)" ở khay: mèo vẫn đi, ngồi, ngủ, cảm xúc bình thường; bật lại thì AI chạy tiếp.

## Ưu tiên 2 — Tính năng mới

### Đi trên cửa sổ
- [x] Để một cửa sổ không phóng to (VS Code, Explorer); ném/thả mèo lên mép trên: đáp đúng mép, đi qua lại, quay đầu ở mép.
- [x] Kéo cửa sổ: mèo đi theo. Thu nhỏ/phóng to/đóng: mèo rơi.
- [x] Cửa sổ thấp gần mèo: tự nhảy lên (chờ 1–2 phút). Cửa sổ cao chạm đáy: đi tới cạnh, trèo, lên đỉnh; click khi đang trèo: buông.
- [x] Mèo ngủ trên cửa sổ rồi thu nhỏ cửa sổ: rơi mà vẫn ngủ.
- [x] Chơi bóng/laser khi mèo trên cửa sổ: đi ra mép và nhảy xuống. Tắt "Mèo leo lên cửa sổ": mèo rơi.
- [x] Chrome/VS Code (thanh tiêu đề tự vẽ): mép có lệch không. Xem dòng Windows trong debug view.

### Đèn laser và quả bóng
- [x] Laser: rê chuột trên màn hình có mèo: nhìn, đuổi, dừng gần thì rình rồi vồ; rê cao thì chỉ nhìn; rê qua người mèo không tính vuốt; sang màn hình khác chấm ẩn; click/chuột phải mèo thì tắt; tự tắt sau 60 s; click app khác trong lúc chơi vẫn được.
- [x] Bóng: nảy/lăn và khi click bóng còn giật/lệch không. Chơi bóng với Balanced/Playful/Curious/Calm xem quan sát/hạ người/đánh/nghỉ tự nhiên không.

### Quiet và fullscreen
- [x] Bật Quiet, vuốt/cho ăn: có câu phản ứng (nếu bật Speech) nhưng không tự đi. Chơi bóng/laser vẫn chạy, hết trò thì đứng yên. Bật Quiet giữa lúc chơi: trò dừng.
- [x] Video YouTube fullscreen, game, PowerPoint trình chiếu trên màn hình có mèo: đứng yên, im, không bong bóng, không bắt đầu được trò chơi; thoát thì bình thường. Maximize thường KHÔNG tính. Fullscreen ở màn hình phụ khi mèo ở màn hình chính: không tính.

### Làm ngay
- [x] Thử từng động tác (cả khi Quiet); chuột phải lúc ngủ rồi chọn (thức 1.2 s rồi làm); Ngủ ngay sau khi vừa thức; click/kéo giữa lúc làm thì ngắt; Pause thì menu mờ.

### Giao diện
- [x] Menu chuột phải: icon rõ, bo góc, bóng, nhóm hợp lý, menu con Làm ngay/Cảm xúc/Chỗ nghỉ/Khác.
- [x] Cài đặt từ menu mèo và từ khay; đổi từng tab rồi Hủy (không đổi gì) và Lưu; tắt Âm thanh/Mèo biết nói/Nhắc nghỉ thì ô phụ mờ; Nghe thử; Esc đóng. Xem ở DPI 125–150%.
- [x] Ngôn ngữ: English → Save: menu mèo, menu khay, tooltip khay, About, Nhật ký, Behavior tuning, hộp thoại Quên đều tiếng Anh; mèo nói tiếng Anh. Đổi lại Tiếng Việt. Debug view luôn tiếng Anh.
- [x] Bộ lông/phụ kiện: đổi, Save, xem khi đi, ngủ, kéo, ném, ăn, vuốt; mèo đen (mắt/miệng rõ không); mũ khi kéo sát mép trên.
- [x] Nhật ký (AI bật và tắt), đổi ngôn ngữ, rê lên biểu đồ xem tooltip; sau vài ngày xem biểu đồ bond/giờ chơi.
- [x] Behavior tuning: Save / đóng không Save / Khôi phục mặc định; khởi động lại xem có giữ.
- [x] Thẻ trạng thái (2026-10-21): chuột phải mèo → Trạng thái của {tên}: thẻ hiện trên đầu mèo (mèo trên cửa sổ cao/mép trên thì hiện dưới), không bị menu đóng mất; dòng "Đang làm"/"Cảm xúc" đổi theo mèo; AI bật: 5 thanh (Năng lượng, No bụng, Vui chơi, Được cưng, Thân thiết) — cho ăn rồi chờ vài giây xem No bụng tăng; AI tắt: chỉ có gợi ý bật AI. Kéo thẻ đi chỗ khác được (bấm giữ chỗ trống trên thẻ), thả ra thẻ đứng yên; thử ở màn hình DPI 125–150% và màn hình phụ: thẻ mở sát đầu mèo. Click chỗ khác/Esc/× đóng; mở thẻ khi mèo đang ngủ không đánh thức thêm; mở trong trận ghi "Đang chiến đấu · HP".
- [x] Hộp thoại ấm (2026-10-21): Cài đặt → Lời nói → Quên những gì mèo đã học…: nền kem, icon cam, nút Hủy/Quên; Esc = Hủy. Khác → Giới thiệu cũng là hộp thoại ấm, chữ đường dẫn không bị cắt.
- [x] Danh sách bộ lông (đọc từ coats.json): đủ 10, tên và chấm màu đúng ở cả tiếng Việt/English.
- [x] Sinh nhật: chọn ngày + tháng = hôm nay → Lưu → thoát và mở lại mèo (Mèo biết nói + AI bật): câu chào là câu chúc sinh nhật; để lâu thỉnh thoảng nói thêm. Bỏ chọn tháng → Lưu → mở lại: chào bình thường. Mở lại Cài đặt thấy đúng ngày đã lưu.
- [x] Phụ kiện mới (Mũ Noel, Mũ phù thủy, Hoa mai, Đèn lồng): chọn trong Cài đặt, xem khi đi, ngủ, kéo, ném, trèo cửa sổ; mũ không bị cắt ở mép trên.
- [x] Tự đội đồ ngày lễ: đặt sinh nhật = hôm nay, mở lại mèo (AI bật): mèo đội mũ tiệc dù đã chọn phụ kiện khác; Cài đặt vẫn hiện phụ kiện đã chọn. Tắt ô → Lưu: về phụ kiện đã chọn. Tắt AI ở khay: bỏ mũ.
- [x] Ngày lễ (khi tới dịp): Halloween 31/10, Noel 24–25/12, Tết, Trung thu: câu chào và câu nói vu vơ theo dịp.

### Chế độ chiến đấu (Battle Mode, 2026-10-16)
- [x] Mặc định tắt; mở lại app không tự vào trận. Menu chuột phải → Chế độ chiến đấu → Bắt đầu trận → bảng chuẩn bị → Sẵn sàng. (Đổi 2026-10-22: menu mở sảnh, không còn Bắt đầu trận — xem mục "Sảnh chiến đấu" cuối file.) (Đổi 2026-10-19: khi mèo ngủ/đang kéo/trên cửa sổ/đang chơi, bảng chuẩn bị vẫn mở nhưng nút Sẵn sàng mờ kèm lý do; chỉ Pause/fullscreen làm mục Bắt đầu mờ.) Mèo không bị đánh thức/ép.
- [x] Click xuyên: trong trận, click/gõ vào app bên dưới ở khoảng trống của dải sàn và quanh HUD vẫn được; con trỏ đang gõ ở app khác không bị mất focus khi click slime/HUD; không có cửa sổ mới trên taskbar/Alt+Tab.
- [x] Mèo tự đi tới slime gần nhất, vung kiếm, slime trúng đòn nhấp trắng; slime phồng lên + dấu "!" đỏ trước khi đánh; hạ slime thì bụi trắng, không máu.
- [x] Click slime: mũi tên vàng + vòng vàng, mèo đổi sang đánh con đó; con đó chết thì marker mất và mèo tự chọn con khác. Xem marker/HP/HUD trên nền desktop sáng và tối.
- [x] Đẩy quái: bấm nút → nút xanh, slime có vòng xanh đứt, con trỏ dấu cộng; click slime → bị đẩy lùi + choáng, mục tiêu không đổi; nút hiện đếm ngược ~5 s; bấm lại nút hoặc chuột phải dải sàn để hủy chọn.
- [x] Kiếm: đúng tay, đổi hướng khi mèo quay đầu; khi vung, khi bị kéo (đung đưa, chóng mặt), khi ném/bay, khi thua (ngồi, sao quay) và thắng (giơ kiếm) không bị cắt mép, không che mặt khó chịu.
- [x] Kéo cứu nguy: bấm giữ mèo khi slime đang "!": slime đứng yên giữ nguyên "!" (đổi 2026-10-18: không hủy đòn nữa); thả mèo ra xa ngoài tầm thì đòn trượt, thả lại chỗ cũ (khi hết bảo vệ) thì bị đánh; kéo/đung đưa/chóng mặt/ném như cũ; khi đang cầm HP không giảm, không ra thêm slime, đợt không chuyển; thả xuống thì sau khi chạm đất mèo mới đánh tiếp, ~1.5 s đầu không bị đánh.
- [x] Pause/Resume trên HUD và trong menu; mở menu giữa trận thì trận dừng, đóng menu thì chạy tiếp; trận đã Pause thì đóng menu vẫn Pause. Pause ở khay: mọi thứ đứng yên, bỏ Pause chạy tiếp không "giật" sát thương.
- [x] (2026-10-18) Ném mèo lên cao rồi bấm Tạm dừng trên HUD khi mèo đang bay: mèo đứng yên giữa không trung (không rơi tiếp), slime/bom/lấp lánh Cổ vũ/khiên cũng đứng yên; bấm Tiếp tục: mèo bay tiếp từ đúng chỗ đó, không giật/nhảy. (Đổi 2026-10-23: trong lúc trận dừng không nhấc được mèo, mọi tư thế/sao choáng đứng yên — xem mục cuối.) Mở menu chuột phải lúc mèo đang bay: mèo cũng đứng yên tới khi đóng menu.
- [x] (2026-10-18, đổi 2026-10-19) Vũ khí: chọn Búa trên bảng chuẩn bị rồi Sẵn sàng (thoát/mở lại mèo vẫn nhớ; menu chuột phải không còn mục Vũ khí). Trận dùng búa: mèo cầm búa gỗ, vung chậm và nặng, mỗi nhát slime bị đẩy lùi một chút (boss/slime đá lùi ít); HUD ghi "Mochi · Búa"; bảng kết quả ghi "Thường · Búa". Búa có bị cắt khi vung/khi bế/khi ngồi choáng không, hai hướng. So với kiếm: có khác lối chơi thật không, búa có quá mạnh/quá yếu không (chỉnh mục "hammer" trong "weapons" của battle.json, hoặc thử trước bằng file tùy chỉnh — docs/BATTLE-DATA.md).
- [x] (2026-10-18, đổi 2026-10-19) Hướng dẫn trận đầu: lần đầu mở bảng chuẩn bị thấy bảng 4 thẻ (chưa có trận, chưa có slime); Xong/Bỏ qua đóng nó, bảng chuẩn bị vẫn còn; bấm Sẵn sàng khi hướng dẫn đang mở thì hướng dẫn đóng và trận chạy ngay; lần sau không tự hiện; nút Hướng dẫn trên bảng chuẩn bị mở lại được; con trỏ đang gõ ở app khác không mất focus; chữ dễ hiểu, đúng tiếng Việt/English; đọc rõ trên nền sáng/tối.
- [x] (2026-10-18, cập nhật 2026-10-21) Phản hồi khi bế mèo lúc slime đang "!": dấu "!" có vòng đỏ nhấp nháy, trên sàn có vòng đỏ đứt nét quanh slime; dòng chữ HUD ghi "slime sắp đánh: thả mèo ra ngoài vòng đỏ"; thả ngoài vòng: đòn trượt.
- [x] (2026-10-21) Cứu mèo không làm quái đứng hình: bế mèo lúc slime đang "!": slime vẫn phồng lên và đập xuống (hụt) rồi nghỉ, không đứng im giữa động tác; thả mèo lại đúng chỗ trước khi đòn rơi: đòn trúng đúng lúc (không trúng ngay khi vừa thả). Slime nhanh đang lao: lao hết đường, không quay theo mèo. Slime thảo dược vẫn hồi máu/teleport, Vua vẫn chạy pha khiên khi đang bế. Quái đang đi tiến về chỗ mèo đứng trước khi nhấc, không tụ dưới con trỏ. Nhấn chuột lên mèo (không kéo) đúng lúc slime đập: mèo vẫn bị đánh, không choáng. Nhấc/thả rất nhanh (dưới 0,3 s): không có "Được bảo vệ"; kéo mèo đi chỗ khác rồi thả: có. Cảm giác có lạ không. Thả xuống có bảo vệ: HUD ghi "Được bảo vệ sau khi tiếp đất: còn N giây". Vòng có rõ không, có rối mắt không.
- [x] (2026-10-18) Pause mèo ở khay giữa trận: dải sàn + HUD vẫn hiện nhưng bấm ô kỹ năng/click slime không có tác dụng (ô mờ); bấm Kết thúc trên HUD vẫn kết thúc trận. Đã Tạm dừng trận rồi Pause khay, bỏ Pause khay: trận vẫn dừng tới khi bấm Tiếp tục.
- [x] (2026-10-18) Tạm dừng → mở menu → đóng menu: vẫn dừng. Tạm dừng rồi mở video fullscreen, thoát: vẫn dừng, phải bấm Tiếp tục. Mèo ở màn hình khác (nếu có) rồi bấm nút Tạm dừng trên HUD: không bị chạy tiếp; đưa mèo về vẫn dừng tới khi bấm Tiếp tục.
- [x] (2026-10-18) Nhấc/thả liên tục tại chỗ (mỗi lần ~0.5 s): chỉ lần thả đầu có ~1.5 s an toàn; sau đó slime vẫn đánh trúng (đòn đang gồng được giữ qua các lần nhấc, không bị hủy). Cứu đúng lúc (nhấc khi thấy "!", thả xa ~2 thân mèo) vẫn né được. Thử cả với Vua Slime (vùng đỏ rộng hơn). Cảm giác có công bằng, có bị đánh "bất ngờ" lúc vừa thả không. Khi đang bế, dòng chữ dưới HUD ghi "kỹ năng chờ mèo xuống" và "thả xuống được 1.5 giây an toàn" hoặc "thả lúc này không có thêm bảo vệ (còn N giây)". Click thường vào mèo (không kéo): HUD không nháy, không được bảo vệ.
- [x] (2026-10-18) Chọn Bom len/Đẩy rồi kéo mèo: lựa chọn bị hủy ngay, ô kỹ năng mờ, số giây hồi chiêu đứng yên khi đang bế/bay, chạy lại khi mèo chạm đất. Thả bom rồi nhấc mèo ngay: bom vẫn nổ. Bật Khiên/Cổ vũ rồi bế mèo lâu: hiệu ứng vẫn hết đúng giờ. Thắng trận có lúc Tạm dừng lâu: thời gian trên bảng kết quả không tính lúc dừng.
- [x] Fullscreen (video/game) trên màn hình có mèo: trận dừng, dải sàn + HUD ẩn; thoát fullscreen thì hiện lại ở trạng thái dừng, bấm Tiếp tục mới chạy.
- [x] Thắng 3 đợt: "Chiến thắng!" rồi tự đóng; thua: mèo ngồi choáng rồi tự đóng; sau đó mèo vẫn bình thường (không buồn/giận, ăn/chơi được, AI chạy lại). Bắt đầu/kết thúc trận nhiều lần liên tiếp, thoát app giữa trận: không còn dải sàn/HUD sót lại.
- [x] Âm thanh: "vút" khi vung kiếm/đẩy, "bộp" khi slime trúng đòn, "bụp" khi slime biến mất; giật mình khi bị đánh, vui khi hết đợt/thắng, cáu khi thua; nghe có dễ chịu, có quá dày/quá to không; tắt Âm thanh/volume 0 thì im.
- [x] Hết đợt 1 và 2: HUD ghi "+15 HP" và thanh HP tăng; đợt cuối không hồi.
- [x] Bảng kết quả sau khi thắng/thua (đổi 2026-10-19, xem mục Trang bị bên dưới): bấm "Kết thúc" giữa trận thì không có bảng.
- [x] Thông báo từ chối: mở menu khi mèo rảnh, đợi mèo bị kéo/ngủ rồi mới bấm "Bắt đầu trận" (khó tái hiện): thông báo nhỏ trên đầu mèo, click xuyên, tự mất sau 2.5 s, không có hộp thoại.
- [x] DPI 125–150% và màn hình tọa độ âm/màn hình phụ (nếu có): dải sàn nằm đúng đáy vùng làm việc, slime đúng sàn, HUD giữa phía trên. Đổi vị trí taskbar hoặc DPI của màn hình đang đánh giữa trận: chiến trường dời theo. (Mèo sang màn hình khác: xem mục Nhiều màn hình bên dưới.)
- [x] Tắt AI ở khay rồi chơi một trận: chạy bình thường.
- [x] HUD kiểu game: chân dung mèo đúng bộ lông/phụ kiện; thanh HP đổi màu xanh/vàng/đỏ, vệt cam chạy theo khi mất máu; chấm đợt + vương miện; banner "ĐỢT n"/"BOSS!"/"CHIẾN THẮNG!"; rê chuột lên ô kỹ năng thấy mô tả ở dòng gợi ý dưới HUD; HUD không che việc đang làm quá nhiều.
- [x] 5 kỹ năng: Đẩy; Cá hồi máu (+25 xanh bay lên trên mèo, đầy máu thì không tốn); Cổ vũ (lấp lánh vàng, đánh nhanh hơn rõ); Bom len (chọn ô → click slime, cuộn len rơi rồi nổ, trúng cả quái gần đó); Khiên (bong bóng quanh mèo, slime đánh không mất máu). Vòng quét hồi chiêu + số giây; bấm lại ô hoặc chuột phải để hủy chọn.
- [x] Mèo chạy nhanh hơn trong trận (bước chân nhanh, không trượt); hết trận đi bộ lại bình thường.
- [x] Đợt 2–3 có Slime nhanh (xanh dương, nhỏ, có vệt gió) và Slime đá (xám, to, vết nứt, khó đẩy): nhìn rõ trên nền sáng/tối; độ khó có hợp lý không.
- [x] Nhiều màn hình (nếu có): ném/kéo mèo sang màn hình khác giữa trận: trận dừng, có thông báo cạnh mèo, chiến trường + HUD vẫn ở màn hình cũ; đưa mèo về: trận chạy tiếp. Bấm Tạm dừng khi mèo ở màn hình khác rồi đưa về: vẫn dừng.
- [x] (2026-10-18, cần 2 màn hình) Đang đánh ở màn hình phụ, rút cáp/tắt màn hình phụ (hoặc Win+P → chỉ màn hình chính): trong ~1 s dải sàn + HUD chuyển sang màn hình còn lại, slime nằm trong sàn mới, HP/đợt/hồi chiêu giữ nguyên, trận dừng kèm thông báo "Màn hình chiến trường đã ngắt…", bấm Tiếp tục thì đánh tiếp. Thử cả lúc đang kéo mèo (mèo không bị dời, vẫn kéo tiếp được), màn hình tọa độ âm và DPI khác nhau. Log có dòng "Battle monitor … is gone".
- [x] Nhấc mèo khi có quái: quái bò theo mèo dọc sàn nhưng không gồng/đánh; thả xuống có 1.5 s an toàn (tối đa 1 lần mỗi ~5 s mèo ở dưới đất).
- [x] Đẩy theo hướng: chọn Đẩy, rê lên nửa trái/phải slime thấy mũi tên xanh đổi hướng; click nửa trái → bay sang phải, nửa phải → sang trái (kể cả về phía mèo).
- [x] Rê chuột lần lượt qua 5 ô kỹ năng và nút Tạm dừng/Kết thúc: dòng chữ dưới HUD đổi đúng theo ô đang rê, rời ra thì trở lại câu gợi ý; HUD không co giãn/nhảy khi đổi chữ.
- [x] Combo: để mèo đánh trúng liền 3 nhát: nhát 3 có vệt kiếm vàng, chùm sao vàng + chữ "COMBO!", số sát thương gấp đôi, slime bị hất lùi (boss/slime đá lùi ít); nhấc mèo giữa chuỗi thì nhát sau lại là nhát thường. Combo có quá mạnh/quá dễ không.
- [x] Chuỗi trên HUD: đánh trúng 1 nhát thì cạnh tên mèo hiện "×1", nhát 2 hiện "×2" màu vàng, nhát 3 (combo) hoặc trượt/nghỉ lâu/nhấc mèo thì chữ biến mất; HUD không xê dịch. Có dễ nhận ra không.
- [x] Kết quả trận sang Python (bật Mèo biết nói): thắng/thua xong một trận, mèo nói một câu về trận (kỷ lục mới > thắng > thua) khi đứng yên; Nhật ký có dòng "Chiến đấu: thắng · thua · combo"; bấm Kết thúc giữa trận không tính.
- [x] HUD né con trỏ: rê chuột lên chân dung/thanh máu/dòng chữ của HUD: HUD mờ hẳn, click được cửa sổ app bên dưới; rê sang ô kỹ năng hoặc nút Tạm dừng/Kết thúc: HUD rõ lại, bấm được, chữ mô tả vẫn hiện; rời HUD: bình thường. Thử cả DPI 125–150% nếu có.
- [x] Độ khó (đổi 2026-10-19: chọn trên bảng chuẩn bị, nút đang chọn màu xanh): thoát và mở lại mèo vẫn nhớ mức đã bấm Sẵn sàng. Dễ: slime ít máu (số HP trên thanh boss ~300), đánh nhẹ, chậm hơn; Khó: ngược lại (boss ~520 HP). Cảm giác 3 mức có khác rõ và hợp lý không.
- [x] Kỷ lục (đổi 2026-10-19: tách theo vũ khí + độ khó): thắng/thua vài trận ở một mức: bảng kết quả ghi vũ khí · độ khó, dòng vàng "Kỷ lục mới" khi phá kỷ lục, dòng "Kỷ lục (mức): nhanh nhất … · nhiều nhất … slime · thắng … lần"; đổi mức khác thì kỷ lục riêng. Thoát và mở lại mèo: kỷ lục còn. Bấm "Kết thúc" giữa trận: không tính.
- [x] Hiệu năng/mượt: slime và hiệu ứng chạy 30 khung/giây (mèo vẫn 60): nhìn có giật không; khi Tạm dừng, debug view (dòng Frame) và Task Manager thấy CPU gần như không đổi so với lúc không chơi. Lúc boss gồng vẫn click được app bên dưới vùng đỏ.
- [x] Boss Vua Slime ở đợt cuối: to, vương miện; vùng đỏ trên sàn khi gồng; dưới nửa máu gọi 2 slime; dưới 30% đổi màu đỏ, nhanh hơn; Đẩy chỉ xê dịch ít; hạ boss thì bụi lớn, bảng kết quả ghi "Đã hạ Vua Slime". Thử trên màn hình hẹp/DPI cao xem boss có bị cắt đầu không.

#### Bảng chuẩn bị và dữ liệu tùy chỉnh (2026-10-19)
- [x] (Cũ, thay bởi mục "Sảnh chiến đấu" 2026-10-22.) Menu chuột phải → Chế độ chiến đấu chỉ còn Bắt đầu trận (khi chưa đánh) / Tạm dừng / Kết thúc. Bắt đầu trận: bảng "Chuẩn bị trận" hiện ở giữa phía trên màn hình có mèo; chưa có dải sàn, slime, HUD.
- [x] Bảng chuẩn bị: bấm Kiếm/Búa và Dễ/Thường/Khó: nút đang chọn đổi màu, dòng gợi ý vũ khí đổi theo; đang gõ ở app khác (Notepad) rồi bấm các nút: con trỏ gõ không mất focus, phím gõ không vào bảng; không có cửa sổ mới trên taskbar/Alt+Tab. Đọc rõ trên nền sáng/tối, tiếng Việt/English.
- [x] Hủy: bảng đóng, mèo đi lại bình thường sau ~2 s, không có kết quả/kỷ lục; mở lại thấy lựa chọn cũ (lựa chọn chỉ lưu khi bấm Sẵn sàng).
- [x] Mở bảng khi mèo đang ngủ: mèo ngủ tiếp (không bị đánh thức), Sẵn sàng mờ + "Chưa thể bắt đầu: Mèo đang ngủ."; mèo tự thức thì nút sáng lại. Kéo mèo khi bảng đang mở: nút mờ, thả xuống thì sáng. Đang mở bảng: mèo đứng yên, không tự đi/ngủ/nói; để máy ~5 phút: mèo không ngủ "away nap".
- [x] Bấm Sẵn sàng nhanh 2 lần (double click): chỉ một trận, một dải sàn, một HUD. Trận dùng đúng vũ khí/độ khó đã chọn (HUD ghi tên vũ khí, bảng kết quả ghi độ khó).
- [x] Pause ở khay khi bảng đang mở: Sẵn sàng mờ; bỏ Pause: sáng lại. Mở video fullscreen: bảng ẩn; thoát fullscreen: bảng hiện lại. Màn hình DPI 125–150%: bảng đúng vị trí, chữ không bị cắt.
- [x] Dữ liệu tùy chỉnh: chép docs/examples/battle.json vào %LOCALAPPDATA%\DesktopPet\battle.json, thêm "waves" có stone_slime (theo ghi chú trong file), mở bảng chuẩn bị: có dòng "Đang dùng dữ liệu trận tùy chỉnh"; vào trận gặp Slime đá tảng (xám, to, nứt). Sửa file thành sai (ví dụ "maxHp": -5) rồi mở lại bảng: dòng vàng báo lỗi kèm tên khóa sai, trận dùng dữ liệu gốc, mèo không crash. Sửa file giữa trận: trận đang chạy không đổi. Xóa file khi xong.

#### Trang bị và phong cách chiến đấu (2026-10-19, đợt 15)
- [x] Bảng chuẩn bị mới: bên trái mèo (đúng bộ lông/phụ kiện) cầm vũ khí đang chọn; bên phải 2 thẻ Kiếm/Búa có hình, mô tả, 4 thanh chỉ số kèm số; thẻ đang chọn có viền trắng + dấu ✓; độ khó có dấu ✓; "Cách chơi" mở hướng dẫn; Đóng / Sẵn sàng. Đổi vũ khí: mèo xem trước đổi ngay. Chữ đọc rõ trên nền sáng/tối, tiếng Việt/English, DPI 125–150% không bị cắt.
- [x] Xem thử: mèo xem trước vung một nhát đúng nhịp (kiếm nhanh; búa giơ lên, khuỵu và rung một chút rồi đập mạnh), có dấu đòn (tia trắng / bụi dưới sàn), có tiếng vút nếu âm thanh đang bật (tắt âm thì im). Mèo thật không động đậy, không có trận. Bấm Xem thử rồi Đóng ngay: không còn gì chạy.
- [x] Trong trận: kiếm trúng ra tia trắng, búa trúng ra bụi + vòng trên sàn và slime lùi xa hơn; búa giơ lâu nên slime chạy ra khỏi tầm thì nhát đập trượt. Nhát chém/đập trong hình có trùng lúc slime bị trúng không. Kiếm có thật sự hợp lối đánh áp sát/combo, búa hợp lối giữ khoảng cách không; búa có quá mạnh/yếu không (số liệu mới: tầm 76, đẩy 170, chạm đích 0.45 s, vung 0.7 s).
- [x] Bảng kết quả: hình mèo thắng (giơ vũ khí) / thua (ngồi choáng), "Vũ khí · Độ khó", số slime + thời gian, HP nhỏ, kỷ lục của đúng vũ khí + độ khó. Không tự đóng nữa. "Chơi lại": trận mới cùng vũ khí/độ khó ngay (bấm nhanh 2 lần vẫn 1 trận); "Đổi trang bị": bảng chuẩn bị với lựa chọn vừa dùng; "Về desktop": đóng. Fullscreen/đưa mèo về: bảng đóng.
- [x] Kỷ lục cũ: máy đã có battle-records.json từ trước (chép dự phòng file trước khi thử): sau trận đầu bảng kết quả có dòng mờ "Trước phân loại trang bị (mức): …" giữ số cũ; kỷ lục kiếm và búa tính riêng từ 0. Chơi với file battle.json tùy chỉnh: kỷ lục ghi "dữ liệu tùy chỉnh", tách khỏi kỷ lục thường.

#### Phiêu lưu — mốc 2 (2026-10-20)
Nên chép dự phòng %LOCALAPPDATA%\DesktopPet\adventure-progress.json (nếu đã có) trước khi thử.
- [x] Chuột phải → Chế độ chiến đấu → sảnh → thẻ Phiêu lưu (trước 2026-10-22: mục "Phiêu lưu" trong menu con): bản đồ ở giữa phía trên màn hình, có 5 nút màn nối bằng đường. Lần đầu chỉ màn 1 xanh, các màn khác xám có ổ khóa. Gõ phím ở app khác vẫn bình thường (bản đồ không lấy focus), không có nút trên taskbar. Chữ không bị cắt ở tiếng Việt/English, DPI 125–150%.
- [x] Click màn bị khóa: dòng "Chưa mở — thắng màn … trước", nút Vào màn mờ. Click màn 1 → Vào màn: bảng chuẩn bị có tiêu đề "Màn 1 · Làm quen" + mục tiêu, nút bên trái ghi "Bản đồ". Bấm Bản đồ: quay lại bản đồ.
- [x] Màn 1: tối đa 2 slime cùng lúc, 2 đợt. Thắng: bảng kết quả ghi "Màn 1 · Làm quen · Vũ khí · Độ khó", dòng vàng "Đã mở: Màn 2 · Giải vây", nút chính "Màn tiếp", bên dưới là Chơi lại / Bản đồ / Về desktop. Màn tiếp mở bảng chuẩn bị màn 2 với vũ khí vừa dùng.
- [x] Thua một màn (có thể bỏ mặc mèo ở độ khó Khó): không mở màn mới, nút chính là "Chơi lại". Thua màn đã thắng trước đó: màn đó vẫn được tính là đã thắng.
- [x] Kết thúc trận giữa chừng (HUD → Kết thúc, hoặc thoát app): không có bảng kết quả, không tính gì. Thoát app rồi mở lại → Phiêu lưu: các màn đã thắng vẫn vàng, màn kế vẫn mở, không thắng trùng.
- [x] Đi hết 5 màn tới Vua Slime: thắng màn 5 thì có dòng "Hoàn thành chương!" và không có nút Màn tiếp. Bấm Bản đồ: cả 5 màn vàng, có số lần thắng và thời gian nhanh nhất.
- [x] (Bỏ 2026-10-22: trận tự do không còn trong menu.) Trận tự do (Bắt đầu trận) vẫn như cũ; kỷ lục của trận tự do không đổi sau khi chơi Phiêu lưu.
- [x] Mở bản đồ khi mèo đang ngủ: mèo vẫn ngủ. Để máy yên 5 phút khi bản đồ mở: mèo không ngủ away nap. Mở app fullscreen: bản đồ ẩn, đóng app thì hiện lại.
- [x] Có file %LOCALAPPDATA%\DesktopPet\battle.json tùy chỉnh: bản đồ ghi rõ "không lưu tiến trình và kỷ lục"; thắng màn không mở màn sau.

#### Phiêu lưu — mốc 3: nâng cấp và sinh tồn (2026-10-20)
- [x] Màn 2 (Giải vây): dọn xong đợt 1 thì hiện bảng "Chọn một nâng cấp cho lượt này" phía trên sàn, 3 thẻ có hình + tên + tác dụng + "Đã có x/y". Trận đứng yên (slime, đồng hồ, kỹ năng). Gõ phím ở app khác vẫn bình thường. Click một thẻ (viền trắng) rồi Xác nhận: bảng đóng, trận chạy tiếp, HUD có dòng vàng nhỏ ghi nâng cấp. Bấm Xác nhận nhanh 2 lần: chỉ lấy 1.
- [x] Hình 6 thẻ (khiên, kiếm, đồng hồ, mũi tên tốc độ, mũi tên đẩy, con cá) nhìn rõ trên nền tối; chữ tiếng Việt không bị cắt ở DPI 125–150%.
- [x] Đang chọn thẻ mà bấm Tạm dừng trên HUD rồi Xác nhận: trận vẫn tạm dừng cho tới khi bấm Tiếp tục. Mở app fullscreen lúc đang chọn: bảng ẩn, đóng app thì hiện lại. Bấm Kết thúc lúc đang chọn: bảng đóng.
- [x] Nhanh chân: mèo chạy nhanh hơn thấy rõ; Cá nướng không xuất hiện khi HP đầy. Chơi lại màn: không còn nâng cấp cũ. Màn 1 không có thẻ. Trận tự do không có thẻ.
- [x] Màn 3 (Cầm cự): HUD ghi "Sống sót: còn 1:15" đếm ngược; bế mèo thì đồng hồ dừng; hết giờ mà còn HP thì thắng; bảng kết quả không có "thắng nhanh nhất". Màn có khó quá/dễ quá không (75 giây, 3 đợt lặp lại)?
- [x] Bảng kết quả màn có dòng "Nâng cấp lượt này: …".

#### Phiêu lưu — mốc 4: quái hồi máu, Vua Slime theo pha, phần thưởng (2026-10-20)
- [x] Màn 4: slime thảo dược (xanh ngọc, dấu "+" trắng). Khi nó gồng: dấu "+" xanh nhấp nháy trên đầu nó, vòng xanh đứt nét dưới con được hồi; sau đó con kia hiện dấu cộng bay lên và "+15". Banner "Quái hồi máu! Hạ nó trước" chỉ một lần. Đẩy nó lúc đang gồng: không hồi. Màn có thắng được không, có kéo dài vô tận không.
- [~] Màn 5: Vua Slime gọi 2 slime ở ~75% máu, 1 slime thảo dược ở ~50%, slime nhanh ở ~40%, nổi giận ở ~30%; mỗi pha có banner, không gọi lại; không bao giờ quá 4 quân của nó cùng lúc. Trận tự do: Vua Slime vẫn như cũ. (Đã thay 2026-10-21: xem mục cuối file.)
- [x] Thắng màn 3: bảng kết quả có dòng vàng "Phần thưởng mới: Mũ slime — đội trong Cài đặt → Mèo"; mèo không tự đội. Cài đặt → Mèo → Phụ kiện: có Mũ slime (trước đó không có), chọn → Lưu: mèo đội, xem khi đi/ngủ/kéo/trèo. Thắng màn 5: Vương miện Vua Slime. Thắng lại: không có dòng phần thưởng nữa. Bản đồ ghi "Phần thưởng: … ✓ đã nhận".
- [x] Bấm Kết thúc (HUD hoặc menu) giữa trận màn: về bản đồ đúng màn đó, không tính gì. Trận tự do: Kết thúc về desktop như cũ.
- [x] Đã thắng màn 3 bằng bản trước (8b93d25/9225d77): mở bản đồ là có Mũ slime trong Cài đặt (nhận bù).

#### Phiêu lưu — mốc 5: mẹo theo màn, cảnh báo lưu, bản 1.1.0-beta (2026-10-20)
- [x] Lần đầu vào màn 2–5: hiện thẻ "Mẹo · Màn N · …" phía trên sàn, trận đứng yên tới khi bấm Xong/Bỏ qua; lần sau không hiện nữa. Bảng chuẩn bị của màn có link "Mẹo màn" mở lại thẻ.
- [x] HUD ghi "Tên mèo · Vũ khí · Màn N"; lúc bắt đầu có banner tên màn.
- [x] Giới thiệu (Khác → Giới thiệu) ghi 1.1.0-beta.
- [x] (Tùy chọn) Đặt adventure-progress.json chỉ đọc (Properties → Read-only) rồi thắng một màn: bảng kết quả và bản đồ báo không lưu được; app không lỗi. Bỏ chỉ đọc sau khi thử.

#### Combat Depth — mốc 1: slime nhanh lao tới, mèo choáng 1,5 giây (2026-10-20)
- [x] Màn 2 hoặc trận tự do: slime nhanh dừng cách mèo một đoạn, gồng (dấu "!" + vạch đỏ đứt nét có mũi tên trên sàn), rồi lao thẳng theo vạch, có vệt gió phía sau. Vạch rõ trên nền sáng và tối.
- [x] Bị lao trúng: mất máu một lần, sao vàng xoay trên đầu + mắt chéo khoảng 1,5 giây; mèo đứng yên, không chém; sau đó đánh tiếp bình thường. Cú lao kế ngay sau đó (≤3 giây) chỉ mất máu, không choáng lại.
- [x] Đang choáng: kéo mèo lên được ngay (không cần đợi), sao vẫn còn khi cầm; lắc mạnh khi đang choáng chỉ có **một** vòng sao; thả/ném rơi như thường; hết choáng khi đang bay thì mèo rơi xuống rồi mới đánh tiếp.
- [x] Pause (HUD/menu) khi đang choáng: sao đứng yên, tiếp tục thì choáng chạy nốt phần còn lại. Bật Khiên trước cú lao: không mất máu, không choáng. Đẩy slime nhanh lúc nó gồng: không lao.
- [x] Lắc mèo ngoài trận (Pet Mode): Chóng mặt vẫn như cũ (lắc nhẹ không chóng mặt). Kết thúc trận khi mèo đang choáng: về desktop không còn sao.

#### Combat Depth — mốc 2: nhóm thẻ, synergy, build cộng dồn (2026-10-20)
- [x] Màn 2: mỗi lần chọn có 1 thẻ Phòng thủ, 1 Sức mạnh, 1 Tốc độ; dưới mô tả có dòng "Phòng thủ 0 → 1/3". Bị mất máu: Cá nướng thay một thẻ (không phải nhóm bạn đang theo nhiều nhất). Tiêu đề ghi "giữ sang màn sau khi thắng". Thẻ Tim to có hình trái tim, chọn xong HP tối đa +20 và thanh máu HUD đúng.
- [x] HUD ghi "Phòng thủ 1/3 · …"; chọn thẻ thứ 3 cùng nhóm: thẻ đó ghi vàng "Mở Giáp gai!" trước khi chọn, chọn xong banner "★ GIÁP GAI!" và HUD có "★ Giáp gai".
- [x] Giáp gai: quái đánh trúng mèo hiện số sát thương trên chính nó. Phá giáp: đòn combo (vệt vàng) mạnh hơn. Dư ảnh: cứ 4 nhát có thêm một tia sáng nhỏ và số sát thương phụ.
- [x] Thắng màn 2 → bảng chuẩn bị màn 3 ghi "Build mang vào màn: …" đúng; vào màn 3 HUD đã có điểm nhóm từ đầu. Thua màn 3 rồi chơi lại: vẫn bắt đầu với build của màn 2 (không cộng thẻ của lượt thua). Màn 3 chỉ cho chọn tối đa 3 lần.
- [x] Thắng lại màn 2 với build khác → màn 3 nhận build mới. Đóng app giữa chương rồi mở lại: build vẫn còn. Trận tự do: không có thẻ, không có build.

#### Combat Depth — mốc 3: đá chặn đòn, support đứng sau, King bật khiên (2026-10-20)
- [x] Slime đá (màn 2–4 và trận tự do): khoảng 1/3 nhát kiếm/búa hiện "CHẶN" + mảnh đá xám, không mất máu, không bị đẩy; không bao giờ chặn 3 nhát liền. Bom len luôn trúng. Có Phá giáp (3 thẻ Sức mạnh): đòn combo vàng không bao giờ bị chặn. Chữ "CHẶN" đọc rõ trên nền sáng và tối.
- [x] Slime thảo dược (màn 4–5): khi còn slime khác thì nó đứng phía sau, không gồng đánh mèo, vẫn hồi máu cho đồng đội; mèo đuổi thì nó lùi chậm và bị dồn ở mép màn hình. Hạ hết đồng đội thì nó tự đánh mèo. Click nó để mèo đánh nó trước vẫn được.
- [~] Màn 5: Vua Slime ở 70% máu bật bong bóng khiên vàng, banner "… bật khiên! Hạ hết quân", chạy về mép xa mèo rồi gọi 2 slime. Một đòn lớn (bom) không làm máu tụt qua 70%. Đánh/bom vào nó hiện "BẤT TỬ", thanh máu HUD vàng "(khiên: hạ hết quân!)". Mèo tự đánh quân, không đứng đánh boss. Hạ hết quân → "KHIÊN VỠ!" một lần, boss quay lại đánh. 40%: khiên lần 2, gọi slime thảo dược + 2 slime nhanh. 30%: nổi giận. (Đã thay 2026-10-21: xem mục cuối file.)
- [x] Trong khiên: kéo mèo lại sát boss thì nó vẫn dậm (vùng đỏ). Pause/menu giữa lúc boss chạy về mép: mọi thứ đứng yên. Thua giữa pha khiên: trận kết thúc bình thường, về Pet Mode không còn gì lạ. Chơi trên màn hình phụ/DPI khác: boss chạy đúng mép của màn hình đó.
- [x] Âm thanh: bật khiên/khiên vỡ/chặn đòn dùng cue sẵn có, không dày hay chói.

#### Enemy Director — mốc 1: đội hình, threat, sinh tồn liên tục (2026-10-20)
- [x] Màn 1: đợt 2 luôn có ít nhất một slime nhanh (vạch lao đỏ). Màn 2: có nhiều slime đá hơn trước.
- [x] Màn 3 (Cầm cự): quái ra liên tục tới hết 75 giây, không có "Đợt 2/3"; không bao giờ quá 4 con cùng lúc. Bảng thẻ hiện ở khoảng giây 20, 40 và 60 của đồng hồ, đồng hồ đứng yên khi đang chọn. Quái cuối màn trâu hơn đầu màn.
- [x] Màn 4: slime thảo dược và 3 slime đá hiện cùng lúc, sát nhau ở một mép, thảo dược đứng phía sau; nếu sàn còn quái cũ thì cả nhóm chờ, không hiện lẻ. Nhóm không hiện đè lên mèo.
- [x] Log (`%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log`) có dòng "Battle started: … seed N." mỗi trận.
- [x] Kỷ lục cũ của 5 màn không bị xóa nhưng không so với luật mới (rulesVersion tăng). Build đã lưu vẫn mang được vào màn sau.

#### Enemy Director — mốc 2: vai trò quái, chiến thuật (2026-10-20)
- [x] Slime thảo dược: có mũ lá và túi thảo dược, không còn dấu + trên thân. Lá sáng và có hạt sáng bay tới đồng minh khi gồng hồi; hết lượng hồi thì lá cụp xỉn. Đẹp/đọc rõ trên nền sáng và tối.
- [~] Đồng minh của nó tụt dưới ~1/3 máu ở xa: nó co người trong vòng lá, có dấu xanh ở điểm đến, rồi hiện ra sau đồng minh. Đẩy lúc đang co người thì hủy. Không teleport liên tục. (Đã thay 2026-10-21: xem mục cuối file.)
- [x] Slime đá: thân rộng/thấp, khiên đá ở phía mặt (khiên mới: xem mục mốc 3), nứt thêm khi yếu. Mèo không tự đi xuyên qua đá; click quái phía sau thì mèo vẫn đánh đá trước (vòng xám dưới chân đá). Bế mèo thả ra sau đá thì đánh thẳng mục tiêu. Thả mèo đè lên đá: mèo bước ra mép gần, không nhảy xa. Đánh từ sau lưng đá không bao giờ "CHẶN"; đá cần một chút thời gian quay lại.
- [x] Không thấy quá 2 quái cùng gồng một lúc; các đòn không trúng cùng một khung hình.
- [x] Màn 4: một slime đá đứng chắn giữa mèo và slime thảo dược; ném mèo sang phía kia thì đá đổi chỗ sau một nhịp.
- [x] Màn 5 đợt 1: mũi tên đỏ + banner "Quái tới từ phía sau!" rồi slime nhanh ra ở mép đó sau ~1 s. Đứng sát mép đó thì quái ra ở mép kia. Cầm cự sau giây 45: quái ra luân phiên hai mép, có mũi tên.
- [~] Vua Slime khi bật khiên: vương miện có vòng sáng, banner "ra lệnh xung phong", một quân chạy nhanh có vệt vàng. Đẩy Vua lúc vương miện sáng thì hủy. Khiên vỡ: Vua choáng ~2 s (sao quanh đầu), đánh được. (Đã thay 2026-10-21: xem mục cuối file.)

#### Enemy Director — mốc 3: tinh anh, thẻ hiếm, cân bằng (2026-10-20)
- [x] Màn 3–5 ở Thường/Khó: thỉnh thoảng có quái viền vàng + ngôi sao vàng ở góc lưng (không quá 1 con cùng lúc); lần đầu mỗi loại có banner "★ … tinh anh: …". Rõ trên nền sáng và tối, không che thanh máu/mũ lá/giáp. Màn 1–2 và mức Dễ không bao giờ có.
- [x] Slime nhanh tinh anh gồng lâu hơn thấy rõ rồi lao nhanh hơn; vẫn né/Đẩy kịp. Slime đá tinh anh chặn chắc đòn đầu phía trước ("CHẶN"); Bom len vẫn trúng. Slime thảo dược tinh anh hồi lần đầu nhiều hơn (+30 thay vì +15).
- [x] Thẻ hiếm: chơi vài lượt màn 2–5, đôi khi có thẻ viền vàng "★ Thẻ hiếm · một lần mỗi build" thay thẻ cùng nhóm. Đã lấy rồi thì không hiện lại. HUD và bảng chuẩn bị màn sau ghi "◆ <tên thẻ>".
- [x] Mai rùa: đòn đầu trúng mèo không mất máu (mảnh đá xám ở mèo), ~12 s sau lại chặn được một đòn; bế mèo thì không hồi nhanh hơn. Đòn mở màn: nhát đầu vào mỗi quái số sát thương gấp đôi. Chạy đà: mèo chạy một quãng tới quái rồi chém, số to hơn và có vệt vàng combo; đứng yên chém thì bình thường.
- [x] Slime đá cầm khiên đá hình huy hiệu (đầu phẳng, đáy nhọn, có bàn tay) ở phía mặt, không còn giống cái loa/vòi. Màn 4: slime thảo dược đứng sau 3 con đá vẫn thấy rõ (vẽ trên cùng, đá đè lên nó hơi mờ); click vào nó chọn đúng nó. Nền sáng và tối.
- [~] Vua Slime (màn 5) ở 70%/40% máu: nhún một nhịp rồi nhảy vòng cung (có bóng dưới sàn) về mép đối diện nửa sân nó đứng; khi mèo ở nửa đó thì về mép kia, không bao giờ đáp cạnh mèo. Thử cả mèo trái và mèo phải. Đáp có bụi, không mất máu. (Đã thay 2026-10-21: xem mục cuối file.)
- [x] Sau khi đáp: vòng tím có "!" ở chỗ quân sắp ra (~0,6 s), quân ra đúng các vòng: thảo dược sát Vua, đá đứng trước nó, slime nhanh ngoài cùng. Vương miện sáng thì có tia vàng nối tới con sắp được tăng tốc.
- [x] Slime thảo dược hồi nhiều thấy rõ (màn 4–5 khoảng +60 mỗi lần); không cắt thì quái khó hạ hẳn, nên hạ nó trước hoặc Đẩy lúc lá sáng.
- [x] Chiến thuật (màn 2–5): slime thảo dược gồng hồi thì con được hồi có vòng lá xanh, đánh vào hiện "MIỄN"; Đẩy slime thảo dược thì hết miễn ngay; mèo tự chuyển sang con khác. Mèo áp sát slime thảo dược thì nó lùi nhanh hơn nhưng vẫn đuổi kịp; nó đứng sau con đá vệ sĩ. Slime thường yếu (dưới ~1/3 máu) có mũi tên lùi, đi về chỗ slime thảo dược rồi quay lại. Slime nhanh lao xong chạy ra xa rồi mới quay lại. Có đá chắn trước mặt thì một slime thường (cung cam trên đầu) vòng ra sau lưng mèo. Có banner giải thích lần đầu; thấy rõ trên nền sáng/tối; không thấy cảnh bị kẹp không lối thoát.
- [x] Cảm nhận độ khó mới: Dễ màn 1–2 vẫn thoải mái; Thường màn 2–5 phải dùng kỹ năng/bế mèo mới thắng; Khó là thử thách thật. Ghi vào "Nhật ký chơi thử" (mô phỏng không thay chơi thật).

### Nhật ký chơi thử Battle Mode (beta, thêm 2026-10-18)
Chơi 3–5 trận mỗi độ khó trên bản publish, ghi 1 dòng/trận. Phiên bản: xem Khác → Giới thiệu (ví dụ `1.0.0 (18cb227)`). Chưa có dòng nào là chưa chơi thử; số mô phỏng trong PROGRESS.md không thay cho bảng này.

| Ngày | Phiên bản | Màn (hoặc "tự do") | Vũ khí | Độ khó | Thời lượng | Thắng/thua | HP cuối | Nâng cấp đã chọn | Số lần cứu nguy | Kỹ năng đã dùng | Vấn đề thấy |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | | | | | | | |

Sau vài trận, trả lời: đòn quái sau khi thả mèo có dễ hiểu/công bằng không; 5 kỹ năng có vai trò rõ hay chỉ bấm hết khi hồi xong; HUD/click xuyên/focus có làm phiền app bên dưới không; âm thanh khi đánh lâu có dày/to quá không; cảnh báo có rõ khi nhiều quái/boss không; các mức khó khác nhau ở quyết định hay chỉ dài hơn.

## Ưu tiên 3 — Cảm nhận và dài hạn

### Động tác và nhịp
- [x] Vuốt liên tục vài giây: ngồi nhồi bột rồi về bình thường; bond ≥ 60 có thể lăn ngửa.
- [x] Để chạy lâu: thỉnh thoảng đuổi đuôi, cào móng, hắt xì, liếm chân, rửa mặt, duỗi người; không quá dày; click/kéo/Pause ngắt được.
- [x] Đi/chậm lại/quay đầu, chuẩn bị nhảy/tiếp đất, ngồi/ngủ/thức trông mượt.
- [x] Needs/Thought trong debug view: cho ăn khi đói, chơi bóng khi chán; bong bóng quá dày hay thưa.

### Mèo nói
- [x] Tần suất Ít/Vừa/Nhiều có dễ chịu; câu sau vuốt/cho ăn/ném mạnh; câu cảm ơn hiện ngay sau khi ăn xong.
- [x] Câu chào khi mở lại app; nếu mèo ngủ lúc mở thì chào sau khi thức.
- [x] Bong bóng sát mép màn hình, trên màn hình phụ/DPI khác.
- [x] Đọc lại `ai/lines.json` để chỉnh giọng văn.

### Âm thanh
- [x] Nghe 9 preview, tiếng chạm bóng/ăn xong/thức dậy: dễ chịu, âm lượng, tần suất; không chồng tiếng.

### Nhắc nghỉ, tự học, khởi động
- [x] Nhắc nghỉ 30 phút: dùng máy bình thường; xem dòng Break reminder; khi nhắc mèo nói + duỗi; tắt Speech vẫn nhắc; rời máy ≥5 phút thì reset.
- [x] Sau vài ngày xem "Learned likes/dislikes"; thử nút Quên.
- [x] Bật/tắt Khởi động cùng Windows (bản publish), đăng xuất/đăng nhập lại; About; mở thư mục logs.

### Bộ nhớ và log
- [x] Để mèo chạy cả ngày, mở debug view xem dòng Memory: `heap` phải lên xuống chứ không tăng mãi; `committed` có thể lớn hơn heap.
- [x] Đo dài: `scripts\perf.ps1 -Minutes 120 -Interval 10 -Label long` khi đang dùng máy bình thường.
- [x] Log (`%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log`): mỗi dòng "started" có dòng "exited" đi kèm; nếu thiếu thì lần đó bị crash hoặc bị kill.

## Chưa làm được (cần phần cứng)
- [x] Nhiều màn hình vật lý mixed-DPI: kéo/ném qua màn hình, ngắt/kết nối màn hình, khôi phục vị trí và chỗ nghỉ.

## Chiến thuật quái gọn lại, hồi xa, Vua Slime mạnh hơn (2026-10-21)

- [x] Màn 2 và 5: slime thường không còn mũi tên xanh xám (lùi về) hay cung cam (vòng ra sau); slime sau đá chỉ đi tới sau đá, không băng qua mèo.
- [x] Màn 4: đánh một slime/đá ở xa slime thảo dược xuống dưới ~1/3 máu: slime thảo dược đứng yên, lá sáng lâu hơn thường (~1,2 s), có vệt lá xanh đứt nét nối qua sân tới con đó, banner "Slime thảo dược hồi máu từ xa! Đẩy nó để cắt" (một lần mỗi trận). Đẩy lúc gồng thì hủy, không hồi. Không hồi xa liên tục (cách ~8 s). Vệt nhìn rõ trên nền tối và sáng.
- [x] Không có đá: slime thảo dược đứng sau slime gần mèo nhất, không lao lên đánh mèo khi còn đồng minh.
- [x] Màn 5: Vua bật khiên ở ~75%, ~50%, ~25% máu (ba lần, lần cuối kèm nổi giận). Vương miện sáng + tia vàng tới một quân → quân đó có viền vàng + sao, thanh máu dài hơn, banner "★ … thành tinh anh!". Đẩy Vua lúc sáng thì hủy. Không phong lại con đã là tinh anh; chỉ còn slime thảo dược thì vương miện không sáng.
- [x] Màn 5 Khó: thử xem có quá khó không (mô phỏng: người chơi đơn giản thắng 5–6/10, người chơi "casual" 0–1/10).

## Viễn chinh — mốc 2: một chuyến hoàn chỉnh (2026-10-21)

Cần đã thắng màn 5 Phiêu lưu (Vua Slime). Không xóa `%LOCALAPPDATA%\DesktopPet\expedition.json` thật nếu muốn giữ kỷ lục; có thể chép ra chỗ khác trước khi thử.

- [x] Chuột phải → Chế độ chiến đấu: có "Viễn chinh". Chưa thắng Vua Slime thì mục bị mờ, ghi "(thắng Vua Slime để mở)".
- [x] Mở Viễn chinh: bản đồ ở đầu màn hình, không chiếm focus (gõ phím vào app khác vẫn được), mèo đứng yên. Chọn Kiếm/Búa và Dễ/Thường/Khó, bấm "Bắt đầu chuyến".
- [x] Bản đồ: 5 bậc, mỗi bậc 2 điểm (⚔ giao tranh, ★ tinh anh), cuối là ♛ trùm. Bậc hiện tại màu xanh, điểm đang chọn viền trắng; bậc đã qua: điểm đã thắng màu vàng, điểm bỏ qua mờ. Khung dưới ghi tên trận, mục tiêu, số đợt, "quái mạnh ×1,10…" từ bậc 2.
- [x] "Vào trận": HUD ghi "· Viễn chinh 1/6", banner "Viễn chinh · Bậc 1/6 · <tên trận>". Thẻ nâng cấp ghi "giữ suốt chuyến viễn chinh khi thắng bậc này".
- [x] Thắng: bảng kết quả ghi "Qua bậc 1/6 · HP mang tiếp …", "Nghỉ chân: +… HP", nút "Tiếp tục" và "Về desktop (chuyến được lưu)". Tiếp tục về bản đồ ở bậc 2; HP và build trên bản đồ đúng như cuối trận.
- [x] Trận bậc sau bắt đầu với HP còn lại (không đầy máu), build cũ còn hiệu lực (ví dụ ★ Giáp gai).
- [x] Bấm "Kết thúc trận" giữa trận: về bản đồ, dòng vàng "Đã rời trận bậc N: mất 25 HP…" (HP tối đa 100), vẫn ở bậc đó; đánh lại thì quái xuất hiện giống lần trước.
- [x] Đóng app (Thoát) giữa trận viễn chinh, mở lại, mở Viễn chinh: bị trừ HP như trên và có dòng báo; không mất chuyến.
- [x] "Về desktop" sau một bậc, mở lại Viễn chinh: tiếp tục đúng bậc, cùng bản đồ.
- [x] "Bỏ chuyến": lần đầu nút đổi thành "Bấm lần nữa để bỏ", bấm lần nữa thì chuyến kết thúc, bản đồ về màn chọn vũ khí, kỷ lục tính thêm một chuyến.
- [x] Thắng trùm: "Hoàn thành viễn chinh!", kỷ lục "nhanh nhất"; nút "Bản đồ viễn chinh". Thua bất kỳ bậc nào: "Chuyến đi kết thúc ở bậc N/6."
- [x] Phiêu lưu và trận tự do không đổi: kỷ lục màn Phiêu lưu và kỷ lục trận tự do không tăng vì trận viễn chinh.
- [x] Ứng dụng toàn màn hình ẩn bản đồ và hiện lại sau; Tạm dừng mèo thì không mở được; chuột phải giữa trận vẫn đóng băng trận như thường; cứu mèo (nhấc/thả), choáng, chóng mặt khi kéo mạnh hoạt động như trận thường.
- [x] Thời lượng thật một chuyến Thường (mô phỏng: ~7–8 phút thời gian đánh, chưa tính chọn thẻ/bản đồ); độ khó có hợp không (mô phỏng Thường: người chơi "casual" thắng 8/10; Khó rất khó).

## Viễn chinh — mốc 3: nghỉ chân, sự kiện, di vật (2026-10-21)

Chuyến đang dở từ mốc 2 (nếu có) sẽ kết thúc khi mở bản đồ, kèm dòng "đã đổi luật hoặc bản đồ" (đã được chấp nhận). Kỷ lục cũ vẫn giữ nhưng tính riêng.

- [x] Bản đồ mới: bậc 2 và 4 có 3 điểm (⚔, ★, ?), bậc 3 và 5 có ♨ nghỉ chân. Rê chuột lên "?" thấy tên sự kiện. Ký hiệu ♨ và ? hiện rõ (không ra ô vuông).
- [x] Bấm ♨ hoặc ?: khung dưới ghi điểm đó làm gì; nút chính đổi thành "Đi tới đây". Bấm vào: hiện bảng lựa chọn ngay trên bản đồ, nút "Đi tới đây"/"Vào trận" biến mất, không bấm được điểm khác.
- [x] Nghỉ chân: "Nghỉ: +N HP" (30% HP tối đa) hoặc chọn một thẻ rồi "Lấy thẻ đã chọn". Đóng bản đồ (hoặc thoát app) rồi mở lại: vẫn đúng 3 thẻ cũ.
- [x] Lữ khách mèo: chọn thẻ bỏ + thẻ lấy rồi "Đổi thẻ"; build trên bản đồ đổi đúng. "Đi tiếp" không đổi gì.
- [x] Rương slime: "An toàn" hồi HP; "Liều" trừ HP và cho một di vật (không biết trước là cái nào). HP thấp quá thì nút "Liều" mờ.
- [x] Đền cũ: hai di vật hiện tên + tác dụng; chọn một rồi "Lấy di vật đã chọn (−N HP)".
- [x] Thắng tinh anh: bảng kết quả ghi "Tinh anh để lại di vật…"; Tiếp tục → bản đồ hiện "Phần thưởng tinh anh" với 2 di vật; chưa chọn/bỏ qua thì không đi tiếp được.
- [x] Dòng "Di vật: ◈ … (n/3)" trên bản đồ; HUD trong trận có "◈ <tên di vật>". Đủ 3 di vật thì tinh anh/đền không đưa thêm.
- [x] Từng di vật trong trận thật: Chuông phản đòn (bật Khiên đỡ một đòn → nhát kế có vệt vàng, mạnh hơn; ô Khiên hồi lâu hơn 3 s), Túi cứu trợ (Cá hồi ~37 HP, hồi chiêu dài hơn), Bùa liều lĩnh (HP thấp đánh mạnh hơn, bị đánh đau hơn chút), Lông vũ nhẹ (chạy nhanh hơn, HP tối đa 85).
- [x] Phiêu lưu và trận tự do không có di vật (HUD không có ◈, Khiên/Cá hồi chiêu như cũ).
- [x] Cảm giác: đi đường tinh anh (nhiều di vật) có đáng hơn đường nghỉ không? Mô phỏng cho thấy đường "tham lam" thắng nhiều hơn đường "an toàn" ở Thường — xem PROGRESS.md.

## Viễn chinh — mốc 4: kỷ vật và góc trưng bày (2026-10-21)

- [x] Bản đồ viễn chinh ghi "Kỷ vật: n/5".
- [x] Thắng cả chuyến lần đầu ở Thường: bảng kết quả ghi "Kỷ vật mới: Cúp bạc viễn chinh (ở góc trưng bày)", "Khăn viễn chinh (đội trong Cài đặt)", "Đệm slime". Thắng lại ở Thường: không có kỷ vật mới. Thắng ở Dễ/Khó: cúp đồng/vàng.
- [x] Về desktop: kệ gỗ với đệm slime và cúp đứng trên cùng mặt sàn với mèo, lần đầu ở ~3/4 bề ngang màn hình (không lơ lửng, không bị thanh taskbar che); kệ không lấy focus, không có nút taskbar. (Từ 2026-10-22 kệ cầm được — xem mục "Kệ kỷ vật thành đồ chơi".)
- [x] Mở app toàn màn hình (video/F11): kệ ẩn, đóng thì hiện lại. Vào trận: kệ ẩn, kết thúc trận thì hiện lại.
- [x] Cài đặt → Mèo → bỏ "Góc trưng bày kỷ vật" → Lưu: kệ biến mất ngay. Bật lại: hiện lại.
- [x] Cài đặt → Mèo → Phụ kiện: có "Khăn viễn chinh"; đội thử, xem khi đi/ngủ/kéo/trèo.
- [x] Để mèo tự do ~5 phút (không Quiet): mèo đi tới bên trái kệ, quay mặt nhìn kệ khoảng 4 giây rồi đi tiếp. Khi đang ngủ, Quiet hoặc đang chơi bóng/laser thì không đi.
- [x] (Từ 2026-10-22 kệ không chạy theo mèo nữa: kệ ở lại màn hình bạn thả nó.)

## Kệ kỷ vật thành đồ chơi (2026-10-22)

Tự động đã có: physics kệ (rơi, nảy, nhún, đẩy, chạm mép, cầm/lắc), rơi trúng đầu mèo (choáng; mèo ngủ thì ngủ tiếp), chạm mép → mèo ngạc nhiên → kệ về giữa (cả khi mèo đứng giữa), mèo tự đẩy kệ, Quiet/Pause. Phần dưới cần làm tay (cần đã có ít nhất một kỷ vật):
- [x] Rê chuột lên kệ: con trỏ bàn tay. Click (không kéo): kệ "boing" nhún một cái. Rê chuột ở vùng trống quanh kệ: click xuyên xuống app/desktop bên dưới.
- [x] Kéo kệ (quá ~6 px mới nhấc): kệ hơi giãn khi nhấc, lắc lư nghiêng theo hướng kéo, không nhấp nháy; kệ không lấy focus khỏi app đang gõ.
- [x] Thả kệ ở trên cao: rơi, chạm đất bẹp → giãn → nhún 1–2 lần, có bụi nhỏ; rơi càng cao nhún càng mạnh. Ném mạnh sang ngang: trượt, dội mép màn hình.
- [x] Thả kệ trúng đầu mèo đang đứng/đi: tiếng "cộp", mèo bẹp người rồi bật lại, sao xoay + mắt choáng ~2 giây, kệ nảy lên rồi rơi bên trái hoặc phải cạnh mèo. Mèo đang ngủ: thức dậy (tư thế thức 1,2 s) và choáng thấy sao; sau đó ~30 s không tự ngủ lại.
- [x] Cỡ món đồ khoảng nửa con mèo (cúp ~55 DIPs).
- [x] Chuột phải món đồ: menu có các kỷ vật (✓ món đang hiện), "Đưa về giữa màn hình", "Ẩn món trưng bày"; menu không lấy focus app đang gõ. Chọn món khác: khói poof, món mới hiện đúng chỗ cũ, Cài đặt cũng đổi theo. Đưa về giữa: poof, hiện giữa màn hình (mèo không giật mình). Ẩn: biến mất, bật lại trong Cài đặt.
- [x] Bật "Mèo leo lên cửa sổ", để một cửa sổ không phóng to (Explorer/VS Code) cùng màn hình với mèo: thả món đồ phía trên → đáp lên mép trên cửa sổ có nhún; kéo cửa sổ → món đồ đi theo; thu nhỏ/đóng cửa sổ → món đồ rơi xuống sàn. Ném trượt quá đầu mép → rơi. Món đồ ở màn hình khác mèo: chỉ đáp sàn.
- [x] Ném ngang mạnh món đồ vào người mèo: mèo choáng thấy sao, món đồ dội ngược lại. Mèo ngủ: thức + choáng.
- [x] Cầm món đồ đưa qua lại: mèo đang rảnh quay mặt nhìn theo. Thả rơi mạnh ngay cạnh mèo (không trúng): mèo giật mình "!".
- [x] Rê chuột lên món đồ: nhún nhẹ, hơi to lên; rời chuột thì về cỡ. Click đúp: mèo đi tới ngắm (cả khi bật Quiet).
- [x] Chơi bóng: bóng lăn trúng món đồ thì dội lại, món đồ lắc.
- [x] Chọn trưng bày đệm slime, để mèo tự do ~6 phút trở lên (không Quiet): mèo đi tới đệm, nằm ngủ TRÊN đệm (không lơ lửng, không chìm); thức sau ~10 s thì bước xuống. Click mèo lúc đó: thức như thường, kéo được.
- [x] Thả món đồ lên mép cửa sổ: bẹp + nảy giống khi đáp sàn (trước đây đáp "cứng").
- [x] Mèo đứng trên cùng cửa sổ với món đồ: chờ/click đúp → mèo ngắm; mèo tự đẩy có thể đẩy rơi khỏi mép. Mèo dưới sàn, món đồ trên cửa sổ: mèo không đi ngắm/đẩy.
- [x] Trưng bày đệm slime: chuột phải mèo → Làm ngay → "Ngủ trên đệm": mèo ở gần (≤ ~2 thân mèo) thì đi tới nằm ngủ trên đệm (cả khi Quiet); ở xa thì mục mờ, rê chuột thấy "Mèo ở xa đệm quá". Trưng bày cúp: không có mục này.
- [x] Nhấc mèo thả rơi (hoặc ném) trúng đệm slime: mèo bật lên cao hơn khi rơi xuống sàn, lệch sang bên, đệm bẹp, tiếng "squish"; 1–2 nhịp rồi đáp cạnh đệm. Cúp: không có.
- [x] Để mèo yên ~1,5–2 phút với đệm slime trên màn hình: mèo tự đi tới đệm rồi ngủ trên đó (thay vì ngủ tại chỗ). Không có đệm: ngủ tại chỗ như cũ.
- [x] Chơi bóng: quả bóng là cuộn len hồng có viền tối và sợi len thòng ra, vẫn lăn/xoay như cũ.
- [x] Làm mèo choáng (thả món đồ trúng đầu) rồi rê chuột thật nhanh qua mèo: KHÔNG hiện mặt Ngạc nhiên trong lúc thấy sao.
- [x] Trên màn hình chỉ có MỘT món (trước đây ~1,8×, nay ~1,2×): mặc định cúp xịn nhất (vàng → bạc → đồng, không có cúp thì đệm). Cài đặt → Mèo → "Món trưng bày": chọn món khác → Lưu: đổi ngay, vẫn đứng đúng sàn. Chưa có kỷ vật: ô mờ "Chưa có kỷ vật".
- [x] Thả kệ sát mép trái/phải màn hình: mèo (cùng màn hình, đang thức) quay sang, mặt ngạc nhiên "!", kệ rung → "poof" khói + lấp lánh → hiện ở giữa màn hình (phóng to nhẹ rồi về cỡ, rơi chút rồi nhún). Mèo đứng giữa: kệ hiện cạnh mèo, không rơi lên đầu.
- [x] Để mèo tự do vài phút (Playful nhanh hơn, Calm lâu hơn; lần đầu ~3 phút): mèo đi tới một bên kệ, cúi người, đẩy — kệ trượt có nảy/lắc, mèo nhìn theo vui vẻ. Nếu kệ trượt tới mép: mèo ngạc nhiên, kệ về giữa. Quiet/ngủ/fullscreen/đang chơi bóng: không đẩy.
- [x] Đóng mở lại app: kệ ở chỗ cũ. Pause: kệ đang rơi thì đứng yên giữa không trung, bỏ Pause thì rơi tiếp.
- [x] Màn hình phụ (khác DPI nếu có): kéo kệ sang màn hình phụ, thả: đứng đúng sàn màn hình đó; mèo ở màn hình khác thì không đẩy/không ngắm/không bị rơi trúng. Rút màn hình phụ: kệ về màn hình của mèo.

## Viễn chinh — mốc 5: kiểm thử phát hành, bản 1.3.0-beta (2026-10-21)

Tự động đã có: 18 chuyến trọn vẹn (Dễ/Thường/Khó × 6 seed) qua nghỉ, cả 3 sự kiện, di vật tinh anh, trận bị ngắt rồi đánh lại và trùm, chạy theo 3 cách: liền mạch, đóng app sau mọi thao tác, bấm đúp mọi thao tác (kết quả giống hệt nhau); crash giữa trận trùm; bản đồ/kệ đặt đúng pixel ở scale thật và 150% giả; mất màn hình giữa trận Viễn chinh; bản đồ trên màn hình 480 DIP. Phần dưới cần làm tay:
- [x] Khác → Giới thiệu ghi 1.3.0-beta.
- [x] Chơi một chuyến Thường trọn vẹn từ đầu tới trùm (ghi thời lượng, HP trước trùm, số di vật, đường đã chọn). Giữa chừng đóng app bằng menu khay ở: bản đồ, bảng nghỉ chân đang mở, bảng chọn di vật, giữa trận. Mở lại: bản đồ/thẻ/di vật được đưa giống hệt lúc trước; đóng giữa trận thì đánh lại đúng trận đó (từ 2026-10-23 không mất HP).
- [x] Bấm đúp nhanh: "Tiếp tục" trên bảng kết quả, "Lấy di vật đã chọn", "Vào trận" — chỉ đi một bước, một di vật, một trận.
- [x] Màn hình thấp hoặc scale cao (vd. 1366×768 ở 125%, hoặc tạm đặt Display scale 175%): mở bản đồ lúc đủ 3 di vật và đang ở điểm nghỉ — bản đồ không tràn khỏi màn hình, có thanh cuộn mảnh màu kem; lăn chuột trên bản đồ cuộn được (không cần bấm vào trước); hàng nút Đóng/Bỏ chuyến luôn thấy. Màn hình thường: không có thanh cuộn, bản đồ như cũ.
- [x] Cùng màn hình thấp: bảng chuẩn bị của màn 2 "Break through/Phá vòng vây" (mục tiêu dài) không tràn, có thanh cuộn, nút Sẵn sàng/Bản đồ luôn thấy; cuộn xuống chọn được độ khó. Bản đồ Phiêu lưu và bảng kết quả không có thanh cuộn và không bị cắt.
- [x] DPI 125–150% thật và màn hình phụ khác DPI: bản đồ nằm giữa mép trên màn hình của mèo; kệ kỷ vật ở góc phải dưới đứng trên sàn. Rút màn hình phụ đang có trận Viễn chinh: trận chuyển sang màn hình còn lại, tạm dừng, HP giữ nguyên, bấm Tiếp tục đánh tiếp (không bị tính là bỏ trận).
- [x] Gói ZIP 1.3.0-beta trên máy sạch (không Visual Studio): mở được, Viễn chinh chơi được, tắt Python thì mèo vẫn chạy.

#### Buff quái/tinh anh, bản 1.2.0-beta (2026-10-21)
- [x] Khác → Giới thiệu ghi 1.2.0-beta.
- [x] Màn 1–2 Thường: quái chịu đòn lâu hơn chút, đánh đau hơn; vẫn thắng được khi dùng kỹ năng. Dễ màn 2 không dùng kỹ năng thì có thể thua (mô phỏng: chỉ ~30% thắng) — báo nếu thấy quá khó.
- [x] Màn 3–5 Thường: gặp slime thường tinh anh (viền vàng + sao): banner "★ Slime tinh anh: máu dày hơn, đánh dồn dập hơn", nó ra đòn dày hơn nhưng vẫn gồng (có cảnh báo) như cũ.
- [x] Quái tinh anh có nhãn vàng nhỏ trên đầu (MIỄN ĐẨY / ĐÁNH NHANH / ĐÒN MẠNH / HỒI MẠNH; tiếng Anh khi đổi ngôn ngữ): đọc được trên nền sáng và tối, không đè thanh máu; khi gồng nhãn nằm trên dấu "!". Khi được chọn (mũi tên vàng) xem có bị che khó đọc không.
- [x] Slime nhanh tinh anh: vệt gió vàng dài hơn khi chạy; khi lao có hai bóng mờ vàng phía sau, không che vạch cảnh báo đỏ trên sàn.
- [x] Slime đá tinh anh: khiên to hơn, viền tấm lót vàng phía sau, hai đinh tán; quay mặt thì khiên đổi bên đúng; trúng đòn nháy trắng như khiên thường.
- [x] Đẩy slime đá tinh anh: nó đứng yên, không choáng, vẫn gồng tiếp; hiện mảnh đá + khiên nhỏ, banner "★ Slime đá tinh anh miễn Đẩy!" chỉ lần đầu; nút Đẩy vào hồi chiêu. Đẩy slime đá thường: bị đẩy/choáng như cũ. Đánh combo hoặc dùng búa vào đá tinh anh: nó không lùi nhưng vẫn mất máu; đá thường vẫn bị đẩy lùi.
- [x] Slime thảo dược tinh anh có quầng xanh lá phập phồng sau thân (không che slime khác quá nhiều); hết lượng hồi thì quầng tắt.
- [x] Slime thảo dược tinh anh: hồi máu dày hơn, mỗi lần hồi không lớn hơn; vẫn hết lượng hồi sau một lúc.
- [x] Vua Slime phong tinh anh cho quân: quân đó cũng khỏe/đau hơn theo loại của nó.
- [x] Bảng kết quả/bản đồ: kỷ lục cũ không còn hiện là kỷ lục của luật mới (tính lại từ đầu). Nếu có chuyến Viễn chinh đang dở từ bản trước: không tiếp tục được (báo luật đã đổi), app không lỗi.
- [x] Khó và Viễn chinh Thường khó hơn rõ (mô phỏng: Viễn chinh Thường đường an toàn ~2/10) — cho biết cảm nhận thật.

#### Cue âm thanh tinh anh (2026-10-21)
- [x] Bật âm thanh tương tác, âm lượng vừa. Tinh anh xuất hiện (màn 3–5 Thường) hoặc Vua phong tinh anh: nghe "ting-ting" hai nốt đi lên, ngắn, không chói, khác tiếng mèo ngạc nhiên trước đây.
- [x] Đẩy slime đá tinh anh: nghe tiếng "cộp" trầm như gõ khiên đá, khác "tách" khi đòn bị chặn thường. Đẩy nó liên tục (mỗi lần hết hồi chiêu): không ồn, không chồng tiếng.
- [x] Tắt âm thanh tương tác hoặc âm lượng 0: hai cue im; có app fullscreen: im. Thắng/Thua/Boss/mèo bị đánh cùng lúc vẫn át hai cue này.

## Sảnh chiến đấu và giao diện game đồng bộ (2026-10-22)

Tự động đã có: menu là một mục không có menu con (trong trận chỉ còn Tạm dừng/Kết thúc), sảnh mở/không lấy focus/giữ thời gian rảnh của mèo, thẻ Vô tận không mở được, Viễn chinh khóa rồi mở, ‹ Sảnh trên bản đồ quay lại sảnh, "Chuẩn bị chuyến" → bảng chuẩn bị chung → "Lên đường" tạo chuyến, ‹ Bản đồ không tạo chuyến, bấm đúp không tạo hai chuyến. Đã xem ảnh render (sảnh, bảng chuẩn bị chuyến, bản đồ Viễn chinh/Phiêu lưu, màn hình thấp). Phần dưới cần làm tay:
- [x] Chuột phải → Chế độ chiến đấu (không có mũi tên menu con): sảnh hiện ở giữa mép trên màn hình có mèo, có mèo cầm vũ khí, 3 thẻ có hình (đồi + cờ, núi tuyết, vòng ∞). Gõ phím ở app khác vẫn bình thường; không có nút trên taskbar/Alt+Tab.
- [x] Rê chuột lên thẻ mở được: viền trắng. Thẻ Phiêu lưu ghi đúng số màn đã thắng, thanh vàng đúng tỉ lệ. Thẻ Viễn chinh: khóa (dải "Đang khóa") nếu chưa thắng Vua Slime; đang có chuyến thì ghi bậc và HP; không thì ghi kỷ vật x/3. Thẻ Vô tận mờ, dải "Sắp ra mắt", bấm không có gì.
- [x] Tạm dừng mèo rồi mở menu: mục Chế độ chiến đấu mờ kèm lý do. Đang trong trận: mục có menu con Tạm dừng/Kết thúc.
- [x] Phiêu lưu → bản đồ → "‹ Sảnh": quay lại sảnh. Sảnh → Đóng: mèo hoạt động lại bình thường. Thử cả với Viễn chinh.
- [x] Viễn chinh chưa có chuyến: dòng "lần trước: …", nút "Chuẩn bị chuyến" → bảng chuẩn bị (tiêu đề Viễn chinh, huy hiệu núi) → chọn Búa + Khó → "Lên đường": quay lại bản đồ, dòng trạng thái ghi Búa · Khó. Mở lại bằng "‹ Bản đồ" thì không có chuyến nào.
- [x] Mèo đang ngủ khi mở sảnh/bản đồ/bảng chuẩn bị chuyến: mèo vẫn ngủ; "Lên đường" vẫn bấm được (chưa vào trận); "Vào trận" thì mờ tới khi mèo thức.
- [x] Bảng chuẩn bị: chip độ khó có 1/2/3 sao vàng; tiêu đề dài (màn 2) — huy hiệu tròn nằm ngang dòng đầu.
- [x] Màn hình thấp/scale cao (1366×768 125% hoặc scale 175%): sảnh, bản đồ Phiêu lưu không tràn; app fullscreen: sảnh ẩn rồi hiện lại.

## Viễn chinh: quay bậc và máy viên nang (2026-10-22)

Tự động đã có: luật bốc trên 150 chuyến, bốc một lần (quay lại, lưu/đọc lại, bấm đúp không đổi), máy viên nang (cả hai viên bốc lúc thắng, không ra di vật khi đã đủ, không ra thẻ hiếm đã đầy, viên vàng khóa khi mèo quá yếu), 18 chuyến trọn vẹn chạy 3 cách giống hệt, mô phỏng cân bằng, UI check bản đồ/máy quay/máy viên nang. Đã xem ảnh render. Phần dưới cần làm tay:
- [x] Mở Viễn chinh lần đầu sau khi cập nhật (nếu đang có chuyến dở): có dòng báo chuyến cũ đã kết thúc vì luật đổi.
- [x] Chuyến mới: bản đồ chỉ có các nút "?" và Trùm; nút "?" của bậc đang đi viền vàng; ô chi tiết có viên nang, tỉ lệ và dòng "Bậc đầu không có tinh anh hay nghỉ."; nút chính "Quay máy".
- [x] Bấm Quay máy: viên nang lắc, bật nắp, có tiếng "pop" (~1 giây), trong lúc đó không bấm được gì; sau đó bậc lộ 2–3 điểm. Bấm đúp nhanh Quay máy: chỉ quay một lần.
- [x] Thắng một trận giao tranh: bảng kết quả ghi "Máy viên nang đang chờ…"; về bản đồ có 2 thẻ viên nang (thường xanh ngọc, vàng) với tỉ lệ và nút Bỏ qua. Chọn viên vàng: HP trừ ngay, viên nang mở trên bản đồ rồi dòng ghi chú báo phần thưởng (tiếng lấp lánh nếu là di vật/thẻ hiếm). Tinh anh thắng: không có máy viên nang (vẫn có di vật).
- [x] Đóng app khi máy viên nang đang chờ hoặc ngay sau khi bấm Quay máy; mở lại: vẫn đúng các viên nang/bậc như trước (không quay lại được).
- [x] Bậc 5 (trước trùm) luôn có điểm nghỉ. Chơi vài chuyến: hình bản đồ khác nhau; cảm nhận độ khó so với trước (mô phỏng: đường an toàn dễ hơn, đường tham lam khó hơn chút).
- [x] Màn hình thấp/scale cao: máy viên nang và ô quay không tràn, cuộn được.

## Túi đồ theo chế độ và Phần thưởng (2026-10-22, thay mục "Túi đồ trên Sảnh chiến đấu")

Tự động đã có: Phần thưởng trên sảnh (ô có hình, khóa, Mới một lần rồi lưu đã xem, nút đếm "n mới", kỷ lục), túi đồ mở từ bản đồ Phiêu lưu/Viễn chinh và HUD, 3 tab, không lấy focus/không taskbar, trận pause khi mở (cả lúc chọn thẻ; chỉ gỡ lý do của túi), × đóng, nhớ chỗ đặt, fullscreen ẩn/hiện, màn 480 DIP cuộn, đóng bản đồ/hết trận thì đóng túi, viên nang đang mở thì chưa mở được, nhật ký chuyến (lưu/đọc lại, giới hạn 60 dòng), tiếng Việt. Đã xem ảnh render (Chỉ số, Thẻ, Di vật, Phần thưởng, HUD). Phần dưới cần làm tay:
- [x] Sảnh → "Phần thưởng": ô phụ kiện là mèo đang đeo, cúp/đệm như trên kệ, ô khóa mờ có ổ khóa; rê chuột lên ô thì dòng dưới ghi cách nhận/nơi dùng. Lần đầu sau cập nhật: các phần thưởng bạn đã có hiện "Mới" một lần (bình thường).
- [x] Trong trận: bấm nút túi (hình túi đỏ-vàng cạnh Tạm dừng/Kết thúc) → trận đứng yên, túi nổi bên phải dưới HUD; bấm lại hoặc × → trận chạy tiếp. Đang Tạm dừng rồi mở/đóng túi: trận vẫn tạm dừng.
- [x] Kéo túi bằng phần đầu (hình mèo/tiêu đề): đi theo chuột, không giật; đóng rồi mở lại vẫn ở chỗ cũ. Gõ phím ở app khác trong lúc túi mở vẫn bình thường. Thử cả khi kéo sang màn hình phụ.
- [x] Lúc chọn thẻ nâng cấp: mở túi tab Thẻ xem build, chọn thẻ + Xác nhận → túi hiện thẻ mới, trận vẫn dừng tới khi đóng túi.
- [x] Bản đồ Phiêu lưu: chọn màn khác rồi mở túi → dòng "Build mang vào Màn n"; Viễn chinh: túi có HP, cảnh báo giá di vật, tab Di vật có nhật ký chuyến khớp những gì bạn đã làm.
- [x] Lúc chọn thẻ nâng cấp, túi đang mở tab Chỉ số: rê chuột qua từng thẻ → khối "Nếu lấy" và số xanh "→" đổi theo thẻ dưới chuột; rời chuột thì về thẻ đã bấm chọn (nếu có). Thẻ mở cộng hưởng có dòng "Sẽ mở ★ …".
- [x] Trong trận, tab Chỉ số có "Trận này" (quái đã hạ, và các dòng gai/dội/Mai rùa/chạy đà/mở màn/chuông khi build có) và khối Kỹ năng ("còn n giây" cho kỹ năng đang hồi). Viễn chinh có Túi cứu trợ/Chuông phản đòn: hồi chiêu Cá/Khiên đỏ và dài hơn.
- [x] Máy viên nang: bấm viên → nút Túi đồ mờ tới khi viên nang bật nắp; túi đang mở không lộ phần thưởng trước khi bật.
- [x] Màn hình thấp/scale cao: túi không tràn, cuộn được; app fullscreen: túi ẩn rồi hiện lại.
- [x] Màn hình thấp/scale cao: túi đồ cuộn được, nút Đóng vẫn thấy. App fullscreen: sảnh (đang ở túi đồ) ẩn rồi hiện lại vẫn ở túi đồ.

## Kế hoạch 22/10 — chuẩn hóa, lịch, quay lại bậc (2026-10-22)
- [ ] Chơi bóng: bấm quả bóng không làm mất focus app đang gõ; quả bóng không hiện trong Alt+Tab.
- [ ] Bong bóng thoại (cỡ chữ 13) vẫn dễ đọc; menu chuột phải mèo/món trưng bày bình thường.
- [ ] Viễn chinh: sau khi quay bậc có nút "↻ Quay lại bậc này · −N HP"; bấm: viên nang mở, điểm mới, HP trừ đúng; không còn nút lần hai; đóng/mở app vẫn giữ kết quả; ghé/vào một điểm rồi thì không còn nút.
- [ ] Ngày lễ mới (1/1, 8/3, 20/10): bật Mèo biết nói + AI, mở mèo đúng ngày: câu chào/câu nói theo ngày lễ.
- [ ] HUD trận: kéo tay nắm 6 chấm ở mép phải bằng chuột thật — HUD đi theo, không lấy focus app đang gõ, không ra khỏi màn hình/không đè dải chiến trường; trận sau mở lại đúng chỗ; bấm đúp tay nắm về giữa trên; thử ở màn hình phụ/DPI khác (mỗi màn hình nhớ riêng).

- [ ] Vô tận: Sảnh → thẻ Vô tận (khóa khi chưa thắng Vua Slime; mở thì "Chưa chơi lần nào") → bảng chuẩn bị "Vô tận — chuẩn bị" (‹ Sảnh quay lại) → Sẵn sàng: HUD "Đợt N · còn x slime" (không có tổng), 10 chấm, vương miện ở đợt 10.
- [ ] Vô tận: chơi tới đợt 3 có thẻ nâng cấp; đợt 10 là Vua Slime; túi đồ ghi "Vô tận · Đợt N"; bấm Kết thúc → bảng "Hết lượt Vô tận · Đạt đợt N", kỷ lục; Chơi lại / Đổi trang bị / ‹ Sảnh / Về desktop; thẻ trên Sảnh hiện "Xa nhất: đợt N". Cảm nhận độ khó (đợt bao nhiêu thì khó?) để chỉnh `endless` trong battle.json.

## Kế hoạch 22/10 (đợt 2) — mũ slime, thẻ trạng thái
- [ ] Mũ slime (to hơn 1,25×): đeo ở Cài đặt → Mèo, xem khi đứng/ngủ/trèo/bị bế: nằm giữa hai tai, không che mắt.
- [ ] Thẻ trạng thái: chờ mèo nói một câu (hoặc AI hiện bong bóng ý nghĩ) rồi mở thẻ: dòng "Vừa nói: “…” · vừa xong" / "Vừa nghĩ: đói bụng · …"; trước khi mèo nói gì thì không có dòng này.
- [ ] Kỹ năng thành dữ liệu: rê chuột lên 5 nút kỹ năng trên HUD (vi và en): tên/mô tả như trước, số đúng.
- [ ] Phần thưởng Vô tận: Sảnh → thẻ Vô tận có "Thưởng k/5 · tiếp: qua đợt N"; Phần thưởng có mục "Phần thưởng Vô tận" (5 ô, khóa/✓). Chơi qua đợt 10 (hạ Vua): banner "★ Mở khóa: Vòng cổ ∞!", bảng kết quả "Mở khóa: …", Cài đặt → Mèo → Phụ kiện có Vòng cổ ∞. Nếu đã từng tới đợt 11+ trước bản này thì vòng cổ có sẵn (Mới).
- [ ] Qua đợt 20: Đồng hồ cát chọn được trong Cài đặt → Mèo (kỷ vật trưng bày), cầm/ném/rơi như cúp. Xem 3 phụ kiện trên mèo thật (vòng cổ, vành sao, áo choàng) — nếu chưa đạt thì có thể xem ảnh trong Phần thưởng.

## 1.4.1 — cài đặt (chào mừng, khởi động cùng Windows, gỡ)
- 2026-10-23: người dùng cài thử Setup 1.4.1 (2227a88) trên máy dev, xác nhận "ổn": màn hình chờ, hộp chào mừng, bàn giao từ bản copy, Cài đặt → Chung; gỡ trong app (không xóa dữ liệu): agent kiểm tra thư mục cài, mục Apps, 2 shortcut, mục Run đều mất, dữ liệu còn. Chưa thử: khởi động lại máy, xóa dữ liệu khi gỡ, cập nhật qua GitHub.
- [ ] Cài 1.4.1 bằng Setup.exe (máy chưa cài, hoặc gỡ 1.4.0 trước): hình con mèo khi giải nén; mèo mở kèm hộp chào mừng (mẹo + ô Khởi động cùng Windows tích sẵn). Bỏ tích → Bắt đầu: Cài đặt → Chung ô tắt. Gỡ, cài lại để thử với ô tích: Task Manager → Startup apps có Desktop Pet; khởi động lại máy thì mèo tự mở (bản đã cài, không phải artifacts\win-x64).
- [ ] Từng bật khởi động cùng Windows cho bản copy thư mục: cài 1.4.1, để ô tích → bấm Bắt đầu: khởi động lại máy chỉ mở bản đã cài.
- [x] Gỡ cài đặt: Startup apps không còn Desktop Pet; mục của bản copy thư mục khác (nếu có) vẫn còn.
- [ ] Máy đã cài 1.4.0: nhận 1.4.1 qua khay (Cập nhật lên bản 1.4.1) — không hiện hộp chào mừng (chỉ lần cài đầu); mèo mở lại thì khay báo "Đã cập nhật lên bản 1.4.1", bấm vào mở trang Release 1.4.1 (có hướng dẫn cài + ghi chú từ CHANGELOG).
- [x] Hộp chào mừng: đổi Language / Ngôn ngữ → chữ trong hộp đổi ngay; bấm Bắt đầu → menu mèo, khay, Cài đặt theo ngôn ngữ đã chọn.
- [x] Đang chạy bản copy thư mục (artifacts\win-x64) rồi gỡ bản cài và cài lại Setup.exe: bản copy tự đóng (không còn icon khay thứ hai), bản cài mở kèm hộp chào mừng.
- [x] Cài đặt → Chung → Cập nhật (bản đã cài): hiện 1.4.1; bấm Kiểm tra cập nhật → nút "Đang kiểm tra…" rồi khay báo "đang dùng bản mới nhất"; tắt "Tự động tải và cài bản mới" → Lưu → mở lại Cài đặt vẫn tắt. Bản copy thư mục: mục này mờ, có dòng "không tự cập nhật".
- [ ] README trên GitHub: link "⬇ Download" tải thẳng Setup.exe của bản mới nhất.
- [x] Gỡ trong app (bản đã cài 1.4.1): Cài đặt → Chung → "Gỡ cài đặt Desktop Pet…" → Hủy: không có gì xảy ra. Lại bấm → Gỡ cài đặt (ô dữ liệu KHÔNG tích): mèo đóng, Settings → Apps không còn Desktop Pet, shortcut mất, Startup apps không còn, `%LOCALAPPDATA%\DesktopPet` còn. Cài lại: mèo nhớ tên/bộ lông.
- [ ] Gỡ trong app có tích "Xóa cả dữ liệu của mèo": sau khi gỡ, `%LOCALAPPDATA%\DesktopPet` không còn (sao lưu thư mục này trước nếu muốn giữ). Bản copy thư mục: không có nút gỡ.

### Menu chuột phải gọn (2026-10-23)
- [ ] Menu còn 10 dòng: Cho ăn, Chơi ›, Chế độ chiến đấu, Làm ngay ›, Cảm xúc ›, Chế độ yên lặng, {tên mèo} ›, Cài đặt…, Khác ›, Thoát mèo. Menu con mở mượt, không bị cắt ở mép màn hình.
- [ ] Chơi › Chơi bóng: bóng ra; mở lại menu thấy "Cất bóng" ngay dưới Chơi, bấm cất được. Khi mèo chưa rảnh (đang bế/ngủ/trận) Chơi bị mờ.
- [ ] {tên mèo} › Trạng thái / Nhật ký / Chỗ nghỉ yêu thích chạy như trước. Khác › Âm thanh bật/tắt có hiệu lực và khớp Cài đặt → Âm thanh.

### Đáp đất chuẩn hóa mèo = món trưng bày (2026-10-23)
- [ ] Ném/thả mèo từ cao: chạm sàn bẹp rõ (rơi càng cao càng bẹp), bật lại hơi dài người rồi lắc nhẹ ~0,4 s; bụi hai bên chân, đứng yên tại chỗ đáp khi mèo nảy lên; tiếng "squish" một lần cho cả chuỗi nảy. Có quá "cao su" không, mặt có méo khó chịu không.
- [ ] Ném ngang trúng cạnh màn hình: mèo bẹp ngang và nghiêng ngược lại một chút. Đáp xuống mép cửa sổ và đệm slime: cùng hiệu ứng.
- [ ] Mèo ngủ rơi khỏi gờ (cửa sổ biến mất): đáp êm, không bụi, không tiếng, vẫn ngủ. Tắt âm/volume 0/fullscreen: không tiếng. Pause giữa lúc nảy: đứng hình.
- [ ] Món trưng bày rơi/ném: như trước (bẹp, nhún, bụi, "squish").
- [ ] Đợt 2: khi đáp tai cụp, chân trước xoè, đuôi giật lên rồi về; thả từ cao (từ ~3 lần chiều cao mèo trở lên) mắt nhắm tịt "> <" ~0,3 s. Nhảy thường (AI/tự nhảy) chỉ giật tai nhẹ, không nhắm mắt. Không bật cảm xúc Chóng mặt. Đang có cảm xúc (Vui/Giận…) thì mặt cảm xúc vẫn hiện bình thường.

### App trên màn hình trong trận (2026-10-23, bản desktop giả)
- [ ] Vào trận Phiêu lưu/Viễn chinh/Vô tận: 5 icon (Tab Lốc Xoáy, Bão Thông Báo, End Task, Ctrl+Z, Thùng Rác) lần lượt bật ra trên các ô kiểu desktop phía trên dải chiến trường, có mũi tên shortcut và tên. Không đè lên HUD (kéo HUD đè lên thì icon dời đi). Trận tự do: không có icon.
- [ ] Con trỏ ở xa: icon mờ đi; đưa chuột lại gần: rõ dần. Rê lên icon: khung chọn + ghi chú bên cạnh (tác dụng, % sạc). Click xuyên qua khoảng trống quanh icon tới app bên dưới.
- [ ] Đầu trận: Tab Lốc Xoáy sạc sẵn nhiều nhất (60%), Bão Thông Báo 40%, còn lại 20%; Tab đầy sau ~20 s (cả ở màn 1 ngắn). Màn sau / bước Viễn chinh sau: sạc lại từ đầu.
- [ ] Icon sạc dần (lớp tối rút xuống), đầy thì phát sáng vàng, sao lấp lánh, banner "App trên màn hình đã sạc đầy!" lần đầu. Bấm khi chưa đầy: báo "% đang sạc".
- [ ] Bấm từng app khi đầy (banner tên + tiếng; thứ được gửi bay vòng từ icon xuống rồi mới có tác dụng): tab xoay quanh người mèo; chuông rơi lên đầu từng slime, slime choáng; cửa sổ "Không phản hồi" bay tới slime nhiều máu nhất, nó biến mất (Vua chỉ mất 1/4 máu); Ctrl+Z (sau khi bị đánh) quả cầu bay về mèo, máu quay lại, mũi tên quay quanh mèo; Thùng Rác: 2 slime gần mèo bị hút bay vào thùng, nắp mở. Đang cầm/ném mèo: báo "Đặt mèo xuống…".
- [ ] Từ đợt 2: mỗi đợt một icon nhảy sang ô khác (nhảy vòng, đáp bẹp).
- [ ] Màn 2 trở đi / Viễn chinh / Vô tận từ ~đợt 9: thỉnh thoảng một slime bắn cục nhớt xanh lên một icon; slime đó có virus nhỏ trên đầu; icon bị phủ nhớt, virus mặt cau ở góc, rung, ngừng sạc, thỉnh thoảng bò sang ô bên. Banner "☣ … nhiễm virus!". Icon nhiễm phá mèo: Tab bật quảng cáo (bấm × để tắt, để lâu thả slime); chuông đỏ rơi lên đầu mèo (mất máu, Khiên chặn được); "Không phản hồi" làm mèo choáng; Ctrl+Z hồi máu slime; Thùng Rác nhả slime.
- [ ] Gỡ virus: bấm icon 3 lần (bọt xà phòng mỗi lần, chấm đỏ dưới icon giảm dần) → lấp lánh, banner "✓ … sạch virus!"; hoặc hạ con slime đeo virus. Bấm được cả khi đang cầm mèo.
- [ ] Mở túi đồ hoặc bảng chọn thẻ đè lên icon: icon đó dời sang ô trống. Đổi Scale (Settings → Display) giữa trận: icon giữ đúng cỡ, đúng chỗ; đồ bay vẫn trúng mèo/slime.
- [ ] Pause/menu: icon và mọi thứ đang bay đứng yên. App fullscreen: icon ẩn, thoát fullscreen hiện lại. Kết thúc trận: icon mờ dần rồi biến mất. Đổi màn hình/độ phân giải giữa trận: icon về đúng ô trên màn hình mới.
- [ ] Cảm giác: app có quá ít/nhiều, virus có phiền quá không; trận đông quái + icon không giật hơn trước.

### Tiếng Nhật (2026-10-23, J1–J4)
- [ ] Cài đặt → Chung: hộp Language / Ngôn ngữ có English, Tiếng Việt, 日本語. Chọn 日本語 rồi Lưu: menu chuột phải, khay, Cài đặt, thẻ trạng thái, nhật ký đổi sang tiếng Nhật, chữ không vỡ ô (font Yu Gothic). Đổi lại English/Tiếng Việt: mọi thứ như cũ.
- [ ] Ở 日本語 bật AI và Lời nói: mèo nói tiếng Nhật (gọi bạn là ご主人, có tên mèo), cả câu chào, đói, chán, ngủ dậy, ngày lễ; nhắc nghỉ (cả khi tắt AI) bằng tiếng Nhật. Thẻ trạng thái "điều mèo học được" đọc được (tên hành động tiếng Nhật).
- [ ] Ở 日本語 vào trận: HUD, bảng chuẩn bị, chọn thẻ, túi đồ (Chỉ số/Thẻ/Di vật), bản đồ Phiêu lưu/Viễn chinh, kết quả, Vô tận, icon app (Không phản hồi, quảng cáo), chữ trên slime (CHẶN/MIỄN, tag tinh anh), tên quái/thẻ/di vật/kỷ vật đều tiếng Nhật và không bị cắt.
- [ ] Bộ cài: máy Windows tiếng Nhật, lần đầu mở sau khi cài thì hộp chào mừng chọn sẵn 日本語 (máy khác: theo ngôn ngữ Windows như cũ).

### Chuẩn hóa S5 (2026-10-23)
- [ ] Tắt AI (hoặc Python lỗi), bật nhắc nghỉ với khoảng ngắn: câu nhắc tiếng Việt/English đúng câu mới (vd. "Ngồi lâu rồi đó, đứng dậy vươn vai với {tên mèo} nha~"), không còn {name}.
- [ ] Đèn laser, bong bóng thoại, bóng len, các trận đấu (slime, icon app, quảng cáo), bộ lông/phụ kiện: màu và nét như trước.

### Menu theo nhóm + Tùy chỉnh (2026-10-23)
- [ ] Chuột phải mèo: 12 dòng như docs/WINDOWS.md mục Menu; Âm thanh bật/tắt ngay ở menu ngoài; {tên mèo} › Tùy chỉnh…; Vị trí › đến/nhớ/quên chỗ nghỉ và Gọi về màn hình chính chạy như trước; Khác chỉ còn Debug và Giới thiệu; Thoát.
- [ ] Tùy chỉnh: đổi tên, tính cách, sinh nhật, bộ lông, phụ kiện (xem trước động), leo cửa sổ, góc trưng bày + món trưng bày → Lưu: áp dụng ngay; Hủy/Esc không đổi gì. Tinh chỉnh hành vi mở giữa cửa sổ Tùy chỉnh.
- [ ] Cài đặt chỉ còn Lời nói, Âm thanh, Chung. Mở cả Cài đặt và Tùy chỉnh, sửa cả hai, Lưu từng cái: cái nào chỉ đổi phần của nó.
- [ ] Khay: có Tùy chỉnh…. Chuột phải món trưng bày → Tùy chỉnh…: mở và cuộn tới thẻ Kỷ vật. Màn hình thấp: cửa sổ vừa, cuộn được, nút Lưu/Hủy luôn thấy.

### Sửa lỗi + hoạt ảnh (2026-10-23, tiếp)
- [ ] Cài đặt → Âm thanh: kéo âm lượng về 0% (không cần Lưu): mọi tiếng mèo và hiệu ứng (vung kiếm, slime, bóng, kỷ vật) im hẳn; âm đang kêu tắt ngay. Kéo lên 50%: nhỏ rõ so với 100%. Hủy: âm lượng trở lại như trước khi mở; Lưu: giữ. Hiệu ứng trận không còn cắt ngang nhau giữa chừng.
- [ ] Tab Lốc Xoáy: 3 tab trình duyệt (chấm màu, dấu ×) xoay quanh chân mèo, vệt bay cong; tab phía sau mờ/nhỏ và khuất sau mèo, tab phía trước đè lên chân/thân mèo (vòng liền, hai nửa không lệch); nhấc/ném mèo lúc đang xoay thì vòng ở lại sàn, không vẽ đôi; không có vạch trắng thẳng cắt ngang mặt mèo. Xem trên nền sáng và tối.
- [ ] Tùy chỉnh → chọn món trưng bày khác → Lưu: món cũ "phụt" khói biến mất, món mới hiện ra đúng chỗ (như menu chuột phải món đồ). Kệ đang ẩn/đang tắt góc trưng bày: đổi ngay, không lỗi.
- [ ] Menu khay và menu chuột phải → Gọi mèo về màn hình chính: mèo phụt khói biến mất ở chỗ cũ (cả từ màn hình phụ/trên cửa sổ), hiện ra giữa màn hình chính có khói + sao, rơi nhẹ xuống rồi Ngạc nhiên. Đang tự điều khiển cảm xúc: không Ngạc nhiên. Mèo đang ngủ: vẫn ngủ, không Ngạc nhiên. Đang Pause: dịch chuyển ngay. Trong lúc khói không nhấc được mèo. Mèo bị kẹt ngoài màn hình vẫn gọi về được.
- [ ] Túi đồ → Kỹ năng: dòng "Lượng hồi" có icon trái tim xanh dấu + (khung giống các ô kỹ năng).
- [ ] Trong trận, mèo đang choáng (sao quay) → Tạm dừng: sao, đầu, kiếm đứng yên hẳn; Tiếp tục: chạy tiếp. Mở túi đồ/menu chuột phải/bảng chọn thẻ cũng vậy.
- [ ] Trận đang dừng (nút HUD, túi đồ, chọn thẻ, menu): bấm/kéo mèo không nhấc lên được; Tiếp tục thì bế được như thường. Mèo ở màn hình khác (trận chờ mèo) vẫn bế về được.
- [ ] Viễn chinh: giữa trận bấm Kết thúc trận / tắt Battle Mode / thoát app → mở lại Viễn chinh: chuyến còn nguyên, HP như lúc vào trận, ghi chú "chuyến đi được giữ nguyên, đánh lại được", đánh lại đúng trận đó. Thua trận (mèo hết máu) vẫn kết thúc chuyến như cũ.
- [ ] Trong trận, slime đứng sát/sau lưng mèo: chữ tinh anh (ĐÁNH NHANH…), số sát thương, CHẶN/MIỄN hiện ngay cạnh thân mèo, không bị mèo che.

### Đồng hồ cát chảy (2026-10-24)
- [ ] Chọn "Đồng hồ cát vô tận" làm món trưng bày: cát tím chảy từ bầu trên xuống bầu dưới, có dòng cát mảnh, hết sau khoảng 1 phút thì đứng yên. Pause: cát dừng; Tiếp tục: chảy tiếp.
- [ ] Cầm đồng hồ cát lên rồi thả/ném: nó lật ngược (~0,35 s) rồi cát chảy lại từ phần đã chảy xuống. Chỉ bấm (không kéo) thì "boing", không lật. Ô trong Phần thưởng vẫn vẽ tĩnh. CPU không tăng rõ khi đồng hồ cát đang hiện.

### Tắt app trong trận (2026-10-24)
- [ ] Bảng chuẩn bị (Phiêu lưu, Viễn chinh, Vô tận) có ô "App trong trận" dưới Độ khó, kèm câu giải thích; trận tự do không có. Bấm để tắt/bật; Sẵn sàng thì lần sau nhớ lựa chọn, bấm Bản đồ/Đóng thì không đổi.
- [ ] Tắt rồi vào trận Phiêu lưu: không icon app, không virus, không quảng cáo; thắng màn vẫn mở màn sau, nhận phần thưởng. Bảng kết quả ghi "Kỷ lục màn (trang bị này, không app)" và kỷ lục tách khỏi lúc có app.
- [ ] Viễn chinh: chuẩn bị chuyến với app tắt → cả chuyến không có app (kể cả khi đổi lựa chọn ở bảng chuẩn bị khác giữa chuyến); bản đồ ghi "· không app" cạnh độ khó. Chuyến có app như cũ.
- [ ] Vô tận tắt app: kỷ lục riêng ("· không app"), mốc phần thưởng vẫn mở khi qua đợt 10/20…; thẻ Sảnh "xa nhất" chỉ tính lượt có app.

### App độc: vòng loading (2026-10-24)
- [ ] Trận Phiêu lưu từ màn có virus (threat ≥ 1,5): thỉnh thoảng thay vì virus vào icon, một vòng loading xuất hiện ngay chỗ mèo (mảng xanh dưới sàn + vòng xoay chấm trên cao), banner lần đầu giải thích. Mèo trong vòng chạy/đánh chậm rõ; ra khỏi vòng thì bình thường.
- [ ] Bấm vòng xoay: vòng đóng ngay (có tiếng "pop"). Để yên ~6 s: tự đóng. Cầm mèo lên: vòng đứng yên, không làm chậm. Pause/menu: đứng yên. Dọn xong đợt: vòng đóng. Vòng xoay không bị mèo che, bấm được.

### App độc: cập nhật bắt buộc (2026-10-24)
- [ ] Trong trận có virus: đôi khi một cửa sổ "CẬP NHẬT" rơi xuống giữa mèo và slime; banner lần đầu giải thích. Slime bên kia không đi qua, không đánh được mèo; mèo đi tới đánh tường (tiếng "clank", tường rung, % nhảy lên).
- [ ] Bấm "Nhắc sau ×2" hai lần: lần đầu còn "Nhắc sau", lần hai tường biến mất. Để yên ~8 s (ít hơn nếu mèo đánh): tự xong. Trùm vẫn nhảy qua. Bấm vào thân tường không chọn slime phía sau. Cầm mèo lên thả sang bên kia tường: mèo đánh slime bên đó bình thường.

### App độc: virus.exe (2026-10-24)
- [ ] Trong trận có virus: đôi khi một file "virus.exe" (tờ giấy có dấu ☣ tím, chữ xanh) hiện ở ô trống giữa các icon; banner lần đầu giải thích; slime thả nó đeo dấu virus. Vài giây sau nó nhân thêm bản ở ô kề bên; đủ 4 bản thì một icon bị nhiễm và các file biến mất.
- [ ] Bấm từng file: biến mất (tiếng "pop"). Hạ slime đeo dấu: mọi file biến mất. Cầm mèo lên: file không nhân thêm. Pause: đứng yên. Fullscreen: file ẩn cùng icon. Kết thúc trận: file đóng. Icon app không nhảy vào ô có file.
- [ ] Kỷ lục Phiêu lưu màn 2–5 và Vô tận bắt đầu lại (phiên bản luật mới; kỷ lục cũ vẫn trong file). Chuyến Viễn chinh đang đi vẫn tiếp tục được.

### Cân bằng trận 2026-10-24 (mốc 1–3)
- [ ] Đẩy hồi 5 s (trả lại 2026-10-24), Cá hồi máu 14 s (số trên ô kỹ năng và mô tả rê chuột khớp). Tab Lốc Xoáy sạc nhanh hơn (~40 s). Bão Thông Báo chỉ rơi lên 4 slime gần mèo nhất.
- [ ] Đòn combo thứ 3 trúng một slime đứng sát slime khác: con bên cạnh cũng mất máu (số nhỏ hơn) và có tia lửa. Búa: thỉnh thoảng một nhát làm slime choáng (đứng khựng, hủy đòn đang gồng); Vua không bao giờ bị búa làm choáng.
- [ ] Slime nhanh đang gồng mà bị Đẩy/chuông/búa làm choáng: các cú lao sau vẫn trúng nhưng không làm mèo choáng nữa.
- [ ] Mèo đang choáng (sao trên đầu): bấm/kéo mèo không nhấc lên được; hết choáng thì nhấc bình thường. Trong trận lắc mèo thật mạnh: không hiện Chóng mặt; ngoài trận lắc mạnh vẫn Chóng mặt như cũ.
- [ ] Vua Slime chương 1 khi bật khiên: vương miện sáng rồi phong tinh anh cho một quân bất kỳ, có lúc là slime thảo dược. Đòn dậm đất mất ít máu hơn trước (~22 ở mức Thường, chưa nhân threat). Slime đá quay đầu chậm (~2 s): bế mèo ra sau lưng đá có thời gian đánh.
- [ ] App độc tới ngẫu nhiên trong lúc đánh (khoảng 15–30 s một lần, tối đa 2 loại cùng lúc): vòng loading, cập nhật bắt buộc, virus.exe, hoặc 3 quảng cáo trải khắp bề ngang sàn (tắt được bằng × khi Tab sạch).
- [ ] Vòng loading rộng hơn, kéo dài 12 s: nhấn giữ vòng xoay ~2 s (vòng vàng đầy dần) thì tắt; thả chuột giữa chừng thì phải giữ lại từ đầu. Cập nhật bắt buộc cần bấm Nhắc sau 4 lần.
- [ ] Tab nhiễm: 3 quảng cáo bay xuống, nút × có ổ khóa, bấm × không tắt (banner nhắc gỡ Tab trước), quảng cáo không tự thả slime khi đang khóa; gỡ Tab (5 lần bấm) xong thì × tắt được.
- [ ] Chuông nhiễm: mèo mất máu và choáng 1 s. Ctrl+Z nhiễm: slime được hồi có vòng xanh "MIỄN" ~2 s, đánh không mất máu. Thùng Rác nhiễm: nhả một slime tinh anh (viền vàng, banner), mỗi lúc chỉ một con. Có thể tới 3 icon nhiễm cùng lúc; mỗi icon cần 5 lần bấm.
- [ ] Slime thường tinh anh (cả khi được Vua phong) bắn virus vào icon vài giây sau khi xuất hiện, tối đa 2 lần mỗi đợt.

### Cân bằng trận 2026-10-24 (mốc 4: Vô tận, thẻ, cộng hưởng)
- [ ] Trận tự do: đợt cuối là Vua chương 1 (bật khiên, gọi quân, phong tinh anh), không phải Vua thường.
- [ ] Vô tận: thẻ sau mỗi 2 đợt; đợt 10 là Vua chương 1; dọn xong đợt trùm thì chọn liền 2 thẻ (bảng thẻ mở lại ngay sau khi xác nhận thẻ đầu). Từ đợt 30 đợt trùm có cả Vua thường đi cùng (thanh máu trùm trên HUD hiện từng con một).
- [ ] Thẻ mới xuất hiện và có hình riêng: Móng vuốt, Kết liễu (hiếm), Nhịp dài, Lướt gió (hiếm), Hồi phục, Kiên cường; thẻ tiện ích Tay nhanh, Sạc pin, Hút năng lượng, Bom to, Khiên dày (Sạc pin/Hút năng lượng không bao giờ hiện khi tắt app). Dòng "Đã có n/m" trong Vô tận ghi giới hạn cao hơn (Sắc bén /5).
- [ ] Vô tận, gom 6 thẻ cùng nhóm: banner ★ tầng mới (Bất khuất / Chấn động / Gió lốc), 9 thẻ: Pháo đài / Hành quyết / Dư ảnh+. Phiêu lưu và Viễn chinh: túi đồ và HUD không nhắc tới tầng 6/9.
- [ ] Vô tận đợt cao: tinh anh nhiều dần (từ đợt 20 có thể 2 con cùng lúc). Tới đợt 48: mọi cộng hưởng tự bật (banner).
- [ ] Kỷ lục Phiêu lưu và Vô tận bắt đầu lại (phiên bản luật mới); Viễn chinh đang đi vẫn tiếp tục.

### Hướng dẫn phiêu lưu (2026-10-24, thay hướng dẫn tại chỗ)
- [ ] Máy mới (xóa/đổi tên %LOCALAPPDATA%\DesktopPet\preferences.json trên máy thử, KHÔNG trên máy chính): menu "Sounds" và Cài đặt → Âm thanh tick sẵn, mèo có tiếng ngay. Máy đã có preferences giữ lựa chọn cũ.
- [ ] Sảnh: thẻ "Hướng dẫn phiêu lưu" ghi "Học chơi qua từng màn…"; bản đồ và túi đồ cũng gọi là Hướng dẫn phiêu lưu. Bảng chuẩn bị màn chương 1 có ô "Hướng dẫn trong trận" (mặc định tick, cả màn đã thắng; bỏ tick rồi Sẵn sàng thì lần sau vẫn bỏ tick), link "Màn này dạy gì" mở danh sách bài học; không còn link "Cách chơi" và hướng dẫn 5 thẻ.
- [ ] Trận tự do, Viễn chinh, Vô tận: không có hướng dẫn. Đẩy hồi 5 giây (ô kỹ năng, mô tả).
- [ ] Màn 1: khi slime gồng tới mèo → trận đứng hình (slime, đồng hồ dừng, mèo không đi), màn hình tối, mèo và slime sáng viền vàng, dòng chữ "Kéo mèo lên…"; kéo được mèo trong lúc đứng hình → trận chạy tiếp. 5 lần + 1 lần cho slime nhanh lao tới, sau đó không đứng hình nữa.
- [ ] Màn 2 lúc mở màn: lần lượt máu, đợt, thanh kỹ năng, từng kỹ năng (tên + mô tả), túi đồ, icon app — mỗi cái bấm "Hiểu rồi". Lần chọn thẻ đầu: sáng bảng thẻ, rồi dòng nhóm thẻ của thẻ đầu. Màn 2 không có app độc/icon nhiễm.
- [ ] Màn 3: đồng hồ sống sót; khoảng giây 6 quảng cáo, 14 vòng loading (giữ chuột thì trận chạy tiếp và vòng tắt sau 2 s), 27 Cập nhật bắt buộc, 34 virus.exe, 47 icon bị nhiễm — mỗi cái đứng hình + sáng đúng chỗ, bấm/giữ đúng thứ hoặc "Hiểu rồi" thì chạy tiếp. Không có tinh anh. Sau giây 50 app độc ngẫu nhiên như thường.
- [ ] Màn 4: giới thiệu Đẩy; slime thảo dược gồng hồi máu → đứng hình, sáng slime + ô Đẩy; bấm Đẩy (chỉ còn sáng slime) rồi bấm slime → chạy tiếp (tối đa 4 lần). Gặp đủ 4 loại, mỗi loại một tinh anh (cả ở Dễ); lần đầu thấy tinh anh có bước giới thiệu.
- [ ] Màn 5: Vua xuất hiện → giới thiệu; lần đầu Vua gồng dậm gần mèo → sáng mèo + vùng dậm (+ ô Khiên nếu sẵn), kéo mèo hoặc bấm Khiên; lần đầu bật khiên gọi quân → giới thiệu; lần đầu vương miện sáng (Đẩy sẵn) → Đẩy rồi bấm Vua, chỉ 1 lần.
- [ ] Màn 3: giữ vòng loading tới khi nó tự tắt, thả chuột, rồi chọn thẻ ngay (không phải đổi cửa sổ). Quảng cáo, vòng loading, tường, virus.exe, icon nhiễm đã hiện đủ (không còn bé/đang rơi) khi hướng dẫn sáng.
- [ ] "Bỏ qua hướng dẫn": tắt cho hết trận. Pause/menu/túi đồ/fullscreen trong lúc đứng hình: hướng dẫn ẩn, Tiếp tục thì hiện lại. Lớp tối không chặn chuột (click xuyên tới mèo/HUD/icon); dòng chữ không lấy focus. Màn hình phụ/DPI khác: lớp tối phủ đúng màn hình trận, chỗ sáng đúng vị trí.
- [ ] Combo (đòn thứ 3, chữ "COMBO!"): chỉ slime sát sau mục tiêu (trong tầm kiếm/búa) có tia lửa và mất máu; slime đứng xa không bị lan.
- [ ] Túi đồ → tab Thẻ (Vô tận có nhiều thẻ; Phiêu lưu/Viễn chinh): cộng hưởng là các ô có icon; ô đã bật lóe sáng một lần khi mở tab; rê chuột lên ô thì dòng dưới hiện mô tả; không chữ nào tràn ra ngoài túi đồ (thử cả tiếng Anh/Nhật).

## Visual Polish 2026-10-25 — cần kiểm tra tay

- [ ] Mở trạng thái khi AI bật/tắt: chân dung/icon và thanh nhu cầu không đè nhãn/số, không đánh thức hoặc di chuyển mèo.
- [ ] Nhật ký ít/nhiều dữ liệu, menu/cài đặt/welcome bằng vi/en/ja: không tràn chữ; Tab/focus/click và nút đóng vẫn hoạt động.
- [ ] Sảnh, bản đồ, prep, thẻ, túi đồ, reward, kết quả: hình cũ và huy hiệu mới rõ; popup nhỏ vẫn scroll và thao tác được.
- [ ] Desktop sáng/tối/nhiều màu, DPI 100/125/150/200%, chuyển màn hình: không clipping, không che cảnh báo; hình trang trí không giữ chuột.
- [ ] Pet Mode, pause/fullscreen, sleep/wake/drag/throw và trận hoạt động như trước; không có animation trang trí chạy liên tục khi nghỉ.
## Minh họa lớn 2026-10-25 — cần nghiệm thu desktop thật

- [ ] Sảnh/map/prep có banner theo mode; máy màn hình thấp cuộn được và nút Ready/Back luôn truy cập được.
- [ ] Kết quả dài (kỷ vật, record, cảnh báo save) cuộn được, các nút không mất; replay/đổi trang bị/desktop đúng flow.
- [ ] Settings/Customize/diary/welcome/túi đồ/reward không tràn vi/en/ja; mở đóng nhiều lần không giữ window/timer cũ.
- [ ] Status portrait đúng coat/accessory/ngủ/emotion; mở status không thay hành vi mèo.
- [ ] Thẻ upgrade vẫn click/hover/confirm đúng; kiểm tra ở DPI thật 100/150/200% và nhiều màn hình, giữ cảnh báo battle dễ đọc.

## Icon toàn app 2026-10-26 — cần nghiệm thu desktop thật

- [ ] Chọn nâng cấp ở Viễn chinh (tiếng Việt): bảng vừa đúng 3 thẻ, tiêu đề xuống dòng, không còn khoảng trống bên phải; "★ Thẻ hiếm · một lần mỗi build" xuống dòng, không bị cắt.
- [ ] Cài đặt và Tùy chỉnh: mỗi thẻ mục có icon cạnh tiêu đề, vi/en/ja không tràn chữ.
- [ ] Bản đồ Viễn chinh: nút hiện kiếm/sao/vương miện/lửa trại/cuộn giấy/dấu hỏi rõ ở mọi trạng thái (đang chọn, đã thắng, đã qua, chưa lộ); bấm nút vẫn chọn đúng.
- [ ] Bản đồ Phiêu lưu: cờ → kiếm → khiên → nấm → vương miện dưới các màn; Phần thưởng ở sảnh có icon từng mục.
- [ ] Hộp thoại (Quên thói quen, Gỡ cài đặt): huy hiệu dấu hỏi / biển cảnh báo.
- [ ] Menu khay: Tùy chỉnh, Cài đặt, Gọi mèo về, Cập nhật, Thoát có icon giống menu chuột phải; nét rõ ở DPI 100/150/200%; Tạm dừng và AI vẫn có ✓.
- [ ] Túi đồ tab Thẻ (1–2, 3, 5+ thẻ) và tab Di vật (Viễn chinh): ô lấp đủ bề rộng khung, không dải trống bên phải; rê chuột lên thẻ vẫn hiện mô tả.

## Visual combat 2026-10-26 — cần nghiệm thu trong trận thật
- [ ] Ô kỹ năng khi hồi chiêu: nhìn ra icon, số giây ở góc dưới phải; vừa hồi xong thì lóe vàng một lần; Pause/tiếp tục không làm lóe.
- [ ] Số sát thương: thường kem, combo cam to hơn, chí mạng (thẻ Móng vuốt) vàng to có "!"; không bị thân mèo che.
- [ ] Bóng chân dưới slime trên hình nền sáng/tối (nhẹ, không thành mảng đen); mèo không có bóng; vòng đỏ cảnh báo trùm vẫn rõ.
- [ ] Hạ slime/trùm: giọt cùng màu bắn ra rồi rơi, tia sáng vàng ngắn; trận đông quái không giật thêm.
- [ ] Hướng dẫn phiêu lưu: chỗ sáng vẫn ôm đúng thanh kỹ năng và nút Đẩy.

## Liên kết quái 1.5 M01 (2026-10-28) — chỉ xem được bằng battle.json tùy chỉnh
Trận gốc chưa có quái dùng liên kết; bỏ qua mục này nếu không muốn thử file tùy chỉnh (quái thật có từ M09). Ví dụ file của người dùng: `{ "schemaVersion": 1, "enemies": [ { "id": "runner", "link": { "kind": "tether" } } ] }` (slime nhanh tự buộc dây vào slime khác ở gần; chơi trận tự do hoặc màn có slime nhanh). Đổi `"tether"` thành `"rhythm"` hoặc `"aura"` để xem hai loại kia.
- [ ] Dây nhầy xanh võng nhẹ nối hai slime, đi theo khi chúng di chuyển; hạ con giữ dây thì con kia choáng; không giật khi đông quái.
- [ ] Thử `rhythm` (chấm nhịp trên đầu, chấm cuối đỏ rồi cả nhóm gồng cùng lúc), `aura` (vòng vàng trên sàn), nét vẽ rõ trên hình nền sáng/tối và DPI 100/150/200%; click vào khoảng trống giữa slime vẫn xuyên xuống desktop.
- [ ] Pause giữa lúc có dây/nhịp: mọi thứ đứng yên, tiếp tục thì chạy tiếp.

## Chọn kỹ năng 1.5 M02 (2026-10-28)
- [ ] Trận tự do / Vô tận / chuẩn bị chuyến Viễn chinh: bảng chuẩn bị có mục "Kỹ năng mang theo (5/5)", hàng trên 5 kỹ năng cũ đã chọn, hàng dưới 5 kỹ năng mới mờ có ổ khóa; rê chuột lên ô thì dòng dưới hiện tác dụng (ô khóa ghi "Chưa mở"). Kiểm tra cả vi/en/ja và DPI 100/150/200% (ô và ổ khóa không bị cắt, bảng không bị cao quá màn hình nhỏ).
- [ ] Bấm bỏ một kỹ năng: còn 4/5, nút Sẵn sàng mờ và dòng trạng thái nhắc chọn đủ 5; bấm lại thì Sẵn sàng sáng. Ô khóa bấm không có tác dụng.
- [ ] Màn chương 1 (Phiêu lưu): không có mục kỹ năng; HUD trong trận vẫn đúng 5 ô cũ như trước; Hướng dẫn phiêu lưu vẫn chỉ đúng thanh kỹ năng và nút Đẩy.

## Mèo đánh khi đang cầm, tương tác ném 1.5 M03 (2026-10-28) — chỉ xem được bằng battle.json tùy chỉnh
Trận gốc chưa có quái bay hay quái có tương tác ném (quái thật có từ M14/M15); mục này không bắt buộc. Vẽ quái bay chưa có (M14), nên quái `hover` vẫn hiện trên sàn dù trận coi nó ở trên cao. Ví dụ file của người dùng: `{ "schemaVersion": 1, "enemies": [ { "id": "runner", "throwHit": { "minSpeed": 600, "damage": 30, "stun": 1.5 } } ] }`.
- [ ] Trận thường: kéo/ném/thả mèo, nhấc mèo cứu, cảm giác giống hệt trước; mèo đang cầm không vung kiếm; slime không đánh được mèo đang cầm/bay.
- [ ] Với file ví dụ: ném mèo mạnh xuyên qua slime nhanh thì nó mất máu và choáng; ném nhẹ hoặc kéo lê qua thì không; slime thường không bị cú ném ảnh hưởng.

## Hiệu ứng trên quái 1.5 M04 (2026-10-28) — chỉ xem được bằng battle.json tùy chỉnh
Trận gốc chưa có vũ khí/thẻ gây hiệu ứng (Bình xịt, thẻ Mưu mẹo, vũ khí mới dùng sau); mục này không bắt buộc. Ảnh độc/đóng băng/dính đã xem qua render UiChecks (nền sáng/tối), chưa xem trong trận thật. Ví dụ file của người dùng (kiếm gây độc mỗi đòn): `{ "schemaVersion": 1, "weapons": [ { "id": "sword", "onHit": { "status": "poison", "seconds": 3, "power": 6 } } ] }` (đổi `poison` thành `chill` với `"power": 0.5`, hoặc `stick` bỏ `power`, để thử hai loại kia).
- [ ] Trận thường không có file: đánh, đẩy, bom, choáng của búa cảm giác như cũ; không thấy huy hiệu hiệu ứng nào.
- [ ] Với file độc: slime trúng đòn phủ tím, bọt nổi, huy hiệu tím có vòng đếm; số tím nhỏ trừ dần; hết giờ thì huy hiệu biến mất. Slime chết vì độc vẫn tính vào số đã hạ.
- [ ] Chill: slime đi/gồng đòn chậm rõ, phủ sương xanh. Stick: slime đứng yên trên vũng keo nhưng vẫn đánh mèo ở gần; Đẩy không đẩy nó đi.
- [ ] Pause (và mở menu chuột phải) giữa lúc có hiệu ứng: vòng đếm và máu đứng yên; tiếp tục thì chạy tiếp. Xem DPI 100/150/200%, huy hiệu không đè thanh máu.

## Đèn laser và Quạt 1.5 M05 (2026-10-28) — trận tự do
Mở Trận tự do → bảng chuẩn bị: bỏ một kỹ năng cũ, chọn Đèn laser và/hoặc Quạt (Kẹp gắp, Kéo cắt, Bình xịt vẫn khóa). Ảnh chấm laser/gió đã xem qua render UiChecks nền sáng/tối, chưa xem trong trận thật.
- [ ] Vô tận / chuẩn bị Viễn chinh: Laser và Quạt vẫn hiện khóa; chương 1 không đổi.
- [ ] Laser: bấm ô (con trỏ thành chữ thập, dòng gợi ý đổi), click một chỗ trên sàn: chấm đỏ có vòng đếm, slime gần đó chạy tới và đứng quanh chấm, không đánh mèo; con trên chấm mất máu dần; ~4 s thì chấm tắt và slime quay lại đánh. Boss không bị dụ.
- [ ] Quạt: bấm ô, giữ chuột trên sàn cạnh đám slime: cánh quạt quay, vệt gió, slime bị đẩy ra xa con trỏ; va nhau/va mép hiện số 4. Rê chuột khi giữ thì gió đi theo. Thả chuột: ngừng, vòng tối trên ô cho thấy hơi đã dùng và hồi dần; giữ tới cạn thì Quạt tự cất, ô chờ ~6 s.
- [ ] Sau khi thả chuột (cả khi cạn hơi giữa chừng): click slime, icon app, thẻ giữa đợt, nút HUD vẫn bình thường (dải trận không giữ chuột).
- [ ] Đang chọn Laser/Quạt: kéo mèo vẫn nhấc mèo, bỏ chọn kỹ năng; mèo đang cầm thì Quạt không thổi. Chuột phải dải trận thì bỏ chọn. Pause/menu giữa lúc có chấm hoặc đang thổi: đứng yên; tiếp tục thì chạy tiếp.
- [ ] Âm thanh: đặt chấm, bật quạt, va chạm có tiếng hoạt hình ngắn, không chồng nhau; DPI 100/150/200% chấm và gió đúng chỗ con trỏ.

## Kẹp gắp, Kéo cắt, Bình xịt nước 1.5 M06 (2026-10-28) — trận tự do
Mở Trận tự do → bảng chuẩn bị: bỏ vài kỹ năng cũ, chọn Kẹp gắp, Kéo cắt, Bình xịt (cả 10 kỹ năng đều chọn được ở trận tự do). Ảnh càng kẹp/nét cắt/tia nước đã xem qua render UiChecks nền sáng/tối, chưa xem trong trận thật, chưa thử chuột thật và DPI thật.
- [ ] Vô tận / chuẩn bị Viễn chinh: 5 kỹ năng mới vẫn hiện khóa; chương 1 không đổi.
- [ ] Kẹp gắp: bấm ô, nhấn giữ một slime thường/slime nhanh/slime thảo dược: nó bị nhấc theo con trỏ (càng kẹp trên dây từ mép trên dải trận), không đánh mèo. Nhấn vào slime đá hoặc Vua: không gắp được. Vung chuột về phía slime khác rồi thả: slime bay vòng cung, trúng con kia thì cả hai hiện số 8, con kia choáng; ném vào mép thì nó mất 8 và choáng khi đáp. Thả nhẹ: chỉ rơi xuống. Giữ quá ~3 s thì slime tự tuột. Ô hồi ~12 s tính từ lúc thả.
- [ ] Kéo cắt: bấm ô, kéo chuột thành một nét qua đám slime: thấy đường đứt nét và cây kéo ở đầu con trỏ; thả chuột thì mỗi slime trên nét hiện số 6, ô hồi ~8 s. Nét rất ngắn (như một click) không làm gì, Kéo cắt vẫn được chọn. (Cắt dây nhầy chỉ thử được bằng battle.json có liên kết `tether`, không bắt buộc.)
- [ ] Bình xịt: bấm ô, giữ chuột cạnh slime: bình xanh và vòng chấm xanh theo con trỏ, slime trong vòng nhỏ lại, có giọt nước và tinh thể băng, đi chậm, mất máu dần (số nhỏ). Thả: ngừng, vòng tối trên ô là nước đã dùng và hồi dần; giữ tới cạn thì tự cất, ô chờ ~8 s.
- [ ] Sau mỗi lần dùng (cả khi slime tự tuột hay cạn nước giữa chừng): click slime, icon app, thẻ giữa đợt, nút HUD vẫn bình thường (dải trận không giữ chuột).
- [ ] Đang chọn một trong ba: kéo mèo vẫn nhấc mèo và bỏ chọn; đang gắp slime mà nhấc mèo (nếu với được) thì slime rơi xuống. Chuột phải dải trận thì bỏ chọn. Pause/menu giữa lúc đang gắp/xịt: đứng yên; tiếp tục thì chạy tiếp.
- [ ] Âm thanh gắp/thả/va chạm/cắt/xịt là cue hoạt hình ngắn, không chồng nhau; DPI 100/150/200% càng kẹp, nét cắt, vòi xịt đúng chỗ con trỏ.
- [ ] Viễn chinh, điểm nghỉ: hiện tại chỉ 5 kỹ năng cũ mở nên **không có** nút "Đổi kỹ năng" (đúng thiết kế). Nút hiện khi chương 2 mở thêm kỹ năng (M21); khi đó kiểm tra: đổi miễn phí, vẫn phải chọn Nghỉ hoặc Lấy thẻ, trận sau mang bộ mới. Ảnh khung đổi kỹ năng đã xem qua render UiChecks (en).

## Mèo nổi giận 1.5 M07 (2026-10-28) — mọi trận
Thanh nộ mỏng dưới thanh HP trên HUD. Ảnh mèo nổi giận (đứng, vung kiếm, bị cầm, bay, có Khiên) và HUD thanh đầy đã xem qua render UiChecks, chưa xem trong trận thật, chưa thử DPI thật.
- [ ] Đầu trận thanh rỗng; mèo đánh trúng hoặc bị đánh thì thanh đỏ cam dài dần (~20 nhát là đầy). Đầy: viền vàng, nhấp nháy, con trỏ thành bàn tay, banner "Thanh nộ đầy!…" một lần, tiếng lấp lánh. Để yên thì mèo **không** tự nổi giận.
- [ ] Click thanh đầy: banner "MÈO NỔI GIẬN!", mèo to dần lên (~1,3 lần) quanh chân, lông ánh đỏ, quầng đỏ sau lưng; thanh đỏ tươi rút dần trong ~6 s rồi mèo nhỏ lại như cũ. Không phải mặt giận (cảm xúc Angry).
- [ ] Khi nổi giận: một nhát trúng cả mấy slime đứng sát nhau phía trước mèo (mỗi con hiện số), slime bị đẩy lùi rõ; slime nhanh lao vào chỉ trừ máu, mèo không choáng (không có sao quay).
- [ ] Đang choáng mà thanh đầy: click không làm gì, hết choáng click lại được. Đang cầm mèo: click không làm gì. Pause/menu/túi đồ: click không làm gì, đang nổi giận thì thời gian đứng yên.
- [ ] Nhấc mèo khi đang nổi giận: kéo/lắc/ném/thả như thường (mèo to hơn một chút khi bị cầm, kiếm không bị cắt mép); thả xuống vẫn còn nổi giận nếu chưa hết giờ.
- [ ] Kết thúc trận (nút End, thắng, thua) giữa lúc nổi giận: mèo về cỡ và màu cũ. Đổi bộ lông (Cài đặt) rồi vào trận: lông đỏ theo màu bộ lông đó. Rê chuột lên thanh: dòng gợi ý mô tả (vi/en/ja). DPI 100/150/200%: thanh không lệch, mèo to không bị cắt.

## Cuộn len và Găng đấm 1.5 M08 (2026-10-28) — mọi bảng chuẩn bị
Bảng chuẩn bị có 4 vũ khí: Kiếm, Búa, Cuộn len, Găng đấm (icon riêng, thanh chỉ số). Ảnh mèo cầm cuộn len/găng ở mọi pha vung đã xem qua render UiChecks, chưa xem trong trận thật, chưa thử DPI thật.
- [ ] Bảng chuẩn bị (trận tự do, Phiêu lưu, Vô tận, Viễn chinh): 4 thẻ vũ khí không tràn khung, chọn được, lựa chọn được nhớ; vi/en/ja đúng tên và gợi ý. Sảnh/túi đồ/bảng kết quả vẽ đúng vũ khí đang dùng.
- [ ] Cuộn len: mèo đánh từ xa hơn kiếm; một nhát làm mấy slime đứng gần nhau phía trước cùng hiện số; slime thảo dược đứng sau bị kéo về sát mèo; không có sao choáng trên slime.
- [ ] Găng đấm: mèo đấm rất nhanh, số ×1…×4 trên HUD, ×4 màu vàng; nhát thứ 5 hất slime bay lên (có bóng dưới sàn), slime rơi xuống thì choáng; Vua/slime đá không bị hất. Đang hất mà Pause: slime đứng yên giữa không trung.
- [ ] Kỷ lục trận tự do/Phiêu lưu tách theo vũ khí (dòng "this gear"/"vũ khí này"); bảng kết quả ghi đúng tên vũ khí.
