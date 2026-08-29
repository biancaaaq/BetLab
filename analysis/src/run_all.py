# -*- coding: utf-8 -*-
"""Ruleaza toate scripturile in ordine. Utilizare: python -X utf8 run_all.py [--from N] [--only N1 N2 ...]"""

import subprocess
import sys
import time
from pathlib import Path

SRC = Path(__file__).parent

SCRIPTS = [
    # ---- Week 1 — overview + gambling ----
    ("01", "01_week1_overview.py"),
    ("02", "02_week1_gambling.py"),
    # ---- Week 1 — analiza avansata ----
    ("03", "03_week1_funnel_analysis.py"),
    ("04", "04_week1_betconfirmation_effect.py"),
    ("05", "05_week1_retention_analysis.py"),
    ("06", "06_week1_dark_pattern_effects.py"),
    ("07", "07_week1_markov_transitions.py"),
    ("08", "08_week1_clustering.py"),
    # ---- Week 2 ----
    ("09", "09_week2_metrics_and_tests.py"),
    ("10", "10_week2_funnel_and_confirmation.py"),
    ("11", "11_week2_retention_dark_patterns.py"),
    ("12", "12_week2_markov_clustering.py"),
    # ---- Crossover ----
    ("13", "13_crossover_merge_and_paired_tests.py"),
    ("14", "14_crossover_period_carryover.py"),
    # ---- Analiza financiara + timp ----
    ("15", "15_stake_analysis.py"),
    ("16", "16_time_to_first_bet.py"),
    # ---- Order effects ----
    ("17", "17_crossover_order_effects.py"),
    # ---- Instrumente protective + rigoare statistica ----
    ("18", "18_autospin_protective_tools.py"),
    ("19", "19_statistical_rigor.py"),
]

def run_script(num, name):
    path = SRC / name
    if not path.exists():
        print(f"  [SKIP] {name} — fisier inexistent")
        return True

    print(f"\n{'='*60}")
    print(f"  [{num}] {name}")
    print(f"{'='*60}")
    t0 = time.time()

    result = subprocess.run(
        [sys.executable, "-X", "utf8", str(path)],
        cwd=str(SRC),
        capture_output=False
    )
    elapsed = time.time() - t0

    if result.returncode != 0:
        print(f"\n  [EROARE] {name} — cod {result.returncode} ({elapsed:.1f}s)")
        return False
    else:
        print(f"\n  [OK] {name} — {elapsed:.1f}s")
        return True

def main():
    args = sys.argv[1:]

    # --only X Y Z
    if "--only" in args:
        idx = args.index("--only")
        only_nums = set(args[idx + 1:])
        to_run = [(n, s) for n, s in SCRIPTS if n in only_nums]

    # --from X
    elif "--from" in args:
        idx  = args.index("--from")
        from_num = int(args[idx + 1])
        to_run = [(n, s) for n, s in SCRIPTS if int(n) >= from_num]

    else:
        to_run = SCRIPTS

    print(f"\nBetLab Analysis — run_all.py")
    print(f"Scripturi de rulat: {len(to_run)}")
    print(f"Director: {SRC}\n")

    t_total = time.time()
    errors  = []

    for num, name in to_run:
        ok = run_script(num, name)
        if not ok:
            errors.append(name)

    elapsed_total = time.time() - t_total
    print(f"\n{'='*60}")
    print(f"FINAL: {len(to_run) - len(errors)}/{len(to_run)} scripturi OK")
    print(f"Timp total: {elapsed_total:.1f}s ({elapsed_total/60:.1f} min)")
    if errors:
        print(f"\nERRORI in:")
        for e in errors:
            print(f"  - {e}")
    else:
        print("Toate scripturile au rulat cu succes.")
    print(f"{'='*60}\n")

if __name__ == "__main__":
    main()
