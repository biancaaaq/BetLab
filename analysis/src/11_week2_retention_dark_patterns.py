# -*- coding: utf-8 -*-
"""Retentia W2 + efectele dark patterns (NearMiss). Doua grafice separate: week2_retention.png, week2_dark_patterns.png."""

import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.ticker as mticker
import numpy as np
from pathlib import Path
from scipy.stats import mannwhitneyu

DATA_PATH     = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week2")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

df = pd.read_csv(DATA_PATH)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)
df["Date"]      = df["Timestamp"].dt.date
df = df.sort_values(["SessionId", "Timestamp"]).reset_index(drop=True)

VARIANTS = ["Dark", "Ethical"]
COLORS   = {"Dark": "#d62728", "Ethical": "#2ca02c"}

retention = (
    df.groupby(["Variant", "UserId"])
    .agg(SessionCount=("SessionId", "nunique"),
         ActiveDays=("Date",      "nunique"))
    .reset_index()
)
retention["SessionsPerDay"] = (
    retention["SessionCount"] / retention["ActiveDays"]
).round(2)
retention.to_csv(OUTPUT_TABLES / "week2_retention_per_user.csv", index=False)

print("=" * 70)
print("WEEK 2 — RETENTIE")
print("=" * 70)
for metric in ["SessionCount", "ActiveDays", "SessionsPerDay"]:
    dark_r    = retention[retention["Variant"] == "Dark"][metric]
    ethical_r = retention[retention["Variant"] == "Ethical"][metric]
    stat, p   = mannwhitneyu(dark_r, ethical_r, alternative="two-sided")
    sig       = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
    print(f"\n  {metric}")
    print(f"    Dark    median={dark_r.median():.1f}  mean={dark_r.mean():.2f}")
    print(f"    Ethical median={ethical_r.median():.1f}  mean={ethical_r.mean():.2f}")
    print(f"    U={stat:.0f}  p={p:.6f}  {sig}")

daily_sessions = (
    df.groupby(["Variant", "Date"])["SessionId"]
    .nunique().reset_index(name="Sessions")
)
daily_sessions["Date"] = pd.to_datetime(daily_sessions["Date"])

daily_users = (
    df.groupby(["Variant", "Date"])["UserId"]
    .nunique().reset_index(name="ActiveUsers")
)
daily_users["Date"] = pd.to_datetime(daily_users["Date"])

fig, axes = plt.subplots(2, 3, figsize=(18, 10))

# --- Rand 1: boxplots ---
for ax, (metric, label) in zip(
    axes[0],
    [("SessionCount",  "Sesiuni per user"),
     ("ActiveDays",    "Zile active per user"),
     ("SessionsPerDay","Sesiuni per zi activa")]
):
    data = [retention[retention["Variant"] == v][metric] for v in VARIANTS]
    bp   = ax.boxplot(data, tick_labels=VARIANTS, patch_artist=True)
    for patch, v in zip(bp["boxes"], VARIANTS):
        patch.set_facecolor(COLORS[v]); patch.set_alpha(0.7)
    for i, (d, v) in enumerate(zip(data, VARIANTS), start=1):
        med = d.median()
        ax.text(i, med + 0.05, f"med={med:.1f}",
                ha="center", va="bottom", fontsize=8,
                color=COLORS[v], fontweight="bold")
    dark_v, eth_v = (retention[retention["Variant"] == v][metric] for v in VARIANTS)
    _, p = mannwhitneyu(dark_v, eth_v, alternative="two-sided")
    sig  = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
    ax.set_title(label, fontsize=11)
    ax.set_ylabel(label)
    ax.set_xlabel(f"p={p:.4f}  {sig}", fontsize=8)
    ax.grid(axis="y", alpha=0.3)

# --- [1,0]: sesiuni zilnice ---
ax = axes[1, 0]
for variant in VARIANTS:
    vdf = daily_sessions[daily_sessions["Variant"] == variant].sort_values("Date")
    ax.plot(vdf["Date"], vdf["Sessions"],
            marker="o", linewidth=2, markersize=5,
            label=variant, color=COLORS[variant])
    ax.fill_between(vdf["Date"], vdf["Sessions"], alpha=0.1, color=COLORS[variant])
ax.set_title("Sesiuni per zi — W2", fontsize=11)
ax.set_ylabel("Sesiuni"); ax.legend(); ax.grid(alpha=0.3)
ax.xaxis.set_major_formatter(plt.matplotlib.dates.DateFormatter("%d %b"))
ax.tick_params(axis="x", rotation=20)

# --- [1,1]: utilizatori activi / zi ---
ax = axes[1, 1]
for variant in VARIANTS:
    vdf = daily_users[daily_users["Variant"] == variant].sort_values("Date")
    ax.plot(vdf["Date"], vdf["ActiveUsers"],
            marker="o", linewidth=2, markersize=5,
            label=variant, color=COLORS[variant])
    ax.fill_between(vdf["Date"], vdf["ActiveUsers"], alpha=0.1, color=COLORS[variant])
ax.set_title("Utilizatori activi per zi — W2", fontsize=11)
ax.set_ylabel("Utilizatori activi"); ax.legend(); ax.grid(alpha=0.3)
ax.xaxis.set_major_formatter(plt.matplotlib.dates.DateFormatter("%d %b"))
ax.tick_params(axis="x", rotation=20)

# --- [1,2]: histogram zile active per user ---
ax = axes[1, 2]
for variant in VARIANTS:
    vals = retention[retention["Variant"] == variant]["ActiveDays"]
    ax.hist(vals, bins=range(1, int(vals.max()) + 2),
            alpha=0.65, label=variant,
            color=COLORS[variant], edgecolor="white", align="left")
ax.set_title("Distributie zile active per user — W2", fontsize=11)
ax.set_xlabel("Zile active"); ax.set_ylabel("Numar utilizatori")
ax.xaxis.set_major_locator(mticker.MaxNLocator(integer=True))
ax.legend(); ax.grid(axis="y", alpha=0.3)

plt.suptitle("Week 2 — Retentie utilizatori: Dark vs Ethical",
             fontsize=14, fontweight="bold")
plt.tight_layout()
plt.savefig(OUTPUT_CHARTS / "week2_retention.png", dpi=300)
plt.close()

dark_df = df[df["Variant"] == "Dark"].copy()

nm_stats = []
for sid, sdf in dark_df.groupby("SessionId"):
    sdf     = sdf.reset_index(drop=True)
    nm_idx  = sdf.index[sdf["EventType"] == "NearMissShown"].tolist()
    spins   = (sdf["EventType"] == "CasinoSpinClicked").sum()
    spins_after = (
        sdf.loc[nm_idx[0] + 1:, "EventType"] == "CasinoSpinClicked"
    ).sum() if nm_idx else 0
    nm_stats.append({
        "SessionId"    : sid,
        "HadNearMiss"  : bool(nm_idx),
        "NearMissCount": len(nm_idx),
        "TotalSpins"   : spins,
        "SpinsAfterNM" : spins_after,
    })

nm_df      = pd.DataFrame(nm_stats)
with_nm    = nm_df[nm_df["HadNearMiss"]]
without_nm = nm_df[~nm_df["HadNearMiss"]]
cont_rate  = (
    (with_nm["SpinsAfterNM"] >= 1).sum() / len(with_nm) * 100
    if len(with_nm) > 0 else 0
)

stat_nm, p_nm = (
    mannwhitneyu(with_nm["TotalSpins"], without_nm["TotalSpins"],
                 alternative="two-sided")
    if len(without_nm) > 0 else (0, 1)
)
nm_df.to_csv(OUTPUT_TABLES / "week2_nearmiss_effect.csv", index=False)

print(f"\n\n=== NEARMISS EFFECT (Dark W2) ===")
print(f"  Sesiuni cu NearMiss: {len(with_nm)}  fara: {len(without_nm)}")
print(f"  Spins medie cu NM:   {with_nm['TotalSpins'].mean():.2f}  "
      f"fara NM: {without_nm['TotalSpins'].mean():.2f}")
print(f"  Continua dupa NM:    {cont_rate:.1f}%")
print(f"  Mann-Whitney:        U={stat_nm:.0f}  p={p_nm:.6f}")

# Dark pattern per user
dp_user = dark_df.groupby("UserId").agg(
    NearMissCount     = ("EventType", lambda x: (x == "NearMissShown").sum()),
    OutcomeRemovedCnt = ("EventType", lambda x: (x == "OutcomeRemoved").sum()),
    CasinoSpins       = ("EventType", lambda x: (x == "CasinoSpinClicked").sum()),
    BetPlaced         = ("EventType", lambda x: (x == "BetPlaced").sum()),
).reset_index()
dp_user.to_csv(OUTPUT_TABLES / "week2_dark_pattern_per_user.csv", index=False)
corr = dp_user[["NearMissCount", "CasinoSpins"]].corr().iloc[0, 1]
print(f"  Corelatie NearMiss vs CasinoSpins: r={corr:.4f}")

fig, axes = plt.subplots(2, 2, figsize=(13, 10))

# --- [0,0] Boxplot spins cu NM vs fara NM ---
ax = axes[0, 0]
ax.boxplot(
    [with_nm["TotalSpins"], without_nm["TotalSpins"]],
    tick_labels=["Cu NearMiss", "Fara NearMiss"],
    patch_artist=True,
    boxprops=dict(facecolor="#ff7f0e", alpha=0.7)
)
sig = "***" if p_nm < 0.001 else "**" if p_nm < 0.01 else "*" if p_nm < 0.05 else "n.s."
ax.set_title("Casino Spins per sesiune\n(Dark W2: cu vs fara NearMiss)",
             fontsize=10, fontweight="bold")
ax.set_ylabel("Numar spins")
ax.set_xlabel(f"p={p_nm:.4f}  {sig}", fontsize=9)
ax.grid(axis="y", alpha=0.3)

# --- [0,1] Histogram spins dupa NearMiss ---
ax = axes[0, 1]
if len(with_nm) > 0 and with_nm["SpinsAfterNM"].max() > 0:
    ax.hist(with_nm["SpinsAfterNM"],
            bins=range(0, int(with_nm["SpinsAfterNM"].max()) + 2),
            color="#ff7f0e", alpha=0.8, edgecolor="white", align="left")
    ax.axvline(with_nm["SpinsAfterNM"].mean(), color="black",
               linestyle="--", linewidth=1.5,
               label=f"medie={with_nm['SpinsAfterNM'].mean():.1f}")
    ax.legend(fontsize=9)
ax.set_title(f"Spins dupa primul NearMiss\n({cont_rate:.0f}% sesiuni continua) — Dark W2",
             fontsize=10, fontweight="bold")
ax.set_xlabel("Numar spins dupa NearMiss"); ax.set_ylabel("Numar sesiuni")
ax.grid(axis="y", alpha=0.3)

# --- [1,0] Scatter NearMiss → CasinoSpins per user ---
ax = axes[1, 0]
ax.scatter(dp_user["NearMissCount"], dp_user["CasinoSpins"],
           color="#ff7f0e", alpha=0.75, s=70, edgecolors="white")
if len(dp_user) > 1:
    z      = np.polyfit(dp_user["NearMissCount"], dp_user["CasinoSpins"], 1)
    x_line = np.linspace(dp_user["NearMissCount"].min(),
                         dp_user["NearMissCount"].max(), 50)
    ax.plot(x_line, np.poly1d(z)(x_line), "k--", linewidth=1.5,
            label=f"r={corr:.2f}")
ax.set_title("Corelatie NearMiss → CasinoSpins\n(per user, Dark W2)",
             fontsize=10, fontweight="bold")
ax.set_xlabel("NearMiss per utilizator"); ax.set_ylabel("Casino Spins per utilizator")
ax.legend(fontsize=9); ax.grid(alpha=0.3)

# --- [1,1] Histogram NearMiss count per user ---
ax = axes[1, 1]
nm_counts = dp_user["NearMissCount"]
if nm_counts.max() > 0:
    ax.hist(nm_counts, bins=range(0, int(nm_counts.max()) + 2),
            color="#d62728", alpha=0.8, edgecolor="white", align="left")
    ax.axvline(nm_counts.median(), color="black", linestyle="--", linewidth=1.5,
               label=f"mediana={nm_counts.median():.0f}")
    ax.legend(fontsize=9)
ax.set_title("Distributia expunerii la NearMiss\n(per user, Dark W2)",
             fontsize=10, fontweight="bold")
ax.set_xlabel("NearMiss events per utilizator"); ax.set_ylabel("Numar utilizatori")
ax.grid(axis="y", alpha=0.3)

plt.suptitle("Week 2 — Efectul NearMiss (Dark variant)",
             fontsize=13, fontweight="bold")
plt.tight_layout()
plt.savefig(OUTPUT_CHARTS / "week2_dark_patterns.png", dpi=300)
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week2_retention_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'week2_nearmiss_effect.csv'}")
print(f"  {OUTPUT_TABLES / 'week2_dark_pattern_per_user.csv'}")
print(f"  {OUTPUT_CHARTS / 'week2_retention.png'}")
print(f"  {OUTPUT_CHARTS / 'week2_dark_patterns.png'}")
