# -*- coding: utf-8 -*-
"""
cs_outline.py - Ultra-lightweight Python/Regex C# Outline & Symbol Token Saver.
No dotnet/Roslyn build needed. Saves AI tokens by extracting only skeleton signatures or pinpoint blocks.
"""
import argparse
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]

RE_TYPE = re.compile(
    r"^(?P<indent>\s*)(?:public|private|protected|internal|static|abstract|sealed|partial|\s)*\b"
    r"(?P<kind>class|struct|interface|enum|record)\s+(?P<name>[A-Za-z0-9_<>]+)"
    r"(?:\s*:\s*(?P<base>[^{]+))?"
)

RE_METHOD = re.compile(
    r"^(?P<indent>\s*)(?P<mods>(?:public|private|protected|internal|static|virtual|override|abstract|async|unsafe|\s)+)"
    r"(?P<ret>[A-Za-z0-9_<>\[\],\?]+)\s+(?P<name>[A-Za-z0-9_]+)\s*(?P<generics><[^>]+>)?\s*"
    r"\((?P<params>[^)]*)\)"
)

RE_PROP = re.compile(
    r"^(?P<indent>\s*)(?P<mods>(?:public|private|protected|internal|static|virtual|override|abstract|\s)+)"
    r"(?P<type>[A-Za-z0-9_<>\[\],\?]+)\s+(?P<name>[A-Za-z0-9_]+)\s*"
    r"(?:\{|=>)"
)


def get_cs_files(target: pathlib.Path, pattern: str = "*.cs"):
    if target.is_file():
        return [target]
    if not target.exists():
        return []
    files = []
    for p in sorted(target.rglob(pattern)):
        s = str(p)
        if any(skip in s for skip in ("/obj/", "\\obj\\", "/bin/", "\\bin\\")):
            continue
        files.append(p)
    return files


def outline_file(path: pathlib.Path, min_lines: int = 1):
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except Exception as e:
        print(f"[Error] Failed to read {path}: {e}", file=sys.stderr)
        return

    lines = text.splitlines()
    total_lines = len(lines)
    rel = path.relative_to(ROOT) if path.is_relative_to(ROOT) else path

    records = []
    stack = []  # (start_line, header, kind)

    # Simple brace depth & header tracking
    for idx, raw in enumerate(lines, start=1):
        stripped = raw.strip()
        if not stripped or stripped.startswith("//") or stripped.startswith("/*") or stripped.startswith("*"):
            continue

        # Check type declaration
        m_t = RE_TYPE.match(raw)
        if m_t and "{" in raw:
            # type opened on same line
            pass

        # Track brace changes roughly
        opens = raw.count("{")
        closes = raw.count("}")

        if m_t:
            name = m_t.group("name")
            kind = m_t.group("kind")
            base = f" : {m_t.group('base').strip()}" if m_t.group("base") else ""
            header = f"{kind} {name}{base}"
            stack.append((idx, header, "type", opens - closes))
            continue

        m_m = RE_METHOD.match(raw)
        if m_m and ("{" in raw or idx < total_lines and "{" in lines[idx]):
            ret = m_m.group("ret")
            name = m_m.group("name")
            params = m_m.group("params").strip()
            params_short = re.sub(r"\s+", " ", params)
            if len(params_short) > 60:
                params_short = params_short[:57] + "..."
            sig = f"{ret} {name}({params_short})"
            stack.append((idx, sig, "method", opens - closes))
            continue

        m_p = RE_PROP.match(raw)
        if m_p and not m_t and not m_m:
            ptype = m_p.group("type")
            pname = m_p.group("name")
            sig = f"{ptype} {pname} {{ get; set; }} / =>"
            stack.append((idx, sig, "prop", opens - closes))
            continue

    print(f"\n[File] {rel} ({total_lines} lines)")
    for start, hdr, kind, _ in stack:
        indent = "    " if kind != "type" else "  "
        print(f"{indent}{hdr} [L{start}]")


def find_symbols(target: pathlib.Path, query: str, regex: bool = False):
    files = get_cs_files(target)
    rx = re.compile(query, re.IGNORECASE) if regex else None

    def match(name: str):
        return rx.search(name) if rx else (query.lower() in name.lower())

    count = 0
    print(f"Searching '{query}' in {len(files)} files...\n")
    for f in files:
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except Exception:
            continue
        if not regex and query.lower() not in text.lower():
            continue

        lines = text.splitlines()
        rel = f.relative_to(ROOT) if f.is_relative_to(ROOT) else f

        for idx, line in enumerate(lines, start=1):
            m_t = RE_TYPE.match(line)
            if m_t and match(m_t.group("name")):
                print(f"[Type]   {rel}:{idx}  {line.strip()}")
                count += 1
                continue

            m_m = RE_METHOD.match(line)
            if m_m and match(m_m.group("name")):
                print(f"[Method] {rel}:{idx}  {line.strip()}")
                count += 1
                continue

            m_p = RE_PROP.match(line)
            if m_p and match(m_p.group("name")):
                print(f"[Prop]   {rel}:{idx}  {line.strip()}")
                count += 1
                continue

    print(f"\nTotal matches: {count}")


def extract_block(file_path: pathlib.Path, target: str):
    if not file_path.is_file():
        print(f"Error: file not found {file_path}", file=sys.stderr)
        return

    text = file_path.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()
    rel = file_path.relative_to(ROOT) if file_path.is_relative_to(ROOT) else file_path

    target_line = None
    if target.isdigit():
        target_line = int(target)
    else:
        # Find symbol definition line
        for idx, line in enumerate(lines, start=1):
            if re.search(r"\b" + re.escape(target) + r"\b", line):
                target_line = idx
                break

    if target_line is None:
        print(f"Symbol '{target}' not found in {file_path}", file=sys.stderr)
        return

    # Backtrack to method header if target_line is inside body
    start_line = target_line
    for i in range(target_line - 1, max(0, target_line - 20), -1):
        line = lines[i].strip()
        if any(line.startswith(kw) for kw in ("public", "private", "protected", "internal")):
            start_line = i + 1
            break

    # Forward track braces
    depth = 0
    end_line = start_line
    started = False
    for i in range(start_line - 1, len(lines)):
        line = lines[i]
        opens = line.count("{")
        closes = line.count("}")
        if opens > 0:
            started = True
        depth += opens - closes
        if started and depth <= 0:
            end_line = i + 1
            break

    total = end_line - start_line + 1
    print(f"// === [Extract] {rel} (L{start_line}-L{end_line}, {total} lines) ===")
    print("\n".join(lines[start_line - 1 : end_line]))
    print("// === [End of Extract] ===\n")


def main():
    parser = argparse.ArgumentParser(description="Python Regex C# Outline & Symbol Extractor")
    sub = parser.add_subparsers(dest="command")

    p_out = sub.add_parser("outline", help="Dump skeleton outline of a file or directory")
    p_out.add_argument("path", help="Path to .cs file or folder")

    p_find = sub.add_parser("find", help="Search symbol names across files")
    p_find.add_argument("query", help="Symbol name or keyword")
    p_find.add_argument("--path", default=".", help="Target directory (default: current)")
    p_find.add_argument("--regex", action="store_true", help="Treat query as regex")

    p_ext = sub.add_parser("extract", help="Extract snippet of method or line")
    p_ext.add_argument("file", help="Path to .cs file")
    p_ext.add_argument("target", help="Symbol name or line number")

    args = parser.parse_args()
    if not args.command:
        parser.print_help()
        return

    if args.command == "outline":
        target = pathlib.Path(args.path)
        for f in get_cs_files(target):
            outline_file(f)
    elif args.command == "find":
        find_symbols(pathlib.Path(args.path), args.query, args.regex)
    elif args.command == "extract":
        extract_block(pathlib.Path(args.file), args.target)


if __name__ == "__main__":
    main()
