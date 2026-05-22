# -*- coding: utf-8 -*-
"""Rough longest brace-blocks in GRC2/*.cs (excludes obj/bin). Not Roslyn-precise."""
import re
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1] / "GRC2"


def strip_line_comment(s: str) -> str:
    in_str = None
    i = 0
    out = []
    while i < len(s):
        c = s[i]
        if in_str:
            out.append(c)
            if c == in_str and (i == 0 or s[i - 1] != "\\"):
                in_str = None
            i += 1
            continue
        if c in ('"', "'"):
            in_str = c
            out.append(c)
            i += 1
            continue
        if c == "/" and i + 1 < len(s) and s[i + 1] == "/":
            break
        out.append(c)
        i += 1
    return "".join(out)


def braces_in_order(s: str) -> list[tuple[str, int]]:
    """Return [('{', col)|('}', col), ...] in source order (0-based col)."""
    out = []
    i = 0
    n = len(s)
    in_str = None
    while i < n:
        c = s[i]
        if in_str:
            if c == in_str and s[i - 1] != "\\":
                in_str = None
            i += 1
            continue
        if c in ('"', "'"):
            in_str = c
            i += 1
            continue
        if c == "/" and i + 1 < n and s[i + 1] == "/":
            break
        if c == "{":
            out.append(("{", i))
        elif c == "}":
            out.append(("}", i))
        i += 1
    return out


def scan_file(path: pathlib.Path) -> list[tuple[int, int, int, int]]:
    """Return list of (span_lines, start_line, end_line, depth_at_open)."""
    text = path.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()
    blocks = []
    d = 0
    st: list[tuple[int, int]] = []  # (start_line_1based, depth_before_open)
    line_no = 0
    for raw in lines:
        line_no += 1
        s = strip_line_comment(raw)
        s2 = re.sub(r'"(?:\\.|[^"\\])*"', '""', s)
        s2 = re.sub(r"'(?:\\.|[^'\\])'", "''", s2)
        for br, _ in braces_in_order(s2):
            if br == "{":
                st.append((line_no, d))
                d += 1
            else:
                if not st:
                    d = max(0, d - 1)
                    continue
                start_ln, depth_open = st.pop()
                end_ln = line_no
                span = end_ln - start_ln + 1
                # depth_open==2: brace opened inside class body (methods, nested types, big field inits)
                if span >= 30 and depth_open == 2:
                    blocks.append((span, start_ln, end_ln, depth_open))
                d -= 1
    return blocks


def main() -> None:
    all_blocks: list[tuple[int, int, int, int, pathlib.Path]] = []
    for p in sorted(ROOT.rglob("*.cs")):
        rel = str(p)
        if "\\obj\\" in rel or "/obj/" in rel or "\\bin\\" in rel or "/bin/" in rel:
            continue
        try:
            for span, a, b, dep in scan_file(p):
                all_blocks.append((span, a, b, dep, p))
        except OSError as e:
            print("skip", p, e, file=sys.stderr)

    all_blocks.sort(key=lambda x: (-x[0], str(x[4])))
    seen = set()
    out = []
    for span, a, b, dep, p in all_blocks:
        key = (p, a, b)
        if key in seen:
            continue
        seen.add(key)
        out.append((span, a, b, dep, p))
        if len(out) >= 30:
            break

    print("span  lines  depth  file")
    for span, a, b, dep, p in out:
        rel = p.relative_to(ROOT.parent)
        print(f"{span:4d}  L{a}-L{b}  d={dep}  {rel}")


if __name__ == "__main__":
    main()
