# Design System — Cozy Desktop Adventure

Chuẩn thiết kế project, được người dùng duyệt ngày 2026-10-24. Chủ đề: **Một thế giới nhỏ trên bàn làm việc**.

> Mèo là bạn đồng hành sống trên desktop; cửa sổ, đồ vật và những sự cố máy tính trở thành chất liệu cho cuộc phiêu lưu của nó.

## Trạng thái và nguồn chuẩn

Đây là chuẩn cho thiết kế hiện tại và tính năng tương lai, không xác nhận mọi màn hình đã được rà trực quan. Nền có sẵn: PetUi, PetControlStyle, PetMenuStyle và PetTrayMenuStyle. Mốc này thiết lập tài liệu, không đổi rendering/gameplay. Những chuẩn mới bên dưới được áp dụng dần khi sửa đúng thành phần; không yêu cầu viết lại UI.

- Token màu/font/cỡ chữ/bo góc thực thi: [PetUi.cs](../src/DesktopPet/PetUi.cs).
- Control: [PetControlStyle.cs](../src/DesktopPet/PetControlStyle.cs); menu: [PetMenuStyle.cs](../src/DesktopPet/PetMenuStyle.cs), [PetTrayMenuStyle.cs](../src/DesktopPet/PetTrayMenuStyle.cs).
- Quy tắc hành vi/ngoại lệ mới nhất: [AGENTS.md](../AGENTS.md). Theme không thay sleep lock, input, combat, pause, physics hoặc quy tắc đã duyệt.
- Khi thêm token, cập nhật code và tài liệu trong cùng mốc. Không copy bảng màu riêng cho mỗi cửa sổ.

## Ba đặc tính

**Ấm áp:** chữ rõ, màu dịu, mèo đồng hành không gây áp lực phải chăm liên tục.
**Tinh nghịch:** hình tròn, đồ vật nảy, biểu cảm và sự cố desktop hài hước có nhịp nghỉ.
**Phiêu lưu:** trang bị, khám phá, chiến đấu, mang kỷ vật về nơi sống.

Cozy là cách thể hiện, không bắt combat dễ. Cảnh báo phải dễ đọc dù boss/elite mạnh. Không biến app thành giao diện neon/cyberpunk hoặc fantasy nặng tách khỏi mèo.

## Palette đang dùng

| Token PetUi | Màu | Vai trò |
| --- | --- | --- |
| Surface | #FFF8F1 | Nền bảng kem |
| CardBack / CardHex | #FFFFFF | Thẻ nội dung |
| Ink | #4A3628 | Chữ chính, nét nâu |
| Muted | #7D6656 | Chữ phụ |
| Accent | #C2561F | Nút chính, lựa chọn |
| AccentHoverHex | #A84816 | Hover nút chính |
| AccentBright | #E8834F | Trang trí, không dùng mặc định cho chữ |
| AccentSoft | #FCE9DB | Nền nhấn nhẹ |
| AccentSofterHex | #F8D9C4 | Biến thể nền nhấn |
| Line | #EEDFD0 | Phân cách |
| ControlLineHex | #E2CDB9 | Viền control |
| Stage | #FBEFE3 | Nền preview mèo |

Vai trò semantic cần gom dần từ code hiện có: xanh lá dịu = hồi phục/nghỉ; tím = nhiễm độc/bí ẩn; đỏ san hô = nguy hiểm; vàng mật ong = elite/phần thưởng. Chưa chốt hex mới hoặc tuyên bố đã áp dụng toàn app. Phải kiểm kê màu hiện có và kiểm tra tương phản trước khi đưa thành token dùng chung.

Không mã hóa trạng thái chỉ bằng màu: stun có sao, Push bị chặn có biểu tượng/nhãn, elite có huy hiệu, lỗi có mô tả. Chữ nhỏ nhắm tương phản ít nhất 4.5:1 trên nền thật; control/focus quan trọng 3:1. Đây là tiêu chí kiểm tra, không phải chứng nhận mọi màu hiện tại đã đạt. Overlay trên wallpaper dùng nền/viền hai tông, không chỉ shadow mờ.

## Typography, khoảng cách và hình khối

Giữ font hiện có: Segoe UI, Yu Gothic UI; icon Segoe Fluent Icons, Segoe MDL2 Assets. Kiểm tra đủ dấu tiếng Việt và tiếng Nhật, không dùng emoji hệ điều hành làm art chính của quái hoặc trạng thái chiến đấu.

Cỡ hiện có (DIP): Caption 11, Small 12, Body 13, Title 14, Heading 16, Display 20. Nội dung quan trọng dùng Body trở lên; caption chỉ cho thông tin phụ, không ép chữ nhỏ để vừa panel.

Bo góc hiện có: Small 6, Card 12, Pill 16. Nhịp khoảng cách khuyến nghị 4/8/12/16/24 DIP; đây là quy ước thiết kế mới, chưa phải bộ constant đã triển khai. Co giãn/wrap/scroll theo nội dung thay vì cắt nhãn. Giữ focus bàn phím và trạng thái disabled rõ.

## Mẫu thành phần

| Thành phần | Chuẩn |
| --- | --- |
| Nút | Một hành động chính màu Accent mỗi vùng quyết định; nút phụ nhẹ; có hover/pressed/focus/disabled; nhãn là động từ rõ |
| Thẻ trang bị/nâng cấp | Hình đại diện, tên, tác dụng chính và đánh đổi; chọn có viền + dấu chọn, không chỉ màu; chỉ số là chi tiết phụ |
| HUD | HP, trạng thái và kỹ năng dễ đọc nhanh; ít văn bản lặp; các giá trị cập nhật không làm panel nhảy kích thước |
| Tooltip | Giải thích tác dụng, điều kiện và hạn chế; không che cảnh báo hoặc nút cần bấm |
| Thông báo | Nhẹ cho thông tin thường; rõ cho lỗi/nguy hiểm; không spam banner/cue; trạng thái lỗi thật không diễn đạt thành thành công |
| Kết quả | Kết quả trận và phần thưởng trước, thống kê sau; tiếp tục/chơi lại/đổi trang bị/về desktop dễ tìm theo flow đang có |
| Cài đặt/menu | Dùng style chung, văn bản thực dụng, không đổi tên chức năng thành ẩn dụ khó hiểu |

Không tự thêm confirm, màn trung gian hoặc đổi navigation chỉ để hợp theme.

## Art và desktop overlay

- Giữ vector/procedural WPF: silhouette tròn rõ ở kích thước thật, nét nâu đậm, bóng nhẹ, chi tiết vừa đủ. Không đổi sang sprite hoặc engine khác.
- Mèo là tâm điểm. Trang bị và quái dùng cùng nét, độ đơn giản và chất liệu gỗ/vải/giấy/kim loại hoạt hình.
- Vai trò slime nhận ra từ hình dáng/phụ kiện, không chỉ recolor. Elite thêm một dấu hiệu rõ, không phủ hiệu ứng che windup.
- App giả là đồ chơi hình cửa sổ/icon máy tính; giữ quy tắc không dùng app/logo thật, không đọc nội dung desktop. Phân biệt cảnh báo game với lỗi Windows thật.
- ZZZ, sao stun, số damage và dấu miễn có viền/nền tương phản. Kiểm tra thứ tự lớp giữa cửa sổ mèo/chiến trường; không để thân mèo che chữ như các lỗi đã ghi ở Issue_fixxed.md.
- Art không tự đổi hitbox, input region hoặc physics. Giữ vùng trong suốt xuyên chuột và vùng tương tác đã được duyệt cho từng vật thể.

## Motion và âm thanh

Sinh hoạt: nhẹ, có anticipation và nhịp nghỉ. Tương tác: nảy/lắc có trọng lượng và giảm dần. Combat: chuẩn bị dễ đọc → impact rõ → recovery. UI: gọn, không làm cả bảng nảy liên tục; không đổi timing chiến đấu theo timing UI.

Giữ cue hoạt hình hiện có: meow cách điệu, pop/boing, gỗ/kim loại nhẹ; elite/boss có lực hơn nhưng cùng chất liệu. Không beep hệ thống hay mèo thật. Giữ volume/mute/preview/cooldown và giới hạn chồng âm hiện có. Hiệu ứng trang trí giảm được nếu gây rối; không loại bỏ cảnh báo cần thiết. Không thêm timer vĩnh viễn hay allocation mỗi frame chỉ cho trang trí; reuse/freeze tài nguyên theo hệ hiện có.

## Một thế giới, nhiều chế độ

| Khu vực | Cách thể hiện |
| --- | --- |
| Pet Mode | Cuộc sống thường ngày trên bàn làm việc |
| Sảnh | Góc chuẩn bị của nhà thám hiểm nhỏ |
| Adventure | Các câu chuyện trong thế giới desktop |
| Viễn chinh | Chọn đường, khám phá, thu thập đồ lạ |
| Vô tận | Giữ desktop trước những đợt hỗn loạn |
| Kỷ vật/nhà mèo tương lai | Dấu vết chuyến đi và nơi sinh hoạt |

Vùng tương lai có thể là Khu vườn cửa sổ, Kho lưu trữ thất lạc, Xưởng đồ chơi. Đây là ví dụ chủ đề, chưa là roadmap được giao. Không bắt người chỉ dùng Pet Mode phải chiến đấu, không đổi tên mode hoặc thêm gameplay trong một mốc theme.

## Checklist cho người/AI phát triển

1. Đọc chuẩn này và AGENTS; tìm thành phần/token có sẵn trước khi tạo style mới.
2. Giải thích phạm vi UI cần đổi; tách visual khỏi gameplay, giữ thay đổi chưa commit của người khác.
3. Kiểm tra normal/hover/pressed/focus/disabled/selected, nhãn vi/en/ja dài, overflow và keyboard.
4. Với overlay: thử nền sáng/tối/nhiều màu, kích thước thật, DPI 100/125/150/200%, nhiều màn hình và z-order. Không báo đã kiểm tra nếu chưa làm.
5. Kiểm tra pause/fullscreen/drag/sleep khi động đến animation; không tự sửa các ngoại lệ mới theo ý theme.
6. Ghi ảnh trước/sau và kết quả thật khi thay visual; build/test phù hợp và quy trình publish theo AGENTS. Tài liệu-only không publish binary.

## Việc tiếp theo để chuẩn hóa dần

- Kiểm kê UI thực tế: sảnh, prep, thẻ, kết quả, settings, menu, HUD, app giả và kỷ vật; đánh dấu đã dùng token hoặc còn hardcode, chụp ảnh nền sáng/tối.
- Gom semantic colors sau khi kiểm tra tương phản; không đổi các màu báo trạng thái đồng loạt thiếu đối chiếu.
- Chuẩn hóa lần lượt từng thành phần đang được sửa, giữ layout/input và sự dễ đọc. Chưa có cuộc rà trực quan toàn app trong mốc tài liệu này.
