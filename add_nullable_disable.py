import os
import sys
from pathlib import Path

def add_nullable_disable(file_path):
    """Add #nullable disable to the top of a C# file if not already present."""
    # print(file_path)
    # return True
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Check if #nullable disable is already at the top
        if content.strip().startswith('#nullable disable'):
            return False

        # Add #nullable disable at the top
        new_content = '#nullable disable\n' + content

        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)

        return True
    except Exception as e:
        print(f"Error processing {file_path}: {e}")
        return False

def main():
    # Get the current directory
    root_dir = Path('.')

    # Find all .cs files recursively
    cs_files = list(root_dir.rglob('*.cs'))

    if not cs_files:
        print("No .cs files found.")
        return

    print(f"Found {len(cs_files)} .cs files")
    print()

    modified_count = 0
    skipped_count = 0

    for cs_file in cs_files:
        if add_nullable_disable(cs_file):
            print(f"Modified: {cs_file}")
            modified_count += 1
        else:
            print(f"Skipped (already has #nullable disable): {cs_file}")
            skipped_count += 1

    print()
    print(f"Summary:")
    print(f"  Modified: {modified_count} files")
    print(f"  Skipped: {skipped_count} files")
    print(f"  Total: {len(cs_files)} files")

if __name__ == '__main__':
    main()
