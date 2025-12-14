import os
import sys
from pathlib import Path

def add_nullable_disable(file_path):
    """Add #nullable disable to the top of a C# file if not already present."""
    try:
        # Read in binary mode to detect BOM
        with open(file_path, 'rb') as f:
            raw_content = f.read()

        # Check if file has UTF-8 BOM (EF BB BF)
        has_bom = raw_content.startswith(b'\xef\xbb\xbf')

        # Use appropriate encoding based on BOM presence
        encoding = 'utf-8-sig' if has_bom else 'utf-8'

        # Decode content
        content = raw_content.decode(encoding)

        # Check if #nullable disable is already at the top
        if content.strip().startswith('#nullable disable'):
            return False

        # Detect line ending style (Windows \r\n or Unix \n)
        line_ending = '\r\n' if '\r\n' in content else '\n'

        # Add #nullable disable at the top with matching line ending
        new_content = '#nullable disable' + line_ending + content

        # Write back with the same encoding and newline style
        with open(file_path, 'w', encoding=encoding, newline='') as f:
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
