# -*- coding: utf-8 -*-
"""Instrumente protective Ethical: AutoSpin (referinta), limite profil, BetConfirmation, CashOut. Adoptie per user + corelatie Spearman."""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import json
from pathlib import Path
from scipy.stats import mannwhitneyu, spearmanr

DATA_W1       = Path("../data/InteractionLogs_Week1.csv")
DATA_W2       = Path("../data/InteractionLogs_Week2.csv")
OUTPUT_TABLES = Path("../outputs/tables")
OUTPUT_CHARTS = Path("../outputs/charts")
(OUTPUT_CHARTS / "protective").mkdir(parents=True, exist_ok=True)
OUT_C = OUTPUT_CHARTS / "protective"

COLORS = {"Dark": "#d62728", "Ethical": "#2ca02c"}


def parse_meta(s):
    if not isinstance(s, str) or not s.strip():
        return {}
    s = s.strip()
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1].replace('""', '"')
    try:
        return json.loads(s)
    except Exception:
        return {}


w1 = pd.read_csv(DATA_W1); w1["Week"] = 1
w2 = pd.read_csv(DATA_W2); w2["Week"] = 2
df = pd.concat([w1, w2], ignore_index=True)
df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True)

print("=" * 70)
print("INSTRUMENTE DE PROTECTIE ETICE (W1+W2)")
print("=" * 70)

print("\n\n=== 1. AUTOSPIN (pentru referinta — grafic in script 06) ===")

spins         = df[df["EventType"] == "CasinoSpinClicked"].copy()
spins["meta"] = spins["MetadataJson"].apply(parse_meta)
spins["mode"] = spins["meta"].apply(lambda m: m.get("mode", ""))
spins["stake"]= pd.to_numeric(
    spins["meta"].apply(lambda m: m.get("stake")), errors="coerce")
spins["is_auto"] = spins["mode"] == "auto"

auto_by_variant = (
    spins.groupby("Variant")
    .agg(total_spins=("EventType","count"),
         auto_spins=("is_auto","sum"),
         total_stake=("stake","sum"))
    .reset_index()
)
auto_by_variant["manual_spins"]  = (
    auto_by_variant["total_spins"] - auto_by_variant["auto_spins"])
auto_by_variant["auto_rate_pct"] = (
    auto_by_variant["auto_spins"] / auto_by_variant["total_spins"] * 100)

auto_stake_by_v = (
    spins[spins["is_auto"]].groupby("Variant")["stake"]
    .sum().rename("auto_stake").reset_index()
)
auto_by_variant = auto_by_variant.merge(auto_stake_by_v, on="Variant", how="left")
auto_by_variant["auto_stake"] = auto_by_variant["auto_stake"].fillna(0)
print(auto_by_variant[["Variant","total_spins","auto_spins",
                        "manual_spins","auto_rate_pct","auto_stake"]].to_string(index=False))

autospin_user = (
    spins.groupby(["UserId", "Variant"])
    .apply(
        lambda g: pd.Series({
            "total_spins": len(g),
            "auto_spins":  int(g["is_auto"].sum()),
        }),
        include_groups=False
    )
    .reset_index()
)
auto_stake_per_user = (
    spins[spins["is_auto"]]
    .groupby(["UserId","Variant"])["stake"]
    .sum().rename("auto_stake").reset_index()
)
autospin_user = autospin_user.merge(auto_stake_per_user, on=["UserId","Variant"], how="left")
autospin_user["auto_stake"] = autospin_user["auto_stake"].fillna(0)
autospin_user["auto_rate"]  = autospin_user["auto_spins"] / autospin_user["total_spins"]
autospin_user.to_csv(OUTPUT_TABLES / "autospin_per_user.csv", index=False)

print("\n\n=== 2. PROFILEEDITED — LIMITE RESPONSABILE ===")

pe = df[df["EventType"] == "ProfileEdited"].copy()
pe["dailyDeposit"] = pd.to_numeric(
    pe["MetadataJson"].apply(lambda s: parse_meta(s).get("dailyDeposit")), errors="coerce"
)
pe["dailyLoss"] = pd.to_numeric(
    pe["MetadataJson"].apply(lambda s: parse_meta(s).get("dailyLoss")), errors="coerce"
)

eth_total_users = df[df["Variant"] == "Ethical"]["UserId"].nunique()
pe_eth          = pe[pe["Variant"] == "Ethical"]
users_limits    = pe_eth["UserId"].nunique()

print(f"ProfileEdited total: {len(pe)}  (Dark: {len(pe[pe['Variant']=='Dark'])}  "
      f"Ethical: {len(pe_eth)})")
print(f"Ethical users cu limite setate: {users_limits}/{eth_total_users} "
      f"({users_limits/eth_total_users*100:.0f}%)")
print(f"dailyDeposit: medie={pe_eth['dailyDeposit'].mean():.0f} RON  "
      f"mediana={pe_eth['dailyDeposit'].median():.0f} RON")
print(f"dailyLoss:    medie={pe_eth['dailyLoss'].mean():.0f} RON  "
      f"mediana={pe_eth['dailyLoss'].median():.0f} RON")

pe_user_last = (
    pe_eth.sort_values("Timestamp")
    .groupby("UserId")[["dailyDeposit","dailyLoss"]].last()
    .reset_index()
)
edit_counts  = pe_eth.groupby("UserId").size().rename("edit_count")
pe_user_last = pe_user_last.merge(edit_counts, on="UserId", how="left")
pe_user_last.to_csv(OUTPUT_TABLES / "profile_limits_per_user.csv", index=False)

print("\n\n=== 3. BETCONFIRMATION REALITY CHECK ===")

bcv_ev = df[df["EventType"] == "BetConfirmationViewed"]
bcc_ev = df[df["EventType"] == "BetConfirmationCancelled"]
bp_ev  = df[df["EventType"] == "BetPlaced"]

bcv_u = bcv_ev.groupby(["UserId","Variant"]).size().rename("Viewed")
bcc_u = bcc_ev.groupby(["UserId","Variant"]).size().rename("Cancelled")
bp_u  = bp_ev.groupby(["UserId","Variant"]).size().rename("BetPlaced_count")

conf_df = (
    pd.concat([bcv_u, bcc_u, bp_u], axis=1)
    .fillna(0).astype(float).reset_index()
)
conf_df["ProtectionRate"] = np.where(
    conf_df["Viewed"] > 0,
    conf_df["Cancelled"] / conf_df["Viewed"],
    np.nan
)
conf_df.to_csv(OUTPUT_TABLES / "betconfirmation_per_user.csv", index=False)

eth_conf       = conf_df[conf_df["Variant"] == "Ethical"]
eth_with_modal = eth_conf[eth_conf["Viewed"] > 0]
eth_clean      = eth_with_modal.dropna(subset=["ProtectionRate"])

print(f"Ethical users cu BetConfirmationViewed: {len(eth_with_modal)}/{eth_total_users}")
print(f"Users care au anulat cel putin o data:  {(eth_conf['Cancelled'] > 0).sum()}/{eth_total_users}")
print(f"ProtectionRate medie:   {eth_clean['ProtectionRate'].mean():.1%}")
print(f"ProtectionRate mediana: {eth_clean['ProtectionRate'].median():.1%}")

r_val = p_val = sig_corr = None
if len(eth_clean) >= 3:
    r_val, p_val = spearmanr(eth_clean["ProtectionRate"], eth_clean["BetPlaced_count"])
    sig_corr = ("***" if p_val < 0.001 else "**" if p_val < 0.01
                else "*" if p_val < 0.05 else "n.s.")
    print(f"Spearman ProtectionRate vs BetPlaced: r={r_val:.3f}  p={p_val:.4f}  {sig_corr}")

print("\n\n=== 4. CASHOUT ===")

cashout = df[df["EventType"] == "CashOut"].copy()
cashout["cashoutValue"] = pd.to_numeric(cashout["OldValue"], errors="coerce")

for v in ["Dark", "Ethical"]:
    cv = cashout[cashout["Variant"] == v]["cashoutValue"].dropna()
    print(f"  {v}: n={len(cv)}  total={cv.sum():.0f} RON  "
          f"medie={cv.mean():.1f} RON  mediana={cv.median():.1f} RON")

cashout_user = (
    cashout.groupby(["UserId","Variant"])["cashoutValue"]
    .agg(NumCashouts="count", TotalCashout="sum", AvgCashout="mean")
    .reset_index()
)
cashout_user.to_csv(OUTPUT_TABLES / "cashout_per_user.csv", index=False)

cashout_eth_set = set(cashout[cashout["Variant"] == "Ethical"]["UserId"])

print("\n\n=== 5. ADOPTIA INSTRUMENTELOR DE PROTECTIE ===")

all_eth_set   = set(df[df["Variant"] == "Ethical"]["UserId"].unique())
conf_used_set = set(eth_conf[eth_conf["Viewed"] > 0]["UserId"])
cancelled_set = set(eth_conf[eth_conf["Cancelled"] > 0]["UserId"])
limits_set    = set(pe_eth["UserId"])

summary_tools = pd.DataFrame([
    {"Tool": "BetConfirmationViewed (a vazut modalul)",
     "Users": len(conf_used_set),   "Rate_pct": len(conf_used_set)/eth_total_users*100},
    {"Tool": "BetConfirmationCancelled (a anulat cel putin un pariu)",
     "Users": len(cancelled_set),   "Rate_pct": len(cancelled_set)/eth_total_users*100},
    {"Tool": "ProfileEdited (limite depozit/pierdere setate)",
     "Users": len(limits_set),      "Rate_pct": len(limits_set)/eth_total_users*100},
    {"Tool": "CashOut (a retras dintr-un pariu activ)",
     "Users": len(cashout_eth_set), "Rate_pct": len(cashout_eth_set)/eth_total_users*100},
])
print(summary_tools.to_string(index=False))
summary_tools.to_csv(OUTPUT_TABLES / "protective_tools_adoption.csv", index=False)

fig, axes = plt.subplots(2, 2, figsize=(14, 10))

ax = axes[0, 0]
if len(pe_user_last) > 0:
    rng    = np.random.default_rng(1)
    jitter = rng.uniform(-1.5, 1.5, len(pe_user_last))
    ax.scatter(pe_user_last["dailyDeposit"] + jitter,
               pe_user_last["dailyLoss"]    + jitter,
               s=120, color=COLORS["Ethical"], alpha=0.85,
               edgecolors="white", linewidth=0.5, zorder=3)
    med_dep  = pe_user_last["dailyDeposit"].median()
    med_loss = pe_user_last["dailyLoss"].median()
    ax.axhline(med_loss, color="gray", linestyle="--", alpha=0.5)
    ax.axvline(med_dep,  color="gray", linestyle="--", alpha=0.5)
    ax.set_title(f"Limite Responsabile Setate\n{len(pe_user_last)}/{eth_total_users} useri Ethical",
                 fontsize=10, fontweight="bold")
    ax.set_xlabel(f"Limita depozit zilnic (RON)  |  mediana={med_dep:.0f} RON", fontsize=9)
    ax.set_ylabel(f"Limita pierdere zilnica (RON)  |  mediana={med_loss:.0f} RON", fontsize=9)
    ax.grid(alpha=0.3)

ax = axes[0, 1]
if len(eth_clean) > 0:
    bins_pct = range(0, 110, 10)
    ax.hist(eth_clean["ProtectionRate"] * 100, bins=bins_pct,
            color=COLORS["Ethical"], alpha=0.8, edgecolor="white")
    ax.axvline(eth_clean["ProtectionRate"].mean() * 100, color="darkgreen",
               linestyle="--", linewidth=2,
               label=f"Medie {eth_clean['ProtectionRate'].mean()*100:.0f}%")
    ax.axvline(eth_clean["ProtectionRate"].median() * 100, color="black",
               linestyle=":", linewidth=2,
               label=f"Mediana {eth_clean['ProtectionRate'].median()*100:.0f}%")
    ax.set_xlabel("Rata protectie per user (Cancelled/Viewed, %)")
    ax.set_ylabel("Numar useri")
    ax.set_title("Distributia Ratei de Protectie\n(BetConfirmation Cancel/Viewed per user)",
                 fontsize=10, fontweight="bold")
    ax.legend(fontsize=8)
    ax.grid(alpha=0.3)

ax = axes[1, 0]
if len(eth_clean) >= 3:
    ax.scatter(eth_clean["ProtectionRate"] * 100, eth_clean["BetPlaced_count"],
               s=80, color=COLORS["Ethical"], alpha=0.85, zorder=3)
    z     = np.polyfit(eth_clean["ProtectionRate"] * 100, eth_clean["BetPlaced_count"], 1)
    xline = np.linspace(0, 100, 100)
    ax.plot(xline, np.polyval(z, xline), color="black",
            linewidth=1.5, linestyle="--", alpha=0.7)
    lbl = (f"Spearman r={r_val:.2f}  p={p_val:.4f}  {sig_corr}"
           if r_val is not None else "Spearman n/a")
    ax.set_xlabel(lbl, fontsize=9)
    ax.set_ylabel("Pariuri plasate (BetPlaced)")
    ax.set_title("Protectie vs Volum Pariuri\n(Ethical users, W1+W2)",
                 fontsize=10, fontweight="bold")
    ax.grid(alpha=0.3)

ax = axes[1, 1]
tool_labels = ["BetConf\nVazut", "BetConf\nAnulat", "Limite\nSetate", "CashOut\nFolosit"]
tool_rates  = summary_tools["Rate_pct"].values
tool_counts = summary_tools["Users"].values
bar_colors  = ["#2ca02c", "#1a7a1a", "#5555cc", "#ff7f0e"]
bars = ax.barh(tool_labels, tool_rates, color=bar_colors, alpha=0.85, edgecolor="white")
for bar, rate, count in zip(bars, tool_rates, tool_counts):
    ax.text(rate + 1, bar.get_y() + bar.get_height() / 2,
            f"{rate:.0f}% ({count}/{eth_total_users})",
            va="center", fontsize=9, fontweight="bold")
ax.set_xlim(0, 130)
ax.set_title(f"Adoptia Instrumentelor de Protectie\n(n={eth_total_users} useri Ethical, W1+W2)",
             fontsize=10, fontweight="bold")
ax.set_xlabel("% utilizatori")
ax.grid(axis="x", alpha=0.3)

plt.suptitle("Instrumente de Protectie Etice — Varianta Ethical\nBetLab Analysis",
             fontsize=13, fontweight="bold")
plt.tight_layout()
plt.savefig(OUT_C / "ethical_protective_tools.png", dpi=300)
plt.close()

print("\nSaved:")
print(f"  {OUTPUT_TABLES / 'autospin_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'profile_limits_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'betconfirmation_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'cashout_per_user.csv'}")
print(f"  {OUTPUT_TABLES / 'protective_tools_adoption.csv'}")
print(f"  {OUT_C / 'ethical_protective_tools.png'}")
