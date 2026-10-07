---
description: Quy chuẩn Git Workflow, Commit tiếng Anh và Huấn luyện AI Service cho EcoLink
globs: ["src/Ecolink.AiService/**/*", "**/*"]
---

# Quy chuẩn Git Workflow & Phân hệ AI cho EcoLink

1. **Git Commit:**
   - Luôn sử dụng chuẩn Conventional Commits 1.0.0 bằng Tiếng Anh: `<type>(<scope>): <subject>`.
   - Types: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore`.
   - Scopes: `trade`, `ai`, `pay`, `gam`, `auth`, `db`, `infra`.
   - Viết ở thì hiện tại mệnh lệnh (Imperative mood), không viết hoa chữ cái đầu, không dấu chấm cuối câu.

2. **Phân hệ AI (FastAPI & YOLOv8):**
   - Áp dụng Two-Stage Pipeline (Detection crop 5% padding -> Classification).
   - Đảm bảo cơ chế phát hiện ảnh Out-of-Distribution (OOD Threshold < 0.50).
   - Tuyệt đối không commit tệp thử nghiệm trong thư mục `runs/` vào Git.

Chi tiết xem tại:
- `documents/guideline/git_workflow_and_commit_guidelines.md`
- `documents/guideline/ai_service_and_benchmark_guidelines.md`
