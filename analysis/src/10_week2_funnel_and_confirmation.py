# -*- coding: utf-8 -*-
"""Funnel OutcomeSelected → BetAttempted → BetPlaced (W2). Efectul confirmarii Ethical."""

import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
from pathlib import Path

DATA_PATH     = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week2")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

df = pd.read_csv(DATA_PATH)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)
df = df.sort_values(["SessionId","Timestamp"]).reset_index(drop=True)

VARIANTS     = ["Dark","Ethical"]
COLORS       = {"Dark":"#d62728","Ethical":"#2ca02c"}
FUNNEL_STEPS = ["OutcomeSelected","StakeChanged","BetAttempted","BetPlaced"]

funnel_counts = {}
total_sessions = {}
for variant in VARIANTS:
    vdf  = df[df["Variant"]==variant]
    total_sessions[variant] = vdf["SessionId"].nunique()
    remaining = set(vdf["SessionId"].unique())
    counts = []
    for step in FUNNEL_STEPS:
        remaining &= set(vdf[vdf["EventType"]==step]["SessionId"])
        counts.append(len(remaining))
    funnel_counts[variant] = counts

rows = []
for variant in VARIANTS:
    counts = funnel_counts[variant]
    top    = counts[0] or 1
    for i, (step, cnt) in enumerate(zip(FUNNEL_STEPS, counts)):
        rows.append({"Variant":variant,"Step":step,"Sessions":cnt,
                     "Conv_top_%":round(cnt/top*100,1),
                     "Conv_prev_%":round(cnt/counts[i-1]*100,1) if i>0 and counts[i-1]>0 else 100.0})

funnel_df = pd.DataFrame(rows)
funnel_df.to_csv(OUTPUT_TABLES / "week2_funnel_analysis.csv", index=False)

print("=" * 70)
print("WEEK 2 — FUNNEL + BETCONFIRMATION")
print("=" * 70)
print(f"\nSesiuni totale — Dark: {total_sessions['Dark']}  Ethical: {total_sessions['Ethical']}")
print(f"\n{funnel_df.to_string(index=False)}")
for v in VARIANTS:
    top, bot = funnel_counts[v][0], funnel_counts[v][-1]
    print(f"  [{v}] OutcomeSelected->BetPlaced: {bot}/{top} = {bot/top*100:.1f}%" if top else "")

eth = df[df["Variant"]=="Ethical"]
drk = df[df["Variant"]=="Dark"]

n_viewed    = (eth["EventType"]=="BetConfirmationViewed").sum()
n_cancelled = (eth["EventType"]=="BetConfirmationCancelled").sum()
n_attempted_e = (eth["EventType"]=="BetAttempted").sum()
n_placed_e  = (eth["EventType"]=="BetPlaced").sum()
n_attempted_d = (drk["EventType"]=="BetAttempted").sum()
n_placed_d  = (drk["EventType"]=="BetPlaced").sum()

protection_rate = n_cancelled/n_viewed*100 if n_viewed>0 else 0

print(f"\n--- BETCONFIRMATION ---")
print(f"  Ethical BetAttempted      : {n_attempted_e}  BetConfirmationViewed: {n_viewed}")
print(f"  Ethical BetConfirmCancelled: {n_cancelled}  BetPlaced: {n_placed_e}")
print(f"  Protection rate            : {protection_rate:.1f}%")
print(f"  Dark BetAttempted          : {n_attempted_d}  BetPlaced: {n_placed_d} (abandon: 0%)")

conf_summary = pd.DataFrame([
    {"Metric":"BetAttempted","Dark":n_attempted_d,"Ethical":n_attempted_e},
    {"Metric":"ConfirmViewed","Dark":0,"Ethical":n_viewed},
    {"Metric":"ConfirmCancelled","Dark":0,"Ethical":n_cancelled},
    {"Metric":"BetPlaced","Dark":n_placed_d,"Ethical":n_placed_e},
    {"Metric":"ProtectionRate_%","Dark":0,"Ethical":round(protection_rate,1)},
])
conf_summary.to_csv(OUTPUT_TABLES / "week2_betconfirmation_summary.csv", index=False)

# per user Ethical
users_eth = eth["UserId"].unique()
user_rows = []
for uid in users_eth:
    udf = eth[eth["UserId"]==uid]
    user_rows.append({"UserId":uid,
        "BetAttempted":(udf["EventType"]=="BetAttempted").sum(),
        "ConfirmViewed":(udf["EventType"]=="BetConfirmationViewed").sum(),
        "ConfirmCancelled":(udf["EventType"]=="BetConfirmationCancelled").sum(),
        "BetPlaced":(udf["EventType"]=="BetPlaced").sum()})
user_conf_df = pd.DataFrame(user_rows)
users_cancelled = (user_conf_df["ConfirmCancelled"]>0).sum()
print(f"  Utilizatori Ethical cu cel putin o anulare: {users_cancelled}/{len(users_eth)}")

fig, axes = plt.subplots(1, 3, figsize=(17, 6))

# Funnel pâlnie Ethical
ax0 = axes[0]
top_e = funnel_counts["Ethical"][0] or 1
pcts_e = [c/top_e*100 for c in funnel_counts["Ethical"]]
y_pos  = np.arange(len(FUNNEL_STEPS))
bars = ax0.barh(y_pos, pcts_e, color=COLORS["Ethical"], alpha=0.8, height=0.55)
for bar, cnt, pct in zip(bars, funnel_counts["Ethical"], pcts_e):
    ax0.text(pct/2, bar.get_y()+bar.get_height()/2,
             f"{cnt} ({pct:.0f}%)", va="center", ha="center",
             fontsize=9, color="white", fontweight="bold")
ax0.set_yticks(y_pos); ax0.set_yticklabels(FUNNEL_STEPS)
ax0.invert_yaxis(); ax0.set_xlim(0,110)
ax0.set_title("Funnel Ethical — W2", fontsize=11, color=COLORS["Ethical"], fontweight="bold")
ax0.set_xlabel("% fata de OutcomeSelected"); ax0.grid(axis="x", alpha=0.3)

# Funnel pâlnie Dark
ax1 = axes[1]
top_d = funnel_counts["Dark"][0] or 1
pcts_d = [c/top_d*100 for c in funnel_counts["Dark"]]
bars = ax1.barh(y_pos, pcts_d, color=COLORS["Dark"], alpha=0.8, height=0.55)
for bar, cnt, pct in zip(bars, funnel_counts["Dark"], pcts_d):
    ax1.text(pct/2, bar.get_y()+bar.get_height()/2,
             f"{cnt} ({pct:.0f}%)", va="center", ha="center",
             fontsize=9, color="white", fontweight="bold")
ax1.set_yticks(y_pos); ax1.set_yticklabels(FUNNEL_STEPS)
ax1.invert_yaxis(); ax1.set_xlim(0,110)
ax1.set_title("Funnel Dark — W2", fontsize=11, color=COLORS["Dark"], fontweight="bold")
ax1.set_xlabel("% fata de OutcomeSelected"); ax1.grid(axis="x", alpha=0.3)

# BetConfirmation flow
ax2 = axes[2]
steps  = ["BetAttempted","Conf.Viewed","Conf.Cancelled","BetPlaced"]
values = [n_attempted_e, n_viewed, n_cancelled, n_placed_e]
colors_bar = ["#aec7e8","#17becf","#e377c2","#2ca02c"]
bars = ax2.bar(steps, values, color=colors_bar, alpha=0.85, edgecolor="white")
for bar, val in zip(bars, values):
    ax2.text(bar.get_x()+bar.get_width()/2, bar.get_height()+0.3,
             str(val), ha="center", va="bottom", fontsize=11, fontweight="bold")
ax2.set_title(f"Ethical BetConfirmation — W2\nProtection rate: {protection_rate:.1f}%",
              fontsize=10, fontweight="bold")
ax2.set_ylabel("Numar evenimente")
ax2.set_ylim(0, max(values)*1.25)
ax2.grid(axis="y", alpha=0.3)
ax2.tick_params(axis="x", rotation=10)

plt.suptitle("Week 2 — Funnel Conversie + Efectul Confirmarii Etice",
             fontsize=13, fontweight="bold")
plt.tight_layout()
plt.savefig(OUTPUT_CHARTS / "week2_funnel_and_confirmation.png", dpi=300)
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week2_funnel_analysis.csv'}")
print(f"  {OUTPUT_TABLES / 'week2_betconfirmation_summary.csv'}")
print(f"  {OUTPUT_CHARTS / 'week2_funnel_and_confirmation.png'}")
