from pathlib import Path
from ultralytics import YOLO

def train():
    # Load mô hình YOLOv8 Nano Classification
    # Ultralytics sẽ tự động tải file trọng số 'yolov8n-cls.pt' về nếu chưa có
    model = YOLO("yolov8n-cls.pt")

    # Đường dẫn tuyệt đối tới tập dữ liệu đã chia train/val
    dataset_path = Path("data/trashnet_split").resolve()
    print(f"[*] Bắt đầu huấn luyện với tập dữ liệu: {dataset_path}")

    # Huấn luyện mô hình
    results = model.train(
        data=str(dataset_path),
        epochs=15,          # 15 epoch là phù hợp và nhanh cho bản demo
        imgsz=224,          # Kích thước ảnh chuẩn 224x224 cho classification
        batch=16,           # Batch size phù hợp cho RAM CPU
        workers=2,
        project="runs/classify",
        name="waste_yolov8n",
        exist_ok=True
    )

    print("\n[+] Huấn luyện hoàn tất!")
    print("[+] Trọng số tốt nhất đã được lưu tại: runs/classify/waste_yolov8n/weights/best.pt")

if __name__ == "__main__":
    train()
