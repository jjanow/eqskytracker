"""Tkinter GUI for eqskytracker. Stdlib-only, so it runs on Windows/macOS/Linux
wherever Python was built with Tk support (on some Linux distros this means
installing a 'python3-tk' package alongside Python itself)."""
from __future__ import annotations

import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

from .components import extract_island_tags
from .discovery import (
    candidate_dirs,
    find_all_characters,
    load_window_geometry,
    save_last_dir,
    save_window_geometry,
)
from .report import build_report, CharacterReport, ItemStatus
from . import theme

DEFAULT_GEOMETRY = "900x600"


class SkyTrackerApp(tk.Tk):
    def __init__(self, initial_dir: str | None = None):
        super().__init__()
        self.title("Plane of Sky Class Tracker")
        theme.apply_dark_theme(self)
        try:
            self.geometry(load_window_geometry() or DEFAULT_GEOMETRY)
        except tk.TclError:
            self.geometry(DEFAULT_GEOMETRY)
        self.protocol("WM_DELETE_WINDOW", self._on_close)

        self.current_dir: Path | None = Path(initial_dir) if initial_dir else None
        self.characters: list = []
        self.report: CharacterReport | None = None

        self._build_widgets()
        self._reload_characters()

    def _on_close(self) -> None:
        try:
            if self.state() == "normal":
                save_window_geometry(self.geometry())
        except tk.TclError:
            pass
        self.destroy()

    # -- layout -----------------------------------------------------------
    def _build_widgets(self) -> None:
        top = ttk.Frame(self, padding=8)
        top.pack(fill="x")

        ttk.Button(top, text="Choose folder...", command=self._choose_dir).pack(side="left")
        self.dir_label = ttk.Label(top, text="(no folder selected)", style="Muted.TLabel")
        self.dir_label.pack(side="left", padx=8)

        ttk.Label(top, text="Character:").pack(side="left", padx=(16, 4))
        self.char_var = tk.StringVar()
        self.char_combo = ttk.Combobox(top, textvariable=self.char_var, state="readonly", width=24)
        self.char_combo.pack(side="left")
        self.char_combo.bind("<<ComboboxSelected>>", lambda _event: self._load_selected_character())

        ttk.Button(top, text="Refresh", command=self._reload_characters).pack(side="left", padx=8)

        self.summary_label = ttk.Label(self, text="", font=("", 13, "bold"), padding=(8, 6))
        self.summary_label.pack(fill="x")

        notebook = ttk.Notebook(self)
        notebook.pack(fill="both", expand=True, padx=8, pady=(8, 0))

        by_class_tab = ttk.Frame(notebook)
        notebook.add(by_class_tab, text="By Class")

        self.tree = ttk.Treeview(by_class_tab, columns=("status",), show="tree headings")
        self.tree.heading("#0", text="Class / Item")
        self.tree.heading("status", text="Status")
        self.tree.column("status", width=180, anchor="w")
        self.tree.pack(fill="both", expand=True)
        self.tree.bind("<<TreeviewSelect>>", self._on_select)

        missing_tab = ttk.Frame(notebook)
        notebook.add(missing_tab, text="All Missing Items")

        missing_columns = [
            ("item", "Item", 220),
            ("class", "Class", 130),
            ("status", "Status", 160),
            ("source", "Source", 140),
        ]
        self.missing_tree = ttk.Treeview(missing_tab, columns=[c for c, _, _ in missing_columns],
                                          show="headings")
        for col, label, width in missing_columns:
            self.missing_tree.heading(col, text=label,
                                       command=lambda c=col: self._sort_missing(c, False))
            self.missing_tree.column(col, width=width, anchor="w")
        self.missing_tree.pack(fill="both", expand=True)
        self.missing_tree.bind("<<TreeviewSelect>>", self._on_missing_select)

        self._item_details: dict[str, str] = {}
        self._missing_item_details: dict[str, str] = {}
        self.detail_text = tk.Text(self, height=4, wrap="word", state="disabled",
                                    padx=10, pady=8)
        theme.style_text_widget(self.detail_text)
        self.detail_text.pack(fill="x", padx=8, pady=8)

        for tree in (self.tree, self.missing_tree):
            tree.tag_configure("unlocked", foreground=theme.GREEN)
            tree.tag_configure("needed", foreground=theme.AMBER)
            tree.tag_configure("keep", foreground=theme.RED)
            tree.tag_configure("stripe", background=theme.STRIPE_BG)

    # -- data loading -------------------------------------------------------
    def _choose_dir(self) -> None:
        chosen = filedialog.askdirectory(title="Select folder containing your EQ dump files")
        if chosen:
            self.current_dir = Path(chosen)
            save_last_dir(chosen)
            self._reload_characters()

    def _reload_characters(self) -> None:
        dirs = [self.current_dir] if self.current_dir else candidate_dirs()
        self.characters = find_all_characters(dirs)

        if self.current_dir:
            self.dir_label.config(text=str(self.current_dir))
        elif dirs:
            self.dir_label.config(text=f"(auto-detected: {dirs[0]})")

        names = [c.name for c in self.characters if c.achievements_path]
        self.char_combo["values"] = names
        if names:
            if self.char_var.get() not in names:
                self.char_var.set(names[0])
            self._load_selected_character()
        else:
            self.char_var.set("")
            self.summary_label.config(text="No character dumps found. Run '/outputfile achievements' "
                                            "and '/outputfile inventory' in-game, then choose that folder.")
            self.tree.delete(*self.tree.get_children())
            self.missing_tree.delete(*self.missing_tree.get_children())

    def _load_selected_character(self) -> None:
        name = self.char_var.get()
        match = next((c for c in self.characters if c.name == name), None)
        if not match or not match.achievements_path:
            return
        try:
            self.report = build_report(match.achievements_path, match.inventory_path)
        except (OSError, ValueError) as exc:
            messagebox.showerror("Failed to read dump", str(exc))
            return
        self._render_report()

    # -- rendering ------------------------------------------------------
    def _render_report(self) -> None:
        assert self.report is not None
        self.summary_label.config(
            text=f"{self.report.character_name} — "
                 f"{self.report.unlocked_count}/{self.report.total_classes} classes unlocked"
        )
        self.tree.delete(*self.tree.get_children())
        self.missing_tree.delete(*self.missing_tree.get_children())
        self._item_details.clear()
        self._missing_item_details.clear()
        self._set_detail_text("Select an item below for its full pickup details.")

        if self.report.farmed_items:
            farmed_node = self.tree.insert("", "end", text="Farmed items (Sky turn-ins)", values=("",), open=True)
            for i, f in enumerate(self.report.farmed_items):
                where = ", ".join(f.locations)
                if f.safe_to_sell:
                    status, detail, tag = "safe to sell/destroy", \
                        f"Not needed for anything still incomplete.\n{where}", "unlocked"
                else:
                    status = "KEEP -- needed"
                    detail = f"Needed for: {', '.join(f.needed_for)}\n{where}"
                    tag = "keep"
                tags = (tag,) if i % 2 == 0 else (tag, "stripe")
                iid = self.tree.insert(farmed_node, "end",
                                        text=f"   {f.name} x{f.count}", values=(status,), tags=tags)
                self._item_details[iid] = detail

        classes = sorted(self.report.classes, key=lambda c: (c.unlocked, c.class_name))
        for cls in classes:
            status = "✓ Unlocked" if cls.unlocked else f"{cls.obtained_count}/{cls.total_count} items"
            node = self.tree.insert("", "end", text=cls.class_name, values=(status,),
                                     open=not cls.unlocked,
                                     tags=("unlocked",) if cls.unlocked else ())
            for i, item in enumerate(cls.items):
                item_status, source, detail = self._describe_item(item)
                tag = "unlocked" if item.complete else "needed"
                tags = (tag,) if i % 2 == 0 else (tag, "stripe")
                iid = self.tree.insert(node, "end", text="   " + item.name, values=(item_status,), tags=tags)
                self._item_details[iid] = detail

                if not item.complete:
                    row = self.missing_tree.insert(
                        "", "end", values=(item.name, cls.class_name, item_status, source),
                        tags=(tag,) if len(self.missing_tree.get_children("")) % 2 == 0
                        else (tag, "stripe")
                    )
                    self._missing_item_details[row] = detail

        self._sort_missing("item", False)

    def _describe_item(self, item: ItemStatus) -> tuple[str, str, str]:
        """Returns (status text, source/drop-location tags, full detail text)
        for a single item, shared by the by-class tree and the unified
        missing-items list so the two views never disagree."""
        detail_lines = [item.name]
        source = ""
        if item.complete:
            return "✓ obtained", source, item.name
        item_status = "needed"
        if item.in_inventory:
            item_status += "  (in bags/bank!)"
            detail_lines.append("Already sitting in your bags/bank/keyring.")
        if item.hint and item.hint.found and item.hint.how_to_obtain:
            detail_lines.append(item.hint.how_to_obtain)
            source = ", ".join(extract_island_tags(item.hint.how_to_obtain))
        elif not item.hint:
            detail_lines.append("No pickup hint available for this item yet.")
        return item_status, source, "\n".join(detail_lines)

    def _on_select(self, _event: object) -> None:
        selected = self.tree.selection()
        if not selected:
            return
        text = self._item_details.get(selected[0], "")
        self._set_detail_text(text or "(class row -- select an item for details)")

    def _on_missing_select(self, _event: object) -> None:
        selected = self.missing_tree.selection()
        if not selected:
            return
        self._set_detail_text(self._missing_item_details.get(selected[0], ""))

    def _sort_missing(self, col: str, reverse: bool) -> None:
        data = [(self.missing_tree.set(k, col), k) for k in self.missing_tree.get_children("")]
        data.sort(key=lambda pair: pair[0].casefold(), reverse=reverse)
        for index, (_value, k) in enumerate(data):
            self.missing_tree.move(k, "", index)
        self.missing_tree.heading(col, command=lambda: self._sort_missing(col, not reverse))

    def _set_detail_text(self, text: str) -> None:
        self.detail_text.config(state="normal")
        self.detail_text.delete("1.0", "end")
        self.detail_text.insert("1.0", text)
        self.detail_text.config(state="disabled")


def run_gui(initial_dir: str | None = None) -> int:
    app = SkyTrackerApp(initial_dir=initial_dir)
    app.mainloop()
    return 0
