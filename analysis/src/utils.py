# -*- coding: utf-8 -*-
"""Constante si functii comune: culori, grupuri de evenimente, parse_meta, cliffs_delta, sig_label."""

import json
import numpy as np
from scipy.stats import mannwhitneyu as _mwu

# Culori Dark / Ethical
COLORS = {"Dark": "#d62728", "Ethical": "#2ca02c"}

# Grupuri de evenimente
BETTING_EV = [
    "OutcomeSelected", "StakeChanged", "BetAttempted", "BetPlaced"
]

CASINO_EV = [
    "CasinoSpinClicked", "CasinoRoundCompleted", "RouletteSpinPlaced",
    "BlackjackStart", "BlackjackRoundCompleted", "WheelSpin", "CasinoStakeChanged"
]

FINANCIAL_EV = [
    "DepositStarted", "DepositCompleted", "CashOut"
]

DARK_EV = [
    "NearMissShown", "OutcomeRemoved"
]


# Functii

def parse_meta(s):
    """Parseaza MetadataJson robust; returneaza {} la orice eroare."""
    if not isinstance(s, str) or not s.strip():
        return {}
    s = s.strip()
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1].replace('""', '"')
    try:
        return json.loads(s)
    except Exception:
        return {}


def cliffs_delta(x, y):
    """
    Cliff's Delta (effect size non-parametric) prin Mann-Whitney U.
    Valori: -1 (Ethical > Dark complet) ... +1 (Dark > Ethical complet).
    """
    xa, ya = np.asarray(x, dtype=float), np.asarray(y, dtype=float)
    u, _ = _mwu(xa, ya, alternative="two-sided")
    return (2 * u / (len(xa) * len(ya))) - 1


def sig_label(p):
    """Eticheta de semnificatie statistica."""
    return "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "n.s."
