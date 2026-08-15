# Reproducibility Package: Improving Emerging-Content Discovery in Unstructured Peer-to-Peer Networks

This repository accompanies the manuscript:

> Vasfi Tataroglu, "Improving Emerging-Content Discovery in Unstructured
> Peer-to-Peer Networks through Local Trend-Triggered Link Acquisition."

It contains the simulator source code, complete run manifests, raw outputs of
all reported simulation runs, batch logs, aggregated results, and the scripts
used to compute the reported summaries and figures. Every simulation is seeded
and deterministic.

## Contents

```text
ScalableP2P/       Simulator source code
SimRunner/         .NET runner project
analysis/          Aggregation and figure-generation scripts
results-final/     Run manifests, raw CSV outputs, logs, and summary statistics
ScalableP2P.sln    Visual Studio solution
```

The main implementation components are:

- `Graph.cs`: overlay growth, churn, link acquisition, controls, and baselines
- `Node.cs`: local trend detector and hysteretic state transitions
- `TrendModel.cs`: time-varying query workloads
- `Zipf.cs`: popularity-weighted query sampling
- `Program.cs`: seeded experiment runner and command-line interface

## Requirements

- .NET SDK 7.0 or later for the simulator
- Python 3.10 or later for result aggregation and figure generation
- Python packages listed in `analysis/requirements.txt`

## Build

```bash
dotnet build SimRunner/SimRunner.csproj -c Release
```

## Quick start

The following commands run the degree-based, popularity-based, and proposed
trend-triggered policies with seed 1:

```bash
dotnet run --project SimRunner/SimRunner.csproj -c Release -- \
  one out degree 1 0 100 0 0

dotnet run --project SimRunner/SimRunner.csproj -c Release -- \
  one out popularity 1 100 0 0 0

dotnet run --project SimRunner/SimRunner.csproj -c Release -- \
  one out trend 1 100 0 25000 1
```

Each run writes a per-window metrics file and a final degree histogram to the
selected output directory.

## Command-line interface

```text
one <outdir> <name> <seed> <Pp> <Pd> <Pt> <rewire 0|1>
    [k] [c] [eps] [qmax] [n] [Theta] [crash] [shape] [multi]
    [randrw] [oracle 0|1] [fixedthr] [rules] [cooper 0|1]
    [cooperDmax] [cooperMu] [noscale 0|1] [plateau] [reactive]
```

The required coefficients `Pp`, `Pd`, and `Pt` control popularity, degree, and
trend weight in attachment. `rewire` enables holder-side link acquisition.
Optional arguments select the detector calibration and evaluation controls:

- `k`, `c`, `eps`, `Theta`: detector parameters
- `qmax`: peak demand share
- `n`: target network size
- `crash`: holder-departure step
- `shape`: demand shape (`0` linear, `1` step, `2` exponential,
  `3` rise-and-decay pulses)
- `multi`: number of concurrent trend items
- `randrw`: random link-acquisition control
- `oracle`: adaptation beginning at the known ramp start
- `fixedthr`: absolute-threshold control
- `rules`: entry-rule bitmask (`1` k-sigma, `2` CUSUM, `4` high-water level;
  default `7` enables all three)
- `cooper`, `cooperDmax`, `cooperMu`: square-root-construct baseline
- `noscale`: fixed demand timing in the scaling control
- `plateau`: length of the fixed-population mature-network phase
- `reactive`: requester-side acquaintance-link control (`0` disabled,
  `1` TBP-only upper bound, `2` all-content matched-budget control)

Arguments omitted from older manifests use the defaults defined in
`Program.cs`. The manifests can therefore be passed directly to the compiled
runner.

## Data package

`results-final/` contains 750 seeded runs. Files named `runs*.txt` are the
complete command manifests, and `batch*.log` files record their execution.
Raw outputs are grouped as follows:

| Directory | Experiment group |
|---|---|
| `main/` | Degree, popularity, and trend-triggered policies |
| `ablation/` | Adaptation-path ablations |
| `controls/` | Random-budget, fixed-threshold, and oracle controls |
| `rev/` | Entry-rule, Cooper, null, plateau, and requester-side controls |
| `qmax/` | Trend-strength sweep |
| `shapes/` | Step and exponential demand shapes |
| `scale/` | Networks from 5,000 to 20,000 nodes |
| `crash/` | Holder departure and recovery |
| `multi/` | Concurrent trends |
| `seq/` | Sequential rise-and-decay pulses |
| `sweep/`, `sweepweak/`, `sweepfaint/` | Parameter sensitivity regimes |

For each run, `<name>-s<seed>.csv` stores the per-window time series and
`<name>-s<seed>-degrees.csv` stores the final degree distribution. The common
time-series columns are:

```text
step,maxId,removed,holderDegree,s4,v4,s6,v6,s8,v8,s12,v12,
holderTrend,bgSucc,bgHits,bgVis,rewireNodes,rewireJoins,edges,
holderTrending
```

Detector-attribution runs additionally contain `entK`, `entC`, `entL`, and
`holderEntryRule`. The file `results-final/summary.txt` contains the aggregate
means and 95% confidence intervals used in the manuscript.

## Reproduce summaries and figures

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r analysis/requirements.txt

mkdir -p generated/tables generated/figures
PAPER_OUTPUT_DIR=generated/tables \
SUMMARY_OUTPUT_PATH=generated/summary.txt \
  python analysis/aggregate.py

FIGURE_OUTPUT_DIR=generated/figures \
  python analysis/make_figures.py
```

The aggregation script uses two-sided Student-t 95% confidence intervals for
reported means and retains the pooled Wilson calculations used for saturated
binomial outcomes.

## Contact

Vasfi Tataroglu  
Department of Computer Engineering, Faculty of Engineering  
Pamukkale University, Denizli, Turkey  
ORCID: [0009-0000-9907-6619](https://orcid.org/0009-0000-9907-6619)  
Email: vtataroglu@pau.edu.tr
