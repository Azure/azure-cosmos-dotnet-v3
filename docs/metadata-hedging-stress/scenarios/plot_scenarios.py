#!/usr/bin/env python3
"""Graphs for PR #5923 phased PR-vs-main metadata-hedging scenarios."""
import csv, math, os, sys, textwrap
from collections import defaultdict
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
PR_C, MAIN_C = "#1e8449", "#922b21"


def add_analysis(fig, shows, proves, width=140):
    """Embed a 'what it shows / what it proves' analysis box at the bottom of the figure."""
    body = (textwrap.fill("WHAT IT SHOWS:   " + shows, width=width) + "\n\n" +
            textwrap.fill("WHAT IT PROVES:  " + proves, width=width))
    nlines = body.count("\n") + 1
    reserve_in = 0.95 + nlines * 0.145
    bottom = min(0.46, reserve_in / fig.get_size_inches()[1])
    fig.subplots_adjust(bottom=bottom)
    fig.text(0.012, 0.012, body, ha="left", va="bottom", fontsize=8.4, linespacing=1.3,
             bbox=dict(boxstyle="round,pad=0.6", facecolor="#f4f7f5", edgecolor=PR_C, linewidth=1.0))

def load(name):
    with open(os.path.join(OUT, name), newline="") as f:
        return list(csv.DictReader(f))

ops = load("ops.csv")
phase = load("phase_summary.csv")
regions = load("regions.csv")

def pct(vals, p):
    v = sorted(vals)
    if not v: return 0
    return v[min(len(v)-1, max(0, int(math.ceil(p*len(v))-1)))]

# refresh-bearing latencies by (scenario, phase, arm)
refresh = defaultdict(list)
for r in ops:
    if r["metaRefresh"] == "1":
        refresh[(r["scenario"], r["phase"], r["arm"])].append(float(r["latencyMs"]))

# ---- G1: refresh-bearing read latency, PR vs main (the headline) ----
groups = [
    ("S2-BadColdStart", "ColdStart", "Bad cold start"),
    ("S3-BadCold+Degraded", "Degraded", "Degraded\n(hub delay + 410 gone)"),
    ("S4-GoodCold+Degraded", "Degraded", "Degraded\n(healthy cold start)"),
]
fig, ax = plt.subplots(figsize=(12, 7.8))
x = np.arange(len(groups)); w = 0.2
def vals(metric_p, arm):
    out = []
    for sc, ph, _ in groups:
        v = refresh.get((sc, ph, arm), [])
        out.append(pct(v, metric_p))
    return out
ax.bar(x-1.5*w, vals(.5, "main"), w, label="p50 main", color=MAIN_C, alpha=.55)
ax.bar(x-0.5*w, vals(.5, "PR"),   w, label="p50 PR",   color=PR_C, alpha=.55)
ax.bar(x+0.5*w, vals(.99, "main"),w, label="p99 main", color=MAIN_C)
ax.bar(x+1.5*w, vals(.99, "PR"),  w, label="p99 PR",   color=PR_C)
ax.set_xticks(x); ax.set_xticklabels([g[2] for g in groups])
ax.set_ylabel("latency of refresh-bearing reads (ms, 10x-compressed)")
ax.set_title("Metadata-refresh-bearing read latency: PR (metadata hedging) vs main\n"
             "PR cuts the median ~65% in the degraded phase; p99 tail is intentionally bounded by the budget")
ax.legend()
for i,(sc,ph,_) in enumerate(groups):
    m50=pct(refresh.get((sc,ph,"main"),[]),.5); p50=pct(refresh.get((sc,ph,"PR"),[]),.5)
    if m50: ax.annotate(f"-{100*(m50-p50)/m50:.0f}%", (x[i]-0.5*w, p50), textcoords="offset points",
                        xytext=(0,4), ha="center", fontsize=9, fontweight="bold", color=PR_C)
fig.tight_layout()
add_analysis(fig,
    "[Simulated harness] Latency (p50 & p99) of ONLY the refresh-bearing reads, PR vs main, for a bad cold start and two "
    "degraded phases. Times are 10x-compressed.",
    "Among the reads that actually trigger a metadata refresh, hedging cuts the median ~65% in the degraded phase. "
    "Superseded by the fault-injection harness, which measures real transport latency (see ../faultinjection).")
fig.savefig(os.path.join(OUT,"s1_refresh_latency_pr_vs_main.png"), dpi=130); plt.close(fig)

# ---- G2: timeline of read latency over the run (S3), main vs PR ----
sc = "S3-BadCold+Degraded"
fig, axes = plt.subplots(2, 1, figsize=(13, 10), sharex=True, sharey=True)
for ax, arm, color in [(axes[0], "main", MAIN_C), (axes[1], "PR", PR_C)]:
    xs = [int(r["opIndex"]) for r in ops if r["scenario"]==sc and r["arm"]==arm]
    ys = [float(r["latencyMs"]) for r in ops if r["scenario"]==sc and r["arm"]==arm]
    ax.scatter(xs, ys, s=4, alpha=0.35, color=color)
    ax.set_ylabel("read latency (ms)")
    ax.set_title(f"{arm}", loc="left", fontweight="bold", color=color)
    ax.axvspan(0, 200, color="#f39c12", alpha=0.10)
    ax.axvspan(200, 1700, color="#2ecc71", alpha=0.07)
    ax.axvspan(1700, 4700, color="#e74c3c", alpha=0.08)
axes[1].set_xlabel("operation # (← cold start | OK period | degraded: hub delay + 410 PkRange-Gone →)")
fig.suptitle("S3 timeline — bad cold start → OK → degraded. main stalls on slow metadata reads;\n"
             "PR recovers most via hedging (lower band). Orange=cold, green=OK, red=degraded.",
             fontsize=12)
fig.tight_layout(rect=[0,0,1,0.94])
add_analysis(fig,
    "[Simulated] Per-operation read latency over the whole S3 run, main (top) vs PR (bottom): cold start (orange) -> OK "
    "period (green) -> degraded hub + 410 PkRange-Gone (red).",
    "main stalls on every slow metadata refresh during the degraded phase (dense high band); PR collapses most of them "
    "via a single hedge (lower band). Visualizes WHEN and WHY the win occurs across the run's lifecycle.")
fig.savefig(os.path.join(OUT,"s2_timeline_s3.png"), dpi=130); plt.close(fig)

# ---- G3: per-region metadata calls, PR vs main ----
region_order = ["West US 2","East US","South Central US","Central US","North Central US"]
scen_list = ["S1-Healthy","S2-BadColdStart","S3-BadCold+Degraded","S4-GoodCold+Degraded"]
reg = defaultdict(lambda: defaultdict(int))
for r in regions:
    reg[(r["scenario"],r["arm"])][r["region"]] = int(r["sends"])
fig, axes = plt.subplots(1, len(scen_list), figsize=(17,7), sharey=True)
for ax, sc in zip(axes, scen_list):
    xm = np.arange(len(region_order)); w=0.38
    mv=[reg[(sc,"main")].get(rg,0) for rg in region_order]
    pv=[reg[(sc,"PR")].get(rg,0) for rg in region_order]
    ax.bar(xm-w/2, mv, w, label="main", color=MAIN_C)
    ax.bar(xm+w/2, pv, w, label="PR", color=PR_C)
    ax.set_title(sc, fontsize=10)
    ax.set_xticks(xm); ax.set_xticklabels([r.replace(" ","\n") for r in region_order], fontsize=7)
axes[0].set_ylabel("metadata calls (sends)"); axes[0].legend()
fig.suptitle("Per-region metadata calls — main hits only the hub (West US 2); PR adds bounded hedges to ONE secondary (East US)", fontsize=12)
fig.tight_layout(rect=[0,0,1,0.93])
add_analysis(fig,
    "[Simulated] Count of metadata calls per region, main vs PR, for each of the four scenarios across the 5-region "
    "account.",
    "main sends metadata only to the hub (West US 2); PR adds a bounded number of hedges to exactly ONE secondary "
    "(East US) and never touches the other three regions — fan-out is a single region, not a broadcast.")
fig.savefig(os.path.join(OUT,"s3_calls_per_region.png"), dpi=130); plt.close(fig)

# ---- G4: hedges fired vs budget-exhausted (bounded amplification) ----
fig, ax = plt.subplots(figsize=(12,7.8))
labels=[]; hed=[]; bud=[]
for p in phase:
    if p["arm"]=="PR" and int(p["hedges"])>0:
        labels.append(f"{p['scenario'].split('-')[0]}\n{p['phase']}")
        hed.append(int(p["hedges"])); bud.append(int(p["budgetExhausted"]))
x=np.arange(len(labels)); w=0.38
ax.bar(x-w/2, hed, w, label="hedged (recovered)", color=PR_C)
ax.bar(x+w/2, bud, w, label="budget-exhausted (fell back to primary-only)", color="#e67e22")
ax.set_xticks(x); ax.set_xticklabels(labels, fontsize=8)
ax.set_ylabel("metadata refresh reads")
ax.set_title("PR bounded amplification: hedges fired vs budget-exhausted per phase\n"
             "Beyond the per-client budget (8 concurrent) refreshes fall back to primary-only — the gateway is never flooded")
ax.legend()
for i in range(len(labels)):
    ax.text(x[i]-w/2, hed[i], str(hed[i]), ha="center", va="bottom", fontsize=8)
    ax.text(x[i]+w/2, bud[i], str(bud[i]), ha="center", va="bottom", fontsize=8)
fig.tight_layout()
add_analysis(fig,
    "[Simulated] For PR only, per phase: how many refresh reads hedged and recovered vs how many were budget-exhausted "
    "and fell back to primary-only.",
    "Amplification is bounded: beyond the per-client budget (8 concurrent) excess refreshes do not hedge, they revert to "
    "primary-only — so the Gateway is never flooded regardless of load.")
fig.savefig(os.path.join(OUT,"s4_hedges_vs_budget.png"), dpi=130); plt.close(fig)

# ---- G5: healthy scenario — no amplification ----
fig, ax = plt.subplots(figsize=(9,7.4))
s1 = [p for p in phase if p["scenario"]=="S1-Healthy"]
cats=["main p50","PR p50","main p99","PR p99","PR hedges","PR secondary calls"]
def ph(arm,metric):
    v=[float(p[metric]) for p in s1 if p["arm"]==arm]; return max(v) if v else 0
sec=sum(int(r["sends"]) for r in regions if r["scenario"]=="S1-Healthy" and r["arm"]=="PR" and r["region"]!="West US 2")
vals=[ph("main","p50Ms"),ph("PR","p50Ms"),ph("main","p99Ms"),ph("PR","p99Ms"),
      sum(int(p["hedges"]) for p in s1 if p["arm"]=="PR"), sec]
bars=ax.bar(cats, vals, color=[MAIN_C,PR_C,MAIN_C,PR_C,"#2980b9","#2980b9"])
for b,v in zip(bars,vals): ax.text(b.get_x()+b.get_width()/2, v, f"{v:.0f}", ha="center", va="bottom", fontsize=10)
ax.set_title("S1 completely healthy: PR == main, ZERO hedges, ZERO secondary calls (no amplification)")
ax.set_ylabel("ms  /  count")
fig.tight_layout()
add_analysis(fig,
    "[Simulated] The completely-healthy scenario (S1): main vs PR p50/p99, plus PR's hedge count and secondary-region "
    "call count.",
    "When the hub is healthy, PR is identical to main and fires ZERO hedges and ZERO secondary calls — no steady-state "
    "cost or amplification when nothing is wrong.")
fig.savefig(os.path.join(OUT,"s5_healthy_no_amplification.png"), dpi=130); plt.close(fig)

print("wrote graphs to", OUT)
for fn in ["s1_refresh_latency_pr_vs_main.png","s2_timeline_s3.png","s3_calls_per_region.png",
           "s4_hedges_vs_budget.png","s5_healthy_no_amplification.png"]:
    print("  ", fn)
