# -*- coding: utf-8 -*-
"""Efecte period + carryover: compara aceeasi varianta intre saptamani. Testeaza daca expunerea la Dark lasa comportament persistent in Ethical."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from pathlib import Path
from scipy.stats import wilcoxon, mannwhitneyu

OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/crossover")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

merged = pd.read_csv(OUTPUT_TABLES/"crossover_per_user.csv")
w1     = pd.read_csv(OUTPUT_TABLES/"week1_gambling_metrics_per_user.csv")
w2     = pd.read_csv(OUTPUT_TABLES/"week2_gambling_metrics_per_user.csv")

COLORS     = {"Dark":"#d62728","Ethical":"#2ca02c"}
COLORS_GRP = {"A":"#1f77b4","B":"#ff7f0e"}
METRICS    = ["BettingActions","CasinoActions","FinancialActions",
              "DarkPatternActions","TotalCoreGamblingActions"]

grp_A = merged[merged["Group"]=="A"]   # Dark W1 → Ethical W2
grp_B = merged[merged["Group"]=="B"]   # Ethical W1 → Dark W2

print("=" * 70)
print("CROSSOVER — PERIOD + CARRYOVER EFFECTS")
print("=" * 70)

# Metoda corecta: comparam valorile din aceeasi varianta intre saptamani
#   - Dark in W1 (Group A) vs Dark in W2 (Group B) → period + group
#   - Ethical in W1 (Group B) vs Ethical in W2 (Group A) → period + group
print("\n\n=== A) PERIOD EFFECT ===")
print("Comparam comportamentul in aceeasi varianta, saptamani diferite:")

period_rows = []
for variant in ["Dark","Ethical"]:
    # W1: utilizatorii cu varianta respectiva in W1
    w1_var = w1[w1["Variant"]==variant]
    # W2: utilizatorii cu varianta respectiva in W2
    w2_var = w2[w2["Variant"]==variant]

    for m in ["TotalCoreGamblingActions","BettingActions","CasinoActions"]:
        v1 = w1_var[m]; v2 = w2_var[m]
        stat, p = mannwhitneyu(v1, v2, alternative="two-sided")
        sig     = "***" if p<0.001 else "**" if p<0.01 else "*" if p<0.05 else "n.s."
        print(f"  [{variant}] {m}")
        print(f"    W1 mean={v1.mean():.2f}  W2 mean={v2.mean():.2f}  "
              f"U={stat:.0f}  p={p:.4f}  {sig}")
        period_rows.append({"Variant":variant,"Metric":m,
                             "W1_mean":round(v1.mean(),2),"W2_mean":round(v2.mean(),2),
                             "U":stat,"p":round(p,6),"sig":sig})

period_df = pd.DataFrame(period_rows)
period_df.to_csv(OUTPUT_TABLES/"crossover_period_effects.csv", index=False)

# Daca A > B => dark patterns au creat comportament persistent
print("\n\n=== B) CARRYOVER EFFECT ===")
print("Group A in Ethical (W2) vs Group B in Ethical (W1):")
print("(Daca A > B => expunerea la Dark lasa urme in Ethical)")

# Group A: Ethical = W2
A_ethical_w2 = w2[w2["UserId"].isin(grp_A["UserId"]) & (w2["Variant"]=="Ethical")]
# Group B: Ethical = W1
B_ethical_w1 = w1[w1["UserId"].isin(grp_B["UserId"]) & (w1["Variant"]=="Ethical")]

carryover_rows = []
for m in METRICS:
    a_vals = A_ethical_w2[m] if m in A_ethical_w2.columns else pd.Series([0]*len(grp_A))
    b_vals = B_ethical_w1[m] if m in B_ethical_w1.columns else pd.Series([0]*len(grp_B))
    if len(a_vals)>0 and len(b_vals)>0:
        stat, p = mannwhitneyu(a_vals, b_vals, alternative="two-sided")
    else:
        stat, p = 0, 1.0
    sig = "***" if p<0.001 else "**" if p<0.01 else "*" if p<0.05 else "n.s."
    direction = "A>B (carryover)" if a_vals.mean()>b_vals.mean() else "B>A (no carryover)"
    print(f"\n  {m}")
    print(f"    GroupA Ethical(W2) mean={a_vals.mean():.2f}  med={a_vals.median():.1f}")
    print(f"    GroupB Ethical(W1) mean={b_vals.mean():.2f}  med={b_vals.median():.1f}")
    print(f"    {direction}  U={stat:.0f}  p={p:.6f}  {sig}")
    carryover_rows.append({"Metric":m,
        "A_Ethical_W2_mean":round(a_vals.mean(),2),
        "B_Ethical_W1_mean":round(b_vals.mean(),2),
        "Direction":direction,"U":stat,"p":round(p,6),"sig":sig})

carryover_df = pd.DataFrame(carryover_rows)
carryover_df.to_csv(OUTPUT_TABLES/"crossover_carryover_effects.csv", index=False)

print("\n\n=== C) WITHIN-GROUP CHANGE ===")

w1_sub = w1[["UserId"]+METRICS].rename(columns={m:f"{m}_W1" for m in METRICS})
w2_sub = w2[["UserId"]+METRICS].rename(columns={m:f"{m}_W2" for m in METRICS})
within = merged[["UserId","Group"]].merge(w1_sub, on="UserId").merge(w2_sub, on="UserId")

change_rows = []
for grp_name, grp_label in [("A","Dark→Ethical"), ("B","Ethical→Dark")]:
    gdf = within[within["Group"]==grp_name]
    print(f"\n  Group {grp_name} ({grp_label}):")
    for m in ["TotalCoreGamblingActions","BettingActions","CasinoActions"]:
        w1v = gdf[f"{m}_W1"]; w2v = gdf[f"{m}_W2"]
        diff = w2v - w1v
        if (diff!=0).any():
            stat, p = wilcoxon(w1v, w2v, alternative="two-sided")
        else:
            stat, p = 0, 1.0
        sig = "***" if p<0.001 else "**" if p<0.01 else "*" if p<0.05 else "n.s."
        direction = "crestere" if diff.mean()>0 else "scadere"
        print(f"    {m}: W1={w1v.mean():.1f} → W2={w2v.mean():.1f} "
              f"(Δ={diff.mean():+.1f} {direction})  p={p:.4f} {sig}")
        change_rows.append({"Group":grp_name,"Transition":grp_label,"Metric":m,
            "W1_mean":round(w1v.mean(),2),"W2_mean":round(w2v.mean(),2),
            "Delta":round(diff.mean(),2),"p":round(p,6),"sig":sig})

change_df = pd.DataFrame(change_rows)
change_df.to_csv(OUTPUT_TABLES/"crossover_within_group_change.csv", index=False)

fig, axes = plt.subplots(2, 3, figsize=(18, 11))

metric_labels = {
    "TotalCoreGamblingActions":"TotalCore\nGambling",
    "BettingActions":"Betting\nActions",
    "CasinoActions":"Casino\nActions",
    "FinancialActions":"Financial\nActions",
    "DarkPatternActions":"DarkPattern\nActions",
}

ax = axes[0,0]
A_means = [A_ethical_w2[m].mean() if m in A_ethical_w2 else 0 for m in METRICS]
B_means = [B_ethical_w1[m].mean() if m in B_ethical_w1 else 0 for m in METRICS]
x = np.arange(len(METRICS)); width=0.35
bars_a = ax.bar(x-width/2, A_means, width, label="Gr.A Ethical (W2)\n[a fost Dark]",
                color=COLORS_GRP["A"], alpha=0.85)
bars_b = ax.bar(x+width/2, B_means, width, label="Gr.B Ethical (W1)",
                color=COLORS_GRP["B"], alpha=0.85)
for bars in [bars_a, bars_b]:
    for bar in bars:
        h = bar.get_height()
        if h>0.5: ax.text(bar.get_x()+bar.get_width()/2, h+0.3,
                          f"{h:.1f}", ha="center", va="bottom", fontsize=7)
ax.set_xticks(x); ax.set_xticklabels([metric_labels[m] for m in METRICS], fontsize=8)
ax.set_title("Carryover Effect\nGrup A Ethical(W2) vs Grup B Ethical(W1)", fontsize=10)
ax.set_ylabel("Actiuni per utilizator"); ax.legend(fontsize=8); ax.grid(axis="y", alpha=0.3)

ax = axes[0,1]
gdf_a = within[within["Group"]=="A"]
for m in ["TotalCoreGamblingActions","BettingActions","CasinoActions"]:
    w1v = gdf_a[f"{m}_W1"].values
    w2v = gdf_a[f"{m}_W2"].values
    for u_w1, u_w2 in zip(w1v, w2v):
        ax.plot([0,1],[u_w1,u_w2], color=COLORS_GRP["A"], alpha=0.35, linewidth=0.8)
    ax.plot([0,1],[np.median(w1v),np.median(w2v)], color=COLORS_GRP["A"],
            linewidth=3, marker="o", markersize=8,
            label=f"{m[:7]} med.{np.median(w1v):.0f}→{np.median(w2v):.0f}")
ax.set_xticks([0,1])
ax.set_xticklabels(["Week 1", "Week 2"], fontsize=9)
ax.set_title("Grup A: Dark(Week 1) → Ethical(Week 2)\n(schimbare per user)", fontsize=10)
ax.legend(fontsize=7); ax.grid(alpha=0.3); ax.set_ylabel("Actiuni")

ax = axes[0,2]
gdf_b = within[within["Group"]=="B"]
for m in ["TotalCoreGamblingActions","BettingActions","CasinoActions"]:
    w1v = gdf_b[f"{m}_W1"].values
    w2v = gdf_b[f"{m}_W2"].values
    for u_w1, u_w2 in zip(w1v, w2v):
        ax.plot([0,1],[u_w1,u_w2], color=COLORS_GRP["B"], alpha=0.35, linewidth=0.8)
    ax.plot([0,1],[np.median(w1v),np.median(w2v)], color=COLORS_GRP["B"],
            linewidth=3, marker="o", markersize=8,
            label=f"{m[:7]} med.{np.median(w1v):.0f}→{np.median(w2v):.0f}")
ax.set_xticks([0,1])
ax.set_xticklabels(["Week 1", "Week 2"], fontsize=9)
ax.set_title("Grup B: Ethical(Week 1) → Dark(Week 2)\n(schimbare per user)", fontsize=10)
ax.legend(fontsize=7); ax.grid(alpha=0.3); ax.set_ylabel("Actiuni")

ax = axes[1,0]
dark_w1 = w1[w1["Variant"]=="Dark"]["TotalCoreGamblingActions"]
dark_w2 = w2[w2["Variant"]=="Dark"]["TotalCoreGamblingActions"]
bp = ax.boxplot([dark_w1, dark_w2],
                tick_labels=["Dark\nWeek 1", "Dark\nWeek 2"],
                patch_artist=True)
bp["boxes"][0].set_facecolor(COLORS["Dark"]); bp["boxes"][0].set_alpha(0.6)
bp["boxes"][1].set_facecolor(COLORS["Dark"]); bp["boxes"][1].set_alpha(0.9)
_, p_pd = mannwhitneyu(dark_w1, dark_w2, alternative="two-sided")
sig_pd = "***" if p_pd<0.001 else "**" if p_pd<0.01 else "*" if p_pd<0.05 else "n.s."
ax.set_title(f"Period Effect — Dark\np={p_pd:.4f} {sig_pd}", fontsize=10)
ax.set_ylabel("TotalCoreGamblingActions"); ax.grid(axis="y", alpha=0.3)

ax = axes[1,1]
eth_w1 = w1[w1["Variant"]=="Ethical"]["TotalCoreGamblingActions"]
eth_w2 = w2[w2["Variant"]=="Ethical"]["TotalCoreGamblingActions"]
bp = ax.boxplot([eth_w1, eth_w2],
                tick_labels=["Ethical\nWeek 1", "Ethical\nWeek 2"],
                patch_artist=True)
bp["boxes"][0].set_facecolor(COLORS["Ethical"]); bp["boxes"][0].set_alpha(0.6)
bp["boxes"][1].set_facecolor(COLORS["Ethical"]); bp["boxes"][1].set_alpha(0.9)
_, p_pe = mannwhitneyu(eth_w1, eth_w2, alternative="two-sided")
sig_pe = "***" if p_pe<0.001 else "**" if p_pe<0.01 else "*" if p_pe<0.05 else "n.s."
ax.set_title(f"Period Effect — Ethical\np={p_pe:.4f} {sig_pe}", fontsize=10)
ax.set_ylabel("TotalCoreGamblingActions"); ax.grid(axis="y", alpha=0.3)

ax = axes[1,2]
metrics_short = ["Betting","Casino","Financial","DarkPattern","TotalCore"]
delta_A = [change_df[(change_df["Group"]=="A")&(change_df["Metric"]==m)]["Delta"].values[0]
           if len(change_df[(change_df["Group"]=="A")&(change_df["Metric"]==m)])>0 else 0
           for m in METRICS]
delta_B = [change_df[(change_df["Group"]=="B")&(change_df["Metric"]==m)]["Delta"].values[0]
           if len(change_df[(change_df["Group"]=="B")&(change_df["Metric"]==m)])>0 else 0
           for m in METRICS]
x = np.arange(len(METRICS)); width=0.35
ax.bar(x-width/2, delta_A, width, label="Gr.A (D→E, scade)", color=COLORS_GRP["A"], alpha=0.85)
ax.bar(x+width/2, delta_B, width, label="Gr.B (E→D, creste)", color=COLORS_GRP["B"], alpha=0.85)
ax.axhline(0, color="black", linewidth=0.8, linestyle="--")
ax.set_xticks(x); ax.set_xticklabels(metrics_short, fontsize=8)
ax.set_title("Delta W2-W1 per grup\n(negativ=scadere, pozitiv=crestere)", fontsize=10)
ax.set_ylabel("Δ (W2 - W1)"); ax.legend(fontsize=8); ax.grid(axis="y", alpha=0.3)

plt.suptitle("Crossover Analysis — Period Effects + Carryover + Within-Group Change",
             fontsize=12, fontweight="bold")
fig.subplots_adjust(top=0.93, bottom=0.06, hspace=0.55, wspace=0.38)

# Linie separatoare intre rand 1 (carryover/within-change) si rand 2 (period/delta)
sep_y = 0.505
fig.add_artist(plt.Line2D([0.02, 0.98], [sep_y, sep_y],
               transform=fig.transFigure, color="#888888",
               linewidth=1.8, linestyle="--", zorder=10, alpha=0.70))
fig.text(0.01, sep_y + 0.010, "↑  Week 1 → Week 2  (schimbare intra-grup + carryover)",
         fontsize=7.5, color="#555555", alpha=0.90, va="bottom", style="italic")
fig.text(0.01, sep_y - 0.018, "↓  Period effect + Delta W2−W1",
         fontsize=7.5, color="#555555", alpha=0.90, va="top", style="italic")

plt.savefig(OUTPUT_CHARTS/"crossover_period_carryover.png", dpi=300, bbox_inches="tight")
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES/'crossover_period_effects.csv'}")
print(f"  {OUTPUT_TABLES/'crossover_carryover_effects.csv'}")
print(f"  {OUTPUT_TABLES/'crossover_within_group_change.csv'}")
print(f"  {OUTPUT_CHARTS/'crossover_period_carryover.png'}")
