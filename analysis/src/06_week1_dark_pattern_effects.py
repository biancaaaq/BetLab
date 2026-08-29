# -*- coding: utf-8 -*-
"""Dark pattern effects W1: NearMiss continuation rate, AutoSpin (W1+W2), corelatie NearMiss vs CasinoSpins per user."""

import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
import numpy as np
import json
from pathlib import Path
from scipy.stats import mannwhitneyu, spearmanr

DATA_W1       = Path("../data/InteractionLogs_Week1.csv")
DATA_W2       = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week1")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

COLORS = {"Dark": "#d62728", "Ethical": "#2ca02c",
          "NearMiss": "#ff7f0e", "AutoSpin": "#d62728",
          "Manual": "#aec7e8"}

def parse_meta(s):
    if not isinstance(s, str) or not s.strip():
        return {}
    s = s.strip()
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1].replace('""', '"')
    try:
        return json.loads(s)
    except Exception:
        return {}

def pval_label(p):
    return "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."

def style_ax(ax, axis="y"):
    ax.grid(axis=axis, alpha=0.22, linestyle="--")
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

def cliffs_delta(a, b):
    a, b = np.asarray(a, float), np.asarray(b, float)
    dom  = (sum(1 for x in a for y in b if x > y)
            - sum(1 for x in a for y in b if x < y))
    return dom / (len(a) * len(b))

df = pd.read_csv(DATA_W1)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)
df = df.sort_values(["SessionId","Timestamp"]).reset_index(drop=True)

w2      = pd.read_csv(DATA_W2)
df_both = pd.concat([df.assign(Week=1), w2.assign(Week=2)], ignore_index=True)

dark_df = df[df["Variant"] == "Dark"].copy()

print("=" * 70)
print("DARK PATTERN EFFECTS — NearMiss + AutoSpin + Miza Preselectionata")
print("=" * 70)

CASINO_SPIN = "CasinoSpinClicked"
NEAR_MISS   = "NearMissShown"

nm_stats = []
for sid in dark_df["SessionId"].unique():
    sdf          = dark_df[dark_df["SessionId"] == sid].reset_index(drop=True)
    had_nm       = NEAR_MISS in sdf["EventType"].values
    total_spins  = (sdf["EventType"] == CASINO_SPIN).sum()
    nm_indices   = sdf.index[sdf["EventType"] == NEAR_MISS].tolist()
    if nm_indices:
        first_nm     = nm_indices[0]
        spins_after  = (sdf.loc[first_nm + 1:, "EventType"] == CASINO_SPIN).sum()
        spins_before = (sdf.loc[:first_nm - 1, "EventType"] == CASINO_SPIN).sum()
        nm_count     = len(nm_indices)
    else:
        spins_after = spins_before = nm_count = 0
    nm_stats.append({
        "SessionId"   : sid,
        "HadNearMiss" : had_nm,
        "NearMissCount": nm_count,
        "TotalSpins"  : total_spins,
        "SpinsAfterNM": spins_after,
        "SpinsBeforeNM": spins_before,
    })

nm_df      = pd.DataFrame(nm_stats)
with_nm    = nm_df[nm_df["HadNearMiss"]]
# Filtram doar sesiunile cu activitate de cazino (TotalSpins > 0)
# pentru a evita contaminarea cu sesiuni pur sportive
without_nm = nm_df[(~nm_df["HadNearMiss"]) & (nm_df["TotalSpins"] > 0)]

continued     = (with_nm["SpinsAfterNM"] >= 1).sum()
continue_rate = continued / len(with_nm) * 100 if len(with_nm) > 0 else 0

stat_nm, p_nm = mannwhitneyu(
    with_nm["TotalSpins"], without_nm["TotalSpins"], alternative="two-sided"
)
cd_nm = cliffs_delta(with_nm["TotalSpins"].values, without_nm["TotalSpins"].values)

nm_df.to_csv(OUTPUT_TABLES / "week1_nearmiss_effect.csv", index=False)

print(f"\n--- A) NearMiss Effect ---")
print(f"  Sesiuni Dark cu NearMiss:  {len(with_nm)}  (medie spins: {with_nm['TotalSpins'].mean():.1f})")
print(f"  Sesiuni Dark fara NearMiss:{len(without_nm)}  (medie spins: {without_nm['TotalSpins'].mean():.1f})")
print(f"  Continua dupa NearMiss:    {continued}/{len(with_nm)} = {continue_rate:.1f}%")
print(f"  Spins dupa primul NM:      {with_nm['SpinsAfterNM'].mean():.1f} (medie)")
print(f"  MW U={stat_nm:.0f}  p={p_nm:.6f} {pval_label(p_nm)}  Cliff's d={cd_nm:.3f}")

spins_all         = df[df["EventType"] == CASINO_SPIN].copy()
spins_all["mode"] = spins_all["MetadataJson"].apply(
    lambda s: parse_meta(s).get("mode", "manual"))
spins_all["is_auto"] = spins_all["mode"] == "auto"

auto_by_variant = (
    spins_all.groupby("Variant")
    .agg(total_spins=("EventType","count"), auto_spins=("is_auto","sum"))
    .reset_index()
)
auto_by_variant["manual_spins"] = auto_by_variant["total_spins"] - auto_by_variant["auto_spins"]
auto_by_variant["auto_rate_pct"] = (
    auto_by_variant["auto_spins"] / auto_by_variant["total_spins"] * 100
).round(1)

print(f"\n--- B) AutoSpin (W1) ---")
print(auto_by_variant[["Variant","total_spins","auto_spins","manual_spins","auto_rate_pct"]].to_string(index=False))

dp_rows = []
for uid in dark_df["UserId"].unique():
    udf = dark_df[dark_df["UserId"] == uid]
    dp_rows.append({
        "UserId"       : uid,
        "NearMissCount": (udf["EventType"] == NEAR_MISS).sum(),
        "CasinoSpins"  : (udf["EventType"] == CASINO_SPIN).sum(),
        "BetPlaced"    : (udf["EventType"] == "BetPlaced").sum(),
    })

dp_user_df = pd.DataFrame(dp_rows)
dp_user_df.to_csv(OUTPUT_TABLES / "week1_dark_pattern_per_user.csv", index=False)

pearson_r = dp_user_df["NearMissCount"].corr(dp_user_df["CasinoSpins"])
spear_r, spear_p = spearmanr(dp_user_df["NearMissCount"], dp_user_df["CasinoSpins"])

print(f"\n--- C) Per user correlations (Dark, n={len(dp_user_df)}) ---")
print(f"  Pearson r (NearMiss vs CasinoSpins):  {pearson_r:.4f}")
print(f"  Spearman r (NearMiss vs CasinoSpins): {spear_r:.4f}  p={spear_p:.6f} {pval_label(spear_p)}")



fig, axes = plt.subplots(2, 3, figsize=(18, 11))
fig.subplots_adjust(hspace=0.52, wspace=0.42)

# [0,0] Boxplot: spins cu NM vs fara NM
ax = axes[0, 0]
bp = ax.boxplot(
    [with_nm["TotalSpins"], without_nm["TotalSpins"]],
    tick_labels=["Cu NearMiss", "Fara NearMiss"],
    patch_artist=True,
    medianprops=dict(color="black", linewidth=2.2),
    flierprops=dict(marker="o", markersize=4, alpha=0.35, linestyle="none"),
    widths=0.52,
)
bp["boxes"][0].set_facecolor(COLORS["NearMiss"]); bp["boxes"][0].set_alpha(0.75)
bp["boxes"][1].set_facecolor("#aaaaaa"); bp["boxes"][1].set_alpha(0.65)

for i, (d, col) in enumerate(zip([with_nm["TotalSpins"], without_nm["TotalSpins"]],
                                  [COLORS["NearMiss"], "#666666"])):
    med = d.median()
    ax.text(i + 1.28, med, f"{med:.0f}",
            va="center", ha="left", fontsize=9, fontweight="bold", color=col)

ax.set_title("Spins per sesiune\n(cu NearMiss vs fara)", fontsize=10, fontweight="bold")
ax.set_ylabel("Numar spins casino")
ax.set_xlabel(f"Mann-Whitney p={p_nm:.4f} {pval_label(p_nm)}   Cliff's d={cd_nm:.2f}", fontsize=8)
style_ax(ax)

# [0,1] Histogram: spins dupa NearMiss
ax = axes[0, 1]
max_bin = min(int(with_nm["SpinsAfterNM"].max()) + 2, 50)
ax.hist(with_nm["SpinsAfterNM"],
        bins=range(0, max_bin),
        color=COLORS["NearMiss"], alpha=0.82, edgecolor="white", align="left")
ax.axvline(with_nm["SpinsAfterNM"].mean(), color="black",
           linestyle="--", linewidth=1.8,
           label=f"Medie: {with_nm['SpinsAfterNM'].mean():.1f}")
ax.axvline(with_nm["SpinsAfterNM"].median(), color="#555555",
           linestyle=":", linewidth=1.5,
           label=f"Mediana: {with_nm['SpinsAfterNM'].median():.0f}")
ax.set_title("Spins dupa primul NearMiss\n(sesiuni Dark cu NearMiss)", fontsize=10, fontweight="bold")
ax.set_xlabel("Numar spins dupa NearMiss")
ax.set_ylabel("Numar sesiuni")
ax.legend(fontsize=8.5)
style_ax(ax)

# [0,2] Rata continuare dupa NearMiss
ax = axes[0, 2]
labels_cont = [f"Continua\n({continued} sesiuni)", f"Opreste\n({len(with_nm)-continued} sesiuni)"]
vals_cont   = [continue_rate, 100 - continue_rate]
colors_cont = [COLORS["NearMiss"], "#cccccc"]
bars = ax.bar(labels_cont, vals_cont, color=colors_cont, alpha=0.85,
              edgecolor="white", width=0.55)
for bar, val in zip(bars, vals_cont):
    ax.text(bar.get_x() + bar.get_width()/2,
            bar.get_height() + 0.8, f"{val:.1f}%",
            ha="center", va="bottom", fontsize=14, fontweight="bold")

# Text central proeminent
ax.text(0.5, 0.60, f"{continue_rate:.0f}%",
        transform=ax.transAxes, ha="center", va="center",
        fontsize=36, fontweight="bold", color=COLORS["NearMiss"], alpha=0.25)

ax.set_ylim(0, 115)
ax.set_ylabel("% sesiuni Dark")
ax.set_title(f"Comportament dupa NearMiss\n(din {len(with_nm)} sesiuni cu NearMiss)",
             fontsize=10, fontweight="bold")
ax.set_xlabel(f"Sesiuni care continua sa joace dupa NearMiss: {continue_rate:.1f}%", fontsize=8.5)
style_ax(ax)

# [1,0] AutoSpin stacked bar (W1+W2)
ax = axes[1, 0]
v_names  = ["Dark", "Ethical"]
auto_v   = [int(auto_by_variant[auto_by_variant["Variant"]==v]["auto_spins"].values[0]) for v in v_names]
manual_v = [int(auto_by_variant[auto_by_variant["Variant"]==v]["manual_spins"].values[0]) for v in v_names]
total_v  = [a+m for a,m in zip(auto_v, manual_v)]
x_pos    = np.arange(2)

bars_auto   = ax.bar(x_pos, auto_v,   color=COLORS["AutoSpin"], alpha=0.85,
                     label="AutoSpin (automat)", width=0.55, edgecolor="white")
bars_manual = ax.bar(x_pos, manual_v, bottom=auto_v, color=COLORS["Manual"], alpha=0.80,
                     label="Manual", width=0.55, edgecolor="white")

for i, (a, m, tot) in enumerate(zip(auto_v, manual_v, total_v)):
    if a > 0:
        ax.text(i, a/2, f"{a}", ha="center", va="center",
                fontsize=10, fontweight="bold", color="white")
    if m > 0:
        ax.text(i, a + m/2, f"{m}", ha="center", va="center",
                fontsize=10, fontweight="bold", color="#333")
    ax.text(i, tot + 20, f"Total: {tot}", ha="center", va="bottom", fontsize=8.5)

auto_dark_pct = auto_v[0] / total_v[0] * 100
ax.set_xticks(x_pos); ax.set_xticklabels(["Dark", "Ethical"], fontsize=10)
ax.set_title("AutoSpin — rulare automata\n(Week 1)", fontsize=10, fontweight="bold")
ax.set_ylabel("Numar spins casino")
ax.set_xlabel(f"AutoSpin Dark: {auto_dark_pct:.1f}%  |  Ethical: 0.0%\n"
              f"(eliminarea deciziei deliberate per spin)", fontsize=8.5)
ax.legend(handles=[mpatches.Patch(color=COLORS["AutoSpin"], label="AutoSpin (automat)"),
                   mpatches.Patch(color=COLORS["Manual"], label="Manual")], fontsize=8)
style_ax(ax)

# [1,1] Scatter: NearMiss vs CasinoSpins per user
ax = axes[1, 1]
ax.scatter(dp_user_df["NearMissCount"], dp_user_df["CasinoSpins"],
           color=COLORS["NearMiss"], alpha=0.75, s=80,
           edgecolors="white", linewidths=0.8, zorder=3)

if len(dp_user_df) > 1:
    z      = np.polyfit(dp_user_df["NearMissCount"], dp_user_df["CasinoSpins"], 1)
    x_line = np.linspace(dp_user_df["NearMissCount"].min(),
                         dp_user_df["NearMissCount"].max(), 100)
    ax.plot(x_line, np.poly1d(z)(x_line), "k--", linewidth=1.8, alpha=0.55)

ax.set_title("Corelatie: NearMiss → CasinoSpins\n(per user, Dark — Week 1)",
             fontsize=10, fontweight="bold")
ax.set_ylabel("Spins casino per utilizator")
ax.set_xlabel(
    f"NearMiss-uri per utilizator\n"
    f"Pearson r={pearson_r:.2f}  |  Spearman r={spear_r:.2f}  p={spear_p:.4f} {pval_label(spear_p)}",
    fontsize=8.5
)
style_ax(ax)

# [1,2] Distributia NearMiss-urilor per user
ax = axes[1, 2]
nm_counts = dp_user_df["NearMissCount"]
ax.hist(nm_counts, bins=10, color=COLORS["NearMiss"],
        alpha=0.82, edgecolor="white")
ax.axvline(nm_counts.mean(), color="black", linewidth=1.8, linestyle="--",
           label=f"Medie: {nm_counts.mean():.0f}")
ax.axvline(nm_counts.median(), color="#555", linewidth=1.5, linestyle=":",
           label=f"Mediana: {nm_counts.median():.0f}")

zero_nm_users = (nm_counts == 0).sum()
ax.set_title(f"Distributia NearMiss per user Dark\n"
             f"({zero_nm_users} useri fara niciun NearMiss)",
             fontsize=10, fontweight="bold")
ax.set_xlabel("NearMiss per utilizator")
ax.set_ylabel("Numar utilizatori")
ax.legend(fontsize=8.5)
style_ax(ax)

plt.suptitle(
    "Week 1 — Mecanisme Dark Pattern: NearMiss + AutoSpin\n"
    f"H3: {continue_rate:.0f}% din sesiunile cu NearMiss continua sa joace  |  "
    f"AutoSpin Dark: {auto_dark_pct:.0f}% din spins",
    fontsize=13, fontweight="bold"
)

plt.savefig(OUTPUT_CHARTS / "week1_dark_pattern_effects.png", dpi=300, bbox_inches="tight")
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week1_nearmiss_effect.csv'}")
print(f"  {OUTPUT_TABLES / 'week1_dark_pattern_per_user.png'}")
print(f"  {OUTPUT_CHARTS / 'week1_dark_pattern_effects.png'}")
