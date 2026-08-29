# -*- coding: utf-8 -*-
"""Efectul ordinii de expunere: Dark prima (Gr.A W1) vs Dark dupa Ethical (Gr.B W2). Ethical prima (Gr.B W1) vs Ethical dupa Dark (Gr.A W2)."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.gridspec as gridspec
from pathlib import Path
from scipy.stats import mannwhitneyu

DATA_W1       = Path("../data/InteractionLogs_Week1.csv")
DATA_W2       = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/crossover")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

w1_metrics = pd.read_csv(OUTPUT_TABLES / "week1_gambling_metrics_per_user.csv")
w2_metrics = pd.read_csv(OUTPUT_TABLES / "week2_gambling_metrics_per_user.csv")
groups     = pd.read_csv(OUTPUT_TABLES / "crossover_groups.csv")

COLORS_GRP  = {"A": "#1f77b4", "B": "#ff7f0e"}
COLORS_VAR  = {"Dark": "#d62728", "Ethical": "#2ca02c"}
METRICS     = ["TotalCoreGamblingActions", "BettingActions",
               "CasinoActions", "FinancialActions", "DarkPatternActions"]
METRICS_SHORT = {"TotalCoreGamblingActions": "TotalCore",
                 "BettingActions": "Betting",
                 "CasinoActions":  "Casino",
                 "FinancialActions": "Financial",
                 "DarkPatternActions": "DarkPattern"}

grp_A_ids = set(groups[groups["Group"] == "A"]["UserId"])
grp_B_ids = set(groups[groups["Group"] == "B"]["UserId"])

# Cele 4 celule ale designului 2x2
# Grup A W1 = Dark, prima expunere
A_w1 = w1_metrics[w1_metrics["UserId"].isin(grp_A_ids) & (w1_metrics["Variant"] == "Dark")]
# Grup B W1 = Ethical, prima expunere
B_w1 = w1_metrics[w1_metrics["UserId"].isin(grp_B_ids) & (w1_metrics["Variant"] == "Ethical")]
# Grup A W2 = Ethical, dupa Dark
A_w2 = w2_metrics[w2_metrics["UserId"].isin(grp_A_ids) & (w2_metrics["Variant"] == "Ethical")]
# Grup B W2 = Dark, dupa Ethical
B_w2 = w2_metrics[w2_metrics["UserId"].isin(grp_B_ids) & (w2_metrics["Variant"] == "Dark")]

print("=" * 70)
print("CROSSOVER ORDER EFFECTS — Design 2x2")
print("=" * 70)
print(f"\n  Grup A: n={len(A_w1)} (Dark W1 -> Ethical W2)")
print(f"  Grup B: n={len(B_w1)} (Ethical W1 -> Dark W2)")

print("\n\n=== TABEL 2x2 — Mediане per celula ===")
print(f"{'Metrica':<30} {'A-Dark(W1)':>12} {'B-Eth(W1)':>12} "
      f"{'A-Eth(W2)':>12} {'B-Dark(W2)':>12}")
print("-" * 70)

table_rows = []
for m in METRICS:
    a1 = A_w1[m].median(); b1 = B_w1[m].median()
    a2 = A_w2[m].median(); b2 = B_w2[m].median()
    print(f"  {m:<28} {a1:>12.1f} {b1:>12.1f} {a2:>12.1f} {b2:>12.1f}")
    table_rows.append({"Metric": m,
                       "A_Dark_W1": round(a1, 2), "B_Ethical_W1": round(b1, 2),
                       "A_Ethical_W2": round(a2, 2), "B_Dark_W2": round(b2, 2)})

table_2x2 = pd.DataFrame(table_rows)
table_2x2.to_csv(OUTPUT_TABLES / "crossover_2x2_table.csv", index=False)

print("\n\n=== DARK: prima expunere (A-W1) vs dupa Ethical (B-W2) ===")
print("Intrebare: Dark e mai puternic dupa ce ai vazut Ethical?")
print(f"{'Metrica':<30} {'A-Dark-W1':>10} {'B-Dark-W2':>10} {'p':>10} {'sig':>6}")
print("-" * 60)

dark_comparison = []
for m in METRICS:
    a1v = A_w1[m]; b2v = B_w2[m]
    stat, p = mannwhitneyu(a1v, b2v, alternative="two-sided")
    sig = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
    direction = "B>A" if b2v.median() > a1v.median() else "A>B"
    print(f"  {m:<28} {a1v.median():>10.1f} {b2v.median():>10.1f} "
          f"{p:>10.4f} {sig:>6}  ({direction})")
    dark_comparison.append({"Metric": m, "A_Dark_W1_med": round(a1v.median(), 2),
                            "B_Dark_W2_med": round(b2v.median(), 2),
                            "Direction": direction, "p": round(p, 6), "sig": sig})

pd.DataFrame(dark_comparison).to_csv(
    OUTPUT_TABLES / "crossover_dark_order_comparison.csv", index=False)

print("\n\n=== ETHICAL: prima expunere (B-W1) vs dupa Dark (A-W2) ===")
print("Intrebare: Ethical e mai protectiv dupa ce ai vazut Dark?")
print(f"{'Metrica':<30} {'B-Eth-W1':>10} {'A-Eth-W2':>10} {'p':>10} {'sig':>6}")
print("-" * 60)

ethical_comparison = []
for m in METRICS:
    b1v = B_w1[m]; a2v = A_w2[m]
    stat, p = mannwhitneyu(b1v, a2v, alternative="two-sided")
    sig = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
    direction = "A2>B1 (carryover)" if a2v.median() > b1v.median() else "B1>A2"
    print(f"  {m:<28} {b1v.median():>10.1f} {a2v.median():>10.1f} "
          f"{p:>10.4f} {sig:>6}  ({direction})")
    ethical_comparison.append({"Metric": m, "B_Ethical_W1_med": round(b1v.median(), 2),
                                "A_Ethical_W2_med": round(a2v.median(), 2),
                                "Direction": direction, "p": round(p, 6), "sig": sig})

pd.DataFrame(ethical_comparison).to_csv(
    OUTPUT_TABLES / "crossover_ethical_order_comparison.csv", index=False)

w1_raw = pd.read_csv(DATA_W1)
w2_raw = pd.read_csv(DATA_W2)

def conf_stats(df, user_ids, label):
    sub = df[df["UserId"].isin(user_ids) & (df["Variant"] == "Ethical")]
    viewed    = (sub["EventType"] == "BetConfirmationViewed").sum()
    cancelled = (sub["EventType"] == "BetConfirmationCancelled").sum()
    placed    = (sub["EventType"] == "BetPlaced").sum()
    attempted = (sub["EventType"] == "BetAttempted").sum()
    rate = cancelled / viewed * 100 if viewed > 0 else 0
    users_cancelled = sub[sub["EventType"] == "BetConfirmationCancelled"]["UserId"].nunique()
    total_users = sub["UserId"].nunique()
    print(f"\n  [{label}]")
    print(f"    BetAttempted: {attempted}  Viewed: {viewed}  "
          f"Cancelled: {cancelled}  Placed: {placed}")
    print(f"    Protection rate: {rate:.1f}%")
    print(f"    Utilizatori care au anulat: {users_cancelled}/{total_users}")
    return {"Label": label, "Viewed": viewed, "Cancelled": cancelled,
            "ProtectionRate": round(rate, 1), "UsersWhoCancel": users_cancelled,
            "TotalUsers": total_users}

print("\n\n=== BETCONFIRMATION: protectia etica dupa ordine ===")
print("Grup B W1 = Ethical prima oara")
print("Grup A W2 = Ethical dupa ce a vazut Dark")

conf_b_w1 = conf_stats(w1_raw, grp_B_ids, "Grup B Ethical W1 (prima oara Ethical)")
conf_a_w2 = conf_stats(w2_raw, grp_A_ids, "Grup A Ethical W2 (dupa Dark)")

conf_df = pd.DataFrame([conf_b_w1, conf_a_w2])
conf_df.to_csv(OUTPUT_TABLES / "crossover_betconfirmation_by_order.csv", index=False)

diff_rate = conf_a_w2["ProtectionRate"] - conf_b_w1["ProtectionRate"]
print(f"\n  Diferenta rata protectie: {conf_a_w2['ProtectionRate']:.1f}% - "
      f"{conf_b_w1['ProtectionRate']:.1f}% = +{diff_rate:.1f}pp")
print(f"  Interpretare: utilizatorii expusi anterior la Dark anuleaza de "
      f"{conf_a_w2['ProtectionRate']/conf_b_w1['ProtectionRate']:.1f}x mai des")

fig = plt.figure(figsize=(18, 14))
gs  = gridspec.GridSpec(3, 3, figure=fig, hspace=0.4, wspace=0.35)

metrics_plot = ["TotalCoreGamblingActions", "BettingActions", "CasinoActions"]
for col, m in enumerate(metrics_plot):
    ax = fig.add_subplot(gs[0, col])
    a1 = A_w1[m].values; a2 = A_w2[m].values
    b1 = B_w1[m].values; b2 = B_w2[m].values

    for v1, v2 in zip(a1, a2):
        ax.plot([0, 1], [v1, v2], color=COLORS_GRP["A"], alpha=0.25, linewidth=0.8)
    for v1, v2 in zip(b1, b2):
        ax.plot([0, 1], [v1, v2], color=COLORS_GRP["B"], alpha=0.25, linewidth=0.8)

    ax.plot([0, 1], [np.median(a1), np.median(a2)], color=COLORS_GRP["A"],
            linewidth=3, marker="o", markersize=10, zorder=5,
            label=f"Gr.A D→E  {np.median(a1):.0f}→{np.median(a2):.0f}")
    ax.plot([0, 1], [np.median(b1), np.median(b2)], color=COLORS_GRP["B"],
            linewidth=3, marker="s", markersize=10, zorder=5,
            label=f"Gr.B E→D  {np.median(b1):.0f}→{np.median(b2):.0f}")

    ax.set_xticks([0, 1])
    ax.set_xticklabels(["W1", "W2"], fontsize=10)
    ax.set_title(METRICS_SHORT[m], fontsize=11, fontweight="bold")
    ax.set_ylabel("Actiuni per user")
    ax.legend(fontsize=8)
    ax.grid(alpha=0.3)

# Dark order comparison — boxplot
ax = fig.add_subplot(gs[1, 0])
for j, (data, label, color) in enumerate([
    (A_w1["TotalCoreGamblingActions"], "A-Dark\n(W1, prima oara)", COLORS_GRP["A"]),
    (B_w2["TotalCoreGamblingActions"], "B-Dark\n(W2, dupa Ethical)", COLORS_GRP["B"])
]):
    bp = ax.boxplot([data], positions=[j], widths=0.5, patch_artist=True,
                    boxprops=dict(facecolor=color, alpha=0.7))
    ax.text(j, data.median() + 2, f"{data.median():.0f}",
            ha="center", fontsize=9, fontweight="bold", color=color)
ax.set_xticks([0, 1])
ax.set_xticklabels(["Grup A\nWeek 1", "Grup B\nWeek 2"], fontsize=8)
ax.set_title("TotalCore — Dark\nGrup A (Week 1) vs Grup B (Week 2)", fontsize=10, fontweight="bold")
ax.set_ylabel("Actiuni"); ax.grid(axis="y", alpha=0.3)
_, p_d = mannwhitneyu(A_w1["TotalCoreGamblingActions"],
                      B_w2["TotalCoreGamblingActions"], alternative="two-sided")
sig_d = "***" if p_d < 0.001 else "**" if p_d < 0.01 else "*" if p_d < 0.05 else "n.s."
ax.set_xlabel(f"p={p_d:.4f} {sig_d}", fontsize=9)

ax = fig.add_subplot(gs[1, 1])
for j, (data, color) in enumerate([
    (B_w1["TotalCoreGamblingActions"], COLORS_GRP["B"]),
    (A_w2["TotalCoreGamblingActions"], COLORS_GRP["A"])
]):
    bp = ax.boxplot([data], positions=[j], widths=0.5, patch_artist=True,
                    boxprops=dict(facecolor=color, alpha=0.7))
    ax.text(j, data.median() + 0.5, f"{data.median():.0f}",
            ha="center", fontsize=9, fontweight="bold", color=color)
ax.set_xticks([0, 1])
ax.set_xticklabels(["Grup B\nWeek 1", "Grup A\nWeek 2"], fontsize=8)
ax.set_title("TotalCore — Ethical\nGrup B (Week 1) vs Grup A (Week 2)", fontsize=10, fontweight="bold")
ax.set_ylabel("Actiuni"); ax.grid(axis="y", alpha=0.3)
_, p_e = mannwhitneyu(B_w1["TotalCoreGamblingActions"],
                      A_w2["TotalCoreGamblingActions"], alternative="two-sided")
sig_e = "***" if p_e < 0.001 else "**" if p_e < 0.01 else "*" if p_e < 0.05 else "n.s."
ax.set_xlabel(f"p={p_e:.4f} {sig_e}", fontsize=9)

ax = fig.add_subplot(gs[1, 2])
labels_conf = ["Grup B\nWeek 1", "Grup A\nWeek 2"]
rates       = [conf_b_w1["ProtectionRate"], conf_a_w2["ProtectionRate"]]
users_canc  = [conf_b_w1["UsersWhoCancel"], conf_a_w2["UsersWhoCancel"]]
total_users_conf = [conf_b_w1["TotalUsers"], conf_a_w2["TotalUsers"]]
bar_colors  = [COLORS_GRP["B"], COLORS_GRP["A"]]
bars = ax.bar(labels_conf, rates, color=bar_colors, alpha=0.85, edgecolor="white", width=0.5)
for bar, rate in zip(bars, rates):
    ax.text(bar.get_x() + bar.get_width() / 2, bar.get_height() + 0.5,
            f"{rate:.1f}%", ha="center", va="bottom", fontsize=11, fontweight="bold")
ax.set_title("BetConfirmation Protection Rate\ndupa ordine", fontsize=10, fontweight="bold")
ax.set_ylabel("% pariuri anulate la confirmare")
ax.set_ylim(0, max(rates) * 1.4)
ax.grid(axis="y", alpha=0.3)

ax = fig.add_subplot(gs[2, :])
metrics_hm = ["TotalCoreGamblingActions", "BettingActions", "CasinoActions",
              "FinancialActions", "DarkPatternActions"]
labels_rows = ["Grup A\nDark — Week 1",
               "Grup B\nEthical — Week 1",
               "Grup A\nEthical — Week 2",
               "Grup B\nDark — Week 2"]
data_cells = [
    [A_w1[m].median() for m in metrics_hm],
    [B_w1[m].median() for m in metrics_hm],
    [A_w2[m].median() for m in metrics_hm],
    [B_w2[m].median() for m in metrics_hm],
]
hm_data = np.array(data_cells, dtype=float)

# Normalizeaza per coloana
hm_norm = hm_data / (hm_data.max(axis=0, keepdims=True) + 1e-9)

im = ax.imshow(hm_norm, cmap="RdYlGn_r", aspect="auto", vmin=0, vmax=1)
ax.set_xticks(range(len(metrics_hm)))
ax.set_xticklabels([METRICS_SHORT[m] for m in metrics_hm], fontsize=10)
ax.set_yticks(range(4))
ax.set_yticklabels(labels_rows, fontsize=9)
ax.set_title("Heatmap 2×2: Mediane per celula (normalizat per coloana — rosu=mai mult gambling)",
             fontsize=11, fontweight="bold")

for i in range(4):
    for j in range(len(metrics_hm)):
        ax.text(j, i, f"{hm_data[i, j]:.1f}",
                ha="center", va="center", fontsize=9,
                color="white" if hm_norm[i, j] > 0.6 else "black",
                fontweight="bold")

plt.colorbar(im, ax=ax, shrink=0.6, label="Intensitate relativa (per metrica)")

ax.axhline(1.5, color="white", linewidth=2, linestyle="--")

plt.suptitle("Efectul Ordinii de Expunere — Design Crossover 2×2\n"
             "Albastru = Grup A (Dark→Ethical)   Portocaliu = Grup B (Ethical→Dark)",
             fontsize=13, fontweight="bold")
plt.savefig(OUTPUT_CHARTS / "crossover_order_effects.png", dpi=300, bbox_inches="tight")
plt.close()

print("\n\nSaved:")
print(f"  {OUTPUT_TABLES / 'crossover_2x2_table.csv'}")
print(f"  {OUTPUT_TABLES / 'crossover_dark_order_comparison.csv'}")
print(f"  {OUTPUT_TABLES / 'crossover_ethical_order_comparison.csv'}")
print(f"  {OUTPUT_TABLES / 'crossover_betconfirmation_by_order.csv'}")
print(f"  {OUTPUT_CHARTS / 'crossover_order_effects.png'}")
