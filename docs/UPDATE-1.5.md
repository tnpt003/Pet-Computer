# Bản 1.5 — Slime phản công (bản chốt)

Cập nhật: 2026-11-06 (Asia/Saigon). Trạng thái: **xong M01–M29, đợt B/C, góp ý 05/11; đã hợp nhất Slime Nén thiết kế lại và hai boss nâng cấp, kiểm tra tự động bản tích hợp đạt. Chưa phát hành 1.5.0.** Còn M30 và nghiệm thu tay.

Cách đọc file này:

- Đây là bản mô tả **luật hiện tại** của 1.5, gom từ thiết kế 26/10, đợt B, đợt C và góp ý 05/11. Khi tài liệu khác lệch với file này thì đối chiếu code và battle.json.
- Số liệu gốc nằm ở `src/DesktopPet/Assets/battle.json`. Ý nghĩa từng khóa: docs/BATTLE-DATA.md. Số trong file này để hiểu luật, không thay battle.json.
- Ngoại lệ đã duyệt: docs/APPROVED-EXCEPTIONS.md (có hiệu lực như AGENTS.md).
- Lịch sử: thiết kế gốc và nhật ký từng mốc ở docs/archive/UPDATE-1.5-design-2026-10.md; đợt C ở docs/GOPY-C-2026-11-04.md; từng lỗi ở Issue_fixxed.md.
- Thêm nội dung mới: docs/EXTENDING.md. Ý tưởng mới vào mục "Backlog sau 1.5" cuối file, không làm trong 1.5.
- "Trận tự do" trong tài liệu cũ nay là bộ Mặc định của **Trận tự chọn**.
- Chuẩn hóa logic, hình học cảnh báo, giới hạn quân, phương pháp cân bằng và điều kiện hoàn tất: [STANDARDIZATION-1.5.md](STANDARDIZATION-1.5.md). Số đo nhánh riêng có ghi commit, không đại diện bản tích hợp.

## Giữ nguyên

- Phân vai: toàn bộ combat ở C# (PetBattle*), số liệu trong battle.json. Python không tham gia combat.
- Battle Mode mặc định tắt. Ngoài trận không đổi gì (sleep, drag/throw, emotion, đi trên cửa sổ, kệ kỷ vật…).
- Physics kéo/ném của mèo giữ nguyên. Các ngoại lệ trận đổi vị trí hoặc vận tốc mèo (lò xo đẩy, va vỏ băng, Cú đáp địa chấn) đã được duyệt riêng.
- App trong trận vẫn là icon tự vẽ, không đọc app thật.
- Mọi chữ mới có vi/en/ja. Icon và tranh mới theo docs/DESIGN-SYSTEM.md.
- **Chương 1:** 5 màn, đợt quái, Hướng dẫn phiêu lưu và rulesVersion giống hệt 1.4.4; bộ kỹ năng luôn là 5 kỹ năng cũ; quái mới không vào chương 1.
  - Các hệ dùng chung vẫn có mặt ở chương 1: Cuộn len, Găng đấm, Nộ (tự kích hoạt) và lượt mời thẻ (`utilityChance` 0,25 → 0,35, thêm 2 thẻ hiếm Cú đáp địa chấn, Ảo thuật). Mô phỏng chương 1 vì vậy có đổi. Người dùng chốt 2026-11-05: **giữ nguyên rulesVersion chương 1**, kỷ lục cũ và mới dùng chung.

## Ngoại lệ Battle Mode của 1.5

Bản đầy đủ: docs/APPROVED-EXCEPTIONS.md → Battle Mode, App trong trận, Sửa sau chơi thử 1.5. Tóm tắt:

- **Mèo đang cầm** chỉ đánh được quái trên không (slime bay, slime bám tường), mèo tự vung vuốt. Quái dưới sàn không bị mèo đang cầm đánh.
- **Mèo đang bay sau cú ném** không tự đánh, chỉ tác động quái có tương tác ném: vỏ băng, slime bám tường, dây nhầy, tay kéo bom Slime Nén.
- **Mèo được thả:** chỉ tác động slime đào hang lúc trồi, và thẻ Cú đáp địa chấn.
- **Đạn dính của slime bay** trúng cả mèo đang cầm và trong `landingGrace`, không làm rơi mèo, không choáng; Khiên chặn. Đây là quái duy nhất làm mèo đang cầm mất máu.
- **Con trỏ tương tác với slime** trong khung trận và trên cửa sổ riêng của slime bay/bám tường. Click trúng mèo luôn là nhấc mèo. Chỉ chặn khi mèo nằm trong tay; mèo đang bay/nảy vẫn rê chuột được.
- **Trận được đổi mèo:** lò xo/Slime Mẹ đẩy mèo dưới sàn ~80 DIPs, Vua biến dị phình nổ đẩy 65; va vỏ băng thì mèo bật ngược; Cú đáp địa chấn thì mèo không nảy, đứng yên ~0,6 s. Nộ chặn và giải đẩy lùi nhưng không miễn sát thương thường.
- **Cửa sổ:** slime bay và slime bám tường trên cao có cửa sổ topmost riêng; lớp chọn vị trí cho Kéo cắt và Quạt; Chụp Màn Hình chớp không chặn chuột.
- **Slime đi theo mèo đang bế** với 50% tốc độ.
- **Hướng dẫn phiêu lưu** mở rộng sang chương 2, luật như chương 1.

## Nguyên tắc thiết kế

- **Không quái nào bắt buộc một kỹ năng cụ thể.** Kỹ năng phù hợp chỉ làm dễ hơn; mỗi quái luôn hạ được bằng thao tác của mèo hoặc kỹ năng cũ.
- **Quái liên kết:** mỗi con có việc riêng và thêm tối đa một liên kết. Liên kết luôn nhìn thấy và phá được; phá được thì có thưởng. Mỗi liên kết có công tắc tắt trong battle.json.
- **Quái solo:** không cần đồng đội, mỗi con buộc một cách chơi riêng gắn với desktop pet.
- Mỗi trận tối đa 2 loại liên kết và 2 loại quái dùng chuột. Ngoại lệ: đội hình do slime hẹn giờ gọi.
- Mỗi quái mới có bản tinh anh, có lần giới thiệu ở chương 2 hoặc banner, và trang trong Sổ tay quái.
- Thứ tự click: mèo → kỹ năng đang chọn → đồ rơi → quái → icon.

## Nội dung

### Quái liên kết (Q)

| Mã | Quái | Luật | Cách phá |
|---|---|---|---|
| Q1 | Slime phun | Đứng xa, phun toàn khung trận, có vòng báo chỗ rơi. Vũng làm mèo mất 2 HP/s và chậm ×0,7. Slime nhanh chạm vũng được buff cả trận (lao xa ×1,5, nhanh ×1,4); slime phun ưu tiên phun trước mặt slime nhanh chưa buff | Nhấc mèo khỏi vòng; Đẩy; Cuộn len kéo lại; Quạt làm khô vũng |
| Q2 | Slime lò xo | Bẫy vòng 100. Mèo vào vòng thì nén 0,6 s rồi bật: mất máu, choáng 0,8 s, bị đẩy ~80 DIPs; nghỉ 4 s. Mèo đứng ngoài vòng trong 170 DIPs quá 1,5 s, hoặc đánh từ ngoài vòng, thì nó bật nhảy tới | Nhấc mèo ra trong lúc nén; 2 đòn làm bẹp 3 s; Bom, Đẩy, Quạt |
| Q3 | Slime nhạc trưởng | Vòng 220, 4 nhịp (tinh anh 3). Nhịp cuối là **trống lớn**: mèo trong vòng mất máu và loạn nhịp 2 s, mọi nhát trượt | Nhấc mèo đúng nhịp cuối; Khiên; nộ; đánh, Đẩy, choáng, Quạt, Kéo thì đếm lại |
| Q4 | Slime xích | Đứng cách đồng minh đúng 200, dây gánh 50% (tinh anh 70%). Còn xích thì nó nhận ×0,5. Mỗi đồng minh tối đa 2 dây | Kéo/ném mèo xuyên dây; Kéo cắt; hạ xích thì đồng minh choáng 1 s |
| Q5 | Slime hợp thể | Bầy 3 ra cùng lúc, chia đòn trong 260 DIPs, một con bị hạ thì con còn lại nổi giận, 2 con vẫn nhập được. Nhập thành Slime khổng lồ (máu bằng tổng, 3 lớp chắn hồi 10 s, dậm đất lúc vừa nhập) | Quạt giữ chân; Bom khi đang chụm; hạ một con ở giữa |
| Q6 | Slime bom | Cưỡi slime nhanh. Mèo cách 120–210 thì ngựa quăng bom (vòng đỏ theo mèo lúc gồng, khóa khi quăng), bom chạm đất là nổ. Hạ bom đang cháy ngòi thì bom xịt. Mỗi thảo dược ghép một bom bằng **Lá chắn lá**: đỡ vụ nổ đầu, bom cháy đen bay về tay slime nhanh để ném lại | Đánh ngựa lúc gồng; hạ thảo dược; Kéo cắt; Kẹp gắp biến bom thành bom của người dùng |
| Q7 | Slime tách đôi | Bị hạ vỡ thành 2 nửa (tinh anh 3). Thảo dược ở đâu cũng tự hồi nửa, không trừ ngân sách, hai nửa nhập lại thành con mẹ; con nhập lại bị hạ thì không tách nữa | Hạ thảo dược trước; Bom cả 2 nửa |
| Q8 | Slime cờ hiệu | Bất tử tới khi cắm cờ đầu. Tự chọn lúc và chỗ cắm, cắm 2 cờ cách nhau 12 s, né mèo khi còn cờ. Vòng 180: slime bớt 30% sát thương, không bị choáng. Cờ 30 HP, bất tử khi chủ còn sống | Hạ chủ rồi nhổ cờ; Kéo cắt nhổ một nhát; cờ đổ thì cả đội hoảng 2 s; Laser dụ quái ra khỏi vòng |

**9 đội hình** (Director bốc theo trọng số; một đội hình là một đơn vị: ra cùng lúc, cùng phía, chiếm 1 chỗ):

| Đội hình | Thành phần | Mèo cần làm |
|---|---|---|
| Pháo đài | Phun, thảo dược, đá | Nhấc mèo qua đá, hạ phun trước |
| Kỵ binh | 2 bom cưỡi 2 slime nhanh, rồi slime phun | Hạ ngựa giữa đường |
| Đội trống | Nhạc trưởng + 3 slime | Đẩy nhạc trưởng trước nhịp cuối |
| Dây chuyền | 2 xích nối vào 1 đá tinh anh | Cắt dây trước rồi đánh đá |
| Bạt nhún | Slime + slime nhanh, lò xo ở mép sau lưng mèo | Đánh bẹp lò xo hoặc nhấc mèo khỏi vòng |
| Bầy nhập | 2 bầy hợp thể + thảo dược | Bom đúng lúc chụm |
| Quân cảm tử | Thảo dược + bom + 2 slime nhanh | Hạ thảo dược; đánh slime nhanh lúc chờ bắt bom |
| Pháo kích | 2 bám tường + băng đứng trước | Quạt thổi rơi, ném mèo phá băng |
| Báo động | Hẹn giờ + trộm + bóng ma | Hạ hẹn giờ trước |

### Quái solo (S)

| Mã | Quái | Luật | Cách hạ |
|---|---|---|---|
| S1 | Slime bóng ma | Ẩn hoàn toàn thì xuyên mèo, không bị đánh; hiện ra chờ ít nhất 0,7 s mới gồng đòn | Rê chuột soi (hiện 2 s); Laser |
| S2 | Slime bong bóng | Trong bong bóng miễn mọi đòn; mọc lại sau 6 s | 3 lần chọc (tinh anh 5), click hoặc đòn combo của mèo; Kéo cắt |
| S3 | Slime ảo ảnh | Bản thật một phía mèo, bản giả phía kia. Mỗi 3 s quay ~60%: trúng thì bản thật đổi chỗ với một bản giả trong khói, trượt thì chỉ phụt khói | Rê chuột: giả nhòe, thật có bóng; Laser; hạ bản thật thì bản giả tan |
| S4 | Slime dỗi | Chỉ nhát mèo đánh trực tiếp mới tính, và làm nó phồng. Mèo chỉ đánh nó khi còn toàn slime dỗi hoặc người dùng chọn. Phồng tối đa thêm 3 nhát thì òa khóc dậm chân rồi bỏ chạy: tính hạ, không rơi đồ | Rê chuột qua lại 1400 DIPs (tinh anh 2200); dỗ xong "lây vui" +30% cho con gần |
| S5 | Slime bay | Bay khắp màn hình có trận, đổi chỗ định kỳ, bắn đạn dính. Sàn hết quái thì mèo đi theo bên dưới nó | Cầm mèo lại gần vuốt 3 nhát (tinh anh 4), Đẩy, Quạt, Kẹp gắp, choáng: rơi, nằm 3 s, nhận ×1,5, gồng 1 s bay lên |
| S6 | Slime đào hang | Chui sàn, trồi dưới chân mèo sau vòng nứt 1 s | Nhấc mèo lúc vòng hiện; thả đè lúc trồi thì kẹt ngoài 3 s; Kẹp gắp lôi lên |
| S7 | Slime băng | Vỏ băng làm đòn vũ khí còn ×0,15; mọc lại sau 10 s. Mèo ném va vỏ thì "cộp" và bật ngược, hết vỏ thì bay xuyên qua | Ném ≥ 380 DIPs/s (ướt 340), đủ nửa thì nứt; nhát 5 Găng đấm; cú đập Kẹp gắp; Bình xịt 3 s |
| S8 | Slime bám tường | Leo mép trái/phải tới cao 520 DIPs, ném gai, đổi độ cao sau mỗi loạt; rơi xuống thì hết choáng leo lại ngay. Mèo tới cách mép ~90 và ngước nhìn, không chạy dí sát | Ném mèo ≥ 450 DIPs/s; Đẩy; Quạt; Kẹp gắp; choáng; cầm mèo lại gần |
| S9 | Slime nhím | Xù gai chu kỳ, có pha cảnh báo; đánh lúc xù thì ×0,5 và mèo bị đâm 5 máu | Đánh lúc xẹp; Bom; Bình xịt làm cụp gai |
| S10 | Slime hẹn giờ | Đứng yên, đồng hồ 20 s, gọi tối đa 3 lần. Mỗi lần gọi một đội hình ngẫu nhiên trong 8 đội, ra ngay ở mép sau lưng mèo, vượt được `maxAlive` | Hạ trước; choáng làm đồng hồ chậm; Kẹp gắp làm đồng hồ dừng |
| S11 | Slime trộm | Không đánh mèo. Hút từ xa 160 DIPs năng lượng icon hoặc một kỹ năng rồi chạy về mép, trốn sau 6 s. Tinh anh lấy 2 thứ | Hạ thì trả lại; Đẩy/choáng lúc gồng; Kẹp gắp giữ đủ 5 s thì nhả đồ và biến mất |

Trong trận cảm xúc tự động vẫn bị chặn: S1, S4 không gây cảm xúc hay ngủ cho mèo.

### Boss (B)

| Mã | Boss | Luật |
|---|---|---|
| B1 | Slime Nén, trùm chương 2 (1100 HP; thiết kế lại 05/11, 06/11; Issue #53, #55) | Luật đầy đủ: docs/ZIP-SLIME.md. Ba giai đoạn, mỗi giai đoạn ba đòn chọn từ túi theo seed; mech 66% đánh khóa đai (12 đòn, đổi bên mỗi 3), 33% ghép thư mục + né cây, 1 HP đánh 6 tay kéo; phase 2–3 có đòn đẩy riêng 0 damage; hai debuff Nén và Khóa ZIP (khóa đi/nhấc, vẫn đánh); đòn trúng mèo đang cầm/bay khi thực sự chạm. | |
| B2 | Slime Mẹ, boss phụ chương 2 (650 HP) | Ổ ủ phá được, gọi con yếu về, dây cứu con cắt được, con che thân, bầy nhảy, giận lao và lứa cuối đổi máu; hồi 20 HP/s. Luật chi tiết: [BOSS-TACTICS](BOSS-TACTICS.md) |
| B3 | Vua Slime biến dị, Vô tận (1000 HP) | Giữ tinh anh/xích/trống; thêm tay quét, phình nổ, mưa hai nhịp, sóng sàn, hút nhầy thành giáp cắt được và quá tải; cuối trận ghép hai chiêu. Không có mầm/slime con biến dị. [BOSS-TACTICS](BOSS-TACTICS.md) |

### Vũ khí (V)

| Mã | Vũ khí | Đặc điểm |
|---|---|---|
| V1 | Cuộn len | Tầm 114, 14 sát thương, quét mọi slime trong tầm phía trước, kéo slime đứng xa lại gần, không choáng |
| V2 | Găng đấm | Hồi 0,38 s, 14 sát thương. Combo thường ở nhát 3, nhát 5 hất tung quái và phá vỏ băng |

### Kỹ năng (K): 10 kỹ năng, mang 5 lên HUD

- HUD giữ 5 ô. Bảng chuẩn bị chọn 5 trong 10, nhớ lựa chọn. Mặc định là 5 kỹ năng cũ. Chương 1 không có mục chọn.
- Viễn chinh chọn lúc bắt đầu chuyến, đổi miễn phí ở điểm nghỉ. Vô tận chọn lúc bắt đầu. Trận tự chọn mang ngay cả 10.
- Mỗi màn chương 2 **cho mượn** một kỹ năng tới khi thắng thì mở hẳn, có dòng "Gợi ý mang".
- Thẻ gắn với kỹ năng chỉ ra khi kỹ năng đó có trong bộ mang theo.
- Sát thương kỹ năng không bị đá chặn, có Kết liễu, quái chết tính là mèo hạ; boss nhận 50%.

| Kỹ năng | Cách dùng | Tác dụng | Hồi |
|---|---|---|---|
| Đẩy, Cá hồi máu, Cổ vũ, Bom len, Khiên bong bóng | Như 1.4.4 | Giữ nguyên số liệu | Như cũ |
| Đèn laser | Chọn, click một chỗ | Slime trong 300 DIPs chạy theo chấm 4 s, không gồng đòn mới, 3 máu/s trên chấm; soi lộ S1, S3 | 14 s |
| Kẹp gắp | Chọn, click slime; click chỗ khác để đập | Giữ tối đa 5 s ngoài trận. Đập: 20 máu + choáng, quanh đó 8, vỡ vỏ băng. Lấy được cả slime bay/bám tường. Bom thành bom của người dùng. Trộm: giữ đủ giờ thì nhả đồ. Không gắp đá, trùm | 12 s |
| Kéo cắt | Chọn, click chỗ bất kỳ | Dải ngang 560 DIPs, báo trước 0,4 s, kéo lướt 0,7 s. Mỗi slime chạm dải 15 máu, đứt dây, dây cưỡi, dây về trùm, bong bóng, nhịp trống, Lá chắn lá; nhổ cờ một nhát. Không cắt khiên trùm | 14 s |
| Quạt | Chọn, click chỗ bất kỳ | Cột gió 150 DIPs từ quạt trần, 4 s. Slime sàn đứng yên, đòn −30%, không nhập được, nhạc trưởng đếm lại; slime bay, bám tường rơi; vũng khô. Không gây sát thương | 14 s |
| Bình xịt nước nóng | Chọn, click slime | Tự khóa và xịt tới hết nước (4 s): 2 máu/s, chậm 35%, ướt. Con đó ngã thì chuyển con gần nhất, bình +1,5 s (tối đa 1,5 bình). Xịt 3 s tan vỏ băng; gai nhím cụp | 8 s |

**K5 Mèo nổi giận** (không chiếm ô HUD, là tư thế trận, không phải cảm xúc Angry): vòng nộ quanh avatar, đầy dần khi đánh trúng (~50 nhát) và khi mất máu. Đầy thì click avatar, hoặc tự bật ở nhát trúng kế tiếp hay thay cho cú choáng sắp dính. Dùng được cả khi đang choáng, không khi đang cầm. Trong 5 s: hình mèo ×1,3, đánh lan, đẩy lùi mạnh, miễn mọi hiệu ứng bất lợi; lúc bật giải sạch hiệu ứng đang dính. Pause đóng băng thời gian.

### Cơ chế trận (M)

| Mã | Nội dung |
|---|---|
| M1 | Mèo đánh khi đang cầm, chỉ quái trên không |
| M2 | Hiệu ứng trên quái: độc, đóng băng, dính; từ thẻ, di vật, Bình xịt |
| M4 | Đồ rơi: cá (+10 HP), bình (−2 s hồi cho kỹ năng đang hồi lâu nhất), xu; click để nhặt, nằm 8 s, hình ×1,6 có quầng sáng. Có ở chương 2, Viễn chinh, Vô tận (Vô tận không có xu) |
| M5 | Xu trong chuyến, tiêu ở cửa hàng Viễn chinh; không thành tiền tệ vĩnh viễn |

M3 (bục tự vẽ) đã bỏ.

### Kiểu màn, chương 2, chế độ chơi

| Mã | Nội dung |
|---|---|
| T1 | Bảo vệ giỏ cá: slime tha cá đi; mất hết cá thì thua dù mèo còn máu |
| C1 | Chương 2 "Bãi rác Tải về", mở khi thắng Vua Slime: Cổng tải về (Quạt), Thư mục ma (Laser), Giữ giỏ cá (Kẹp gắp, sống sót 75 s), Đầm nhầy (Bình xịt, cuối là Slime Mẹ), Slime Nén (Kéo cắt) |
| C2 | Tranh, bản đồ, phông trận riêng (`scene` "dump") |
| C3 | Phần thưởng: phụ kiện Mũ thư mục, kỷ vật Thú bông Slime Nén (physics như kệ kỷ vật) |
| X1 | Viễn chinh tuyến 2 "Lối Bãi rác" (mở khi thắng Slime Nén): 6 bậc qua Đầm nhầy, Kho cá, Mê cung thư mục; trùm Slime Nén; kỷ lục riêng |
| X2 | Sự kiện Lò rèn, Lời nguyền, Mèo bị lạc |
| X3 | Cửa hàng mèo ở cả hai tuyến, tiêu xu mua thẻ, hồi máu, di vật |
| X4 | 6 di vật: Nam châm xu, Vòng gai, Răng nanh độc, Viên đá lạnh, Pin dự phòng, Mũ bảo hiểm |
| E1 | Vô tận: từ đợt 15 có 4 chặng quái mới (Đầm nhầy, Ban nhạc slime, Bãi rác, Mê cung ảo ảnh, rồi lặp); trùm xoay vòng Vua → Slime Nén → Vua biến dị; từ đợt 8, 3 đợt một lần có đội hình |
| E2 | Vô tận "Đột biến": dọn xong mỗi đợt trùm bắt buộc chọn 1 trong 3 luật lạ, dọn xong đợt trùm kế thì nhận thưởng |
| J1 | Nhóm thẻ thứ 4 "Mưu mẹo" (14 thẻ), cộng hưởng ở 3/6/9 (6 và 9 chỉ Vô tận). Thẻ hiếm **Cú đáp địa chấn**: thả mèo sau nhấc ≥ 0,3 s thì mèo không nảy, đứng yên 0,6 s, sóng nứt chạy ra hai bên 170 DIPs: 12 máu, đẩy văng, choáng 0,8 s, hồi 6 s |
| A1 | App mới Chụp Màn Hình: chớp toàn màn hình, mọi quái đứng hình 2 s. Không làm slime bay/bám tường rơi. Nhiễm thì mèo lóa, vuốt trượt |
| A2 | App độc Captcha: cửa sổ đè lên mèo, che ~1/3 sàn, bấm hết ô hình mèo thì đóng, sai thì xáo, tự đóng sau 15 s. Không ra khi có slime bay |
| A3 | App độc Pin yếu: kỹ năng hồi chậm tới khi bấm Cắm sạc |
| A4 | App độc Con trỏ đơ: khóa 5 ô kỹ năng tới 5 s hoặc tới khi bấm Tiếp tục |
| A5 | Bộ app chọn tối đa 5 icon ở bảng chuẩn bị. App độc cùng lúc tối đa 4 ở chương 2, Viễn chinh, Vô tận (chương 1 giữ 2) |
| A6 | Tác động của app lên quái trên cao: Bão Thông Báo, End Task, Thùng Rác làm slime bay/bám tường rơi; Tab Lốc Xoáy chỉ trúng quái dưới sàn. End Task nhiễm: mèo mất 10% máu tối đa (không chết), đứng hình 3 s, click mèo 3 lần tỉnh sớm. Quảng cáo khóa: đếm 8 s rồi thả một slime ngẫu nhiên trong `popupKinds` |
| L1 | Sổ tay quái trong Sảnh: hình, cách khắc chế, kỹ năng hợp, số đã hạ, trang đội hình |
| L2 | 20 thành tựu trong trận, thưởng lông và phụ kiện |
| L3 | 3 bộ lông (Bạc hà, Nửa đêm, Vàng kim), 3 phụ kiện (Khăn buộc trán, Huy chương anh hùng, Vòng cổ cá nhỏ) |
| F1 | **Trận tự chọn** thay Trận tự do: tự chọn quái, trùm, app, app độc đã gặp, số đợt, độ khó, vũ khí, kỹ năng. Lưu tối đa 6 bộ. Bộ Mặc định là trận tự do cũ, giữ kỷ lục cũ. Bộ tự dựng không kỷ lục, không thành tựu |

## Luật đã thay so với thiết kế 26/10

| Nội dung | Thiết kế gốc | Hiện tại | Nguồn |
|---|---|---|---|
| Quạt | Giữ chuột, có "hơi", va mất 4 máu | Click, cột gió | Đợt C3, #43 |
| Kéo cắt | Vuốt nét, ~6 sát thương | Click, dải 560 DIPs, 15 sát thương | #43, #46 |
| Kẹp gắp | Nắm rồi ném | Gắp, giữ, đập; lấy được quái trên cao | #43, #50 |
| Bình xịt | Giữ chuột, rê theo | Nước nóng, tự khóa mục tiêu | #41, #49, #50 |
| Nộ | 6 s, thanh dưới HP, không tự bật | 5 s, vòng quanh avatar, tự bật, giải và miễn hiệu ứng | #42 |
| Lò xo | Nén 0,8 s, vòng 60, không đẩy mèo | Nén 0,6 s, vòng 100, đẩy mèo, bật nhảy | 30/10, #34 |
| Nhạc trưởng | Cả vòng gồng đánh cùng lúc | Trống lớn, loạn nhịp | #44, #50 |
| Xích | Đi sát sau đồng minh | Đứng cách 200, có giáp ×0,5 | #36, #52 |
| Hợp thể | Đi riêng, chạm nhau 2 s là nhập | Ra cùng lúc, chia đòn, nổi giận, khổng lồ 3 lớp chắn | 30/10, #38, #41 |
| Bom | Nhảy xuống, tự đi tới, hạ thì nổ | Ngựa quăng bom, hạ thì xịt, Lá chắn lá, bật về tay | #35, #44 |
| Tách đôi | Thảo dược hồi thường rồi mới nhập | Thảo dược tự hồi lúc tách, không trừ ngân sách | #39 |
| Cờ hiệu | Đi theo, +tốc độ | Cắm 2 cờ, −30% sát thương, bất tử theo luật | #44, #47, #48 |
| Bóng ma | Còn viền mờ | Ẩn hoàn toàn | 30/10 |
| Dỗi | Mọi đòn làm phồng | Chỉ nhát mèo, có òa khóc | #41, #49 |
| Ảo ảnh | 3 bản đứng yên chỗ | Hai phía mèo, gacha đổi chỗ | #41, #49 |
| Băng | Ném ≥ 650 mới có tác dụng | Ngưỡng 380, cộp, bật ngược, nứt | #31, #42, #49 |
| Bám tường | Leo tới 110 | Leo tới 520 trong cửa sổ riêng, đổi độ cao | #32, #40 |
| Hẹn giờ | Tối đa 2 lần, gọi quân lẻ | Đứng yên, 3 lần, đội hình ngẫu nhiên | #42, #44, #50 |
| Trộm | Lấy khi đứng cạnh mèo | Hút từ xa 160 DIPs, tinh anh 2 thứ | #42 |
| Đội hình | 6 đội, các phần ra lần lượt | 9 đội, ra cùng lúc như một đơn vị | #44 |
| Đồ rơi: bình | Làm kỹ năng sẵn sàng ngay | −2 s một kỹ năng | #41 |
| Găng đấm | Combo 5 nhát | Combo nhát 3, nhát 5 hất tung và phá băng | #41 |
| Chụp Màn Hình | Trong dải trận | Chớp toàn màn hình, chỉ đứng hình | #43, #51 |
| Cú đáp địa chấn | Sóng bán kính 110, mèo nảy | Hai sóng 170, đẩy, choáng, mèo đứng yên | #42 |

## Các mốc

Nhật ký chi tiết và kết quả test từng mốc: docs/archive/UPDATE-1.5-design-2026-10.md.

| Mốc | Nội dung | Xong |
|---|---|---|
| M01–M04 | Khung liên kết, chọn 5 trong 10, mèo đánh khi cầm, hiệu ứng trên quái | 28/10 |
| M05–M08 | 5 kỹ năng mới, Nộ, 2 vũ khí | 28/10 |
| M09–M12 | Q1–Q8, đội hình | 28–29/10 |
| M13–M16 | S1–S11 | 29/10 |
| M17–M18 | B1, B2, B3 | 29/10 |
| M19–M22 | Đồ rơi, giỏ cá, chương 2, tranh và phần thưởng | 29/10 |
| M23a–M24 | Viễn chinh tuyến 2, cửa hàng, sự kiện, di vật; Vô tận chặng và Đột biến | 29/10 |
| M25–M28 | Thẻ Mưu mẹo, app mới, Sổ tay quái, thành tựu | 30/10 |
| M29a, M29 | Trận tự chọn; đo cân bằng, perf, CHANGELOG | 30/10 |
| Sau M29 | Sửa sau chơi thử 30/10, đợt B (#32–#40), đợt C (#41–#44), góp ý 05/11 (#45–#52) | 05/11 |
| Hợp nhất/chuẩn hóa | Slime Nén mới (#53), hai boss nâng cấp; sửa lứa cuối, đồng bộ vùng báo/trúng, Nộ chống đẩy, giữ luật vũng cũ; bảng luật và tiêu chí xong | 06/11; Python 58 + 5, C# 3170, WPF 1418 đạt; còn nghiệm thu tay |
| Slime Nén 06/11 | Thiết kế lại đòn, mech, debuff, luật bế mèo, HP (Issue #54, #55) | 06/11; Python 58 + 5, C# 3260, WPF 1430 đạt; còn nghiệm thu tay |
| **M30** | **Release 1.5.0 theo docs/RELEASE.md** | Chưa |

### Việc trước M30

1. Người dùng test tay theo docs/MANUAL-TEST.md → "Bản 1.5 — thứ tự test tay".
2. Đo lại rồi quyết 5 đề xuất cân bằng và perf (docs/BALANCE-1.5.md, số đo có từ trước đợt B/C).
3. Nghiệm thu bản tích hợp theo STANDARDIZATION-1.5.md. Banner Nộ đã đổi sang avatar và giải thích tự kích hoạt, có en/ja; không còn việc sửa chữ "Bấm vào thanh".
4. Mèo tự rớt khỏi tay khi đánh slime bay: chưa rõ nguyên nhân, chờ log "Cat let go in battle".
5. Nâng `<Version>` lên 1.5.0, rà README và website, tag theo docs/RELEASE.md.

rulesVersion bản tích hợp: chương 1 giữ như 1.4.4 theo duyệt 05/11; chương 2 là 3, 4, 2, 4, 4; Vô tận 12; Viễn chinh tuyến 1 r8 (`keepRuns` 6, 7), Lối Bãi rác r5 (`keepRuns` 1, 2, 3, 4). Hai nhánh thử đều từng dùng Vô tận 11/tuyến 2 r4 cho luật khác nhau, nên bản tích hợp dùng số mới. Chuyến cũ được giữ không đồng nghĩa với replay nguyên bản luật cũ. Đổi luật đáng kể phải xét tách kỷ lục cả trước release, giữ ngoại lệ chương 1 đã duyệt.

## Rủi ro còn lại

- **Hiệu năng:** `crowd-late` trung vị 56,3 fps, thấp nhất 34,4, ở bản đo 05/11 (#50); mục tiêu ~57 fps.
- **Input:** kỹ năng dùng chuột và quái dùng chuột dễ xung đột với nhấc mèo và icon app; giữ thứ tự click ở mục Nguyên tắc.
- **Cân bằng:** nhiều tổ hợp kỹ năng × quái, bot mô phỏng không đánh giá được cảm giác. Màn 3 và 5 chương 2 còn sát ngưỡng; Cuộn len màn 2 đã nới ngưỡng có duyệt.
- **Màn hình thật:** cửa sổ sát mép, lớp chọn vị trí, chớp toàn màn hình chưa thử trên DPI thật.

## Chỗ đã chừa cho mở rộng

Checklist từng loại nội dung: docs/EXTENDING.md.

- **Dữ liệu là chính:** quái ghép từ khóa có sẵn, đội hình, màn, chương, tuyến Viễn chinh, chặng Vô tận, thẻ đều thêm được trong battle.json.
- **Kỷ lục:** `rulesVersion` tách kỷ lục khi đổi luật, `keepRuns` giữ chuyến đang đi.
- **Bảng chọn kỹ năng:** thêm kỹ năng thì thành 5 trong N, HUD giữ 5 ô (`BattleSkills.Carry`).
- **Bản sau:** các hướng đã trao đổi ở docs/UPDATE-1.5.1.md, chưa chốt.

## Backlog sau 1.5

- **Bút vẽ tường** (kỹ năng 11): chọn rồi vuốt chuột một nét trên sàn, thành tường tạm 4 s mà slime không đi qua. Để sau vì trùng vai trò giữ quái xa với Đẩy/Quạt, dễ làm trận quá dễ, và phải viết lại luật tường đang dùng cho app độc "Cập nhật".
- **Kính lúp** (kỹ năng 12): click một slime, 5 s mọi đòn vào nó là chí mạng. Để sau vì chỉ tăng sát thương thuần như Cổ vũ.
- Vũ khí Cần câu cá (V3), thẻ riêng theo vũ khí (V4).
- Kỹ năng Cào lướt (K4); Cục tẩy bóc tinh anh.
- Kiểu màn Đuổi trộm, Cầm cự tính giờ, Dọn nhiễm, Liên hoàn trùm (T2–T5).
- Mốc thưởng Vô tận đợt 60/75/100 (E3); khoảng 12 thẻ mới chia đều 4 nhóm (J2).
- Mèo nói tiếng người (docs/NEXT-STEPS.md), cần đổi quy tắc AGENTS.md.
- **Chuẩn bị code cho mở rộng:** đã dùng chung thứ tự app độc và thêm test bắt lệch tên/trọng số/Sổ tay. Còn sau 1.5: kiểm tra tự động khóa quái mới thiếu trong `UsesMouse`/điều kiện loại trừ, xem xét gom `Scenes` thành dữ liệu; không refactor rộng trước release.
- Kho ý tưởng bản sau: docs/UPDATE-1.5.1.md (chưa chốt, không phải cam kết).
