import os
import sys
from pathlib import Path

def remove_nullable_disable(file_path):
    """Remove #nullable disable from the top of a C# file if present."""
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

        # Check if #nullable disable is at the top
        lines = content.splitlines(keepends=True)

        if not lines:
            return False

        # Check first non-empty line
        first_line = lines[0].strip()
        if first_line != '#nullable disable':
            return False

        # Remove the first line
        new_content = ''.join(lines[1:])

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
        if remove_nullable_disable(cs_file):
            print(f"Modified: {cs_file}")
            modified_count += 1
        else:
            print(f"Skipped (no #nullable disable at top): {cs_file}")
            skipped_count += 1

    print()
    print(f"Summary:")
    print(f"  Modified: {modified_count} files")
    print(f"  Skipped: {skipped_count} files")
    print(f"  Total: {len(cs_files)} files")

if __name__ == '__main__':
    main()
