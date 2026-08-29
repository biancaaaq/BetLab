# -*- coding: utf-8 -*-
"""Markov transitions W1+W2: stari bazate pe cuartilele total_actions = casino_spins + bet_placed. Dark vs Ethical."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from pathlib import Path
from collections import defaultdict
from matplotlib.patches import Rectangle, Circle

DATA_W1       = Path("../data/InteractionLogs_Week1.csv")
DATA_W2       = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts/week1")
OUTPUT_TABLES.mkdir(parents=True, exist_ok=True)
OUTPUT_CHARTS.mkdir(parents=True, exist_ok=True)

w1 = pd.read_csv(DATA_W1); w1["Week"] = 1
w2 = pd.read_csv(DATA_W2); w2["Week"] = 2
df = pd.concat([w1, w2], ignore_index=True)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)
df["Date"]      = df["Timestamp"].dt.date
df = df.sort_values(["UserId", "Timestamp"]).reset_index(drop=True)

STATES = ["Scăzut", "Moderat", "Ridicat", "Intens"]
SHORT  = ["Scăzut", "Moderat", "Ridicat", "Intens"]

COLORS_STATE = {
    "Scăzut":        "#2ca02c",
    "Moderat":   "#1f77b4",
    "Ridicat":        "#ff7f0e",
    "Intens": "#d62728",
}
COLORS_VAR = {"Dark": "#d62728", "Ethical": "#2ca02c"}

session_feats = (
    df.groupby(["Variant", "UserId", "SessionId"])
    .agg(
        casino_spins  = ("EventType", lambda x: (x == "CasinoSpinClicked").sum()),
        bet_placed    = ("EventType", lambda x: (x == "BetPlaced").sum()),
        stake_changed = ("EventType", lambda x: (x == "StakeChanged").sum()),
        bc_cancelled  = ("EventType", lambda x: (x == "BetConfirmationCancelled").sum()),
        first_ts      = ("Timestamp", "min"),
    )
    .reset_index()
)

# Activitate totala per sesiune — simetrica pentru ambele variante
# (casino_spins + bet_placed; fara mecanisme specifice variantei)
session_feats["total_actions"] = session_feats["casino_spins"] + session_feats["bet_placed"]

# Praguri bazate pe distributia reala a datelor (quartile)
Q1 = session_feats["total_actions"].quantile(0.25)
Q2 = session_feats["total_actions"].quantile(0.50)
Q3 = session_feats["total_actions"].quantile(0.75)

def classify_session(total):
    if total <= Q1: return "Scăzut"
    if total <= Q2: return "Moderat"
    if total <= Q3: return "Ridicat"
    return "Intens"

session_feats["State"] = session_feats["total_actions"].apply(classify_session)
session_feats          = session_feats.sort_values(["UserId", "Variant", "first_ts"])
session_feats.to_csv(OUTPUT_TABLES / "behavioral_states_per_session.csv", index=False)

print("=" * 70)
print("BEHAVIORAL STATE MARKOV — Dark vs Ethical (W1+W2)")
print("=" * 70)
print(f"\nPraguri cuartile (total_actions = casino_spins + bet_placed):")
print(f"  Q1 (Scăzut <=)       : {Q1:.1f}")
print(f"  Q2 (Moderat <=)  : {Q2:.1f}")
print(f"  Q3 (Ridicat <=)       : {Q3:.1f}")
print(f"  Intens        : > {Q3:.1f}")

state_dist = (
    session_feats
    .groupby(["Variant", "State"])
    .size()
    .unstack(fill_value=0)
    .reindex(columns=STATES, fill_value=0)
)
state_dist["Total"] = state_dist.sum(axis=1)
for s in STATES:
    state_dist[f"{s}_%"] = (state_dist[s] / state_dist["Total"] * 100).round(1)

print("\nDistributia starilor comportamentale per varianta:")
print(state_dist)

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

mat_dark_raw,  mat_dark    = build_transition_matrix(dark_sess)
mat_eth_raw,   mat_ethical = build_transition_matrix(ethical_sess)

mat_dark.round(4).to_csv(OUTPUT_TABLES / "markov_behavioral_dark.csv")
mat_ethical.round(4).to_csv(OUTPUT_TABLES / "markov_behavioral_ethical.csv")

total_dark_tr    = int(mat_dark_raw.values.sum())
total_ethical_tr = int(mat_eth_raw.values.sum())

print("\n--- Matrice tranzitii DARK (conturi brute) ---")
print(mat_dark_raw)
print("\n--- Matrice tranzitii DARK (probabilitati) ---")
print(mat_dark.round(3))
print("\n--- Matrice tranzitii ETHICAL (probabilitati) ---")
print(mat_ethical.round(3))

KEY_TR = [
    ("Intens", "Intens", "Intens -> Intens"),
    ("Scăzut",        "Intens", "Scăzut -> Intens"),
    ("Ridicat",        "Intens", "Ridicat -> Intens"),
]
print("\n--- TRANZITII CHEIE ---")
print(f"{'Tranzitie':<30} {'Dark':>8} {'Ethical':>10}")
print("-" * 50)
for a, b, label in KEY_TR:
    pd_ = mat_dark.loc[a, b]    if a in mat_dark.index    else 0.0
    pe_ = mat_ethical.loc[a, b] if a in mat_ethical.index else 0.0
    print(f"  {label:<28} {pd_:>8.3f} {pe_:>10.3f}")

N = len(STATES)

def get_pct(variant, state):
    try:
        return float(state_dist.loc[variant, f"{state}_%"])
    except Exception:
        return 0.0

def annotate_heatmap(ax, mat_prob, mat_raw):
    """Text negru + 2 zecimale. Rândul e ajustat astfel incat suma afisata = 1.00."""
    for i in range(N):
        row = mat_prob.values[i, :]
        # rotunjim la 2 zecimale si corectam diferenta pe cel mai mare element
        rounded = [round(float(v), 2) for v in row]
        if sum(row) > 0:          # doar daca randul are tranzitii reale
            diff = round(1.0 - sum(rounded), 2)
            if diff != 0:
                max_j = int(np.argmax(row))
                rounded[max_j] = round(rounded[max_j] + diff, 2)
        for j in range(N):
            val = rounded[j]
            cnt = int(mat_raw.values[i, j])
            if mat_prob.values[i, j] > 0.01:
                star = " *" if cnt < 5 else ""
                fw   = "normal" if cnt < 5 else "bold"
                ax.text(j, i, f"{val:.2f}{star}\n(n={cnt})",
                        ha="center", va="center", fontsize=8,
                        color="black", fontweight=fw)

fig, axes = plt.subplots(2, 2, figsize=(15, 12))

# [0,0] Heatmap Dark
ax = axes[0, 0]
im = ax.imshow(mat_dark.values, cmap="Reds", vmin=0, vmax=1, aspect="auto")
ax.set_xticks(range(N)); ax.set_yticks(range(N))
ax.set_xticklabels(SHORT, fontsize=10); ax.set_yticklabels(SHORT, fontsize=10)
ax.set_title("Tranzitii comportamentale — Dark W1+W2",
             fontsize=10, fontweight="bold")
ax.set_xlabel("Stare urmatoare (sesiunea t+1)")
ax.set_ylabel("Stare curenta (sesiunea t)")
annotate_heatmap(ax, mat_dark, mat_dark_raw)
plt.colorbar(im, ax=ax, shrink=0.8, label="Probabilitate")

# [0,1] Heatmap Ethical
ax = axes[0, 1]
im = ax.imshow(mat_ethical.values, cmap="Greens", vmin=0, vmax=1, aspect="auto")
ax.set_xticks(range(N)); ax.set_yticks(range(N))
ax.set_xticklabels(SHORT, fontsize=10); ax.set_yticklabels(SHORT, fontsize=10)
ax.set_title("Tranzitii comportamentale — Ethical W1+W2",
             fontsize=10, fontweight="bold")
ax.set_xlabel("Stare urmatoare (sesiunea t+1)")
ax.set_ylabel("Stare curenta (sesiunea t)")
annotate_heatmap(ax, mat_ethical, mat_eth_raw)
plt.colorbar(im, ax=ax, shrink=0.8, label="Probabilitate")

# [1,0] Distributia starilor
ax = axes[1, 0]
x     = np.arange(N)
width = 0.36

dark_pcts    = [get_pct("Dark",    s) for s in STATES]
ethical_pcts = [get_pct("Ethical", s) for s in STATES]

bars_d = ax.bar(x - width/2, dark_pcts,    width,
                label="Dark",    color=COLORS_VAR["Dark"],    alpha=0.82)
bars_e = ax.bar(x + width/2, ethical_pcts, width,
                label="Ethical", color=COLORS_VAR["Ethical"], alpha=0.82)

for bars in [bars_d, bars_e]:
    for bar in bars:
        h = bar.get_height()
        if h > 1:
            ax.text(bar.get_x() + bar.get_width() / 2, h + 1.2,
                    f"{h:.0f}%", ha="center", va="bottom",
                    fontsize=9, fontweight="bold")

ax.set_xticks(x); ax.set_xticklabels(SHORT, fontsize=10)
ax.set_ylim(0, 100)
ax.set_ylabel("% din sesiuni")
ax.set_title("Distributia starilor comportamentale\nDark vs Ethical (W1+W2)",
             fontsize=11, fontweight="bold")
ax.legend()
ax.grid(axis="y", alpha=0.22, linestyle="--")
ax.spines["top"].set_visible(False)
ax.spines["right"].set_visible(False)

# [1,1] Diagrama Markov — varianta Dark (noduri colorate + sageti cu grosime proportionala cu probabilitatea; p >= MIN_P)
ax = axes[1, 1]
ax.set_xlim(0, 1)
ax.set_ylim(0, 1)
ax.set_aspect("equal", adjustable="box")
ax.axis("off")

NODE_POS = {
    "Scăzut":        (0.18, 0.76),
    "Moderat":   (0.82, 0.76),
    "Ridicat":        (0.18, 0.24),
    "Intens": (0.82, 0.24),
}
NODE_R = 0.11
MIN_P  = 0.10   # afisam tranzitii cu p >= MIN_P

# Self-loop: puncte pe marginea cercului la ±45° fata de varf/baza nodului
# Top nodes  (Scăzut, Moderat)  : 135°→45°  (stanga-sus→dreapta-sus), rad=+1.5 → arc SUS
# Bottom nodes (Ridicat, Intens): 315°→225° (dreapta-jos→stanga-jos), rad=+1.5 → arc JOS
# arc_peak = chord_mid_y + RAD_LOOP*(ex-sx)/2
LOOP_DEG = {
    "Scăzut":        (135, 45),
    "Moderat":   (135, 45),
    "Ridicat":        (315, 225),
    "Intens": (315, 225),
}
RAD_LOOP = 1.5

LABEL_OFFSETS = {
    ("Scăzut", "Ridicat"): (-0.055, 0.020),
    ("Ridicat", "Scăzut"): (0.055, -0.020),

    ("Scăzut", "Intens"): (0.000, 0.060),
    ("Intens", "Scăzut"): (0.000, -0.060),
}

for fs in STATES:
    for ts in STATES:
        p = mat_dark.loc[fs, ts]
        if p < MIN_P:
            continue
        lw    = 0.8 + p * 6.5
        alpha = min(1.0, 0.30 + p)

        if fs == ts:
            cx, cy = NODE_POS[fs]

            if fs in ["Scăzut", "Moderat"]:
                # buclă deasupra nodului
                start_deg, end_deg = 160, 20
                loop_rad = -1.5
                label_y = cy + NODE_R + 0.12
            else:
                # buclă dedesubtul nodului
                start_deg, end_deg = 200, 340
                loop_rad = 1.5
                label_y = cy - NODE_R - 0.12

            sx = cx + NODE_R * np.cos(np.deg2rad(start_deg))
            sy = cy + NODE_R * np.sin(np.deg2rad(start_deg))

            ex = cx + NODE_R * np.cos(np.deg2rad(end_deg))
            ey = cy + NODE_R * np.sin(np.deg2rad(end_deg))

            ax.annotate(
                "",
                xy=(ex, ey),
                xytext=(sx, sy),
                arrowprops=dict(
                    arrowstyle="->, head_width=0.22, head_length=0.16",
                    color=COLORS_STATE[fs],
                    lw=lw,
                    alpha=alpha,
                    connectionstyle=f"arc3,rad={loop_rad}",
                ),
                zorder=2,
            )

            ax.text(
                cx,
                label_y,
                f"{p:.2f}",
                fontsize=8.5,
                ha="center",
                va="center",
                fontweight="bold",
                bbox=dict(
                    boxstyle="round,pad=0.15",
                    facecolor="white",
                    edgecolor=COLORS_STATE[fs],
                    alpha=0.95,
                    lw=0.9,
                ),
                zorder=4,
            )
        else:
            fx, fy = NODE_POS[fs]
            tx, ty = NODE_POS[ts]
            dx, dy = tx - fx, ty - fy
            dist   = np.hypot(dx, dy)
            sx, sy = fx + dx/dist * NODE_R, fy + dy/dist * NODE_R
            ex, ey = tx - dx/dist * NODE_R, ty - dy/dist * NODE_R
            # rad pozitiv constant: fiecare arc se inflecteaza la STANGA directiei
            # de deplasare → perechi bidirecționale ajung pe laturi OPUSE ale corzii
            rad = 0.18
            ax.annotate("",
                xy=(ex, ey), xytext=(sx, sy),
                arrowprops=dict(
                    arrowstyle="->, head_width=0.20, head_length=0.14",
                    color=COLORS_STATE[fs], lw=lw, alpha=alpha,
                    connectionstyle=f"arc3,rad={rad}",
                ),
                zorder=2,
            )
            # eticheta la mijlocul arcului (formula corecta: - rad*(ey-sy), + rad*(ex-sx))
            mid_x = (sx + ex) / 2 - rad * (ey - sy) * 0.35
            mid_y = (sy + ey) / 2 + rad * (ex - sx) * 0.35
            dx_lbl, dy_lbl = LABEL_OFFSETS.get((fs, ts), (0.0, 0.0))
            mid_x += dx_lbl
            mid_y += dy_lbl
            ax.text(mid_x, mid_y, f"{p:.2f}",
                    fontsize=7.5, ha="center", va="center",
                    bbox=dict(boxstyle="round,pad=0.10", facecolor="white",
                              alpha=0.88, edgecolor="gray", lw=0.3),
                    zorder=3)

for state in STATES:
    cx, cy = NODE_POS[state]
    circ = Circle((cx, cy), NODE_R,
                  color=COLORS_STATE[state], alpha=0.92, zorder=5)
    ax.add_patch(circ)
    lbl = state
    ax.text(cx, cy, lbl, ha="center", va="center",
            fontsize=9, fontweight="bold", color="white", zorder=6)

ax.set_title(
    f"Diagrama Markov — varianta Dark  (p >= {MIN_P})\n"
    "Grosimea sagetilor este proportionala cu probabilitatea de tranzitie",
    fontsize=9, fontweight="bold")

dark_he      = get_pct("Dark",    "Intens")
eth_he       = get_pct("Ethical", "Intens")
dark_persist = mat_dark.loc["Intens", "Intens"]

plt.suptitle(
    f"Lanțuri Markov: tranziții între stările de activitate · Dark vs Ethical· "
    f"Tranzitii: Dark={total_dark_tr}, Ethical={total_ethical_tr}",
    fontsize=12, fontweight="bold"
)
fig.text(
    0.5, 0.005,
    f"Stari prin cuartile (casino_spins + bet_placed): "
    f"Scăzut (<=Q1={Q1:.0f})  *  Moderat (Q1–Q2={Q2:.0f})  *  "
    f"Ridicat (Q2–Q3={Q3:.0f})  *  Intens (>Q3)  |  "
    "* celule cu n<5 = estimare instabila",
    ha="center", fontsize=7.5, color="#555555", style="italic"
)
fig.subplots_adjust(top=0.90, bottom=0.06, hspace=0.45, wspace=0.38)
plt.savefig(OUTPUT_CHARTS / "week1_markov.png", dpi=300, bbox_inches="tight")
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'behavioral_states_per_session.csv'}")
print(f"  {OUTPUT_TABLES / 'markov_behavioral_dark.csv'}")
print(f"  {OUTPUT_TABLES / 'markov_behavioral_ethical.csv'}")
print(f"  {OUTPUT_CHARTS / 'week1_markov.png'}")
