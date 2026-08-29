# -*- coding: utf-8 -*-
"""Metrici gambling per user (W2): BettingActions, CasinoActions, DarkPatternActions, TotalCoreGamblingActions. Mann-Whitney + Cliff's delta."""

import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
from pathlib import Path
from scipy.stats import mannwhitneyu
from collections import Counter

DATA_PATH     = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week2")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

df = pd.read_csv(DATA_PATH)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)

VARIANTS = ["Dark", "Ethical"]
COLORS   = {"Dark": "#d62728", "Ethical": "#2ca02c"}

print("=" * 70)
print("WEEK 2 — OVERVIEW")
print("=" * 70)
print(f"\nRanduri totale : {len(df)}")
print(f"Utilizatori    : {df['UserId'].nunique()}")
print(f"Sesiuni        : {df['SessionId'].nunique()}")
print(f"Interval date  : {df['Timestamp'].min().date()} -> {df['Timestamp'].max().date()}")
print(f"\nVariante:\n{df['Variant'].value_counts().to_string()}")
print(f"\nEvenimente (top 20):\n{df['EventType'].value_counts().head(20).to_string()}")

event_counts = (
    df.groupby(["Variant", "EventType"]).size()
    .reset_index(name="Count")
)
pivot = event_counts.pivot(index="EventType", columns="Variant", values="Count").fillna(0)
pivot["Diff_Dark_minus_Ethical"] = pivot.get("Dark", 0) - pivot.get("Ethical", 0)
pivot = pivot.sort_values("Diff_Dark_minus_Ethical", ascending=False)
pivot.to_csv(OUTPUT_TABLES / "week2_event_distribution.csv")

betting_events = ["OutcomeSelected","StakeChanged","BetAttempted","BetPlaced"]
casino_events  = ["CasinoSpinClicked","CasinoRoundCompleted","RouletteSpinPlaced",
                  "BlackjackStart","BlackjackRoundCompleted","WheelSpin","CasinoStakeChanged"]
financial_ev   = ["DepositStarted","DepositCompleted","CashOut"]
dark_pat_ev    = ["NearMissShown","OutcomeRemoved"]

def count_ev(frame, events):
    return frame[frame["EventType"].isin(events)].groupby(["Variant","UserId"]).size().reset_index(name="n")

betting_pu  = count_ev(df, betting_events).rename(columns={"n":"BettingActions"})
casino_pu   = count_ev(df, casino_events).rename(columns={"n":"CasinoActions"})
fin_pu      = count_ev(df, financial_ev).rename(columns={"n":"FinancialActions"})
dp_pu       = count_ev(df, dark_pat_ev).rename(columns={"n":"DarkPatternActions"})

user_metrics = (betting_pu
    .merge(casino_pu,  on=["Variant","UserId"], how="outer")
    .merge(fin_pu,     on=["Variant","UserId"], how="outer")
    .merge(dp_pu,      on=["Variant","UserId"], how="outer")
    .fillna(0))

user_metrics["TotalCoreGamblingActions"] = (
    user_metrics["BettingActions"] + user_metrics["CasinoActions"]
)
user_metrics.to_csv(OUTPUT_TABLES / "week2_gambling_metrics_per_user.csv", index=False)

def cliffs_delta(x, y):
    x, y = list(x), list(y)
    greater = sum(xi > yi for xi in x for yi in y)
    lesser  = sum(xi < yi for xi in x for yi in y)
    return (greater - lesser) / (len(x) * len(y))

METRICS = ["BettingActions","CasinoActions","FinancialActions",
           "DarkPatternActions","TotalCoreGamblingActions"]

print("\n\n" + "=" * 70)
print("WEEK 2 — GAMBLING METRICS SUMMARY + TESTS")
print("=" * 70)

results = []
for metric in METRICS:
    dark    = user_metrics[user_metrics["Variant"]=="Dark"][metric]
    ethical = user_metrics[user_metrics["Variant"]=="Ethical"][metric]
    stat, p = mannwhitneyu(dark, ethical, alternative="two-sided")
    cd      = cliffs_delta(dark, ethical)
    sig     = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
    results.append({"Metric": metric,
                    "Dark_mean": round(dark.mean(),2), "Dark_med": round(dark.median(),2),
                    "Eth_mean":  round(ethical.mean(),2), "Eth_med": round(ethical.median(),2),
                    "U": stat, "p": round(p,8), "sig": sig,
                    "CliffsDelta": round(cd,4)})
    print(f"\n  {metric}")
    print(f"    Dark    mean={dark.mean():.2f}  median={dark.median():.1f}")
    print(f"    Ethical mean={ethical.mean():.2f}  median={ethical.median():.1f}")
    print(f"    U={stat:.0f}  p={p:.8f}  {sig}  d={cd:.4f}")

tests_df = pd.DataFrame(results)
tests_df.to_csv(OUTPUT_TABLES / "week2_statistical_tests.csv", index=False)

fig, axes = plt.subplots(2, 3, figsize=(18, 11))
axes_flat = axes.flatten()

METRIC_LABELS = {
    "BettingActions":           "Betting Actions",
    "CasinoActions":            "Casino Actions",
    "FinancialActions":         "Financial Actions",
    "DarkPatternActions":       "Dark Pattern Actions",
    "TotalCoreGamblingActions": "Total Core Gambling",
}

for ax, metric in zip(axes_flat[:5], METRICS):
    data = [user_metrics[user_metrics["Variant"] == v][metric] for v in VARIANTS]
    bp   = ax.boxplot(data, tick_labels=VARIANTS, patch_artist=True)
    for patch, v in zip(bp["boxes"], VARIANTS):
        patch.set_facecolor(COLORS[v]); patch.set_alpha(0.7)
    for i, (d, v) in enumerate(zip(data, VARIANTS), 1):
        med = d.median()
        ax.text(i, med + 0.3, f"{med:.0f}",
                ha="center", va="bottom", fontsize=8,
                color=COLORS[v], fontweight="bold")
    row = tests_df[tests_df["Metric"] == metric].iloc[0]
    ax.set_xlabel(f"p={row['p']:.4f} {row['sig']}\nd={row['CliffsDelta']:.2f}", fontsize=8)
    ax.set_title(METRIC_LABELS[metric], fontsize=10, fontweight="bold")
    ax.set_ylabel("Actions per User")
    ax.grid(axis="y", alpha=0.3)

ax = axes_flat[5]
top12 = pivot.head(12)
colors_bar = ["#d62728" if v >= 0 else "#2ca02c"
              for v in top12["Diff_Dark_minus_Ethical"]]
ax.barh(top12.index, top12["Diff_Dark_minus_Ethical"],
        color=colors_bar, alpha=0.85, edgecolor="white")
ax.axvline(0, color="black", linewidth=0.8)
ax.set_title("Top Event Differences\n(Dark − Ethical) W2", fontsize=10, fontweight="bold")
ax.set_xlabel("Diferenta numar evenimente")
ax.tick_params(axis="y", labelsize=7)
ax.grid(axis="x", alpha=0.3)

plt.suptitle("Week 2 — Gambling Metrics per User: Dark vs Ethical",
             fontsize=13, fontweight="bold")
plt.tight_layout()
plt.savefig(OUTPUT_CHARTS / "week2_gambling_metrics.png", dpi=300)
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week2_gambling_metrics_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'week2_statistical_tests.csv'}")
print(f"  {OUTPUT_CHARTS / 'week2_gambling_metrics.png'}")
