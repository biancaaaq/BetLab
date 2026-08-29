# -*- coding: utf-8 -*-
"""Clustering ierarhic Ward k=5: Minimal/Prudent/Recreational/Engaged/Highly Engaged. 8 features pure gambling, StandardScaler, PCA 2D."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from matplotlib.gridspec import GridSpec
from matplotlib.patches import Polygon
from pathlib import Path
from scipy.cluster.hierarchy import linkage, fcluster
from scipy.spatial import ConvexHull
from sklearn.preprocessing import StandardScaler
from sklearn.decomposition import PCA
from sklearn.metrics import silhouette_score, davies_bouldin_score

DATA_PATH     = Path("../data/InteractionLogs_Week1.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week1")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

COLORS_VAR = {"Dark": "#d62728", "Ethical": "#2ca02c"}
CLUSTER_STATE_COLORS = {
    "Minimal"        : "#7f7f7f",
    "Prudent"        : "#2ca02c",
    "Recreational"   : "#1f77b4",
    "Engaged"        : "#ff7f0e",
    "Highly Engaged" : "#d62728",
}
CLUSTER_ORDER = ["Minimal", "Prudent", "Recreational", "Engaged", "Highly Engaged"]

def draw_convex_hull(x, y, ax, color, alpha_fill=0.12, lw=1.4):
    """
    Deseneaza convex hull in jurul unui set de puncte PCA.
    Nu implica nicio presupunere despre distributie (spre deosebire de elipse).
    Functioneaza si cu n=4 puncte.
    """
    pts = np.column_stack([x, y])
    if len(pts) < 3:
        ax.scatter(x, y, color=color, s=150, alpha=0.4,
                   marker="o", zorder=1)
        return
    try:
        hull    = ConvexHull(pts)
        verts   = np.append(hull.vertices, hull.vertices[0])
        polygon = Polygon(pts[verts], closed=True,
                          facecolor=color, alpha=alpha_fill,
                          edgecolor=color, linewidth=lw,
                          linestyle="--", zorder=1)
        ax.add_patch(polygon)
    except Exception:
        pass

def style_ax(ax, axis="y"):
    ax.grid(axis=axis, alpha=0.20, linestyle="--")
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

df = pd.read_csv(DATA_PATH)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)
df["Date"]      = df["Timestamp"].dt.date

VARIANT_SPECIFIC_EVENTS = {"NearMissShown", "BetConfirmationCancelled"}

users = df.groupby(["UserId", "Variant"]).apply(
    lambda g: pd.Series({
        # TotalCoreEvents: toate evenimentele comune ambelor variante
        # (exclude NearMissShown=Dark only si BetConfirmationCancelled=Ethical only)
        "TotalCoreEvents"  : (~g["EventType"].isin(VARIANT_SPECIFIC_EVENTS)).sum(),
        "ActiveDays"       : g["Date"].nunique(),
        "SessionCount"     : g["SessionId"].nunique(),
        "BetsPlaced"       : (g["EventType"] == "BetPlaced").sum(),
        "BetAttempts"      : (g["EventType"] == "BetAttempted").sum(),
        "OutcomeSelections": (g["EventType"] == "OutcomeSelected").sum(),
        "CasinoSpins"      : (g["EventType"] == "CasinoSpinClicked").sum(),
        "NearMissCount"    : (g["EventType"] == "NearMissShown").sum(),
        "PageViews"        : (g["EventType"] == "PageViewed").sum(),
        "ConfirmCancelled" : (g["EventType"] == "BetConfirmationCancelled").sum(),
        "Deposits"         : (g["EventType"] == "DepositCompleted").sum(),
        "CashOuts"         : (g["EventType"] == "CashOut").sum(),
    }),
    include_groups=False
).reset_index()

# Features excluse intentionat:
#   NearMissCount, ConfirmCancelled — specifice variantei (circularitate)
#   TotalCoreEvents, PageViews     — comportament de browsing, nu de gambling
# Raman 8 features pure de gambling, comune ambelor variante.
FEATURE_COLS = [
    "ActiveDays", "SessionCount",
    "BetsPlaced", "BetAttempts", "OutcomeSelections",
    "CasinoSpins", "Deposits", "CashOuts",
]

X_raw    = users[FEATURE_COLS].values
scaler   = StandardScaler()
X_scaled = scaler.fit_transform(X_raw)

K_RANGE   = range(2, 7)
Z         = linkage(X_scaled, method="ward")

sil_scores = []
db_scores  = []
for k in K_RANGE:
    lbl = fcluster(Z, t=k, criterion="maxclust")
    sil_scores.append(silhouette_score(X_scaled, lbl))
    db_scores.append(davies_bouldin_score(X_scaled, lbl))

best_k_sil = list(K_RANGE)[int(np.argmax(sil_scores))]
best_k_db  = list(K_RANGE)[int(np.argmin(db_scores))]

print("=" * 70)
print("WEEK 1 — VALIDARE NUMAR DE CLUSTERE")
print("=" * 70)
print(f"{'k':>4}  {'Silhouette':>12}  {'Davies-Bouldin':>16}")
print("-" * 36)
for k, s, d in zip(K_RANGE, sil_scores, db_scores):
    marker = " ← ales" if k == 5 else ""
    print(f"{k:>4}  {s:>12.4f}  {d:>16.4f}{marker}")
print(f"\nBest Silhouette: k={best_k_sil}  |  Best DB (min): k={best_k_db}")

labels_4     = fcluster(Z, t=5, criterion="maxclust")
users["Cluster"] = labels_4

_intensity    = users.groupby("Cluster")[["CasinoSpins","BetsPlaced","Deposits"]].mean().sum(axis=1)
_rank_order   = _intensity.sort_values().index.tolist()
CLUSTER_NAMES = {cid: name for cid, name in zip(_rank_order, CLUSTER_ORDER)}
users["ClusterName"] = users["Cluster"].map(CLUSTER_NAMES)

users.to_csv(OUTPUT_TABLES / "week1_user_clusters.csv", index=False)

cluster_profile = (
    users.groupby("ClusterName")[FEATURE_COLS]
    .mean().round(2).reindex(CLUSTER_ORDER)
)
cluster_profile.to_csv(OUTPUT_TABLES / "week1_cluster_profiles.csv")

comp = users.groupby(["ClusterName","Variant"]).size().unstack(fill_value=0)
comp = comp.reindex(CLUSTER_ORDER, fill_value=0)
for c in ["Dark","Ethical"]:
    if c not in comp.columns:
        comp[c] = 0
comp["Total"] = comp.sum(axis=1)
comp.to_csv(OUTPUT_TABLES / "week1_cluster_composition.csv")

print("\n" + "=" * 70)
print("PROFIL MEDIU PER CLUSTER")
print("=" * 70)
print(cluster_profile[["SessionCount","BetsPlaced","CasinoSpins","Deposits","CashOuts"]].T.to_string())
print("\nCOMPOZITIE DARK vs ETHICAL:")
print(comp.to_string())

pca   = PCA(n_components=2)
X_pca = pca.fit_transform(X_scaled)
users["PC1"] = X_pca[:, 0]
users["PC2"] = X_pca[:, 1]
var_exp  = pca.explained_variance_ratio_ * 100
var_loss = 100 - sum(var_exp)
print(f"\nPCA: PC1={var_exp[0]:.1f}%  PC2={var_exp[1]:.1f}%  "
      f"Total={sum(var_exp):.1f}%  (varianta pierduta: {var_loss:.1f}%)")

fig = plt.figure(figsize=(18, 12))
gs  = GridSpec(2, 3, figure=fig, hspace=0.44, wspace=0.38)

ax_sil  = fig.add_subplot(gs[0, 0])   # Silhouette + DB
ax_pca  = fig.add_subplot(gs[0, 1])   # PCA by cluster
ax_comp = fig.add_subplot(gs[0, 2])   # Composition %
ax_heat = fig.add_subplot(gs[1, 0:2]) # Heatmap (wide)
ax_rad  = fig.add_subplot(gs[1, 2], projection="polar")  # Radar

# [0,0] Silhouette + Davies-Bouldin
k_list = list(K_RANGE)
color_sil = "#1f77b4"
color_db  = "#ff7f0e"

ax_sil2 = ax_sil.twinx()
ln1 = ax_sil.plot(k_list, sil_scores, "o-", color=color_sil,
                  linewidth=2.2, markersize=7, label="Silhouette Score (↑ mai bine)")
ln2 = ax_sil2.plot(k_list, db_scores, "s--", color=color_db,
                   linewidth=2.2, markersize=7, label="Davies-Bouldin (↓ mai bine)")

ax_sil.axvline(5, color="gray", linestyle=":", linewidth=2, alpha=0.7)
ax_sil.text(5.07, min(sil_scores) + 0.01, "k=5 ales",
            fontsize=8.5, color="gray", va="bottom")

for k, s, d in zip(k_list, sil_scores, db_scores):
    ax_sil.annotate(f"{s:.2f}", (k, s),
                    textcoords="offset points", xytext=(0, 7),
                    ha="center", fontsize=7.5, color=color_sil)
    ax_sil2.annotate(f"{d:.2f}", (k, d),
                     textcoords="offset points", xytext=(0, -13),
                     ha="center", fontsize=7.5, color=color_db)

ax_sil.set_xlabel("Numar de clustere (k)", fontsize=9)
ax_sil.set_ylabel("Silhouette Score", fontsize=9, color=color_sil)
ax_sil2.set_ylabel("Davies-Bouldin Index", fontsize=9, color=color_db)
ax_sil.set_xticks(k_list)
ax_sil.set_title("Validarea numarului de clustere\n(Silhouette + Davies-Bouldin)",
                 fontsize=10, fontweight="bold")
lns = ln1 + ln2
ax_sil.legend(lns, [l.get_label() for l in lns], fontsize=7.5, loc="upper right")
ax_sil.grid(alpha=0.20, linestyle="--")
ax_sil.spines["top"].set_visible(False)

# [0,1] PCA colorat dupa cluster (convex hull)
for name in CLUSTER_ORDER:
    mask = users["ClusterName"] == name
    sub  = users[mask]
    col  = CLUSTER_STATE_COLORS[name]
    n    = mask.sum()

    draw_convex_hull(sub["PC1"].values, sub["PC2"].values,
                     ax_pca, color=col)

    ax_pca.scatter(sub["PC1"], sub["PC2"],
                   color=col, s=85, alpha=0.88, zorder=3,
                   edgecolors="white", linewidths=0.7,
                   label=f"{name} (n={n})")

    # Centroid
    cx, cy = sub["PC1"].mean(), sub["PC2"].mean()
    ax_pca.text(cx, cy, name.replace("Highly ","H.\n"),
                ha="center", va="center", fontsize=7.5,
                fontweight="bold", color="white", zorder=6,
                bbox=dict(boxstyle="round,pad=0.25", facecolor=col,
                          edgecolor="white", linewidth=0.8, alpha=0.90))

ax_pca.set_xlabel(f"PC1 ({var_exp[0]:.1f}% varianta)", fontsize=9)
ax_pca.set_ylabel(f"PC2 ({var_exp[1]:.1f}% varianta)", fontsize=9)
ax_pca.set_title(f"PCA 2D — separarea tipologiilor\n"
                 f"(varianta explicata: {sum(var_exp):.0f}%; "
                 f"pierduta: {var_loss:.0f}%)",
                 fontsize=10, fontweight="bold")
ax_pca.legend(fontsize=7.5, loc="upper right")
ax_pca.grid(alpha=0.20, linestyle="--")
ax_pca.spines["top"].set_visible(False)
ax_pca.spines["right"].set_visible(False)

# [0,2] Compozitie % stacked bar
short_labels = ["Minimal", "Prudent", "Recr.", "Engaged", "H.Eng."]
x    = np.arange(len(CLUSTER_ORDER))
w    = 0.58

dark_pct = [100 * comp.loc[c,"Dark"]    / comp.loc[c,"Total"]
            if comp.loc[c,"Total"] > 0 else 0.0 for c in CLUSTER_ORDER]
eth_pct  = [100 * comp.loc[c,"Ethical"] / comp.loc[c,"Total"]
            if comp.loc[c,"Total"] > 0 else 0.0 for c in CLUSTER_ORDER]
dark_n   = [comp.loc[c,"Dark"]    for c in CLUSTER_ORDER]
eth_n    = [comp.loc[c,"Ethical"] for c in CLUSTER_ORDER]
totals   = [comp.loc[c,"Total"]   for c in CLUSTER_ORDER]

bars_d = ax_comp.bar(x, dark_pct, w,
                     color=COLORS_VAR["Dark"], alpha=0.82,
                     edgecolor="white", label="Dark")
bars_e = ax_comp.bar(x, eth_pct, w, bottom=dark_pct,
                     color=COLORS_VAR["Ethical"], alpha=0.82,
                     edgecolor="white", label="Ethical")

for i, (dp, ep, dn, en, tot) in enumerate(
        zip(dark_pct, eth_pct, dark_n, eth_n, totals)):
    if dp > 8:
        ax_comp.text(i, dp/2, f"{dp:.0f}%\n(n={dn})",
                     ha="center", va="center", fontsize=8.5,
                     fontweight="bold", color="white")
    if ep > 8:
        ax_comp.text(i, dp + ep/2, f"{ep:.0f}%\n(n={en})",
                     ha="center", va="center", fontsize=8.5,
                     fontweight="bold", color="white")
    ax_comp.text(i, 102, f"n={tot}",
                 ha="center", va="bottom", fontsize=8, color="#333")

ax_comp.set_xticks(x)
ax_comp.set_xticklabels(short_labels, fontsize=8.5)
ax_comp.set_ylim(0, 115)
ax_comp.set_ylabel("% utilizatori per tipologie")
ax_comp.set_title("Compozitia tipologiilor\nDark vs Ethical (%)",
                  fontsize=10, fontweight="bold")
ax_comp.legend(handles=[mpatches.Patch(color=COLORS_VAR[v], label=v)
                          for v in ["Dark","Ethical"]],
               fontsize=8, loc="upper left")
ax_comp.axhline(50, color="gray", linewidth=0.8, linestyle=":", alpha=0.5)
style_ax(ax_comp)

# [1,0:1] Heatmap features normalizate
HEATMAP_FEATURES = {
    "SessionCount"     : "Sesiuni",
    "ActiveDays"       : "Zile\nactive",
    "BetsPlaced"       : "Pariuri\nplasate",
    "BetAttempts"      : "Incercari\npariu",
    "CasinoSpins"      : "Spins\ncazino",
    "OutcomeSelections": "Selectii\noutcome",
    "Deposits"         : "Depozite",
    "CashOuts"         : "Retrageri",
}

heat_data = cluster_profile[list(HEATMAP_FEATURES.keys())].copy()
heat_norm = (heat_data - heat_data.min()) / (heat_data.max() - heat_data.min() + 1e-9)

im = ax_heat.imshow(heat_norm.values, cmap="RdYlGn_r",
                    aspect="auto", vmin=0, vmax=1)

for i, cluster_name in enumerate(CLUSTER_ORDER):
    for j, feat in enumerate(HEATMAP_FEATURES.keys()):
        raw_val  = heat_data.loc[cluster_name, feat]
        norm_val = heat_norm.loc[cluster_name, feat]
        txt_col  = "white" if norm_val > 0.65 or norm_val < 0.18 else "black"
        ax_heat.text(j, i, f"{raw_val:.0f}",
                     ha="center", va="center",
                     fontsize=10, fontweight="bold", color=txt_col)

ax_heat.set_xticks(range(len(HEATMAP_FEATURES)))
ax_heat.set_xticklabels(list(HEATMAP_FEATURES.values()), fontsize=9)
ax_heat.set_yticks(range(len(CLUSTER_ORDER)))
ax_heat.set_yticklabels(CLUSTER_ORDER, fontsize=10.5, fontweight="bold")
for ytick, name in zip(ax_heat.get_yticklabels(), CLUSTER_ORDER):
    ytick.set_color(CLUSTER_STATE_COLORS[name])

ax_heat.set_title("Profilul mediu per tipologie  (valori absolute; "
                  "culoare = intensitate relativa: verde=scazut → rosu=ridicat)",
                  fontsize=10, fontweight="bold")

cbar = plt.colorbar(im, ax=ax_heat, orientation="vertical",
                    shrink=0.85, pad=0.01)
cbar.set_label("Intensitate relativa", fontsize=8)
cbar.set_ticks([0, 0.5, 1])
cbar.set_ticklabels(["Scazut", "Mediu", "Ridicat"])

# [1,2] Radar
RADAR_FEATS = ["BetsPlaced", "CasinoSpins", "SessionCount",
               "BetAttempts", "ActiveDays", "Deposits"]
RADAR_LBLS  = ["Pariuri", "Spins\ncazino", "Sesiuni",
               "Incercari\npariu", "Zile\nactive", "Depozite"]
N      = len(RADAR_FEATS)
angles = np.linspace(0, 2 * np.pi, N, endpoint=False).tolist()
angles += angles[:1]

radar_data = cluster_profile[RADAR_FEATS].copy()
# Normalizare cu floor la 0.08: clusterul de minim are radius 0.08, nu 0
# Altfel Prudent (cel mai putin intens) ar colaba intr-un punct invizibil in centru
_rmin = radar_data.min()
_rmax = radar_data.max()
radar_norm = 0.08 + 0.88 * (radar_data - _rmin) / (_rmax - _rmin + 1e-9)

# Deseneaza in ordine inversa intensitatii (H.Engaged primul, Prudent ultimul = pe deasupra)
# Asa Prudent nu e ascuns sub fill-urile clusterelor mai mari
_radar_draw_order = list(reversed(CLUSTER_ORDER))
for name in _radar_draw_order:
    if name in radar_norm.index:
        vals  = radar_norm.loc[name, RADAR_FEATS].tolist()
        vals += vals[:1]
        lw = 2.8 if name == "Prudent" else 2.0
        ax_rad.plot(angles, vals, linewidth=lw, zorder=4,
                    color=CLUSTER_STATE_COLORS[name], label=name)
        ax_rad.fill(angles, vals, alpha=0.10, zorder=3,
                    color=CLUSTER_STATE_COLORS[name])

ax_rad.set_xticks(angles[:-1])
ax_rad.set_xticklabels(RADAR_LBLS, fontsize=8.5)
ax_rad.set_ylim(0, 1)
ax_rad.set_yticks([0.25, 0.5, 0.75])
ax_rad.set_yticklabels(["0.25", "0.50", "0.75"], fontsize=6.5)
ax_rad.set_title("Amprenta comportamentala\nper tipologie (normalizat 0-1)",
                 fontsize=10, fontweight="bold", pad=18)
# Reconstituie legenda in CLUSTER_ORDER (Prudent primul), indiferent de ordinea desenarii
_radar_handles = {
    name: plt.Line2D([0], [0], color=CLUSTER_STATE_COLORS[name],
                     linewidth=2.4 if name == "Prudent" else 2.0, label=name)
    for name in CLUSTER_ORDER if name in radar_norm.index
}
ax_rad.legend(
    handles=[_radar_handles[n] for n in CLUSTER_ORDER if n in _radar_handles],
    loc="upper right", bbox_to_anchor=(1.42, 1.15), fontsize=7.5
)

sil_k5 = sil_scores[list(K_RANGE).index(5)]
plt.suptitle(
    f"Week 1 — Segmentare comportamentala (n=40) · Clustering ierarhic Ward · k=5 · Silhouette={sil_k5:.3f}",
    fontsize=12, fontweight="bold"
)

plt.savefig(OUTPUT_CHARTS / "week1_clustering.png", dpi=300, bbox_inches="tight")
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'week1_user_clusters.csv'}")
print(f"  {OUTPUT_TABLES / 'week1_cluster_profiles.csv'}")
print(f"  {OUTPUT_TABLES / 'week1_cluster_composition.csv'}")
print(f"  {OUTPUT_CHARTS / 'week1_clustering.png'}")
