#!/usr/bin/env python3
# results-final/main CSV'lerinden makale figurlerini uretir (vektorel PDF).
# Uc politika, sabit slot sirasi: trend=mavi, popularity=yesil, degree=sari;
# dusuk kontrastli seriler icin ikincil kodlama: cizgi stili + isaretci.
import csv
import glob
import math
import os

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from scipy.stats import t as student_t

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RESULTS = os.path.join(BASE, "results-final", "main")
FIGS = os.environ.get("FIGURE_OUTPUT_DIR", os.path.join(BASE, "paper", "figs"))
os.makedirs(FIGS, exist_ok=True)

SERIES = [
    ("trend", "Trend-aware (proposed)", "#2a78d6", "-", "o"),
    ("popularity", "Popularity-based", "#1baf7a", "--", "s"),
    ("degree", "Degree-based", "#eda100", ":", "^"),
]
RAMP_START, RAMP_PEAK = 2500, 7000


def files(config):
    return sorted(p for p in glob.glob(os.path.join(RESULTS, config + "-s*.csv"))
                  if "-degrees" not in p)


def load(config, col):
    by_step = {}
    for path in files(config):
        with open(path) as f:
            for row in csv.DictReader(f):
                value = float(row[col])
                # holderDegree=-1 is an absence sentinel before the holder joins,
                # not a degree observation to average across seeds.
                if col == "holderDegree" and value < 0:
                    continue
                by_step.setdefault(int(row["step"]), []).append(value)
    steps = sorted(s for s, v in by_step.items() if len(v) >= 8)
    means, cis = [], []
    for s in steps:
        v = by_step[s]
        n = len(v)
        m = sum(v) / n
        var = sum((x - m) ** 2 for x in v) / (n - 1)
        means.append(m)
        cis.append(student_t.ppf(0.975, n - 1) * math.sqrt(var / n))
    return steps, means, cis


def style(ax, ylabel, ramp=True):
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)
    ax.grid(True, axis="y", color="#e6e5e0", linewidth=0.6)
    ax.set_axisbelow(True)
    ax.set_xlabel("Simulation step $t$")
    ax.set_ylabel(ylabel)
    if ramp:
        ax.axvline(RAMP_START, color="#8a8984", linestyle=(0, (2, 3)), linewidth=0.9)
        ax.axvline(RAMP_PEAK, color="#8a8984", linestyle=(0, (2, 3)), linewidth=0.9)


def annotate_ramp(ax, y):
    ax.text(RAMP_START, y, " demand ramp starts", fontsize=7.5,
            color="#5b5a55", ha="left", va="top")
    ax.text(RAMP_PEAK, y, " demand peaks", fontsize=7.5,
            color="#5b5a55", ha="left", va="top")


def plot(col, ylabel, fname, scale=1.0, ylim=None):
    fig, ax = plt.subplots(figsize=(5.4, 3.2), dpi=150)
    for config, label, color, ls, marker in SERIES:
        steps, means, cis = load(config, col)
        means = [m * scale for m in means]
        cis = [c * scale for c in cis]
        ax.plot(steps, means, ls, color=color, linewidth=1.8, marker=marker,
                markersize=3.5, markevery=6, label=label)
        ax.fill_between(steps,
                        [m - c for m, c in zip(means, cis)],
                        [m + c for m, c in zip(means, cis)],
                        color=color, alpha=0.15, linewidth=0)
    style(ax, ylabel)
    if ylim:
        ax.set_ylim(*ylim)
    annotate_ramp(ax, ax.get_ylim()[1] * 0.58)
    ax.legend(frameon=False, fontsize=8, loc="upper left")
    fig.tight_layout()
    out = os.path.join(FIGS, fname)
    fig.savefig(out)
    print("yazildi:", out)


def plot_ccdf():
    """son topolojinin derece CCDF'i (log-log): olcek-serbest kuyruk korunuyor mu.
    Cooper square-root-construct taban cizgisi kuyrugu nasil sildigini gosterir"""
    fig, ax = plt.subplots(figsize=(5.4, 3.2), dpi=150)
    ccdf_series = list(SERIES) + [
        ("cooperm", "Square-root-construct (Cooper)", "#b3489b", "-.", "d")]
    for config, label, color, ls, marker in ccdf_series:
        rdir = RESULTS if config != "cooperm" else os.path.join(BASE, "results-final", "rev")
        counts = {}
        total = 0
        for path in glob.glob(os.path.join(rdir, config + "-s*-degrees.csv")):
            with open(path) as f:
                for row in csv.DictReader(f):
                    d, c = int(row["degree"]), int(row["count"])
                    counts[d] = counts.get(d, 0) + c
                    total += c
        degs = sorted(counts)
        ccdf, acc = [], total
        for d in degs:
            ccdf.append(acc / total)
            acc -= counts[d]
        ax.loglog(degs, ccdf, ls, color=color, linewidth=1.6, marker=marker,
                  markersize=3, markevery=0.12, label=label)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)
    ax.grid(True, which="both", color="#e6e5e0", linewidth=0.5)
    ax.set_axisbelow(True)
    ax.set_xlabel("Degree $d$")
    ax.set_ylabel("$P(D \\geq d)$")
    ax.legend(frameon=False, fontsize=8, loc="lower left")
    fig.tight_layout()
    out = os.path.join(FIGS, "degree_ccdf.pdf")
    fig.savefig(out)
    print("yazildi:", out)


def plot_adaptation():
    """holder'in trend durumu + ag geneli rewire yuku zaman icinde"""
    fig, ax = plt.subplots(figsize=(5.4, 3.2), dpi=150)
    steps, trending, _ = load("trend", "holderTrending")
    _, rwn, rwn_ci = load("trend", "rewireNodes")
    ax2 = ax.twinx()
    ax2.plot(steps, rwn, "-", color="#c9c7c0", linewidth=1.2, zorder=1,
             label="Rewiring nodes per window (right)")
    ax2.fill_between(steps, [m - c for m, c in zip(rwn, rwn_ci)],
                     [m + c for m, c in zip(rwn, rwn_ci)],
                     color="#c9c7c0", alpha=0.3, linewidth=0)
    ax2.set_ylabel("Rewiring nodes per window", color="#7a7871")
    ax2.tick_params(axis="y", labelcolor="#7a7871")
    ax2.set_ylim(0, max(rwn) * 3)
    ax2.spines["top"].set_visible(False)
    ax.plot(steps, trending, "-", color="#2a78d6", linewidth=1.8, zorder=3,
            label="Fraction of seeds with TBP holder in trend mode")
    style(ax, "TBP holder in trend mode (fraction of seeds)")
    ax.set_ylim(-0.03, 1.35)
    annotate_ramp(ax, 1.32)
    h1, l1 = ax.get_legend_handles_labels()
    h2, l2 = ax2.get_legend_handles_labels()
    ax.legend(h1 + h2, l1 + l2, frameon=False, fontsize=8, loc="center right")
    ax.set_zorder(ax2.get_zorder() + 1)
    ax.patch.set_visible(False)
    fig.tight_layout()
    out = os.path.join(FIGS, "adaptation_timeline.pdf")
    fig.savefig(out)
    print("yazildi:", out)


plt.rcParams.update({"font.size": 9, "axes.labelsize": 9,
                     "xtick.labelsize": 8, "ytick.labelsize": 8})

def plot_success_vs_ttl():
    """son penceredeki basarinin TTL butcesine gore egrisi: doygunluk gorseli"""
    fig, ax = plt.subplots(figsize=(5.4, 3.2), dpi=150)
    ttls = [4, 6, 8, 12]
    cols = ["s4", "s6", "s8", "s12"]
    for config, label, color, ls, marker in SERIES:
        means, cis = [], []
        for col in cols:
            vals = []
            for path in files(config):
                with open(path) as f:
                    rows = list(csv.DictReader(f))
                if rows:
                    vals.append(float(rows[-1][col]) * 5.0)
            n = len(vals)
            m = sum(vals) / n
            var = sum((x - m) ** 2 for x in vals) / (n - 1)
            means.append(m)
            cis.append(student_t.ppf(0.975, n - 1) * math.sqrt(var / n))
        ax.errorbar(ttls, means, yerr=cis, fmt=ls, color=color, linewidth=1.8,
                    marker=marker, markersize=4.5, capsize=3, label=label)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)
    ax.grid(True, axis="y", color="#e6e5e0", linewidth=0.6)
    ax.set_axisbelow(True)
    ax.set_xticks(ttls)
    ax.set_xlabel("Probe TTL")
    ax.set_ylabel("TBP discovery success (%)")
    ax.set_ylim(0, 105)
    ax.axvspan(11, 13, color="#efeeea", zorder=0)
    ax.text(12, 55, "saturating\nbudget", fontsize=7.5, color="#5b5a55",
            ha="center")
    ax.legend(frameon=False, fontsize=8, loc="upper left")
    fig.tight_layout()
    out = os.path.join(FIGS, "success_vs_ttl.pdf")
    fig.savefig(out)
    print("yazildi:", out)


def plot_crash():
    """cokme deneyi: basari ve tutucu derecesi zaman icinde, cokme cizgisiyle"""
    import csv as _csv
    crash_dir = os.path.join(BASE, "results-final", "crash")
    by_step_s, by_step_d = {}, {}
    for path in sorted(glob.glob(os.path.join(crash_dir, "crash-s*.csv"))):
        if "-degrees" in path:
            continue
        with open(path) as f:
            for row in _csv.DictReader(f):
                st = int(row["step"])
                by_step_s.setdefault(st, []).append(float(row["s8"]) * 5.0)
                by_step_d.setdefault(st, []).append(max(0.0, float(row["holderDegree"])))
    steps = sorted(s for s, v in by_step_s.items() if len(v) >= 8)
    def mci(dd):
        ms, cs = [], []
        for s in steps:
            v = dd[s]
            n = len(v)
            m = sum(v) / n
            var = sum((x - m) ** 2 for x in v) / (n - 1)
            ms.append(m)
            cs.append(student_t.ppf(0.975, n - 1) * math.sqrt(var / n))
        return ms, cs
    sm, sc = mci(by_step_s)
    dm, dc = mci(by_step_d)
    fig, ax = plt.subplots(figsize=(5.4, 3.2), dpi=150)
    ax2 = ax.twinx()
    ax2.plot(steps, dm, "--", color="#c9a227", linewidth=1.4, label="Holder degree (right)")
    ax2.fill_between(steps, [m - c for m, c in zip(dm, dc)], [m + c for m, c in zip(dm, dc)],
                     color="#c9a227", alpha=0.15, linewidth=0)
    ax2.set_ylabel("Holder degree", color="#8a7118")
    ax2.tick_params(axis="y", labelcolor="#8a7118")
    ax2.spines["top"].set_visible(False)
    ax.plot(steps, sm, "-", color="#2a78d6", linewidth=1.8, label="Discovery success at TTL 8 (left)")
    ax.fill_between(steps, [m - c for m, c in zip(sm, sc)], [m + c for m, c in zip(sm, sc)],
                    color="#2a78d6", alpha=0.15, linewidth=0)
    style(ax, "TBP discovery success at TTL 8 (%)", ramp=True)
    ax.axvline(9000, color="#c0392b", linestyle="-", linewidth=1.1)
    ax.text(9000, 12, " holder departs\n copy reenters", fontsize=7.5, color="#c0392b", ha="left")
    ax.set_ylim(0, 118)
    h1, l1 = ax.get_legend_handles_labels()
    h2, l2 = ax2.get_legend_handles_labels()
    ax.legend(h1 + h2, l1 + l2, frameon=False, fontsize=8, loc="upper left")
    ax.set_zorder(ax2.get_zorder() + 1)
    ax.patch.set_visible(False)
    fig.tight_layout()
    out = os.path.join(FIGS, "crash_recovery.pdf")
    fig.savefig(out)
    print("yazildi:", out)


plot("holderDegree", "TBP holder degree", "holder_degree.pdf")
plot("s8", "TBP discovery success at TTL 8 (%)", "success_ttl8.pdf",
     scale=5.0, ylim=(0, 105))
plot_ccdf()
plot_adaptation()
plot_success_vs_ttl()
plot_crash()
