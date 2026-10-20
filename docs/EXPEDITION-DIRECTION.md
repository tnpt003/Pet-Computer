# Hướng mở rộng lớn — Viễn chinh và góc chiến lợi phẩm

Ngày bàn giao: 2026-10-20. Trạng thái: người dùng đồng ý đưa hướng này cho Claude đánh giá; chưa phải yêu cầu code ngay hoặc phê duyệt toàn bộ chi tiết thiết kế. Đọc AGENTS.md, PROGRESS.md, NEXT-STEPS.md và code thật trước khi đề xuất tích hợp.

## Mục tiêu sản phẩm

Tạo lý do chơi lại sau Adventure: chọn đường theo tình trạng mèo, xây build khác nhau qua từng chuyến đi, mang kỷ vật về desktop. Viễn chinh là phần chính; góc chiến lợi phẩm là phần nhỏ nối Battle Mode với Pet Mode. Adventure cố định và trận tự do giữ nguyên, Pet Mode dùng độc lập.

Luồng đề xuất: chọn trang bị → chọn đường → giao tranh/sự kiện/nghỉ → hoàn thiện build → boss → nhận kỷ vật → trưng bày trên desktop.

## Phạm vi bản đầu đề xuất để đánh giá

- Một chuyến đi mục tiêu 10–15 phút để thử nghiệm, chưa là con số balance chốt.
- Bản đồ nhỏ có đường rẽ, bốn loại điểm: giao tranh, tinh anh, nghỉ, sự kiện; boss cuối ban đầu tái sử dụng King.
- HP/build giữ trong chuyến đi, chuyến mới reset. Tận dụng thẻ/synergy hiện có nhưng kiểm tra luật hồi HP và chuyển màn để không áp dụng nhầm luật Adventure.
- Lựa chọn có thông tin: đường tinh anh rủi ro cao đổi phần thưởng tốt; nghỉ hồi HP nhưng bỏ cơ hội nâng cấp; sự kiện có thể đổi một thẻ để lấy lựa chọn khác. Tỷ lệ, chi phí, bảo đảm rare và luật đổi thẻ cần Claude đề xuất, không mặc định đã duyệt.
- Gacha có kiểm soát: người chơi chọn loại rủi ro trước kết quả ngẫu nhiên; giới hạn elite/quái/thẻ, không tạo tổ hợp không thể vượt hoặc card vô tác dụng. Không thêm tiền thật.
- 3–4 di vật chỉ tồn tại trong chuyến đi, có lợi ích và đánh đổi. Dùng modifier riêng, không sửa definition chung hay tăng sức mạnh vĩnh viễn.
- Ví dụ thiết kế CHƯA CHỐT: chuông phản đòn tăng đòn sau khi chặn; túi cứu trợ thêm tác dụng cho Cá nhưng hồi chiêu dài; bùa liều lĩnh mạnh khi HP thấp. Phải kiểm tra engine hiện có hỗ trợ điều kiện nào, không tự thêm cơ chế chặn mới chỉ để khớp ví dụ.
- Nhóm kỷ vật nhỏ: cúp, phụ kiện, một đồ trưng bày. Mèo có thể ngắm/nằm cạnh/nghịch bằng animation sẵn có nếu phù hợp. Có bật/tắt trưng bày, không che công việc hoặc bắt người dùng đánh quái mới được chăm mèo.
- Lưu/tiếp tục tại điểm nghỉ giữa trận; chưa lưu giữa combat. Quy tắc thoát/crash khi đang đánh, thất bại và bỏ chuyến cần chốt trước code.

## Tích hợp kỹ thuật cần Claude kiểm chứng

- Đề xuất bộ điều phối chuyến đi ngoài BattleSim: chọn node, giữ HP/build/di vật, quyết định phần thưởng. BattleSim tiếp tục thực thi một trận. Tên lớp/schema do Claude đối chiếu code rồi đề xuất, không bắt buộc framework mới.
- Tận dụng chuẩn bị, upgrade, kết quả, enemy registry, giới hạn spawn và modifier hiện có; không sao chép engine combat hoặc nối cứng Viễn chinh vào luật mở màn Adventure.
- Tách dữ liệu phiên viễn chinh khỏi tiến trình Adventure; schema version, atomic write/backup và xử lý bản mới hơn. Không sửa save thật khi test. Quyền sở hữu kỷ vật có thể dùng hệ reward hiện tại nếu phù hợp.
- Seed và trạng thái random phải cho phép kiểm thử, không đổi offer chỉ vì đóng/mở bảng hoặc resume. Đề xuất rõ lưu kết quả đã chọn hay trạng thái RNG, tránh reroll vô tình.
- Phần thưởng chỉ cấp một lần theo run/node identity; double click/replay result/crash quanh save không nhân thưởng. Chuyển node và ghi nhận reward cần ranh giới lưu rõ.
- C# giữ combat/UI/render/input/physics/save; Python chỉ hành vi pet cấp cao, không tham gia combat. Mất Python không chặn viễn chinh.
- Giữ pause reasons, rescue/held/airborne immunity, drag/throw/dizzy, sleep lock và trở lại Pet Mode sạch. Đối chiếu bàn giao cứu mèo, không lấy bản Adventure “ổn” làm lý do bỏ lỗi còn mở.
- Cân nhắc rulesVersion/record riêng cho viễn chinh; không so kỷ lục với Adventure hoặc âm thầm đổi cân bằng màn cũ.

## Mốc triển khai đề xuất, chưa bắt đầu

1. Claude kiểm tra code và báo thiết kế tối thiểu, thành phần tái dùng, quyết định chưa chốt, file dự kiến và rủi ro. Chờ xác nhận theo AGENTS.md.
2. Một chuyến hoàn chỉnh nhỏ: chọn đường → trận cũ → chuyển HP/build → boss → kết quả, state riêng, không ảnh hưởng Adventure. Hoàn tất luật save/resume trước mở rộng nội dung.
3. Node nghỉ/sự kiện, 3–4 di vật và lựa chọn có đánh đổi; mô phỏng bot thường và tối ưu, playtest để chỉnh thời lượng/độ khó.
4. Reward một lần và góc trưng bày có thể ẩn; mèo tương tác nhẹ, không tạo hệ nhà ở lớn.
5. Kiểm thử end-to-end, save lỗi/crash/replay, DPI và hiệu năng, đóng gói theo quy tắc project.

## Tiêu chí nghiệm thu đề xuất

- Có ít nhất hai đường hợp lệ dẫn tới lựa chọn khác nhau theo HP/build; không chỉ là chọn cùng một trận với phần thưởng khác tên.
- Build/di vật giữ đúng trong chuyến, reset khi bắt đầu mới; không rò modifier sang Adventure/trận tự do/Pet Mode.
- Save/resume không reroll node/offer, không cấp trùng reward, xử lý file lỗi/bản mới hơn an toàn.
- Pause/menu/fullscreen/upgrade, cứu mèo, chết/end/replay không kẹt UI hoặc timer. Test bằng dữ liệu riêng.
- Desktop yên tĩnh khi không chơi, trưng bày tắt được, không thêm chi phí xử lý trận khi Pet Mode chạy.
- Phân biệt mô phỏng và chơi thật; báo kiểm thử thực tế, không tự coi con số 10–15 phút đã đạt.

## Ngoài phạm vi bản đầu

Nhà mèo đầy đủ, crafting/shop/currency lớn, stat vĩnh viễn, nhiều thú đồng hành, multiplayer, LLM/voice/CV, engine mới, chương 2 có môi trường/vật cản mới. Các hướng này để sau khi vòng chơi viễn chinh được kiểm chứng.

Claude cần trả lời: hướng này tận dụng được gì; phần nào đã có; thiết kế nào còn thiếu; MVP nhỏ nhất mang lại vòng chơi đầy đủ; đánh đổi/rủi ro và thứ tự thực hiện. Không triển khai tất cả backlog chỉ vì được liệt kê ở đây.
