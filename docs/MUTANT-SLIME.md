# Slime biến dị — thiết kế được duyệt 06/11/2026

Trạng thái: **thiết kế, chưa triển khai**. Code hiện tại vẫn là `mutant_king` với luật Vua và boss tactics cũ. Tài liệu này ghi quyết định từ hội thoại, không phải báo cáo tính năng đã chạy. Đối chiếu luật hiện tại trong BOSS-TACTICS.md. Không thay đổi boss Vua thường.

## Nhận diện

Đổi tên hiển thị Vua Slime biến dị thành Slime biến dị; vẫn là trùm. Giữ xanh lá, bỏ vương miện. Ảnh người dùng là mẫu biểu cảm mặc định: hai mắt xoắn, miệng ngoằn ngoèo lệch. Thân thạch bất đối xứng, bướu và bọt chuyển động trong thân; tăng mức biến dạng qua ba phase. Art vector/procedural, không dùng bitmap concept trong game. Biểu cảm giãn/méo khi ra đòn, kiệt sức khi lộ lõi; không tự gây Dizzy.

## Luật chung

- Combat thuộc C#; Python không tham gia. Giữ Pause/menu/hướng dẫn đứng hình, Khiên và Nộ theo luật chung trừ ngoại lệ cụ thể.
- Đòn boss và vùng nhầy gây damage khi chạm mèo dưới đất, đang bế, bay hoặc bounce. Dùng hình học chung giữa cảnh báo/render/collision, quét chuyển động để tránh xuyên khi ném nhanh.
- Đòn thường không tự tuột tay/đổi vận tốc. Túi phun và đòn all-in cuối có ép rơi riêng bên dưới.
- Slime Nén và Slime biến dị miễn debuff khi vào mech, trong mech và chuyển phase; giải hiệu ứng bất lợi cũ khi bắt đầu. Miễn debuff không đồng nghĩa miễn damage khi lõi đang mở.
- Slime Nén phase 2–3 còn miễn choáng và đẩy ngoài mech. Trạng thái lộ yếu/kiệt sức do chính mech tạo vẫn chạy.
- Slime biến dị miễn damage trong chuyển phase và animation biến đổi sân. Damage nhiều hit/độc không được bỏ qua ngưỡng hoặc tiêu thụ nhiều lượt lõi cùng frame.
- Không có tương tác cắt dây ở boss này. Ống hút hồi máu chỉ để vẽ.
- Mọi timer dùng simulation time, dọn sạch khi boss chết/trận kết thúc; layout chịu được resize/DPI và không che đường né.

## Sân biến dị tồn tại đến hết trận boss

Chuyển sang phase 2 tự biến đổi nền: tổng 25% chiều ngang sân ban đầu, mỗi mép 12,5%, lan từ mép vào giữa. Sang phase 3: tổng 50%, mỗi mép 25%, thêm bốn túi nhầy không trung. Không lưu vùng nhầy qua boss/đợt/trận sau.

Các mảng nền và túi có nhịp riêng, không đồng loạt tắt/bật. Ba trạng thái nghỉ, chuẩn bị, hoạt động phải phân biệt rõ. Nền nghỉ rút nhầy cho mèo đi qua đánh boss; nền hoạt động gây damage và Nhiễu nhịp. Giữ đường an toàn thực sự, xét cả túi trời và đòn boss.

Animation chuyển phase 2: boss cắm tay bơm nhầy, nhầy lan từ thân xuống hai mép. Phase 3: thân vặn xoắn/phình, mở rộng mạch nền và thổi túi lên trời. Không chồng đòn mới trong animation; boss miễn damage. Boss chết: mạch nhầy mất màu, vỡ bọt rồi tan.

Bốn túi trôi chậm, nhấp nhô theo đường cong giữa các vị trí ngẫu nhiên theo seed, không bám mèo, không overlap cả trên đường đi. Nghỉ: túi rũ và giọt treo; chuẩn bị: phồng, mở lỗ phun và hiện vòng bán kính; hoạt động: co bóp phun quanh thân. Trúng phun liên tục đủ lâu thì mèo đang bế/bounce bị ép rơi, sau đó dính Nhiễu nhịp.

Thông số khởi điểm đề xuất, cần xác nhận bằng test/chơi thật: túi báo 1,2 s, bán kính 85 DIP, phun 2 s, hồi riêng 6–9 s, tối đa hai túi phun cùng lúc, tích nhầy 0,6 s trước ép rơi; nền hoạt động 8 s/nghỉ 5 s với offset riêng.

## Nhiễu nhịp và gỡ bằng chuột

Mèo có bọt nhầy báo nhịp; tạm khựng tự đi/tự đánh, không đổi physics/vùng click, không khựng vận tốc đang bế/bay. Khiên chặn áp, Nộ giải/miễn, không stack hoặc refresh vô hạn theo tick.

Người dùng rê qua lại trên thân mèo khoảng **2 giây** để gỡ. Phải có chuyển động đổi hướng thực sự, đứng yên/đi một chiều không tính. Hướng triển khai: tích thời gian chuyển động hợp lệ, cần tối thiểu bốn lần đổi hướng, lọc rung chuột rất nhỏ; thanh tiến độ/bọt bong dần báo gỡ. Không yêu cầu đếm lần chính xác, không nhận động tác do cửa sổ mèo tự chạy qua con trỏ. Pause không tính; không kích hoạt drag, cảm xúc hay Dizzy. Gỡ xong cần khoảng bảo vệ tránh tái áp ngay khi chưa ra khỏi vùng.

Thời lượng gốc 4 s, nhịp khựng 1,3 s/0,25 s là thông số khởi điểm; kiểm tra 2 s gỡ vẫn có ý nghĩa và thao tác không quá nhạy DPI.

## Đòn thường: ba đòn mỗi phase

Túi đòn theo seed, hết ba đòn rồi trộn, tránh lặp ngay. Mỗi đòn có báo, khóa mục tiêu, thực hiện và hồi phục; không kế thừa gọi quân/buff/vương miện của Vua.

| Phase | Đòn | Biểu hiện/cách né |
|---|---|---|
| 1 | Bước chân đi trước | Chân trượt rời rồi thân kéo theo/bật về; báo cả lượt đi và gom |
| 1 | Mắt thoát khỏi mặt | Hai mắt thành bọt bay hai cung lệch nhịp qua vị trí khóa rồi nhập về |
| 1 | Nấc đổi đầu | Miệng trượt mặt/hông/đỉnh, mỗi vị trí báo hướng của phát nhầy kế |
| 2 | Lột xác ngược | Vỏ ở lại phồng nổ, thân thật trượt đi rồi mở đòn tay có báo riêng |
| 2 | Tay tự đi săn | Tay rời thân ở tầng thấp/cao, khép vào điểm khóa rồi chảy về |
| 2 | Nuốt đất, nhả trời | Hút mảng nền cho khô tạm, bụng phồng nhả bọt theo cung |
| 3 | Cơ thể lộn trái | Thân cuộn qua chính mình, bướu bật thành nan quạt có khe né |
| 3 | Bốn phổi, một tiếng nấc | Báo một/hai túi trời phun trả lời, boss phun thấp nhịp cuối |
| 3 | Bóng nhầy đi sai hướng | Bóng trườn ra nổi thành thân méo rồi đổ sóng; thân thật vẫn rõ |

## Mech 1 — sáu bướu, ba vòng

Ngưỡng dự kiến 66%. Tổng 60 s cho ba vòng. Mỗi vòng spawn ngẫu nhiên ba bướu sàn và ba bướu tường ở hai mép, không overlap boss/nhau. Thứ tự 1–6 trộn hai loại.

- Sàn: đệm dẹt lõm, icon mèo/mũi tên xuống; chỉ tính mèo thả đáp từ trên, không phải mục tiêu quái thường.
- Tường: mọc ngang xoay 90 độ, lõi nhô, icon mèo + kiếm; bế mèo tới để tự đánh.
- Chỉ bướu đúng số mở lõi/phát sáng; sai thứ tự không tiến triển. Xong thì vỏ xẹp, bọt thoát và dấu hoàn thành riêng cho từng loại.
- Mèo không tự đi khi chờ thao tác. Boss cản bằng Nấc bắn lệch: khóa vị trí rồi phun một viên có báo, không đánh ngay khi bướu vừa sáng.
- Đủ sáu mở lõi; một nhát hợp lệ kết thúc vòng. Ba vòng xong sang phase 2. Hết giờ báo đòn toàn sân 50% max HP mèo rồi sang phase 2 với boss 100% HP.
- Cú đáp địa chấn: ưu tiên collision bướu, không để sóng giải nhiều bướu; cú giải bướu không tiêu hao/kích thẻ, không bị StickLanding triệt cú nảy. Đáp sàn khác giữ luật cũ. Cần test thực tế cầu nối PetWindow.Battle.cs.

## Mech 2 — sáu lượt ping pong

Ngưỡng dự kiến 33%, không quay lại phase 2 sau mech.

Boss báo điểm đến xa thân rồi phun lõi theo cung hợp khoảng cách; thời gian bay có tối thiểu phản ứng, không spawn lõi ngay tại boss. Ném mèo chạm lõi thì lõi bật về boss/gây choáng do mech. Lõi rơi nền tạo bướu kín riêng phase 2, không thể tương tác bằng bất kỳ cách nào. Trúng hay trượt đều tính một trong sáu lượt.

Sau sáu lượt:
- Không bướu: mở lõi cho mèo đánh một lần rồi sang phase 3.
- Có bướu: boss nối ống/hút nhầy hồi máu, không mở lõi, rồi **sang phase 3 luôn**, không lặp phase 2. Hồi theo 1/6 max HP mỗi bướu, cap max HP.
- 1–2 bướu không có đòn phạt toàn map; 3–5 bướu phạt 20% max HP mèo; 6 bướu phạt 40%.
- 20%: Nấc quá tải, ba bướu nổi trong bụng, màng nhầy lan toàn màn rồi vỡ. 40%: Vỡ kén biến dị, sáu bướu phồng, thân ép rồi bung làn nhầy dày toàn màn. Có báo trước; overlay xuyên chuột. Đây là đòn phạt không có khe né.

Hồi máu không hạ phase, không mở lại mech trước. Dọn bướu/ống sau hút.

## Mech 3 — sáu nhát lõi ở 1 HP

Chỉ bắt đầu ở 1 HP, giữ HP để độc/execute/combo không bỏ qua. Gào 5 s: miệng kéo dài, mắt xoắn siết, hút nhầy rồi phụt vòng gợn. Sau đó mở lõi có sáu vết nứt đếm nhát hợp lệ.

Mỗi nhát lập tức khóa damage/debuff, boss đẩy mèo ra, đổi vị trí ngẫu nhiên hợp lệ rồi chọn một trong ba all-in, tránh lặp ngay. Kết thúc đòn mở lõi. Nhát sáu hạ boss, không phản công thêm. Mèo trúng all-in bị ép rơi không nảy và choáng 1 s sau đáp; cần khoảng bảo vệ để nhấc lại.

### Con ngươi truy sát

Sáu nhịp, mỗi nhịp chỉ dò vị trí mèo một lần rồi khóa; random dải dọc toàn chiều cao hoặc dải ngang toàn chiều rộng. Dải đánh rồi giữ trên màn hình, tích tới sáu; không tracking sau khóa. Phải xét hợp vùng các dải và hazard để còn đường né, không để random phủ kín.

Mỗi dải chạm gây **5% max HP mèo**. Cooldown riêng từng dải và cooldown chung giữa các dải để giao điểm không tính nhiều hit. Đề xuất khởi điểm: chung 2,5 s từ lúc hit, riêng 3 s; tối thiểu 2,5 s mỗi nhịp báo. Ngoài ra bảo vệ đến sau khi đáp/hết stun và có ít nhất 1 s cứu mèo: không chỉ dùng timer từ lúc hit vì có thể rơi từ rất cao. Thời gian bảo vệ chặn cả damage và ép rơi lặp của các dải, có visual rõ. Mèo vẫn tiếp tục trúng nếu cố ở lì sau khi bảo vệ hết.

Sau nhịp sáu, cả sáu dải rung mềm, rạn thành chuỗi bọt, co về các giọt rồi tan. Ngắt collision ngay khi vào đoạn tan; không còn hitbox vô hình.

### Thác nhầy đảo chiều

Nhánh boss vươn lên mép trên, đổ thác toàn màn trừ hành lang báo trước; sau khoảng nghỉ đổi hành lang rồi trào từ dưới lên. Impact bằng thác dày, bọt va sàn và thân boss co bóp. Layout phối hợp hazard đang tồn tại để có lối né thực sự.

### Hơi thở quá sinh

Miệng mọc thành vòi, phun theo đường thẳng tăng tầm 1/3 rồi 2/3 chiều ngang; **dừng một nhịp 2 s ở mốc 2/3**, sau đó phun nốt 1/3 còn lại tới hết sân. Báo biên từng nấc, chừa dải trên/dưới để bế mèo né. Thân phồng và vòi giật theo áp lực, tia dày/mạnh lên qua từng nấc.

## Cần kiểm tra trước khi báo triển khai xong

- Unit collision mèo bế/bay/bounce, sweep nhanh; Khiên/Nộ, Pause, ảnh hưởng thẻ/độc/execute; mỗi cửa lõi chỉ nhận một nhát.
- Sáu bướu không overlap boss/nhau, thứ tự, hết giờ, Cú đáp địa chấn và StickLanding.
- Ping pong 0–6 bướu, hồi cap, chuyển phase một chiều dù đầy HP.
- Sáu dải giao nhau, rơi cao, stun và nhấc lại, cooldown riêng/chung, hết đòn hết collision; vùng an toàn màn hẹp/resize.
- Miễn debuff/chill/stick/poison, choáng/đẩy Slime Nén phase 2–3 từ tất cả skill/app/thẻ.
- Chuột gỡ debuff không ăn rung, FPS/DPI/drag không ảnh hưởng, không đếm khi Pause hoặc chỉ mèo di chuyển.
- Render phase/mech/debuff và animation nền/trời, perf; cân bằng trước/sau, rulesVersion, vi/en/ja và sổ tay.
- Build/test đầy đủ, commit trước publish, smoke và mở lại pet nếu trước đó đang chạy; không ghi đè preferences người dùng.
