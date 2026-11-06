# Lỗi đã sửa

Sổ ghi các lỗi đã sửa của Desktop Pet. Phần A: lỗi người dùng phát hiện khi dùng thật (đánh số theo thời gian, mục mới ở trên). Phần B: lỗi do test, mô phỏng hoặc review tìm ra trong lúc làm. Chi tiết đầy đủ ở PROGRESS.md / docs/archive/PROGRESS-2026-10.md.

Mỗi mục: triệu chứng, nguyên nhân, cách sửa, kiểm chứng. Thêm lỗi mới: tạo mục số tiếp theo ở đầu phần A (hoặc một dòng ở phần B).

# A. Lỗi người dùng phát hiện
## #64 — Mèo cắt được dây khi kéo/ném xuyên qua; chỉ Kéo cắt mới được đứt dây (06/11)
- **Yêu cầu:** bỏ tương tác kéo/ném mèo xuyên dây ở mọi slime liên quan, dây chỉ đứt bằng Kéo cắt.
- **Nguyên nhân:** `CatThroughRopes` trong PetBattle.Held.cs cắt mọi loại dây khi mèo đi xuyên đủ nhanh, là luật từ M10.
- **Cách sửa:** xóa `CatThroughRopes`, `CatThroughBossRopes`, `CatRopeCuts`, `BossCordCutSpeed`, khóa `cutSpeed`/`ropeCutSpeed`; sửa câu hướng dẫn/banner/mẹo vi/en/ja. Kéo cắt vốn chỉ mở ở Slime Nén nên đổi chỗ: Đầm nhầy cho mượn Kéo cắt, Slime Nén cho mượn Bình xịt. Records: Đầm nhầy 5, Slime Nén 14, Vô tận 22, Bãi rác 15. Giữ Kẹp gắp đứt xích và ném mèo vỡ vỏ băng.
- **Kiểm chứng:** full `artifacts/m0-full.log`: Python58+i18n5, C#3692, WPF1467 đạt (lần đầu 3 FAIL là test ghim thứ tự kỹ năng/rulesVersion, đã sửa kỳ vọng). Ngưỡng cân bằng đạt; chưa so bảng trước/sau vì thiếu log đối chứng. Chưa chơi trận thật/DPI.
- **Cần test tay:** MANUAL-TEST mục Issue #64; slime xích và dây hồng khó xử lý hơn khi không mang Kéo cắt, cần chơi thật.
- **Chốt kỹ thuật:** PENDING_COMMIT

## #63 — Slime biến dị còn giống Vua; thiết kế ba phase và sân nhầy (06/11)
- **Yêu cầu:** đổi tên/bỏ vương miện, xanh lá/mắt xoắn, 9 đòn/3 mech; hazard đánh cả bế/bounce, gỡ Nhiễu nhịp bằng rê chuột; miễn debuff boss trong mech. Khi vào mech cuối phải xóa toàn bộ nền/túi trời, có visual hút về.
- **Cách sửa:** opt-in `mutant: true` tách khỏi Vua; state machine core cho 6 bướu ×3 vòng/60 s, ping pong6 và hồi/phạt rồi luôn phase3, chốt1HP/sáu nhát lõi xen ba all-in. Terrain25/50% và túi trời nhịp riêng; quét va chạm, cooldown giao dải, bảo vệ suốt rơi và sau stun để nhấc lại. Bướu tường dùng hit-time vung thật, bướu sàn không phải quái và không kích/tiêu Cú đáp địa chấn. Final entry hủy pending attack/tắt hazard trước animation co/hút2,5 s trong gào5 s. ZIP miễn status trong mech và choáng/đẩy phase2–3. vi/en/ja; records ZIP13/Endless21/Bãi rác14.
- **Kiểm chứng:** full `artifacts/mutant-full-tests.log`: Python58+i18n5 đạt; C#3681 đạt/7 FAIL (3 kỳ vọng cũ, 4 startup do sandbox). Đã sửa kỳ vọng/tables và kiểm lại có quyền: 11 suite541 đạt (`mutant-recheck.log`), test mutant mở rộng45 đạt (`mutant-contact-check.log`). WPF1461 đạt trước test ép rơi mở rộng; kết quả cuối/publish ghi sau. Không nới ngưỡng cân bằng. Ảnh thật ở artifacts/mutant-render (sáng/tối), đã xem bướu và sân phase3.
- **Cần test tay:** thao tác thả/đánh/throw toàn trận, gỡ bằng chuột, nhấc lại sau stun, thời gian phản ứng/hành lang né màn nhỏ, perf boss thật/DPI/multi-monitor. Bot cân bằng hiện chưa giải đầy đủ mech mới, không dùng kết quả tự động để khẳng định boss cân bằng khi chơi thật.
- **Chốt kỹ thuật:** code/publish `22062b1`, artifact `artifacts/DesktopPet-1.4.4-22062b1-win-x64.zip`; smoke3/3 PASS (`mutant-smoke.log`). Final battle7suite689 đạt (`mutant-final-core.log`); WPF1467 đạt gồm sáu check ép rơi qua cửa sổ thật (`mutant-ui-final.log`); i18n5 đạt sau xóa khóa cũ/sửa dấu phẩy cuối. Crowd-late28s: median56,9/min41,8FPS, gap p95 63/max140ms, tối đa19quái (`mutant-perf.log`), không phải số đo boss biến dị. Không push/tag/release remote.

## #62 — Tràn bộ nhớ cho mèo đứng gap đánh boss và bế cao né toàn bộ (06/11)
- **Nguyên nhân:** WarnBeat gộp Clamp/Overflow nên kéo boss vào ZipAimX là tâm gap. Vùng tràn70 DIP/bung170 DIP; renderer chỉ nằm trong dải sàn.
- **Sửa theo duyệt:** layout gap190 DIP lệch ngang khỏi mèo, chọn theo seed, retreat goal ngoài gap+tầm vũ khí có tính tăng kích thước/tolerance; bỏ chọn khi không đủ chỗ, hủy khi resize không còn hợp lệ. Boss rút trong gồng/giữ goal khi đánh, không thêm bất tử. Overflow.air=true, tràn và bung dùng SkyHeight. Overlay hai vách giấy/thư mục/răng ZIP, luồng file phát từ boss và ray vàng; bỏ lớp sàn cũ/impact chung. SetArea ánh xạ gap/from/goal; refresh chiều cao overlay khi đổi màn. Chi tiết ZIP-SLIME #62, MANUAL-TEST #62.
- **Kiểm chứng:** full zip62-full-tests.log Python58+i18n5 đạt, C#3686 đạt/2 FAIL cân bằng (Len16/40, careful probe3/4). Bot careful còn né bằng cao: đã cập nhật riêng nhánh dodge=true để bế ngang700 DIP/s vào hành lang; bot cân bằng chính không đổi. zip62-verified.log **780 đạt/1 FAIL** còn Len16/40 <20, không nới ngưỡng/đổi damage. WPF **1454 đạt**, zip62-ui-final.log; hai FAIL fixture visual trước đã sửa: Canvas cần invalidate lớp con để render frame mới, check glyph giấy thay hình khối đặc cũ. Đã mở ảnh full-warning/filling/peak sáng/tối; bốn vũ khí × ba vị trí boss, sàn/bế/bay, map âm/Pause/resize/narrow được kiểm. Build qua WPF; không đổi Python/IPC/physics.
- **Phát hành:** người dùng đã yêu cầu publish bản chơi thử sau #61 và xác nhận thiết kế #62; giữ FAIL cân bằng và báo rõ, không coi đạt nghiệm thu. Đã publish sạch source6ce2586, smoke3/3 PASS, mở lại mèo. ZIP artifacts/DesktopPet-1.4.4-6ce2586-win-x64.zip; zip62-publish.log/zip62-smoke.log. Perf crowd-late19 s median57/min46,8 FPS, gap p95/max141 ms, CPU avg3,27%; không phải cảnh Overflow, chưa kết luận hiệu năng overlay mới (zip62-perf.log). HP1100, records12/20/13; main8. Chưa chơi thật/DPI/multi-monitor.

## #61 — ZIP lăn về chỗ cũ; Vua ngoài chương1 thiếu phối hợp đội hình (06/11, code beecc78)
- **Theo duyệt:** ZIP lăn3 lần ngắm lại giữa lượt, báo1,1/0,6/0,6 s, giữ damage/tầm/tốc độ; king và mutant_king gọi3 đội hình/buff2 con, king_c1 giữ nguyên. Chi tiết docs/ZIP-SLIME.md, BOSS-TACTICS.md.
- **Sửa:** AimZipRoll cập nhật trong Windup, chốt khi Dash; count3/hủy theo luật đòn cũ. Sửa clock ZIP gồng lăn không tiến trước đây. Bốn bộ3-trong-4 Pháo đài/Bạt nhún/Đội trống/Pháo kích,9–10 quân/cap10 trong một frame sau báo0,6 s, theo seed; không dùng timer/bầy nhập/bomber. King thường thêm3 pha khiên75/50/25. Hai mục tiêu rally riêng, thay mục tiêu chết không trùng và ngắt cả2, hai đường vẽ hiện đúng mục tiêu; fallback basic/custom có thể không opt-in.
- **Kiểm chứng:** full m61-full-tests.log Python58+i18n5 đạt, C#3630 đạt/9 FAIL:7 test kỳ vọng cũ,2 khóa ja cũ tính chung một FAIL,1 cân bằng (các dòng lỗi cụ thể trong log); đã cập nhật kỳ vọng/dọn khóa và chạy suite liên quan. Bản cuối m61-core-final.log **1013 đạt**, build đạt; WPF **1450 đạt** (m61-ui.log), ảnh king-rally-one/two/cancelled sáng/tối đã mở. Lăn3/nhắm lại khi gồng/khóa hướng khi lao/Pause; KingFormationTests8 seed mỗi loại xác nhận đủ quân cùng frame/hai mục tiêu, mục tiêu chết chọn bù/choáng ngắt. Không sửa physics/input/sleep/Python; lưới hành vi đã chạy.
- **Còn FAIL:** m61-final-balance.log **547 đạt/1 FAIL**, Cuộn len Bãi rác tham lam Normal19/40 <20/40; giữ HP/damage/ngưỡng/bot. Đã thử bỏ tổ hợp hồi máu kép+bomber và sửa timer công khai, vẫn19/40. Phần còn lại của cân bằng đạt; ở thời điểm #61 chưa publish/smoke/perf bản mới; nay đã gộp #62 (6ce2586). Người dùng đã duyệt gộp publish với #62 (6ce2586); giữ kết quả FAIL tại thời điểm #61 để đối chứng. docs/BALANCE-ZIP-2026-11-06.md.
- **Test tay:** MANUAL-TEST #61. Chưa chơi thật/DPI/multi-monitor.

## #60 — Bom cuối ZIP né được bằng bế cao, timeout không reset tiến độ (2026-11-06)
- **Thiết kế người dùng duyệt:** phạt toàn sân 80% max HP cơ sở, 14 s mỗi vòng; bế/ném/bounce trúng thì buộc rơi không nảy, đáp mới stun5 s. Dọn mọi tay/cây, sống thì sau burst0,9 s random lại6 tay và gồng lại; đủ6 vẫn thắng. Khiên/vỏ rùa/Nộ và giảm damage giữ luật. Chi tiết docs/ZIP-SLIME.md và ngoại lệ #60.
- **Sửa:** bomb.hpShare0,8 có fallback/Validate, damage không nhân threat boss. ZipBurstLeft tách vụ nổ khỏi gồng; ArmBomb sinh lứa mới theo seed. ZipForcedFall chỉ đưa yêu cầu sang C# window, release capture/hủy pickup, VX0/VY không hướng lên, StickLanding và bỏ bounce đệm riêng trạng thái này. Đáp thật mới bắt đầu5 s, không rescue grace/quake; Nộ/end battle dọn trạng thái. Overlay procedural72 file/thư mục hút về rồi phóng ra khắp màn hình cùng răng ZIP, không chặn chuột, không dùng Slam.
- **Kiểm chứng:** full zip60-full-tests.log: Python58+i18n5 đạt, C#3510 đạt/3 FAIL cân bằng với bot cũ. Bot cũ chờ nổ bằng bế cao/đi bộ qua tay sàn, không còn hợp luật; ZipPuzzlePlayer bỏ né bom theo độ cao, bế chuyển giữa tay sàn với tốc độ700 DIP/s như file mech, đặt xuống mới đánh; khi bị force fall giả lập đáp sàn (bot không có physics cửa sổ). Không gây damage trực tiếp, đổi clock vũ khí hay nới ngưỡng. Đo lại baseline048862f và #60 với cùng bot (baseline chỉ bỏ nhánh API ZipForcedFall chưa tồn tại). ZIP436 đạt; cân bằng/record đo lại ghi docs/BALANCE-ZIP-2026-11-06.md. WPF1448 đạt, gồm bridge bế/bounce → rơi thật không nảy → stun sau đáp/cleanup; ảnh charge/burst sáng/tối đã mở trong zip60-render. Chưa chơi thật/DPI/multi-monitor. Python/IPC không đổi.
- **Phát hành:** source9fcbed2, smoke3/3 PASS, mở lại mèo. ZIP artifacts/DesktopPet-1.4.4-9fcbed2-win-x64.zip; logs zip60-publish.log/zip60-smoke.log. Cân bằng cuối3 suite293 đạt, ZIP436/WPF1448 đạt. Perf crowd-late19 s: median48,6/min35,3 FPS, gap p95/max133 ms, CPU avg3,36%; dưới mục tiêu~60, chưa quy cho thay đổi ZIP vì cảnh không có burst. Cần profile và test burst thật; zip60-perf.log. Điểm test tay docs/MANUAL-TEST.md #60.

## #59 — Mech cuối thiếu cản trở trên cao; đòn đẩy phase 2–3 ít xuất hiện (2026-11-06)
- **Nguyên nhân:** mech cuối chỉ có sáu tay và bom sàn. Đẩy hồi 22 s hợp lệ, khi ra đòn gặp mèo bế/bay vẫn tiêu hồi chiêu nên ít thấy tác dụng.
- **Sửa đã duyệt:** cây toàn chiều cao, dải 100 DIP chốt X mèo; lần đầu sau 2 s, chu kỳ 4,5 s, báo 1 s, hoạt động 0,8 s; 10 damage cơ sở một lần/cây, choáng sàn 0,4 s. Bế/bay chỉ mất máu; không chặn chuột, đủ sáu tay dọn cây ngay. Đẩy lần đầu 5 s sau chuyển phase, hồi 12 s, chỉ mở lúc mèo sàn/giữa đòn; ưu tiên trước đòn thường. Giữ né bằng nhấc trong cảnh báo, không damage/đổi physics. HP 1100, records 9/17/10.
- **Kiểm chứng:** Python 58 + i18n 5 đạt. Lượt đầy đủ zip59-full-tests.log: C# 3484 đạt/6 FAIL (test ưu tiên đẩy chưa tính ân hạn đáp, key record r9 đã đổi r10, bốn check startup bị sandbox chặn). Đã sửa hai test và chạy lại ba suite liên quan: **562 đạt**, ToolingTests ngoài sandbox **24 đạt**, không còn FAIL trong các suite đó. WPF **1439 đạt** ngoài sandbox (zip59-ui-final.log); lượt sandbox 35 FAIL input/màu, sửa màu test và lượt ngoài sandbox đều đạt. Không sửa input/sleep/drag/throw/emotion/fallback ngoài combat; lưới kiểm tra các hành vi này đã chạy. Build qua WPF; xem ảnh cây báo/hoạt động sáng/tối trong artifacts/zip59-render. Không đổi Python/IPC.
- **Cân bằng:** mọi ngưỡng đạt; đối chứng docs/BALANCE-ZIP-2026-11-06.md. Bãi rác tham lam Normal Kiếm22/Búa20/Len22/Găng28 trên40; Búa/Len còn gần ngưỡng. Đây là mô phỏng, chưa chơi thật/DPI/multi-monitor.
- **Cần test tay:** docs/MANUAL-TEST.md mục #59. Đã publish sạch source 048862f, smoke 3/3 PASS, mở lại mèo. ZIP artifacts/DesktopPet-1.4.4-048862f-win-x64.zip; log zip59-publish.log/zip59-smoke.log.

## #58 — Đai phase 1 gây damage tức thì; Khóa ZIP đứng yên không tự phá (2026-11-06)
- **Triệu chứng:** đai phủ như tường trên cả nửa sân, không thấy boss đánh ra; mèo bị Khóa ZIP xa quái đứng im tới timeout.
- **Nguyên nhân:** đai là ZipBlow đánh cả box khi hết 0,8 giây, không có đầu quét/travel; bộ đếm mở khóa chỉ tăng khi bắt đầu đánh quái trong tầm.
- **Sửa:** gồng/đai gắn với boss, đầu quét 0,35 giây kiểm đoạn tiếp xúc rộng ±12 DIP, thu 0,2 giây không damage, giữ nhịp 2,8 giây/damage8/choáng0,5. Vạch báo nhẹ, không che mèo. Self-swing ưu tiên riêng của Khóa ZIP dùng nhịp vũ khí, đếm tại HitTime, không cần mục tiêu, không ResolveHit hay tích Nộ/combo. Vũ khí quay vào ổ khóa, khóa rung/chớp/nứt. Giữ 3 click/6 giây, hủy nhát chờ khi mở/Nộ/kết thúc trận, Pause đóng băng.
- **Kiểm chứng:** ZIP mục tiêu 390 đạt; C# đầy đủ 3467, Python 58 + 5, WPF 1437 đạt. Log artifacts/zip58-target.log và zip58-full-tests.log; render artifacts/zip58-render (gồng/quét trái theo 2 khung/thu và tự đánh khóa). Lượt cuối ZIP 392 (gồm đồng bộ đường đai/marker khi đổi vùng trận/monitor-relative bằng unit test) và WPF 1437 đạt, đã mở ảnh render sáng/tối; đã publish sạch 3b3682a, smoke 3/3 PASS (artifacts/zip58-smoke.log), mở lại mèo. ZIP artifacts/DesktopPet-1.4.4-3b3682a-win-x64.zip. Đối chứng cân bằng trước/sau docs/BALANCE-ZIP-2026-11-06.md, mọi ngưỡng đạt. Chưa chơi trận thật, DPI/multi-monitor. Không nới ngưỡng hoặc hạ HP; records 8/16/9.
- **Rà render:** bút vàng tham chiếu màu ở partial class chưa khởi tạo gây mất lớp vàng; dùng palette chung trực tiếp và kiểm pixel màu. WPF cuối 1437 đạt (artifacts/zip58-ui-verified.log).
- **Cần test tay:** docs/MANUAL-TEST.md mục #58, đặc biệt Búa đủ 5 nhát trước timeout, 3 click/Nộ giữa nhát và hai hướng đai.

## #57 — File mech bị boss che/bom xóa; giấy xuyên mèo và hiệu ứng boss trùng nhau (2026-11-06, commit 57c3055)
- **Triệu chứng:** file/thư mục mech 2 đè boss khó thấy; nổ Giải nén lỗi làm file mech biến mất; giấy đi xuyên đầu/chân khi bế/bounce; nhiều đòn dùng chung sóng chấn động.
- **Nguyên nhân:** vị trí file/đích cố định không xét thân boss, `ClearPackets` gom mọi zip_packet cùng chủ; gói/giấy cũ còn sống qua chuyển mech; đạn chỉ kiểm điểm tâm và khóa hướng một lần. Renderer dùng Slam cho nhiều đòn.
- **Sửa:** làn file/đích random theo seed tránh boss cả đường đẩy, chừa mép; map thiếu chỗ boss né trước khi đặt, boss đứng tại chỗ khi giải. Dọn gói thường lọc `ZipProp.None`, vào mech dọn bom/giấy thường cùng chủ. Giấy homing tối đa 2,8 rad/s, quét hộp thân theo chuyển động tương đối ở mọi tư thế; giữ physics/choáng/Khiên/Nộ/Pause. `ZipVisual` riêng cho chín đòn, đẩy, bom và đai, thay Slam, không đổi hitbox bằng art.
- **Kiểm chứng:** C# đầy đủ 3451 đạt (artifacts/zip57-full-tests.log), ZIP 374 đạt (zip57-target-final.log), gồm 36 layout trên 480/640/1600 DIP, bom trễ không xóa file, giấy trúng đầu/thân/chân khi đứng/bế/bounce. WPF ban đầu lỗi fixture thiếu RollGoal, đã sửa mẫu cho giống trạng thái thật; lượt cuối 1433 đạt. Python 58 + 5 đạt; ZIP/slime bay cuối 432 đạt, cân bằng chạy lại 293 đạt, không còn ngưỡng FAIL. Đã xem render sáng/tối từng đòn và layout file/boss. Chưa chơi trận thật hoặc DPI/multi-monitor. HP 1100, không nới ngưỡng test; records 7/15/8.
- **Cần test tay:** docs/MANUAL-TEST.md → Issue #57. Publish sạch 57c3055, smoke 3/3 PASS (artifacts/zip57-smoke.log), đã mở lại mèo. ZIP artifacts/DesktopPet-1.4.4-57c3055-win-x64.zip; không ghi đè dữ liệu người dùng khi smoke.

## #56 — Slime Nén sau chơi thử: khóa đứng yên, mech tương tác và bom sinh tử 1 HP (2026-11-06)
- **Triệu chứng/yêu cầu:** khóa vẫn đi, hình khóa/cây/tay kéo khó nhận ra; hai line lăn; mech 1 giống đòn lăn, mech cuối lặp ngắt đòn thường. Người dùng chốt tổng 12 nhát khóa, đổi bên mỗi 3; mech ghép thư mục có cây gây damage/choáng, final chỉ ở 1 HP.
- **Nguyên nhân:** luật cũ cho khóa đi, renderer vẽ đường đi/về cùng lúc; phase cuối dùng tỷ lệ 20% và nạp lại 14 giây; mục tiêu cũ dựa vào thả đè/cắt dây/kéo xuyên.
- **Sửa:** mục tiêu Belt/File/Tab đi qua nhịp đánh sàn/trên không; chặn skill/đòn lan/physics dịch chuyển mục tiêu. Hai khóa rìa + boss nhảy giữa + đai quét; ba file ghép thư mục chừa mép + cây có cảnh báo. Chặn mọi damage ở 1 HP, sáu tay kéo một nhát, timeout giữ mục tiêu, phá hết xì hơi rồi hạ boss. Hai phase cuối có đẩy riêng 0 damage, choáng sau đẩy; Khiên/Nộ/cầm/bay/Pause đúng luật. Khóa đi/nhấc, ổ khóa lớn; 3 giấy, bom ẩn phải soi, 3 mẫu cây, bỏ line lặp. Hướng dẫn/Sổ tay vi/en/ja và rulesVersion đồng bộ.
- **Lỗi tìm khi rà:** giữ tích Nộ từ nhát trúng mục tiêu mech; file giả hết giờ không được xóa bom trước lượt nó; không khóa mèo giữa cú đẩy; nhấc/Nộ/mech hủy choáng trễ. Mô phỏng boss đơn trước đây vẫn bật app độc dù ghi không app: sửa `apps:false`; cập nhật bot giải mech bằng nhịp vũ khí và né cây/bom đã soi, không nới test. Mô tả Sổ tay phải ≤200 ký tự.
- **Kiểm chứng/publish:** source 8cd669f (code 29d83ab), build/publish đạt; C# 3246 đạt/3 FAIL cân bằng, Python 58 + 5 đạt, WPF 1431 đạt. Smoke 3/3 PASS, mở lại mèo. Đóng đúng process project bằng WM_CLOSE trước đóng gói, smoke dùng dữ liệu riêng. Người dùng yêu cầu publish bản HP 1100 sau khi đã được báo kết quả cân bằng; đây là bản chơi thử, không coi các ngưỡng cân bằng đã đạt. ZIP artifacts/DesktopPet-1.4.4-8cd669f-win-x64.zip; log artifacts/zip-publish-tests.log, zip-publish-ui.log, zip-publish.log, zip-publish-smoke.log. Đã xem render; chưa chơi thật/DPI/multi-monitor.
- **Thử HP 1000 theo duyệt của người dùng:** chỉ đổi HP, giữ bot/seed/ngưỡng; ba suite đo lại 289 đạt/4 FAIL. Boss Cuộn len 43 → 47/60 (cần 48), Bãi rác tham lam Kiếm 15 → 15/40, Búa 19 → 17/40, Cuộn len 13 → 13/40. Đã trả dữ liệu đúng HP 1100; chưa publish hoặc thay mèo đang chạy. Log artifacts/zip-hp1000-measure.log; bảng đầy đủ trong docs/BALANCE-ZIP-2026-11-06.md.
- **Cần test tay:** docs/MANUAL-TEST.md → Issue #56; đặc biệt file bị đẩy sai về mép, mèo cầm đánh tay kéo cao, cảm giác nhịp quét/đẩy và bom chạy ẩn.

## #55 — Thiết kế lại Slime Nén: túi đòn, mech, debuff, boss đánh mèo đang bế (2026-11-06, commit 01a11ad)
- **Yêu cầu (người dùng):** mỗi phase 3 đòn ngẫu nhiên và 1 mech theo HP; hai debuff Nén và Khóa ZIP; bế mèo là cách né chứ không còn miễn sát thương; chốt HP theo DPS đo được.
- **Sửa:** luật đầy đủ ở docs/ZIP-SLIME.md. Thêm `PetBattle.ZipMoves.cs`, `PetBattle.ZipDebuff.cs`, `PetBattleView.ZipMoves.cs`, quái `zip_packet`; va chạm có chiều cao cho riêng boss này; `maxHp` 900 → 1100; rulesVersion c2_zip_slime 5, Vô tận 13, Lối Bãi rác 6.
- **Tìm ra khi đo:** khoảng ×1,5 sau mech khiến build mạnh bỏ qua gần hết giai đoạn 2 (một cú 234 sát thương); thêm giới hạn `opening` 6% HP tối đa.
- **Quyết định người dùng duyệt:** áp dụng riêng Slime Nén; mèo bay dùng cùng luật va chạm; đòn trên cao vẽ ở lớp phủ màn hình; gói dữ liệu là quái mới; bỏ "3 nhát thoát Nén sớm"; boss không mở đòn mới trong lúc Khóa.
- **Kiểm chứng:** BattleZipMoveTests mới (88 check), BattleZipTests sửa, ZipVisualChecks thêm ảnh 4 dải đòn và 3 trạng thái mèo; Python 58 + 5, C# 3260, WPF 1430 đạt; publish aa627fd, smoke 3/3 PASS. ZipProbeTests báo số vòng đòn theo giai đoạn.
- **Còn lại:** giai đoạn 2 chưa đạt 1–2 vòng với build vừa và mạnh ở Normal (docs/ZIP-SLIME.md); chưa chơi tay, chưa thử DPI/nhiều màn hình thật.
- **Cần test tay:** docs/MANUAL-TEST.md, mục Slime Nén thiết kế lại 06/11.

## #54 — Điểm vàng và mũi tên trên sàn quay theo thân Slime Nén (2026-11-06, commit e6b7a44)
- **Triệu chứng (người dùng):** boss lăn thì điểm vàng và mũi tên chỉ chỗ thả mèo quay theo thân thay vì đứng yên tại vị trí đã chốt.
- **Nguyên nhân:** `DrawSlime` chạy bên trong transform xoay của boss đang lăn; mọi thứ nó vẽ, kể cả dấu trên sàn, nhãn dung lượng và chấm tiến trình, đều quay theo.
- **Sửa:** `PushUnturned` xoay ngược đúng tâm cho phần không phải thân, nên chỉ thân boss quay; thêm bù `RollLift` để dấu nằm đúng mặt sàn.
- **Kiểm chứng:** ZipVisualChecks render 7 góc quay, số pixel vàng tại điểm chốt không đổi; bỏ phần sửa thì check FAIL (438 pixel lệch). 1419 check WPF đạt.
- **Cần test tay:** xem boss lăn trong trận thật.

## #53 — Slime Nén chưa đúng chủ đề file nén: thiết kế lại boss (2026-11-06, commit 6739762)
- **Yêu cầu (người dùng):** hình boss chưa ra file nén; boss phải nhỏ lúc đầu, càng ít máu càng to; đầu nhiều đợt nhanh, giữa ít đợt hơn và đau hơn, cuối ít nhất và đau nhất; mỗi giai đoạn có một đợt mech cần giải; đánh boss phải thích thú, mưu mẹo.
- **Nguyên nhân:** thân vàng kèm vương miện chép từ Vua Slime, khóa kéo bé; kích thước cố định ×2, đòn giống nhau suốt trận; pha cuối còn co nhỏ lại, ngược chủ đề.
- **Sửa:** dữ liệu `zip` mới trong battle.json (3 giai đoạn: ×1,1/3 đòn 9, ×2/2 đòn 20, ×2,9/1 đòn 40). Mech: khóa vỏ (66%, thả mèo đè 2 lần), bung file (33%, giữ cắt dây, thêm thưởng cắt đôi), bom nén (20%, kéo tay kéo 3 nấc thì xì hơi, hết giờ thì nổ rồi nạp lại). Không giải chỉ mất thưởng, trận không kẹt. Vẽ lại 3 dạng thân, nhãn dung lượng, điểm vàng, thanh bom, tay kéo; tranh Bãi rác và thú bông khớp hình mới. Bỏ pha `pack` và quái `zipshard`. Tăng `rulesVersion`: `c2_zip_slime` 4, `dump_trail` 4 (giữ chuyến cũ), Vô tận 11. Ngoại lệ mới ở docs/APPROVED-EXCEPTIONS.md → Battle Mode.
- **Cân bằng (mô phỏng, không phải chơi thật; bot không tự đè và không kéo tay kéo bằng mèo, nên là trường hợp xấu):** chỉ các dòng màn 5 chương 2 đổi. Normal: Kiếm 20/20, Búa 19 → 16/20, Cuộn len 19/20, Găng 20/20. Hard: Kiếm 13 → 8/20, Búa 11 → 9/20, Cuộn len 10 → 11/20, Găng 16 → 13/20. Trung vị Vô tận: búa 27, len 25, găng 29 (trước 27, 23, 30).
- **Kiểm chứng:** BattleZipTests viết lại (94 check), ZipVisualChecks; Python 58 + 5, 3106 check C#, 1415 check WPF đạt; đã publish, smoke PASS.
- **Cần test tay:** docs/MANUAL-TEST.md, mục Slime Nén thiết kế lại 05/11.

## #52 — Slime xích đứng xa 200; mèo tự rớt khỏi tay khi đánh slime bay (chưa rõ nguyên nhân) (2026-11-05, commit 296dfc4)
- **Yêu cầu (người dùng):** slime xích vẫn quá gần, cho cách xa bằng khoảng nối dây (200). Mèo tự rớt khi đang cầm đánh slime bay gần cuối trận, kiểm tra xem có phải lỗi.
- **Slime xích:** `TetherPost` đặt ở `link.Range` tính từ tâm đồng minh, bỏ `TetherClear` của #50.
- **Mèo rớt, đã kiểm tra:** không có luật trận nào thả mèo; log trận 21:38 và 21:39 không có lỗi; cửa sổ tạm đóng, bật, tắt dưới con trỏ không làm mất capture (không giữ chuột thật). `EndDrag` chỉ chạy khi nhả chuột, mất capture, Pause hoặc đặt lại. **Chưa tái hiện, chưa xác định nguyên nhân, chưa sửa.** Thêm dòng log "Cat let go in battle" (lý do, nút chuột, ai giữ capture, thời gian cầm, số quái) để lần sau biết.
- **Cân bằng (mô phỏng, không phải chơi thật):** 150 dòng chương 2 đổi 4 số ván thắng, không dòng nào lệch từ 3 ván; Viễn chinh 3 dòng đổi; trung vị Vô tận hammer 27, yarn 23, glove 30 như trước. Bot không đo được việc mèo vượt qua đồng minh khi chơi thật.
- **Kiểm chứng:** BattleLinkTests. Kết quả test: xem PROGRESS.md.
- **Cần test tay:** docs/MANUAL-TEST.md, mục Góp ý sau chơi thử 05/11, đợt 2.

## #51 — Chụp Màn Hình chỉ làm đứng yên, không làm slime bay/bám tường rơi (2026-11-05, commit e59b1f0)
- **Yêu cầu (người dùng):** app Chụp Màn Hình chỉ làm quái đứng yên, không choáng slime bay và slime bám tường khiến chúng rơi. Thay luật 04/11 (GOPY-C D10).
- **Nguyên nhân:** `FreezeEnemies` gọi `KnockFromSky` với mọi quái, nên chúng choáng, treo giữa không trung trong lúc đứng hình rồi rơi.
- **Sửa:** bỏ lời gọi đó. Hàm `KnockFromSky` vẫn dùng cho đòn gây sát thương của app.
- **Cân bằng (mô phỏng, không phải chơi thật):** 150 dòng chương 2 đổi 11 số ván thắng, không dòng nào lệch từ 3 ván trở lên, không ngưỡng nào hỏng; trung vị Vô tận hammer 27, yarn 23, glove 30 như trước.
- **Kiểm chứng:** BattleSkyTests, BattleWallTests. Kết quả test: xem PROGRESS.md.
- **Cần test tay:** docs/MANUAL-TEST.md, mục Góp ý sau chơi thử 05/11, đợt 2.

## #50 — Góp ý chơi thử đợt 2: slime hẹn giờ, nhạc trưởng, slime xích, Bình xịt khi bế mèo, Kẹp gắp lên slime trên cao (2026-11-05, commit 32f049c)
- **Yêu cầu (người dùng):** (1) hết giờ gọi đội hình ngẫu nhiên, được ngoại lệ vì do triệu hồi và được vượt số slime trong trận; (2) bỏ việc mọi slime trong 220 với tới mèo cùng đánh; (3) slime xích đứng xa con đang xích để tránh sát thương lan; (4) hiệu ứng Bình xịt trên slime bay/bám tường tự mất khi bế mèo; (5) chọn slime bay/bám tường bằng Kẹp gắp thì chúng bị đẩy, không có hoạt cảnh gắp.
- **Nguyên nhân:** (1) chỉ 2 đội, có cổng `minThreat`, chờ chỗ theo `maxAlive`; (2) `StrikeTogether`; (3) `TetherPost` cách mép 6 trong khi lan 60; (4) nhấc mèo tắt Bình xịt theo luật cũ; (5) `Grab` gọi `KnockDown`/`KnockOff` thay vì giữ.
- **Sửa:** (1) 8 đội, không xét threat, không chờ chỗ, `calledOut` cho liên kết vượt giới hạn, bỏ `callFormations` khỏi `Reach`, lúc nạp từ chối đội có slime hẹn giờ/boss; (2) có trống lớn thì chỉ trống lớn; (3) `comboSplashRadius` + 8; (4) đang xịt thì nhấc mèo không dừng; (5) `OutOfHand`, `Reeling`, `Reeled`: gắp tại chỗ, kéo xuống, vẽ càng kẹp trên lớp phủ.
- **Quyết định thêm khi làm:** Vua Slime biến dị giữ đòn đồng loạt; (4) áp dụng cho mọi slime vì luật cũ không tách slime trên cao; đang bế mèo mà kẹp còn trên cao thì slime rơi.
- **Cân bằng (mô phỏng, không phải chơi thật):** Cuộn len màn 2 chương 2, Normal, simple 13 → 12/20 làm hỏng ngưỡng 65%; đo 60 seed thì 43 → 42/60 (~70%), là dao động mẫu, người dùng duyệt hạ ngưỡng riêng dòng này xuống 60%. Chương 2 ở 60 seed: 4 dòng lệch từ 3 ván trở lên, cả tăng lẫn giảm. Viễn chinh lệch 1–2 ván; Vô tận trung vị hammer 27, yarn 23, glove 30 như trước, nhưng bot ít gặp slime hẹn giờ nên chưa chứng minh đội hình lớn là ổn.
- **Kiểm chứng:** BattleQuillsTests, BattleLinkTests, BattleBandTests, BattleSkyTests, BattleWallTests, BattleHandToolsTests. Kết quả test: xem PROGRESS.md.
- **Cần test tay:** docs/MANUAL-TEST.md, mục Góp ý sau chơi thử 05/11, đợt 2. Hình càng kẹp trên cao chưa xem trong trận thật.

## #49 — Góp ý chơi thử: ảo ảnh, slime băng, slime dỗi, Bình xịt, app trên quái ở cao (2026-11-05, commit 5f3b12d)
- **Triệu chứng (người dùng):** (1) hiệu ứng gacha vị trí của slime ảo ảnh quá nhỏ; (2) slime băng hết vỏ vẫn bị cú ném trúng; (3) Kẹp gắp không tác dụng lên vỏ băng; (4) slime dỗi không có hoạt cảnh òa khóc rồi rời đi; (5) Bình xịt không dùng được với slime bám tường cao và slime bay; (6) hiệu ứng app nằm dưới sàn dù quái ở trên cao; (7) dùng Tab mà mèo đi ngang dưới slime bay/bám tường thì chúng tự choáng và rơi.
- **Nguyên nhân:** (1) dùng chung khói trắng cỡ thường với lúc slime chết; (2) `ThrowContact` chỉ bỏ dội lại khi hết vỏ, `throwHit` vẫn chạy; (3) `Slam` không đụng vỏ; (4) `Tantrum` gọi `Defeat` ngay, không có phần vẽ; (5) `SprayCandidate` loại `InSky`, click cửa sổ slime bay bỏ qua Bình xịt, vòi xịt bị cắt ở dải trận; (6) hiệu ứng và đường bay dùng `Height` không cộng độ cao, dải trận chỉ cao 170, khung đường bay không tới đỉnh màn hình; (7) `TabPulse` chỉ so khoảng cách ngang, mà từ 04/11 đòn app kéo quái trên cao xuống.
- **Sửa:** (1) hiệu ứng `MirageSmoke` và báo trước `ShuffleSoon`, chỉ đổi hình; (2) không có vỏ thì mèo ném bay xuyên (cách A người dùng duyệt); (3) cú đập vỡ vỏ con bị đập và con trong vùng đập; (4) hoạt cảnh khóc 0,9 s rồi chạy tới 1,6 s; (5) khóa mọi độ cao, nhận click cửa sổ slime bay, vẽ trên lớp phủ, thêm giọt nước trên slime bay; (6) app dùng `BodyTop`/`BodyBottom`, `SkyEffect` vẽ qua `SkyShotView`, `UpdateAppFlights` mở khung tới mục tiêu cao; (7) `TabReach` 30: lốc xoáy chỉ trúng quái dưới sàn. Câu gợi ý slime băng đổi vi/en/ja.
- **Quyết định thêm khi làm:** hai góp ý (6), (7) gửi kèm chữ "ok" nên gom vào mốc này. Bão Thông Báo, End Task, Thùng Rác, Chụp Màn Hình vẫn kéo quái trên cao xuống như luật 04/11; chỉ Tab đổi. Slime bám tường ở độ cao 60 cũng không bị Tab trúng.
- **Cân bằng (mô phỏng, không phải chơi thật):** 2/150 dòng đổi số ván thắng, đều +1: màn 4 Kiếm Hard casual 14 → 15, màn 5 Kiếm Hard casual 9 → 10; HP còn lại dao động vài %. Không ngưỡng nào hỏng.
- **Kiểm chứng:** BattleWallTests, BattleSkyTests, BattleTrickTests, TrickVisualChecks (ảnh render khói, quầng tím, mặt khóc). Kết quả test: xem PROGRESS.md.
- **Cần test tay:** docs/MANUAL-TEST.md, mục Góp ý sau chơi thử 05/11. Lớp phủ hiệu ứng trên cao và đường bay mới chưa xem trong trận thật, chưa thử DPI thật.

## #48 — Slime cờ hiệu: bất tử tới khi cắm cờ, cờ bất tử khi chủ sống, cờ thứ hai, né mèo (2026-11-05, commit 301781a)
- **Yêu cầu (người dùng):** con mang cờ bất tử tới khi cắm cờ đầu; cắm thêm cờ thứ hai và ưu tiên né mèo để kịp cắm; cờ bất tử tới khi chủ chết, chủ chết thì cờ đổ được và gây hoảng; Kéo cắt nhổ một nhát cờ bất tử; áp dụng mọi chế độ, tính cả tinh anh.
- **Làm:** `PetBattle.Quills.cs` (`PlantFlag`, `DodgeCat`, `FlagOwnerFell`); dùng lại `Shielded` để mèo không nhắm, hiệu ứng không dính, thêm `FlagGuard` để tách khỏi khiên boss; `plant.count` 2, `every` 12, `dodge` 220, `dodgeSpeed` 2; vòng vàng và dây nét đứt tới chủ; banner mới vi/en/ja; tăng `rulesVersion` Thư mục ma (4) và Vô tận (10); Viễn chinh giữ 8 vì số này còn quyết định giữ chuyến đang dở.
- **Quyết định thêm khi làm:** Kéo một nhát cả cờ đã hết bất tử (nếu không thì cờ thường lại bền hơn cờ bất tử); cờ mất không được bù; cờ thứ hai không cắm chồng dù hết giờ; đang bị len kéo thì không né.
- **Cân bằng (mô phỏng, không phải chơi thật):** bot được dạy chọn con mang cờ khi đã đánh được và dùng Kéo vào cờ bất tử. Màn 2 chương 2, người chơi simple, thắng/20: Normal Kiếm 18, Búa 19, Cuộn len 13, Găng 20; Hard Kiếm 16, Búa 12, Cuộn len 6, Găng 13. Cuộn len tụt vì luật bất tử trước khi cắm: nó không còn kéo và giết con mang cờ sớm (tắt riêng luật đó thì về 18/20, không check nào fail). Người dùng giữ luật và duyệt nới: Cuộn len màn 2 Normal 80% → 65%, Hard lệch Kiếm 45 → 50 điểm.
- **Kiểm chứng:** BattleFormationTests.GuardChecks, FlagVisualChecks (ảnh render, nền sáng và tối). Kết quả test: xem PROGRESS.md.
- **Cần test tay:** docs/MANUAL-TEST.md, mục Slime cờ hiệu bất tử 05/11. Giới hạn đã biết: bị dồn vào mép mà không đủ khoảng cách thì nó không cắm được, không chạy vòng qua mèo.

---

## #47 — Slime cờ hiệu đặt cờ chồng nhau, không tận dụng được vùng cờ (2026-11-05, commit dd24428)
- **Triệu chứng (người dùng):** hai slime cờ hiệu có thể đặt cờ chồng lên nhau, hiệu ứng không được dùng hết.
- **Nguyên nhân:** cắm ngay sau 1,5 s tại chỗ đứng, không xét cờ khác. Vòng không cộng dồn (`AuraCut` lấy `Math.Max`) nên cờ thứ hai gần như vô dụng; hai con lại xuất hiện cạnh cùng một đồng minh nên dễ trùng chỗ. Nặng hơn: 1,5 s chỉ đi ~45 DIP nên cờ thường nằm sau đội hình, quân rời vòng trước khi giao tranh.
- **Sửa:** `PetBattle.Quills.cs`: chờ tuyến đầu tới gần mèo và nằm trong vòng (`plant.near` 300) hoặc tới `plant.by` 10 s; giữ cách cờ khác `plant.spacing` 1,5 vòng; chỗ đã có cờ thì đi tới chỗ trống che nhiều đồng minh chưa có vòng; luôn cắm được khi hết giờ. Banner giải thích cờ hiện cả lúc cắm vì cắm muộn hơn dễ không có quái mới xuất hiện sau đó.
- **Cân bằng (mô phỏng, không phải chơi thật):** màn 2 chương 2, người chơi simple, thắng/20 trước → sau: Normal: Kiếm 18 → 19, Búa 19 → 19, Cuộn len 17 → 18; Hard: Kiếm 12 → 14, Búa 14 → 10, Cuộn len 6 → 5. Cờ nhiều HP hơn hay `near`/`by` khác đều không giữ được ngưỡng cũ; chưa rõ vì sao Kiếm mạnh lên. Người dùng duyệt nới 2 ngưỡng: Cuộn len màn 2 Hard 30 → 45 điểm so với Kiếm; Búa `dump_trail` route greedy 20 → 18/40 (đo được 19/40). Giữ nguyên `rulesVersion`.
- **Kiểm chứng:** BattleFormationTests.PlantingChecks. Kết quả test: xem PROGRESS.md.
- **Cần test tay:** Vô tận chặng Mê cung ảo ảnh và Thư mục ma (docs/MANUAL-TEST.md, mục Slime cờ hiệu 05/11).

---


---

## #46 — Góp ý sau #45: kéo lệch đường cắt, tầm Kéo ngắn, Kẹp gắp chỉ là chấm nhỏ, nhịp cuối nhạc trưởng không nổi (2026-11-05, commit 9464def)
- **Triệu chứng (ảnh người dùng):** cắt sát sàn thì cây kéo nằm cao hơn đường cắt; muốn tầm Kéo dài hơn; Kẹp gắp không giống cái kẹp đang gắp slime; nhịp cuối của slime nhạc trưởng chưa nổi bật.
- **Nguyên nhân:** (1) lỗi do bản sửa #45: chỉ nâng cây kéo khỏi sàn, đường cắt vẫn ở chỗ click. (2) Tầm 360 DIP. (3) Kẹp chỉ là hai đoạn thẳng 9 DIP và chấm tròn trên đỉnh đầu slime. (4) Nhịp cuối chỉ đổi ba chấm nhỏ sang đỏ.
- **Sửa:** (1) sim đặt đường cắt thấp nhất cao 42 DIP (`CutFloorHeight`), đường, kéo và dải trúng cùng một độ cao; cắt sát sàn vẫn trúng tới sàn. (2) `scissorsLength` 560; `scissorsSweep` 0,7 để tốc độ chạy gần như cũ. (3) Vẽ càng cẩu: đầu kẹp, trục vàng, hai càng cong ôm hai bên thân, theo cỡ slime, siết nhẹ theo thời gian. (4) Trước nhịp cuối: vòng đỏ sáng nhấp nháy, sóng đầy dần tới lúc đánh, trống to gấp 1,8 lần và rung, dùi giơ cao, "!" đỏ lớn. Lúc đánh: vòng dày hơn, mảng nổ và chữ BÙM!. Phần (3), (4) chỉ đổi hình ảnh.
- **Kiểm chứng:** xem PROGRESS.md. ScissorsVisualChecks vẽ thật (kéo đúng tâm đường cắt, càng kẹp, nhịp cuối).
- **Cần test tay:** cân bằng Kéo 560 DIP; độ dễ đọc của vòng đỏ khi đông quái; càng kẹp với slime lớn và trùm.

---

## #45 — Góp ý sau C4: vũng nhầy không hiện số sát thương, Kéo chỉ thấy chấm đỏ, bảng chuẩn bị Trận tự chọn "không có button" (2026-11-05, commit 8188126, d231dc1)
- **Triệu chứng (góp ý người dùng):** (1) mèo đi qua bãi nhầy của slime phun mất máu nhưng không có số; (2) đường cắt ổn nhưng không có cây kéo lớn đi qua, chỉ thấy chấm đỏ chạy trên đường cắt; (3) bảng chuẩn bị của Trận tự chọn không có nút quay về sảnh chọn chế độ (người dùng làm rõ sau khi hỏi lại).
- **Nguyên nhân:** (1) `PuddleOnCat` chỉ gọi `LandHit`, không tạo số. (2) Kéo vẽ chỉ khoảng 35 DIP, lưỡi mảnh màu nhạt, nên chỉ thấy hai vòng tay cầm đỏ; với đường cắt sát sàn còn bị cắt mất phần dưới. (3) nút hủy của bảng này chỉ là Đóng, về thẳng desktop; Vô tận thì có "‹ Sảnh". Tìm thêm một lỗi nhỏ: chiều cao panel thiếu 2 DIP nên hiện thanh cuộn.
- **Sửa:** (1) mỗi lần đốt hiện số đỏ "-1" trên đầu mèo, tính theo máu thật sự mất (giáp làm lần đốt dưới 1 thì cộng dồn đủ 1 mới hiện); các đòn khác vào mèo vẫn không có số như trước. (2) Vẽ lại kéo dài khoảng 100 DIP, hai lưỡi và vòng tay cầm rõ, đóng mở chậm hơn, không chìm xuống sàn. Chỉ đổi phần vẽ. (3) Mở từ sảnh, hoặc từ "Đổi trang bị" ở bảng kết quả, nút đổi thành "‹ Sảnh" và quay về sảnh chọn chế độ. Sửa phần thiếu 2 DIP, thêm check nút nằm trong cửa sổ.
- **Kiểm chứng:** xem PROGRESS.md. Test mới: BattleSpitSpringTests (số đốt), ScissorsVisualChecks vẽ thật, BattlePrepChecks (nút trong cửa sổ).
- **Cần test tay:** số đỏ khi đi qua vũng; cây kéo khi cắt ở sát sàn, giữa màn hình và phần giáp ranh dải trận; nút "‹ Sảnh" của bảng chuẩn bị Trận tự chọn.

---

## #44 — Đợt C4 góp ý 04/11: nhạc trưởng, cờ hiệu, bom + thảo dược, đội hình, hẹn giờ, slime theo mèo đang bế, mèo theo slime bay (2026-11-05, commit 124098c)
- **Triệu chứng (góp ý người dùng):** nhạc trưởng và cờ hiệu mờ nhạt, ít khác biệt; thảo dược hồi máu slime bom trông vô lý, cặp bom + thảo không có gì đáng sợ; đội hình gần như không gặp, mỗi con ra lẻ tẻ, từng phần ra ở hai phía khác lượt; bế mèo lên thì slime đứng im hoặc chạy như thường; trên sàn hết quái mà còn slime bay thì mèo đứng ngây ra.
- **Sửa (bản chốt docs/GOPY-C-2026-11-04.md D5, D6, D9, ngoại lệ D1c):** (1) **Nhạc trưởng:** nhịp cuối là trống lớn: mèo trên sàn trong vòng mất máu và loạn nhịp 2 giây (tinh anh 2,5), vung trượt, có nốt nhạc quanh đầu; nhấc mèo, Khiên, nộ tránh được; đánh/Đẩy thì đếm lại. (2) **Cờ hiệu:** sau 1,5 giây cắm cờ 30 HP tại chỗ; slime trong vòng vàng bớt 30% sát thương, không bị choáng; cờ đổ cả đội hoảng 2 giây. (3) **Bom + thảo dược:** Lá chắn lá cả sân, 1 thảo – 1 bom, chặn mọi sát thương; thảo dược không bao giờ hồi bom; lá chắn đỡ vụ nổ đầu, bom cháy đen bay về tay slime nhanh (bất tử lúc bay nên không có khoảng hở) để ném lại, không ai đỡ thì đáp choáng. (4) **Đội hình:** một đội là một đơn vị, cùng lúc cùng phía; tăng tỉ lệ ở chương 2, Viễn chinh, Vô tận (từ đợt 8, 3 đợt một lần); thêm Quân cảm tử, Pháo kích, Báo động. (5) **Hẹn giờ** reo gọi đội hình theo threat. (6) **Bế mèo:** slime tới với 50% tốc độ. (7) **Slime bay:** sàn hết quái thì mèo đi theo dưới nó. Tăng rulesVersion 4 màn chương 2 và Vô tận vì đợt quái đổi.
- **Kiểm chứng:** xem PROGRESS.md. Test mới: BattleDrumLeafTests (trống, lá chắn, bay về, đi theo, gọi đội hình), BattleFormationTests (một đơn vị, cờ cắm), LeafVisualChecks và FlagVisualChecks vẽ thật.
- **Cần test tay:** mức khó chịu của loạn nhịp, độ dễ đọc của lá chắn và "!" khi đông quái, cảm giác đội hình ở màn thật (docs/MANUAL-TEST.md Đợt C4). Bot mô phỏng không đánh giá được những thứ này.

---

## #43 — Đợt C3 góp ý 04/11: Kẹp gắp, Kéo cắt, Quạt, app với slime bay/bám tường, End Task nhiễm (2026-11-05, commit 26a898b)
- **Triệu chứng (góp ý người dùng):** vung chuột ném của Kẹp gắp khó trúng, kỹ năng yếu; Kéo cắt kéo nét ngang không hợp dây xích nằm ngang; Quạt "va mất 4 máu" gần như không thấy; app không hại slime bay, hiệu ứng Chụp Màn Hình chỉ ở dải dưới; "Không phản hồi" của End Task nhiễm khó hiểu.
- **Sửa (bản chốt docs/GOPY-C-2026-11-04.md D4, D8, D10):** (1) **Kẹp gắp:** click gắp (mặt "><", mèo không nhắm kể cả khi chọn, đứt dây, đồng hồ hẹn giờ dừng), click chỗ khác để đập: 20 máu + choáng, xung quanh mất 8; hết 5 giây đặt xuống phía xa mèo; slime bom thành bom của người dùng; slime trộm không đập được, giữ đủ 5 giây thì bị lật ngược lắc, nhả đồ và biến mất (chưa trộm gì: 1 xu hoặc +20% năng lượng icon thấp nhất). (2) **Kéo cắt:** click bất kỳ đâu, dải ngang 360 DIP, nét đứt 0,4 s rồi kéo lướt 0,5 s, hướng ngẫu nhiên; mỗi slime chạm dải 15 máu, đứt dây, dây cưỡi, dây về trùm, bong bóng, nhịp trống. Hồi 14 s. (3) **Quạt:** click bất kỳ đâu, cột gió 150 DIP 4 giây, hồi 14 s: slime sàn đứng yên, đòn lên mèo −30%, không nhập được, nhạc trưởng đếm lại; slime bay và bám tường trong cột bị thổi rơi; quạt trần ở đỉnh màn hình. (4) Lớp phủ chọn vị trí (`PetWindow.BattleAim.cs`) chừa HUD, mèo, dải trận; chuột phải hủy. (5) Đòn app (`DamageSource.App`) làm slime bay/bám tường rơi; Chụp Màn Hình chớp toàn màn hình + 4 góc khung, làm chúng rơi. (6) **End Task nhiễm:** mất 10% máu tối đa (không làm chết), đứng hình 3 giây, mỗi 15 giây; mèo mờ trắng, thanh "(Không phản hồi)", vòng xoay; click mèo 3 lần tỉnh sớm. Đã gỡ hẳn code giữ quạt/thổi/va, nét kéo, vung ném; bot mô phỏng dùng cơ chế mới.
- **Kiểm chứng:** xem PROGRESS.md. Test mới: BattleHandToolsTests (gắp, đập, đặt xuống, trộm; dải cắt), BattleToolsTests (cột gió), BattleSkyTests (app làm slime bay rơi), AppTests (End Task, 3 click), BattleZipTests (cắt dây trùm).
- **Cần test tay:** lớp phủ chọn vị trí trên màn hình thật (DPI, kéo mèo và bấm HUD khi đang chọn); cột gió, dải cắt và chớp sáng trên toàn màn hình; slime trộm bị lắc.

---

## #42 — Đợt C2 góp ý 04/11: ném mèo xuyên qua slime băng, Cú đáp địa chấn mờ nhạt, nộ khó thấy, gai nhím và đồng hồ hẹn giờ khó thấy, slime trộm phải lại gần (2026-11-04, commit 02e6d36)
- **Triệu chứng (góp ý người dùng):** ném mèo vào slime băng thì bay xuyên qua, không chạm gì; Cú đáp địa chấn chỉ là một vòng nhỏ, mèo vẫn nảy, không đẩy/choáng rõ; thanh nộ dưới HP quá mờ, không biết bấm đâu; muốn tự nộ; gai nhím dựng khó nhận ra; slime hẹn giờ chạy theo mèo, đồng hồ nhỏ; slime trộm phải lại sát mèo, tinh anh chỉ chạy nhanh hơn.
- **Nguyên nhân:** (1) khung va chạm cú ném dùng nửa bề rộng mèo (18) cả theo chiều dọc, mà tâm cửa sổ mèo khi lướt sát sàn ở ~56 DIP còn đỉnh slime băng 34 + 18 = 52 → **không bao giờ chạm**; ném chậm hơn 240 DIP/s thì bỏ qua hoàn toàn; (2) quake tức thời bán kính 110, không choáng, chạy khi mèo đã nảy xong; (3) nộ chỉ bấm thanh mảnh, bị khóa khi đang choáng.
- **Sửa (bản chốt docs/GOPY-C-2026-11-04.md D1a, D1b, D1f, D2, D3, D5):** (1) khung chạm theo chân (50 DIP dưới tâm) và đầu (38 trên tâm); mọi cú ném chạm vỏ băng: "cộp" + mèo bật ngược (≥220 DIP/s, hơi lên trên), nứt khi đủ nửa ngưỡng, vỡ khi đủ ngưỡng (480 → 380); (2) thẻ sẵn sàng thì lần chạm sàn đó dính, không nảy (`StickLanding`), mèo đứng yên 0,6 s; hai sóng nứt chạy ra hai bên 170 DIP trong 0,35 s: mỗi slime trúng 12 sát thương, đẩy văng (260), choáng 0,8 s; vẽ vết nứt sáng, bụi, chữ "RẦM!", rung khung trận, vòng hồi chiêu cạnh mèo; mô tả thẻ đổi; (3) vòng nộ quanh avatar (đầy dần cam, đầy thì vàng-đỏ phập phồng + lửa + dấu giận + huy hiệu "NỘ!" có tay chỉ; đang nộ lửa quanh vòng và viền HUD đỏ), click avatar để nộ; `rageAuto`: đầy thì tự nộ ở nhát đánh trúng kế tiếp hoặc thay cho cú choáng sắp dính (cú choáng bị hủy); dùng được khi đang choáng, kích hoạt giải choáng/đạn dính/chói mắt; đang nộ miễn chậm (vũng nhầy, vòng loading, đạn dính) và chói mắt; (4) nhím: gai dựng dài ×1,5, đỏ cam viền đỏ, pha cảnh báo rung mạnh + "!" đỏ; (5) hẹn giờ đứng yên (speed 0), đồng hồ to ×1,5, 3 s cuối phóng to, rung, nháy đỏ + tiếng (`ClockAlarm`), gọi tối đa 3 lần, lúc gọi có vòng sóng lớn; (6) trộm hút từ 160 DIP (tia tím có tia sáng chạy về nó), tinh anh lấy 2 thứ và trả lại cả 2 khi bị hạ.
- **Kiểm chứng:** test mới/sửa: BattleWallTests (ném chậm sát sàn chạm và "cộp", mèo bật ngược, lấy một lần), BattleTrickeryTests (sẵn sàng khi nhấc đủ lâu, mèo đứng yên, sóng tới chậm, 2 con trúng 12, đẩy văng, choáng; physics: đáp dính không nảy, không cờ thì nảy như cũ), BattleRageTests (đang choáng vẫn bấm được và hết choáng; tự nộ ở nhát kế tiếp; cú lao gây choáng thành tự nộ, không bị choáng; đang nộ không bị chậm), BattleQuillsTests (báo động 3 s cuối; tinh anh trộm 2 kỹ năng, trả cả 2; số liệu gốc). Cân bằng: mục B 2026-11-04 (nộ nạp chậm hơn, 5 giây). Kết quả bộ đầy đủ: xem PROGRESS.md.
- **Cần test tay:** ném mèo sát sàn vào slime băng (cộp, bật lại, cảm giác tay); Cú đáp địa chấn (đáp không nảy, sóng, rung có quá mạnh không); vòng nộ quanh avatar, tự nộ có quá dễ kích hoạt không; gai nhím, đồng hồ hẹn giờ, tia hút của slime trộm.

---

## #41 — Đợt C1 góp ý chơi thử 04/11: slime dỗi lặp vô hạn, ném mèo xong không dỗ được, bám tường, ảo ảnh, hợp thể, HUD chữ, túi đồ, bình xịt, quảng cáo (2026-11-04, commit a92053b)
- **Triệu chứng (góp ý người dùng):** (1) hết slime khác thì mèo đánh slime dỗi mãi, mà đánh thì nó không vui để bỏ đi → loop; ném mèo ra xa để dỗ thì trong lúc mèo bay/nảy rê chuột không dỗ được và thanh dỗ tự giảm; (2) slime bám tường đứng yên mà mèo chạy dí sát mép màn hình; (3) bong bóng không tính đòn combo; (4) bản thật và bản giả ảo ảnh xuất hiện cùng bên; (5) bầy hợp thể ra lần lượt từng con; (6) bình rớt làm sẵn sàng ngay cả kỹ năng (muốn −2 s một kỹ năng); (7) găng đấm chỉ có combo ở nhát 5; (8) icon Kéo cắt không giống chiếc kéo; (9) chữ thông báo HUD quá dài bị co thành một dòng chữ rất nhỏ / bị cắt ("9 wave…"), danh sách thẻ trong HUD dài cả khối khi chơi Vô tận lâu; (10) chữ đột biến bị cắt khi bảng chọn hiện; (11) mô tả thẻ trong túi đồ nằm cuối danh sách, cuộn là mất; (12) bình xịt phải giữ và rê chuột tới hết bình; (13) quảng cáo của Tab nhiễm: muốn vẫn đếm và thả slime ngẫu nhiên đúng luật; (14) trận tự chọn thiếu nút về Sảnh.
- **Nguyên nhân:** (1) `PickTarget` chỉ hạ ưu tiên slime dỗi ("guarded"), hết quái khác thì chọn nó; mọi nguồn sát thương (bom, splash, quét của cuộn len…) đều làm nó phồng; `PointerOn` bị chặn bằng `!CatHeld`, mà `CatHeld` bật suốt lúc mèo bay/nảy; (2) không có luật cho slime trên tường khi mèo hết mục tiêu dưới sàn, và chọn nó bị từ chối (Airborne); (4) bản giả sinh xen kẽ hai bên bản thật; (5) mỗi con là một đơn vị sinh riêng (1,6 s, chờ chỗ `maxAlive`); (9) banner trong `Viewbox` DownOnly, dòng trạng thái không xuống dòng, `upgradesText` liệt kê mọi tên thẻ; (11) dòng mô tả là một phần của nội dung cuộn; (13) quảng cáo bị khóa thì dừng đồng hồ, luôn thả `popupSpawn` "slime".
- **Sửa (bản chốt docs/GOPY-C-2026-11-04.md D5, D7, D10):** (1) slime dỗi chỉ nhận nhát mèo đánh trực tiếp (Weapon/Combo); mèo chỉ đánh nó khi chỉ còn toàn slime dỗi hoặc người dùng chọn, không đánh con đang được rê chuột (0,6 s), còn toàn slime dỗi thì đánh con xa con trỏ nhất; mỗi nhát −15% thanh dỗ; phồng tối đa + 3 nhát (tinh anh 5) → òa khóc dậm chân (mèo trong tầm mất máu) bỏ chạy: tính hạ nhưng không rơi đồ/xu, không tính "dỗ"; dỗ 1400 DIP (tinh anh 2200), "lây vui" +30% cho con dỗi trong 200 DIP (tinh anh nửa); `HandBusy` (D1g): chỉ chặn chuột khi mèo nằm trong tay, thanh dỗ không giảm khi mèo đang bay/nảy; (2) `WallToWatch`: chọn slime bám tường (giờ chọn được) hoặc chỉ còn slime bám tường → mèo đi tới cách mép 90 DIP, đứng nhìn về mép, dấu "!" + mũi tên vàng, gợi ý một lần; quái dưới sàn trong tầm thì đánh trước; (3) đòn combo = 1 lần chọc; (4) bản giả sinh phía bên kia mèo; gacha mỗi 3 s (tinh anh 2,5 s): 60% bản thật đổi chỗ với một bản giả, còn lại chỉ phụt khói; khói xóa thông tin rê chuột (luồng random riêng `Mirage`, trận không có ảo ảnh không đổi); (5) `PackMergers` gom slime hợp thể của đợt thành một đơn vị ra cùng lúc (giữa sàn + 2 con cùng mép, giữ ×1,6 khi tụ), chiếm 1 chỗ; (6) bình: −`potionSeconds` 2 s cho kỹ năng còn hồi lâu nhất; (7) găng: combo thường ở nhát 3 (không hất), nhát 5 hất tung + phá vỏ băng (`BreakIce`); (8) kéo vẽ lại: hai lưỡi thép bắt chéo, ốc vàng, hai vòng tay cầm; (9) banner xuống tối đa 2 dòng (rộng = giữa HUD), hiện lâu thêm theo độ dài; dòng trạng thái 2 dòng + "…", đột biến tách dòng riêng; thẻ thu gọn "nhóm điểm · ★n ◆n ◈n", rê chuột lên hiện đủ tên ở dòng gợi ý; (10) thẻ đột biến có nhãn "Hiệu ứng:" / "★ Thưởng:"; (11) túi đồ có cột "Mô tả" cố định bên trái (240 DIP): rê thẻ/cộng hưởng/di vật thì hiện, rời ra thì hiện câu hướng dẫn; (12) "Bình xịt nước nóng": click một slime → tự khóa và xịt tới hết nước (chuột không lái/dừng nữa), con đó ngã thì chuyển con gần nhất, slime ngã trong tầm xịt +1,5 s nước (tối đa 1,5 bình), xịt 3 s tan vỏ băng; (13) quảng cáo bị khóa vẫn đếm 8 s, thả một slime ngẫu nhiên trong `popupKinds` (13 loại đi một mình) mà trận này có, không có thì `popupSpawn`; (14) HUD trận tự chọn có nút ngôi nhà "Về Sảnh" (kết thúc trận, mở Sảnh).
- **Kiểm chứng:** test C# mới/sửa: BattleTrickTests (dỗi: bom không tác dụng, nhát mèo làm phồng, bùng nổ sau 3 nhát, đang rê thì mèo không đánh, rê được khi mèo bay và thanh không giảm, hai con dỗi: đánh con không được rê + lây vui, quét cuộn len không chạm; bong bóng: chỉ combo chọc; ảo ảnh: bản giả phía bên kia, gacha có đổi và không đổi), BattleWallTests (đứng ngó cách mép 90, chọn được, quái trong tầm đánh trước), BattleSwarmTests (bầy ra cùng một bước dù `maxAlive` 1), BattleDropTests (bình −2 s), BattleNewWeaponTests (combo nhát 3 và 5, nhát 5 phá băng), BattleHandToolsTests (bình khóa mục tiêu, theo slime, +nước, chuyển mục tiêu, tan băng), AppTests (quảng cáo khóa vẫn đếm, slime ra thuộc danh sách an toàn). Cân bằng: xem mục B 2026-11-04. Kết quả bộ đầy đủ: xem PROGRESS.md.
- **Cần test tay:** dỗ slime dỗi khi mèo đang bay sau cú ném; mèo đứng ngó slime bám tường; gacha ảo ảnh có dễ nhầm không; bầy hợp thể ra cùng lúc; HUD Vô tận lâu (chữ 2 dòng, rê chuột lên dòng thẻ); túi đồ cột mô tả; bình xịt nước nóng; nút Về Sảnh ở trận tự chọn.

---

## #40 — Slime bám tường chỉ bám dưới mép màn hình (2026-10-31, commit 1d7cd8a)
- **Triệu chứng (góp ý người dùng):** slime bám tường chỉ leo trong dải trận ở đáy màn hình; muốn nó leo theo chiều cao màn hình.
- **Nguyên nhân:** chiến trường là một dải cao 170 DIPs nên độ cao bị chặn ở `ClingTop` 110.
- **Sửa (người dùng duyệt cửa sổ riêng 31/10):** khóa `cling.top` 520: trượt sau mỗi loạt gai chọn độ cao trong 60–trần (trần = `top`, dưới đỉnh màn hình), trượt nhanh ×2,5; cao hơn 110 thì `InSky` (qua `HighOnWall`) → vẽ trong cửa sổ topmost riêng như slime bay (đặt theo `BodyX`, xoay áp tường), click Đẩy/Kẹp gắp như slime bay; gai ném từ trên cao bay lâu hơn và vẽ trên canvas trên trời (`SpikeInSky`; dải trận chỉ vẽ gai thấp). `DrawSpike` tách thành `DrawSpikeAt` dùng chung.
- **Kiểm chứng:** test C#: 5 độ cao liên tiếp cách nhau ≥ 30, trong 60–520, có lần vượt dải trận, cùng seed ra cùng kết quả. Check WPF mới: ở cao thì vẽ trong cửa sổ riêng, dải trận không vẽ; gai từ trên cao hiện trên canvas trên trời. `scripts/test.ps1 -Ui`: C# 2890, WPF 1389/1389; đã xem ảnh enemy-wall-high, enemy-wall-spike (`artifacts/render-b9`). Chưa chơi thử trên màn hình thật (vị trí cửa sổ sát mép, DPI).

---

## #39 — Thảo dược không tự nối lại slime tách đôi (2026-10-31, commit 4a5ce08)
- **Triệu chứng (góp ý người dùng):** muốn slime thảo dược tự hồi cho slime tách đôi ngay khi nó tách, hai nửa có animation hợp lại làm một; lần hồi này riêng, không tính vào ngân sách hồi, và xảy ra được cùng lúc khi đang hồi con khác.
- **Nguyên nhân:** M11: hai nửa chỉ hợp lại khi thảo dược đã hồi từng nửa bằng lượt hồi thường (trong tầm, trừ ngân sách, mỗi lượt một con).
- **Sửa:** khóa mới `rejoinSplits` cho thảo dược: lúc tách, thảo dược gần nhất (ở đâu cũng được) hồi ngay mỗi nửa 0,5 × lượng hồi, không trừ ngân sách, không chiếm lượt gồng; hai nửa đánh dấu đã hồi nên chụm lại và hợp lại (thanh đếm + hiệu ứng hợp thể sẵn có). Lần đầu thử hồi đủ lượng và cho con hợp lại tách tiếp: tổng máu phải đánh ~250 thay vì ~112, chương 2 màn 5 Khó búa tụt 25% (kiếm 70%, lệch > 40 điểm); chốt: hồi 0,5 × và con hợp lại bị hạ thì không tách nữa (~140). Gợi ý + âm thanh mới.
- **Kiểm chứng:** test mới: thảo dược ở xa vẫn hồi ngay hai nửa, ngân sách không đổi, không vào trạng thái gồng; hai nửa hợp lại; con hợp lại bị hạ thì không tách. C# đạt cả mô phỏng cân bằng.

---

## #38 — Slime hợp thể quá yếu (2026-10-31, commit 5a1aa21)
- **Triệu chứng (góp ý người dùng):** slime hợp thể dễ bị mèo hạ dù khổng lồ có 3 lớp chắn; muốn buff gắn với việc "3 con".
- **Nguyên nhân:** mỗi con 26 HP đi riêng lẻ, mèo hạ từng con trước khi chúng kịp chụm đủ 3; thiếu 1 con là cả bầy không bao giờ hợp thể.
- **Sửa:** khóa mới trong `link` của hợp thể: `pack` (đòn chia đều cho các con cùng loại trong 260 DIPs, sợi chấm hồng nối bầy sáng lên khi chia), `packRage` 1,35 (một con bị hạ, các con còn lại nhanh hơn tới hết trận, lông mày giận), `minCount` 2 (còn ít hơn 3 thì 2 con vẫn hợp thể), `slamRadius` 140 / `slamShare` 0,6 (khổng lồ vừa hợp thể dậm đất, trúng mèo dưới sàn trong vòng + choáng 0,5 s). Gợi ý + âm thanh mới, lời mẹo đổi.
- **Kiểm chứng:** test mới: đòn 30 vào một con thì cả 3 mất 10; một con bị hạ thì 2 con kia nổi giận; 2 con còn lại hợp thể (máu = tổng 2 con) và dậm đất trúng chỗ mèo. C# đạt cả mô phỏng cân bằng.

---

## #37 — Vũng nhầy không hại mèo; slime nhanh chỉ được buff khi lao qua; slime phun không ưu tiên buff (2026-10-31, commit c7242cd)
- **Triệu chứng (góp ý người dùng):** bãi nhầy của slime phun phải gây sát thương và làm chậm khi mèo đứng vào; slime nhanh chỉ cần chạm vũng là được buff cả trận (lao xa ×1,5, nhanh ×1,4) và có hiệu ứng buff; slime phun nên ưu tiên buff slime nhanh rồi mới bắn mèo.
- **Nguyên nhân:** thiết kế M09: vũng chỉ kéo dài **cú lao đang đi qua nó**; không ảnh hưởng mèo; slime phun luôn ngắm chỗ mèo.
- **Sửa:** `spit.puddleDamage` 2 HP/s (mỗi 0,5 s, Khiên chặn, không khi nhấc hay `landingGrace`) và `puddleSlow` 0,7 (`MoveFactor`); slime lao **chạm** vũng là `GooBoosted` cả trận (mọi cú lao ×1,5 quãng, ×1,4 tốc độ), hình quầng tím + giọt nhầy rơi sau + thân bóng tím + dấu "+"; `spit.lane`: có slime nhanh chưa buff thì lần phun tới rơi trước mặt nó (tối đa 70 DIPs về phía mèo), mỗi con một lần. Gợi ý mới (vũng bỏng/chậm; buff cả trận). Bù cân bằng: nhịp phun 4 → 4,5 s; chương 2 màn 5 `threat` 1,45 → 1,4.
- **Kiểm chứng:** test mới: chạm vũng thì lần lao sau (vũng đã khô) vẫn xa hơn ×1,3; mèo đứng trong vũng mất ~2 HP/s và chậm ×0,7, nhấc mèo thì không; phát chặn rơi đúng 70 DIPs trước slime nhanh, chỉ một lần. Cân bằng: tắt bỏng và `lane` vẫn làm màn 5 búa 75% (do buff cả trận); 3 lần thử khác làm lần lượt búa/cuộn len 75%; chốt bộ trên: màn 5 kiếm 80%, búa 80%, cuộn len 85%, găng 85% (vẫn sát ngưỡng: nên rà cân bằng chương 2).

---

## #36 — Dây của slime xích khó thấy; slime xích chết quá nhanh (2026-10-31, commit 05c44bf)
- **Triệu chứng (góp ý người dùng):** dây nối của slime xích chưa đẹp, khó để ý; mèo vung kiếm vài cái là slime xích chết dù đang nối dây.
- **Nguyên nhân:** dây vẽ là hai nét cong mảnh màu nhầy; slime xích (36 HP) gánh 50% mọi đòn vào đồng minh mà không giảm gì, nên mỗi đòn vào đồng minh là nửa đòn vào nó.
- **Sửa:** khóa mới `link.armor` (chỉ cho `tether`, gốc 0,5): còn xích thì mọi sát thương slime xích nhận (cả phần gánh) ×0,5; cắt xích thì nhận đủ. Dây vẽ thành chuỗi mắt xích thép (mắt xen kẽ quay nghiêng), mỗi lần gánh có chớp vàng chạy dọc xích từ đồng minh về nó (`LinkPulse` 0,35 s), đồng minh có huy hiệu mắt xích cạnh đầu. Lời mẹo đổi theo ("còn xích thì nó cứng gấp đôi").
- **Kiểm chứng:** test mới: còn xích thì nhận 10/20 phần gánh và 10/20 đòn thẳng, có chớp; cắt xích nhận đủ 20. Sửa kỳ vọng 3 test cũ (phần gánh × armor). C# đạt cả mô phỏng cân bằng.

---

## #35 — Slime nhanh không quăng bom; hạ slime bom thì nó nổ luôn (OP) (2026-10-31, commit a23b799)
- **Triệu chứng (góp ý người dùng):** muốn slime nhanh cõng bom tới gần thì quăng bom vào mèo (có animation, đứng yên ~2 s sau khi quăng, bom chạm đất là nổ); hạ slime bom đang cháy ngòi thì nó nổ ngay, quá mạnh.
- **Nguyên nhân:** thiết kế M11: bom chỉ nhảy xuống cách mèo 120 rồi tự đi tới châm ngòi; `Defeat` gọi `Blast` cho bom đang cháy ngòi.
- **Sửa:** khóa mới `link.throwRange` 210 (+ `throwWindup` 0,5, `throwFlight` 0,55, `throwHeight` 70, `throwRest` 2): mèo dưới sàn cách con ngựa 120–210 thì nó đứng lại, cúi người gồng (bom nhấc cao), vòng đỏ **bám theo mèo trong lúc gồng, khóa khi quăng** (lần đầu khóa ngay lúc bắt đầu gồng thì mèo tự đi luôn ra khỏi vòng, hầu như không trúng), bom bay vòng cung, chạm đất là nổ; ngựa đứng yên 2 s. Đánh/Đẩy/choáng/Kẹp gắp ngựa lúc gồng: bom rơi, châm ngòi ngắn. Bom bị hạ khi đang cháy ngòi: **bom xịt** (phương án (a), không nổ). Gợi ý + âm thanh mới. Bù cân bằng: máu bom 30 → 45, sát thương 22 → 24, ngòi 1,5 → 1,1 s (tinh anh 0,8 s).
- **Kiểm chứng:** test mới: quăng trong khoảng 120–210, vòng theo mèo khi gồng, nổ đúng chỗ khóa và trúng mèo, ngựa đứng yên; mèo ra khỏi vòng sau khi bom bay thì trượt; Đẩy ngựa lúc gồng thì bom rơi; hạ bom đang cháy thì xịt, không ai mất máu. Cân bằng: chỉ đổi luật thì chương 2 màn 3 kiếm 92%/búa 91% HP (quá dễ, ngưỡng ≤ 90%); thử sát thương 26/30, thêm slime nhanh vào đợt cố định, ngân sách 10 đều không đạt hoặc làm màn 5 tụt; chốt bộ trên: màn 3 kiếm 90%, búa 88%; màn 5 búa 80% (sát ngưỡng).

---

## #34 — Slime lò xo chỉ làm choáng, không đẩy lùi; vô dụng trước cuộn len (2026-10-31, commit 96ee6be)
- **Triệu chứng (góp ý người dùng):** lò xo bật trúng chỉ làm choáng, không hất mèo như lò xo; mèo cầm cuộn len (tầm 114) đứng ngoài vòng 100 đánh nó mà không bao giờ bị bật.
- **Nguyên nhân:** thiết kế M09 "không đổi physics mèo": chỉ mất máu + choáng; lò xo chỉ trượt chậm tới cách mèo 120 rồi nằm im, không có cách với tới mèo đứng ngoài vòng.
- **Sửa (người dùng duyệt đẩy lùi 31/10):** `spring.push` 80 / `pushSeconds` 0,25: đòn lọt qua Khiên/Mai rùa thì trận trượt mèo ra xa (lệnh mới `CatCommand.Shove`, cửa sổ đặt vị trí mèo, không phải cú ném; nhấc mèo là dừng). **Bật nhảy:** mèo đứng ngoài vòng trong 170 DIPs suốt 1,5 s, hoặc vũ khí đánh trúng từ ngoài vòng, thì nó nén 0,45 s (vòng đỏ đầy dần ở chỗ mèo lúc đó), bay 0,55 s, chạm đất bật ngay; 5 s mới nhảy lại; Đẩy/choáng/Kẹp gắp lúc nén thì hủy. Gợi ý một lần mỗi trận, tiếng mới. `HurtCat` trả về đòn có lọt không.
- **Kiểm chứng:** test mới: bật trúng thì đẩy ~80 DIPs, quay mặt về lò xo; Khiên chặn thì không đẩy; đứng ngoài vòng 1,5 s thì nén nhảy tới đúng chỗ mèo và bật trúng; không nhảy lại ngay; nhấc mèo trước khi đáp thì né; đánh từ ngoài vòng (tầm 140) thì nhảy ngay. C# đạt cả mô phỏng cân bằng. Lỗi phát hiện khi làm: lời mẹo dài > 200 ký tự làm battle.json không nạp được (mọi test dùng dữ liệu gốc hỏng), đã rút gọn.

---

## #33 — Vũ khí trên bảng chuẩn bị tràn khỏi nút; app trong bảng dựng trận chỉ là nút chữ (2026-10-31, commit c82f605)
- **Triệu chứng (ảnh người dùng):** bảng "Chuẩn bị trận": hình kiếm/búa/cuộn len/găng thò ra mép trái nút, dấu ✓ chen giữa hình và tên. Bảng "Dựng trận" phần "App trong trận": toàn nút chữ ("Quảng cáo: Tắt"…), xấu.
- **Nguyên nhân:** `WeaponIcon` dịch cố định rồi vẽ vũ khí dài ~46 DIPs (xoay 35°) vào ô 24×28 nên tràn; ✓ nằm trong hàng cùng tên. Phần app dùng `Chip` chữ.
- **Sửa:** `WeaponIcon` đo khung thật của hình (DrawingGroup.Bounds, cache theo kiểu) rồi co cho vừa ô, căn giữa; thẻ vũ khí: ô vuông 32 tối màu bên trái chứa vũ khí, tên bên phải, ✓ thành huy hiệu tròn ở góc phải trên. Bảng dựng trận: app và app độc là ô như ô quái (icon, tên, trạng thái Bật/Tắt hoặc Tắt/Ít/Vừa/Nhiều, ô cam khi bật), chưa gặp là ô "?"; thêm tiêu đề "App độc · bấm để đổi"; icon app độc vẽ mới (PetMalwareIcon.cs: quảng cáo, vòng loading, cập nhật bắt buộc, virus.exe, virus vào icon, Captcha, pin yếu, con trỏ đơ).
- **Kiểm chứng:** `scripts/test.ps1 -Ui`: C# 2859, WPF 1387/1387 (check mới: mọi app/app độc đã gặp hiện thành ô); ảnh `artifacts/render-b2` (custom-fight-setup-en, custom-fight-builder-en, custom-fight-builder-apps-en) đã xem. Chưa xem trên DPI thật.

---

## #32 — Slime bám tường rơi xuống thì đuổi mèo, bám tường thì đứng yên một chỗ (2026-10-31, commit 22cf959)
- **Triệu chứng (góp ý người dùng):** bị choáng rơi xuống, slime bám tường chạy lại đánh mèo thay vì bám lên tường; trên tường nó chỉ đứng một độ cao mà bắn mãi.
- **Nguyên nhân:** thiết kế M15: sau khi rơi nó nằm choáng `downed` 2,5 s rồi đánh dưới sàn `ground` 4 s mới leo lại; trên mép không có bước di chuyển nào.
- **Sửa:** `ground` 0 (hết choáng đi thẳng về mép leo lên); khóa mới `cling.shift` (30): sau mỗi loạt gai trượt lên/xuống mép tới độ cao mới cách ít nhất 30 DIPs trong 60–110, tránh con khác cùng mép, bốc từ luồng random riêng `BattleRandom.Wall` (không đổi các luồng khác). Bù cân bằng: `downed` 2,5 → 3. Leo theo cả chiều cao màn hình: làm ở đợt B9 (cửa sổ riêng, người dùng duyệt 31/10).
- **Kiểm chứng:** test mới: hết choáng đi thẳng về mép và leo lại; trượt ≥ 30 sau mỗi loạt, trong khoảng, cùng seed ra cùng độ cao. Cân bằng: chỉ `ground` 0 thì chương 2 màn 5 găng 75% (< 80%); `downed` 4 hoặc máu 30 hay nhịp 3,5 s đều trượt (nhiễu ±1 ván/20); chốt `downed` 3 + `shift` 30: kiếm 80%, búa 85%, găng 80% (sát ngưỡng, nên rà lại cân bằng chương 2).

---

## #31 — Hợp thể ra hai hướng, đạn phun bay phẳng, trận tự chọn không có tinh anh, ném mèo vào slime băng không có gì (2026-10-31, commit 33c1337)
- **Triệu chứng (người dùng, sau khi test):** (1) bầy 3 slime hợp thể: hai con ngoài rìa ra ở hai mép khác nhau; (2) đạn slime phun bay ở cùng độ cao dù xa hay gần; (3) trận tự chọn như không bật được tinh anh; (4) ném mèo vào slime băng không thấy gì xảy ra.
- **Nguyên nhân:** (1) PetBattle.Waves.cs `PlaceMergeArrival` đặt cố định giữa / mép trái / mép phải. (2) PetBattleView.Traps.cs: đỉnh đường bay cố định 70 DIPs. (3) `RollElite` bỏ qua mọi tinh anh ở mức Dễ (ảnh người dùng đang chọn Dễ) và chỉ cho `maxElites` = 1 tinh anh cùng lúc; thanh chỉ có 0/10/25/50%. (4) Cần mèo bay ≥ 650 DIPs/s (bám tường chỉ 450), chậm hơn thì không có phản hồi gì.
- **Sửa:** (1) con đầu ở giữa, hai con sau cạnh nhau ở cùng mép xa mèo hơn. (2) `BattleSim.SpitArc`: đỉnh = khoảng cách × 0,28, trong 22–110 DIPs. (3) Trận tự chọn (`stage.Custom`): tinh anh cả ở mức Dễ, không giới hạn số cùng lúc; thanh 0–100% bước 10 (file cũ làm tròn về bội 10); bỏ dòng "Mức Dễ không có tinh anh". (4) `minSpeed` 480, `wetSpeed` 340; ném trúng ≥ nửa mức cần thì vỏ nứt (tia lóe, tiếng "keng", gợi ý một lần mỗi trận, không mất máu), mỗi vết nứt (tối đa 2) giảm 15% tốc độ cần.
- **Kiểm chứng:** test mới: trật tự xuất hiện của bầy hợp thể (BattleSwarmTests), độ cao đường bay đạn (BattleFeedbackFixTests), tinh anh 100% ở mức Dễ vượt `maxElites` (CustomFightTests), ném chưa đủ nhanh / nứt / nứt hai lần thì 75% đủ vỡ (BattleWallTests). `scripts/test.ps1 -Ui`: Python 58, i18n 5, C# 2858, WPF 1386/1386 (`artifacts/groupA-tests.log`, ảnh `artifacts/render-groupA`: bảng dựng trận không còn dòng "Mức Dễ không có tinh anh"). Chưa chơi thử trong trận thật (cảm giác ném 480, độ cao đường bay đạn).

---

## #30 — Sổ tay quái dư khoảng trống bên phải (2026-10-30, commit a452422)
- **Triệu chứng:** ảnh người dùng: lưới ô Sổ tay quái mỗi hàng 5 ô, bên phải (trước thanh cuộn) trống một dải rộng gần một ô.
- **Nguyên nhân:** ô là Border rộng cố định 82 DIP trong WrapPanel; vùng 526 DIP chỉ vừa 5 ô (5 × 88), phần dư không ai lấp. Cùng kiểu lỗi #25/#26.
- **Sửa:** PetWindow.MonsterBook.cs: UniformGrid 5 cột đều, ô giãn theo cột (bỏ Width 82 và MaxWidth tên), khoảng hở cột cuối kéo về để ô chạm sát làn thanh cuộn.
- **Kiểm chứng:** check WPF mới: mọi hàng đầy chạm mép phải vùng cuộn (526/526 DIP). `scripts/test.ps1 -Ui` source sạch: Python 58, i18n 5, C# 2852, WPF 1386/1386. Xem ảnh render battle-book-en. Publish 1.4.4+a452422, smoke 3/3. Chưa xem trên màn hình thật.

---

## #29 — Vũ khí cuộn len không giống cuộn len, đánh không giống quất roi (2026-10-30, commit 24046c6)
- **Triệu chứng:** cuộn len trong tay mèo trông như cây que với một chấm hồng nhỏ; đòn đánh vung cứng như kiếm, không ra cú quất roi.
- **Nguyên nhân:** cuộn len vẽ như một hình cứng (cán + sợi len uốn cố định + hình tròn r 6,5 với hai nét chéo), xoay nguyên khối theo góc vung giống kiếm, nên sợi len không bao giờ cong hay trễ sau cán.
- **Sửa:** PetRenderer.cs: cuộn len to hơn (r 8,5) có sợi quấn, chấm sáng, xoay theo đường vung; cán có núm; sợi len là đường cong nối núm với cuộn len theo góc riêng (YarnAngle). Đứng yên cuộn len rủ xuống, đung đưa; đòn đánh: nhấc lên, nạp ra sau đầu (vẽ sau đầu), quất vụt ra trước đúng lúc trúng với vệt hồng theo đường cuộn len và tia "chát", vượt nhẹ rồi rủ lại. Cổ vũ: quay vòng quanh cán; cầm trên tay: vung theo cú hất. Không đổi tầm, sát thương hay luật.
- **Kiểm chứng:** `scripts/test.ps1 -Ui` trên source sạch: Python 58, i18n 5, C# 2852, WPF 1385/1385 (gồm 4 vũ khí × 2 hướng × 3 góc treo × 11 thời điểm không cắt mép khung). Đã xem ảnh render 12 thời điểm vung + icon thẻ. Publish 1.4.4+24046c6, smoke 3/3. Chưa xem trong trận thật.

---

## #28 — Chương 2 như chưa có slime bay (2026-10-30, commit 33c1337)
- **Triệu chứng:** chơi chương 2 không gặp slime bay, dù mục tiêu và mẹo màn Giữ giỏ cá hứa có "slime bay bắn đạn dính".
- **Nguyên nhân:** slime bay và slime bom chỉ có trong dòng quái ngẫu nhiên đợt 3 của c2_fish_basket (mỗi loại ~1/8 mỗi lượt), và khi sàn gần đủ budget 9 thì lượt tốn 2 điểm bị đổi sang slime thường, nên một ván có thể không gặp con nào.
- **Sửa:** battle.json, c2_fish_basket: thêm một đợt cố định (1 slime bay, 1 slime bom, 2 slime) trước dòng quái ngẫu nhiên; dòng quái giữ nguyên. (2026-10-31: ban đầu 1 slime; với code sau đó check "Chapter 2 balance: stage 3, hammer, Normal, ≤ 90% HP" trượt 91%, xác nhận trên worktree HEAD sạch + chỉ diff battle.json. Đã thử: slime → slime nhanh (màn 5 găng 75% < 80%), đưa bay + bom vào đợt 2 (màn 5 găng 70%); chốt 2 slime: màn 3 búa 88% HP, màn 5 găng 80% vì build được mang sang các màn sau.)
- **Kiểm chứng:** C# 2809 PASS. Mô phỏng màn 3 chương 2 (thắng/20, trước → sau): Normal simple 19→20 (kiếm), 20→20 (búa); Hard simple 15→18, 16→18; Hard casual 16→19, 16→19; dễ hơn một chút vì dòng quái bắt đầu muộn hơn. Bản 2 slime: commit 33c1337 (cùng #31). Chưa chơi thử trong trận thật.

---

## #27 — Bóng chân của mèo trong trận trông như mảng tối (2026-10-26, commit bacde5a)
- **Triệu chứng:** ảnh người dùng (bản 7cd2bf5, nền tối): dưới chân mèo có một mảng tối lộ quanh chân và đuôi, không đọc ra là bóng.
- **Nguyên nhân:** bóng mèo vẽ trên dải chiến trường, dưới cửa sổ riêng của mèo; chân mèo trong cửa sổ cao hơn sàn dải vài DIP nên bóng không khớp bàn chân, và trên nền tối bóng nâu đen thành một khối.
- **Sửa:** bỏ bóng của mèo (mèo vốn không có bóng ở Pet Mode); giữ bóng slime (cùng lớp nên luôn khớp), nhỏ hơn (0,45 → 0,4 bề rộng, cao 4,5 → 4) và nhạt hơn (alpha 70 → 55, viền 90 → 70).
- **Kiểm chứng:** WPF 1027/1027, C# 1538; ảnh battle-kinds sáng/tối không còn mảng xám ở chỗ mèo. Chưa xem trong trận thật.

---

## #26 — Túi đồ: lưới thẻ và ô di vật dư khoảng trống bên phải (2026-10-26, commit 958146a)
- **Triệu chứng:** ảnh người dùng: tab Di vật (Viễn chinh) 3 ô "Ô trống" chừa một dải trống bên phải; tab Thẻ (Hướng dẫn phiêu lưu) mỗi hàng 3 thẻ, bên phải trống một khoảng.
- **Nguyên nhân:** cả hai dùng WrapPanel với ô cố định (thẻ 92 DIP, di vật 116 DIP); khung 389 DIP chỉ vừa 3 ô, phần dư không ai lấp.
- **Sửa:** UniformGrid cột đều: thẻ 3 cột (thẳng hàng 3 huy hiệu cộng hưởng phía trên), ô di vật chia đều một hàng (tối đa 3 cột); ô giãn theo cột.
- **Kiểm chứng:** WPF 1024/1024 (thêm: hàng thẻ và hàng ô di vật chạm mép phải khung, 389/389 DIP), xem ảnh bag-adventure-cards-en, bag-expedition-relics-en. Chưa xem trên màn hình thật.

---

## #25 — Bảng chọn nâng cấp Viễn chinh dư khoảng trống bên phải (2026-10-26, commit a58dbd5)
- **Triệu chứng:** ảnh người dùng: bảng "Chọn một nâng cấp (giữ suốt chuyến viễn chinh khi thắng bậc này)" rộng hơn 3 thẻ, bên phải trống; tiêu đề sát mép; "★ Thẻ hiếm · một lần mỗi build" bị cắt.
- **Nguyên nhân:** cửa sổ tự co theo nội dung (SizeToContent) mà tiêu đề không xuống dòng, nên bảng rộng theo tiêu đề tiếng Việt dài; dòng đếm cũng không wrap trong thẻ 132 DIP.
- **Sửa:** tiêu đề wrap với MaxWidth = số thẻ × 140 − 8; dòng đếm wrap.
- **Kiểm chứng:** WPF 1022/1022 (tiêu đề Viễn chinh vi nằm trong bề rộng hàng thẻ, bảng không rộng hơn thẻ + padding), xem ảnh expedition-upgrade-vi. Chưa xem trên màn hình thật.

---

## #24 — Chữ cộng hưởng trong túi đồ tràn ra ngoài khung; cần sinh động hơn (2026-10-25, commit aa6312b)
- **Triệu chứng:** tab Thẻ ở Vô tận: dòng "★ Giáp gai · còn 2 thẻ → ★ Bất khuất…" bị cắt ở mép phải túi đồ; phần cộng hưởng chỉ là chấm + chữ.
- **Nguyên nhân:** mỗi nhóm là một hàng ngang (StackPanel) nên chữ không xuống dòng; Vô tận có 3 mốc/nhóm nên chuỗi dài quá khung.
- **Sửa:** mỗi mốc là một huy hiệu có icon riêng (9 hình vector), vòng tiến độ, trạng thái, mô tả khi rê chuột; các huy hiệu nằm trong cột Grid bằng nhau, tên tối đa 2 dòng rồi "…". Thanh nhóm chia đoạn theo mốc. Lóe sáng một lần khi mở tab.
- **Kiểm chứng:** WPF 850/850 (Vô tận vi/en/ja: không chữ nào ra ngoài huy hiệu hay túi đồ), xem ảnh render. Chưa xem trên màn hình thật.

## #23 — Đòn combo thứ 3 đánh trúng slime ở khoảng cách xa (2026-10-24, commit 61324a8)
- **Triệu chứng:** combo trúng mục tiêu sát mèo nhưng slime đứng xa (khoảng gấp đôi tầm kiếm) cũng có tia lửa và mất máu.
- **Nguyên nhân:** sát thương lan của combo tính mọi quái trong 60 DIPs từ mép mục tiêu, không xét vị trí mèo/tầm vũ khí.
- **Sửa:** `PetBattle.Splash` chỉ lan tới quái phía mèo đang vung, mép gần cách tâm mèo ≤ tầm vũ khí + 14 DIPs. Nâng rulesVersion các chế độ.
- **Kiểm chứng:** C# 1535 (slime sau mục tiêu 40 DIPs không còn dính, slime sát sau vẫn dính), WPF 847/847, Python OK. Chưa xem trên màn hình thật.

## #22 — Vòng loading khi hướng dẫn trông như chưa hiện hết (2026-10-24, commit 5cf8980)
- **Triệu chứng:** ở bước hướng dẫn vòng loading (màn 3), vòng trông chưa hiện đủ.
- **Nguyên nhân:** (1) trước ec27b0a trận đứng hình đúng lúc vòng còn đang lớn dần (0,3 s); (2) chỗ sáng chỉ ôm vòng xoay phía trên, còn vùng làm chậm dưới sàn (hình bầu dục quanh chân mèo) và con mèo nằm trong vùng tối.
- **Sửa:** bước chờ vòng hiện xong (≥ 0,35 s); chiếu sáng cả vòng xoay, vùng dưới sàn (`BattleFieldView.LoadingZoneBounds`) và con mèo.
- **Kiểm chứng:** WPF 847/847 (bước loading bắt đầu khi vòng đã 0,35 s, có 3 chỗ sáng, vùng sàn rộng ≥ 2 × bán kính). Chưa xem trên màn hình thật.

## #21 — Hướng dẫn chiếu sáng virus.exe khi nó chưa hiện hết (2026-10-24, commit ec27b0a)
- **Triệu chứng:** virus.exe còn nhỏ, đang bung ra mà hướng dẫn đã sáng và trận đứng hình.
- **Nguyên nhân:** hướng dẫn đứng hình ngay khung đầu tiên file xuất hiện; thời gian trận dừng nên hoạt ảnh bung (0,35 s) dừng giữa chừng. Quảng cáo (0,25 s), tường cập nhật (0,3 s), icon lúc bị nhiễm (0,6 s) cùng kiểu.
- **Sửa:** các bước app độc chỉ bắt đầu khi thứ đó đã hiện xong (quảng cáo ≥ 0,3 s hoặc đang khóa, vòng/tường ≥ 0,35 s, virus.exe ≥ 0,4 s, icon nhiễm ≥ 0,65 s).
- **Kiểm chứng:** WPF (bước virus.exe bắt đầu khi file đã hiện 0,42 s).

## #20 — Không chọn được thẻ, phải chuyển cửa sổ khác mới chọn được (2026-10-24, commit ec27b0a)
- **Triệu chứng:** màn Cầm cự, bảng chọn thẻ hiện mà bấm không ăn; chuyển sang cửa sổ phía sau rồi quay lại mới chọn được (log: bảng thẻ mở ~22:46, chọn được 22:47:43).
- **Nguyên nhân:** giữ chuột lên vòng loading thì dải chiến trường `CaptureMouse`; vòng tự tắt khi đang giữ (đủ 2 s) → lúc thả, `EndHold` thấy không còn vòng được giữ nên thoát luôn, không `ReleaseMouseCapture`. Mọi click sau đó (cả bảng thẻ) chạy vào dải chiến trường cho tới khi Windows lấy lại chuột (đổi cửa sổ). Lỗi có từ khi thêm "giữ 2 s" (6b), lộ ra vì hướng dẫn màn 3 dạy giữ vòng ngay trước mốc chọn thẻ 20 s.
- **Sửa:** `EndHold` luôn trả chuột; `DropStaleHold()` mỗi khung trả chuột khi không còn vòng nào được giữ.
- **Kiểm chứng:** WPF (vòng tự tắt dưới chuột → khung sau trả chuột; thả chuột sau đó cũng trả). Cần thử tay: giữ vòng tới khi tắt rồi chọn thẻ ngay.

## #19 — Chỗ sáng của hướng dẫn nằm lệch khỏi con mèo (2026-10-24, commit 1d07490)
- **Triệu chứng:** bước "Kéo mèo lên", viền vàng nằm phía trên bên trái con mèo.
- **Nguyên nhân:** cửa sổ mèo có lề `PickupAnimation.Padding` 40 DIP quanh khung mèo 112×112; chỗ sáng đo từ góc cửa sổ nên lệch 40 DIP. Test lúc đó đo cùng gốc sai nên vẫn PASS (xem phần B).
- **Sửa:** cộng lề; test so thêm với `pet.X + CenterX` (độc lập với cách đo).
- **Kiểm chứng:** WPF 843/843 (tâm chỗ sáng 960 = pet.X + 56).

## #18 — Chỗ sáng của hướng dẫn chưa căn giữa (thanh kỹ năng HUD) (2026-10-24, commit 8dab2d4)
- **Triệu chứng:** viền vàng quanh thanh kỹ năng lệch, khoảng hở hai bên không đều.
- **Nguyên nhân:** HUD có Margin 8 mà chỗ sáng tính theo khung bên trong; thanh kỹ năng ôm cả lề StackPanel; slime chưa tính lúc phồng khi gồng; quảng cáo/vòng xoay/tường dùng số ước lượng.
- **Sửa:** đo mọi chỗ sáng bằng `PointToScreen` của chính phần tử (đúng lề, HUD bị kéo, DPI), đệm đều 6 DIP; thanh kỹ năng = hợp 5 ô; sân trận lấy đúng hình chữ nhật đang vẽ.
- **Kiểm chứng:** WPF (ô máu, thanh kỹ năng, ô Đẩy, túi đồ đều đệm đúng 6 DIP; tâm slime < 1 DIP).

## #17 — Âm thanh mèo và ô hướng dẫn chưa tick sẵn ở máy khác (2026-10-24, commit f6ea181)
- **Triệu chứng:** máy khác cài mới, âm thanh mèo tắt; ô "Hướng dẫn trong trận" tắt ở màn đã thắng.
- **Nguyên nhân:** `PetPreferences.Sounds` mặc định false (thiết kế cũ "off by default"); ô hướng dẫn mặc định theo "màn chưa thắng".
- **Sửa:** `Sounds` = true cho preferences mới (file đã lưu giữ giá trị cũ, theo người dùng chọn); `AdventureGuide` = true, ô luôn tick sẵn, bỏ tick lúc Sẵn sàng thì được nhớ. Rà các mặc định khác: giữ nguyên.
- **Kiểm chứng:** C# (preferences mới bật cả hai), WPF 841/841.

## #16 — Hướng dẫn tại chỗ: nhiều kiểu, khó hiểu; cần một kiểu và chỉ ở Phiêu lưu (2026-10-24, commit cb77240)
- **Góp ý:** hướng dẫn nên một kiểu, bỏ những gì nhìn là hiểu, theo từng màn chương 1 (màn 1 kéo mèo 6 lần có slime nhanh, màn 2 HUD/kỹ năng/túi/thẻ/cộng hưởng có làm tối, màn 3 gom app độc + icon nhiễm, màn 4 Đẩy ~4 lần + tinh anh, màn 5 Vua theo cơ chế, Đẩy chặn phong tinh anh 1 lần); đổi Phiêu lưu → Hướng dẫn phiêu lưu; mô tả kỹ từng màn.
- **Làm:** khung "Hướng dẫn phiêu lưu" (đứng hình không phải Pause, làm tối + chỗ sáng + một dòng, Hiểu rồi/Bỏ qua); bỏ hướng dẫn 5 thẻ và dấu tay; màn 2 không app độc, màn 3 lịch app độc cố định + không tinh anh, màn 4 đủ 4 loại slime mỗi loại một tinh anh; goal/tip từng màn viết lại. Ngoại lệ nhấc mèo khi đứng hình ghi vào AGENTS.md.
- **Kiểm chứng:** C# 1533, WPF 839/839 lúc đó.

## #15 — Đẩy hồi 3 giây thay vì 5 giây (2026-10-24, commit cb77240)
- **Triệu chứng:** người dùng nhớ Đẩy hồi 5 s, game đang 3 s.
- **Nguyên nhân:** mốc cân bằng 1 (5c1efcc) hạ 5 → 3 s.
- **Sửa:** trả về 5 s (battle.json `pushCooldown`, mặc định code), cập nhật mô tả/test.

## #14 — Trận "pause bất thường" và chậm (slow down) khi đang chơi (2026-10-24, commit 9fe79b2, rồi thay bằng cb77240)
- **Triệu chứng:** đang đánh thì trận như khựng, chậm hẳn.
- **Bằng chứng:** log 16:10 trận trùm: 8 bài hướng dẫn tại chỗ (target, lift, infected, ads, heal, app, stun, loading) nổ trong 47 s đầu.
- **Nguyên nhân:** mỗi bài làm trận chạy ×0,25 tới 6 s (mèo vẫn chạy tốc độ thường) và các bài nối nhau; bài chạy ở mọi chế độ; hết giờ vẫn tính đã học.
- **Sửa:** 9fe79b2 bỏ làm chậm, chỉ Phiêu lưu, giãn 10 s; sau đó cb77240 thay hẳn bằng Hướng dẫn phiêu lưu (#16).

---

## #13 — Máy đã cài báo "Bạn đang dùng bản mới nhất" dù đã có bản mới (sửa ở 1.4.3, 2026-10-23)

**Triệu chứng**
- Đã phát hành 1.4.2 lên GitHub, nhưng máy khác (đã cài 1.4.1, mạng khác máy dev) bấm khay → "Kiểm tra cập nhật" vẫn báo "Bạn đang dùng bản mới nhất." và không tải bản mới.

**Bằng chứng (log máy đó, `%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log`)**
```
21:32:34 Desktop Pet 1.4.1 (01ded63) started
21:32:36 Update check failed: Response status code does not indicate success: 403 (rate limit exceeded).
23:06:38 Update check failed: ... 403 (rate limit exceeded).
23:07:31 ... (khoảng 20 lần bấm liên tiếp, lần nào cũng 403)
```
Release 1.4.2 trên GitHub lúc đó hoàn toàn bình thường: Actions thành công, đủ 6 file, `releases.win.json` ghi 1.4.2.

**Nguyên nhân — hai lỗi chồng lên nhau**
1. **Hỏi qua API GitHub bị giới hạn lượt.** 1.4.0–1.4.2 dùng `GithubSource` của Velopack, hỏi `api.github.com` để lấy danh sách Release. API cho người không đăng nhập chỉ **60 lượt/giờ cho mỗi địa chỉ IP**, mỗi lần kiểm tra tốn vài lượt. Máy kia dùng mạng có **IP chung với nhiều người** (4G, wifi trường/công ty, nhà mạng gom khách vào một IP — CGNAT): người khác đã dùng hết lượt, nên ngay lần kiểm tra **đầu tiên** lúc mở app đã bị 403. Mỗi lần bấm lại tốn thêm lượt nên càng khó hồi. (Không phải do số máy cài: tải file bằng trình duyệt không tính lượt; mỗi mạng có hạn mức riêng. Sau đó `https://api.github.com/rate_limit` trên máy kia đã hồi 60/60.)
2. **App báo nhầm lỗi thành "mới nhất".** Trong `PetUpdates.Check`, khi kiểm tra lỗi, code gửi cùng tín hiệu với "không có bản mới" (`Changed(null)`), nên khay hiện "Bạn đang dùng bản mới nhất." — người dùng không biết là đang lỗi.

**Lỗi phụ cùng đợt: Actions không chạy khi đẩy 1.4.2.** Hướng dẫn cũ dùng `git tag v1.4.2` (tag thường) rồi `git push origin main --follow-tags`; `--follow-tags` chỉ đẩy tag *annotated*, nên chỉ `main` lên GitHub, workflow release (chạy theo tag `v*`) không chạy. Xử lý lúc đó: `git push origin v1.4.2`.

**Cách sửa (1.4.3, commit 7854009)**
- `src/DesktopPet/PetUpdates.cs`: nguồn cập nhật đổi từ API sang link tải thẳng của Release mới nhất `PetUpdates.FeedUrl` = `https://github.com/tnpt003/Pet-Computer/releases/latest/download/` (Velopack đọc `releases.win.json` rồi tải gói từ cùng chỗ). Đây là tải file thường, không bị giới hạn 60 lượt.
- Lỗi khi người dùng bấm kiểm tra → sự kiện `Failed` → khay (`App.xaml.cs`) báo "Chưa kiểm tra được cập nhật (lỗi mạng hoặc GitHub). Thử lại sau ít phút." (vi/en/ja). Trong 10 phút sau một lần lỗi (`RetryAfterFailure`), bấm lại chỉ báo như vậy, không hỏi GitHub.
- `docs/RELEASE.md`: tag dùng `git tag -a vX -m "..."`; thêm quy tắc giữ nguồn `latest/download` đúng (chỉ workflow tạo Release, Release khác để pre-release, bản vá nhánh cũ không đặt Latest, kiểm tra link `releases.win.json` sau mỗi lần phát hành).

**Kiểm chứng (thật, trên GitHub)** — harness giả lập máy đã cài (`TestVelopackLocator` + `Update.exe` thật):
- 1.4.1 → 1.4.2 qua link mới: tải delta 2,3 MB, ghép thành công.
- 1.4.0 → 1.4.2 qua link mới: delta không khớp → Velopack tự tải gói đầy đủ (~90 MB), vẫn lên được.
- Sau khi 1.4.3 lên GitHub: 1.4.2 cách cũ (API) thấy 1.4.3; 1.4.2 qua link mới tải delta 1.4.3 và ghép thành công; 1.4.3 báo "đã mới nhất".
- `scripts/test.ps1 -Ui`: Python OK, C# 51 suite, WPF 805/805 (check mới: FeedUrl, giữ 10 phút).
- **Chưa thử trọn vòng** trên máy đã cài: 1.4.3 tự cập nhật lên bản kế tiếp (kiểm chứng khi phát hành 1.4.4).

**Việc người dùng cần làm**
- Máy còn 1.4.0–1.4.2 vẫn hỏi qua API cũ cho tới khi lên 1.4.3: bấm "Kiểm tra cập nhật" **một lần** (không bấm liên tục); nếu vẫn báo "mới nhất" thì cài đè bằng Setup (không cần gỡ, dữ liệu giữ nguyên): https://github.com/tnpt003/Pet-Computer/releases/latest/download/PetComputer-win-Setup.exe
- Từ 1.4.3 trở đi: tự cập nhật lúc mở mèo và mỗi 6 giờ (nếu bật "Tự động tải và cài bản mới"); lỗi thì khay nói rõ là lỗi.

---

## #12 — Vòng Tab Lốc Xoáy bị con mèo che hết (2026-10-23, commit f6a35e7)
- **Triệu chứng:** trong trận, cả vòng tab của app "Tab Tornado" nằm sau lưng mèo, nửa phía trước cũng bị che.
- **Nguyên nhân:** dải chiến trường là một cửa sổ riêng, luôn nằm dưới cửa sổ mèo, nên mọi thứ vẽ trên dải đều ở sau mèo.
- **Sửa:** chia vòng làm hai nửa: dải trận vẽ nửa sau, cửa sổ mèo vẽ nửa trước đè lên mèo. Mèo bị nhấc/đang bay thì dải trận vẽ cả vòng như cũ. Bán kính vẽ giới hạn để nửa trước không bị cắt ở mép cửa sổ mèo.
- **Kiểm chứng:** test.ps1 -Ui đạt; đã xem mô phỏng trước khi làm. Chưa xem trên màn hình thật.

## #11 — Thanh âm lượng không có tác dụng; tiếng sau cắt ngang tiếng trước (2026-10-23, commit 62848ae)
- **Triệu chứng:** kéo thanh âm lượng mà tiếng không đổi.
- **Nguyên nhân:** (1) âm lượng chỉ áp dụng khi bấm Lưu (preferences của người dùng vẫn 0.7); (2) thang tuyến tính nên 50% nghe gần như 100%; (3) thời lượng cue tính theo 44100 Hz trong khi file WAV là 22050 Hz → cue sau cắt ngang cue trước.
- **Sửa:** kéo là áp dụng ngay (Hủy thì trả lại, 0% tắt luôn âm đang kêu); gain = âm lượng² theo tai người; thời lượng đọc byte rate từ header WAV.
- **Kiểm chứng:** check C# (thang âm lượng, thời lượng cue) và WPF (kéo áp dụng ngay, Hủy trả lại).

## #10 — Chữ trong trận bị con mèo che (2026-10-23, commit 62848ae)
- **Triệu chứng (ảnh người dùng):** nhãn tinh anh ("ĐÁNH NHANH"), số sát thương, CHẶN/MIỄN của slime đứng sát mèo bị thân mèo che.
- **Nguyên nhân:** như #12 — chữ vẽ trên dải trận, nằm dưới cửa sổ mèo.
- **Sửa:** `ClearOfCat` dời chữ ra sát cạnh thân mèo theo phía của nó.

## #9 — Tạm dừng trận nhưng mèo vẫn cử động và nhấc lên được (2026-10-23, commit 62848ae)
- **Triệu chứng:** Pause trận mà sao choáng, đầu, kiếm của mèo vẫn chạy; vẫn cầm mèo lên được.
- **Sửa:** khi trận bị đóng băng (mọi lý do trừ Away) dừng cả thời gian hoạt ảnh/động tác của mèo và chặn bắt đầu kéo. Thay quy tắc cũ (18/10) "kéo được mèo khi trận dừng" theo yêu cầu mới.
- **Kiểm chứng:** check WPF: pause đóng băng tư thế, không nhấc được, Tiếp tục thì nhấc lại được.

## #8 — Gỡ 1.4.0 để lại mục khởi động cùng Windows trỏ vào file đã xóa (2026-10-23, sửa ở 1.4.1)
- **Triệu chứng:** sau khi gỡ bản cài 1.4.0, Windows vẫn cố mở Desktop Pet lúc khởi động (mục Run còn sót).
- **Sửa:** hook gỡ cài đặt của Velopack xóa mục Run nếu nó trỏ tới chính bản đã cài (mục của bản copy thư mục khác giữ nguyên). Kèm theo: gỡ trong app chạy trình gỡ ở bước cuối khi thoát, để mèo tự đóng gọn (trước đó trình gỡ đóng mạnh mèo, log thiếu dòng "exited") — commit 01ded63.
- **Kiểm chứng:** cài/gỡ thật trên máy dev: gỡ 1.4.1 thì mục Run mất, dữ liệu 8 file còn nguyên. Máy đã cài 1.4.0 phải xóa mục Run bằng tay (Task Manager → Startup apps) hoặc lên 1.4.1 rồi mới gỡ.

## #7 — Cài Setup khi bản copy thư mục đang chạy: trông như không có gì xảy ra (2026-10-23, sửa ở 1.4.1)
- **Triệu chứng:** chạy Setup xong không thấy mèo mới mở.
- **Nguyên nhân:** khóa single-instance — bản copy đang chạy giữ khóa, bản vừa cài mở lên thấy đã có mèo nên tự thoát.
- **Sửa:** `PetHandover.cs`: lần chạy đầu sau khi cài, bản cũ được đóng bình thường (WM_CLOSE như bấm Thoát), chờ tối đa 10 s rồi bản cài giữ khóa.
- **Kiểm chứng:** cài thật: bản dev thoát code 0, bản cài mở sau ~0,05 s.

## #6 — Đang choáng mà rê chuột nhanh qua mèo vẫn hiện mặt Ngạc nhiên (2026-10-22, commit 77f67f3)
- **Nguyên nhân:** bộ cảm xúc không coi trạng thái choáng (Bonk, khi món trưng bày rơi trúng đầu) là "bận".
- **Sửa:** `PetEmotions.Step` coi `Bonk > 0` là bận: không Ngạc nhiên, không cảm xúc khác trong lúc thấy sao.
- **Kiểm chứng:** test C#: đối chứng có Surprised khi không choáng, đang choáng thì không.

## #5 — Thẻ trạng thái mở rất xa mèo và không kéo được (2026-10-21, commit 3a751ea)
- **Triệu chứng (ảnh người dùng):** bấm Trạng thái, thẻ hiện ở chỗ khác hẳn trên màn hình.
- **Nguyên nhân:** thẻ gán thẳng Left/Top của WPF, mà cửa sổ mới hiểu tọa độ theo DPI khác với tọa độ của mèo (DIP theo scale màn hình mèo đang đứng).
- **Sửa:** đo kích thước theo DIP, đặt vị trí bằng pixel theo scale của mèo (như thông báo trận); kéo được bằng chỗ trống trên thẻ.
- **Kiểm chứng:** check WPF: thẻ nằm ngay trên đầu mèo tính bằng pixel ở scale thật và scale giả 150%. Chưa thử trên màn hình phụ thật.

## #4 — Đòn combo vẫn đẩy lùi slime đá tinh anh "miễn Đẩy" (2026-10-21, commit 6d0c666)
- **Nguyên nhân:** "miễn Đẩy" lúc đầu chỉ áp dụng cho kỹ năng Đẩy; đòn đánh thường, búa, thẻ Đẩy mạnh vẫn đẩy lùi.
- **Sửa (người dùng duyệt mở rộng):** mọi đòn trúng không đẩy lùi tinh anh miễn Đẩy, sát thương giữ nguyên.
- **Kiểm chứng:** test C#: combo lệch 0 DIP nhưng vẫn mất máu; tinh anh thường bị đẩy 44 DIP.

## #3 — Banner trận tràn sang thanh HP và ô kỹ năng (2026-10-21, commit c66ba1d)
- **Triệu chứng (ảnh người dùng):** "Expedition · Step 1/6 · Shortcut" quá dài so với cột giữa HUD (230 DIP).
- **Sửa:** banner nằm trong Viewbox chỉ thu nhỏ — dài thì tự nhỏ lại, HUD không đổi kích thước; banner Viễn chinh rút gọn thành "Bậc 1/6 · <tên>".
- **Kiểm chứng:** check WPF banner dài thu vào ≤ 230 DIP; đã xem ảnh render.

## #2 — Tư thế trèo cửa sổ trông như mèo đứng lơ lửng vẫy tay (2026-10-15)
- **Triệu chứng (ảnh người dùng):** mèo leo cạnh cửa sổ không giống đang trèo.
- **Sửa:** vẽ lại tư thế Climbing: thân kéo dài và nghiêng, hai chân trước bám qua mép (có móng), chân sau đạp vào cạnh, đuôi rủ, mắt nhìn lên. Chỉ đổi phần vẽ, physics giữ nguyên. (Ảnh "mèo không có chân trước" lúc đó là bản publish cũ.)
- **Kiểm chứng:** check hình 6 khung × 2 hướng × có/không mũ nằm trong cửa sổ; đã xem ảnh cảnh trèo.

## #1 — Mèo giật hình khi bị ném và khi đi (2026-10-09)
- **Nguyên nhân:** timer chạy không đều, cửa sổ được dời hai bước (Left rồi Top) nên có khung hình lệch.
- **Sửa:** nhịp ~60 FPS đều; nội suy vị trí vẽ giữa hai bước physics 1/120 s; dời cửa sổ bằng một lệnh SetWindowPos. Physics, lực ném, trọng lực, độ nảy, FPS mục tiêu không đổi. Debug view hiện FPS và khoảng hở dài nhất.

---

# B. Lỗi do test, mô phỏng hoặc review tìm ra

| Ngày | Lỗi | Tìm bởi | Sửa |
|---|---|---|---|
| 2026-11-06 | Review bản tích hợp: lứa cuối tốn máu khi đầy ổ/con; vòng báo sóng 27 nhưng hit 38; Nộ vẫn bị đẩy và không giải cú trượt; nhánh boss làm đổi gộp vũng của quái thường | Test tái hiện BossTacticsTests, ma trận trạng thái, render WPF, mô phỏng đối chứng 0172091 | Merge ac93f41: chỉ tiêu máu khi đặt được ổ; dùng chung hình học có tune/Validate; chặn/giải Shove khi Nộ; giữ gộp vũng thường. Python 58 + 5, C# 3170, WPF 1418 đạt; publish sạch, smoke 3/3 PASS. Perf 51,8 FPS dưới ~57, còn test tay/profile trước M30; docs/STANDARDIZATION-1.5.md |
| 2026-11-05 | Nâng cấp hai boss: ổ bị kẹt Spawning, choáng ổ không hết, gọi con khi sân trống làm mẹ dễ hơn; số đếm ổ nhạt trên nền sáng | BossTacticsTests, AdventureBalanceTests, render WPF | Commit 9477828, nhánh feature/mother-mutant-mechanics: hoàn tất spawn, giảm stun timer của ổ, bỏ lượt gọi rỗng, huy hiệu tối cho số. Python 58 + 5, C# 3093, WPF 1412 đạt; publish sạch, smoke 3/3 PASS; perf/giới hạn: docs/BALANCE-BOSS-TACTICS.md. Còn test tay, chưa merge main |
| 2026-11-04 | Đợt C2 làm lệch cân bằng: bot mô phỏng chưa bao giờ bấm nộ, giờ nộ tự kích hoạt → màn 4 chương 1 kiếm 96% HP, màn 3 chương 2 96%; Mai rùa và lịch app độc màn 3 hỏng vì mèo nộ chặn cú đánh/choáng trong test | AdventureBalanceTests, EnemyGachaTests, GuideStageTests | Nạp nộ chậm lại: `rageHitGain` 0,05 → 0,02, `rageHurtGain` 0,012 → 0,006 (khoảng 50 nhát mới đầy, bù lại vì nộ tự dùng không bao giờ phí), `rageSeconds` 6 → 5. Thử tắt nộ tự động thì màn 3 chương 2 vẫn 93% (Cú đáp địa chấn mạnh hơn), tăng `threat` màn 3 thì mức Khó cầm kiếm tụt 45% → giữ dữ liệu màn 3 như C1, nới hai ngưỡng do agent đặt: màn 3 chương 2 ≤ 95% (như màn 2), màn 4 chương 1 kiếm ≤ 93%. Ngưỡng 88% cộng hưởng màn 3 (người dùng đặt) giữ nguyên, đạt |
| 2026-11-04 | Đợt C1 làm lệch cân bằng chương 2: màn 5 búa còn thắng 70% (cần ≥80%), màn 2/3 kiếm còn 95%/93% HP (cần ≤95/≤90%), người chơi đứng im thắng màn 5 chậm (đến 372 s) vì combo giờ tự chọc vỡ bong bóng | AdventureBalanceTests (so với worktree HEAD trước C1) | Thử tắt từng thay đổi: quảng cáo, bám tường, bình rớt không phải thủ phạm chính; chỉnh dữ liệu: màn 2 `threat` 2,2→2,3, màn 3 `threatGrowth` 0,5→0,8, màn 5 `threat` 1,4→1,25; kiểm tra "≤ 4 phút" của chương 2 bỏ người chơi đứng im như chương 1 (M25). Chọn quái quảng cáo dùng luồng random riêng `Popup` (không xáo trộn app). Thêm trần `adCount` slime từ quảng cáo mỗi đợt |
| 2026-11-04 | battle.json gốc bị từ chối âm thầm sau khi thêm `popupKinds` (slime phun là `support`) → mọi test app đọc dữ liệu built-in | AppTests | Luật `popupKinds` chỉ cấm trùm, liên kết, ảo ảnh, hẹn giờ, tách đôi; mẹo màn c2_slime_swamp cũng vượt 400 ký tự sau khi đổi tên bình xịt, đã rút gọn |
| 2026-10-24 | Lịch app độc (`malwarePlan`/`malwareFrom`) không chạy ở màn thường: đếm bằng `FightSeconds`, giá trị chỉ ghi lúc trận kết thúc | Test virus.exe của hướng dẫn (UiChecks) | Màn thường đếm `Time` (màn sống sót vẫn theo đồng hồ) — ec27b0a |
| 2026-10-24 | Test tâm chỗ sáng của mèo PASS dù lệch 40 DIP: test đo cùng gốc sai với code | Người dùng gửi ảnh (#19) | Test so thêm với `pet.X` (độc lập) — 1d07490 |
| 2026-10-24 | Một seed màn 3 không ra "Cập nhật bắt buộc": không có chỗ đặt tường thì game đổi sang vòng loading | GuideStageTests | Lịch chờ tới khi đặt được tường (`ThrowUpdate(orLoading: false)`) — cb77240 |
| 2026-10-24 | battle.json gốc bị từ chối (goal > 160 ký tự) → test cân bằng không thấy chương | Test C# | Rút gọn goal; đo độ dài trước khi sửa dữ liệu — cb77240 |
| 2026-10-23 | Lên bản 1.4.2: `git tag` thường + `--follow-tags` không đẩy tag → GitHub Actions không chạy | Người dùng thấy Release không lên | Tài liệu dùng `git tag -a`; lần đó đẩy riêng `git push origin v1.4.2` |
| 2026-10-23 | Câu nhắc nghỉ dựng sẵn trong C# lệch với ai/lines.json (dòng thứ hai vi/en) | Test đối chiếu mới (S5) | Sửa cho khớp lines.json |
| 2026-10-23 | Thuộc tính `PetWindow.Language` che `FrameworkElement.Language` của WPF | Lúc làm hộp chào mừng | Đổi tên `UiLanguage` |
| 2026-10-23 | PowerShell 5.1 đọc .ps1 không BOM theo ANSI → hướng dẫn cài tiếng Việt trong ghi chú Release hỏng dấu | Đóng gói thử | Tách sang `scripts/release-install.md` đọc bằng UTF-8 |
| 2026-10-22 | `battle-records.json` chứa `null` làm app ném lỗi không bắt | Viết JsonStore (S2) | Đọc `null` → bắt đầu lại, ghi log |
| 2026-10-22 | Tết 2030 ghi 3/2 (đúng 2/2), Trung thu 2034 ghi 27/9 (đúng 26/9) | Nối bảng ngày lễ tới 2050 | Sửa ngày |
| 2026-10-22 | Quả bóng đồ chơi lấy focus và hiện trong Alt+Tab | Rà trước 1.4.0 | Cửa sổ bóng không kích hoạt, ẩn khỏi Alt+Tab |
| 2026-10-22 | Món đồ đáp lên mép cửa sổ không bẹp/nảy | Làm tính năng đệm slime | Chỉ coi "đang đứng" khi VY == 0 và chạm mép |
| 2026-10-22 | Bảng chuẩn bị màn 2 cao 519 DIP, tràn màn hình thấp (480 DIP) | Check đo chiều cao panel | Giới hạn theo màn hình + cuộn, nút luôn ở dưới |
| 2026-10-21 | Bản đồ Viễn chinh cao ~574 DIP, tràn ở 1366×768 125%, 1280×720 150%… | Test phát hành Viễn chinh | Giới hạn theo màn hình + cuộn (người dùng chọn) |
| 2026-10-20 | Vua Slime lùi về phía mèo đang đứng (hướng lui tính sai) | Review chiến thuật quái | Vua nhảy về mép xa mèo |
| 2026-10-20 | Slime đá chặn đường thành bức tường che cho mèo (quái khác không đi qua) | Mô phỏng cân bằng | Quái chắn chỉ chặn mèo, không chặn đồng đội |
| 2026-10-20 | Kế hoạch "đòn trùng lúc nhấc mèo luôn hụt" khiến nhấc/thả liên tục thành bất tử | Test cứu mèo | Chỉ hụt khi mèo đang được nhấc; bảo vệ tiếp đất cần nhấc ≥ 0,3 s |
| 2026-10-15 | Chế độ Quiet không gửi quan sát cho AI → nhật ký/nhu cầu/câu phản ứng bị cũ | Review của Codex | Quiet vẫn gửi quan sát nhưng từ chối mọi lệnh |
| 2026-10-15 | Thức dậy bị kéo dài (1,2 s → 2,6 s) khi click/chuột phải/Quiet/fullscreen giữa lúc đang thức | Review của Codex + test WPF | Reset giữ bộ đếm khi đang Waking |
| 2026-10-15 | Đổi ngôn ngữ xong vẫn hiện câu nói ngôn ngữ cũ (phản hồi AI đến muộn) | Review của Codex | Ghi ngôn ngữ lúc gửi, bỏ câu nếu đã đổi |
| 2026-10-13 | Bấm Làm ngay khi đang thức dậy kéo dài waking 1,2 s → 2 s | Test WPF | Chỉ hủy hoạt động khi lệnh thật sự bắt đầu |
| 2026-10-13 | Cửa sổ cài đặt cao quá màn hình của mèo | Rà soát tối ưu | Giới hạn chiều cao theo màn hình + cuộn |
| 2026-10-09 | Tài liệu/PROGRESS.md hỏng dấu tiếng Việt | Đọc lại tài liệu | Sửa lại UTF-8 |
