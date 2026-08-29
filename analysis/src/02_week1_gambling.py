# -*- coding: utf-8 -*-
"""Metrici gambling per user (W1): BettingActions, CasinoActions, DarkPatternActions, TotalCoreGamblingActions. Mann-Whitney + Cliff's delta."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from pathlib import Path
from scipy.stats import mannwhitneyu
from utils import BETTING_EV, CASINO_EV, FINANCIAL_EV, DARK_EV

DATA_PATH     = Path("../data/InteractionLogs_Week1.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week1")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

COLORS = {"Dark": "#d62728", "Ethical": "#2ca02c"}

METRICS = [
    "BettingActions",
    "CasinoActions",
    "DarkPatternActions",
    "TotalCoreGamblingActions",
]

METRIC_LABELS_RO = {
    "BettingActions":           "Actiuni pariere",
    "CasinoActions":            "Actiuni cazino",
    "DarkPatternActions":       "Dark pattern",
    "TotalCoreGamblingActions": "Total gambling",
}


def cliffs_delta(a, b):
    """Cliff's delta (pozitiv => a > b)."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    dom = (
        sum(1 for x in a for y in b if x > y)
        - sum(1 for x in a for y in b if x < y)
    )
    return dom / (len(a) * len(b))

def sig_label(p):
    return "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."

def style_ax(ax, axis="y"):
    ax.grid(axis=axis, alpha=0.22, linestyle="--")
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

def boxplot_colored(ax, dark_vals, eth_vals, ylabel, title):
    """Boxplot Dark/Ethical cu culori, mediane annotate, p-value + Cliff's delta."""
    bp = ax.boxplot(
        [dark_vals, eth_vals],
        tick_labels=["Dark", "Ethical"],
        patch_artist=True,
        showfliers=True,
        flierprops=dict(marker="o", markersize=4, alpha=0.35, linestyle="none"),
        medianprops=dict(color="black", linewidth=2.2),
        widths=0.52,
    )
    for patch, v in zip(bp["boxes"], ["Dark", "Ethical"]):
        patch.set_facecolor(COLORS[v])
        patch.set_alpha(0.72)

    for i, (d, v) in enumerate(zip([dark_vals, eth_vals], ["Dark","Ethical"])):
        med = np.median(d)
        ax.text(i + 1.28, med, f"{med:.0f}",
                va="center", ha="left", fontsize=9,
                fontweight="bold", color=COLORS[v])

    stat, p = mannwhitneyu(dark_vals, eth_vals, alternative="two-sided")
    cd      = cliffs_delta(dark_vals, eth_vals)
    ax.set_xlabel(f"p={p:.4f} {sig_label(p)}   Cliff's δ={cd:.2f}", fontsize=8)
    ax.set_ylabel(ylabel, fontsize=9)
    ax.set_title(title, fontsize=10, fontweight="bold")
    style_ax(ax)
    return p, cd



df = pd.read_csv(DATA_PATH)

print("=" * 60)
print("GAMBLING METRICS PER USER — Week 1")
print("=" * 60)


betting_pu   = (df[df["EventType"].isin(BETTING_EV)]
                .groupby(["Variant","UserId"]).size()
                .reset_index(name="BettingActions"))
casino_pu    = (df[df["EventType"].isin(CASINO_EV)]
                .groupby(["Variant","UserId"]).size()
                .reset_index(name="CasinoActions"))
financial_pu = (df[df["EventType"].isin(FINANCIAL_EV)]
                .groupby(["Variant","UserId"]).size()
                .reset_index(name="FinancialActions"))
dark_pu      = (df[df["EventType"].isin(DARK_EV)]
                .groupby(["Variant","UserId"]).size()
                .reset_index(name="DarkPatternActions"))

user_metrics = (
    betting_pu
    .merge(casino_pu,    on=["Variant","UserId"], how="outer")
    .merge(financial_pu, on=["Variant","UserId"], how="outer")
    .merge(dark_pu,      on=["Variant","UserId"], how="outer")
    .fillna(0)
)
user_metrics["TotalCoreGamblingActions"] = (
    user_metrics["BettingActions"] + user_metrics["CasinoActions"]
)
user_metrics.to_csv(OUTPUT_TABLES / "week1_gambling_metrics_per_user.csv", index=False)


print("\n--- Statistici descriptive ---")
summary = user_metrics.groupby("Variant")[METRICS].describe().round(2)
summary.to_csv(OUTPUT_TABLES / "week1_gambling_metrics_summary.csv")
print(summary)

print("\n--- Teste statistice (Mann-Whitney) + Cliff's delta ---")
test_rows = []
for metric in METRICS:
    dark_v = user_metrics[user_metrics["Variant"]=="Dark"][metric].values
    eth_v  = user_metrics[user_metrics["Variant"]=="Ethical"][metric].values
    stat, p = mannwhitneyu(dark_v, eth_v, alternative="two-sided")
    cd      = cliffs_delta(dark_v, eth_v)
    lbl     = sig_label(p)
    print(f"  {metric:<30} U={stat:.0f}  p={p:.6f} {lbl}  d={cd:.3f}")
    test_rows.append({
        "Metric": metric,
        "Dark_median":    round(np.median(dark_v), 2),
        "Ethical_median": round(np.median(eth_v), 2),
        "U": stat, "p": round(p, 8), "sig": lbl,
        "CliffsDelta": round(cd, 4),
    })

test_df = pd.DataFrame(test_rows)
test_df.to_csv(OUTPUT_TABLES / "week1_statistical_tests.csv", index=False)

# Event audit
dark_evs    = set(df[df["Variant"]=="Dark"]["EventType"])
ethical_evs = set(df[df["Variant"]=="Ethical"]["EventType"])
print("\n--- Evente exclusive Dark:", sorted(dark_evs - ethical_evs))
print("--- Evente exclusive Ethical:", sorted(ethical_evs - dark_evs))



fig, axes = plt.subplots(2, 3, figsize=(16, 10))
fig.subplots_adjust(hspace=0.52, wspace=0.38)

dark_u = user_metrics[user_metrics["Variant"]=="Dark"]
eth_u  = user_metrics[user_metrics["Variant"]=="Ethical"]

box_specs = [
    ("BettingActions",           "Actiuni per utilizator", "Pariere sportiva\n(actiuni per utilizator)"),
    ("CasinoActions",            "Actiuni per utilizator", "Cazino\n(actiuni per utilizator)"),
    ("TotalCoreGamblingActions", "Actiuni per utilizator", "Total gambling\n(pariere + cazino)"),
    ("DarkPatternActions",       "Actiuni per utilizator", "Dark pattern cues\n(NearMiss etc.)"),
]

positions = [(0,0), (0,1), (0,2), (1,0)]
p_vals, cd_vals = {}, {}

for (r, c), (metric, ylabel, title) in zip(positions, box_specs):
    ax = axes[r][c]
    p, cd = boxplot_colored(
        ax,
        dark_u[metric].values,
        eth_u[metric].values,
        ylabel, title
    )
    p_vals[metric]  = p
    cd_vals[metric] = cd

ax_bar = axes[1][1]
metrics_plot = ["BettingActions", "CasinoActions", "TotalCoreGamblingActions"]
labels_short  = ["Pariere", "Cazino", "Total"]
x = np.arange(len(metrics_plot))
width = 0.35

for j, variant in enumerate(["Dark", "Ethical"]):
    meds = [
        user_metrics[user_metrics["Variant"]==variant][m].median()
        for m in metrics_plot
    ]
    bars = ax_bar.bar(x + j*width, meds, width,
                      label=variant, color=COLORS[variant], alpha=0.80,
                      edgecolor="white")
    for bar, med in zip(bars, meds):
        ax_bar.text(bar.get_x() + bar.get_width()/2, bar.get_height() + 1.5,
                    f"{med:.0f}", ha="center", va="bottom",
                    fontsize=8, fontweight="bold")

for i, metric in enumerate(metrics_plot):
    med_d = dark_u[metric].median()
    med_e = eth_u[metric].median()
    ratio = med_d / med_e if med_e > 0 else 0
    ax_bar.text(i + width/2, max(med_d, med_e) * 1.12,
                f"×{ratio:.1f}", ha="center", fontsize=9,
                color="black", fontweight="bold")

ax_bar.set_xticks(x + width/2)
ax_bar.set_xticklabels(labels_short, fontsize=9)
ax_bar.set_ylabel("Mediana actiuni per utilizator", fontsize=9)
ax_bar.set_title("Mediane comparative\nDark vs Ethical (×ratio)", fontsize=10, fontweight="bold")
ax_bar.legend(handles=[mpatches.Patch(color=COLORS[v], label=v) for v in ["Dark","Ethical"]],
              fontsize=8)
style_ax(ax_bar)

ax_cd = axes[1][2]
cd_metrics = ["BettingActions", "CasinoActions",
              "DarkPatternActions", "TotalCoreGamblingActions"]
cd_labels  = ["Pariere", "Cazino", "Dark pattern", "Total"]
cd_values  = [cd_vals[m] for m in cd_metrics]
colors_cd  = ["#d62728" if v > 0.7 else "#ff7f0e" if v > 0.4 else "#2ca02c"
              for v in cd_values]
bars = ax_cd.barh(cd_labels, cd_values, color=colors_cd, alpha=0.82, edgecolor="white")
for bar, val, metric in zip(bars, cd_values, cd_metrics):
    p_v = p_vals[metric]
    ax_cd.text(val + 0.01, bar.get_y() + bar.get_height()/2,
               f"{val:.2f}  {sig_label(p_v)}",
               va="center", fontsize=9, fontweight="bold")

ax_cd.axvline(0.7, color="gray", linestyle="--", alpha=0.5, linewidth=1)
ax_cd.axvline(0.4, color="gray", linestyle=":", alpha=0.4, linewidth=1)
ax_cd.set_xlim(0, 1.15)
ax_cd.set_xlabel("Cliff's delta (0=niciun efect, 1=efect maxim)", fontsize=8)
ax_cd.set_title("Marimea efectului\n(Cliff's delta per metrica)", fontsize=10, fontweight="bold")
ax_cd.text(0.71, -0.55, "mare", fontsize=7, color="gray")
ax_cd.text(0.41, -0.55, "mediu", fontsize=7, color="gray")
style_ax(ax_cd, axis="x")

med_d_total = dark_u["TotalCoreGamblingActions"].median()
med_e_total = eth_u["TotalCoreGamblingActions"].median()

plt.suptitle(
    f"Week 1 — Actiuni de gambling per utilizator: Dark vs Ethical\n"
    f"Total gambling median: Dark={med_d_total:.0f}  Ethical={med_e_total:.0f}"
    f"  (×{med_d_total/med_e_total:.1f})",
    fontsize=13, fontweight="bold"
)

plt.savefig(OUTPUT_CHARTS / "week1_gambling_metrics.png", dpi=300, bbox_inches="tight")
plt.close()

print("\nSaved:")
for f in [
    OUTPUT_TABLES / "week1_gambling_metrics_per_user.csv",
    OUTPUT_TABLES / "week1_gambling_metrics_summary.csv",
    OUTPUT_TABLES / "week1_statistical_tests.csv",
    OUTPUT_CHARTS / "week1_gambling_metrics.png",
]:
    print(f"  {f}")
