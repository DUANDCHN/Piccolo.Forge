#!/usr/bin/env python3
"""离线验证固定的上游输入与色板输出，不修改仓库中的生成物。"""

import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile


def digest(path):
    """对原始文件字节计算摘要，避免换行转换掩盖来源变化。"""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalized_bytes(path):
    """只统一文本换行，兼容 Git 在 Windows 检出时的 CRLF 转换。"""
    return path.read_bytes().replace(b"\r\n", b"\n")


def main():
    """先核对输入摘要，再将真实生成器运行在隔离的默认临时目录。"""
    root = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--upstream", type=Path,
                        default=root / "third_party/intellij-community")
    args = parser.parse_args()
    upstream = args.upstream.resolve()
    manifest = json.loads((root / "docs/upstream/reference-manifest.json").read_text(encoding="utf-8"))
    failures = []
    for entry in manifest["files"]:
        source = upstream / entry["path"]
        if not source.is_file() or digest(source) != entry["sha256"]:
            failures.append(entry["path"])
    if failures:
        raise SystemExit("Upstream files missing or changed: " + ", ".join(failures))

    # 导入现有生成器，重定向输入和输出；绝不覆盖源码树以免验证本身改变待验证对象。
    spec = importlib.util.spec_from_file_location("forge_palette", root / "scripts/gen-forge-palette.py")
    palette = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(palette)
    old = palette.UP
    new = str(upstream / "platform/platform-resources/src/themes")
    palette.SRC = {key: path.replace(old, new) for key, path in palette.SRC.items()}
    palette.PARENTS = {key: path.replace(old, new) for key, path in palette.PARENTS.items()}
    with tempfile.TemporaryDirectory(prefix="forge-palette-verify-") as folder:
        palette.ROOT = folder
        for entry in manifest["generated_files"]:
            (Path(folder) / entry["path"]).parent.mkdir(parents=True, exist_ok=True)
        with contextlib.redirect_stdout(io.StringIO()):
            palette.main()
        for entry in manifest["generated_files"]:
            actual = root / entry["path"]
            generated = Path(folder) / entry["path"]
            if (hashlib.sha256(normalized_bytes(actual)).hexdigest() != entry["sha256"]
                    or normalized_bytes(generated) != normalized_bytes(actual)):
                failures.append(entry["path"])
    if failures:
        raise SystemExit("Generated files differ from verified baseline: " + ", ".join(failures))
    print("Verified: %s; %d upstream files; %d identical palettes (LF-normalized)" %
          (manifest["revision"], len(manifest["files"]), len(manifest["generated_files"])))


if __name__ == "__main__":
    main()
