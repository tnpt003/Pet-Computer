# Bản 1.5 — Slime phản công (thiết kế đã chốt)

Ngày chốt hướng: 2026-10-26 (Asia/Saigon). Người dùng chọn "bản rất lớn", rồi chỉnh lại phần quái và kỹ năng qua trao đổi cùng ngày.
Trạng thái: **đang code** — xong M01 tới M22, M23a, M23b, M24 (2026-10-29), M25 và M26 (2026-10-30). Mỗi mốc bên dưới làm trong một phiên chat; trước mỗi mốc vẫn theo quy trình AGENTS.md (đọc → giải thích → chờ "ok" → làm → test → ghi nhớ).

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
| M11 ✅ | Q5 hợp thể, Q6 bom (cưỡi), Q7 tách đôi | **Xong 2026-10-28:** `fuser` (liên kết `merge`: 3 con trong 70 DIPs 2 s thành `giant`, máu bằng tổng), `bomber` (liên kết `ride` trên `runner`, khóa mới `fuse`: cách mèo 70 thì châm ngòi 1,5 s, vòng nổ 90: mèo dưới sàn mất 22 + choáng 0,6 s, slime khác trong vòng mất 20; tự nổ thì biến mất không tính là mèo hạ; hạ trước khi châm thì xì, khi đang cháy thì nổ; hạ ngựa/cắt dây thì rơi, ngòi 0,8 s, nổ tại chỗ; Kẹp gắp ném thì nổ chỗ trúng/đáp; tinh anh ngòi 1,1 s, vòng ×1,3) và `splitter` (khóa mới `split`: bị hạ vỡ thành 2 `splitling` 60% máu, tinh anh 3; nửa không tách tiếp; `merge` với cờ mới `afterHeal`: nửa nào đã được thảo dược hồi mới tính, 2 nửa trong 120 DIPs 1,5 s thì nhập lại). Người dùng chốt: bom tự nổ không tính là mèo hạ; nửa luôn ra kể cả quá `maxAlive` (quái mới chờ chỗ, ngân sách: nửa giá 1, mẹ 2). Có trong battle.json gốc nhưng chưa trận nào dùng (`binSpit: false`), thử bằng docs/examples/battle-m11.json. Đo perf bằng `perf.ps1 -Battle crowd-new` (sàn đầy 16 quái 1.5): 2026-10-29 fps trung vị 56, thấp nhất 39,6, tối đa 19 quái; cùng máy, cùng bản `crowd` quái thường 57,4/47,4. BattleSwarmTests + UiChecks (`SwarmVisualChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Slime hợp thể, Slime bom, Slime tách đôi |
| M12 ✅ | Q8 cờ hiệu + 6 đội hình phối hợp trong Enemy Director | **Xong 2026-10-29:** `flagbearer` (quái hỗ trợ, liên kết `aura`: vòng 180, đồng minh ×1,3, tinh anh `eliteSpeed` 1,45; hạ nó thì cả đội hoảng 2 s). Danh sách `formations` (6 đội hình: Pháo đài, Kỵ binh, Đội trống, Dây chuyền, Bạt nhún, Bầy nhập; mỗi đội hình 1–4 phần, phần `side: "b"` ra ở mép sau lưng mèo) và nhóm đợt `{ "formation": { id: trọng số } }`: Director bốc theo trọng số trong số đội hình có `minThreat` ≤ threat và vừa ngân sách (không vừa: rẻ nhất), chỉ dùng luồng spawn khi đợt có nhóm đội hình; banner "Đội hình …! <gợi ý>" lần đầu mỗi trận. Người dùng chọn: mỗi đồng minh tối đa 2 dây xích (dây thứ hai gánh phần của nó trên phần còn lại), cờ đi theo con cầm cờ. Có trong battle.json gốc nhưng chưa trận nào dùng, thử bằng docs/examples/battle-m12.json. BattleFormationTests + UiChecks (`FlagVisualChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Slime cờ hiệu và đội hình phối hợp |
| **Quái solo** | | |
| M13 ✅ | S1 bóng ma, S2 bong bóng, S3 ảo ảnh, S4 dỗi | **Xong 2026-10-29:** cửa sổ đưa vị trí chuột vào trận chỉ khi con trỏ trong dải trận (không khi đang cầm mèo, Pause, hướng dẫn đứng hình); không quái nào đổi cảm xúc mèo. `phantom` (khóa `ghost`: hiện 2,5 s / ẩn 2,5 s; ẩn thì không bị đánh, không đánh, trôi xuyên mèo; rê chuột hoặc laser làm hiện 2 s), `bubble` (bong bóng chặn mọi đòn, click 3 lần hoặc Kéo cắt làm vỡ: rơi choáng 1,5 s, mọc lại sau 6 s), `mirage` (2 bản giả, tinh anh 3; bản giả đánh không đau, trúng đòn thì tan, không tính hạ; rê chuột: giả nhòe, thật có bóng), `sulky` (khóa `sulk`: đòn không trừ máu mà làm phồng ×1,1 tới ×1,6, đòn của nó mạnh theo; rê chuột qua lại 1800 DIPs thì hết dỗi, vẫy tay bỏ đi, tính là hạ). Mỗi trận tối đa 2 loại quái dùng chuột (kiểm tra lúc nạp). Có trong battle.json gốc nhưng chưa trận nào dùng, thử bằng docs/examples/battle-m13.json và battle-m13b.json. BattleTrickTests + UiChecks (`TrickVisualChecks`, đọc chuột trong `BattleChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng. Chi tiết: docs/BATTLE-DATA.md → Quái dùng chuột |
| M14 ✅ | S5 bay, S6 đào hang | **Xong 2026-10-29:** `flyer` (khóa `fly`: bay khắp màn hình có trận trong cửa sổ topmost riêng theo DPI, chỉ màn có trận, ẩn khi fullscreen/Pause/menu, né HUD/bảng thẻ/túi đồ/hàng icon; ngắm 0,8 s rồi bắn đạn dính trúng cả mèo đang cầm và trong `landingGrace`, không choáng, không làm rơi mèo, Khiên chặn, mèo dưới sàn chậm ×0,6 trong 2 s; 3 nhát vuốt khi cầm/Đẩy/Kẹp gắp giật xuống → rơi, choáng 3 s, ×1,5 sát thương, gồng 1 s bay lên) và `burrower` (khóa `burrow`: dưới đất chỉ thấy ụ đất, không gì trúng; vòng nứt 1 s ở chỗ mèo rồi trồi: mất máu + choáng 0,8 s; nhấc mèo né được; thả mèo đè lên vòng nứt/lúc trồi thì kẹt ngoài 3 s; Kẹp gắp lôi lên). Có trong battle.json gốc nhưng chưa trận nào dùng, thử bằng docs/examples/battle-m14.json. BattleSkyTests + UiChecks (`SkyVisualChecks`, cửa sổ thật trong `BattleChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng. Captcha (A2) chưa có nên luật "không ra khi có slime bay" làm cùng A2. Chi tiết: docs/BATTLE-DATA.md → Slime bay và slime đào hang |
| M15 ✅ | S7 băng, S8 bám tường | **Xong 2026-10-29:** `icy` (khóa `ice` + `throwHit`: vỏ băng làm đòn vũ khí chỉ còn ×0,15, mèo ưu tiên đánh con khác; mèo bay sau cú ném xuyên qua ≥ 650 DIPs/s thì vỡ vỏ, mất 12, choáng 1,5 s; ướt vì Bình xịt chỉ cần ≥ 400; vỏ mọc lại sau 10 s, tinh anh 6 s; Bom/kỹ năng/app/độc không bị vỏ giảm) và `clinger` (khóa `cling` + `throwHit`: tới mép trái/phải gần nhất, leo lên chân cao 60 (con thứ hai cùng mép +46, tối đa 110), là quái trên không; ngắm 0,9 s vòng đỏ ở chỗ mèo rồi ném gai theo đường cong, trúng mèo dưới sàn trong 36 DIPs, không choáng, nhấc/grace/Khiên né; tinh anh 2 gai; ném mèo xuyên qua ≥ 450 DIPs/s, Đẩy, Kẹp gắp giật xuống hoặc choáng → rơi, choáng 2,5 s, đánh dưới sàn 4 s rồi leo lại). Có trong battle.json gốc nhưng chưa trận nào dùng, thử bằng docs/examples/battle-m15.json. BattleWallTests + UiChecks (`WallVisualChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng (232/232). Chi tiết: docs/BATTLE-DATA.md → Slime băng và slime bám tường |
| M16 ✅ | S9 nhím, S10 hẹn giờ, S11 trộm | **Xong 2026-10-29:** `hedgehog` (khóa `quills`: gai nằm 3 s, dựng 0,5 s báo trước, xù 2,5 s (tinh anh 4 s); đang xù thì đòn vũ khí ×0,5 và mèo bị đâm 5 máu mỗi nhát, không choáng, Khiên chặn; Bom/kỹ năng/app/độc không bị giảm; mèo ưu tiên con khác; Bình xịt làm gai cụp tới khi khô), `timer` (khóa `clock`: 20 s (tinh anh 15 s), choáng thì ×0,3; hết giờ gọi 2 slime thường + 1 slime nhanh ở mép sau lưng mèo, đợt chờ hạ hết; tối đa 2 lần; hạ sớm thì thôi) và `thief` (khóa `steal`: không gây sát thương; lượt gồng lấy năng lượng icon app nhiều nhất hoặc kỹ năng sẵn có hồi lâu nhất, chạy ×1,5 về mép xa, đứng 6 s rồi trốn, không tính là mèo hạ; hạ trước thì trả lại; Đẩy/choáng lúc gồng thì hụt). Người dùng chọn: trộm trốn được, hẹn giờ gọi tối đa 2 lần. Có trong battle.json gốc nhưng chưa trận nào dùng, thử bằng docs/examples/battle-m16.json. BattleQuillsTests + UiChecks (`QuillsVisualChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng (232/232). Chi tiết: docs/BATTLE-DATA.md → Slime nhím, Slime hẹn giờ, Slime trộm |
| **Boss** | | |
| M17 ✅ | B1 Slime Nén | **Xong 2026-10-29:** boss `zip` (900 HP). Pha 1 khóa mới `roll`: mèo dưới sàn cách 140–520 thì gồng 1,1 s (dải đỏ trên sàn) rồi lăn 460 DIPs, mèo trên đường mất 24 + choáng 0,9 s, nhấc mèo thì né; mỗi 7 s (nổi giận 4,5 s); gần thì nện. Pha 2 (70% Pháo đài, 50% Kỵ binh) dùng khóa mới của pha khiên `formation` + `roped`: mỗi quân nối dây về boss, còn dây thì boss không mất máu; cắt bằng mèo đang cầm/đang bay xuyên dây ≥ 300 DIPs/s hoặc Kéo cắt (quân choáng 1 s), hạ quân cũng mất dây; hết dây thì khiên vỡ, quân vẫn đánh. Pha 3 (25%) khóa `pack`: nén tại chỗ ×0,72, 4 `zipshard` (khóa mới `still`: không đi, không đánh) quanh nó, phá hết thì vỡ khiên, nổi giận. Có trong battle.json gốc nhưng chưa trận nào dùng, thử bằng docs/examples/battle-m17.json. BattleZipTests + UiChecks (`ZipVisualChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng (217/217). Chi tiết: docs/BATTLE-DATA.md → Slime Nén |
| M18 ✅ | B2 Slime Mẹ, B3 Vua biến dị | **Xong 2026-10-29:** boss `mother` (650 HP, khóa mới `brood`): mỗi 10 s (nổi giận 6,5 s), khiên tắt và không choáng, vòng tím 0,6 s rồi đẻ 3 slime hợp thể sát nhau ở phía mèo (tối đa 6 quân sống); Slime khổng lồ do bầy của mẹ nhập thành không đánh mèo mà đi về bú: sát mẹ thì chuyển 20 HP/s máu của nó sang mẹ tới khi cạn rồi biến mất (không tính mèo hạ); hạ nó giữa đường thì mẹ không được gì. Boss `mutant_king` (1000 HP): pha khiên và phong tinh anh như Vua chương 1, quân có slime phun; khóa mới `crownLinks`: mỗi lần phong tinh anh nối thêm lần lượt dây xích (quân khác gánh 50% cho tinh anh) hoặc trống nhịp (tinh anh thành nhạc trưởng), bỏ qua loại không còn chỗ trong giới hạn 2 loại; khóa mới `rain`: mỗi 12 s khi khiên tắt, 4 giọt rơi quanh mèo sau vòng cam 1 s (14 máu, không choáng, nhấc mèo/Khiên thì né, để vũng). Có trong battle.json gốc nhưng chưa trận nào dùng, thử bằng docs/examples/battle-m18.json (Mẹ) và battle-m18b.json (Vua; hai boss không chung trận được vì 3 loại liên kết). BattleBossM18Tests + UiChecks (`BroodVisualChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng (335/335). Chi tiết: docs/BATTLE-DATA.md → Slime Mẹ và Vua Slime biến dị |
| **Nội dung và chế độ chơi** | | |
| M19 ✅ | M4 đồ rơi, M5 xu | **Xong 2026-10-29:** khóa mới `drops` (PetBattle.Drops.cs): slime bị mèo hạ rơi cá/xu/bình với 20% (tinh anh 60%), boss rơi 3 xu; click để nhặt (thứ tự: mèo → kỹ năng đang chọn → đồ rơi → quái → icon). Cá +10 HP, bình làm kỹ năng đang hồi lâu nhất sẵn sàng, xu vào chuyến Viễn chinh. Nằm 8 s (1,5 s cuối mờ, nhấp nháy), tối đa 6 món; Pause, chọn thẻ, hướng dẫn đứng hình đóng băng; thắng thì gom xu còn trên sàn. Người dùng chọn: có ở Viễn chinh (cá, xu, bình) và Vô tận (cá, bình), không ở chương 1/Trận tự do; xu chưa tiêu được tới M23; chỉ click, không tự nhặt. `ExpeditionRun.Coins` (thua/rời trận không cộng, hết chuyến là hết; file cũ đọc là 0); HUD/bản đồ/kết quả hiện xu. Luồng ngẫu nhiên riêng nên bảng mô phỏng cân bằng giống hệt trước từng dòng (240/240). rulesVersion Viễn chinh/Vô tận để M23/M24 tăng. BattleDropTests + UiChecks (`DropVisualChecks`, xu trong `ExpeditionChecks`). Chi tiết: docs/BATTLE-DATA.md → Đồ rơi và xu |
| M20 ✅ | T1 Bảo vệ giỏ cá | **Xong 2026-10-29:** khóa màn `basket` (PetBattle.Basket.cs): giỏ giữa dải trận với `fish` cá; cứ `every` slime thường thì một con tha cá: tới giỏ, đứng `grab` giây, giơ cá chạy ra mép xa phía bên kia mèo, tới mép thì mất cá (không tính mèo hạ). Hạ/choáng/Kẹp gắp con đang tha thì cá rơi: click về giỏ ngay, để yên `back` giây tự về, slime khác nhặt được (nhanh hơn). Mèo ưu tiên con đang tha. Mất hết cá thì thua dù còn máu (cả khi đang cầm mèo); thắng thì cá còn rơi/còn trên tay tính là giữ. Kết quả "giữ được/mất N/M cá", kỷ lục màn `MostFish` (chỉ trận thắng). Chưa màn gốc nào dùng, thử bằng docs/examples/battle-m20.json. BattleBasketTests + UiChecks (`BasketVisualChecks`); bảng mô phỏng cân bằng giống hệt trước từng dòng (240/240). Chi tiết: docs/BATTLE-DATA.md → Bảo vệ giỏ cá |
| M21 ✅ | C1 Chương 2: 5 màn + hướng dẫn đứng hình | **Xong 2026-10-29:** chương `download_dump` mở khi thắng Vua Slime (`unlockAfter`), bản đồ có ‹ › đổi chương, kết quả trận trùm có "Chương tiếp". 5 màn: Cổng tải về (Quạt), Thư mục ma (Đèn laser), Giữ giỏ cá (Kẹp gắp, sống sót 75 s), Đầm nhầy (Bình xịt, cuối là Slime Mẹ), Slime Nén (Kéo cắt). Màn mở kỹ năng **cho mượn** nó tới khi thắng (thay ô cuối, người dùng duyệt 2026-10-29), dòng "Gợi ý mang", đồ rơi cá + bình, `maxMalware` 4. Bước hướng dẫn "gặp" cho từng quái mới và bước "làm" (cá rơi, vòng nứt, bong bóng, cú lăn). ChapterTwoTests + `AdventureBalanceTests.RunChapter2` (Thường simple ≥ 80% mọi màn; ngoại lệ ghi trong test: màn 2 simple còn tới 95% máu, Slime Nén mức Khó kiếm/búa chênh tới 40 điểm) + UiChecks; bảng chương 1 giống hệt trước (195/195). Chi tiết: docs/BATTLE-DATA.md → Chương 2 |
| M22 ✅ | C2 tranh/bản đồ/phông, C3 phần thưởng | **Xong 2026-10-29:** khóa chương `scene` (`dump`): banner bản đồ và thẻ Phiêu lưu ở Sảnh vẽ "Bãi rác Tải về" (đống thư mục, trang giấy, mũi tên tải về, Slime Nén, giỏ cá), mốc bản đồ riêng (tải về, thư mục, cá, trang giấy, vương miện), phông trận là cửa sổ xuyên chuột ngay dưới dải trận (hai đống thư mục ở hai đầu, giữa để trống), chỉ ở màn chương 2. Phần thưởng: phụ kiện Mũ thư mục (`FolderHat`, màn 3) và khóa màn mới `keepsake`: Thú bông Slime Nén (`zip_plush`, kiểu `plush`, màn 5) — kỷ vật trên góc trưng bày, physics như kệ (cầm/ném/rơi/nảy/gờ/Bonk), trang Phần thưởng có "Kỷ vật Phiêu lưu". Luật trận, `rulesVersion`, kỷ lục không đổi. ChapterRewardTests + UiChecks `ChapterArtChecks`. Chi tiết: docs/BATTLE-DATA.md → Hình và phần thưởng chương 2 |
| M23a ✅ | X1 Viễn chinh tuyến 2, X3 cửa hàng mèo (người dùng chọn 2026-10-29: chia M23 làm hai; cửa hàng có ở cả hai tuyến, tuyến 1 lên rulesVersion 7 nhưng chuyến r6 đang đi vẫn đi tiếp) | **Xong 2026-10-29:** `expedition.routes` (tuyến chia sẻ luật HP, sự kiện, di vật, viên nang, cửa hàng; kỷ lục riêng), tuyến 2 "Lối Bãi rác" mở khi thắng Slime Nén: 6 bậc qua 3 vùng (`zones`: Đầm nhầy, Kho cá có giỏ cá, Mê cung thư mục), trùm Slime Nén, phông `dump`; chip chọn tuyến trên bản đồ. Cửa hàng: loại ô `shop` (không ở bậc 1, mỗi bậc tối đa một), bữa cá 2 xu, thẻ 3, thẻ hiếm 5, di vật 6, mỗi món một lần. `keepRuns: [6]`: chuyến r6 đi tiếp không có cửa hàng, kỷ lục r6 riêng. expedition.json version 5. ExpeditionRouteTests + ExpeditionReleaseTests chạy cả hai tuyến + UiChecks (chọn tuyến, phông, cửa hàng); mô phỏng tuyến 2 Normal thắng 29–33/40. Chi tiết: docs/BATTLE-DATA.md → Tuyến Viễn chinh và cửa hàng mèo |
| M23b ✅ | X2 sự kiện (người dùng duyệt 2026-10-29): Lò rèn (4 xu hoặc 15% HP: vũ khí +15% sát thương trong chuyến, tối đa 2 cấp), Lời nguyền (chọn 1 trong 2 thẻ hiếm; 2 trận kế quái mạnh hơn 25% và dễ ra tinh anh hơn), Mèo bị lạc (mất 10% HP: +3 xu và chọn 1 trong 3 thẻ, hoặc đi tiếp). X4 6 di vật: Nam châm xu (đồ rơi +50%, nằm sàn 5 s), Vòng gai (phản 30% sát thương cận chiến, chém −10%), Răng nanh độc (chém gây độc, hồi đòn chậm 10%), Viên đá lạnh (chém đóng băng, sát thương −15%), Pin dự phòng (kỹ năng hồi nhanh 20%, không rơi bình), Mũ bảo hiểm (miễn choáng lăn/nện, chạy chậm 10%) | **Xong 2026-10-29:** ba loại sự kiện `forge`/`curse`/`lost` và sáu `effect` di vật mới (`magnet`, `thorns`, `venom`, `frost`, `battery`, `helmet`) đúng số liệu đã duyệt, có ở cả hai tuyến; "nện" hiểu là các đòn đè có choáng: cú lăn Slime Nén, lò xo bật, slime đào hang trồi lên (đòn nện thường của quái vốn không choáng); Vòng gai chỉ phản đòn đánh gần và cú lao. Lò rèn chỉ nhân nhát chém vũ khí; đã rèn đủ 2 lần thì không bốc Lò rèn nữa. rulesVersion không đổi (cùng đợt M23a), expedition.json version 6. ExpeditionEventTests (luật, file, di vật trong trận, kiểm tra dữ liệu, đo từng di vật trên chuyến mô phỏng: 19–23/24 so với 20–22/24 không di vật) + ExpeditionReleaseTests đi qua cả 6 sự kiện + UiChecks (Lò rèn, Lời nguyền, Mèo bị lạc). Chi tiết: docs/BATTLE-DATA.md → Sự kiện và di vật mới |
| M24 ✅ | E1, E2 Vô tận (người dùng chọn 2026-10-29: làm chung một mốc; bắt buộc chọn 1 trong 3 đột biến, không có Bỏ qua) | **Xong 2026-10-29:** `rulesVersion` Vô tận 5 → 6 (kỷ lục cũ đứng riêng, mốc phần thưởng vẫn tính mọi phiên bản). E1: trùm xoay vòng `bosses` Vua chương 1 → Slime Nén → Vua biến dị, Vua thường đi kèm từ đợt 40 (trước: 30); 4 chặng `eras` từ đợt 15 (Đầm nhầy 15–20, Ban nhạc slime 21–30, Bãi rác 31–40, Mê cung ảo ảnh 41–50, rồi lặp), mỗi chặng thêm 4–6 quái 1.5 vào 4 quái cũ (~38–40% trọng số), cả 19 quái thường mới đều có mặt; giới hạn 2 loại liên kết/2 loại quái chuột xét theo từng chặng và từng đợt trùm (lúc chạy, đầu mỗi đợt Vô tận sàn trống nên quên loại liên kết của đợt trước). E2: dọn xong mỗi đợt trùm (sau thẻ) chọn 1 trong 3 đột biến (không lặp luật vừa xong), giữ tới khi dọn xong đợt trùm kế rồi trả thưởng; thua/Kết thúc giữa chừng không có thưởng. 6 luật: Slime tăng tốc (+25% tốc độ quái → đồ rơi ×2 trong lúc đó), Đông nghẹt (+30% quái, +2 trên sàn → +1 lượt thẻ), Toàn tinh anh (+25% tinh anh → chọn 1 thẻ hiếm), Mèo thủy tinh (nhận +30% → gây +30% trong lúc đó), Nhịn đói (không cá, không hồi giữa đợt → +20 HP tối đa), Kỹ năng lười (hồi chậm 40% → hồi đầy + 1 lượt thẻ). Mô phỏng bot simple Normal kiếm 8 lượt: luật cũ trung vị đợt 30, luật mới 31 (thấp nhất 20). EndlessMutationTests + UiChecks (bảng đột biến, HUD). Chi tiết: docs/BATTLE-DATA.md → Chặng, vòng trùm và Đột biến của Vô tận |
| M25 ✅ | J1 nhóm Mưu mẹo + thẻ kỹ năng mới (người dùng duyệt 2026-10-30: 5 thẻ tiện ích cũ vào nhóm, vào qua ô tiện ích 0.25 → 0.35; thẻ hiếm Chấn động đổi tên "Cú đáp địa chấn"; Vô tận r7, Viễn chinh r8 cho chuyến r7 đi tiếp) | **Xong 2026-10-30:** nhóm `trick` (14 thẻ): 5 thẻ tiện ích cũ; mỗi kỹ năng mới một thẻ (Laser bền +1,5 s, Quạt turbo +25%, Kẹp chắc +50% cú ném, Kéo sắc +40%, Bình to +1,5 s; chỉ ra khi mang kỹ năng); Mũi thính (đồ rơi ×1,3), Cá béo (+10 HP; chỉ trận có đồ rơi); hiếm: Cú đáp địa chấn (thả/ném mèo sau nhấc ≥ 0,3 s: sóng 110 DIPs, 12 sát thương quái trên sàn, hồi 6 s), Ảo thuật (25% kỹ năng hồi một nửa). Cộng hưởng: Phối hợp 3 (dùng kỹ năng → 3 s +20% sát thương), Nạp nhanh 6 và Đại tiệc 9 chỉ Vô tận. Mời thẻ: 3 nhóm cũ giữ ô riêng, Mưu mẹo vào ô tiện ích. Khóa mới `shopFrom` cho Viễn chinh. BattleTrickeryTests + UiChecks (thẻ Mưu mẹo trong bảng chọn, icon 9 thẻ, túi đồ 4 nhóm); mô phỏng Vô tận/Viễn chinh gần như không đổi. Chi tiết: docs/BATTLE-DATA.md → Nhóm Mưu mẹo |
| M26 ✅ | A1–A4 app | **Xong 2026-10-30** (người dùng duyệt làm chung cả 4, không tách): chỉ ở trận có `newAppsFrom` (5 màn chương 2, 14 trận Lối Bãi rác: 1; Vô tận: 15); chương 1, tuyến đầu, trận tự do không đổi (bảng mô phỏng giống hệt từng dòng). Icon thứ 6 Chụp Màn Hình (sạc 75 s; đứng hình mọi quái 2 s; nhiễm: mèo lóa 1,5 s mỗi 11 s, vuốt trượt). Captcha (cửa sổ topmost riêng đè lên mèo, 1/3 sàn, 2–4 ô mèo trong 9, sai thì xáo, 15 s, không ra khi có slime bay), Pin yếu (hồi chiêu ×0,4 tới khi Cắm sạc), Con trỏ đơ (khóa kỹ năng 5 s hoặc tới Tiếp tục) trên HUD. rulesVersion chương 2 → 2, Lối Bãi rác → 3 (chuyến cũ đi tiếp), Vô tận → 8. NewAppTests + UiChecks `NewAppVisualChecks` và khối cửa sổ thật trong `BattleChecks`. Hướng dẫn chương 2 chưa có bước cho 4 app này. Chi tiết: docs/BATTLE-DATA.md → App mới |
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
