"""
build_pdf.py
新規章 (09-13) を PDF に変換し、既存の rhythm_game_design.pdf (72ページ) に追記して
rhythm_game_design_v2.pdf を生成する。
"""

import os, re, sys
from pathlib import Path
import pypdf
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm
from reportlab.lib.colors import HexColor
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, Preformatted, PageBreak
)
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

# ── フォント登録 ─────────────────────────────────────────────────
FONT_PATH = r"C:\Windows\Fonts\YuGothR.ttc"
FONT_BOLD = r"C:\Windows\Fonts\YuGothM.ttc"

pdfmetrics.registerFont(TTFont("YuGoth",     FONT_PATH, subfontIndex=0))
pdfmetrics.registerFont(TTFont("YuGothBold", FONT_BOLD, subfontIndex=0))

# ── 出力パス ─────────────────────────────────────────────────────
SCRIPT_DIR   = Path(__file__).parent
EXISTING_PDF = Path(r"C:\Users\CaSte\OneDrive\デスクトップ\rhythm_game_design.pdf")
OUTPUT_PDF   = Path(r"C:\Users\CaSte\OneDrive\デスクトップ\rhythm_game_design_v2.pdf")
TEMP_PDF     = SCRIPT_DIR / "_chapters_temp.pdf"

CHAPTER_FILES = [
    SCRIPT_DIR / "09_phase1_decisions.md",
    SCRIPT_DIR / "10_phase2_implementation.md",
    SCRIPT_DIR / "11_architecture_summary.md",
    SCRIPT_DIR / "12_phase2_debugging_lessons.md",
    SCRIPT_DIR / "13_handbook_summary.md",
]

# ── 既存 PDF のページ数を取得 ─────────────────────────────────────
existing_pages = 0
if EXISTING_PDF.exists():
    r = pypdf.PdfReader(str(EXISTING_PDF))
    existing_pages = len(r.pages)
    print(f"Existing PDF: {existing_pages} pages")
else:
    print("WARNING: existing PDF not found, generating new chapters only")

# ── スタイル定義 ──────────────────────────────────────────────────
PAGE_W, PAGE_H = A4
MARGIN = 20 * mm

def make_styles(page_offset: int):
    """ページ番号オフセットを受け取ってスタイル辞書を返す"""
    base = ParagraphStyle
    styles = {
        "h1": base("h1",
            fontName="YuGothBold", fontSize=18, leading=26,
            spaceBefore=8*mm, spaceAfter=4*mm,
            textColor=HexColor("#1a3a6b"),
        ),
        "h2": base("h2",
            fontName="YuGothBold", fontSize=13, leading=20,
            spaceBefore=6*mm, spaceAfter=3*mm,
            textColor=HexColor("#1a3a6b"),
            borderPad=2, borderWidth=0,
        ),
        "h3": base("h3",
            fontName="YuGothBold", fontSize=11, leading=16,
            spaceBefore=4*mm, spaceAfter=2*mm,
            textColor=HexColor("#2c5f9e"),
        ),
        "body": base("body",
            fontName="YuGoth", fontSize=9, leading=15,
            spaceBefore=1*mm, spaceAfter=1*mm,
        ),
        "code": base("code",
            fontName="YuGoth", fontSize=8, leading=12,
            backColor=HexColor("#f5f5f5"),
            borderColor=HexColor("#cccccc"),
            borderWidth=0.5, borderPad=4,
            spaceBefore=2*mm, spaceAfter=2*mm,
        ),
        "bullet": base("bullet",
            fontName="YuGoth", fontSize=9, leading=14,
            spaceBefore=0.5*mm, spaceAfter=0.5*mm,
            leftIndent=8*mm, bulletIndent=2*mm,
        ),
    }
    return styles

# ── Markdown → ReportLab elements 変換 ────────────────────────────

def md_to_elements(md_text: str, styles: dict) -> list:
    """簡易 Markdown → Reportlab Element リスト変換"""
    elements = []
    lines = md_text.split("\n")
    i = 0
    in_code = False
    code_buf = []

    while i < len(lines):
        line = lines[i]

        # コードブロック
        if line.strip().startswith("```"):
            if not in_code:
                in_code = True
                code_buf = []
            else:
                in_code = False
                code_text = "\n".join(code_buf)
                # コード行を Preformatted で出力 (長い行は折り返し)
                short_lines = []
                for cl in code_buf:
                    if len(cl) > 80:
                        short_lines.append(cl[:80] + "...")
                    else:
                        short_lines.append(cl)
                code_text = "\n".join(short_lines)
                elements.append(Preformatted(
                    _esc(code_text),
                    ParagraphStyle("code_pre",
                        fontName="YuGoth", fontSize=7.5, leading=11,
                        backColor=HexColor("#f5f5f5"),
                        spaceBefore=2*mm, spaceAfter=2*mm,
                    )
                ))
                code_buf = []
            i += 1
            continue

        if in_code:
            code_buf.append(line)
            i += 1
            continue

        # 空行
        if not line.strip():
            elements.append(Spacer(1, 2*mm))
            i += 1
            continue

        # 水平線
        if line.strip().startswith("---"):
            from reportlab.platypus import HRFlowable
            elements.append(HRFlowable(width="100%", thickness=0.5, color=HexColor("#cccccc")))
            elements.append(Spacer(1, 2*mm))
            i += 1
            continue

        # 見出し
        if line.startswith("# "):
            elements.append(Paragraph(_esc(line[2:]), styles["h1"]))
            i += 1
            continue
        if line.startswith("## "):
            elements.append(Paragraph(_esc(line[3:]), styles["h2"]))
            i += 1
            continue
        if line.startswith("### "):
            elements.append(Paragraph(_esc(line[4:]), styles["h3"]))
            i += 1
            continue

        # テーブル
        if line.strip().startswith("|"):
            table_lines = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                table_lines.append(lines[i])
                i += 1
            elements.extend(_make_table(table_lines, styles))
            continue

        # 箇条書き
        if line.startswith("- ") or line.startswith("* "):
            bullet_text = line[2:]
            elements.append(Paragraph(
                "• " + _esc(bullet_text), styles["bullet"]
            ))
            i += 1
            continue

        # チェックリスト
        if line.startswith("- [ ] ") or line.startswith("- [x] "):
            chk = "☑" if "[x]" in line else "☐"
            txt = line.split("] ", 1)[-1]
            elements.append(Paragraph(
                f"{chk} " + _esc(txt), styles["bullet"]
            ))
            i += 1
            continue

        # 通常テキスト (inline code, bold)
        text = _inline_md(line)
        elements.append(Paragraph(text, styles["body"]))
        i += 1

    return elements


def _esc(text: str) -> str:
    """ReportLab XML エスケープ"""
    text = text.replace("&", "&amp;")
    text = text.replace("<", "&lt;")
    text = text.replace(">", "&gt;")
    return text


def _inline_md(text: str) -> str:
    """インライン Markdown: bold, inline code"""
    text = _esc(text)
    # **bold**
    text = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text)
    # *italic*
    text = re.sub(r"\*(.+?)\*", r"<i>\1</i>", text)
    # `code`
    text = re.sub(r"`([^`]+)`",
        lambda m: f'<font name="YuGoth" size="8" color="#555555">[{_esc(m.group(1))}]</font>',
        text)
    return text


def _make_table(table_lines: list, styles: dict) -> list:
    """Markdown テーブル → ReportLab Table"""
    rows = []
    for line in table_lines:
        if re.match(r"^\s*\|[-:| ]+\|\s*$", line):
            continue  # セパレーター行をスキップ
        cells = [c.strip() for c in line.strip("|").split("|")]
        rows.append(cells)

    if not rows:
        return []

    col_count = max(len(r) for r in rows)
    # 列幅を均等に
    col_width = (PAGE_W - 2 * MARGIN) / col_count

    data = []
    for ri, row in enumerate(rows):
        while len(row) < col_count:
            row.append("")
        style_key = "h3" if ri == 0 else "body"
        data.append([
            Paragraph(_inline_md(cell),
                ParagraphStyle(f"cell_{ri}",
                    fontName="YuGothBold" if ri == 0 else "YuGoth",
                    fontSize=8, leading=11,
                ))
            for cell in row
        ])

    t = Table(data, colWidths=[col_width] * col_count)
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), HexColor("#dce6f5")),
        ("GRID",       (0, 0), (-1, -1), 0.5, HexColor("#999999")),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [HexColor("#ffffff"), HexColor("#f5f8ff")]),
        ("VALIGN",     (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 3),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
    ]))
    return [t, Spacer(1, 3*mm)]


# ── ページ番号フッター ─────────────────────────────────────────────

def make_footer_canvas(page_offset: int):
    """フッターにページ番号を追加するキャンバスファクトリ"""
    def footer(canvas, doc):
        canvas.saveState()
        canvas.setFont("YuGoth", 8)
        canvas.setFillColor(HexColor("#666666"))
        page_num = doc.page + page_offset
        canvas.drawCentredString(PAGE_W / 2, 10 * mm, str(page_num) + " / ?")
        canvas.restoreState()
    return footer


# ── メイン PDF 生成 ────────────────────────────────────────────────

def build_chapters_pdf(page_offset: int) -> Path:
    """新規章を1つの PDF に変換"""
    doc = SimpleDocTemplate(
        str(TEMP_PDF),
        pagesize=A4,
        leftMargin=MARGIN, rightMargin=MARGIN,
        topMargin=MARGIN, bottomMargin=18*mm,
    )
    styles = make_styles(page_offset)
    all_elements = []

    for chapter_file in CHAPTER_FILES:
        if not chapter_file.exists():
            print(f"SKIP (not found): {chapter_file.name}")
            continue

        print(f"Processing: {chapter_file.name}")
        md_text = chapter_file.read_text(encoding="utf-8")
        elements = md_to_elements(md_text, styles)
        all_elements.extend(elements)
        all_elements.append(PageBreak())

    footer_fn = make_footer_canvas(page_offset)
    doc.build(all_elements, onFirstPage=footer_fn, onLaterPages=footer_fn)
    print(f"Chapters PDF generated: {TEMP_PDF}")
    return TEMP_PDF


# ── 既存 PDF + 新規章 を結合 ──────────────────────────────────────

def merge_pdfs(existing: Path, new_chapters: Path, output: Path):
    writer = pypdf.PdfWriter()

    # 既存72ページを追加
    if existing.exists():
        reader = pypdf.PdfReader(str(existing))
        for page in reader.pages:
            writer.add_page(page)
        print(f"Added {len(reader.pages)} pages from existing PDF")

    # 新規章を追加
    reader2 = pypdf.PdfReader(str(new_chapters))
    for page in reader2.pages:
        writer.add_page(page)
    print(f"Added {len(reader2.pages)} new chapter pages")

    total = len(writer.pages)
    output.parent.mkdir(parents=True, exist_ok=True)
    with open(str(output), "wb") as f:
        writer.write(f)
    print(f"\nOutput: {output}")
    print(f"Total pages: {total}")
    return total


# ── エントリポイント ──────────────────────────────────────────────

if __name__ == "__main__":
    print("=" * 60)
    print("rhythm_game_design_v2.pdf 生成")
    print("=" * 60)

    chapters_pdf = build_chapters_pdf(page_offset=existing_pages)
    total_pages  = merge_pdfs(EXISTING_PDF, chapters_pdf, OUTPUT_PDF)

    # 一時ファイル削除
    if TEMP_PDF.exists():
        TEMP_PDF.unlink()

    print()
    print(f"✅ 完成: {OUTPUT_PDF}")
    print(f"   既存 {existing_pages}p + 新規章 = 合計 {total_pages}p")
