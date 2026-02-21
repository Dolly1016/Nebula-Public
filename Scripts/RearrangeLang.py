import os
import re

def parse_line(line):
    line = line.lstrip('\ufeff').strip()
    if not line or line.startswith('#'):
        return None
    
    if ':' not in line:
        return None
    
    key_part, value_part = line.split(':', 1)
    
    key = key_part.strip().strip('"')
    
    val = value_part.strip()
    if len(val) >= 2 and val.startswith('"') and val.endswith('"'):
        val = val[1:-1]
        
    return key, val

def load_all_pairs(directories):
    kv_store = {}
    for directory in directories:
        for root, _, files in os.walk(directory):
            for file in files:
                path = os.path.join(root, file)
                with open(path, 'r', encoding='utf-8') as f:
                    for line in f:
                        result = parse_line(line)
                        if result:
                            key, val = result
                            kv_store[key] = val
    return kv_store

def rearrange_files(source_dirs, target_template_dir, output_dir):
    source_data = load_all_pairs(source_dirs)
    print(f"Source data loaded: {len(source_data)} keys found.")

    for root, _, files in os.walk(target_template_dir):
        for file in files:
            target_path = os.path.join(root, file)
            
            relative_path = os.path.relpath(target_path, target_template_dir)
            dest_path = os.path.join(output_dir, relative_path)
            os.makedirs(os.path.dirname(dest_path), exist_ok=True)

            new_lines = []
            with open(target_path, 'r', encoding='utf-8') as f:
                for line in f:
                    parsed = parse_line(line)
                    if parsed:
                        key, _ = parsed
                        if key in source_data:
                            new_val = source_data[key]
                            new_lines.append(f'"{key}" : "{new_val}"\n')
                        else:
                            new_lines.append(f'# Key "{key}" not found in source\n')
                            print(f'# Key "{key}" not found in source (File:{target_path})\n')
                    else:
                        new_lines.append(line)

            with open(dest_path, 'w', encoding='utf-8') as f:
                f.writelines(new_lines)
    
    print(f"Rearrangement complete. Files saved to: {output_dir}")

if __name__ == "__main__":
    SRC = []
    print('If you want to end to input source directories, enter "end"')
    while True:
        user_input = input("Source: ").strip()

        if user_input.lower() == 'end':
            break

        if user_input:
            SRC.append(user_input)
            print(f"Added directory: {user_input}")
        else:
            print("Invalid path")
    
    TMPL = input("Target: ").strip()
    OUT = input("Output: ").strip()
    
    rearrange_files(SRC, TMPL, OUT)
    