# -*- coding: utf-8 -*-
"""Efectul BetConfirmation (Ethical only): rata de anulare per user, adoptie, corelatie Spearman rata_anulare vs BetPlaced."""

import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
import numpy as np
from pathlib import Path
from scipy.stats import spearmanr, fisher_exact

DATA_PATH     = Path("../data/InteractionLogs_Week1.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week1")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

COLORS = {"Dark": "#d62728", "Ethical": "#2ca02c",
          "Viewed": "#17becf", "Cancelled": "#e377c2"}

df = pd.read_csv(DATA_PATH)

def style_ax(ax, axis="y"):
    ax.grid(axis=axis, alpha=0.22, linestyle="--")
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

def pval_label(p):
    return "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."

eth = df[df["Variant"] == "Ethical"]
drk = df[df["Variant"] == "Dark"]

def ev_cnt(frame, event): return len(frame[frame["EventType"] == event])

n_attempted_e  = ev_cnt(eth, "BetAttempted")
n_viewed       = ev_cnt(eth, "BetConfirmationViewed")
n_cancelled    = ev_cnt(eth, "BetConfirmationCancelled")
n_placed_e     = ev_cnt(eth, "BetPlaced")
n_attempted_d  = ev_cnt(drk, "BetAttempted")
n_placed_d     = ev_cnt(drk, "BetPlaced")

protection_rate = n_cancelled / n_viewed * 100 if n_viewed > 0 else 0
abandon_eth     = (n_attempted_e - n_placed_e) / n_attempted_e * 100 if n_attempted_e > 0 else 0
abandon_drk     = (n_attempted_d - n_placed_d) / n_attempted_d * 100 if n_attempted_d > 0 else 0

# Fisher test: BetAttempted → BetPlaced (Dark vs Ethical)
d_not_placed = n_attempted_d - n_placed_d
e_not_placed = n_attempted_e - n_placed_e
_, p_fisher = fisher_exact([[n_placed_d, d_not_placed], [n_placed_e, e_not_placed]])

print("=" * 70)
print("WEEK 1 — EFECTUL CONFIRMARII (ETHICAL)")
print("=" * 70)
print(f"\n--- Ethical ---")
print(f"  BetAttempted:              {n_attempted_e}")
print(f"  BetConfirmationViewed:     {n_viewed}  ({100*n_viewed/n_attempted_e:.1f}% din BetAttempted)")
print(f"  BetConfirmationCancelled:  {n_cancelled}  ({100*n_cancelled/n_viewed:.1f}% din Viewed)")
print(f"  BetPlaced:                 {n_placed_e}  ({100*n_placed_e/n_attempted_e:.1f}% din BetAttempted)")
print(f"\n--- Dark ---")
print(f"  BetAttempted: {n_attempted_d}  BetPlaced: {n_placed_d}  (abandon: {abandon_drk:.1f}%)")
print(f"\nFisher BetAttempted→BetPlaced (Dark vs Ethical): p={p_fisher:.4f} {pval_label(p_fisher)}")

users_eth = eth["UserId"].unique()
user_rows = []
for uid in users_eth:
    udf = eth[eth["UserId"] == uid]
    att = ev_cnt(udf, "BetAttempted")
    canc = ev_cnt(udf, "BetConfirmationCancelled")
    view = ev_cnt(udf, "BetConfirmationViewed")
    placed = ev_cnt(udf, "BetPlaced")
    cancel_rate_attempted = canc / att * 100 if att > 0 else 0.0
    cancel_rate_viewed    = canc / view * 100 if view > 0 else 0.0
    user_rows.append({
        "UserId": uid,
        "BetAttempted": att,
        "ConfirmationViewed": view,
        "ConfirmationCancelled": canc,
        "BetPlaced": placed,
        "CancelRate_pct": round(cancel_rate_attempted, 1),
        "CancelRate_ofViewed_pct": round(cancel_rate_viewed, 1),
        "CancelledAtLeastOnce": int(canc > 0),
    })

user_df = pd.DataFrame(user_rows)
user_df.to_csv(OUTPUT_TABLES / "week1_betconfirmation_per_user.csv", index=False)

n_users          = len(users_eth)
users_with_view  = (user_df["ConfirmationViewed"] > 0).sum()
users_with_canc  = user_df["CancelledAtLeastOnce"].sum()

# Spearman: rata anulare (din confirmari VAZUTE) vs BetPlaced
# Metrica mai curata: elimina confundarea cu nivelul general de activitate
users_with_view_df = user_df[user_df["ConfirmationViewed"] > 0]
spear_r, spear_p = spearmanr(
    users_with_view_df["CancelRate_ofViewed_pct"],
    users_with_view_df["BetPlaced"]
)

print(f"\n--- Per utilizator (Ethical, n={n_users}) ---")
print(f"  Au vazut confirmare:        {users_with_view}/{n_users} ({100*users_with_view/n_users:.0f}%)")
print(f"  Au anulat cel putin o data: {users_with_canc}/{n_users} ({100*users_with_canc/n_users:.0f}%)")
print(f"\nSpearman rata_anulare vs BetPlaced: r={spear_r:.3f}  p={spear_p:.4f} {pval_label(spear_p)}")

summary_df = pd.DataFrame([
    {"Metric": "BetAttempted (total)", "Dark": n_attempted_d, "Ethical": n_attempted_e},
    {"Metric": "BetConfirmationViewed", "Dark": 0, "Ethical": n_viewed},
    {"Metric": "BetConfirmationCancelled", "Dark": 0, "Ethical": n_cancelled},
    {"Metric": "BetPlaced (total)", "Dark": n_placed_d, "Ethical": n_placed_e},
    {"Metric": "Abandon rate (%)", "Dark": round(abandon_drk, 1), "Ethical": round(abandon_eth, 1)},
    {"Metric": "Protection rate (Canc/Viewed %)", "Dark": 0, "Ethical": round(protection_rate, 1)},
    {"Metric": "Useri cu confirmare vizualizata", "Dark": 0, "Ethical": users_with_view},
    {"Metric": "Useri cu cel putin o anulare", "Dark": 0, "Ethical": users_with_canc},
])
summary_df.to_csv(OUTPUT_TABLES / "week1_betconfirmation_summary.csv", index=False)
print("\n", summary_df.to_string(index=False))



fig, axes = plt.subplots(2, 3, figsize=(18, 11))
fig.subplots_adjust(hspace=0.50, wspace=0.40)

# [0,0] Fluxul Ethical
ax = axes[0, 0]
steps_labels = ["Pariu\ninitiat", "Confirmare\nvizualizata", "Pariu\nanulat", "Pariu\nplasat"]
flow_vals    = [n_attempted_e, n_viewed, n_cancelled, n_placed_e]
flow_colors  = ["#aec7e8", COLORS["Viewed"], COLORS["Cancelled"], COLORS["Ethical"]]

bars = ax.bar(steps_labels, flow_vals, color=flow_colors, alpha=0.85, edgecolor="white")
for bar, val in zip(bars, flow_vals):
    ax.text(bar.get_x() + bar.get_width() / 2,
            bar.get_height() + 0.5, str(val),
            ha="center", va="bottom", fontsize=11, fontweight="bold")

ax.set_title("Ethical — Fluxul de confirmare\n(evenimente totale)", fontsize=10, fontweight="bold")
ax.set_ylabel("Numar evenimente")
ax.set_ylim(0, max(flow_vals) * 1.28)
ax.set_xlabel(
    f"Confirmare vazuta: {100*n_viewed/n_attempted_e:.0f}% din pariuri\n"
    f"Anulate: {protection_rate:.0f}% din confirmari",
    fontsize=8.5
)
style_ax(ax)

# [0,1] Dark vs Ethical: BetAttempted → BetPlaced
ax = axes[0, 1]
cats    = ["Pariu\ninitiat\n(BetAttempted)", "Pariu\nplasat\n(BetPlaced)",
           "Blocat de\nconfirmare"]
d_vals  = [n_attempted_d, n_placed_d, 0]
e_vals  = [n_attempted_e, n_placed_e, n_cancelled]
x       = np.arange(len(cats))
w       = 0.35

bars_d = ax.bar(x - w / 2, d_vals, w, label="Dark",    color=COLORS["Dark"],    alpha=0.82, edgecolor="white")
bars_e = ax.bar(x + w / 2, e_vals, w, label="Ethical", color=COLORS["Ethical"], alpha=0.82, edgecolor="white")

for bars_set in [bars_d, bars_e]:
    for bar in bars_set:
        h = bar.get_height()
        if h > 0:
            ax.text(bar.get_x() + bar.get_width() / 2,
                    h + 0.5, str(int(h)),
                    ha="center", va="bottom", fontsize=9, fontweight="bold")

ax.set_title("Dark vs Ethical\nBetAttempted → BetPlaced", fontsize=10, fontweight="bold")
ax.set_xticks(x)
ax.set_xticklabels(cats, fontsize=9)
ax.set_ylabel("Numar evenimente")
ax.legend(handles=[mpatches.Patch(color=COLORS[v], label=v) for v in ["Dark","Ethical"]], fontsize=8)
ax.set_xlabel(
    f"Fisher: p={p_fisher:.4f} {pval_label(p_fisher)}\n"
    f"Dark: {100*n_placed_d/n_attempted_d:.0f}% conversie  |  Ethical: {100*n_placed_e/n_attempted_e:.0f}%",
    fontsize=8.5
)
ax.set_ylim(0, max(max(d_vals), max(e_vals)) * 1.30)
style_ax(ax)

# [0,2] Adoptia instrumentelor per user
ax = axes[0, 2]
adopt_labels = [
    f"Confirmare\nvizualizata\n({users_with_view}/{n_users})",
    f"Cel putin\no anulare\n({users_with_canc}/{n_users})",
    f"Nicio anulare\n({n_users-users_with_canc}/{n_users})",
]
adopt_vals   = [
    100 * users_with_view  / n_users,
    100 * users_with_canc  / n_users,
    100 * (n_users - users_with_canc) / n_users,
]
adopt_colors = [COLORS["Viewed"], COLORS["Cancelled"], COLORS["Ethical"]]
bars = ax.bar(adopt_labels, adopt_vals, color=adopt_colors, alpha=0.82, edgecolor="white")
for bar, val in zip(bars, adopt_vals):
    ax.text(bar.get_x() + bar.get_width() / 2,
            bar.get_height() + 0.8, f"{val:.0f}%",
            ha="center", va="bottom", fontsize=11, fontweight="bold")

ax.set_ylim(0, 120)
ax.set_ylabel("% utilizatori Ethical")
ax.set_title(f"Adoptia instrumentelor de protectie\nper utilizator Ethical (n={n_users})",
             fontsize=10, fontweight="bold")
style_ax(ax)

# [1,0] Distributia anularilor per user
ax = axes[1, 0]
cancelled_counts = user_df["ConfirmationCancelled"].value_counts().sort_index()
colors_hist = [COLORS["Cancelled"] if c > 0 else "#aaaaaa" for c in cancelled_counts.index]
bars = ax.bar(cancelled_counts.index.astype(str), cancelled_counts.values,
              color=colors_hist, alpha=0.85, edgecolor="white")
for bar, val in zip(bars, cancelled_counts.values):
    ax.text(bar.get_x() + bar.get_width() / 2,
            bar.get_height() + 0.08, str(val),
            ha="center", va="bottom", fontsize=10, fontweight="bold")

ax.set_title("Distributia anularilor\nper utilizator Ethical", fontsize=10, fontweight="bold")
ax.set_xlabel("Numar de anulari per utilizator")
ax.set_ylabel("Numar utilizatori")
ax.set_ylim(0, max(cancelled_counts.values) * 1.30)
style_ax(ax)

# [1,1] Scatter: rata anulare vs BetPlaced (Spearman)
ax = axes[1, 1]
scatter_df = users_with_view_df.copy()

scatter_df = users_with_view_df.copy()
sc = ax.scatter(scatter_df["CancelRate_ofViewed_pct"], scatter_df["BetPlaced"],
                c=[COLORS["Cancelled"] if c > 0 else "#aaaaaa"
                   for c in scatter_df["ConfirmationCancelled"]],
                s=90, alpha=0.80, edgecolors="white", linewidths=0.8, zorder=3)

if len(scatter_df) > 2:
    z = np.polyfit(scatter_df["CancelRate_ofViewed_pct"], scatter_df["BetPlaced"], 1)
    p_fit = np.poly1d(z)
    x_line = np.linspace(scatter_df["CancelRate_ofViewed_pct"].min(),
                         scatter_df["CancelRate_ofViewed_pct"].max(), 100)
    ax.plot(x_line, p_fit(x_line), "k--", alpha=0.45, linewidth=1.5)

ax.set_title(
    "Corelatie: rata anulare vs pariuri plasate\n(Spearman, Cancelled/Viewed)",
    fontsize=10, fontweight="bold"
)
ax.set_xlabel(
    f"Rata anulare din confirmari vazute (%)\n"
    f"Spearman r={spear_r:.2f}  p={spear_p:.4f}  {pval_label(spear_p)}",
    fontsize=8.5
)

# Legenda culori
h1 = mpatches.Patch(color=COLORS["Cancelled"], label="A anulat cel putin o data")
h2 = mpatches.Patch(color="#aaaaaa", label="Nicio anulare")
ax.legend(handles=[h1, h2], fontsize=7.5)
style_ax(ax)

# [1,2] Rata anulare per user (sorted bar)
ax = axes[1, 2]
sorted_df = user_df.sort_values("CancelRate_ofViewed_pct", ascending=False).reset_index(drop=True)
bar_colors = [COLORS["Cancelled"] if r > 0 else "#cccccc" for r in sorted_df["CancelRate_ofViewed_pct"]]
bars = ax.bar(range(len(sorted_df)), sorted_df["CancelRate_ofViewed_pct"],
              color=bar_colors, alpha=0.82, edgecolor="white")

ax.axhline(sorted_df["CancelRate_ofViewed_pct"].mean(), color="black",
           linewidth=1.5, linestyle="--", alpha=0.6,
           label=f"Medie: {sorted_df['CancelRate_ofViewed_pct'].mean():.1f}%")
ax.set_xticks([])
ax.set_xlabel(f"Utilizatori Ethical (n={n_users}), sortati dupa rata anulare\n(din confirmari vazute)", fontsize=8.5)
ax.set_ylabel("Rata anulare din confirmari vizualizate (%)")
ax.set_title("Rata de anulare per utilizator\n(Cancelled/Viewed, sortata descrescator)", fontsize=10, fontweight="bold")
ax.legend(fontsize=8)
style_ax(ax)

plt.suptitle(
    "Week 1 — Efectul confirmarii pariului: protectie vs impulsivitate\n"
    f"Ethical: {protection_rate:.0f}% din confirmari anulate  |  "
    f"Dark: 0% abandon (fara confirmare)",
    fontsize=13, fontweight="bold"
)

plt.savefig(OUTPUT_CHARTS / "week1_betconfirmation.png", dpi=300, bbox_inches="tight")
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week1_betconfirmation_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'week1_betconfirmation_summary.csv'}")
print(f"  {OUTPUT_CHARTS / 'week1_betconfirmation.png'}")
