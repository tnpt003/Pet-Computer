# Website tải game

- Tên hiển thị: **Chilo — Desktop Adventure** (Desktop Pet là tên ứng dụng/repo).
- URL: https://tnpt003.github.io/Pet-Computer/
- Hosting: GitHub Pages, nguồn GitHub Actions; workflow `.github/workflows/pages.yml` chỉ chạy khi phần website/media liên quan đổi hoặc chạy thủ công. Không tạo tag/release game.
- Source: `website/index.html`, `website/style.css`; không dependency, JavaScript, analytics hay tài khoản.
- Chạy `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-website.ps1` để đóng gói vào `artifacts/website`. Script dùng chung trên Windows và PowerShell của runner Ubuntu; không sửa preferences hoặc binary game.
- Media dùng lại từ `docs/media` khi đóng gói, không nhân đôi file trong git. Nguồn/quyền media và giới hạn bản preview: `docs/media/README.md`.
- Nút tải trỏ đến `releases/latest/download/PetComputer-win-Setup.exe`; ZIP qua trang Releases. Giữ tên asset này khi phát hành mới. Ảnh/video 1.5 được đánh dấu bản phát triển, không gắn nhãn là nội dung của latest Release.
- Palette CSS chuyển từ token trong `docs/DESIGN-SYSTEM.md`: Surface, Stage, Ink, Muted, Accent, AccentText, nền game tối; HTML không dùng trực tiếp control WPF.

## Đổi tên miền

Đề xuất: `chiloadventure.com`, hoặc `chilodesktop.com`; chưa kiểm tra còn trống và chưa đăng ký. Người dùng cần sở hữu tên miền trước khi cấu hình. Không đổi tên repo hay tài khoản GitHub chỉ để đổi tên hiển thị.

Sau khi có tên miền: xác minh quyền sở hữu với GitHub, thêm custom domain trong Settings → Pages, cấu hình DNS theo GitHub, chờ HTTPS rồi kiểm tra redirect/link tải. Website dùng đường dẫn asset tương đối để dùng được cả dưới `/Pet-Computer/` lẫn tại gốc domain mới.

Tài liệu: https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/about-custom-domains-and-github-pages

## Kiểm tra tay

Xem bố cục desktop/mobile, điều hướng bàn phím, mở video và bấm tải trên trình duyệt thật. Nút tải khởi động tải ứng dụng Windows, không chơi ngay trên web. Website không đổi sleep/input/combat/Python hoặc dữ liệu người chơi.
