import os
import sys
import copy
import time
from pathlib import Path

# Đảm bảo in UTF-8 không lỗi trên Windows
if sys.stdout and hasattr(sys.stdout, 'reconfigure'):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import DataLoader
from torchvision import datasets, transforms, models

DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")
BASE_DIR = Path(__file__).resolve().parent
DATA_DIR = BASE_DIR / "data" / "trashnet_split"
MODELS_DIR = BASE_DIR / "models"
MODELS_DIR.mkdir(parents=True, exist_ok=True)

# 1. Pipeline Tăng cường Dữ liệu (Data Augmentation)
data_transforms = {
    'train': transforms.Compose([
        transforms.RandomResizedCrop(224, scale=(0.8, 1.0)),
        transforms.RandomHorizontalFlip(p=0.5),
        transforms.RandomRotation(degrees=15),
        transforms.ColorJitter(brightness=0.2, contrast=0.2, saturation=0.2),
        transforms.ToTensor(),
        transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225])
    ]),
    'val': transforms.Compose([
        transforms.Resize(256),
        transforms.CenterCrop(224),
        transforms.ToTensor(),
        transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225])
    ]),
}

def train_model(model, dataloaders, dataset_sizes, class_names, criterion, optimizer, scheduler, num_epochs=10, save_name="model.pth"):
    since = time.time()
    best_model_wts = copy.deepcopy(model.state_dict())
    best_acc = 0.0

    print(f"[*] Bắt đầu huấn luyện {save_name} trong {num_epochs} epochs...", flush=True)

    for epoch in range(num_epochs):
        epoch_start = time.time()
        print(f"\n--- Epoch {epoch + 1}/{num_epochs} ---", flush=True)

        for phase in ['train', 'val']:
            if phase == 'train':
                model.train()
            else:
                model.eval()

            running_loss = 0.0
            running_corrects = 0
            batch_count = len(dataloaders[phase])

            for b_idx, (inputs, labels) in enumerate(dataloaders[phase]):
                inputs = inputs.to(DEVICE)
                labels = labels.to(DEVICE)

                optimizer.zero_grad()

                with torch.set_grad_enabled(phase == 'train'):
                    outputs = model(inputs)
                    _, preds = torch.max(outputs, 1)
                    loss = criterion(outputs, labels)

                    if phase == 'train':
                        loss.backward()
                        optimizer.step()

                running_loss += loss.item() * inputs.size(0)
                running_corrects += torch.sum(preds == labels.data)

                if phase == 'train' and (b_idx + 1) % 20 == 0:
                    current_loss = running_loss / ((b_idx + 1) * inputs.size(0))
                    print(f"  [Train] Batch {b_idx + 1}/{batch_count} | Loss tạm thời: {current_loss:.4f}", flush=True)

            if phase == 'train' and scheduler:
                scheduler.step()

            epoch_loss = running_loss / dataset_sizes[phase]
            epoch_acc = running_corrects.double() / dataset_sizes[phase]

            print(f"[{phase.upper()}] Loss: {epoch_loss:.4f} | Acc: {epoch_acc * 100:.2f}%", flush=True)

            # Lưu trọng số tốt nhất theo validation accuracy
            if phase == 'val' and epoch_acc > best_acc:
                best_acc = epoch_acc
                best_model_wts = copy.deepcopy(model.state_dict())
                save_path = MODELS_DIR / save_name
                torch.save({
                    'model_state_dict': best_model_wts,
                    'classes': class_names,
                    'val_acc': float(best_acc),
                    'epoch': epoch + 1
                }, save_path)
                print(f"  --> [SAVED] Đã lưu model tốt nhất ({best_acc * 100:.2f}%) vào {save_path.name}", flush=True)

        print(f"Thời gian epoch: {time.time() - epoch_start:.1f}s", flush=True)

    time_elapsed = time.time() - since
    print(f"\n[+] Huấn luyện xong trong {time_elapsed // 60:.0f}m {time_elapsed % 60:.0f}s", flush=True)
    print(f"[+] Độ chính xác tốt nhất (Best Val Acc): {best_acc * 100:.2f}%", flush=True)
    return best_acc

def main():
    print(f"[*] Thiết bị huấn luyện: {DEVICE}", flush=True)
    
    # num_workers=0 tối ưu hoàn hảo trên Windows CPU, tránh lỗi multiprocessing
    image_datasets = {
        x: datasets.ImageFolder(str(DATA_DIR / x), data_transforms[x])
        for x in ['train', 'val']
    }
    dataloaders = {
        x: DataLoader(image_datasets[x], batch_size=32, shuffle=(x == 'train'), num_workers=0)
        for x in ['train', 'val']
    }
    dataset_sizes = {x: len(image_datasets[x]) for x in ['train', 'val']}
    class_names = image_datasets['train'].classes
    num_classes = len(class_names)

    print(f"[*] Kích thước tập dữ liệu: Train = {dataset_sizes['train']}, Val = {dataset_sizes['val']}", flush=True)
    print(f"[*] 6 nhóm rác: {class_names}", flush=True)

    # 1. Huấn luyện MobileNetV3-Small (10 epochs)
    print("\n" + "=" * 60, flush=True)
    print("GIAI DOAN 1: HUAN LUYEN MOBILENET-V3-SMALL", flush=True)
    print("=" * 60, flush=True)
    model_mobile = models.mobilenet_v3_small(weights=models.MobileNet_V3_Small_Weights.DEFAULT)
    in_features_mobile = model_mobile.classifier[3].in_features
    model_mobile.classifier[3] = nn.Linear(in_features_mobile, num_classes)
    model_mobile = model_mobile.to(DEVICE)

    criterion = nn.CrossEntropyLoss(label_smoothing=0.1)
    optimizer_mobile = optim.AdamW(model_mobile.parameters(), lr=5e-4, weight_decay=1e-2)
    scheduler_mobile = optim.lr_scheduler.CosineAnnealingLR(optimizer_mobile, T_max=10)

    train_model(model_mobile, dataloaders, dataset_sizes, class_names, criterion, optimizer_mobile, scheduler_mobile, num_epochs=10, save_name="mobilenet_v3_best.pth")

    # 2. Huấn luyện EfficientNet-B0 (10 epochs)
    print("\n" + "=" * 60, flush=True)
    print("GIAI DOAN 2: HUAN LUYEN EFFICIENTNET-B0", flush=True)
    print("=" * 60, flush=True)
    model_eff = models.efficientnet_b0(weights=models.EfficientNet_B0_Weights.DEFAULT)
    in_features_eff = model_eff.classifier[1].in_features
    model_eff.classifier[1] = nn.Linear(in_features_eff, num_classes)
    model_eff = model_eff.to(DEVICE)

    optimizer_eff = optim.AdamW(model_eff.parameters(), lr=4e-4, weight_decay=1e-2)
    scheduler_eff = optim.lr_scheduler.CosineAnnealingLR(optimizer_eff, T_max=10)

    train_model(model_eff, dataloaders, dataset_sizes, class_names, criterion, optimizer_eff, scheduler_eff, num_epochs=10, save_name="efficientnet_b0_best.pth")

    print("\n" + "=" * 60, flush=True)
    print("[SUCCESS] ĐÃ HUẤN LUYỆN XONG CẢ 2 MÔ HÌNH VÀ LƯU VÀO THƯ MỤC models/!", flush=True)
    print("=" * 60, flush=True)

if __name__ == "__main__":
    main()
