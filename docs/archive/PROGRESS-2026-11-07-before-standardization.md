# Các mốc 06–07/11 trước rà soát chuẩn hóa 1.5

Chuyển từ PROGRESS ngày 07/11; giữ kết quả theo từng commit. Test tay chưa hoàn tất vẫn theo MANUAL-TEST và REVIEW-1.5-2026-11-07.

- **Slime Mẹ polish (#68,07/11, code/publish4557dbd):** pose dùng chung thân/nguồn phát, gồng-phát-thu riêng9đòn, vận tốc hình ảnh và chuyển mech mượt; tay thạch nối vai, nơ vải/dải quạt, trứng/vỏ/vũng hồng kem, core bụng mở và nôi rung. Chỉ thêm metadata animation, không đổi vị trí/hitbox/timing/damage/HP/seed/rulesVersion. FullPython58+i18n5/C#3757/WPF1541 đạt; visual cuối1541đạt sau review. Chuỗi9đòn×6frame sáng/tối; snapshot render cuối median6,09/p9512,13/max14,03ms (không phảiFPS). Đối chứng409dòng mô phỏng giống#67. Publish sạch4557dbd/smoke3/3 PASS; ZIP artifacts/DesktopPet-1.4.4-4557dbd-win-x64.zip. Crowd-late33s median49.3/min34.9FPS,gap p9577/max144ms, chưa đo FPS boss thật. Đã mở lại mèo; chưa chơi thật/DPI/multi-monitor.


- **Slime biến dị cơ thể/action (#66, 06/11, code/publish38c8be7):** core trong khoang/vỏ tách có đáy nối, mở0,35s trước hit/khép trước trượt; pose nạp/phát/thu/chung seed/màng/muzzle, xoắn mắt rõ. Bỏ nhãn số, icon cạnh thân tên. Phase2–3 tới mép chờ5s rồi đổi; Truy sát khóa đúng bốn biên và kiểm chỗ né toàn thân. Records23/16, ZIP14. FullPython58+i18n5/C#3709/WPF1482 đạt; cuối core331/WPF1482 đạt;40ảnh mutant-cohesion-render, đã xem core/guide/action sáng-tối. Smoke3/3 PASS; ZIP artifacts/DesktopPet-1.4.4-38c8be7-win-x64.zip. Crowd-late27s median55,5/min32,5FPS, gapmax156ms, chưa đo boss thật. Chưa chơi tay/DPI/multi-monitor/duyệt mỹ thuật-cân bằng; bot chưa giải đủ boss. Luật docs/MUTANT-SLIME.md #66, test tay MANUAL-TEST #66.

- **Slime Mẹ mới (#67,06–07/11, code/publish7712dc3):** code đủ hình vector/thân thạch6trứng/nơ,3phase/9đòn,Quấn khăn,Trứng treo40s/Trả con45s,Thanh dỗ33/66/100 và Trứng cuối bắt buộc. Chặn nguồn damage/debuff mech; đạn quét cả mèo/đạn; HUD/guide/Sổ tay vi/en/ja,bot biết giải. HP650→600,damage26/threat/ngưỡng test giữ; records Đầm nhầy6/Bãi rác17/Vô tận24. Python58+i18n5 đạt; fullC#3748đạt/9FAIL đã sửa, kiểm lại Mother/balance340+metadata232+Tooling24 đạt; WPF1496 đạt. Đối chứng BALANCE-MOTHER-2026-11-06.md; Búa Bãi rác18/40 sát ngưỡng. Publish sạch7712dc3, smoke3/3 PASS; ZIP artifacts/DesktopPet-1.4.4-7712dc3-win-x64.zip. Luật docs/MOTHER-SLIME.md. Đã xem render sáng/tối,37ảnh mother. Crowd-late33s median54,6/min32,1FPS,gapmax142ms,CPUavg2,9%; chưa đo boss thật; chưa chơi tay/DPI/nhiều màn hình.

- **Chỉ Kéo cắt đứt dây (#64, 06/11, code6d93cf4, gộp publish117420b ở #65):** mèo không còn cắt dây nào; Đầm nhầy cho mượn Kéo cắt, Slime Nén cho mượn Bình xịt; records Đầm nhầy5/Slime Nén14/Vô tận22/Bãi rác15. Python58+5, C#3692, WPF1467 đạt, ngưỡng cân bằng đạt; chưa so bảng trước/sau vì thiếu log đối chứng, so ở mốc cân bằng Slime Mẹ. Chi tiết Issue_fixxed #64, test tay MANUAL-TEST #64.

- **Bom cuối ZIP (#60):** publish9fcbed2/smoke3/3; 80%maxHP/rơi không nảy/stun5 sau đáp/reset6 tay. ZIP436/WPF1448/cân bằng cùng bot293 đạt; full3 FAIL cũ đã xử lý. Chi tiết Issue_fixxed #60; crowd-late48,6 FPS cần profile, chưa đo burst thật.

- **Cây/đẩy ZIP (#59):** publish048862f, smoke3/3; cây toàn chiều cao, đẩy5/12 s. WPF1439/core liên quan562+Tooling24 đạt,6 FAIL full đã xử lý. Chi tiết Issue_fixxed #59, test tay MANUAL-TEST #59/DPI.

- **Đai/Khóa ZIP (#58):** publish3b3682a, smoke3/3; đai damage tiếp xúc, tự phá khóa không cần quái. C#3467/Python58+5/WPF1437/ZIP392 đạt. Chi tiết Issue_fixxed #58; test tay MANUAL-TEST #58/DPI.

- **ZIP sau test (#57):** publish57c3055, smoke3/3, mở lại mèo. File mech tránh boss/mép và bom; giấy bám toàn thân bế/bay, hiệu ứng riêng. C#3451/Python58+5/WPF1433, ZIP432/cân bằng293 đạt. Chi tiết Issue_fixxed #57/BALANCE-ZIP; còn chơi thật/DPI theo MANUAL-TEST #57.

