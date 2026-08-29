# -*- coding: utf-8 -*-
"""Analiza financiara: stake per user din MetadataJson (BetPlaced/BetAttempted/StakeChanged). Mann-Whitney W1/W2/combined + Wilcoxon crossover."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import json, re
from pathlib import Path
from scipy.stats import mannwhitneyu, wilcoxon

DATA_W1       = Path("../data/InteractionLogs_Week1.csv")
DATA_W2       = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
(OUTPUT_CHARTS / "stake").mkdir(parents=True, exist_ok=True)
OUT_C = OUTPUT_CHARTS / "stake"

COLORS = {"Dark": "#d62728", "Ethical": "#2ca02c"}

def parse_meta(s):
    """Returneaza dict din MetadataJson; {} daca nu se poate parsa."""
    if not isinstance(s, str) or not s.strip():
        return {}
    s = s.strip()
    # unquote outer quotes daca exista (artefact CSV)
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1].replace('""', '"')
    try:
        return json.loads(s)
    except Exception:
        return {}

def extract_stake(meta_str):
    m = parse_meta(meta_str)
    try:
        return float(m.get("stake", None))
    except (TypeError, ValueError):
        return np.nan

def extract_selections(meta_str):
    m = parse_meta(meta_str)
    try:
        return int(m.get("selectionCount", None))
    except (TypeError, ValueError):
        return np.nan

w1 = pd.read_csv(DATA_W1)
w2 = pd.read_csv(DATA_W2)
w1["Week"] = 1
w2["Week"] = 2
df = pd.concat([w1, w2], ignore_index=True)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)

for event_type in ["BetPlaced", "BetAttempted"]:
    mask = df["EventType"] == event_type
    df.loc[mask, "Stake"]         = df.loc[mask, "MetadataJson"].apply(extract_stake)
    df.loc[mask, "SelectionCount"]= df.loc[mask, "MetadataJson"].apply(extract_selections)

# Extrage si din StakeChanged (NewValue contine miza)
sc_mask = df["EventType"] == "StakeChanged"
df.loc[sc_mask, "Stake"] = pd.to_numeric(df.loc[sc_mask, "NewValue"], errors="coerce")

placed   = df[df["EventType"] == "BetPlaced"].copy()
attempted= df[df["EventType"] == "BetAttempted"].copy()

print("=" * 70)
print("STAKE ANALYSIS — Week1 + Week2 combined")
print("=" * 70)
print(f"\nBetPlaced total: {len(placed)}  (stake valid: {placed['Stake'].notna().sum()})")
print(f"BetAttempted total: {len(attempted)}  (stake valid: {attempted['Stake'].notna().sum()})")
print(f"\nStake BetPlaced — range: {placed['Stake'].min():.0f} – {placed['Stake'].max():.0f} RON")
print(f"Stake BetPlaced — medie: {placed['Stake'].mean():.2f}  mediana: {placed['Stake'].median():.2f}")

def user_stake_metrics(frame, label):
    rows = []
    for (uid, variant), gdf in frame.groupby(["UserId", "Variant"]):
        stakes = gdf["Stake"].dropna()
        sels   = gdf["SelectionCount"].dropna()
        rows.append({
            "UserId"         : uid,
            "Variant"        : variant,
            "Week"           : gdf["Week"].iloc[0],
            "NumBets"        : len(stakes),
            "TotalStake"     : round(stakes.sum(), 2),
            "AvgStakePerBet" : round(stakes.mean(), 2) if len(stakes) > 0 else 0,
            "MaxStake"       : round(stakes.max(), 2)  if len(stakes) > 0 else 0,
            "AvgSelections"  : round(sels.mean(), 2)   if len(sels) > 0 else 0,
        })
    df_out = pd.DataFrame(rows)
    df_out.to_csv(OUTPUT_TABLES / f"{label}_stake_per_user.csv", index=False)
    return df_out

placed_w1   = placed[placed["Week"] == 1]
placed_w2   = placed[placed["Week"] == 2]

metrics_w1  = user_stake_metrics(placed_w1,  "week1")
metrics_w2  = user_stake_metrics(placed_w2,  "week2")
metrics_all = user_stake_metrics(placed,     "combined")

def print_and_test(metrics_df, label):
    print(f"\n\n{'='*70}")
    print(f"STAKE METRICS — {label}")
    print(f"{'='*70}")
    results = []
    for metric in ["NumBets", "TotalStake", "AvgStakePerBet", "MaxStake", "AvgSelections"]:
        dark    = metrics_df[metrics_df["Variant"] == "Dark"][metric].fillna(0)
        ethical = metrics_df[metrics_df["Variant"] == "Ethical"][metric].fillna(0)
        if len(dark) > 0 and len(ethical) > 0:
            stat, p = mannwhitneyu(dark, ethical, alternative="two-sided")
        else:
            stat, p = 0, 1.0
        sig = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
        print(f"\n  {metric}")
        print(f"    Dark    mean={dark.mean():.2f}  median={dark.median():.2f}")
        print(f"    Ethical mean={ethical.mean():.2f}  median={ethical.median():.2f}")
        print(f"    U={stat:.0f}  p={p:.6f}  {sig}")
        results.append({"Metric": metric, "Label": label,
                        "Dark_mean": round(dark.mean(), 2),
                        "Dark_med":  round(dark.median(), 2),
                        "Eth_mean":  round(ethical.mean(), 2),
                        "Eth_med":   round(ethical.median(), 2),
                        "U": stat, "p": round(p, 6), "sig": sig})
    return results

r1  = print_and_test(metrics_w1,  "Week1")
r2  = print_and_test(metrics_w2,  "Week2")
rall= print_and_test(metrics_all, "Combined")

all_results = pd.DataFrame(r1 + r2 + rall)
all_results.to_csv(OUTPUT_TABLES / "stake_statistical_tests.csv", index=False)

crossover = pd.read_csv(OUTPUT_TABLES / "crossover_per_user.csv")

# Calculeaza stake dark/ethical per user din metrics_all
stake_by_user = metrics_all[["UserId", "Variant", "TotalStake", "AvgStakePerBet", "NumBets"]]
stake_dark    = stake_by_user[stake_by_user["Variant"] == "Dark"].rename(
    columns={"TotalStake": "TotalStake_Dark", "AvgStakePerBet": "AvgStake_Dark",
             "NumBets": "NumBets_Dark"})
stake_eth     = stake_by_user[stake_by_user["Variant"] == "Ethical"].rename(
    columns={"TotalStake": "TotalStake_Eth", "AvgStakePerBet": "AvgStake_Eth",
             "NumBets": "NumBets_Eth"})

cross_stake = (crossover[["UserId", "Group"]]
    .merge(stake_dark[["UserId", "TotalStake_Dark", "AvgStake_Dark", "NumBets_Dark"]], on="UserId")
    .merge(stake_eth[["UserId",  "TotalStake_Eth",  "AvgStake_Eth",  "NumBets_Eth"]],  on="UserId"))
cross_stake.to_csv(OUTPUT_TABLES / "crossover_stake.csv", index=False)

print(f"\n\n{'='*70}")
print("CROSSOVER STAKE — Wilcoxon Signed-Rank (within-subject)")
print(f"{'='*70}")
for d_col, e_col, label in [
    ("TotalStake_Dark",  "TotalStake_Eth",  "TotalStake"),
    ("AvgStake_Dark",    "AvgStake_Eth",    "AvgStakePerBet"),
    ("NumBets_Dark",     "NumBets_Eth",     "NumBets"),
]:
    dv = cross_stake[d_col].fillna(0)
    ev = cross_stake[e_col].fillna(0)
    diff = dv - ev
    if (diff != 0).any():
        stat, p = wilcoxon(dv, ev, alternative="two-sided")
    else:
        stat, p = 0, 1.0
    sig = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
    print(f"\n  {label}")
    print(f"    Dark    mean={dv.mean():.2f}  median={dv.median():.2f}")
    print(f"    Ethical mean={ev.mean():.2f}  median={ev.median():.2f}")
    print(f"    Diff mean={diff.mean():+.2f}  W={stat:.1f}  p={p:.8f}  {sig}")

fig, axes = plt.subplots(2, 3, figsize=(18, 11))

ax = axes[0, 0]
for j, variant in enumerate(["Dark", "Ethical"]):
    data = metrics_all[metrics_all["Variant"] == variant]["TotalStake"]
    ax.boxplot([data], positions=[j], widths=0.5, patch_artist=True,
               boxprops=dict(facecolor=COLORS[variant], alpha=0.7))
ax.set_xticks([0, 1]); ax.set_xticklabels(["Dark", "Ethical"])
ax.set_title("Total Stake per User\n(W1+W2 combined)", fontsize=10, fontweight="bold")
ax.set_ylabel("RON"); ax.grid(axis="y", alpha=0.3)
r = all_results[(all_results["Metric"] == "TotalStake") & (all_results["Label"] == "Combined")].iloc[0]
ax.set_xlabel(f"p={r['p']:.4f} {r['sig']}", fontsize=9)

ax = axes[0, 1]
for j, variant in enumerate(["Dark", "Ethical"]):
    data = metrics_all[metrics_all["Variant"] == variant]["AvgStakePerBet"].fillna(0)
    ax.boxplot([data], positions=[j], widths=0.5, patch_artist=True,
               boxprops=dict(facecolor=COLORS[variant], alpha=0.7))
ax.set_xticks([0, 1]); ax.set_xticklabels(["Dark", "Ethical"])
ax.set_title("Avg Stake per Bet\n(W1+W2 combined)", fontsize=10, fontweight="bold")
ax.set_ylabel("RON"); ax.grid(axis="y", alpha=0.3)
r = all_results[(all_results["Metric"] == "AvgStakePerBet") & (all_results["Label"] == "Combined")].iloc[0]
ax.set_xlabel(f"p={r['p']:.4f} {r['sig']}", fontsize=9)

ax = axes[0, 2]
for variant in ["Dark", "Ethical"]:
    stakes = placed[placed["Variant"] == variant]["Stake"].dropna()
    ax.hist(stakes, bins=20, alpha=0.6, label=variant,
            color=COLORS[variant], edgecolor="white")
ax.set_title("Distributia Stake la BetPlaced\n(toate evenimentele)", fontsize=10, fontweight="bold")
ax.set_xlabel("Stake (RON)"); ax.set_ylabel("Frecventa")
ax.legend(); ax.grid(axis="y", alpha=0.3)

ax = axes[1, 0]
x = np.arange(2); width = 0.3
for i, label, metrics in [
    (0, "W1", metrics_w1), (1, "W2", metrics_w2)
]:
    for j, variant in enumerate(["Dark", "Ethical"]):
        val = metrics[metrics["Variant"] == variant]["TotalStake"].median()
        offset = width * (j - 0.5)
        bar = ax.bar(i + offset, val, width,
                     color=COLORS[variant], alpha=0.85,
                     label=variant if i == 0 else "")
        ax.text(i + offset, val + 2, f"{val:.0f}",
                ha="center", va="bottom", fontsize=9, fontweight="bold")
ax.set_xticks([0, 1]); ax.set_xticklabels(["Week 1", "Week 2"])
ax.set_title("Total Stake mediana per user\nW1 vs W2", fontsize=10, fontweight="bold")
ax.set_ylabel("RON"); ax.legend(); ax.grid(axis="y", alpha=0.3)

ax = axes[1, 1]
for _, row in cross_stake.iterrows():
    color = "#1f77b4" if row["Group"] == "A" else "#ff7f0e"
    ax.plot([0, 1], [row["TotalStake_Dark"], row["TotalStake_Eth"]],
            color=color, alpha=0.5, linewidth=1.2, marker="o", markersize=4)
ax.plot([0, 1],
        [cross_stake["TotalStake_Dark"].median(),
         cross_stake["TotalStake_Eth"].median()],
        color="black", linewidth=3, marker="D", markersize=9, zorder=5,
        label=f"Mediana {cross_stake['TotalStake_Dark'].median():.0f}→"
              f"{cross_stake['TotalStake_Eth'].median():.0f} RON")
ax.set_xticks([0, 1]); ax.set_xticklabels(["Dark", "Ethical"])
ax.set_title("Crossover: TotalStake per user\n(albastru=GrA D→E  portocaliu=GrB E→D)",
             fontsize=10, fontweight="bold")
ax.set_ylabel("RON"); ax.legend(fontsize=8); ax.grid(alpha=0.3)

ax = axes[1, 2]
for j, variant in enumerate(["Dark", "Ethical"]):
    data = metrics_all[metrics_all["Variant"] == variant]["AvgSelections"].fillna(0)
    ax.boxplot([data], positions=[j], widths=0.5, patch_artist=True,
               boxprops=dict(facecolor=COLORS[variant], alpha=0.7))
ax.set_xticks([0, 1]); ax.set_xticklabels(["Dark", "Ethical"])
ax.set_title("Avg Selectii per Pariu\n(W1+W2 combined)", fontsize=10, fontweight="bold")
ax.set_ylabel("Numar selectii"); ax.grid(axis="y", alpha=0.3)
r = all_results[(all_results["Metric"] == "AvgSelections") & (all_results["Label"] == "Combined")].iloc[0]
ax.set_xlabel(f"p={r['p']:.4f} {r['sig']}", fontsize=9)

plt.suptitle("Analiza Financiara: Stake si Risc per Utilizator\nDark vs Ethical — Week1 + Week2",
             fontsize=13, fontweight="bold")
plt.tight_layout()
plt.savefig(OUT_C / "stake_analysis.png", dpi=300)
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week1_stake_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'week2_stake_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'combined_stake_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'stake_statistical_tests.csv'}")
print(f"  {OUTPUT_TABLES / 'crossover_stake.csv'}")
print(f"  {OUT_C / 'stake_analysis.png'}")
