"""Dark ttk theme for the GUI. Stdlib-only (built on ttk's 'clam' base, the
only built-in theme that actually honors background/fieldbackground/
bordercolor/troughcolor overrides) -- no image assets, no third-party theme
packages, so the app keeps its zero-dependency, run-in-place story."""
from __future__ import annotations

import tkinter as tk
import tkinter.font as tkfont
from tkinter import ttk

BG = "#1e1f22"          # window background
PANEL_BG = "#26282b"    # frames, notebook body, text widgets
FIELD_BG = "#2b2d30"    # entry/combobox/treeview field background
BORDER = "#3a3d41"
FG = "#e6e6e6"
FG_MUTED = "#9a9ea3"
ACCENT = "#5b9cf6"
ACCENT_ACTIVE = "#7cb0f8"
ACCENT_FG = "#0d1117"
SELECTED_BG = "#3a4a63"
STRIPE_BG = "#2a2c2f"

GREEN = "#7ec699"
AMBER = "#e0b350"
RED = "#e08787"

FONT_FAMILY = "TkDefaultFont"


def apply_dark_theme(root: tk.Tk) -> None:
    default_font = tkfont.nametofont("TkDefaultFont")
    default_font.configure(size=10)
    text_font = tkfont.nametofont("TkTextFont")
    text_font.configure(size=10)
    heading_font = tkfont.nametofont("TkHeadingFont")
    heading_font.configure(size=10, weight="bold")

    root.configure(bg=BG)
    root.option_add("*TCombobox*Listbox.background", FIELD_BG)
    root.option_add("*TCombobox*Listbox.foreground", FG)
    root.option_add("*TCombobox*Listbox.selectBackground", SELECTED_BG)
    root.option_add("*TCombobox*Listbox.selectForeground", FG)

    style = ttk.Style(root)
    style.theme_use("clam")

    style.configure(".", background=BG, foreground=FG, bordercolor=BORDER,
                     darkcolor=BG, lightcolor=BG, troughcolor=PANEL_BG,
                     focuscolor=ACCENT)

    style.configure("TFrame", background=BG)
    style.configure("TLabel", background=BG, foreground=FG)
    style.configure("Muted.TLabel", background=BG, foreground=FG_MUTED)

    style.configure("TButton", background=PANEL_BG, foreground=FG,
                     bordercolor=BORDER, relief="flat", padding=(12, 6))
    style.map("TButton",
              background=[("active", ACCENT), ("pressed", ACCENT_ACTIVE)],
              foreground=[("active", ACCENT_FG), ("pressed", ACCENT_FG)])

    style.configure("TCombobox", fieldbackground=FIELD_BG, background=PANEL_BG,
                     foreground=FG, arrowcolor=FG, bordercolor=BORDER,
                     relief="flat", padding=4)
    style.map("TCombobox",
              fieldbackground=[("readonly", FIELD_BG), ("disabled", PANEL_BG)],
              foreground=[("readonly", FG), ("disabled", FG_MUTED)],
              background=[("readonly", PANEL_BG)])

    style.configure("Treeview", background=FIELD_BG, fieldbackground=FIELD_BG,
                     foreground=FG, bordercolor=BORDER, relief="flat",
                     rowheight=24)
    style.map("Treeview",
              background=[("selected", SELECTED_BG)],
              foreground=[("selected", FG)])

    style.configure("Treeview.Heading", background=PANEL_BG, foreground=FG,
                     bordercolor=BORDER, relief="flat", padding=(6, 6))
    style.map("Treeview.Heading",
              background=[("active", BORDER)],
              foreground=[("active", FG)])

    style.configure("TNotebook", background=BG, bordercolor=BORDER, tabmargins=(2, 4, 2, 0))
    style.configure("TNotebook.Tab", background=PANEL_BG, foreground=FG_MUTED,
                     padding=(14, 6), bordercolor=BORDER)
    style.map("TNotebook.Tab",
              background=[("selected", BG)],
              foreground=[("selected", FG)])

    style.configure("Vertical.TScrollbar", background=PANEL_BG, troughcolor=BG,
                     bordercolor=BG, arrowcolor=FG_MUTED, relief="flat")
    style.map("Vertical.TScrollbar", background=[("active", BORDER)])
    style.configure("Horizontal.TScrollbar", background=PANEL_BG, troughcolor=BG,
                     bordercolor=BG, arrowcolor=FG_MUTED, relief="flat")
    style.map("Horizontal.TScrollbar", background=[("active", BORDER)])


def style_text_widget(widget: tk.Text) -> None:
    """tk.Text isn't a ttk widget, so ttk.Style has no effect on it -- it
    needs its dark colors set directly."""
    widget.configure(
        background=PANEL_BG, foreground=FG, insertbackground=FG,
        selectbackground=SELECTED_BG, selectforeground=FG,
        relief="flat", borderwidth=0, highlightthickness=1,
        highlightbackground=BORDER, highlightcolor=ACCENT,
    )
