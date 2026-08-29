# -*- coding: utf-8 -*-
"""Week 1 — descriptive overview: activitate per user, sesiuni, heterogeneitate, compozitie evenimente."""

from pathlib import Path
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from matplotlib.gridspec import GridSpec
from scipy.stats import mannwhitneyu



DATA_PATH     = Path("../data/InteractionLogs_Week1.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week1")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

COLORS       = {"Dark": "#d62728", "Ethical": "#2ca02c"}
VARIANT_ORDER = ["Dark", "Ethical"]


def cliffs_delta(a, b):
    """Cliff's delta (pozitiv => a > b)."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    dom = (
        sum(1 for x in a for y in b if x > y)
        - sum(1 for x in a for y in b if x < y)
    )
    return dom / (len(a) * len(b))

def mw_test(a, b):
    stat, p = mannwhitneyu(a, b, alternative="two-sided")
    sig = "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
    return p, sig

def coefficient_of_variation(s):
    m = s.mean()
    return s.std(ddof=1) / m if m else np.nan

def categorize_event(ev):
    if ev in {"BetPlaced", "BetAttempted", "StakeChanged", "OutcomeSelected",
              "BetConfirmed", "BetSlipOpened", "BetSlipCleared", "BetCancelled"}:
        return "Pariere sportiva"
    if ev in {"CasinoSpinClicked", "CasinoRoundCompleted", "CasinoPageViewed",
              "CasinoGameOpened", "CasinoStakeChanged", "RouletteSpinPlaced",
              "BlackjackStart", "WheelSpin", "NearMissShown"}:
        return "Cazino"
    if ev in {"BetConfirmationViewed", "BetConfirmationCancelled",
              "BetConfirmationConfirmed", "ProfileEdited", "LimitSet",
              "LimitChanged", "SessionEndedEarly"}:
        return "Instrumente protective"
    if ev in {"DepositStarted", "DepositCompleted", "CashOut", "WalletViewed"}:
        return "Financiar"
    if ev in {"HomeViewed", "PageViewed", "OddsViewed", "EventViewed",
              "PromotionsViewed", "ProfileViewed", "ActivityPageViewed",
              "MyBetsPageViewed", "BonusClaimed"}:
        return "Navigare"
    if ev in {"LoginSuccess", "RegisterSuccess", "Logout"}:
        return "Autentificare"
    return "Altele"

def style_ax(ax):
    ax.grid(axis="y", alpha=0.22, linestyle="--")
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

def boxplot_variant(ax, data_dark, data_eth, ylabel, title):
    """
    Boxplot colorat (Dark=rosu, Ethical=verde) cu mediane annotate
    si test Mann-Whitney + Cliff's delta in xlabel.
    """
    bp = ax.boxplot(
        [data_dark, data_eth],
        tick_labels=["Dark", "Ethical"],
        patch_artist=True,
        showmeans=False,
        showfliers=True,
        flierprops=dict(marker="o", markersize=4, alpha=0.35, linestyle="none"),
        medianprops=dict(color="black", linewidth=2.2),
        widths=0.52,
    )
    for patch, v in zip(bp["boxes"], ["Dark", "Ethical"]):
        patch.set_facecolor(COLORS[v])
        patch.set_alpha(0.72)

    for i, d in enumerate([data_dark, data_eth]):
        med = np.median(d)
        ax.text(
            i + 1.28, med, f"{med:.1f}",
            va="center", ha="left", fontsize=9, fontweight="bold",
            color=COLORS[["Dark","Ethical"][i]]
        )

    p, sig   = mw_test(data_dark, data_eth)
    cd       = cliffs_delta(data_dark, data_eth)
    cd_label = ("|δ|=1.00 sep. completa" if abs(cd) == 1.0
                else f"Cliff's δ={cd:.2f}")
    ax.set_xlabel(f"Mann-Whitney  p={p:.4f} {sig}   {cd_label}", fontsize=8)
    ax.set_ylabel(ylabel, fontsize=9)
    ax.set_title(title, fontsize=10, fontweight="bold")
    style_ax(ax)


df = pd.read_csv(DATA_PATH)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], errors="coerce", utc=True)
df = df.dropna(subset=["Timestamp"]).copy()
df["EventCategory"] = df["EventType"].apply(categorize_event)

variants = [v for v in VARIANT_ORDER if v in df["Variant"].unique()]

print("=" * 70)
print("WEEK 1 — DESCRIPTIVE OVERVIEW")
print("=" * 70)
print(f"Events: {len(df):,}  |  Users: {df['UserId'].nunique()}"
      f"  |  Sessions: {df['SessionId'].nunique()}")


kpis = (
    df.groupby("Variant")
    .agg(Users=("UserId","nunique"),
         Sessions=("SessionId","nunique"),
         Events=("EventType","size"))
    .reindex(variants)
)
kpis["SessionsPerUser"]  = kpis["Sessions"] / kpis["Users"]
kpis["EventsPerUser"]    = kpis["Events"]   / kpis["Users"]
kpis["EventsPerSession"] = kpis["Events"]   / kpis["Sessions"]
kpis = kpis.round(2)
kpis.to_csv(OUTPUT_TABLES / "week1_kpis.csv")
print("\nKPIs:\n", kpis)

user_act = (
    df.groupby(["Variant","UserId"])
    .agg(Sessions=("SessionId","nunique"),
         Events=("EventType","size"),
         First=("Timestamp","min"),
         Last=("Timestamp","max"))
    .reset_index()
)
user_act["SpanMin"] = (
    (user_act["Last"] - user_act["First"]).dt.total_seconds() / 60
)
user_act.to_csv(OUTPUT_TABLES / "week1_user_activity_distribution.csv", index=False)

print("\nUser summary:")
print(
    user_act.groupby("Variant")
    .agg(n=("UserId","nunique"),
         med_sess=("Sessions","median"), mean_sess=("Sessions","mean"),
         med_ev=("Events","median"),    mean_ev=("Events","mean"))
    .round(2)
)

sess_act = (
    df.groupby(["Variant","SessionId"])
    .agg(Events=("EventType","size"),
         Start=("Timestamp","min"),
         End=("Timestamp","max"))
    .reset_index()
)
sess_act["DurMin"] = (
    (sess_act["End"] - sess_act["Start"]).dt.total_seconds() / 60
).clip(lower=0)
sess_act.to_csv(OUTPUT_TABLES / "week1_session_distribution.csv", index=False)

cat_piv = (
    df.groupby(["Variant","EventCategory"])
    .size().reset_index(name="Count")
    .pivot(index="EventCategory", columns="Variant", values="Count")
    .fillna(0).astype(int)
)
for v in variants:
    cat_piv[f"{v}_pct"] = cat_piv[v] / cat_piv[v].sum() * 100
cat_piv.to_csv(OUTPUT_TABLES / "week1_event_categories.csv")


fig = plt.figure(figsize=(16, 11))
gs  = GridSpec(2, 3, figure=fig, hspace=0.52, wspace=0.40)

ax1 = fig.add_subplot(gs[0, 0])
ax2 = fig.add_subplot(gs[0, 1])
ax3 = fig.add_subplot(gs[0, 2])
ax4 = fig.add_subplot(gs[1, 0:2])   # heterogeneitate — lat
ax5 = fig.add_subplot(gs[1, 2])     # categorii

dark_u = user_act[user_act["Variant"] == "Dark"]
eth_u  = user_act[user_act["Variant"] == "Ethical"]
dark_s = sess_act[sess_act["Variant"] == "Dark"]
eth_s  = sess_act[sess_act["Variant"] == "Ethical"]

boxplot_variant(
    ax1,
    dark_u["Events"].values, eth_u["Events"].values,
    "Numar evenimente",
    "Activitate totala\n(evenimente per utilizator)"
)

boxplot_variant(
    ax2,
    dark_u["Sessions"].values, eth_u["Sessions"].values,
    "Numar sesiuni",
    "Sesiuni per utilizator"
)

boxplot_variant(
    ax3,
    dark_s["Events"].values, eth_s["Events"].values,
    "Numar evenimente",
    "Intensitate sesiune\n(evenimente per sesiune)"
)

for variant in variants:
    sub = (
        user_act[user_act["Variant"] == variant]
        .sort_values("Events")
        .reset_index(drop=True)
    )
    ax4.scatter(
        np.arange(1, len(sub) + 1), sub["Events"],
        color=COLORS[variant], label=variant,
        s=65, alpha=0.82, zorder=3
    )
    ax4.plot(
        np.arange(1, len(sub) + 1), sub["Events"],
        color=COLORS[variant], linewidth=1.6, alpha=0.50
    )

for variant in variants:
    med = user_act[user_act["Variant"]==variant]["Events"].median()
    n   = (user_act["Variant"]==variant).sum()
    ax4.axhline(med, color=COLORS[variant], linewidth=1.4,
                linestyle="--", alpha=0.70,
                label=f"Mediana {variant}: {med:.0f}")

ax4.set_yscale("log")
ax4.set_xlabel("Utilizatori sortati dupa activitate (1=cel mai putin activ)",
               fontsize=9)
ax4.set_ylabel("Numar total evenimente (scala log)", fontsize=9)
ax4.set_title(
    "Distributia activitatii per utilizator\n"
    "(fiecare punct = un utilizator; liniile punctate = mediane)",
    fontsize=10, fontweight="bold"
)
ax4.legend(fontsize=8, ncol=2)
ax4.grid(alpha=0.22, linestyle="--")
ax4.spines["top"].set_visible(False)
ax4.spines["right"].set_visible(False)

relevant = ["Pariere sportiva", "Cazino", "Instrumente protective", "Financiar"]
cat_cols = [f"{v}_pct" for v in variants]
cat_plot = cat_piv[cat_cols].copy()
cat_plot.columns = variants
cat_plot = cat_plot.reindex([c for c in relevant if c in cat_plot.index])
cat_plot = cat_plot.sort_values("Dark", ascending=True)

cat_plot.plot(
    kind="barh", ax=ax5,
    color=[COLORS[v] for v in variants],
    alpha=0.82, edgecolor="white", width=0.65
)
for bar in ax5.patches:
    w = bar.get_width()
    if w > 1.5:
        ax5.text(w + 0.4, bar.get_y() + bar.get_height() / 2,
                 f"{w:.1f}%", va="center", fontsize=8)

ax5.set_xlabel("% din totalul evenimentelor variantei", fontsize=9)
ax5.set_ylabel("")
ax5.set_title(
    "Compozitia evenimentelor\n(categorii relevante)",
    fontsize=10, fontweight="bold"
)
ax5.legend(
    handles=[mpatches.Patch(color=COLORS[v], label=v) for v in variants],
    fontsize=8
)
ax5.grid(axis="x", alpha=0.22, linestyle="--")
ax5.spines["top"].set_visible(False)
ax5.spines["right"].set_visible(False)

med_dark = dark_u["Events"].median()
med_eth  = eth_u["Events"].median()
ratio    = med_dark / med_eth if med_eth > 0 else 0

plt.suptitle(
    f"Week 1 — Volumul activitatii de joc: Dark vs Ethical\n"
    f"Mediana evenimente/user: Dark={med_dark:.0f}  Ethical={med_eth:.0f}"
    f"  (x{ratio:.1f})",
    fontsize=13, fontweight="bold"
)

plt.savefig(
    OUTPUT_CHARTS / "week1_overview.png",
    dpi=300, bbox_inches="tight"
)
plt.close()

print(f"\nSaved: {OUTPUT_CHARTS / 'week1_overview.png'}")
print(f"       {OUTPUT_TABLES / 'week1_kpis.csv'}")
