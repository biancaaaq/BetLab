# -*- coding: utf-8 -*-
"""Retentia W1: sesiuni/user, zile active, DAU zilnic, curba retentie ziua 1-7."""

import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
import matplotlib.ticker as mticker
import numpy as np
from pathlib import Path
from scipy.stats import mannwhitneyu

DATA_PATH     = Path("../data/InteractionLogs_Week1.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week1")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

COLORS   = {"Dark": "#d62728", "Ethical": "#2ca02c"}
VARIANTS = ["Dark", "Ethical"]

df = pd.read_csv(DATA_PATH)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)
df["Date"]      = df["Timestamp"].dt.date

def cliffs_delta(a, b):
    a, b = np.asarray(a, float), np.asarray(b, float)
    dom  = (sum(1 for x in a for y in b if x > y)
            - sum(1 for x in a for y in b if x < y))
    return dom / (len(a) * len(b))

def pval_label(p):
    return "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."

def style_ax(ax, axis="y"):
    ax.grid(axis=axis, alpha=0.22, linestyle="--")
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

def boxplot_ret(ax, dark_v, eth_v, ylabel, title):
    """Boxplot colorat cu mediane annotate, MW + Cliff's delta."""
    bp = ax.boxplot(
        [dark_v, eth_v],
        tick_labels=["Dark", "Ethical"],
        patch_artist=True,
        showfliers=True,
        flierprops=dict(marker="o", markersize=4, alpha=0.35, linestyle="none"),
        medianprops=dict(color="black", linewidth=2.2),
        widths=0.52,
    )
    for patch, v in zip(bp["boxes"], VARIANTS):
        patch.set_facecolor(COLORS[v])
        patch.set_alpha(0.72)

    for i, (d, v) in enumerate(zip([dark_v, eth_v], VARIANTS)):
        med = np.median(d)
        ax.text(i + 1.28, med, f"{med:.1f}",
                va="center", ha="left", fontsize=9,
                fontweight="bold", color=COLORS[v])

    stat, p = mannwhitneyu(dark_v, eth_v, alternative="two-sided")
    cd      = cliffs_delta(dark_v, eth_v)
    ax.set_xlabel(f"Mann-Whitney p={p:.4f} {pval_label(p)}   Cliff's d={cd:.2f}", fontsize=8)
    ax.set_ylabel(ylabel, fontsize=9)
    ax.set_title(title, fontsize=10, fontweight="bold")
    style_ax(ax)
    return p, cd

sessions_per_user = (
    df.groupby(["Variant", "UserId"])["SessionId"]
    .nunique().reset_index(name="SessionCount")
)
days_per_user = (
    df.groupby(["Variant", "UserId"])["Date"]
    .nunique().reset_index(name="ActiveDays")
)
user_retention = pd.merge(sessions_per_user, days_per_user, on=["Variant","UserId"])
user_retention["SessionsPerActiveDay"] = (
    user_retention["SessionCount"] / user_retention["ActiveDays"]
).round(2)
user_retention.to_csv(OUTPUT_TABLES / "week1_retention_per_user.csv", index=False)

print("=" * 70)
print("WEEK 1 — RETENTION ANALYSIS")
print("=" * 70)

for metric in ["SessionCount", "ActiveDays", "SessionsPerActiveDay"]:
    dark_v = user_retention[user_retention["Variant"]=="Dark"][metric].values
    eth_v  = user_retention[user_retention["Variant"]=="Ethical"][metric].values
    stat, p = mannwhitneyu(dark_v, eth_v, alternative="two-sided")
    cd      = cliffs_delta(dark_v, eth_v)
    print(f"\n{metric}: Dark med={np.median(dark_v):.2f}  Ethical med={np.median(eth_v):.2f}"
          f"  p={p:.4f} {pval_label(p)}  Cliff's d={cd:.3f}")

daily_active = (
    df.groupby(["Variant","Date"])["UserId"]
    .nunique().reset_index(name="ActiveUsers")
)
daily_active["Date"] = pd.to_datetime(daily_active["Date"])
daily_active.to_csv(OUTPUT_TABLES / "week1_daily_active_users.csv", index=False)

daily_sessions = (
    df.groupby(["Variant","Date"])["SessionId"]
    .nunique().reset_index(name="Sessions")
)
daily_sessions["Date"] = pd.to_datetime(daily_sessions["Date"])

all_dates_sorted = sorted(df["Date"].unique())
date_to_day = {d: i+1 for i, d in enumerate(all_dates_sorted)}  # 1..7

user_days = (
    df.groupby(["Variant","UserId"])["Date"]
    .apply(lambda x: set(x))
    .reset_index()
)

retention_data = {}
for variant in VARIANTS:
    vdf     = user_days[user_days["Variant"] == variant]
    n_users = len(vdf)
    ret_pct = {}
    for day_num in range(1, 8):
        target_date = all_dates_sorted[day_num - 1] if day_num <= len(all_dates_sorted) else None
        if target_date is None:
            ret_pct[day_num] = 0.0
        else:
            active = sum(1 for dates in vdf["Date"] if target_date in dates)
            ret_pct[day_num] = 100 * active / n_users
    retention_data[variant] = ret_pct

print("\n--- Curba de retentie (% useri activi in ziua N) ---")
for variant in VARIANTS:
    vals_str = "  ".join(f"D{k}: {v:.0f}%" for k, v in retention_data[variant].items())
    print(f"  {variant}: {vals_str}")



fig, axes = plt.subplots(2, 3, figsize=(18, 10))
fig.subplots_adjust(hspace=0.52, wspace=0.40)

dark_u = user_retention[user_retention["Variant"]=="Dark"]
eth_u  = user_retention[user_retention["Variant"]=="Ethical"]

boxplot_ret(axes[0,0],
            dark_u["SessionCount"].values, eth_u["SessionCount"].values,
            "Numar sesiuni", "Sesiuni per utilizator")

boxplot_ret(axes[0,1],
            dark_u["ActiveDays"].values, eth_u["ActiveDays"].values,
            "Numar zile active", "Zile active per utilizator")

boxplot_ret(axes[0,2],
            dark_u["SessionsPerActiveDay"].values, eth_u["SessionsPerActiveDay"].values,
            "Sesiuni / zi activa", "Sesiuni per zi activa")

# [1,0] DAU (utilizatori activi per zi)
ax = axes[1, 0]
for variant in VARIANTS:
    vdf = daily_active[daily_active["Variant"]==variant].sort_values("Date")
    ax.plot(vdf["Date"], vdf["ActiveUsers"],
            marker="o", linewidth=2.2, markersize=6,
            label=variant, color=COLORS[variant])
    ax.fill_between(vdf["Date"], vdf["ActiveUsers"], alpha=0.10, color=COLORS[variant])
    for _, row in vdf.iterrows():
        ax.text(row["Date"], row["ActiveUsers"] + 0.15, str(int(row["ActiveUsers"])),
                ha="center", va="bottom", fontsize=7.5, color=COLORS[variant])

ax.set_title("Utilizatori activi per zi (DAU)", fontsize=10, fontweight="bold")
ax.set_ylabel("Utilizatori activi")
ax.legend(handles=[mpatches.Patch(color=COLORS[v], label=v) for v in VARIANTS], fontsize=8)
ax.xaxis.set_major_formatter(plt.matplotlib.dates.DateFormatter("%d %b"))
ax.tick_params(axis="x", rotation=20)
style_ax(ax)

# [1,1] Sesiuni per zi
ax = axes[1, 1]
for variant in VARIANTS:
    vdf = daily_sessions[daily_sessions["Variant"]==variant].sort_values("Date")
    ax.plot(vdf["Date"], vdf["Sessions"],
            marker="o", linewidth=2.2, markersize=6,
            label=variant, color=COLORS[variant])
    ax.fill_between(vdf["Date"], vdf["Sessions"], alpha=0.10, color=COLORS[variant])
    for _, row in vdf.iterrows():
        ax.text(row["Date"], row["Sessions"] + 0.2, str(int(row["Sessions"])),
                ha="center", va="bottom", fontsize=7.5, color=COLORS[variant])

ax.set_title("Sesiuni per zi", fontsize=10, fontweight="bold")
ax.set_ylabel("Numar sesiuni")
ax.legend(handles=[mpatches.Patch(color=COLORS[v], label=v) for v in VARIANTS], fontsize=8)
ax.xaxis.set_major_formatter(plt.matplotlib.dates.DateFormatter("%d %b"))
ax.tick_params(axis="x", rotation=20)
style_ax(ax)

# [1,2] Curba de retentie
ax = axes[1, 2]
days = list(range(1, 8))
for variant in VARIANTS:
    pcts = [retention_data[variant][d] for d in days]
    ax.plot(days, pcts,
            marker="o", linewidth=2.5, markersize=8,
            label=variant, color=COLORS[variant])
    ax.fill_between(days, pcts, alpha=0.10, color=COLORS[variant])
    for day, pct in zip(days, pcts):
        ax.text(day, pct + 1.5, f"{pct:.0f}%",
                ha="center", va="bottom", fontsize=8,
                color=COLORS[variant], fontweight="bold")

ax.set_xlim(0.5, 7.5)
ax.set_ylim(0, 115)
ax.set_xticks(days)
ax.set_xticklabels([f"Ziua {d}" for d in days], fontsize=8)
ax.set_xlabel("Ziua din saptamana (W1: 15-21 mai)")
ax.set_ylabel("% utilizatori activi")
ax.set_title("Curba de retentie\n(% useri activi in ziua N)", fontsize=10, fontweight="bold")
ax.legend(handles=[mpatches.Patch(color=COLORS[v], label=v) for v in VARIANTS], fontsize=8)
style_ax(ax)

med_d_sess = dark_u["SessionCount"].median()
med_e_sess = eth_u["SessionCount"].median()

plt.suptitle(
    f"Week 1 — Retentia utilizatorilor: Dark vs Ethical\n"
    f"Sesiuni median: Dark={med_d_sess:.0f}  Ethical={med_e_sess:.0f}"
    f"  (x{med_d_sess/med_e_sess:.1f})",
    fontsize=13, fontweight="bold"
)

plt.savefig(OUTPUT_CHARTS / "week1_retention.png", dpi=300, bbox_inches="tight")
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week1_retention_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'week1_daily_active_users.csv'}")
print(f"  {OUTPUT_CHARTS / 'week1_retention.png'}")
