# -*- coding: utf-8 -*-
"""Markov transitions + clustering W2. Aceleasi state definitions ca script 07, k=5 Ward."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from pathlib import Path
from collections import defaultdict
from scipy.cluster.hierarchy import linkage, fcluster
from sklearn.preprocessing import StandardScaler
from sklearn.decomposition import PCA
from utils import parse_meta

DATA_PATH     = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week2")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

df = pd.read_csv(DATA_PATH)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)
df["Date"]      = df["Timestamp"].dt.date
df = df.sort_values(["UserId", "Timestamp"]).reset_index(drop=True)

STATES = ["Prudent", "Recreational", "Engaged", "Highly Engaged"]
SHORT  = ["Prudent", "Recr.", "Engaged", "H.Eng."]

COLORS_STATE = {
    "Prudent"        : "#2ca02c",
    "Recreational"   : "#1f77b4",
    "Engaged"        : "#ff7f0e",
    "Highly Engaged" : "#d62728",
}
COLORS_VAR = {"Dark": "#d62728", "Ethical": "#2ca02c"}

spins_all         = df[df["EventType"] == "CasinoSpinClicked"].copy()
spins_all["mode"] = spins_all["MetadataJson"].apply(
    lambda s: parse_meta(s).get("mode", ""))
auto_per_session  = (
    spins_all.groupby("SessionId")["mode"]
    .apply(lambda m: (m == "auto").sum())
    .rename("auto_spins")
)

def classify_session(row):
    # S4: dark pattern dominant in sesiune
    if row["near_miss"] > 2 or row["auto_spins"] > 5:
        return "Highly Engaged"
    # S3: orice contact dark pattern SAU volum mare deliberat
    if row["near_miss"] > 0 or row["auto_spins"] > 0 or row["casino_spins"] > 15:
        return "Engaged"
    # S2: casino moderat
    if row["casino_spins"] > 5:
        return "Recreational"
    # S1: activitate minima
    return "Prudent"

session_feats = (
    df.groupby(["Variant", "UserId", "SessionId"])
    .agg(
        casino_spins = ("EventType", lambda x: (x == "CasinoSpinClicked").sum()),
        near_miss    = ("EventType", lambda x: (x == "NearMissShown").sum()),
        bet_placed   = ("EventType", lambda x: (x == "BetPlaced").sum()),
        first_ts     = ("Timestamp", "min"),
    )
    .reset_index()
)
session_feats = session_feats.merge(auto_per_session, on="SessionId", how="left")
session_feats["auto_spins"] = session_feats["auto_spins"].fillna(0).astype(int)
session_feats["State"]      = session_feats.apply(classify_session, axis=1)
session_feats               = session_feats.sort_values(["UserId", "Variant", "first_ts"])

def build_transition_matrix(df_variant):
    counts = defaultdict(lambda: defaultdict(int))
    for uid, user_df in df_variant.groupby("UserId"):
        states = user_df.sort_values("first_ts")["State"].tolist()
        for a, b in zip(states[:-1], states[1:]):
            counts[a][b] += 1
    mat_raw  = pd.DataFrame(0, index=STATES, columns=STATES)
    for a in counts:
        for b in counts[a]:
            if a in STATES and b in STATES:
                mat_raw.loc[a, b] = counts[a][b]
    row_sums = mat_raw.sum(axis=1)
    mat_prob = mat_raw.div(row_sums, axis=0).fillna(0)
    return mat_raw, mat_prob

dark_sess    = session_feats[session_feats["Variant"] == "Dark"]
ethical_sess = session_feats[session_feats["Variant"] == "Ethical"]

mat_dark_raw, mat_dark    = build_transition_matrix(dark_sess)
mat_eth_raw,  mat_ethical = build_transition_matrix(ethical_sess)

mat_dark.round(4).to_csv(OUTPUT_TABLES / "week2_markov_behavioral_dark.csv")
mat_ethical.round(4).to_csv(OUTPUT_TABLES / "week2_markov_behavioral_ethical.csv")

print("=" * 70)
print("WEEK 2 — MARKOV BEHAVIORAL STATES + CLUSTERING")
print("=" * 70)

state_dist = (
    session_feats.groupby(["Variant", "State"]).size()
    .unstack(fill_value=0)
    .reindex(columns=STATES, fill_value=0)
)
state_dist["Total"] = state_dist.sum(axis=1)
for s in STATES:
    state_dist[f"{s}_%"] = (state_dist[s] / state_dist["Total"] * 100).round(1)

print("\nDistributia starilor comportamentale — W2:")
print(state_dist)
print("\n--- Matrice tranzitii DARK W2 (conturi brute) ---")
print(mat_dark_raw)
print("\n--- Matrice tranzitii DARK W2 (probabilitati) ---")
print(mat_dark.round(3))
print("\n--- Matrice tranzitii ETHICAL W2 (probabilitati) ---")
print(mat_ethical.round(3))

users = df.groupby(["UserId", "Variant"]).apply(
    lambda g: pd.Series({
        "TotalEvents"         : len(g),
        "ActiveDays"          : g["Date"].nunique(),
        "SessionCount"        : g["SessionId"].nunique(),
        "BetsPlaced"          : (g["EventType"] == "BetPlaced").sum(),
        "BetAttempts"         : (g["EventType"] == "BetAttempted").sum(),
        "OutcomeSelections"   : (g["EventType"] == "OutcomeSelected").sum(),
        "CasinoSpins"         : (g["EventType"] == "CasinoSpinClicked").sum(),
        "NearMissCount"       : (g["EventType"] == "NearMissShown").sum(),
        "PageViews"           : (g["EventType"] == "PageViewed").sum(),
        "OutcomeRemovedCount" : (g["EventType"] == "OutcomeRemoved").sum(),
        "ConfirmCancelled"    : (g["EventType"] == "BetConfirmationCancelled").sum(),
        "Deposits"            : (g["EventType"] == "DepositCompleted").sum(),
    }), include_groups=False
).reset_index()

FEAT = ["TotalEvents", "ActiveDays", "SessionCount", "BetsPlaced", "BetAttempts",
        "OutcomeSelections", "CasinoSpins", "NearMissCount", "PageViews",
        "OutcomeRemovedCount", "ConfirmCancelled", "Deposits"]

X_scaled = StandardScaler().fit_transform(users[FEAT].values)
Z        = linkage(X_scaled, method="ward")
users["ClusterID"] = fcluster(Z, t=4, criterion="maxclust")

# Sortare dupa intensitate → denumiri consistente cu starile comportamentale
intensity     = users.groupby("ClusterID")[["CasinoSpins", "BetsPlaced"]].mean().sum(axis=1)
rank_order    = intensity.sort_values().index.tolist()   # de la cel mai putin la cel mai mult activ
NAME_BY_RANK  = ["Prudent", "Recreational", "Engaged", "Highly Engaged"]
CLUSTER_NAME_MAP = {cid: name for cid, name in zip(rank_order, NAME_BY_RANK)}

users["ClusterName"] = users["ClusterID"].map(CLUSTER_NAME_MAP)
users.to_csv(OUTPUT_TABLES / "week2_user_clusters.csv", index=False)

CLUSTER_ORDER = ["Prudent", "Recreational", "Engaged", "Highly Engaged"]
comp = users.groupby(["ClusterName", "Variant"]).size().unstack(fill_value=0)
comp = comp.reindex(CLUSTER_ORDER, fill_value=0)
if "Dark"    not in comp.columns: comp["Dark"]    = 0
if "Ethical" not in comp.columns: comp["Ethical"] = 0
comp["Total"] = comp.sum(axis=1)
comp.to_csv(OUTPUT_TABLES / "week2_cluster_composition.csv")

pca     = PCA(n_components=2)
X_pca   = pca.fit_transform(X_scaled)
users["PC1"] = X_pca[:, 0]
users["PC2"] = X_pca[:, 1]
var_exp = pca.explained_variance_ratio_ * 100

print("\n--- CLUSTERING W2 (Dark vs Ethical per tipologie) ---")
print(comp.to_string())
print(f"\nPCA: PC1={var_exp[0]:.1f}%  PC2={var_exp[1]:.1f}%  Total={sum(var_exp):.1f}%")

N   = len(STATES)
fig, axes = plt.subplots(2, 2, figsize=(15, 12))

# --- [0,0] Heatmap Dark W2 ---
ax = axes[0, 0]
im = ax.imshow(mat_dark.values, cmap="Reds", vmin=0, vmax=1, aspect="auto")
ax.set_xticks(range(N)); ax.set_yticks(range(N))
ax.set_xticklabels(SHORT, fontsize=10)
ax.set_yticklabels(SHORT, fontsize=10)
ax.set_title("Tranzitii comportamentale — Dark W2", fontsize=11, fontweight="bold")
ax.set_xlabel("Stare urmatoare (sesiunea t+1)")
ax.set_ylabel("Stare curenta (sesiunea t)")
for i in range(N):
    for j in range(N):
        val = mat_dark.values[i, j]
        cnt = mat_dark_raw.values[i, j]
        if val > 0.01:
            ax.text(j, i, f"{val:.2f}\n(n={cnt})",
                    ha="center", va="center", fontsize=8,
                    color="white" if val > 0.55 else "black", fontweight="bold")
plt.colorbar(im, ax=ax, shrink=0.8, label="Probabilitate")

# --- [0,1] Heatmap Ethical W2 ---
ax = axes[0, 1]
im = ax.imshow(mat_ethical.values, cmap="Greens", vmin=0, vmax=1, aspect="auto")
ax.set_xticks(range(N)); ax.set_yticks(range(N))
ax.set_xticklabels(SHORT, fontsize=10)
ax.set_yticklabels(SHORT, fontsize=10)
ax.set_title("Tranzitii comportamentale — Ethical W2", fontsize=11, fontweight="bold")
ax.set_xlabel("Stare urmatoare (sesiunea t+1)")
ax.set_ylabel("Stare curenta (sesiunea t)")
for i in range(N):
    for j in range(N):
        val = mat_ethical.values[i, j]
        cnt = mat_eth_raw.values[i, j]
        if val > 0.01:
            ax.text(j, i, f"{val:.2f}\n(n={cnt})",
                    ha="center", va="center", fontsize=8,
                    color="white" if val > 0.55 else "black", fontweight="bold")
plt.colorbar(im, ax=ax, shrink=0.8, label="Probabilitate")

# --- [1,0] PCA 2D — tipologii ---
ax = axes[1, 0]
for name in CLUSTER_ORDER:
    mask = users["ClusterName"] == name
    if mask.sum() > 0:
        ax.scatter(users.loc[mask, "PC1"], users.loc[mask, "PC2"],
                   color=COLORS_STATE[name], s=90, alpha=0.85,
                   label=name, edgecolors="white", linewidths=0.5)
ax.set_title(
    f"PCA 2D — Tipologii utilizatori W2\n({var_exp[0]:.1f}% + {var_exp[1]:.1f}% varianta)",
    fontsize=10, fontweight="bold")
ax.set_xlabel(f"PC1 ({var_exp[0]:.1f}%)")
ax.set_ylabel(f"PC2 ({var_exp[1]:.1f}%)")
ax.legend(fontsize=8); ax.grid(alpha=0.3)

# --- [1,1] Compozitia tipologiilor ---
ax = axes[1, 1]
x  = np.arange(4)
dark_cnt    = [comp.loc[c, "Dark"]    for c in CLUSTER_ORDER]
ethical_cnt = [comp.loc[c, "Ethical"] for c in CLUSTER_ORDER]
bar_colors  = [COLORS_STATE[c] for c in CLUSTER_ORDER]

ax.bar(x, dark_cnt,    color=bar_colors, alpha=0.55,
       label="Dark",    edgecolor="#d62728", linewidth=2)
ax.bar(x, ethical_cnt, bottom=dark_cnt, color=bar_colors, alpha=0.90,
       label="Ethical", edgecolor="#2ca02c", linewidth=2)
ax.set_xticks(x)
ax.set_xticklabels(["Prudent", "Recr.", "Engaged", "H.Eng."], fontsize=9)
ax.set_title("Compozitia tipologiilor\n(Dark vs Ethical) — W2", fontsize=10, fontweight="bold")
ax.set_ylabel("Numar utilizatori"); ax.legend(fontsize=9); ax.grid(axis="y", alpha=0.3)
for xi, (d, e) in enumerate(zip(dark_cnt, ethical_cnt)):
    tot = d + e
    if tot > 0:
        ax.text(xi, tot + 0.1, str(tot),
                ha="center", va="bottom", fontsize=9, fontweight="bold")

plt.suptitle(
    "Week 2 — Markov Stari Comportamentale + Tipologii Utilizatori\n"
    "S1 Prudent  ·  S2 Recreational  ·  S3 Engaged  ·  S4 Highly Engaged",
    fontsize=13, fontweight="bold"
)
plt.tight_layout()
plt.savefig(OUTPUT_CHARTS / "week2_markov_clustering.png", dpi=300)
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week2_markov_behavioral_dark.csv'}")
print(f"  {OUTPUT_TABLES / 'week2_markov_behavioral_ethical.csv'}")
print(f"  {OUTPUT_TABLES / 'week2_user_clusters.csv'}")
print(f"  {OUTPUT_TABLES / 'week2_cluster_composition.csv'}")
print(f"  {OUTPUT_CHARTS / 'week2_markov_clustering.png'}")
