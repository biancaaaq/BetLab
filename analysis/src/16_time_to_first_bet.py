# -*- coding: utf-8 -*-
"""Time-to-First-Wager (TTFW): secunde de la start sesiune la prima mizare (BetPlaced/CasinoSpin/Roulette/Blackjack/WheelSpin). Mann-Whitney + Wilcoxon crossover."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from pathlib import Path
from scipy.stats import mannwhitneyu, wilcoxon

DATA_W1       = Path("../data/InteractionLogs_Week1.csv")
DATA_W2       = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts")
(OUTPUT_CHARTS / "ttfb").mkdir(parents=True, exist_ok=True)
OUT_C = OUTPUT_CHARTS / "ttfb"

COLORS = {"Dark": "#d62728", "Ethical": "#2ca02c"}

# Evenimente care reprezinta o mizare efectiva de bani (pariere SAU cazino)
WAGER_EVENTS = {
    "BetPlaced",           # pariu sportiv confirmat
    "CasinoSpinClicked",   # spin cazino (slot)
    "RouletteSpinPlaced",  # spin ruleta
    "BlackjackStart",      # incepere mana blackjack
    "WheelSpin",           # roata norocului
}

def compute_ttfb(df, week_label):
    """
    Returneaza DataFrame cu o linie per sesiune:
    SessionId, UserId, Variant, Week, TTFB_sec
    TTFB = timp pana la primul eveniment de wagering (BetPlaced SAU CasinoSpin etc.)
    """
    df = df.sort_values(["SessionId", "Timestamp"])
    rows = []
    for sid, sdf in df.groupby("SessionId"):
        sdf = sdf.reset_index(drop=True)
        session_start  = sdf["Timestamp"].iloc[0]
        first_wager    = sdf[sdf["EventType"].isin(WAGER_EVENTS)]["Timestamp"]
        if len(first_wager) == 0:
            continue     # sesiune fara nicio mizare — excludem
        ttfb_sec = (first_wager.iloc[0] - session_start).total_seconds()
        if ttfb_sec < 0:
            ttfb_sec = 0
        rows.append({
            "SessionId" : sid,
            "UserId"    : sdf["UserId"].iloc[0],
            "Variant"   : sdf["Variant"].iloc[0],
            "Week"      : week_label,
            "TTFB_sec"  : round(ttfb_sec, 1),
            "TTFB_min"  : round(ttfb_sec / 60, 2),
        })
    return pd.DataFrame(rows)

w1 = pd.read_csv(DATA_W1)
w2 = pd.read_csv(DATA_W2)
w1["Timestamp"] = pd.to_datetime(w1["Timestamp"], utc=True)
w2["Timestamp"] = pd.to_datetime(w2["Timestamp"], utc=True)

ttfb_w1  = compute_ttfb(w1, "W1")
ttfb_w2  = compute_ttfb(w2, "W2")
ttfb_all = pd.concat([ttfb_w1, ttfb_w2], ignore_index=True)

ttfb_all.to_csv(OUTPUT_TABLES / "ttfb_per_session.csv", index=False)

ttfb_user = (
    ttfb_all.groupby(["UserId", "Variant", "Week"])["TTFB_sec"]
    .agg(["median", "mean", "min", "count"])
    .rename(columns={"median": "MedianTTFB", "mean": "MeanTTFB",
                     "min": "MinTTFB", "count": "BettingSessions"})
    .reset_index()
)
ttfb_user.to_csv(OUTPUT_TABLES / "ttfb_per_user.csv", index=False)

print("=" * 70)
print("TIME TO FIRST WAGER (TTFW) — secunde de la start sesiune la prima mizare")
print("(BetPlaced / CasinoSpin / Roulette / Blackjack / WheelSpin)")
print("=" * 70)

def run_tests(frame, label):
    dark    = frame[frame["Variant"] == "Dark"]["TTFB_sec"]
    ethical = frame[frame["Variant"] == "Ethical"]["TTFB_sec"]
    stat, p = mannwhitneyu(dark, ethical, alternative="two-sided")
    sig     = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
    print(f"\n  [{label}]  n_Dark={len(dark)}  n_Ethical={len(ethical)}")
    print(f"    Dark    median={dark.median():.1f}s  mean={dark.mean():.1f}s  "
          f"min={dark.min():.1f}s")
    print(f"    Ethical median={ethical.median():.1f}s  mean={ethical.mean():.1f}s  "
          f"min={ethical.min():.1f}s")
    print(f"    Mann-Whitney U={stat:.0f}  p={p:.8f}  {sig}")
    return {"Label": label,
            "Dark_med": round(dark.median(), 1), "Dark_mean": round(dark.mean(), 1),
            "Eth_med":  round(ethical.median(), 1), "Eth_mean": round(ethical.mean(), 1),
            "U": stat, "p": round(p, 8), "sig": sig,
            "Ratio_med": round(ethical.median() / dark.median(), 2) if dark.median() > 0 else 0}

results = []
results.append(run_tests(ttfb_w1,  "Week1"))
results.append(run_tests(ttfb_w2,  "Week2"))
results.append(run_tests(ttfb_all, "Combined"))

tests_df = pd.DataFrame(results)
tests_df.to_csv(OUTPUT_TABLES / "ttfb_statistical_tests.csv", index=False)

print(f"\n  Ratio mediana (Ethical / Dark): "
      f"W1={results[0]['Ratio_med']}x  "
      f"W2={results[1]['Ratio_med']}x  "
      f"Total={results[2]['Ratio_med']}x")

crossover = pd.read_csv(OUTPUT_TABLES / "crossover_per_user.csv")

ttfb_user_dark = (ttfb_user[ttfb_user["Variant"] == "Dark"]
    .groupby("UserId")["MedianTTFB"].median().reset_index()
    .rename(columns={"MedianTTFB": "TTFB_Dark"}))
ttfb_user_eth  = (ttfb_user[ttfb_user["Variant"] == "Ethical"]
    .groupby("UserId")["MedianTTFB"].median().reset_index()
    .rename(columns={"MedianTTFB": "TTFB_Ethical"}))

cross_ttfb = (crossover[["UserId", "Group"]]
    .merge(ttfb_user_dark, on="UserId", how="left")
    .merge(ttfb_user_eth,  on="UserId", how="left"))
cross_ttfb = cross_ttfb.dropna(subset=["TTFB_Dark", "TTFB_Ethical"])
cross_ttfb.to_csv(OUTPUT_TABLES / "crossover_ttfb.csv", index=False)

print(f"\n\n{'='*70}")
print("CROSSOVER TTFB — Wilcoxon Signed-Rank (within-subject)")
print(f"{'='*70}")
dv   = cross_ttfb["TTFB_Dark"]
ev   = cross_ttfb["TTFB_Ethical"]
diff = ev - dv
if (diff != 0).any():
    stat, p = wilcoxon(dv, ev, alternative="two-sided")
else:
    stat, p = 0, 1.0
sig = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
print(f"  Dark    median={dv.median():.1f}s  mean={dv.mean():.1f}s")
print(f"  Ethical median={ev.median():.1f}s  mean={ev.mean():.1f}s")
print(f"  Diff (Ethical-Dark) mean={diff.mean():+.1f}s")
print(f"  Wilcoxon W={stat:.1f}  p={p:.8f}  {sig}")
print(f"  Interpretare: utilizatorii mizeaza cu {diff.median():.0f}s mai tarziu in Ethical")

fig, axes = plt.subplots(2, 3, figsize=(18, 11))

ax = axes[0, 0]
for j, v in enumerate(["Dark", "Ethical"]):
    d = ttfb_w1[ttfb_w1["Variant"] == v]["TTFB_sec"]
    ax.boxplot([d], positions=[j], widths=0.5, patch_artist=True,
               boxprops=dict(facecolor=COLORS[v], alpha=0.7))
    ax.text(j, d.median() + 2, f"{d.median():.0f}s",
            ha="center", fontsize=9, fontweight="bold", color=COLORS[v])
ax.set_xticks([0, 1]); ax.set_xticklabels(["Dark", "Ethical"])
ax.set_title("TTFW — Week 1", fontsize=11, fontweight="bold")
ax.set_ylabel("Secunde"); ax.grid(axis="y", alpha=0.3)
r = results[0]
ax.set_xlabel(f"p={r['p']:.6f} {r['sig']}  |  Ethical/{r['Ratio_med']}× Dark",
              fontsize=8)

ax = axes[0, 1]
for j, v in enumerate(["Dark", "Ethical"]):
    d = ttfb_w2[ttfb_w2["Variant"] == v]["TTFB_sec"]
    ax.boxplot([d], positions=[j], widths=0.5, patch_artist=True,
               boxprops=dict(facecolor=COLORS[v], alpha=0.7))
    ax.text(j, d.median() + 2, f"{d.median():.0f}s",
            ha="center", fontsize=9, fontweight="bold", color=COLORS[v])
ax.set_xticks([0, 1]); ax.set_xticklabels(["Dark", "Ethical"])
ax.set_title("TTFW — Week 2", fontsize=11, fontweight="bold")
ax.set_ylabel("Secunde"); ax.grid(axis="y", alpha=0.3)
r = results[1]
ax.set_xlabel(f"p={r['p']:.6f} {r['sig']}  |  Ethical/{r['Ratio_med']}× Dark",
              fontsize=8)

ax = axes[0, 2]
for v in ["Dark", "Ethical"]:
    d = ttfb_all[ttfb_all["Variant"] == v]["TTFB_sec"]
    ax.hist(d, bins=25, alpha=0.6, color=COLORS[v], label=v, edgecolor="white")
    ax.axvline(d.median(), color=COLORS[v], linestyle="--", linewidth=2,
               label=f"{v} med={d.median():.0f}s")
ax.set_title("Distributia TTFW\n(W1+W2 combined)", fontsize=11, fontweight="bold")
ax.set_xlabel("Secunde"); ax.set_ylabel("Frecventa sesiuni")
ax.legend(fontsize=8); ax.grid(axis="y", alpha=0.3)

ax = axes[1, 0]
for _, row in cross_ttfb.iterrows():
    color = "#1f77b4" if row["Group"] == "A" else "#ff7f0e"
    ax.plot([0, 1], [row["TTFB_Dark"], row["TTFB_Ethical"]],
            color=color, alpha=0.5, linewidth=1.2, marker="o", markersize=4)
ax.plot([0, 1], [dv.median(), ev.median()],
        color="black", linewidth=3, marker="D", markersize=9, zorder=5,
        label=f"Mediana {dv.median():.0f}s→{ev.median():.0f}s")
ax.set_xticks([0, 1]); ax.set_xticklabels(["Dark", "Ethical"])
ax.set_title("Crossover: TTFW per user\n(albastru=GrA D→E  portocaliu=GrB E→D)",
             fontsize=10, fontweight="bold")
ax.set_ylabel("Secunde (mediana per user)"); ax.legend(fontsize=8); ax.grid(alpha=0.3)
ax.set_xlabel(f"Wilcoxon p={p:.6f} {sig}", fontsize=8)

ax = axes[1, 1]
ttfb_all["Date"] = ttfb_all.apply(
    lambda r: pd.read_csv(DATA_W1 if r["Week"] == "W1" else DATA_W2,
                          nrows=0).columns[0], axis=1
) if False else None   # placeholder — calculam din timestamp
# Refacem cu timestamp
w1_ts = w1[["SessionId", "Timestamp"]].groupby("SessionId")["Timestamp"].min().reset_index()
w1_ts.columns = ["SessionId", "SessionStart"]
w2_ts = w2[["SessionId", "Timestamp"]].groupby("SessionId")["Timestamp"].min().reset_index()
w2_ts.columns = ["SessionId", "SessionStart"]
ts_all = pd.concat([w1_ts, w2_ts])
ttfb_dated = ttfb_all.merge(ts_all, on="SessionId")
ttfb_dated["Date"] = ttfb_dated["SessionStart"].dt.date

daily_ttfb = (ttfb_dated.groupby(["Variant", "Date"])["TTFB_sec"]
              .median().reset_index())
daily_ttfb["Date"] = pd.to_datetime(daily_ttfb["Date"])

for v in ["Dark", "Ethical"]:
    vd = daily_ttfb[daily_ttfb["Variant"] == v].sort_values("Date")
    ax.plot(vd["Date"], vd["TTFB_sec"], marker="o", linewidth=2,
            color=COLORS[v], label=v)
    ax.fill_between(vd["Date"], vd["TTFB_sec"], alpha=0.1, color=COLORS[v])
ax.set_title("TTFW mediana zilnica\n(W1 + W2)", fontsize=10, fontweight="bold")
ax.set_ylabel("Secunde"); ax.legend(); ax.grid(alpha=0.3)
ax.xaxis.set_major_formatter(plt.matplotlib.dates.DateFormatter("%d %b"))
plt.setp(ax.xaxis.get_majorticklabels(), rotation=30, ha="right")

ax = axes[1, 2]
week_labels  = ["W1 Dark", "W1 Ethical", "W2 Dark", "W2 Ethical"]
median_vals  = [
    ttfb_w1[ttfb_w1["Variant"] == "Dark"]["TTFB_sec"].median(),
    ttfb_w1[ttfb_w1["Variant"] == "Ethical"]["TTFB_sec"].median(),
    ttfb_w2[ttfb_w2["Variant"] == "Dark"]["TTFB_sec"].median(),
    ttfb_w2[ttfb_w2["Variant"] == "Ethical"]["TTFB_sec"].median(),
]
bar_colors = [COLORS["Dark"], COLORS["Ethical"], COLORS["Dark"], COLORS["Ethical"]]
bars = ax.bar(week_labels, median_vals, color=bar_colors, alpha=0.85, edgecolor="white")
for bar, val in zip(bars, median_vals):
    ax.text(bar.get_x() + bar.get_width() / 2, val + 1,
            f"{val:.0f}s", ha="center", va="bottom", fontsize=10, fontweight="bold")
ax.set_title("Mediana TTFW\nW1 vs W2 per varianta", fontsize=10, fontweight="bold")
ax.set_ylabel("Secunde"); ax.grid(axis="y", alpha=0.3)
ax.tick_params(axis="x", rotation=10)

plt.suptitle("Time to First Wager (TTFW): Cat de Repede Mizeaza Utilizatorii?\n"
             "Dark vs Ethical  |  Pariere sportiva SAU cazino",
             fontsize=13, fontweight="bold")
plt.tight_layout()
plt.savefig(OUT_C / "ttfb_analysis.png", dpi=300)
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'ttfb_per_session.csv'}")
print(f"  {OUTPUT_TABLES / 'ttfb_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'ttfb_statistical_tests.csv'}")
print(f"  {OUTPUT_TABLES / 'crossover_ttfb.csv'}")
print(f"  {OUT_C / 'ttfb_analysis.png'}")
