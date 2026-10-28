# Bản 1.5 — Slime phản công (thiết kế đã chốt)

Ngày chốt hướng: 2026-10-26 (Asia/Saigon). Người dùng chọn "bản rất lớn", rồi chỉnh lại phần quái và kỹ năng qua trao đổi cùng ngày.
Trạng thái: **đang code** — xong M01 tới M11 (2026-10-28). Mỗi mốc bên dưới làm trong một phiên chat; trước mỗi mốc vẫn theo quy trình AGENTS.md (đọc → giải thích → chờ "ok" → làm → test → ghi nhớ).

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

1. **Mèo đang được cầm gây được sát thương, chỉ với quái trên không:** đưa mèo lại gần slime bay (S5) thì mèo tự vung vuốt; quái dưới sàn không bị mèo đang cầm đánh, thả mèo đè slime đào hang lúc nó trồi (S6), kéo mèo xuyên dây nhầy để cắt (Q4, B1).
2. **Mèo đang bay sau cú ném không tự đánh.** Cú ném chỉ tác động lên quái có tương tác ném riêng: slime băng vỡ băng (S7), slime bám tường rơi xuống (S8), dây nhầy đứt (Q4, B1). Mọi quái khác không bị cú ném ảnh hưởng.
3. **Con trỏ tương tác với slime trong khung trận:** soi (S1, S3), vuốt ve (S4), chọc bong bóng (S2), các kỹ năng dùng chuột. Click trúng mèo luôn là nhấc mèo.

Physics kéo/ném giữ nguyên; mèo vẫn không nhận sát thương khi đang cầm/bay.

**Chốt thêm 2026-10-26:**
- **Thả đè (S6)** là tương tác riêng của slime đào hang: mèo vừa được thả rơi trúng nó lúc nó trồi thì nó kẹt ngoài. Quái khác không bị mèo rơi trúng ảnh hưởng (trừ thẻ hiếm Chấn động bên dưới).
- **Đạn dính của slime bay vẫn trúng trong `landingGrace`** (vài giây không bị đánh sau khi thả mèo), cho khớp với việc trúng cả khi đang cầm. Khiên bong bóng vẫn chặn.
- **Hướng dẫn phiêu lưu mở rộng sang chương 2**, luật như chương 1 (đứng hình nhưng không phải Pause), dạy cả quái mới lẫn kỹ năng mới.
- **Chuột không quá tải:** mỗi trận chương 2/Viễn chinh tối đa 2 loại quái cần dùng chuột (S1–S4) cùng lúc; Vô tận tính sau khi chơi thật.
- **Trận tự do thành Trận tự chọn (F1)**, xem mục F1 bên dưới. Bộ mặc định giữ đúng 4 đợt hiện tại.
- **App độc cùng lúc: `maxMalware` 2 → 4** ở Viễn chinh, Vô tận và chương 2. Chương 1 giữ 2 (giữ nguyên chương 1 và rulesVersion của nó; người dùng duyệt 2026-10-26). Mỗi loại vẫn tối đa 1 cái cùng lúc; `maxInfected` vẫn 3. **Captcha không xuất hiện khi đang có slime bay.**
- **Bỏ bục tự vẽ (M3).**

**Ngoại lệ miễn sát thương khi cầm (người dùng chốt 2026-10-26):** đạn dính của **slime bay (S5)** gây sát thương cho mèo **kể cả khi đang cầm** (Khiên bong bóng vẫn chặn). Đây là quái duy nhất làm vậy; mọi quái khác vẫn không gây sát thương cho mèo đang cầm/bay. Trúng đạn khi đang cầm không làm rơi mèo khỏi tay, không choáng mèo (drag giữ nguyên).
Người dùng chốt (2026-10-26): mèo đang cầm **chỉ đánh được quái trên không** (slime bay), không đánh quái dưới sàn, để không có chuyện "cầm mèo lơ lửng đánh mãi" mà không bị gì.

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
| Q1 | Slime phun | Đứng xa phun giọt nhầy cong, có vòng báo chỗ rơi | Giọt rơi thành **vũng tăng tốc**: slime nhanh lao qua vũng thì lao xa và nhanh hơn | Nhấc mèo khỏi vòng; Đẩy; cho mèo chạy tới đánh; Quạt làm khô vũng |
| Q2 | Slime lò xo (bẫy, chốt 2026-10-26) | Nằm im trên sàn như tấm thảm, luôn có vòng vàng. Mèo vào vòng thì nó nén 0,8 s (kẽo kẹt, vòng đỏ) rồi bật: mèo còn trong vòng mất máu và choáng ~0,8 s; nghỉ ~4 s mới bật lại. Không bật trúng mèo đang cầm; không đổi physics của mèo | Không có liên kết riêng; mạnh khi đi cùng đội hình (slime khác dồn mèo vào vòng) | Nhấc mèo ra khỏi vòng trong 0,8 s; đánh 2 đòn thì nó bẹp, nằm im 3 s; Bom, Đẩy, Quạt |
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
| Bạt nhún | Lò xo + slime thường + slime nhanh lao tới ép mèo lùi vào vòng lò xo | Đánh bẹp lò xo trước, hoặc nhấc mèo khỏi vòng khi nó nén |
| Bầy nhập | 2 bầy hợp thể + thảo dược | Bom đúng lúc chụm |

### Quái solo (S)

| Mã | Quái | Cơ chế | Cách hạ |
|---|---|---|---|
| S1 | Slime bóng ma | Lúc ẩn lúc hiện; đang ẩn thì xuyên qua mèo, đánh không trúng | Rê chuột soi (hiện 2 s); Đèn laser |
| S2 | Slime bong bóng | Trong bong bóng miễn mọi đòn; bong bóng mọc lại sau 6 s | Click 3 lần chọc vỡ (rơi xuống choáng 1,5 s); Kéo cắt |
| S3 | Slime ảo ảnh | 3 bản giống nhau, 1 bản thật | Rê chuột: bản giả nhòe, bản thật có bóng; đánh trúng bản giả thì nó tan; Đèn laser |
| S4 | Slime dỗi | Đánh không mất máu, còn phồng to dần | Vuốt ve (rê chuột qua lại); hết giận thì vẫy tay bỏ đi, tính là hạ |
| S5 | Slime bay | Lơ lửng ở **vị trí bất kỳ trên màn hình** (không chỉ trên dải trận; vẽ trong cửa sổ nhỏ topmost riêng như icon app), đổi chỗ định kỳ. Bắn **đạn dính** có báo trước vào mèo: trúng thì mất máu **kể cả khi đang cầm**; mèo dưới sàn trúng đạn thì chậm một lúc. Bị choáng thì **rơi xuống đất**, nằm choáng khoảng 3 s và nhận sát thương ×1,5, rồi gồng khoảng 1 s bay lên lại, lặp lại vòng đó | Cầm mèo đưa lại gần để mèo vung vuốt; làm choáng bằng Đẩy, Kẹp gắp kéo xuống hoặc vài đòn liên tiếp khi cầm; rơi xuống thì đánh dưới sàn. Số liệu chốt ở M14 |
| S6 | Slime đào hang | Chui sàn, trồi dưới chân mèo (vòng nứt báo trước) | Nhấc mèo lúc vòng hiện, thả đè khi nó trồi (kẹt ngoài 3 s; tương tác thả đè riêng của S6); Kẹp gắp lôi lên |
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
- Viễn chinh chọn lúc bắt đầu chuyến. **Điểm nghỉ (`rest`, đã có trong code: hồi `restHeal` HP hoặc lấy 1 trong 3 thẻ) có thêm nút "Đổi kỹ năng"**, miễn phí, không thay cho lựa chọn hồi máu/thẻ. Vô tận chọn lúc bắt đầu.
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
- **K5 Mèo nổi giận** (không chiếm ô HUD): thanh nộ đầy dần khi đánh và bị đánh; đầy thì 6 s mèo to lên, đánh lan, không bị choáng. Chỉ to ra về hình vẽ; vùng nhấc mèo và vùng trúng đòn giữ nguyên. Đây là tư thế trận, không phải cảm xúc Angry. Thanh nộ nằm dưới thanh HP; đầy thì sáng lên và **người dùng click vào thanh để bật** (không tự bật). Trong 6 s: hình mèo to ~1,3 lần, lông ánh đỏ; mỗi nhát trúng mọi slime trong tầm, đẩy lùi mạnh hơn; quái không làm choáng được. Vẫn nhấc được (mèo đang cầm vẫn chỉ đánh quái trên không); Pause đóng băng thời gian.

### Cơ chế trận (M)

| Mã | Nội dung |
|---|---|
| M1 | Mèo đánh khi đang cầm: đưa mèo lại gần slime thì tự vung vuốt. Cú ném không phải đòn đánh chung, chỉ có tác dụng với quái có tương tác ném (S7, S8, dây Q4/B1) |
| M2 | Hiệu ứng trên quái: độc (mất máu dần), đóng băng (chậm), dính (đứng yên); thẻ, vũ khí, Bình xịt gây được |
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
| J1 | Nhóm thẻ thứ 4 "Mưu mẹo" (kỹ năng, bom, app, đồ rơi), cộng hưởng ở 3/6/9 thẻ; kèm 1–2 thẻ riêng cho mỗi kỹ năng mới. **Thẻ hiếm Chấn động** (ý của người dùng): mèo được thả rơi xuống sàn thì tạo chấn động quanh chỗ đáp, gây sát thương cho slime đang ở mặt đất (không trúng slime bay). Chặn lạm dụng: cần nhấc tối thiểu `rescueGraceMinLiftSeconds` và có thời gian hồi; số liệu chốt khi làm |
| A1 | App mới Chụp màn hình: chớp sáng đóng băng mọi quái 2 s |
| A2 | App độc Captcha: tung ra như app độc khác. Cửa sổ giả "Xác minh bạn không phải robot" che ~1/3 dải trận quanh mèo, lưới 3×3 hình; click hết ô có hình mèo thì đóng, click sai thì xáo lại; tự đóng sau ~15 s. Slime sau nó vẫn đánh; nhấc mèo ở chỗ không bị che vẫn được. Không ra khi đang có slime bay |
| A3 | App độc Pin yếu: kỹ năng hồi chậm tới khi click "cắm sạc" |
| A4 | App độc Con trỏ đơ: khóa 5 ô kỹ năng 5 s tới khi click "Tiếp tục" |
| L1 | Sổ tay quái trong Sảnh: hình, cách khắc chế (cả kỹ năng hợp), số đã hạ, trang đội hình phối hợp |
| L2 | Khoảng 20 thành tựu trong trận, thưởng màu lông hoặc phụ kiện |
| L3 | 2–3 bộ lông và phụ kiện mới (thành tựu, chương 2) |
| F1 | **Trận tự chọn** (thay Trận tự do, ý người dùng 2026-10-26): người dùng tự dựng trận. Chọn: quái nào và tần suất (trọng số mỗi loại, kể cả tinh anh), số đợt và số quái tối đa trên sàn, boss cuối (hoặc không boss), app nào có (từng icon) và app độc nào (từng loại, tần suất), độ khó/threat, vũ khí, bộ kỹ năng, có thẻ giữa đợt hay không. Chỉ chọn được quái/boss/app đã gặp (theo Sổ tay quái). Lưu được vài bộ (preset); bộ "Mặc định" là đúng 4 đợt của trận tự do hiện tại. Không lưu kỷ lục, không cho phần thưởng (như dữ liệu custom hiện nay). Người dùng duyệt các luật này 2026-10-26; chi tiết giao diện chốt khi làm mốc |

## Các mốc

Mỗi mốc: build + `scripts/test.ps1 -Ui` đạt, test mới cho phần thêm vào, vi/en/ja, commit + publish + check-publish theo AGENTS.md, mục test tay thêm vào cuối docs/MANUAL-TEST.md. Mốc có quái mới: trận đông ~20 quái vẫn khoảng 57 fps (`perf.ps1 -Battle crowd`).

| Mốc | Nội dung | Xong khi |
|---|---|---|
| **Nền** | | |
| M01 ✅ | Khung liên kết quái: khóa `link` trong battle.json (tether / rhythm / ride / merge / aura; **bỏ `bounce`** vì Q2 thành bẫy, người dùng duyệt 2026-10-28), vẽ dây/vòng, công tắc tắt từng liên kết, giới hạn 2 loại mỗi trận. Làm cả tác dụng gốc của 5 loại (người dùng chọn 2026-10-28); M09–M12 chỉ thêm quái thật | **Xong 2026-10-28:** BattleLinkTests (39 kiểm tra, quái giả); mô phỏng cân bằng chương 1 và Viễn chinh giống hệt trước từng dòng; chi tiết docs/BATTLE-DATA.md → Liên kết quái |
| M02 ✅ | Chọn 5 trong 10 kỹ năng: dữ liệu, bảng chuẩn bị, lưu lựa chọn, ô khóa, lọc thẻ theo kỹ năng | **Xong 2026-10-28:** BattleLoadoutTests + UiChecks; mô phỏng cân bằng giống hệt trước từng dòng. Người dùng chốt: chương 1 luôn 5 kỹ năng cũ (mục chọn ẩn, chương có `skillChoice` mới chọn); kỷ lục không tách theo bộ kỹ năng; nút "Đổi kỹ năng" ở điểm nghỉ dời sang M06. 5 kỹ năng mới hiện ô khóa tới khi có tác dụng (M05/M06) và được mở bằng `unlocksSkill` (M21). Chi tiết: docs/BATTLE-DATA.md → Bộ kỹ năng mang theo |
| M03 ✅ | Mèo đánh khi đang cầm (M1) + khung "tương tác ném" cho quái | **Xong 2026-10-28:** khóa `hover` (quái trên không) và `throwHit` (tương tác ném); mèo đang cầm tự vung vuốt chỉ với quái `hover` (đòn thường, không combo); cú ném chỉ tác động quái có `throwHit`; physics kéo/ném không đổi, test drag/throw cũ đạt; vẫn không nhận sát thương khi cầm/bay. Cách chặn "cầm đánh mãi": chỉ quái trên không (người dùng chốt 26/10). BattleHeldTests (quái giả) + ảnh UiChecks; mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Quái trên không và tương tác ném |
| M04 ✅ | Hiệu ứng trên quái (M2): độc, đóng băng, dính | **Xong 2026-10-28:** khóa `onHit` cho vũ khí và thẻ (`effect: "onHit"`, `amount` là tỉ lệ), `ApplyStatus` cho kỹ năng/app sau này. Độc tối đa 3 lớp, trừ mỗi 0,5 s, không bị đá chặn, tính là mèo hạ; đóng băng giữ mức chậm mạnh hơn; dính đứng yên nhưng vẫn đánh, hết dính miễn 1,5 s; boss nhận một nửa. Hình: độc tím + bọt, băng xanh + tinh thể, keo hổ phách dưới chân, huy hiệu có vòng đếm. BattleStatusTests (thời gian, cộng dồn, Pause, boss, dữ liệu) + ảnh UiChecks; mô phỏng cân bằng giống hệt trước từng dòng. Chưa gắn âm thanh. Chi tiết: docs/BATTLE-DATA.md → Hiệu ứng trên quái |
| **Kỹ năng và vũ khí** | | |
| M05 ✅ | Đèn laser, Quạt | **Xong 2026-10-28:** Laser click một chỗ (dụ quái trong 300 DIPs 4 s, không gồng đòn mới, 3 máu/s trên chấm, hồi 14 s); Quạt giữ chuột (thổi ra xa con trỏ, va nhau/va mép 4 máu, hơi 3 s, cạn chờ 6 s); boss nhận 50%, không bị đá chặn; bước hướng dẫn `laser`/`fan` chỉ ở màn có `unlocksSkill` đó. Người dùng chốt: trận tự do mang được ngay, chế độ khác chờ chương 2. BattleToolsTests + UiChecks (ảnh, hướng dẫn, bảng chuẩn bị); mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Đèn laser và Quạt |
| M06 ✅ | Kẹp gắp, Kéo cắt, Bình xịt nước; nút "Đổi kỹ năng" ở điểm nghỉ Viễn chinh (dời từ M02) | **Xong 2026-10-28:** Kẹp gắp nhấn giữ slime nhỏ (không đá/boss/quái lớn), vung chuột thả là ném: trúng con khác cả hai mất 8, con kia choáng 1 s, trúng mép tự mất 8, thả chậm chỉ rơi; giữ tối đa 3 s, hồi 12 s. Kéo cắt kéo một nét ≥ 40 DIPs: cắt dây nhầy, slime trên nét mất 6, hồi 8 s. Bình xịt giữ chuột: ướt + đóng băng 35% + 2 máu/s trong 70 DIPs, nước 4 s, cạn chờ 8 s. Đổi kỹ năng ở điểm nghỉ: miễn phí, không thay hồi máu/thẻ, thẻ của kỹ năng vừa bỏ rút khỏi lựa chọn; nút chỉ hiện khi đã mở hơn 5 kỹ năng. Click trúng mèo vẫn nhấc mèo (mèo là cửa sổ riêng). BattleHandToolsTests (va chạm khi ném, đá/boss không gắp, Pause, nhấc mèo, cắt dây, xịt, đổi kỹ năng ở điểm nghỉ) + UiChecks (ảnh, hướng dẫn, bảng chuẩn bị, điểm nghỉ); mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Kẹp gắp, Kéo cắt, Bình xịt nước |
| M07 ✅ | K5 Mèo nổi giận | **Xong 2026-10-28:** thanh nộ dưới thanh HP, đầy dần khi mèo đánh trúng (5%/nhát) và mất máu (1,2%/HP), mỗi trận bắt đầu rỗng; đầy thì sáng, người dùng click mới bật (không khi đang cầm, đang choáng, Pause). 6 s: hình mèo ×1,3 (khi bị cầm ×1,09) lông đỏ, mỗi nhát trúng mọi slime trong tầm, đẩy lùi mạnh hơn, không bị choáng (vẫn mất máu); không cắt cú choáng đang có; Pause/chọn thẻ/hướng dẫn đóng băng, khi cầm vẫn trôi. Cửa sổ, vùng trúng đòn, kéo/ném không đổi. Số liệu `rage*` trong battle.json. BattleRageTests + UiChecks (HUD, ảnh mọi tư thế trong cửa sổ); mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Mèo nổi giận |
| M08 ✅ | V1 Cuộn len, V2 Găng đấm | **Xong 2026-10-28:** hai vũ khí dữ liệu trong `weapons` (chọn được ở mọi bảng chuẩn bị, kỷ lục tách theo vũ khí) với khóa mới `sweep`, `pull`, `comboHits`, `launch`/`launchStun` (PetBattle.Weapons.cs) và hình `yarn`/`glove`. Cuộn len: tầm 114 (×1,5), 14 sát thương, mỗi nhát trúng mọi slime trong tầm phía trước, kéo slime đứng xa (`support`; slime phun Q1 dùng ở M09) lại sát mèo, không choáng. Găng: 14 sát thương, hồi 0,38 s, combo 5 nhát, nhát cuối hất tung quái (không boss/đá/quái lớn/tinh anh miễn Đẩy), đáp đất choáng 0,8 s. BattleNewWeaponTests + mô phỏng cân bằng riêng cho hai vũ khí (trong 30 điểm so với kiếm ở mọi màn/độ khó, ≥ 80% Normal); bảng kiếm/búa giống hệt trước. Chi tiết: docs/BATTLE-DATA.md → Vũ khí |
| **Quái liên kết** | | |
| M09 ✅ | Q1 phun, Q2 lò xo | **Xong 2026-10-28:** `spitter` (khóa `spit`, quái hỗ trợ: vòng cam ở chỗ mèo, giọt bay vòng cung trúng mèo dưới sàn không choáng, vũng tăng lao ×1,5 xa/×1,4 nhanh, Quạt làm khô; tinh anh 2 giọt) và `springer` (khóa `spring`: bẫy vòng 60, nén 0,8 s rồi bật mất máu + choáng 0,8 s, nghỉ 4 s, 2 đòn thì bẹp 3 s, trượt lại gần khi mèo xa hơn 120 — người dùng duyệt "trượt chậm", số 120 thay 250 vì mèo đứng cách mục tiêu ~83 > vòng 60; tinh anh vòng ×1,35). Có trong battle.json gốc nhưng chưa trận nào dùng (`binSpit: false`), thử bằng docs/examples/battle-m09.json (người dùng chọn). BattleSpitSpringTests + UiChecks; bảng mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Slime phun và Slime lò xo |
| M10 ✅ | Q3 nhạc trưởng, Q4 xích | **Xong 2026-10-28:** `drummer` (liên kết `rhythm`: vòng 220, 0,8 s/nhịp, nhịp 4 cả vòng cùng gồng; Đẩy/choáng/Quạt đếm lại; tinh anh `eliteBeats` 3) và `chain` (liên kết `tether`: dây 200, gánh 50%, tinh anh `eliteShare` 0,7). Cắt dây bằng Kéo cắt và bằng mèo đang cầm (quá ngưỡng kéo) hoặc đang bay xuyên dây đủ nhanh (`cutSpeed` 300): đồng minh choáng 1 s. Có trong battle.json gốc nhưng chưa trận nào dùng (`binSpit: false`), thử bằng docs/examples/battle-m10.json. Dây dài không còn võng xuống dưới sàn. BattleBandTests + UiChecks (`BandVisualChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Slime nhạc trưởng và Slime xích |
| M11 ✅ | Q5 hợp thể, Q6 bom (cưỡi), Q7 tách đôi | **Xong 2026-10-28:** `fuser` (liên kết `merge`: 3 con trong 70 DIPs 2 s thành `giant`, máu bằng tổng), `bomber` (liên kết `ride` trên `runner`, khóa mới `fuse`: cách mèo 70 thì châm ngòi 1,5 s, vòng nổ 90: mèo dưới sàn mất 22 + choáng 0,6 s, slime khác trong vòng mất 20; tự nổ thì biến mất không tính là mèo hạ; hạ trước khi châm thì xì, khi đang cháy thì nổ; hạ ngựa/cắt dây thì rơi, ngòi 0,8 s, nổ tại chỗ; Kẹp gắp ném thì nổ chỗ trúng/đáp; tinh anh ngòi 1,1 s, vòng ×1,3) và `splitter` (khóa mới `split`: bị hạ vỡ thành 2 `splitling` 60% máu, tinh anh 3; nửa không tách tiếp; `merge` với cờ mới `afterHeal`: nửa nào đã được thảo dược hồi mới tính, 2 nửa trong 120 DIPs 1,5 s thì nhập lại). Người dùng chốt: bom tự nổ không tính là mèo hạ; nửa luôn ra kể cả quá `maxAlive` (quái mới chờ chỗ, ngân sách: nửa giá 1, mẹ 2). Có trong battle.json gốc nhưng chưa trận nào dùng (`binSpit: false`), thử bằng docs/examples/battle-m11.json. Đo perf bằng `perf.ps1 -Battle crowd-new` (sàn đầy 16 quái 1.5). BattleSwarmTests + UiChecks (`SwarmVisualChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Slime hợp thể, Slime bom, Slime tách đôi |
| M12 | Q8 cờ hiệu + 6 đội hình phối hợp trong Enemy Director | Director bốc đội hình theo threat/ngân sách; banner lần đầu |
| **Quái solo** | | |
| M13 | S1 bóng ma, S2 bong bóng, S3 ảo ảnh, S4 dỗi | Chỉ đọc chuột trong khung trận; không gây cảm xúc cho mèo |
| M14 | S5 bay, S6 đào hang | Slime bay đi khắp màn hình (theo DPI, đa màn hình chỉ màn có trận, ẩn khi app khác fullscreen), đạn dính trúng cả mèo đang cầm mà không làm rơi mèo; vòng choáng → rơi → ×1,5 sát thương → bay lên chạy đúng; hạ được bằng nhấc/thả mèo và bằng Kẹp gắp |
| M15 | S7 băng, S8 bám tường | Hạ được bằng ném mèo; ngưỡng tốc độ trong battle.json |
| M16 | S9 nhím, S10 hẹn giờ, S11 trộm | Thứ bị trộm trả lại khi hạ; test |
| **Boss** | | |
| M17 | B1 Slime Nén | 3 pha chạy đủ; dây về boss cắt được bằng ném mèo và Kéo cắt |
| M18 | B2 Slime Mẹ, B3 Vua biến dị | Như trên |
| **Nội dung và chế độ chơi** | | |
| M19 | M4 đồ rơi, M5 xu | Đồ rơi tan đúng giờ; Pause đóng băng; xu chỉ trong chuyến |
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
| M29a | F1 Trận tự chọn: bảng dựng trận, preset, dựng đợt từ lựa chọn bằng cấu trúc `waves` có sẵn | Bộ Mặc định cho kết quả như trận tự do cũ (test mô phỏng); preset lưu an toàn (.tmp + .bak); quái chưa gặp bị khóa |
| M29 | Cân bằng mô phỏng toàn bản, perf, rà ja, CHANGELOG vi/en/ja, MANUAL-TEST | Toàn bộ test đạt; người dùng test tay phần mới |
| M30 | Release 1.5.0 | Theo docs/RELEASE.md |

Thứ tự có thể đổi trong một nhóm; nhóm Nền làm trước.

## Rủi ro

- **Phạm vi rất lớn** (31 mốc). Nếu cần phát hành sớm: tách 1.5 (Nền, kỹ năng, quái liên kết, B1, chương 2) và 1.6 (quái solo còn lại, Viễn chinh, Vô tận, sưu tầm).
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
