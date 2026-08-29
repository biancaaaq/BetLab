# -*- coding: utf-8 -*-
"""Funnel OutcomeSelected → BetAttempted → BetPlaced (W1). Efectul confirmarii Ethical + miza preselectionata Dark."""

import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
import numpy as np
import json
from pathlib import Path
from scipy.stats import fisher_exact, chi2_contingency

DATA_PATH     = Path("../data/InteractionLogs_Week1.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week1")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

COLORS   = {"Dark": "#d62728", "Ethical": "#2ca02c"}
VARIANTS = ["Dark", "Ethical"]

df = pd.read_csv(DATA_PATH)

# Funnel cu 3 pasi (StakeChanged exclus din secventa obligatorie —
# Dark design foloseste miza preselectionata, deci 35% din sesiuni
# nu au StakeChanged inainte de BetAttempted)
FUNNEL_STEPS  = ["OutcomeSelected", "BetAttempted", "BetPlaced"]
STEP_LABELS_RO = [
    "Selectie cota\n(OutcomeSelected)",
    "Pariu initiat\n(BetAttempted)",
    "Pariu plasat\n(BetPlaced)",
]


def style_ax(ax, axis="y"):
    ax.grid(axis=axis, alpha=0.22, linestyle="--")
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

def pval_label(p):
    return "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."


total_sessions = {}
funnel_counts  = {}

for variant in VARIANTS:
    vdf          = df[df["Variant"] == variant]
    all_sessions = set(vdf["SessionId"].unique())
    total_sessions[variant] = len(all_sessions)
    remaining = all_sessions
    counts    = []
    for step in FUNNEL_STEPS:
        sessions_with_step = set(vdf[vdf["EventType"] == step]["SessionId"])
        remaining = remaining & sessions_with_step
        counts.append(len(remaining))
    funnel_counts[variant] = counts

print("=" * 70)
print("WEEK 1 — FUNNEL ANALYSIS")
print("=" * 70)
print(f"Total sesiuni Dark:    {total_sessions['Dark']}")
print(f"Total sesiuni Ethical: {total_sessions['Ethical']}")

rows = []
for variant in VARIANTS:
    counts = funnel_counts[variant]
    top    = counts[0] if counts[0] > 0 else 1
    for i, (step, cnt) in enumerate(zip(FUNNEL_STEPS, counts)):
        conv_top  = cnt / top * 100
        conv_prev = (cnt / counts[i - 1] * 100) if i > 0 and counts[i - 1] > 0 else 100.0
        rows.append({"Variant": variant, "Step": step, "Sessions": cnt,
                     "Conv_from_top_%": round(conv_top, 1),
                     "Conv_from_prev_%": round(conv_prev, 1)})

funnel_df = pd.DataFrame(rows)
funnel_df.to_csv(OUTPUT_TABLES / "week1_funnel_analysis.csv", index=False)
print("\n", funnel_df.to_string(index=False))


# (Ethical drop cauzat de BetConfirmation; Dark aproape 100%)
d_att, d_pla = funnel_counts["Dark"][1], funnel_counts["Dark"][2]
e_att, e_pla = funnel_counts["Ethical"][1], funnel_counts["Ethical"][2]
d_cancel = d_att - d_pla
e_cancel = e_att - e_pla
table    = [[d_pla, d_cancel], [e_pla, e_cancel]]

if min(d_cancel, e_cancel) < 5:
    _, p_conv = fisher_exact(table)
    test_name = "Fisher"
else:
    chi2_val, p_conv, _, _ = chi2_contingency(table)
    test_name = "Chi2"

d_conv_pct = d_pla / d_att * 100 if d_att > 0 else 0
e_conv_pct = e_pla / e_att * 100 if e_att > 0 else 0
print(f"\nTest {test_name} BetAttempted→BetPlaced:")
print(f"  Dark    {d_pla}/{d_att} = {d_conv_pct:.1f}%")
print(f"  Ethical {e_pla}/{e_att} = {e_conv_pct:.1f}%")
print(f"  p={p_conv:.4f}  {pval_label(p_conv)}")



def get_stake_source(meta_str):
    """Extrage stakeSource din MetadataJson ('preselected' / 'manual')."""
    if not isinstance(meta_str, str) or not meta_str.strip():
        return "unknown"
    s = meta_str.strip()
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1].replace('""', '"')
    try:
        d = json.loads(s)
        return d.get("stakeSource", "unknown")
    except Exception:
        return "unknown"

dark_df = df[df["Variant"] == "Dark"]
eth_df_placed = df[(df["Variant"] == "Ethical") & (df["EventType"] == "BetPlaced")]

dark_placed = dark_df[dark_df["EventType"] == "BetPlaced"].copy()
dark_placed["stakeSource"] = dark_placed["MetadataJson"].apply(get_stake_source)

n_presel_bets = (dark_placed["stakeSource"] == "preselected").sum()
n_manual_bets = (dark_placed["stakeSource"] == "manual").sum()
n_unknown_bets = (dark_placed["stakeSource"] == "unknown").sum()
total_dark_bets = len(dark_placed)
pct_presel = 100 * n_presel_bets / total_dark_bets if total_dark_bets > 0 else 0
pct_manual = 100 * n_manual_bets / total_dark_bets if total_dark_bets > 0 else 0

# Valori pentru compatibilitate cu referintele de mai jos
n_presel = n_presel_bets
n_manual = n_manual_bets
total_bet_d = total_dark_bets

print(f"\nDark — pariuri BetPlaced (nivel bet): {total_dark_bets}")
print(f"  Miza manuala (stakeSource=manual):       {n_manual_bets} ({pct_manual:.1f}%)")
print(f"  Miza preselectionata (stakeSource=pres.): {n_presel_bets} ({pct_presel:.1f}%)")
print(f"  Nedeterminat:                            {n_unknown_bets}")
print(f"\nEthical — pariuri BetPlaced: {len(eth_df_placed)} (toate cu miza manuala)")

# sesiuni dark cu bet (pentru [1,0] titlu)
dark_bet_ses = set(dark_df[dark_df["EventType"] == "BetAttempted"]["SessionId"])

eth_df = df[df["Variant"] == "Ethical"]
eth_bet_ses = set(eth_df[eth_df["EventType"] == "BetAttempted"]["SessionId"])
eth_conf_ses = set(eth_df[eth_df["EventType"] == "BetConfirmationViewed"]["SessionId"])
eth_canc_ses = set(eth_df[eth_df["EventType"] == "BetConfirmationCancelled"]["SessionId"])
eth_pla_ses  = set(eth_df[eth_df["EventType"] == "BetPlaced"]["SessionId"])

n_eth_att   = len(eth_bet_ses)
n_eth_conf  = len(eth_conf_ses & eth_bet_ses)
n_eth_canc  = len(eth_canc_ses & eth_bet_ses)
n_eth_placed = len(eth_pla_ses & eth_bet_ses)

print(f"\nEthical — BetAttempted sessions: {n_eth_att}")
print(f"  BetConfirmationViewed:    {n_eth_conf} ({100*n_eth_conf/n_eth_att:.1f}%)")
print(f"  Anulari la confirmare:    {n_eth_canc} ({100*n_eth_canc/n_eth_att:.1f}%)")
print(f"  BetPlaced (final):        {n_eth_placed} ({100*n_eth_placed/n_eth_att:.1f}%)")


fig, axes = plt.subplots(2, 3, figsize=(18, 11))
fig.subplots_adjust(hspace=0.50, wspace=0.42)

# [0,0] Funnel absolut grouped bars
ax = axes[0, 0]
x     = np.arange(len(FUNNEL_STEPS))
width = 0.35
for j, variant in enumerate(VARIANTS):
    offset = (j - 0.5) * width
    bars   = ax.bar(x + offset, funnel_counts[variant], width,
                    label=variant, color=COLORS[variant], alpha=0.82, edgecolor="white")
    for bar, val in zip(bars, funnel_counts[variant]):
        ax.text(bar.get_x() + bar.get_width() / 2,
                bar.get_height() + 0.5, str(val),
                ha="center", va="bottom", fontsize=9, fontweight="bold")

ax.set_xticks(x)
ax.set_xticklabels(STEP_LABELS_RO, fontsize=8.5)
ax.set_ylabel("Numar sesiuni")
ax.set_title("Funnel — sesiuni absolute\nDark vs Ethical", fontsize=10, fontweight="bold")
ax.legend(handles=[mpatches.Patch(color=COLORS[v], label=v) for v in VARIANTS], fontsize=8)
ax.set_ylim(0, max(max(funnel_counts["Dark"]), max(funnel_counts["Ethical"])) * 1.28)
style_ax(ax)

# [0,1] Rata conversie (%) relativa la OutcomeSelected
ax = axes[0, 1]
for variant in VARIANTS:
    top  = funnel_counts[variant][0] or 1
    pcts = [cnt / top * 100 for cnt in funnel_counts[variant]]
    ax.plot(STEP_LABELS_RO, pcts,
            marker="o", linewidth=2.5, markersize=9,
            label=variant, color=COLORS[variant])
    for lbl, pct in zip(STEP_LABELS_RO, pcts):
        ax.annotate(f"{pct:.1f}%", (lbl, pct),
                    textcoords="offset points", xytext=(0, 10),
                    ha="center", fontsize=9, color=COLORS[variant], fontweight="bold")

ax.set_ylim(50, 118)
ax.set_ylabel("% fata de OutcomeSelected")
ax.set_title("Rata de conversie (%)\nrelativa la OutcomeSelected", fontsize=10, fontweight="bold")
ax.legend(fontsize=8)
ax.tick_params(axis="x", labelsize=8.5)
style_ax(ax)

# [0,2] Efectul confirmarii: BetAttempted → BetPlaced
ax = axes[0, 2]
placed_vals  = [d_pla, e_pla]
cancel_vals  = [d_cancel, e_cancel]

bars_placed = ax.bar(VARIANTS, placed_vals,
                     color=[COLORS["Dark"], COLORS["Ethical"]],
                     alpha=0.82, edgecolor="white", label="BetPlaced")
bars_cancel = ax.bar(VARIANTS, cancel_vals,
                     bottom=placed_vals,
                     color=["#ff9999", "#aaddaa"], alpha=0.75,
                     edgecolor="white", label="Anulat la confirmare")

for bar, val in zip(bars_placed, placed_vals):
    if val > 0:
        ax.text(bar.get_x() + bar.get_width() / 2,
                bar.get_y() + bar.get_height() / 2,
                f"{val}", ha="center", va="center",
                fontsize=11, fontweight="bold", color="white")
for bar, val in zip(bars_cancel, cancel_vals):
    if val > 0:
        ax.text(bar.get_x() + bar.get_width() / 2,
                bar.get_y() + bar.get_height() / 2,
                f"{val}", ha="center", va="center",
                fontsize=11, fontweight="bold", color="#444")

ax.set_xlabel(
    f"Conversie: Dark {d_conv_pct:.1f}%  vs  Ethical {e_conv_pct:.1f}%\n"
    f"({test_name} p={p_conv:.4f} {pval_label(p_conv)})",
    fontsize=8.5
)
ax.set_ylabel("Numar sesiuni")
ax.set_title("Efectul confirmarii:\nBetAttempted → BetPlaced", fontsize=10, fontweight="bold")
ax.legend(fontsize=8)
style_ax(ax)

# [1,0] Funnel clasic Dark (orizontal)
ax = axes[1, 0]
top  = funnel_counts["Dark"][0] or 1
pcts = [c / top * 100 for c in funnel_counts["Dark"]]
y_pos = np.arange(len(STEP_LABELS_RO))
bars  = ax.barh(y_pos, pcts, align="center",
                color=COLORS["Dark"], alpha=0.75, height=0.55)
for bar, cnt, pct in zip(bars, funnel_counts["Dark"], pcts):
    label = f"{cnt} sesiuni  ({pct:.1f}%)"
    ax.text(min(pct / 2, 40), bar.get_y() + bar.get_height() / 2,
            label, va="center", ha="center",
            fontsize=9, color="white", fontweight="bold")

ax.set_yticks(y_pos)
ax.set_yticklabels(STEP_LABELS_RO, fontsize=9)
ax.set_xlim(0, 112)
ax.set_xlabel("% fata de OutcomeSelected")
ax.set_title(f"Funnel — Dark  (total sesiuni: {total_sessions['Dark']})\n"
             f"Miza preselectionata in {pct_presel:.0f}% din pariurile plasate",
             fontsize=10, fontweight="bold", color=COLORS["Dark"])
ax.invert_yaxis()
style_ax(ax, axis="x")

# [1,1] Funnel clasic Ethical (orizontal)
ax = axes[1, 1]
top  = funnel_counts["Ethical"][0] or 1
pcts = [c / top * 100 for c in funnel_counts["Ethical"]]
y_pos = np.arange(len(STEP_LABELS_RO))
bars  = ax.barh(y_pos, pcts, align="center",
                color=COLORS["Ethical"], alpha=0.75, height=0.55)
for bar, cnt, pct in zip(bars, funnel_counts["Ethical"], pcts):
    label = f"{cnt} sesiuni  ({pct:.1f}%)"
    ax.text(min(pct / 2, 40), bar.get_y() + bar.get_height() / 2,
            label, va="center", ha="center",
            fontsize=9, color="white", fontweight="bold")

# Adnotare BetConfirmation
ax.text(102, 1.5,
        f"Anulari confirmare:\n{n_eth_canc} sesiuni\n({100*n_eth_canc/n_eth_att:.1f}%)",
        fontsize=7.5, color="gray", ha="right", va="center",
        bbox=dict(boxstyle="round,pad=0.3", facecolor="#f0f0f0", edgecolor="gray"))

ax.set_yticks(y_pos)
ax.set_yticklabels(STEP_LABELS_RO, fontsize=9)
ax.set_xlim(0, 112)
ax.set_xlabel("% fata de OutcomeSelected")
ax.set_title(f"Funnel — Ethical  (total sesiuni: {total_sessions['Ethical']})\n"
             f"BetConfirmation: {100*n_eth_conf/n_eth_att:.0f}% vazuta, {100*n_eth_canc/n_eth_att:.0f}% anulata",
             fontsize=10, fontweight="bold", color=COLORS["Ethical"])
ax.invert_yaxis()
style_ax(ax, axis="x")

# [1,2] Dark pattern: miza preselectionata (nivel PARIU)
ax = axes[1, 2]
labels_ps = ["Dark\nmanuala", "Dark\npresel.", "Ethical\nmanual"]
vals_ps   = [n_manual_bets, n_presel_bets, len(eth_df_placed)]
colors_ps = [COLORS["Dark"], "#ff7777", COLORS["Ethical"]]

bars = ax.bar(labels_ps, vals_ps, color=colors_ps, alpha=0.82, edgecolor="white")
for bar, val, lbl in zip(bars, vals_ps, labels_ps):
    pct_of_dark = 100 * val / total_dark_bets if "Dark" in lbl else None
    if pct_of_dark is not None:
        top_txt = f"{val}\n({pct_of_dark:.0f}%)"
    else:
        top_txt = f"{val}\n(100%)"
    ax.text(bar.get_x() + bar.get_width() / 2,
            bar.get_height() + 0.3, top_txt,
            ha="center", va="bottom", fontsize=9, fontweight="bold")

ax.set_xlabel(
    f"Dark: {pct_presel:.0f}% din pariuri au miza preselectionata\n"
    f"(fara actiune StakeChanged — miza default impusa de UI)",
    fontsize=8.5
)
ax.set_ylabel("Numar pariuri BetPlaced")
ax.set_title("Dark pattern: miza preselectionata\n(analiza la nivel de pariu individual)", fontsize=10, fontweight="bold")
style_ax(ax)

plt.suptitle(
    "Week 1 — Funnel de conversie: Dark vs Ethical\n"
    f"Sesiuni totale: Dark={total_sessions['Dark']}  Ethical={total_sessions['Ethical']}",
    fontsize=13, fontweight="bold"
)

plt.savefig(OUTPUT_CHARTS / "week1_funnel.png", dpi=300, bbox_inches="tight")
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week1_funnel_analysis.csv'}")
print(f"  {OUTPUT_CHARTS / 'week1_funnel.png'}")
