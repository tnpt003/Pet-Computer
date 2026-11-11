# Chơi chung qua Internet (Multiplayer)

Trạng thái: **hướng đã duyệt 11/11/2026, chưa code.** Luật trận hai mèo, giao diện và thời điểm làm còn chờ duyệt (mục 7). Ngoại lệ tóm tắt ở [APPROVED-EXCEPTIONS.md](APPROVED-EXCEPTIONS.md) → Chơi chung qua Internet.

## 1. Mục tiêu người dùng chốt

- Mèo của người khác **ghé thăm** desktop mình và **chiến đấu chung** trong Battle Mode.
- **Chơi cùng lúc thật** (không phải mèo do máy điều khiển), bạn bè ở khác nhà nên phải qua Internet.
- Kết nối **bấm một phát** và **không mất phí**: không thuê server, bạn bè không phải cài thêm app hay tạo tài khoản.

## 2. Công nghệ: Epic Online Services (EOS)

Đã so sánh: kết nối thẳng bằng IP (hỏng vì NAT nhà mạng), mạng ảo Tailscale/ZeroTier (bạn bè phải cài thêm), server relay tự thuê (tốn phí), Steam (phí lên Steam), Cloudflare Workers miễn phí (có trần tin nhắn/ngày). Chọn **EOS**.

| Dùng | Để làm gì |
|---|---|
| Connect + Device ID | Mỗi máy tự có danh tính ẩn (Product User ID), người chơi không cần tài khoản Epic |
| Lobbies | Tạo phòng, vào phòng bằng mã; tối đa 64 người/phòng, mỗi người 30 lần vào phòng/phút |
| P2P | Gửi thao tác và trạng thái trận; thử nối thẳng trước, không được thì qua relay của Epic |

Đã xác nhận (nguồn chính thức, 11/11/2026):
- Miễn phí: "no royalty or hosting fees, no cap on the number of titles, and no limit on player accounts" (onlineservices.epicgames.com/licensing).
- Connect, Lobbies, Peer-to-Peer thuộc *Service Addendum for Standard Services*; mô tả P2P ghi kết nối "may be routed through a proxy backend", tức relay nằm trong dịch vụ chuẩn.
- `EOS_ERelayControl`: `AllowRelays` (mặc định, relay chỉ khi nối thẳng thất bại), `ForceRelays` (luôn qua relay, giấu IP người chơi, trễ hơn).
- Có SDK C# và hướng dẫn chính thức bằng WPF (viết cho .NET Core 3.1, phải chỉnh cho .NET 10).

Chưa xác nhận, mốc 0 phải đo:
- Độ trễ thật ở Việt Nam, nhất là khi phải đi relay (bài bên thứ ba nói relay miễn phí có ít địa điểm).
- Kích thước gói tối đa (bên thứ ba ghi ~1170 byte).
- Device ID trên Windows có ổn định không (vài báo lỗi cũ trên Unreal/Quest).

## 3. Thiết lập trên Epic (người dùng đã làm 11/11)

- Organization, Product "Pet Desktop", Sandbox Live, Deployment "Live Deployment".
- Client `PetDesktopClient`, policy loại **Peer2Peer** (có Lobbies; Connect và P2P không bị policy giới hạn, mốc 0 kiểm lại). IP allow list và Redirect URL để trống. Không bật Epic Account Services.
- ProductId, SandboxId, DeploymentId, ClientId không phải bí mật. **ClientSecret không gửi vào chat và không vào git.**

## 4. Điều khoản EOS phải giữ

Đọc Developer Agreement (15/02/2024) và Standard Services Addendum (17/06/2025) ngày 11/11/2026. Đây là cách đọc của dự án, không phải tư vấn pháp lý.

- **SDK không vào git (3.1c).** Chỉ được phân phối "Distributable Code" dạng đã biên dịch, nằm trong game. Mã C# và DLL của SDK để ở `.tools/eos-sdk/` (đã gitignore). Không có SDK thì build bỏ phần chơi chung, app vẫn chạy.
- **Mã client không vào git (3.3.2).** Để ở `secrets/eos.local.json` (gitignore); khi build thì nhúng vào bản phát hành.
- **EULA (3.1c).** Bản phát hành phải kèm thỏa thuận người dùng có câu miễn trừ mọi bảo đảm cho thành phần của Epic. License MIT của repo chỉ miễn trừ cho code dự án.
- **ThirdPartyNotices (6.5).** Kèm thư mục ThirdPartyNotices của SDK trong bản phát hành.
- **License repo.** MIT hợp lệ; cấm kết hợp với license kiểu GPL bắt SDK theo luật khác (4.1c).
- **Không cờ bạc.** "Video Game" không gồm sản phẩm cờ bạc: gacha trong game không bao giờ gắn với tiền thật.
- **Dữ liệu.** SDK gửi số liệu chất lượng dịch vụ (mã phiên ngẫu nhiên, số lần gọi, độ trễ, mã lỗi; 6.6); dữ liệu có thể xử lý ở Mỹ (11).
- **Privacy Policy.** Epic chỉ bắt buộc khi dùng Epic Account Services (dự án không dùng). Dự án vẫn làm một trang "Quyền riêng tư" ngắn trên website để minh bạch.
- **Tuổi.** Chỉ người đăng ký nhà phát triển phải từ 13 tuổi; giới hạn trẻ dưới 13 chỉ thuộc Epic Account Services.
- **Duy trì.** Không gọi dịch vụ 1 năm thì Epic có thể chấm dứt (2.3, 10.2); lên SDK mới trong 3 năm (5.1); Epic báo trước 3 năm mới ngừng dịch vụ Live (5.2). Voice là Preview, không dùng.

## 5. Nguyên tắc thiết kế

- **Mạng chỉ bật khi người dùng bấm** "Mời bạn" hoặc "Vào phòng" (hoặc mở link mời). Lúc thường app không khởi tạo EOS, không gọi mạng.
- **Chỉ gửi:** mã phòng, phiên bản game, tên và ngoại hình mèo, thao tác và trạng thái trận. **Không gửi:** tên người dùng Windows, nội dung hay danh sách cửa sổ, ảnh wallpaper, file, dữ liệu nông trại hay preferences.
- **C# lo toàn bộ mạng.** Python không tham gia, giống luật Battle Mode hiện có. IPC JSON-lines giữ nguyên.
- **Máy chủ phòng (host) chạy trận thật** (`BattleSim` có seed); máy khách gửi thao tác, nhận trạng thái để vẽ. Mỗi người thấy sân trận trên màn hình của mình.
- **Lớp đường truyền thay được.** Code game gọi qua một giao diện truyền tin; EOS là một bản cài đặt, sau này đổi được nếu điều khoản Epic thay đổi.
- **Mất mạng hay Epic lỗi** chỉ tắt phần chơi chung; mèo, trận một người, nông trại chạy như cũ.
- **Bất biến mèo giữ nguyên:** mèo khách không đánh thức mèo chính, không phá khóa ngủ, kéo/ném, ưu tiên cảm xúc, Pause; FPS active ~60.
- **Link mời** `desktoppet://join/<mã>` cần đăng ký giao thức trong HKCU: thuộc mốc riêng, duyệt khi tới.

## 6. Kế hoạch mốc (đề xuất)

| Mốc | Nội dung | Tiêu chí xong |
|---|---|---|
| **M0 Bản thử kết nối** | Chương trình console riêng ở `tools/EosProbe/`, không vào app: Device ID login, tạo phòng có mã 6 ký tự, tìm và vào phòng bằng mã, gửi ping P2P | Hai máy khác mạng (một máy dùng 4G) vào được phòng; đo độ trễ khi nối thẳng và khi `ForceRelays`; ghi số đo vào file này |
| M1 Lớp mạng trong app | Giao diện truyền tin + bản EOS, build tùy chọn khi có SDK; nút "Mời bạn"/"Vào phòng" | Bật/tắt mạng không làm đổi hành vi mèo; test tự động với đường truyền giả |
| M2 Mèo bạn ghé thăm | Mèo bạn hiện trên desktop mình theo trạng thái gửi về | Luật ghé thăm được duyệt; giữ mọi bất biến mèo |
| M3 Trận hai mèo | Chế độ "Trận chung" riêng (mục 6.1): `BattleSim` nhận thêm mèo thứ hai chỉ trong chế độ này, host gửi trạng thái, luật quái nhắm, độ khó, rớt mạng | Luật Trận chung được duyệt; các chế độ một người cho kết quả y như cũ (test hồi quy cùng seed); chơi thử thật hai máy |
| M4 Phát hành | EULA, ThirdPartyNotices, trang Quyền riêng tư, publish kèm DLL, link mời | Smoke bản phát hành; kiểm không có SDK/mã client trong git |

### 6.0 M0 — trạng thái 11/11

- Code: `tools/EosProbe/` (console .NET 10, không thuộc app). SDK C# 1.19.2.1 ở `.tools/eos-sdk/`, mã ở `secrets/eos.local.json`, cả hai ngoài git. Build: `.tools\dotnet\dotnet.exe build tools\EosProbe\EosProbe.csproj -c Release`. Gói tự chạy cho máy thứ hai: `dotnet publish ... --self-contained -p:PublishSingleFile=true -o artifacts\eos-probe` rồi chép `eos.local.json` vào; ZIP `artifacts\eos-probe.zip` (~40 MB, có mã client: chỉ gửi người tin cậy, không đăng công khai).
- Phòng dùng LobbyId tự đặt `petcoop-probe-<mã 6 ký tự>` (bỏ chữ dễ nhầm I, L, O, 0, 1) với `EnableJoinById`, vào thẳng bằng `JoinLobbyById`, không cần tìm kiếm. Tối đa 2 người, host không chuyển.
- **Đã chạy thật trên máy dev:** Device ID login thành công (policy Peer2Peer đủ quyền), tạo phòng thành công, vào phòng mã sai báo `NotFound` rõ ràng, bản đóng gói tự chạy được. NAT máy dev: **Strict** → nối thẳng khó thành công, nhiều khả năng phải đi relay.
- **Đo lần 1 hai máy (11/11, máy thứ hai điều khiển qua Parsec; người dùng chưa xác nhận hai máy có khác mạng không):** cả hai NAT Strict, cả hai lượt đều **RelayedConnection**, mất 0/40, vào phòng ~1,3 s. Cho nối thẳng: min 14,9 / median 31 ms. Ép relay: min 48,1 / median 63 ms. p90/max 1,3–2,2 s là lỗi cách đo (ping gửi lúc đang mở kết nối bị SDK xếp hàng); bản thử đã sửa chỉ đo ping sau khi kết nối, cần đo lại để có p90 thật.
- **Cách đo:** ping giữa hai máy khác mạng (một máy không tự ping chính mình được vì cùng Device ID). Cách đo: máy A chạy `host.cmd`, máy B chạy `join.cmd` và nhập mã; mỗi bên đo hai lần (cho nối thẳng, ép relay). Ghi kết quả median/p90 và loại kết nối (Direct/Relayed) vào đây.

### 6.1 Người dùng chốt 11/11

- **Trận chung là chế độ riêng**, tách khỏi Phiêu lưu, Viễn chinh, Vô tận và trận tự do. Các chế độ một người không đổi luật, số liệu hay cân bằng.
- **2 người co-op trước.** Lobby EOS cho tới 64 người, nhưng game giới hạn 2; code mạng không khóa cứng số 2 để sau này cân nhắc tối đa 4.

## 7. Còn chờ người dùng quyết

1. Thời điểm: làm sau khi xong 1.6 (kế hoạch 1.6 không gồm "Nhiều mèo"), hay chen M0 vào trước. M0 độc lập, không đụng app.
2. Luật Trận chung: dạng trận (sóng quái, boss riêng hay mục tiêu khác), quái nhắm ai, hồi sinh/cứu bạn, một người rớt mạng thì trận tiếp hay dừng, mỗi mèo mang đồ/thẻ của mình hay dùng bộ chung, phần thưởng có về tiến trình một người không.
3. Mèo ghé thăm làm được gì trên desktop chủ nhà, có lúc ngoài trận không, chủ nhà có kéo/ném được mèo khách không.
4. Dùng `AllowRelays` (nhanh hơn, hai máy có thể thấy IP nhau) hay `ForceRelays` (giấu IP, trễ hơn); quyết sau khi có số đo M0.
