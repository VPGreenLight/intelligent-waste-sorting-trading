import os
import shutil
import random
from pathlib import Path

def split_dataset(
    source_dir="data/dataset-resized",
    target_dir="data/trashnet_split",
    train_ratio=0.8,
    val_ratio=0.2,
    seed=42
):
    random.seed(seed)
    source_path = Path(source_dir)
    target_path = Path(target_dir)

    if not source_path.exists():
        print(f"Error: Source directory '{source_dir}' does not exist.")
        return

    # Categories
    categories = [d.name for d in source_path.iterdir() if d.is_dir() and not d.name.startswith('.')]
    print(f"Found {len(categories)} categories: {categories}")

    # Create target directories
    splits = ['train', 'val']
    for s in splits:
        for cat in categories:
            (target_path / s / cat).mkdir(parents=True, exist_ok=True)

    summary = {}

    for cat in categories:
        cat_dir = source_path / cat
        # Supported image formats
        images = [f for f in cat_dir.iterdir() if f.is_file() and f.suffix.lower() in ['.jpg', '.jpeg', '.png', '.bmp', '.webp']]
        random.shuffle(images)

        total = len(images)
        train_count = int(total * train_ratio)
        val_images = images[train_count:]
        train_images = images[:train_count]

        summary[cat] = {
            'total': total,
            'train': len(train_images),
            'val': len(val_images)
        }

        # Copy files to train
        for img in train_images:
            shutil.copy2(img, target_path / 'train' / cat / img.name)

        # Copy files to val
        for img in val_images:
            shutil.copy2(img, target_path / 'val' / cat / img.name)

    print("\n=== DATASET SPLIT SUMMARY ===")
    total_train = 0
    total_val = 0
    total_all = 0

    print(f"{'Category':<15} | {'Total':<8} | {'Train (80%)':<12} | {'Val (20%)':<10}")
    print("-" * 52)
    for cat, counts in summary.items():
        print(f"{cat:<15} | {counts['total']:<8} | {counts['train']:<12} | {counts['val']:<10}")
        total_train += counts['train']
        total_val += counts['val']
        total_all += counts['total']

    print("-" * 52)
    print(f"{'TOTAL':<15} | {total_all:<8} | {total_train:<12} | {total_val:<10}")
    print(f"\nSuccessfully split dataset into: {target_path.resolve()}")

if __name__ == "__main__":
    split_dataset()
