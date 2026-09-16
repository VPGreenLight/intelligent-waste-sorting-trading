import sys
from pathlib import Path
from ultralytics import YOLO

# Bảng quy tắc phân loại rác (Rule-based mapping theo Usecase 3)
WASTE_RULES = {
    "plastic": {
        "vn_name": "Rác Tái Chế - Nhựa",
        "bin_color": "Màu Vàng / Trắng",
        "instruction": "Tráng sạch nước/dầu thừa, tháo nắp chai, ép xẹp chai nhựa để tiết kiệm diện tích."
    },
    "paper": {
        "vn_name": "Rác Tái Chế - Giấy",
        "bin_color": "Màu Xanh Dương / Trắng",
        "instruction": "Giữ khô ráo, không để dính dầu mỡ thực phẩm. Gấp gọn trước khi bỏ thùng."
    },
    "cardboard": {
        "vn_name": "Rác Tái Chế - Bìa Carton",
        "bin_color": "Màu Xanh Dương / Trắng",
        "instruction": "Gỡ bỏ băng dính bọc ngoài, làm phẳng/gấp gọn hộp trước khi tái chế."
    },
    "metal": {
        "vn_name": "Rác Tái Chế - Kim Loại",
        "bin_color": "Màu Trắng / Xám",
        "instruction": "Rửa sạch cặn đồ uống trong lon, bóp bẹp lon nhôm nếu có thể."
    },
    "glass": {
        "vn_name": "Rác Tái Chế - Thủy Tinh",
        "bin_color": "Màu Trắng / Xanh Lá",
        "instruction": "Cẩn thận tránh làm vỡ. Rửa sạch chai lọ, bỏ riêng nắp kim loại/nhựa."
    },
    "trash": {
        "vn_name": "Rác Thải Sinh Hoạt / Khác",
        "bin_color": "Màu Đen / Xám Đậm",
        "instruction": "Rác không thể tái chế. Bọc kín túi rác và bỏ vào thùng rác vô cơ sinh hoạt."
    }
}

def predict(image_path, model_path=None):
    # Các vị trí có thể có của file best.pt
    candidates = [
        model_path,
        "runs/classify/runs/classify/waste_yolov8n/weights/best.pt",
        "runs/classify/waste_yolov8n/weights/best.pt",
        "models/best.pt",
        "best.pt",
        "yolov8n-cls.pt"
    ]
    resolved_model = None
    for cand in candidates:
        if cand and Path(cand).exists():
            resolved_model = cand
            break

    if not resolved_model:
        resolved_model = "yolov8n-cls.pt"
        print(f"[!] Không tìm thấy mô hình train, sử dụng mặc định '{resolved_model}'")
    else:
        print(f"[+] Sử dụng mô hình: {resolved_model}")

    print(f"[*] Đang nhận diện ảnh: {image_path}")
    model = YOLO(resolved_model)
    results = model.predict(source=image_path, imgsz=224, verbose=False)

    for result in results:
        top1_idx = result.probs.top1
        top1_name = result.names[top1_idx]
        confidence = result.probs.top1conf.item()

        print("\n" + "="*50)
        # Usecase 2: Out-of-Distribution Handling (< 60% confidence)
        if confidence < 0.60:
            print("[CẢNH BÁO - OUT OF DISTRIBUTION]")
            print(f"Độ tự tin quá thấp ({confidence*100:.1f}%). AI không chắc chắn về vật thể này!")
            print("Khuyến nghị: Vui lòng chụp lại ảnh với góc sáng rõ ràng hơn.")
        else:
            rule = WASTE_RULES.get(top1_name.lower(), {})
            print(f"Loại rác nhận diện: {top1_name.upper()} ({rule.get('vn_name', '')})")
            print(f"Độ tin cậy:        {confidence*100:.2f}%")
            print(f"Thùng rác chỉ định: {rule.get('bin_color', 'N/A')}")
            print(f"Hướng dẫn xử lý:   {rule.get('instruction', 'N/A')}")
        print("="*50)

        print("\nTop các xác suất cao nhất:")
        for idx in result.probs.top5[:3]:
            name = result.names[idx]
            conf = result.probs.data[idx].item()
            print(f"  - {name:<12}: {conf*100:.2f}%")

if __name__ == "__main__":
    test_target = sys.argv[1] if len(sys.argv) > 1 else "data/trashnet_split/val/plastic"
    p = Path(test_target)
    if p.is_dir():
        imgs = list(p.glob("*.jpg")) + list(p.glob("*.png"))
        if imgs:
            test_target = str(imgs[0])
    predict(test_target)
