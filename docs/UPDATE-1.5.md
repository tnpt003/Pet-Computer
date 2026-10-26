# Bản 1.5 — Slime phản công (thiết kế đã chốt)

Ngày chốt hướng: 2026-10-26 (Asia/Saigon). Người dùng chọn "bản rất lớn", rồi chỉnh lại phần quái và kỹ năng qua trao đổi cùng ngày.
Trạng thái: **thiết kế, chưa code.** Mỗi mốc bên dưới làm trong một phiên chat; trước mỗi mốc vẫn theo quy trình AGENTS.md (đọc → giải thích → chờ "ok" → làm → test → ghi nhớ).

Ngoài danh sách này thì ý tưởng mới vào mục "Backlog sau 1.5" cuối file, không làm trong 1.5.

## Giữ nguyên

- Phân vai: toàn bộ combat ở C# (PetBattle*), số liệu trong battle.json. Python không tham gia combat.
- Battle Mode mặc định tắt. Ngoài trận không đổi gì (sleep, drag/throw, emotion, đi trên cửa sổ, kệ kỷ vật…).
- Physics kéo/ném của mèo giữ nguyên; nội dung mới chỉ **đọc** vị trí/vận tốc của mèo để tính va chạm.
- Chương 1, Hướng dẫn phiêu lưu chương 1 và rulesVersion của chương 1 không đổi (bộ kỹ năng mặc định vẫn là 5 kỹ năng cũ, quái mới không vào chương 1).
- App trong trận vẫn là icon tự vẽ, không đọc app thật.
- Mọi chữ mới có vi/en/ja. Icon và tranh mới theo docs/DESIGN-SYSTEM.md.

## Bổ sung ngoại lệ Battle Mode (đã duyệt 2026-10-26)

Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Battle Mode. Tóm tắt:

1. **Mèo đang được cầm gây được sát thương:** đưa mèo lại gần slime thì mèo tự vung vuốt (ví dụ slime bay S5), thả mèo đè slime đào hang lúc nó trồi (S6), kéo mèo xuyên dây nhầy để cắt (Q4, B1).
2. **Mèo đang bay sau cú ném không tự đánh.** Cú ném chỉ tác động lên quái có tương tác ném riêng: slime băng vỡ băng (S7), slime bám tường rơi xuống (S8), dây nhầy đứt (Q4, B1). Mọi quái khác không bị cú ném ảnh hưởng.
3. **Con trỏ tương tác với slime trong khung trận:** soi (S1, S3), vuốt ve (S4), chọc bong bóng (S2), các kỹ năng dùng chuột. Click trúng mèo luôn là nhấc mèo.

Physics kéo/ném giữ nguyên; mèo vẫn không nhận sát thương khi đang cầm/bay.

**Cân bằng cần chốt ở M03:** mèo đang cầm vừa miễn sát thương vừa đánh được, nên có thể "cầm mèo lơ lửng đánh mãi". M03 sẽ đề xuất cách chặn (ví dụ đòn khi cầm yếu hơn hoặc hồi lâu hơn đòn dưới sàn) để người dùng chọn; không bỏ miễn sát thương khi cầm nếu chưa được duyệt.

## Nguyên tắc thiết kế

- **Không quái nào bắt buộc một kỹ năng cụ thể.** Kỹ năng phù hợp chỉ làm dễ hơn; mỗi quái luôn hạ được bằng thao tác của mèo (đánh, nhấc, ném) hoặc kỹ năng cũ.
- **Quái liên kết:** mỗi con có việc riêng và thêm tối đa một liên kết với đồng đội. Liên kết luôn nhìn thấy (dây, vòng, nhịp) và phá được; phá được thì có thưởng (choáng, lộ sơ hở). Mỗi liên kết có công tắc tắt trong battle.json như các chiến thuật hiện có.
- **Quái solo:** không cần đồng đội, mỗi con buộc một cách chơi riêng gắn với desktop pet (con trỏ, nhấc, ném, đúng lúc).
- Mỗi trận tối đa 2 loại liên kết cùng lúc (Vô tận có thể nới sau khi chơi thật).
- Mỗi quái mới có bản tinh anh, có lần giới thiệu bằng hướng dẫn đứng hình ở chương 2 và trang trong Sổ tay quái.

## Nội dung

### Quái liên kết (Q)

| Mã | Quái | Việc riêng | Liên kết | Cách phá |
|---|---|---|---|---|
| Q1 | Slime phun | Đứng xa phun giọt nhầy cong, có vòng báo chỗ rơi | Giọt rơi thành **vũng tăng tốc**: slime nhanh lao qua vũng thì lao xa và nhanh hơn | Nhấc mèo khỏi vòng; Đẩy; cầm mèo đưa tới đánh; Quạt làm khô vũng |
| Q2 | Slime lò xo | Không đánh, nằm bẹp như bạt nhún | Đồng minh nhảy lên thì **bật qua đầu mèo** đánh từ sau, hoặc qua tường "Cập nhật" | Đẩy/Quạt nó khỏi đường; thả mèo lên nó thì mèo bật lên đánh xuống |
| Q3 | Slime nhạc trưởng | Đánh trống, vòng nhịp trên đầu | Mỗi 4 nhịp mọi quái trong vùng **gồng đánh cùng lúc** | Đẩy/choáng/Quạt để lệch nhịp; Khiên đúng nhịp 4 |
| Q4 | Slime xích | Yếu, đi sau đồng đội | **Dây nhầy** nối một đồng minh, gánh 50% sát thương đồng minh nhận | Kéo/ném mèo xuyên dây; Kéo cắt; hạ xích thì đồng minh choáng 1 s |
| Q5 | Slime hợp thể | Nhỏ, đi bầy 3 con | Chạm nhau quá 2 s thì **nhập thành Slime khổng lồ** (máu bằng tổng), có thanh đếm ngược | Đẩy/Quạt/Kẹp gắp tách bầy; Bom khi đang chụm |
| Q6 | Slime bom | Chạy tới cạnh mèo, châm ngòi rồi nổ | **Cưỡi slime nhanh** như kỵ binh, tới sát mèo mới nhảy xuống | Hạ ngựa giữa đường thì bom nổ tại chỗ; Đẩy/Kẹp gắp ném bom vào đám quái |
| Q7 | Slime tách đôi | Bị hạ thì vỡ thành 2 slime con (con không tách tiếp) | Slime thảo dược hồi cho 2 con thì **chúng nhập lại** thành con mẹ | Hạ thảo dược trước; Bom cả 2 con |
| Q8 | Slime cờ hiệu | Cắm cờ tạo vùng hào quang | Quái trong vùng +tốc độ; hạ nó thì cả đội **hoảng** 2 s | Laser dụ quái ra khỏi vùng; hạ nó trước |

**Đội hình phối hợp** (Enemy Director bốc, có banner lần đầu mỗi trận và trang Sổ tay):

| Đội hình | Thành phần | Mèo cần làm |
|---|---|---|
| Pháo đài | Đá trước, phun sau, thảo dược giữa | Nhấc mèo qua đá, hạ phun trước |
| Kỵ binh | 2 slime nhanh cõng 2 slime bom, thêm vũng của phun | Hạ ngựa giữa đường |
| Đội trống | Nhạc trưởng + 3 slime thường | Đẩy nhạc trưởng trước nhịp 4 |
| Dây chuyền | 2 slime xích nối vào 1 đá tinh anh | Cắt dây trước rồi đánh đá |
| Bạt nhún | Lò xo + slime thường + slime nhanh | Đẩy lò xo đi, hoặc mèo tự dùng lò xo |
| Bầy nhập | 2 bầy hợp thể + thảo dược | Bom đúng lúc chụm |

### Quái solo (S)

| Mã | Quái | Cơ chế | Cách hạ |
|---|---|---|---|
| S1 | Slime bóng ma | Lúc ẩn lúc hiện; đang ẩn thì xuyên qua mèo, đánh không trúng | Rê chuột soi (hiện 2 s); Đèn laser |
| S2 | Slime bong bóng | Trong bong bóng miễn mọi đòn; bong bóng mọc lại sau 6 s | Click 3 lần chọc vỡ (rơi xuống choáng 1,5 s); Kéo cắt |
| S3 | Slime ảo ảnh | 3 bản giống nhau, 1 bản thật | Rê chuột: bản giả nhòe, bản thật có bóng; đánh trúng bản giả thì nó tan; Đèn laser |
| S4 | Slime dỗi | Đánh không mất máu, còn phồng to dần | Vuốt ve (rê chuột qua lại); hết giận thì vẫy tay bỏ đi, tính là hạ |
| S5 | Slime bay | Lơ lửng trên cao, thỉnh thoảng sà xuống cắn | Nhấc mèo lên sát nó (mèo tự vung vuốt); đánh lúc sà xuống; Kẹp gắp kéo xuống |
| S6 | Slime đào hang | Chui sàn, trồi dưới chân mèo (vòng nứt báo trước) | Nhấc mèo lúc vòng hiện, thả đè khi nó trồi (kẹt ngoài 3 s); Kẹp gắp lôi lên |
| S7 | Slime băng | Giáp băng: kiếm/búa chỉ xước | Ném mèo đập vào đủ mạnh (ngưỡng tốc độ) thì vỡ; Bình xịt làm mềm (hạ ngưỡng) |
| S8 | Slime bám tường | Leo mép trái/phải khung trận, bắn gai xuống | Ném mèo lên tường; Đẩy; Kẹp gắp kéo xuống |
| S9 | Slime nhím | Phồng gai theo chu kỳ; đánh lúc phồng thì mèo bị phản sát thương | Đánh lúc xẹp; Bom; Bình xịt làm xẹp |
| S10 | Slime hẹn giờ | Đồng hồ 20 s; hết giờ gọi thêm một đợt | Hạ trước; choáng làm đồng hồ chậm |
| S11 | Slime trộm | Không đánh mèo; hút năng lượng một icon app hoặc lấy một lần hồi kỹ năng rồi chạy | Laser dụ, Đẩy chặn, Bình xịt làm chậm; hạ thì trả lại thứ bị lấy |

Trong trận cảm xúc tự động vẫn bị chặn: S1, S4 không gây cảm xúc hay ngủ cho mèo.

### Boss (B)

| Mã | Boss | Cơ chế |
|---|---|---|
| B1 | Slime Nén (.zip), trùm chương 2 | Pha 1 lăn và đè. Pha 2 "giải nén" bung một đội hình phối hợp (Pháo đài, rồi Kỵ binh); mỗi file-slime nối dây về boss, còn dây thì boss không mất máu, phải cắt dây (ném mèo xuyên dây, Kéo cắt). Pha 3 tự "nén" thành khiên, phải phá các mảnh file quanh nó |
| B2 | Slime Mẹ (boss phụ chương 2) | Đẻ bầy hợp thể; con nào nhập xong quay về "cho bú" hồi máu cho mẹ |
| B3 | Vua Slime biến dị (Vô tận, Viễn chinh) | Vương miện phong tinh anh và còn nối liên kết giữa 2 quân (dây xích hoặc nhịp trống); thêm mưa nhầy, gọi slime phun |

### Vũ khí (V)

| Mã | Vũ khí | Đặc điểm |
|---|---|---|
| V1 | Cuộn len (roi) | Tầm ~1,5 lần, quét nhiều quái, sát thương thấp, không choáng; kéo slime phun lại gần |
| V2 | Găng đấm | Rất nhanh, combo 5 nhát thay vì 3; nhát cuối hất tung quái |

### Kỹ năng (K): 10 kỹ năng, mang 5 lên HUD

- HUD giữ 5 ô như hiện tại. Bảng chuẩn bị có ô chọn 5 trong 10, nhớ lựa chọn. **Mặc định là 5 kỹ năng cũ.**
- Viễn chinh chọn lúc bắt đầu chuyến, đổi được ở trạm nghỉ; Vô tận chọn lúc bắt đầu.
- Màn chương 2 có dòng "Gợi ý mang: …". 5 kỹ năng mới mở khóa dần qua chương 2; chưa mở thì ô hiện khóa.
- Thẻ gắn với kỹ năng (Đẩy mạnh, Bom to, Khiên dày…) chỉ ra khi kỹ năng đó có trong bộ mang theo.

| Nhóm | Kỹ năng | Cách dùng | Tác dụng | Sát thương (chốt khi làm) |
|---|---|---|---|---|
| Cũ | Đẩy, Cá hồi máu, Cổ vũ, Bom len, Khiên bong bóng | Như hiện tại | Giữ nguyên số liệu | Giữ nguyên (Đẩy không thêm sát thương) |
| Mới | Đèn laser | Chọn, click một điểm | Slime chạy theo chấm 4 s; soi lộ S1, S3 | ~3/s cho con trên chấm |
| Mới | Kẹp gắp | Chọn, kéo một slime nhỏ | Ném slime vào slime khác; kéo S5/S8 xuống, lôi S6 lên. Đá và boss không gắp được | ~8 mỗi con khi va |
| Mới | Kéo cắt | Chọn, vuốt ngang | Cắt dây nhầy (Q4, B1), chọc vỡ bong bóng (S2) | ~6 mỗi vết cắt |
| Mới | Quạt | Chọn, giữ chuột | Thổi slime theo hướng; khô vũng; tách bầy; lệch nhịp | ~4 khi va nhau/va tường |
| Mới | Bình xịt nước | Chọn, giữ chuột | Slime chậm và co lại; băng mềm; gai nhím xẹp | ~2/s khi đang xịt |

- Sát thương kỹ năng mới ≈ 1/5 sức đánh của mèo; được kết liễu và tính như mèo hạ (cả Hút năng lượng); boss nhận 50%.
- Quạt và Bình xịt dùng kiểu giữ chuột: vòng hồi trên ô là thanh "hơi", cạn thì chờ.
- Icon 5 kỹ năng mới vẽ đúng kiểu ô hiện tại (viền vàng, nền nâu).
- **K5 Mèo nổi giận** (không chiếm ô HUD): thanh nộ đầy dần khi đánh và bị đánh; đầy thì 6 s mèo to lên, đánh lan, không bị choáng.

### Cơ chế trận (M)

| Mã | Nội dung |
|---|---|
| M1 | Mèo đánh khi đang cầm: đưa mèo lại gần slime thì tự vung vuốt. Cú ném không phải đòn đánh chung, chỉ có tác dụng với quái có tương tác ném (S7, S8, dây Q4/B1) |
| M2 | Hiệu ứng trên quái: độc (mất máu dần), đóng băng (chậm), dính (đứng yên); thẻ, vũ khí, Bình xịt gây được |
| M3 | Bục tự vẽ trong trận (không phải cửa sổ thật): mèo trên bục đánh xuống mạnh hơn, slime phun bắn không tới |
| M4 | Đồ rơi: slime đôi khi rơi cá, xu, bình; click để nhặt, để lâu thì tan |
| M5 | Xu trong chuyến (từ M4), tiêu ở cửa hàng Viễn chinh; không thành tiền tệ vĩnh viễn |

### Kiểu màn, chương 2, chế độ chơi

| Mã | Nội dung |
|---|---|
| T1 | Bảo vệ giỏ cá: slime tha cá đi; mất hết cá thì thua dù mèo còn máu |
| C1 | Chương 2 "Bãi rác Tải về": 5 màn, mỗi màn giới thiệu cái mới và mở khóa kỹ năng mới; trùm B1; dạy bằng hướng dẫn đứng hình. Mở sau khi thắng chương 1 |
| C2 | Tranh minh họa, bản đồ chương, phông nền trận riêng (đống file, thư mục tự vẽ) |
| C3 | Phần thưởng chương 2: một phụ kiện, một kỷ vật (thú bông Slime Nén, physics như kệ kỷ vật) |
| X1 | Viễn chinh tuyến 2: vùng Đầm nhầy, Kho cá, Mê cung thư mục; trùm Slime Nén |
| X2 | Sự kiện mới: Lò rèn (nâng vũ khí trong chuyến), Lời nguyền (thẻ hiếm đổi quái mạnh hơn), Mèo bị lạc |
| X3 | Cửa hàng mèo: tiêu xu mua thẻ, hồi máu, di vật |
| X4 | 6–8 di vật mới, có đánh đổi |
| E1 | Vô tận: quái mới vào dần từ khoảng đợt 15; trùm xoay vòng Vua → Slime Nén → Vua biến dị |
| E2 | Vô tận "Đột biến": mỗi 10 đợt chọn 1 trong 3 luật lạ có thưởng |
| J1 | Nhóm thẻ thứ 4 "Mưu mẹo" (kỹ năng, bom, app, đồ rơi), cộng hưởng ở 3/6/9 thẻ; kèm 1–2 thẻ riêng cho mỗi kỹ năng mới |
| A1 | App mới Chụp màn hình: chớp sáng đóng băng mọi quái 2 s |
| A2 | App độc Captcha: che khung trận tới khi click đúng ô có hình mèo |
| A3 | App độc Pin yếu: kỹ năng hồi chậm tới khi click "cắm sạc" |
| A4 | App độc Con trỏ đơ: khóa 5 ô kỹ năng 5 s tới khi click "Tiếp tục" |
| L1 | Sổ tay quái trong Sảnh: hình, cách khắc chế (cả kỹ năng hợp), số đã hạ, trang đội hình phối hợp |
| L2 | Khoảng 20 thành tựu trong trận, thưởng màu lông hoặc phụ kiện |
| L3 | 2–3 bộ lông và phụ kiện mới (thành tựu, chương 2) |

## Các mốc

Mỗi mốc: build + `scripts/test.ps1 -Ui` đạt, test mới cho phần thêm vào, vi/en/ja, commit + publish + check-publish theo AGENTS.md, mục test tay thêm vào cuối docs/MANUAL-TEST.md. Mốc có quái mới: trận đông ~20 quái vẫn khoảng 57 fps (`perf.ps1 -Battle crowd`).

| Mốc | Nội dung | Xong khi |
|---|---|---|
| **Nền** | | |
| M01 | Khung liên kết quái: khóa `link` trong battle.json (tether / rhythm / ride / merge / bounce / aura), vẽ dây/vòng, công tắc tắt từng liên kết, giới hạn 2 loại mỗi trận | Có test cho mỗi loại liên kết với quái giả; battle.json hiện tại không đổi kết quả mô phỏng chương 1 |
| M02 | Chọn 5 trong 10 kỹ năng: dữ liệu, bảng chuẩn bị, lưu lựa chọn, ô khóa, lọc thẻ theo kỹ năng | Mặc định 5 kỹ năng cũ, chương 1 và kỷ lục cũ không đổi; đổi bộ thì HUD đổi đúng ô |
| M03 | Mèo đánh khi đang cầm (M1) + khung "tương tác ném" cho quái | Cầm mèo gần slime thì mèo vung vuốt; cú ném không gây sát thương cho quái thường; quái có cờ tương tác ném nhận va chạm (test bằng quái giả); physics kéo/ném không đổi (test drag/throw cũ đạt); vẫn không nhận sát thương khi cầm/bay; người dùng chọn cách chặn "cầm đánh mãi" |
| M04 | Hiệu ứng trên quái (M2): độc, đóng băng, dính | Hiển thị rõ trên quái; có test thời gian/cộng dồn; Pause đóng băng hiệu ứng |
| **Kỹ năng và vũ khí** | | |
| M05 | Đèn laser, Quạt | Hai kỹ năng chạy, có sát thương nhẹ và thanh hơi (Quạt); hướng dẫn đứng hình chỉ cho kỹ năng đang dạy |
| M06 | Kẹp gắp, Kéo cắt, Bình xịt nước | Click trúng mèo luôn nhấc mèo; đá/boss không gắp được; có test va chạm khi ném slime |
| M07 | K5 Mèo nổi giận | Thanh nộ trên HUD, 6 s hiệu ứng; không phá choáng/Pause/drag |
| M08 | V1 Cuộn len, V2 Găng đấm | Chọn được ở bảng chuẩn bị; số liệu trong `weapons`; mô phỏng cân bằng không lệch quá kiếm/búa |
| **Quái liên kết** | | |
| M09 | Q1 phun, Q2 lò xo | Mỗi con có hình vector, tinh anh, banner, test hành vi |
| M10 | Q3 nhạc trưởng, Q4 xích | Như trên; cắt dây bằng ném mèo và Kéo cắt đều được |
| M11 | Q5 hợp thể, Q6 bom (cưỡi), Q7 tách đôi | Như trên; số quái trên sàn vẫn theo `maxAlive`/ngân sách; fps trận đông đạt |
| M12 | Q8 cờ hiệu + 6 đội hình phối hợp trong Enemy Director | Director bốc đội hình theo threat/ngân sách; banner lần đầu |
| **Quái solo** | | |
| M13 | S1 bóng ma, S2 bong bóng, S3 ảo ảnh, S4 dỗi | Chỉ đọc chuột trong khung trận; không gây cảm xúc cho mèo |
| M14 | S5 bay, S6 đào hang | Hạ được bằng nhấc/thả mèo và bằng Kẹp gắp |
| M15 | S7 băng, S8 bám tường | Hạ được bằng ném mèo; ngưỡng tốc độ trong battle.json |
| M16 | S9 nhím, S10 hẹn giờ, S11 trộm | Thứ bị trộm trả lại khi hạ; test |
| **Boss** | | |
| M17 | B1 Slime Nén | 3 pha chạy đủ; dây về boss cắt được bằng ném mèo và Kéo cắt |
| M18 | B2 Slime Mẹ, B3 Vua biến dị | Như trên |
| **Nội dung và chế độ chơi** | | |
| M19 | M3 bục tự vẽ, M4 đồ rơi, M5 xu | Bục không phải cửa sổ thật; đồ rơi tan đúng giờ; Pause đóng băng |
| M20 | T1 Bảo vệ giỏ cá | Thua khi mất hết cá; kết quả/kỷ lục đúng |
| M21 | C1 Chương 2: 5 màn + hướng dẫn đứng hình | Chơi được từ màn 1 tới trùm; mở khóa kỹ năng đúng màn; mô phỏng cân bằng |
| M22 | C2 tranh/bản đồ/phông, C3 phần thưởng | Ảnh kiểm tra UI đạt; kỷ vật mới có physics như kệ |
| M23 | X1–X4 Viễn chinh tuyến 2, sự kiện, cửa hàng, di vật | Chuyến đang đi của tuyến 1 không bị ảnh hưởng |
| M24 | E1, E2 Vô tận | rulesVersion Vô tận tăng, kỷ lục cũ tách riêng |
| M25 | J1 nhóm Mưu mẹo + thẻ kỹ năng mới | Cộng hưởng 3/6/9 chạy; thẻ chỉ ra khi có kỹ năng |
| M26 | A1–A4 app | Theo ngoại lệ App trong trận; tắt được bằng ô "App trong trận" |
| M27 | L1 Sổ tay quái | Mở trang khi gặp quái lần đầu; có cả đội hình |
| M28 | L2 thành tựu, L3 lông/phụ kiện | Lưu tiến trình an toàn (.tmp + .bak) |
| **Chốt** | | |
| M29 | Cân bằng mô phỏng toàn bản, perf, rà ja, CHANGELOG vi/en/ja, MANUAL-TEST | Toàn bộ test đạt; người dùng test tay phần mới |
| M30 | Release 1.5.0 | Theo docs/RELEASE.md |

Thứ tự có thể đổi trong một nhóm; nhóm Nền làm trước.

## Rủi ro

- **Phạm vi rất lớn** (30 mốc). Nếu cần phát hành sớm: tách 1.5 (Nền, kỹ năng, quái liên kết, B1, chương 2) và 1.6 (quái solo còn lại, Viễn chinh, Vô tận, sưu tầm).
- **Hiệu năng:** hợp thể, tách đôi, kỵ binh làm tăng số quái; nhiều dây/vòng cần vẽ.
- **Input:** kỹ năng dùng chuột (kẹp, kéo, quạt, xịt) và quái dùng chuột (S1–S4) dễ xung đột với nhấc mèo và icon app; luật ưu tiên: mèo → kỹ năng đang chọn → quái → icon.
- **Cân bằng:** nhiều tổ hợp kỹ năng × quái; cần mô phỏng như AdventureBalanceTests và chơi thật.
- **Kỷ lục:** màn/chế độ có quái mới tăng rulesVersion, kỷ lục cũ tách riêng.

## Backlog sau 1.5

- **Bút vẽ tường** (kỹ năng 11): chọn rồi vuốt chuột một nét trên sàn, thành tường tạm 4 s mà slime không đi qua (giống tường "Cập nhật" nhưng bạn tự đặt chỗ). Dùng chặn Kỵ binh/slime nhanh, che giỏ cá. Để sau vì: trùng vai trò giữ quái xa với Đẩy/Quạt, dễ làm trận quá dễ (chặn hết quái đánh gần), và phải viết lại luật tường đang dùng cho app độc "Cập nhật".
- **Kính lúp** (kỹ năng 12): click một slime, 5 s mọi đòn vào nó là chí mạng. Dùng cho boss, Slime khổng lồ. Để sau vì: chỉ tăng sát thương thuần như Cổ vũ, không tạo cách chơi mới; 10 kỹ năng đã đủ để chọn.
- Thêm hai cái này thì bảng chọn thành 5 trong 12, không cần đổi HUD.

- Vũ khí Cần câu cá (V3), thẻ riêng theo vũ khí (V4).
- Kỹ năng Cào lướt (K4); Cục tẩy bóc tinh anh.
- Kiểu màn Đuổi trộm, Cầm cự tính giờ, Dọn nhiễm, Liên hoàn trùm (T2–T5).
- Mốc thưởng Vô tận đợt 60/75/100 (E3); khoảng 12 thẻ mới chia đều 4 nhóm (J2).
- Mèo nói tiếng người (docs/NEXT-STEPS.md), cần đổi quy tắc AGENTS.md.
