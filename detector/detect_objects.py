"""
Object detection script using YOLO from Ultralytics.
Detects people, vehicles, and animals in images and outputs results to CSV.
"""

import argparse
import csv
import glob
import os
import sys
from pathlib import Path
from typing import Set, List, Tuple
from ultralytics import YOLO


# Object class mappings from COCO dataset
PEOPLE_CLASSES = {0}  # person

VEHICLE_CLASSES = {
    1: "bicycle",
    2: "car",
    3: "motorcycle",
    4: "airplane",
    5: "bus",
    6: "train",
    7: "truck",
    8: "boat"
}

ANIMAL_CLASSES = {
    14: "bird",
    15: "cat",
    16: "dog",
    17: "horse",
    18: "sheep",
    19: "cow",
    20: "elephant",
    21: "bear",
    22: "zebra",
    23: "giraffe"
}


def detect_objects_in_image(model: YOLO, image_path: str) -> Set[str]:
    """
    Run YOLO detection on a single image and return formatted tags.

    Args:
        model: YOLO model instance
        image_path: Path to the image file

    Returns:
        Set of tags found in the image
    """
    tags = set()

    try:
        results = model(image_path, verbose=False)

        # Get detected class IDs
        for result in results:
            if result.boxes is not None:
                class_ids = result.boxes.cls.cpu().numpy().astype(int)

                for class_id in class_ids:
                    if class_id in PEOPLE_CLASSES:
                        tags.add("People")
                    elif class_id in VEHICLE_CLASSES:
                        vehicle_name = VEHICLE_CLASSES[class_id]
                        tags.add(f"Other|vehicles|{vehicle_name}")
                    elif class_id in ANIMAL_CLASSES:
                        animal_name = ANIMAL_CLASSES[class_id]
                        tags.add(f"Other|animals|{animal_name}")

    except Exception as e:
        print(f"Error processing {image_path}: {e}", file=sys.stderr)

    return tags


def write_results_to_csv(results: List[Tuple[str, str]], output_file: str) -> None:
    """
    Write detection results to CSV file.

    Args:
        results: List of tuples (image_path, tags_string)
        output_file: Path to output CSV file
    """
    with open(output_file, 'w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        for image_path, tags in results:
            writer.writerow([image_path, tags])


def main() -> int:
    parser = argparse.ArgumentParser(description='Detect objects in images using YOLO')
    parser.add_argument('image_pattern', help='Image file pattern (e.g., c:\\pictures\\*.jpg)')
    parser.add_argument('--output', '-o', default='detection_results.csv',
                        help='Output CSV file (default: detection_results.csv)')

    args = parser.parse_args()

    # Get list of image files
    image_files = glob.glob(args.image_pattern)

    if not image_files:
        print(f"No images found matching pattern: {args.image_pattern}", file=sys.stderr)
        return 1

    print(f"Found {len(image_files)} images to process")

    # Load YOLO model
    print("Loading YOLO model: yolov8n.pt")
    model = YOLO('yolov8n.pt')

    # Process images
    results = []
    processed_count = 0

    for i, image_path in enumerate(image_files, 1):
        abs_path = os.path.abspath(image_path)
        tags = detect_objects_in_image(model, image_path)
        tags_string = '^'.join(sorted(tags)) if tags else ''

        results.append((abs_path, tags_string))
        processed_count += 1

        # Rewrite CSV every 100 images
        if processed_count % 100 == 0:
            write_results_to_csv(results, args.output)
            print(f"Processed {processed_count}/{len(image_files)} images (saved checkpoint)")

    # Write final results
    write_results_to_csv(results, args.output)
    print(f"Completed! Processed {processed_count} images. Results saved to {args.output}")

    return 0


if __name__ == '__main__':
    sys.exit(main())
