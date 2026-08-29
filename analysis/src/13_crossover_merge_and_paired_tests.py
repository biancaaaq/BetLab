# -*- coding: utf-8 -*-
"""Crossover within-subject: Wilcoxon Signed-Rank Dark vs Ethical (acelasi user). Group A: Dark→Ethical, Group B: Ethical→Dark."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from pathlib import Path
from scipy.stats import wilcoxon, mannwhitneyu

OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/crossover")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

W1_METRICS = Path("../outputs/tables/week1_gambling_metrics_per_user.csv")
W2_METRICS = Path("../outputs/tables/week2_gambling_metrics_per_user.csv")

w1 = pd.read_csv(W1_METRICS)
w2 = pd.read_csv(W2_METRICS)

COLORS     = {"Dark":"#d62728","Ethical":"#2ca02c"}
COLORS_GRP = {"A":"#1f77b4","B":"#ff7f0e"}

w1_variant = w1[["UserId","Variant"]].rename(columns={"Variant":"VariantW1"})
w2_variant = w2[["UserId","Variant"]].rename(columns={"Variant":"VariantW2"})

groups = w1_variant.merge(w2_variant, on="UserId")
groups["Group"] = groups.apply(
    lambda r: "A" if r["VariantW1"]=="Dark"    and r["VariantW2"]=="Ethical" else
              "B" if r["VariantW1"]=="Ethical" and r["VariantW2"]=="Dark"    else
              "Unknown", axis=1
)
groups.to_csv(OUTPUT_TABLES/"crossover_groups.csv", index=False)

print("=" * 70)
print("CROSSOVER — MERGE + PAIRED TESTS")
print("=" * 70)
print(f"\nGroup A (Dark→Ethical): {(groups['Group']=='A').sum()} utilizatori")
print(f"Group B (Ethical→Dark): {(groups['Group']=='B').sum()} utilizatori")

METRICS = ["BettingActions","CasinoActions","FinancialActions",
           "DarkPatternActions","TotalCoreGamblingActions"]

w1_sub = w1[["UserId"]+METRICS].rename(columns={m:f"{m}_W1" for m in METRICS})
w2_sub = w2[["UserId"]+METRICS].rename(columns={m:f"{m}_W2" for m in METRICS})

merged = (groups
    .merge(w1_sub, on="UserId")
    .merge(w2_sub, on="UserId"))

# Coloane "Dark" si "Ethical" per user (indiferent de saptamana)
for m in METRICS:
    merged[f"{m}_Dark"]    = merged.apply(
        lambda r: r[f"{m}_W1"] if r["VariantW1"]=="Dark" else r[f"{m}_W2"], axis=1)
    merged[f"{m}_Ethical"] = merged.apply(
        lambda r: r[f"{m}_W1"] if r["VariantW1"]=="Ethical" else r[f"{m}_W2"], axis=1)

merged.to_csv(OUTPUT_TABLES/"crossover_per_user.csv", index=False)

print("\n\n" + "=" * 70)
print("WILCOXON SIGNED-RANK — Dark vs Ethical (within-subject)")
print("=" * 70)

def cliffs_delta(x, y):
    x, y = list(x), list(y)
    g = sum(xi>yi for xi in x for yi in y)
    l = sum(xi<yi for xi in x for yi in y)
    return (g-l)/(len(x)*len(y))

wilcoxon_results = []
for m in METRICS:
    dark_vals    = merged[f"{m}_Dark"]
    ethical_vals = merged[f"{m}_Ethical"]
    diff = dark_vals - ethical_vals
    # Wilcoxon necesita cel putin o diferenta nenula
    if (diff != 0).any():
        stat, p = wilcoxon(dark_vals, ethical_vals, alternative="two-sided")
    else:
        stat, p = 0, 1.0
    cd  = cliffs_delta(dark_vals, ethical_vals)
    sig = "***" if p<0.001 else "**" if p<0.01 else "*" if p<0.05 else "n.s."

    print(f"\n  {m}")
    print(f"    Dark    mean={dark_vals.mean():.2f}  med={dark_vals.median():.1f}")
    print(f"    Ethical mean={ethical_vals.mean():.2f}  med={ethical_vals.median():.1f}")
    print(f"    Diff    mean={diff.mean():.2f}  (Dark > Ethical per user)")
    print(f"    Wilcoxon W={stat:.1f}  p={p:.8f}  {sig}  Cliff's d={cd:.4f}")

    wilcoxon_results.append({"Metric":m,
        "Dark_mean":round(dark_vals.mean(),2), "Dark_med":round(dark_vals.median(),2),
        "Ethical_mean":round(ethical_vals.mean(),2), "Ethical_med":round(ethical_vals.median(),2),
        "MeanDiff":round(diff.mean(),2),
        "W":stat, "p":round(p,8), "sig":sig, "CliffsDelta":round(cd,4)})

wilcoxon_df = pd.DataFrame(wilcoxon_results)
wilcoxon_df.to_csv(OUTPUT_TABLES/"crossover_wilcoxon_tests.csv", index=False)

print("\n\n" + "=" * 70)
print("COMPARATIE GRUPURI — comportament in saptamana Dark")
print("=" * 70)

grp_A = merged[merged["Group"]=="A"]
grp_B = merged[merged["Group"]=="B"]

for m in ["TotalCoreGamblingActions","BettingActions","CasinoActions"]:
    # Group A: Dark = W1; Group B: Dark = W2
    A_dark = grp_A[f"{m}_Dark"]
    B_dark = grp_B[f"{m}_Dark"]
    stat, p = mannwhitneyu(A_dark, B_dark, alternative="two-sided") if len(A_dark)>0 and len(B_dark)>0 else (0,1)
    sig = "***" if p<0.001 else "**" if p<0.01 else "*" if p<0.05 else "n.s."
    print(f"\n  {m} (in saptamana Dark)")
    print(f"    Group A (Dark=W1) mean={A_dark.mean():.2f}  med={A_dark.median():.1f}")
    print(f"    Group B (Dark=W2) mean={B_dark.mean():.2f}  med={B_dark.median():.1f}")
    print(f"    Mann-Whitney U={stat:.0f}  p={p:.6f}  {sig}")

fig, axes = plt.subplots(2, 3, figsize=(18, 11))

for ax, m in zip(axes[0], ["BettingActions","CasinoActions","TotalCoreGamblingActions"]):
    dark_v    = merged[f"{m}_Dark"].values
    ethical_v = merged[f"{m}_Ethical"].values
    grp_color = [COLORS_GRP[g] for g in merged["Group"]]

    for i, (d, e, gc) in enumerate(zip(dark_v, ethical_v, grp_color)):
        ax.plot([0, 1], [d, e], color=gc, alpha=0.5, linewidth=1.2, marker="o",
                markersize=5)

    ax.plot([0,1], [np.median(dark_v), np.median(ethical_v)],
            color="black", linewidth=3, marker="D", markersize=8,
            label=f"Mediana\nDark:{np.median(dark_v):.0f} → Eth:{np.median(ethical_v):.0f}",
            zorder=5)

    ax.set_xticks([0,1]); ax.set_xticklabels(["Dark","Ethical"])
    ax.set_title(m, fontsize=10, fontweight="bold")
    ax.set_ylabel("Valoare")
    row = wilcoxon_df[wilcoxon_df["Metric"]==m].iloc[0]
    ax.set_xlabel(f"Wilcoxon p={row['p']:.6f} {row['sig']}", fontsize=8)
    ax.legend(fontsize=7)
    ax.grid(alpha=0.3)

    from matplotlib.lines import Line2D
    leg_el = [Line2D([0],[0],color=COLORS_GRP["A"],lw=2,label="Group A (D→E)"),
              Line2D([0],[0],color=COLORS_GRP["B"],lw=2,label="Group B (E→D)")]
    ax.legend(handles=leg_el+ax.get_legend_handles_labels()[0][0:1], fontsize=7)

metrics_row2 = ["BettingActions","CasinoActions","DarkPatternActions"]
for ax, m in zip(axes[1], metrics_row2):
    dark_v    = merged[f"{m}_Dark"].values
    ethical_v = merged[f"{m}_Ethical"].values
    bp = ax.boxplot([dark_v, ethical_v], tick_labels=["Dark","Ethical"],
                    patch_artist=True)
    bp["boxes"][0].set_facecolor(COLORS["Dark"]);    bp["boxes"][0].set_alpha(0.7)
    bp["boxes"][1].set_facecolor(COLORS["Ethical"]); bp["boxes"][1].set_alpha(0.7)
    ax.set_title(f"{m}\n(within-subject)", fontsize=10, fontweight="bold")
    row = wilcoxon_df[wilcoxon_df["Metric"]==m].iloc[0]
    ax.set_xlabel(f"Wilcoxon p={row['p']:.6f} {row['sig']}\nCliff's d={row['CliffsDelta']:.2f}",
                  fontsize=8)
    ax.grid(axis="y", alpha=0.3)

plt.suptitle("Crossover Analysis — Wilcoxon Signed-Rank (within-subject)\n"
             "Albastru=Group A (Dark→Ethical)  Portocaliu=Group B (Ethical→Dark)",
             fontsize=12, fontweight="bold")
plt.tight_layout()
plt.savefig(OUTPUT_CHARTS/"crossover_wilcoxon_paired.png", dpi=300)
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES/'crossover_groups.csv'}")
print(f"  {OUTPUT_TABLES/'crossover_per_user.csv'}")
print(f"  {OUTPUT_TABLES/'crossover_wilcoxon_tests.csv'}")
print(f"  {OUTPUT_CHARTS/'crossover_wilcoxon_paired.png'}")
