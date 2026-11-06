# Phân bố nội dung 1.5 — Viễn chinh và Vô tận (#70)

Phạm vi người dùng duyệt 07/11: rà và sắp xếp slime, đội hình, boss, app độc, thẻ và các hệ mới của Update 1.5. Nối tiếp #69; không đổi đòn, HP, sát thương hay câu đố boss. Dữ liệu thật: `src/DesktopPet/Assets/battle.json`.

## Viễn chinh

Đường mòn Slime vẫn là tuyến cơ bản, mở sau Vua chương 1; Lối Bãi rác mở sau ZIP. Chọn 5 kỹ năng trước chuyến, đổi miễn phí tại điểm nghỉ; bộ app tối đa 5 giữ suốt chuyến. Không ép mang kỹ năng mới hay thêm icon ngoài bộ chọn.

| Vùng Bãi rác | Slime và đội hình chính | App độc được rút |
|---|---|---|
| Đầm nhầy, bậc 1–2 | Phun/lò xo; băng/nhím/dỗi; tách đôi + thảo dược. Ổ Mẹ tùy chọn | Bốn loại cũ + CAPTCHA |
| Kho cá, bậc 3–4 | Giữ cá, Báo động, bom cưỡi, bay, đào hang/hợp thể; tinh anh Quân cảm tử | Thêm Pin yếu |
| Mê cung thư mục, bậc 5–6 | Bóng ma/ảo ảnh, nhạc trưởng rồi xích/đá, Pháo kích; cờ hiệu, Pháo đài/Kỵ binh; ổ Biến dị tùy chọn threat 0,7 | Thêm Đơ con trỏ |
| Cuối tuyến | Bài mở đầu bong bóng/bám tường, sau đó ZIP ba phase | Đủ bảy loại |

Mỗi trận Bãi rác vẫn tối đa 4 app độc. CAPTCHA vẫn không ra khi còn slime bay. Screenshot có từ đầu tuyến vì người chơi đã qua chương 2; lọc app độc riêng bằng `malwareKinds`, không tắt cả nhóm app mới. Tổng roster tuyến có đủ 19 loại slime thường mới của 1.5, tính cả thành viên đội hình; không gom tất cả vào một trận. Tổ Mẹ, ổ Biến dị và ZIP giữ chỉ số/threat đã có.

## Vô tận

| Đợt thường trong vòng đầu | Pool đội hình | App độc mới |
|---|---|---|
| 1–14 | Từ đợt 8, cứ đợt chia hết cho 3: Pháo đài/Bạt nhún | Chưa có |
| 15–19, Đầm nhầy | Pháo đài/Bạt nhún/Bầy nhập | CAPTCHA; Screenshot nếu thuộc bộ app |
| 21–29, Ban nhạc | Đội trống/Dây chuyền | Thêm Pin yếu |
| 31–39, Bãi rác | Kỵ binh/Quân cảm tử/Bầy nhập/Báo động | Thêm Đơ con trỏ |
| 41–49, Mê cung | Pháo đài/Pháo kích | Đủ ba loại mới |

Từ đợt 51 bốn chặng lặp lại, cả pool quái, đội hình và app độc đi theo chặng. Giữ trọng số quái lẻ, nhịp một đội hình mỗi 3 đợt, threat, tinh anh và số đợt chọn thẻ. Đủ cả 19 slime thường mới và 9 đội hình trong một chu kỳ nội dung; Mê cung không có Báo động vì sẽ thêm bóng ma bên cạnh ảo ảnh và dỗi, vượt giới hạn hai loại dùng chuột.

Boss giữ vòng **10 Vua chương 1 → 20 ZIP → 30 Mẹ → 40 Biến dị**, sau đó lặp; Vua bản thường đi cùng từ đợt 50. Đột biến vẫn bắt buộc chọn 1/3 sau boss, thưởng khi qua boss kế. Phần thưởng đợt 10/20/30/40/50 giữ nguyên; mốc 60/75/100 tiếp tục ở backlog sau 1.5.

## Thẻ, di vật và sự kiện

- Nhóm Mưu mẹo có 14 thẻ; cộng hưởng 3 điểm ở các chế độ, 6/9 chỉ Vô tận. Giới hạn cộng dồn riêng Vô tận và các nhóm còn lại giữ nguyên.
- Sửa viên vàng: thẻ hiếm gắn `skill` chỉ rút khi kỹ năng nằm trong bộ mang, cùng bộ lọc với cửa hàng/Lời nguyền; vẫn tôn trọng giới hạn cộng dồn. Không còn kỹ năng phù hợp thì bỏ nhánh thưởng đó, dùng các nhánh hợp lệ còn lại hoặc hồi máu.
- Sửa lượt mời thẻ ngoài trận: điểm nghỉ, đổi thẻ, Mèo bị lạc, cửa hàng và viên thưởng dùng đúng ngữ cảnh Viễn chinh có đồ rơi/xu và công tắc app. Thẻ tăng đồ rơi/hồi cá có thể xuất hiện; thẻ sạc app chỉ có khi bật app. Thẻ gắn kỹ năng vẫn lọc theo bộ mang.
- Sáu di vật mới (Nam châm xu, Vòng gai, Răng nanh độc, Viên đá lạnh, Pin dự phòng, Mũ bảo hiểm), ba sự kiện mới (Lò rèn, Lời nguyền, Mèo bị lạc), cửa hàng, xu và đồ rơi đã được nối vào cả hai tuyến. Không thêm loại hay đổi giá/chỉ số trong mốc này.

## Dữ liệu tùy chỉnh và chuyến cũ

`endless.eras[].formations` ghi đè pool đội hình trong chặng; bỏ khóa/null thì dùng `endless.formations`. `endless.eras[].malwareKinds` lọc app độc ngẫu nhiên trong chặng; bỏ khóa/null thì dùng pool màn hoặc luật rút cũ. `newAppsFrom` vẫn là cổng mở app mới. Validator kiểm tra ID/trọng số/độ lớn đội, cùng giới hạn liên kết/chuột trên cả pool quái lẻ và thành viên đội hình. Dữ liệu không có hai khóa mới vẫn dùng fallback cũ.

Kỷ lục mới: Vô tận r26; Đường mòn r9 (`keepRuns` 6/7/8); Bãi rác r19 (`keepRuns` 1/2/3/4/17/18). Chuyến và lựa chọn đã rút được giữ; bậc chưa rút dùng luật hiện hành, không cam kết replay luật cũ. Không ghi vào preferences/expedition thật khi test.

## Kiểm tra

Test hồi quy viên vàng đã FAIL với bộ lọc cũ (39 lượt không mang Laser), PASS sau sửa; có test kiểm tra trường hợp mang Laser. Có test thẻ app/đồ rơi ngoài trận, pool theo vùng/chặng, đủ roster, fallback và từ chối đội hình vượt giới hạn chuột. Kết quả bộ đầy đủ, đối chứng mô phỏng và publish ghi ở Issue_fixxed #70; chơi thật và DPI/nhiều màn hình còn cần người dùng kiểm tra.
