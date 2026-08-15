#!/usr/bin/env python3
# results-final/ altindaki tum kosu gruplarini toplar: ana karsilastirma,
# ablasyon, duyarlilik, trend gucu (qmax) ve olcek deneyleri icin ortalama
# ve %95 guven araligi hesaplar; hem okunur ozet hem LaTeX tablolari uretir.
import csv
import glob
import math
import os
import sys

from scipy.stats import t as student_t

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RESULTS = os.path.join(BASE, "results-final")
PAPER = os.environ.get("PAPER_OUTPUT_DIR", os.path.join(BASE, "paper"))
SUMMARY_PATH = os.environ.get(
    "SUMMARY_OUTPUT_PATH", os.path.join(RESULTS, "summary.txt")
)

MAIN_CONFIGS = ["degree", "popularity", "trend"]
MAIN_METRICS = [
    ("holderDegree", "TBP holder degree"),
    ("s6", "TBP success @TTL=6 (of 20)"),
    ("s8", "TBP success @TTL=8 (of 20)"),
    ("s12", "TBP success @TTL=12 (of 20)"),
    ("v8", "Visited nodes @TTL=8"),
    ("bgSucc", "Background success @TTL=8 (of 20)"),
    ("bgHits", "Background hits (total)"),
    ("bgVis", "Background visited @TTL=8"),
    ("edges", "Total overlay links"),
]


def mean_ci(values):
    n = len(values)
    if n == 0:
        return float("nan"), 0.0
    m = sum(values) / n
    if n < 2:
        return m, 0.0
    var = sum((x - m) ** 2 for x in values) / (n - 1)
    critical = student_t.ppf(0.975, n - 1)
    return m, critical * math.sqrt(var / n)


def read_rows(path):
    with open(path) as f:
        return list(csv.DictReader(f))


def seed_files(subdir, config):
    pat = os.path.join(RESULTS, subdir, config + "-s*.csv")
    return sorted(p for p in glob.glob(pat) if "-degrees" not in p)


def collect(subdir, config, at_step=None):
    """seed -> row (son pencere ya da at_step'e en yakin pencere)"""
    out = {}
    for path in seed_files(subdir, config):
        seed = path.split("-s")[-1].split(".")[0]
        rows = read_rows(path)
        if not rows:
            continue
        if at_step is None:
            out[seed] = rows[-1]
        else:
            out[seed] = min(rows, key=lambda r: abs(int(r["step"]) - at_step))
    return out


def metric_stats(subdir, configs, cols, at_step=None):
    """col -> config -> (mean, ci, n)"""
    table = {}
    for config in configs:
        rows = collect(subdir, config, at_step)
        for col in cols:
            vals = [float(r[col]) for r in rows.values()]
            m, ci = mean_ci(vals)
            table.setdefault(col, {})[config] = (m, ci, len(vals))
    return table


def detection_times(subdir, config):
    """tohum basina tespit ani: holderTrending kolonunun ilk 1 oldugu adim
    (tespit hic olmadiysa None)"""
    out = {}
    for path in seed_files(subdir, config):
        seed = path.split("-s")[-1].split(".")[0]
        t = None
        for r in read_rows(path):
            if r.get("holderTrending") == "1":
                t = int(r["step"])
                break
        out[seed] = t
    return out


def rewire_load(subdir, config):
    """pencere basina ortalama rewire dugumu ve join'i (tum tohumlar)"""
    nodes, joins = [], []
    for path in seed_files(subdir, config):
        rows = read_rows(path)
        if not rows:
            continue
        nodes.append(sum(float(r["rewireNodes"]) for r in rows) / len(rows))
        joins.append(sum(float(r["rewireJoins"]) for r in rows) / len(rows))
    return mean_ci(nodes), mean_ci(joins)


def fmt(m, ci, nd=1):
    return "$%.*f \\pm %.*f$" % (nd, m, nd, ci)


def latex_main_table(table, caption, label):
    lines = [
        "\\begin{table*}[t]", "\\centering",
        "\\caption{%s}" % caption, "\\label{%s}" % label,
        "\\resizebox{\\textwidth}{!}{%",
        "\\begin{tabular}{lccc}", "\\toprule",
        "Metric & Degree-based & Popularity-based~\\cite{gunduz2016popularity} & Trend-aware (proposed) \\\\",
        "\\midrule",
    ]
    pretty = dict(MAIN_METRICS)
    for col, _ in MAIN_METRICS:
        cells = []
        for config in MAIN_CONFIGS:
            m, ci, n = table[col][config]
            nd = 0 if col == "edges" else 1
            cells.append(fmt(m, ci, nd))
        lines.append("%s & %s \\\\" % (pretty[col], " & ".join(cells)))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table*}"]
    return "\n".join(lines)


def summarize_main():
    parts = []
    tables = {}
    for at_step, label in [(None, "final"), (11000, "mid")]:
        table = metric_stats("main", MAIN_CONFIGS,
                             [c for c, _ in MAIN_METRICS], at_step)
        tables[label] = table
        parts.append("=== main @ %s ===" % (label,))
        for col, name in MAIN_METRICS:
            row = "  %-32s" % name
            for config in MAIN_CONFIGS:
                m, ci, n = table[col][config]
                row += "  %s: %8.1f +/- %5.1f (n=%d)" % (config[:3], m, ci, n)
            parts.append(row)
    det = detection_times("main", "trend")
    parts.append("  trend detection times: %s" % det)
    (rwn_m, rwn_ci), (rwj_m, rwj_ci) = rewire_load("main", "trend")
    parts.append("  rewire load per window: nodes %.2f+/-%.2f, joins %.2f+/-%.2f"
                 % (rwn_m, rwn_ci, rwj_m, rwj_ci))
    return "\n".join(parts), tables, det, (rwn_m, rwn_ci, rwj_m, rwj_ci)


ABL_CONFIGS = [
    ("popularity", "main", "Baseline (no trend layer)"),
    ("weightonly", "ablation", "Attachment weight only ($P_t$, no rewiring)"),
    ("rewireonly", "ablation", "Rewiring only ($P_t{=}0$)"),
    ("trend", "main", "Full mechanism (proposed)"),
]


def summarize_ablation():
    parts = ["=== ablation @ final ==="]
    rows_out = []
    for config, subdir, label in ABL_CONFIGS:
        table = metric_stats(subdir, [config], ["holderDegree", "s8", "s12", "edges"])
        det = detection_times(subdir, config)
        det_vals = [t for t in det.values() if t is not None]
        det_n = len(det_vals)
        det_m, det_ci = mean_ci(det_vals) if det_vals else (float("nan"), 0)
        hd = table["holderDegree"][config]
        s8 = table["s8"][config]
        s12 = table["s12"][config]
        rows_out.append((label, hd, s8, s12, det_n, len(det), det_m, det_ci))
        parts.append("  %-45s deg=%6.1f+/-%5.1f  s8=%5.1f+/-%4.1f  s12=%5.1f+/-%4.1f  detected=%d/%d (t=%.0f+/-%.0f)"
                     % (label, hd[0], hd[1], s8[0], s8[1], s12[0], s12[1],
                        det_n, len(det), det_m, det_ci))
    lines = [
        "\\begin{table}[t]", "\\centering",
        "\\caption{Ablation of the two adaptation paths, mean $\\pm$ 95\\% CI over 20 seeds at the final window. Detection column: seeds in which the holder entered trend mode; the detector runs under every policy for measurement purposes, but only variants with rewiring or a nonzero $P_t$ act on its output.}",
        "\\label{tab:ablation}",
        "\\resizebox{\\columnwidth}{!}{%",
        "\\begin{tabular}{lcccc}", "\\toprule",
        "Variant & Holder degree & Success @TTL=8 & Success @TTL=12 & Detected \\\\",
        "\\midrule",
    ]
    for label, hd, s8, s12, det_n, det_tot, det_m, det_ci in rows_out:
        det_cell = "%d/%d" % (det_n, det_tot)
        lines.append("%s & %s & %s & %s & %s \\\\"
                     % (label, fmt(*hd[:2]), fmt(*s8[:2]), fmt(*s12[:2]), det_cell))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table}"]
    return "\n".join(parts), "\n".join(lines)


SWEEP_VARIANTS = [
    ("k4", "$k=4$"), ("k6", "$k=6$"),
    ("c1", "$c=1$"), ("c3", "$c=3$"),
    ("eps001", "$\\varepsilon=0.01$"), ("eps004", "$\\varepsilon=0.04$"),
    ("th3", "$\\Theta=3$"), ("th12", "$\\Theta=12$"),
    ("pt5000", "$P_t=5000$"), ("pt100000", "$P_t=100{,}000$"),
]


def summarize_sweep(default_row, weak_default):
    """her varyant iki is yukunde: guclu trend (qmax=0.2) ve sinir bolgesi (qmax=0.1)"""
    parts = ["=== sweep @ final (10 seeds; strong qmax=0.2 + weak qmax=0.1) ==="]
    lines = [
        "\\begin{table*}[t]", "\\centering",
        "\\caption{Sensitivity of the proposed policy around the default configuration ($k{=}5$, $c{=}2$, $\\varepsilon{=}0.02$, $\\Theta{=}6$, $P_t{=}25{,}000$); each row varies one parameter and uses ten seeds per workload. The strong-trend columns use the default $q_{\\max}{=}0.2$; the weak-trend columns repeat every variant at $q_{\\max}{=}0.1$, a weaker but still reliably detectable trend; the stochastic transition region sits at $q_{\\max}{=}0.05$, per Table~\\ref{tab:qmax}. Rewiring load is the network-wide mean number of rewiring nodes per window at $q_{\\max}{=}0.2$; the default row repeats the main article's twenty-seed result and the ten-seed $q_{\\max}{=}0.1$ row of Table~\\ref{tab:qmax}.}",
        "\\label{tab:sweep}",
        "\\resizebox{\\textwidth}{!}{%",
        "\\begin{tabular}{lcccc}", "\\toprule",
        "Variant & Success ($q_{\\max}{=}0.2$) & Rewiring nodes/window & Detected ($q_{\\max}{=}0.1$) & Success ($q_{\\max}{=}0.1$) \\\\",
        "\\midrule",
    ]
    lines.append("default & %s & %s & %s & %s \\\\" % (default_row[0], default_row[2], weak_default[0], weak_default[1]))
    for config, label in SWEEP_VARIANTS:
        table = metric_stats("sweep", [config], ["holderDegree", "s8"])
        (rwn_m, rwn_ci), _ = rewire_load("sweep", config)
        s8 = table["s8"][config]
        wtable = metric_stats("sweepweak", [config], ["s8"])
        ws8 = wtable["s8"][config]
        wdet = detection_times("sweepweak", config)
        wdet_n = len([t for t in wdet.values() if t is not None])
        parts.append("  %-22s s8=%5.1f+/-%4.1f rwN=%5.2f | weak: det=%d/%d s8=%5.1f+/-%4.1f"
                     % (label, s8[0], s8[1], rwn_m, wdet_n, len(wdet), ws8[0], ws8[1]))
        lines.append("%s & %s & %s & %d/%d & %s \\\\"
                     % (label, fmt(*s8[:2]), fmt(rwn_m, rwn_ci, 2), wdet_n, len(wdet), fmt(*ws8[:2])))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table*}"]
    return "\n".join(parts), "\n".join(lines)


QMAX_ROWS = [
    ("qmax002", "qmax", "0.02"),
    ("qmax005", "qmax", "0.05"),
    ("qmax010", "qmax", "0.10"),
    ("trend", "main", "0.20 (default)"),
]


def summarize_qmax():
    parts = ["=== qmax (trend strength) @ final ==="]
    lines = [
        "\\begin{table}[t]", "\\centering",
        "\\caption{Detectability versus trend strength: peak demand share $q_{\\max}$ of the TBP item. Detection column: seeds in which the holder entered trend mode; detection time is the mean over detected seeds (demand ramp spans $t{=}2{,}500$ to $t{=}7{,}000$). The $q_{\\max}{=}0.20$ row is the twenty-seed default; other rows use ten seeds.}",
        "\\label{tab:qmax}",
        "\\resizebox{\\columnwidth}{!}{%",
        "\\begin{tabular}{lcccc}", "\\toprule",
        "$q_{\\max}$ & Detected & Detection time & Holder degree & Success @TTL=8 \\\\",
        "\\midrule",
    ]
    for config, subdir, label in QMAX_ROWS:
        table = metric_stats(subdir, [config], ["holderDegree", "s8"])
        det = detection_times(subdir, config)
        det_vals = [t for t in det.values() if t is not None]
        hd = table["holderDegree"][config]
        s8 = table["s8"][config]
        if det_vals:
            dm, dci = mean_ci(det_vals)
            det_time = "$%d \\pm %d$" % (round(dm), round(dci))
        else:
            det_time = "--"
        parts.append("  qmax=%-14s detected=%d/%d t=%s deg=%6.1f+/-%5.1f s8=%5.1f+/-%4.1f"
                     % (label, len(det_vals), len(det), det_time, hd[0], hd[1], s8[0], s8[1]))
        lines.append("%s & %d/%d & %s & %s & %s \\\\"
                     % (label, len(det_vals), len(det), det_time,
                        fmt(*hd[:2]), fmt(*s8[:2])))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table}"]
    return "\n".join(parts), "\n".join(lines)


SCALE_SIZES = ["5000", "15000", "20000"]


def edge_overhead(sub_t, cfg_t, sub_p, cfg_p):
    """tohum bazinda (trend - pop)/pop kenar fazlasi yuzdesi"""
    te = [float(read_rows(p)[-1]["edges"]) for p in seed_files(sub_t, cfg_t)]
    pe = [float(read_rows(p)[-1]["edges"]) for p in seed_files(sub_p, cfg_p)]
    n = min(len(te), len(pe))
    ov = [100.0 * (te[i] - pe[i]) / pe[i] for i in range(n)]
    return mean_ci(ov)


def summarize_scale(main_tables):
    parts = ["=== scale @ final ==="]
    lines = [
        "\\begin{table}[t]", "\\centering",
        "\\caption{Scaling behavior: final-window TBP discovery at TTL~8 as the network grows, with the demand scenario scaled proportionally. The 10{,}000-node column repeats the twenty-seed main result; other sizes use ten seeds. Visited counts show that the TTL~8 probe covers a shrinking fraction of larger networks, which lowers any fixed-budget success; the trend-aware policy sustains discovery where the baseline collapses. Link overhead is the seed-paired excess of the trend-aware overlay's final links over the popularity baseline's, and grows with network size because the network-wide false-alarm population scales with $N$ at a constant per-node rate (Section~\\ref{sec:sensitivity}).}",
        "\\label{tab:scale}",
        "\\resizebox{\\columnwidth}{!}{%",
        "\\begin{tabular}{lcccc}", "\\toprule",
        "Network size & 5{,}000 & 10{,}000 & 15{,}000 & 20{,}000 \\\\",
        "\\midrule",
    ]
    rows = {"pop_s8": [], "tr_s8": [], "tr_deg": [], "cov": [], "ov": [], "rwn": []}
    for n in ["5000", "10000", "15000", "20000"]:
        if n == "10000":
            pop_s8 = main_tables["final"]["s8"]["popularity"]
            tr_s8 = main_tables["final"]["s8"]["trend"]
            tr_deg = main_tables["final"]["holderDegree"]["trend"]
            v8 = main_tables["final"]["v8"]["trend"]
            ov = edge_overhead("main", "trend", "main", "popularity")
            (rwn_m, rwn_ci), _ = rewire_load("main", "trend")
        else:
            t1 = metric_stats("scale", ["popularity-n%s" % n], ["s8"])
            t2 = metric_stats("scale", ["trend-n%s" % n], ["s8", "holderDegree", "v8"])
            pop_s8 = t1["s8"]["popularity-n%s" % n]
            tr_s8 = t2["s8"]["trend-n%s" % n]
            tr_deg = t2["holderDegree"]["trend-n%s" % n]
            v8 = t2["v8"]["trend-n%s" % n]
            ov = edge_overhead("scale", "trend-n%s" % n, "scale", "popularity-n%s" % n)
            (rwn_m, rwn_ci), _ = rewire_load("scale", "trend-n%s" % n)
        cov = 100.0 * v8[0] / float(n)
        rows["pop_s8"].append(fmt(*pop_s8[:2]))
        rows["tr_s8"].append(fmt(*tr_s8[:2]))
        rows["tr_deg"].append(fmt(*tr_deg[:2]))
        rows["cov"].append("%.0f\\%%" % cov)
        rows["ov"].append("$%.1f\\%% \\pm %.1f$" % (ov[0], ov[1]))
        rows["rwn"].append(fmt(rwn_m, rwn_ci, 1))
        parts.append("  N=%-6s pop_s8=%5.1f+/-%4.1f trend_s8=%5.1f+/-%4.1f deg=%6.1f coverage=%.0f%% overhead=%.2f%%+/-%.2f rwN=%.2f"
                     % (n, pop_s8[0], pop_s8[1], tr_s8[0], tr_s8[1], tr_deg[0], cov, ov[0], ov[1], rwn_m))
    lines.append("Popularity-based success & %s \\\\" % " & ".join(rows["pop_s8"]))
    lines.append("Trend-aware success & %s \\\\" % " & ".join(rows["tr_s8"]))
    lines.append("Trend-aware holder degree & %s \\\\" % " & ".join(rows["tr_deg"]))
    lines.append("Probe coverage at TTL 8 & %s \\\\" % " & ".join(rows["cov"]))
    lines.append("Adaptation link overhead & %s \\\\" % " & ".join(rows["ov"]))
    lines.append("Rewiring nodes/window & %s \\\\" % " & ".join(rows["rwn"]))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table}"]
    # sure-sabit 20K kontrolu: boyut etkisini sure etkisinden ayirir
    try:
        ovf = edge_overhead("rev", "trendfx-n20000", "scale", "popularity-n20000")
        t3 = metric_stats("rev", ["trendfx-n20000"], ["s8", "holderDegree"])
        (rwf_m, rwf_ci), _ = rewire_load("rev", "trendfx-n20000")
        fx = dict(ov=ovf, s8=t3["s8"]["trendfx-n20000"],
                  deg=t3["holderDegree"]["trendfx-n20000"], rwn=(rwf_m, rwf_ci))
        parts.append("  fixed-duration 20K control: overhead=%.2f%%+/-%.2f s8=%.1f deg=%.0f rwN=%.2f"
                     % (ovf[0], ovf[1], fx["s8"][0], fx["deg"][0], rwf_m))
    except Exception:
        fx = None
    return "\n".join(parts), "\n".join(lines), fx


def wilson_lower(successes, total, z=1.96):
    """binom orani icin Wilson %95 alt siniri"""
    if total == 0:
        return float("nan")
    p = successes / total
    denom = 1 + z * z / total
    center = p + z * z / (2 * total)
    margin = z * math.sqrt(p * (1 - p) / total + z * z / (4 * total * total))
    return (center - margin) / denom


def pooled_wilson(subdir, config, col, per=20):
    rows = collect(subdir, config)
    succ = sum(float(r[col]) for r in rows.values())
    total = per * len(rows)
    return succ, total, wilson_lower(succ, total)


CTRL_CONFIGS = [
    ("randomrw4", "rev", "Random link acquisition, matched budget"),
    ("reactive", "rev", "Requester shortcuts, TBP-scoped, no budget cap"),
    ("reactive2", "rev", "Requester shortcuts, all content, matched budget"),
    ("fixed005c", "rev", "Fixed threshold $\\theta_{\\mathrm{fix}}=0.05$"),
    ("fixed015c", "rev", "Fixed threshold $\\theta_{\\mathrm{fix}}=0.15$"),
    ("oracle", "controls", "Oracle entry at $T_s$ ($P_t{=}0$)"),
]


def det_summary(subdir, config):
    det = detection_times(subdir, config)
    det_vals = [t for t in det.values() if t is not None]
    dm, dci = mean_ci(det_vals) if det_vals else (float("nan"), 0)
    return det_vals, len(det), dm, dci


def summarize_controls():
    parts = ["=== controls @ final (10 seeds; fixed rows run k-sigma path only, level rule disabled) ==="]
    lines = [
        "\\begin{table*}[t]", "\\centering",
        "\\caption{Detector control baselines, mean $\\pm$ 95\\% CI over 10 seeds at the final window. Random link acquisition grants a matched join budget to randomly chosen nodes (four nodes with two joins per window, eight joins against the proposed policy's measured $7.9 \\pm 0.5$). The all-content requester-shortcut quota is consumed by each successful request even when its edge already exists, yielding 7.82 effective additions per window. The fixed-threshold variants replace the self-calibrating limit of Eq.~\\ref{eq:rule1} with a single absolute threshold and disable the CUSUM and level rules, so that the fixed threshold alone decides entry. Detection time is the mean over detected seeds; the demand peak is at $t{=}7{,}000$. The oracle forces the holder into adaptation mode from the start of the demand ramp, bounding what any detector could achieve with $P_t{=}0$. Rewiring-only and full-mechanism rows repeat Table~\\ref{tab:ablation} for reference.}",
        "\\label{tab:controls}",
        "\\resizebox{\\textwidth}{!}{%",
        "\\begin{tabular}{lcccccc}", "\\toprule",
        "Variant & Holder degree & Success @TTL=6 & Success @TTL=8 & Detected & Detection time & Rewiring nodes/window \\\\",
        "\\midrule",
    ]
    for config, subdir, label in CTRL_CONFIGS:
        table = metric_stats(subdir, [config], ["holderDegree", "s6", "s8"])
        det_vals, det_tot, dm, dci = det_summary(subdir, config)
        (rwn_m, rwn_ci), _ = rewire_load(subdir, config)
        hd = table["holderDegree"][config]
        s6 = table["s6"][config]
        s8 = table["s8"][config]
        det_cell = "forced" if config == "oracle" else "%d/%d" % (len(det_vals), det_tot)
        t_cell = "--" if config in ("oracle", "randomrw4") else "$%d \\pm %d$" % (round(dm), round(dci))
        parts.append("  %-42s deg=%6.1f+/-%5.1f s8=%5.1f+/-%4.1f det=%s t=%.0f+/-%.0f rwN=%5.2f"
                     % (label, hd[0], hd[1], s8[0], s8[1], det_cell, dm, dci, rwn_m))
        lines.append("%s & %s & %s & %s & %s & %s & %s \\\\"
                     % (label, fmt(*hd[:2]), fmt(*s6[:2]), fmt(*s8[:2]),
                        det_cell, t_cell, fmt(rwn_m, rwn_ci, 2)))
    for config, subdir, label in [("rewireonly", "ablation", "Rewiring only (proposed detector, $P_t{=}0$)"),
                                   ("trend", "main", "Full mechanism (proposed)")]:
        table = metric_stats(subdir, [config], ["holderDegree", "s6", "s8"])
        det_vals, det_tot, dm, dci = det_summary(subdir, config)
        (rwn_m, rwn_ci), _ = rewire_load(subdir, config)
        hd = table["holderDegree"][config]
        s6 = table["s6"][config]
        s8 = table["s8"][config]
        lines.append("%s & %s & %s & %s & %d/%d & $%d \\pm %d$ & %s \\\\"
                     % (label, fmt(*hd[:2]), fmt(*s6[:2]), fmt(*s8[:2]), len(det_vals), det_tot,
                        round(dm), round(dci), fmt(rwn_m, rwn_ci, 2)))
        parts.append("  %-42s deg=%6.1f+/-%5.1f s8=%5.1f+/-%4.1f det=%d/%d t=%.0f+/-%.0f rwN=%5.2f"
                     % (label, hd[0], hd[1], s8[0], s8[1], len(det_vals), det_tot, dm, dci, rwn_m))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table*}"]
    return "\n".join(parts), "\n".join(lines)


RULE_CONFIGS = [
    ("rulek", "rev", "$k$-sigma rule only"),
    ("rulec", "rev", "CUSUM only"),
    ("rulel", "rev", "Level rule only"),
    ("trend", "main", "All three rules (proposed)"),
]


def summarize_rules():
    parts = ["=== detector rule ablation (10 seeds; full mechanism runs 20) ==="]
    lines = [
        "\\begin{table}[t]", "\\centering",
        "\\caption{Detector entry-rule ablation at the default workload ($q_{\\max}{=}0.2$), mean $\\pm$ 95\\% CI at the final window; each single-rule variant disables the other two entry rules and uses ten seeds, while the combined row repeats the twenty-seed default. Detection time is the mean over detected seeds. At TTL~8 the level-only and combined rows are both at ceiling; TTL~6 and the weak-trend and holder-departure diagnostics in the text expose the complementary regimes. The combined detector also succeeds in each of the first ten seeds used by the single-rule variants.}",
        "\\label{tab:rules}",
        "\\resizebox{\\columnwidth}{!}{%",
        "\\begin{tabular}{lccccc}", "\\toprule",
        "Entry rules & Detected & Detection time & Holder degree & Success @TTL=6 & Success @TTL=8 \\\\",
        "\\midrule",
    ]
    for config, subdir, label in RULE_CONFIGS:
        table = metric_stats(subdir, [config], ["holderDegree", "s6", "s8"])
        det_vals, det_tot, dm, dci = det_summary(subdir, config)
        hd = table["holderDegree"][config]
        s6 = table["s6"][config]
        s8 = table["s8"][config]
        parts.append("  %-28s det=%d/%d t=%.0f+/-%.0f deg=%6.1f+/-%5.1f s8=%5.1f+/-%4.1f"
                     % (label, len(det_vals), det_tot, dm, dci, hd[0], hd[1], s8[0], s8[1]))
        lines.append("%s & %d/%d & $%d \\pm %d$ & %s & %s & %s \\\\"
                     % (label, len(det_vals), det_tot, round(dm), round(dci),
                        fmt(*hd[:2]), fmt(*s6[:2]), fmt(*s8[:2])))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table}"]
    return "\n".join(parts), "\n".join(lines)


def entry_attribution(subdir, config):
    """tum tohumlarda tutucunun giris kurali dagilimi (1 k-sigma, 2 CUSUM, 3 seviye)"""
    counts = {1: 0, 2: 0, 3: 0}
    for path in seed_files(subdir, config):
        rows = read_rows(path)
        er = next((int(r["holderEntryRule"]) for r in rows
                   if r.get("holderTrending") == "1" and "holderEntryRule" in r), 0)
        if er in counts:
            counts[er] += 1
    return counts


def summarize_cooper(main_tables):
    parts = ["=== Cooper square-root-construct baseline (dmax=113 budget-matched, 10 seeds) ==="]
    lines = [
        "\\begin{table*}[t]", "\\centering",
        "\\caption{Cooper's square-root-construct~\\cite{cooper2005square} transplanted onto the identical growth substrate and workload, with $d_{\\max}{=}113$ calibrated for the $\\mu{=}1$ variant so that its final overlay matches the popularity baseline's total links (the $\\mu{=}0.95$ overlay ends about 1.3\\% smaller); $\\mu$ is his optional decay factor for changing popularities. Mean $\\pm$ 95\\% CI over 10 seeds at the final window; the proposed-policy row repeats the twenty-seed main result. Hits per visited node measures per-message search efficiency for popularity-weighted background probes. Link additions count successful adds only; the construction's link removals are not logged.}",
        "\\label{tab:cooper}",
        "\\resizebox{\\textwidth}{!}{%",
        "\\begin{tabular}{lcccccc}", "\\toprule",
        "Policy & TBP success @TTL=6 & TBP success @TTL=8 & Visited @TTL=8 & Bg hits per visited node & Successful link additions/window (drops not logged) & Max degree \\\\",
        "\\midrule",
    ]
    rows_spec = [("cooperm", "rev", "Square-root-construct, $\\mu{=}1$"),
                 ("cooperd", "rev", "Square-root-construct, $\\mu{=}0.95$"),
                 ("trend", "main", "Trend-aware (proposed)")]
    for config, subdir, label in rows_spec:
        table = metric_stats(subdir, [config], ["holderDegree", "s6", "s8", "v8", "bgHits", "bgVis", "edges"])
        _, rwj = rewire_load(subdir, config)
        s6 = table["s6"][config]
        s8 = table["s8"][config]
        v8 = table["v8"][config]
        eff = table["bgHits"][config][0] / table["bgVis"][config][0]
        # havuzlanmis maksimum derece (yapi kuyrugu)
        mx = []
        for path in sorted(glob.glob(os.path.join(RESULTS, subdir, config + "-s*-degrees.csv"))):
            h = read_rows(path)
            mx.append(max(int(x["degree"]) for x in h))
        mxm, mxci = mean_ci(mx)
        parts.append("  %-36s s8=%5.1f+/-%4.1f v8=%6.0f eff=%.3f joins/win=%8.1f maxdeg=%5.0f edges=%6.0f deg=%5.1f"
                     % (label, s8[0], s8[1], v8[0], eff, rwj[0], mxm,
                        table["edges"][config][0], table["holderDegree"][config][0]))
        lines.append("%s & %s & %s & %s & %.3f & %s & %s \\\\"
                     % (label, fmt(*s6[:2]), fmt(*s8[:2]), fmt(v8[0], v8[1], 0), eff,
                        fmt(rwj[0], rwj[1], 1), fmt(mxm, mxci, 0)))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table*}"]
    return "\n".join(parts), "\n".join(lines)


def summarize_null():
    """trend icermeyen is yuku: saf yanlis-alarm orani (canli dugum-pencere
    paydasiyla: canli = maxId + 1 - removed), kural atfi, link maliyeti"""
    parts = ["=== null workload (no TBP item, 10 seeds) ==="]
    ent_k = ent_c = ent_l = 0
    windows = 0
    node_windows = 0
    sub_e = sub_nw = sub_rows = 0
    max_win = 0
    rwn, edges = [], []
    for path in seed_files("rev", "nullwl"):
        rows = read_rows(path)
        windows += len(rows)
        for r in rows:
            alive = int(r["maxId"]) + 1 - int(r["removed"])
            e = int(r["entK"]) + int(r["entC"]) + int(r["entL"])
            node_windows += alive
            max_win = max(max_win, e)
            if alive >= 9000:
                sub_e += e
                sub_nw += alive
                sub_rows += 1
        ent_k += sum(int(r["entK"]) for r in rows)
        ent_c += sum(int(r["entC"]) for r in rows)
        ent_l += sum(int(r["entL"]) for r in rows)
        rwn.append(sum(float(r["rewireNodes"]) for r in rows) / len(rows))
        edges.append(float(rows[-1]["edges"]))
    total = ent_k + ent_c + ent_l
    total_rate = total / windows
    rn_m, rn_ci = mean_ci(rwn)
    ed_m, ed_ci = mean_ci(edges)
    parts.append("  false entries: %d over %d active node-windows -> rate %.2e; N>=9000 subset: %d/%d = %.2e (mean alive %.0f)"
                 % (total, node_windows, total / node_windows,
                    sub_e, sub_nw, sub_e / sub_nw if sub_nw else float("nan"),
                    sub_nw / sub_rows if sub_rows else float("nan")))
    parts.append("  false entries/window: k-sigma=%.2f cusum=%.2f level=%.2f total=%.2f, max in any window=%d"
                 % (ent_k / windows, ent_c / windows, ent_l / windows, total_rate, max_win))
    parts.append("  nodes in adaptation per window: %.2f+/-%.2f -> mean residence %.1f windows"
                 % (rn_m, rn_ci, rn_m / total_rate if total_rate else float("nan")))
    parts.append("  final edges: %.0f+/-%.0f" % (ed_m, ed_ci))
    return "\n".join(parts), dict(rk=ent_k / windows, rc=ent_c / windows, rl=ent_l / windows,
                                   total=total_rate, rwn=(rn_m, rn_ci), edges=(ed_m, ed_ci),
                                   residence=rn_m / total_rate if total_rate else float("nan"))


def summarize_crash():
    """cokme deneyi: yeniden tespit suresi, cukur, toparlanma"""
    parts = ["=== crash (holder departs at t=9000, 10 seeds) ==="]
    redetect, dip, recover, final_s8, final_deg = [], [], [], [], []
    for path in seed_files("crash", "crash"):
        rows = read_rows(path)
        post = [r for r in rows if int(r["step"]) > 9000]
        rd = next((int(r["step"]) for r in post if r.get("holderTrending") == "1"), None)
        if rd is not None:
            redetect.append(rd - 9000)
        dip.append(min(float(r["s8"]) for r in post[:10]))
        rec = next((int(r["step"]) for r in post if float(r["s8"]) >= 18), None)
        if rec is not None:
            recover.append(rec - 9000)
        final_s8.append(float(rows[-1]["s8"]))
        final_deg.append(float(rows[-1]["holderDegree"]))
    rd_m, rd_ci = mean_ci(redetect)
    dp_m, dp_ci = mean_ci(dip)
    rc_m, rc_ci = mean_ci(recover)
    fs_m, fs_ci = mean_ci(final_s8)
    fd_m, fd_ci = mean_ci(final_deg)
    parts.append("  redetected %d/10 in %.0f+/-%.0f steps; dip s8=%.1f+/-%.1f; recovered(>=18/20) %d/10 in %.0f+/-%.0f steps; final s8=%.1f+/-%.1f deg=%.1f+/-%.1f"
                 % (len(redetect), rd_m, rd_ci, dp_m, dp_ci, len(recover), rc_m, rc_ci, fs_m, fs_ci, fd_m, fd_ci))
    return "\n".join(parts), dict(n_re=len(redetect), rd=(rd_m, rd_ci), dip=(dp_m, dp_ci),
                                   n_rec=len(recover), rc=(rc_m, rc_ci), fs=(fs_m, fs_ci), fd=(fd_m, fd_ci))


def summarize_multi():
    parts = ["=== concurrent trends (5 items, qmax=0.1 each, 10 seeds) ==="]
    lines = [
        "\\begin{table*}[t]", "\\centering",
        "\\caption{Five concurrent TBP items with staggered entries and ramps, $q_{\\max}{=}0.1$ each, mean $\\pm$ 95\\% CI over 10 seeds. Detection column: seeds in which the item's holder entered adaptation mode. The popularity baseline row averages final success over all five items under the identical workload.}",
        "\\label{tab:multi}",
        "\\resizebox{\\textwidth}{!}{%",
        "\\begin{tabular}{lcccc}", "\\toprule",
        "Item (entry, ramp start) & Detected & Detection time & Holder degree & Success @TTL=8 \\\\",
        "\\midrule",
    ]
    entries = [(2000, 3000), (2400, 3600), (2800, 4200), (3200, 4800), (3600, 5400)]
    for k in range(5):
        degcol = "holderDegree" if k == 0 else "deg%d" % k
        scol = "s8" if k == 0 else "s8i%d" % k
        tcol = "holderTrending" if k == 0 else "trending%d" % k
        det, degs, succ = [], [], []
        for path in seed_files("multi", "trendmulti"):
            rows = read_rows(path)
            t = next((int(r["step"]) for r in rows if r.get(tcol) == "1"), None)
            det.append(t)
            degs.append(float(rows[-1][degcol]))
            succ.append(float(rows[-1][scol]))
        det_vals = [t for t in det if t is not None]
        dm, dci = mean_ci(det_vals) if det_vals else (float("nan"), 0)
        gm, gci = mean_ci(degs)
        sm, sci = mean_ci(succ)
        parts.append("  item%d det=%d/%d t=%.0f+/-%.0f deg=%.1f+/-%.1f s8=%.1f+/-%.1f"
                     % (k, len(det_vals), len(det), dm, dci, gm, gci, sm, sci))
        lines.append("Item %d ($i{=}%d$, $T_s{=}%d$) & %d/%d & $%d \\pm %d$ & %s & %s \\\\"
                     % (k + 1, entries[k][0], entries[k][1], len(det_vals), len(det),
                        round(dm), round(dci), fmt(gm, gci), fmt(sm, sci)))
    # popularity taban cizgisi: 5 item ortalamasi
    base = []
    for path in seed_files("multi", "popmulti"):
        rows = read_rows(path)
        last = rows[-1]
        vals = [float(last["s8"])] + [float(last["s8i%d" % k]) for k in range(1, 5)]
        base.append(sum(vals) / len(vals))
    bm, bci = mean_ci(base)
    parts.append("  popularity baseline mean-over-items s8=%.1f+/-%.1f" % (bm, bci))
    lines.append("\\midrule")
    lines.append("Popularity baseline, mean over items & -- & -- & -- & %s \\\\" % fmt(bm, bci))
    # rewire yuku
    (rwn_m, rwn_ci), _ = rewire_load("multi", "trendmulti")
    parts.append("  rewire load: %.2f+/-%.2f nodes/window" % (rwn_m, rwn_ci))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table*}"]
    return "\n".join(parts), "\n".join(lines), (rwn_m, rwn_ci)


def summarize_seq():
    """ardisik sonumlu darbeler: tespit, temiz cikis, bayat hub birikimi"""
    parts = ["=== sequential decaying pulses (5 pulses, 10 seeds) ==="]
    det = 0; exited = 0; total = 0
    peak_eq_fin = 0
    pop_edges, tr_edges = [], []
    for path in seed_files("seq", "seqpulse"):
        rows = read_rows(path)
        tr_edges.append(float(rows[-1]["edges"]))
        for k in range(5):
            dcol = "holderDegree" if k == 0 else "deg%d" % k
            tcol = "holderTrending" if k == 0 else "trending%d" % k
            total += 1
            d = any(r[tcol] == "1" for r in rows)
            if d:
                det += 1
                if rows[-1][tcol] == "0":
                    exited += 1
                peak = max(float(r[dcol]) for r in rows)
                fin = float(rows[-1][dcol])
                if fin >= peak - 2:
                    peak_eq_fin += 1
    for path in seed_files("seq", "popseqpulse"):
        rows = read_rows(path)
        pop_edges.append(float(rows[-1]["edges"]))
    te, tci = mean_ci(tr_edges)
    pe, pci = mean_ci(pop_edges)
    parts.append("  detected %d/%d pulses; exited after decay %d/%d; links persisted without growth in %d/%d"
                 % (det, total, exited, det, peak_eq_fin, det))
    parts.append("  final edges: trend %.0f+/-%.0f vs popularity %.0f+/-%.0f (+%.1f%%)"
                 % (te, tci, pe, pci, 100 * (te - pe) / pe))
    return "\n".join(parts), dict(det=det, total=total, exited=exited,
                                   persist=peak_eq_fin, te=(te, tci), pe=(pe, pci),
                                   delta=100 * (te - pe) / pe)


def summarize_plateau(tcfg="trendstat", pcfg="popstat"):
    """duragan olgun ag: buyume 10K'da durur; churn slot-devralmadir, yani
    nufus VE kenar butcesi sabittir (taban cizgisinde E degismez, dogrulanir);
    5 ardisik darbe platoda gelir. Hicbir seyrelme olmadan bayat hub
    birikimi olusuyor mu?"""
    parts = ["=== stationary plateau pulses (fixed population AND edges, 5 pulses, 10 seeds) ==="]
    det = 0; exited = 0; total = 0; persisted = 0
    tr_edges, pop_edges = [], []
    bg_tr, bg_pop, fe = [], [], []
    peak_degs, fin_degs = [], []
    exit_degs, exit_lat = [], []
    pop_drift = []
    pulse_marks = [19000, 22000, 25000, 28000, 31000]
    tr_at, pop_at = {m: [] for m in pulse_marks}, {m: [] for m in pulse_marks}
    for path in seed_files("rev", tcfg):
        rows = read_rows(path)
        plat = [r for r in rows if int(r["step"]) >= 14400]
        tr_edges.append(float(rows[-1]["edges"]))
        bg_tr.append(float(rows[-1]["bgSucc"]))
        # saf yanlis girisler: bu tohumda tespit edilen gercek TBP
        # tutucularinin girisleri toplamdan dusulur
        seed_genuine = 0
        for kk in range(5):
            tc = "holderTrending" if kk == 0 else "trending%d" % kk
            if any(x[tc] == "1" for x in rows):
                seed_genuine += 1
        fe.append((sum(int(r["entK"]) + int(r["entC"]) + int(r["entL"]) for r in plat) - seed_genuine) / len(plat))
        for m in pulse_marks:
            tr_at[m].append(float(min(rows, key=lambda r: abs(int(r["step"]) - m))["edges"]))
        for k in range(5):
            dcol = "holderDegree" if k == 0 else "deg%d" % k
            tcol = "holderTrending" if k == 0 else "trending%d" % k
            total += 1
            adapting = [x for x in rows if x[tcol] == "1"]
            if adapting:
                det += 1
                if rows[-1][tcol] == "0":
                    exited += 1
                # dogru metrikler: talep tepesindeki derece, histerezis
                # cikisindaki (son adapting pencere) derece, cikis gecikmesi
                tpeak = 17500 + 3000 * k
                decay_end = 19000 + 3000 * k
                pk = min(rows, key=lambda x: abs(int(x["step"]) - tpeak))
                peak_degs.append(float(pk[dcol]))
                last = adapting[-1]
                exit_degs.append(float(last[dcol]))
                # cikis ani: son adapting pencereyi IZLEYEN ilk pencere
                # (durum o sinirda 0'a doner; adapting satirda hala kazanim var)
                li = rows.index(last)
                exit_step = int(rows[li + 1]["step"]) if li + 1 < len(rows) else int(last["step"]) + 200
                exit_lat.append(exit_step - decay_end)
                fin = float(rows[-1][dcol])
                fin_degs.append(fin)
                if fin <= float(last[dcol]) + 2:
                    persisted += 1
    for path in seed_files("rev", pcfg):
        rows = read_rows(path)
        pop_edges.append(float(rows[-1]["edges"]))
        bg_pop.append(float(rows[-1]["bgSucc"]))
        plat = [r for r in rows if int(r["step"]) >= 14400]
        pop_drift.append(float(plat[-1]["edges"]) - float(plat[0]["edges"]))
        for m in pulse_marks:
            pop_at[m].append(float(min(rows, key=lambda r: abs(int(r["step"]) - m))["edges"]))
    n = min(len(tr_edges), len(pop_edges))
    ov = [100.0 * (tr_edges[i] - pop_edges[i]) / pop_edges[i] for i in range(n)]
    ov_m, ov_ci = mean_ci(ov)
    te, tci = mean_ci(tr_edges)
    pe, pci = mean_ci(pop_edges)
    fe_m, fe_ci = mean_ci(fe)
    parts.append("  detected %d/%d pulses; exited after decay %d/%d; links persisted without growth in %d/%d"
                 % (det, total, exited, det, persisted, det))
    parts.append("  holder degree @demand-peak %.1f, @hysteresis-exit %.1f, final %.1f; frozen after exit %d/%d"
                 % (mean_ci(peak_degs)[0], mean_ci(exit_degs)[0], mean_ci(fin_degs)[0], persisted, det))
    el_m, el_ci = mean_ci(exit_lat)
    parts.append("  exit latency after decay end: %.0f+/-%.0f steps (max %d)"
                 % (el_m, el_ci, max(exit_lat) if exit_lat else -1))
    parts.append("  final edges: trend %.0f+/-%.0f vs popularity %.0f+/-%.0f (paired overhead %.2f%%+/-%.2f)"
                 % (te, tci, pe, pci, ov_m, ov_ci))
    parts.append("  bg success final: trend %.1f vs pop %.1f; false entries/window in plateau %.2f+/-%.2f"
                 % (mean_ci(bg_tr)[0], mean_ci(bg_pop)[0], fe_m, fe_ci))
    parts.append("  baseline plateau edge drift (must be 0): %s" % pop_drift)
    resid = []
    n2 = min(len(tr_at[19000]), len(pop_at[19000]))
    for m in pulse_marks:
        resid.append(sum(tr_at[m][i] - pop_at[m][i] for i in range(n2)) / n2)
    parts.append("  paired residual links at pulse ends %s: %s (increments: %s)"
                 % (pulse_marks, ["%.0f" % r for r in resid],
                    ["%.0f" % (resid[i] - (resid[i - 1] if i else 0)) for i in range(len(resid))]))
    return "\n".join(parts), dict(det=det, total=total, exited=exited, persisted=persisted,
                                   ov=(ov_m, ov_ci), fe=(fe_m, fe_ci),
                                   peak=mean_ci(peak_degs)[0], exitd=mean_ci(exit_degs)[0],
                                   fin=mean_ci(fin_degs)[0], exitlat=mean_ci(exit_lat),
                                   resid=resid, drift=pop_drift)


def summarize_shapes():
    parts = ["=== demand shapes (10 seeds) ==="]
    lines = [
        "\\begin{table}[t]", "\\centering",
        "\\caption{Demand-shape variants at $q_{\\max}{=}0.2$, mean $\\pm$ 95\\% CI over 10 seeds. The step shape jumps to $q_{\\max}$ at $T_s{=}2{,}500$, before the holder joins, so detection there exercises the high-water level rule rather than the rise rules; the exponential shape reaches 95\\% of $q_{\\max}$ by $T_p$. The linear row repeats the twenty-seed default.}",
        "\\label{tab:shapes}",
        "\\resizebox{\\columnwidth}{!}{%",
        "\\begin{tabular}{lccc}", "\\toprule",
        "Demand shape & Detection time & Holder degree & Success @TTL=8 \\\\",
        "\\midrule",
    ]
    rows_spec = [("linear (default)", "main", "trend"), ("step (flash crowd)", "shapes", "step"),
                 ("exponential", "shapes", "expo")]
    for label, subdir, config in rows_spec:
        det = detection_times(subdir, config)
        det_vals = [t for t in det.values() if t is not None]
        dm, dci = mean_ci(det_vals) if det_vals else (float("nan"), 0)
        table = metric_stats(subdir, [config], ["holderDegree", "s8"])
        hd = table["holderDegree"][config]
        s8 = table["s8"][config]
        parts.append("  %-20s det=%d/%d t=%.0f+/-%.0f deg=%.1f+/-%.1f s8=%.1f+/-%.1f"
                     % (label, len(det_vals), len(det), dm, dci, hd[0], hd[1], s8[0], s8[1]))
        lines.append("%s & $%d \\pm %d$ (%d/%d) & %s & %s \\\\"
                     % (label, round(dm), round(dci), len(det_vals), len(det),
                        fmt(*hd[:2]), fmt(*s8[:2])))
    lines += ["\\bottomrule", "\\end{tabular}%", "}", "\\end{table}"]
    return "\n".join(parts), "\n".join(lines)


def main():
    out = []
    main_txt, main_tables, det, rload = summarize_main()
    out.append(main_txt)

    cap_final = ("Multi-seed results, mean $\\pm$ 95\\% CI over 20 seeds, at the final "
                 "measurement window ($t \\approx 14{,}300$). TBP probes target the "
                 "single-copy to-be-popular item; background probes target ordinary "
                 "items drawn from the popularity-weighted query distribution.")
    cap_mid = "Multi-seed results at $t=11{,}000$ (after the demand peak, network still growing)."
    tex = [latex_main_table(main_tables["final"], cap_final, "tab:multiseed"),
           latex_main_table(main_tables["mid"], cap_mid, "tab:multiseed-mid")]
    with open(os.path.join(PAPER, "table_multiseed.tex"), "w") as f:
        f.write("\n\n".join(tex) + "\n")

    abl_txt, abl_tex = summarize_ablation()
    out.append(abl_txt)
    with open(os.path.join(PAPER, "table_ablation.tex"), "w") as f:
        f.write(abl_tex + "\n")

    tf = main_tables["final"]
    default_row = (fmt(*tf["s8"]["trend"][:2]),
                   fmt(*tf["holderDegree"]["trend"][:2]),
                   fmt(rload[0], rload[1], 2))
    wq = metric_stats("qmax", ["qmax010"], ["s8"])
    wdet = detection_times("qmax", "qmax010")
    weak_default = ("%d/%d" % (len([t for t in wdet.values() if t is not None]), len(wdet)),
                    fmt(*wq["s8"]["qmax010"][:2]))
    sw_txt, sw_tex = summarize_sweep(default_row, weak_default)
    out.append(sw_txt)
    with open(os.path.join(PAPER, "table_sweep.tex"), "w") as f:
        f.write(sw_tex + "\n")

    qm_txt, qm_tex = summarize_qmax()
    out.append(qm_txt)
    with open(os.path.join(PAPER, "table_qmax.tex"), "w") as f:
        f.write(qm_tex + "\n")

    sc_txt, sc_tex, fx = summarize_scale(main_tables)
    out.append(sc_txt)
    with open(os.path.join(PAPER, "table_scale.tex"), "w") as f:
        f.write(sc_tex + "\n")

    ct_txt, ct_tex = summarize_controls()
    out.append(ct_txt)
    with open(os.path.join(PAPER, "table_controls.tex"), "w") as f:
        f.write(ct_tex + "\n")

    rl_txt, rl_tex = summarize_rules()
    out.append(rl_txt)
    with open(os.path.join(PAPER, "table_rules.tex"), "w") as f:
        f.write(rl_tex + "\n")
    att = entry_attribution("rev", "trendatt")
    out.append("  full-mechanism holder entry attribution over 20 seeds (trendatt rerun): k-sigma=%d cusum=%d level=%d"
               % (att[1], att[2], att[3]))

    co_txt, co_tex = summarize_cooper(main_tables)
    out.append(co_txt)
    with open(os.path.join(PAPER, "table_cooper.tex"), "w") as f:
        f.write(co_tex + "\n")

    nl_txt, nl = summarize_null()
    out.append(nl_txt)

    cr_txt, cr = summarize_crash()
    out.append(cr_txt)

    mu_txt, mu_tex, mu_load = summarize_multi()
    out.append(mu_txt)
    with open(os.path.join(PAPER, "table_multi.tex"), "w") as f:
        f.write(mu_tex + "\n")

    sh_txt, sh_tex = summarize_shapes()
    out.append(sh_txt)

    try:
        sq_txt, sq = summarize_seq()
        out.append(sq_txt)
    except Exception as e:
        out.append("seq: veri yok (%s)" % e)

    try:
        pl_txt, pl = summarize_plateau()
        out.append(pl_txt)
    except Exception as e:
        out.append("plateau: veri yok (%s)" % e)
    with open(os.path.join(PAPER, "table_shapes.tex"), "w") as f:
        f.write(sh_tex + "\n")

    s, tot, wl = pooled_wilson("main", "trend", "s8")
    out.append("=== pooled Wilson: trend s8 final = %d/%d, %%95 alt sinir = %.3f ===" % (int(s), int(tot), wl))
    s2, tot2, wl2 = pooled_wilson("main", "trend", "s12")
    out.append("    trend s12 final = %d/%d, alt sinir = %.3f" % (int(s2), int(tot2), wl2))

    summary = "\n\n".join(out) + "\n"
    print(summary)
    with open(SUMMARY_PATH, "w") as f:
        f.write(summary)
    print("Yazildi: results-final/summary.txt + paper/table_{multiseed,ablation,sweep,qmax,scale}.tex")


if __name__ == "__main__":
    sys.exit(main())
