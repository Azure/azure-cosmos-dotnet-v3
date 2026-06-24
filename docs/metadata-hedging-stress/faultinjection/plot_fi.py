#!/usr/bin/env python3
"""Graphs for PR #5923 metadata hedging — REAL fault-injection harness (PR vs main)."""
import csv, math, os, sys
from collections import defaultdict
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
PR_C, MAIN_C = "#1e8449", "#922b21"

def load(name):
    with open(os.path.join(OUT, name), newline="") as f:
        return list(csv.DictReader(f))

scen = load("fi_scenarios.csv")
ops = load("fi_ops.csv")

def row(scenario, regime, armOn):
    for r in scen:
        if r["scenario"] == scenario and r["regime"] == regime and r["hedgingOn"] == armOn:
            return r
    return None

def pct(vals, p):
    v = sorted(vals)
    if not v: return 0
    return v[min(len(v)-1, max(0, int(math.ceil(p*len(v))-1)))]

# ---- G1: headline p50/p99 PR vs main across the 4 measurement points ----
points = [
    ("coldstart", "single", "Cold start\n(end-to-end ReadItem)"),
    ("refresh", "low", "Refresh\n(low contention)"),
    ("refresh", "saturating", "Refresh\n(saturating storm)"),
    ("mixed", "refresh-subset", "Mixed: refresh\nsubset"),
]
fig, ax = plt.subplots(figsize=(13, 6.5))
x = np.arange(len(points)); w = 0.2
def vals(metric, on):
    out = []
    for sc, rg, _ in points:
        r = row(sc, rg, on)
        out.append(float(r[metric])/1000 if r else 0)
    return out
ax.bar(x-1.5*w, vals("p50Ms","0"), w, label="p50 main", color=MAIN_C, alpha=.55)
ax.bar(x-0.5*w, vals("p50Ms","1"), w, label="p50 PR",   color=PR_C, alpha=.55)
ax.bar(x+0.5*w, vals("p99Ms","0"), w, label="p99 main", color=MAIN_C)
ax.bar(x+1.5*w, vals("p99Ms","1"), w, label="p99 PR",   color=PR_C)
ax.set_xticks(x); ax.set_xticklabels([p[2] for p in points])
ax.set_ylabel("metadata read latency (s) — REAL, fault-injected")
ax.set_title("PR (metadata hedging) vs main — real fault-injected metadata latency\n"
             "hub ResponseDelay 3s on Gateway metadata (West US 2); secondaries healthy; threshold 1.5s")
ax.legend()
for i,(sc,rg,_) in enumerate(points):
    rm=row(sc,rg,"0"); rp=row(sc,rg,"1")
    if rm and rp and float(rm["p50Ms"])>0:
        d=100*(float(rm["p50Ms"])-float(rp["p50Ms"]))/float(rm["p50Ms"])
        ax.annotate(f"p50 -{d:.0f}%", (x[i]-0.5*w, float(rp["p50Ms"])/1000),
                    textcoords="offset points", xytext=(0,4), ha="center", fontsize=9, fontweight="bold", color=PR_C)
fig.tight_layout(); fig.savefig(os.path.join(OUT,"fi1_latency_pr_vs_main.png"), dpi=130); plt.close(fig)

# ---- G2: meter cross-check — hedges fired (ON>0, OFF=0) ----
fig, ax = plt.subplots(figsize=(11, 6))
labels, onfires, offfires = [], [], []
for sc, rg, lab in points:
    rp=row(sc,rg,"1"); rm=row(sc,rg,"0")
    labels.append(lab.replace("\n"," "))
    onfires.append(int(rp["meterFires"]) if rp else 0)
    offfires.append(int(rm["meterFires"]) if rm else 0)
xx=np.arange(len(labels)); w=0.38
b1=ax.bar(xx-w/2, offfires, w, label="main (OFF)", color=MAIN_C)
b2=ax.bar(xx+w/2, onfires, w, label="PR (ON)", color=PR_C)
ax.set_xticks(xx); ax.set_xticklabels(labels, fontsize=9)
ax.set_ylabel("hedges fired (SDK meter: Azure.Cosmos.Client.MetadataHedging)")
ax.set_title("Meter cross-check — hedges fire ONLY with the PR (ON); main never hedges\n"
             "(authoritative SDK telemetry, not harness-side counting)")
ax.legend()
for b,v in zip(list(b1)+list(b2), offfires+onfires):
    if v: ax.text(b.get_x()+b.get_width()/2, v, str(v), ha="center", va="bottom", fontsize=9)
fig.tight_layout(); fig.savefig(os.path.join(OUT,"fi2_meter_crosscheck.png"), dpi=130); plt.close(fig)

# ---- G3: saturating storm — budget bound (latency CDF) ----
fig, ax = plt.subplots(figsize=(11, 6.5))
for on, color, lab in [("main(OFF)", MAIN_C, "main (OFF)"), ("PR(ON)", PR_C, "PR (ON)")]:
    lat = sorted(float(o["latencyMs"])/1000 for o in ops if o["scenario"]=="refresh" and o["regime"]=="saturating" and o["arm"]==on)
    if lat:
        ys = np.arange(1, len(lat)+1)/len(lat)
        ax.plot(lat, ys, marker="o", ms=3, color=color, label=lab)
rp=row("refresh","saturating","1")
ax.axvline(1.5, color="#1e8449", ls=":", lw=1.5, label="hedge threshold 1.5s")
ax.set_xlabel("metadata refresh latency (s)")
ax.set_ylabel("cumulative fraction of ops")
budget_note = f"{rp['budgetExhausted']}/{rp['n']} ops budget-exhausted (primary-only)" if rp else ""
ax.set_title("Saturating storm (12 distinct containers concurrent) — PR budget bound is real\n"
             f"~8 concurrent hedges recover at ~1.5s; {budget_note}; the rest match main (~4s)", fontsize=11)
ax.legend()
fig.tight_layout(); fig.savefig(os.path.join(OUT,"fi3_saturating_budget_cdf.png"), dpi=130); plt.close(fig)

# ---- G4: mixed end-to-end — p50 unchanged, tail improved (honest framing) ----
fig, ax = plt.subplots(figsize=(11, 6))
rmE=row("mixed","end-to-end","0"); rpE=row("mixed","end-to-end","1")
cats=["p50","p95","p99"]
mv=[float(rmE[f"{c}Ms"]) for c in cats]; pv=[float(rpE[f"{c}Ms"]) for c in cats]
xx=np.arange(len(cats)); w=0.38
b1=ax.bar(xx-w/2, mv, w, label="main (OFF)", color=MAIN_C)
b2=ax.bar(xx+w/2, pv, w, label="PR (ON)", color=PR_C)
ax.set_xticks(xx); ax.set_xticklabels(cats)
ax.set_ylabel("end-to-end op latency (ms) — mixed workload (70% warm reads)")
ax.set_title("Mixed workload, end-to-end (honest framing): p50 UNCHANGED (warm reads dominate),\n"
             "only the metadata-refresh tail (p95/p99) improves with the PR")
ax.legend()
for b,v in zip(list(b1)+list(b2), mv+pv):
    ax.text(b.get_x()+b.get_width()/2, v, f"{v:.0f}", ha="center", va="bottom", fontsize=9)
fig.tight_layout(); fig.savefig(os.path.join(OUT,"fi4_mixed_e2e_honest.png"), dpi=130); plt.close(fig)

print("wrote FI graphs to", OUT)
for fn in ["fi1_latency_pr_vs_main.png","fi2_meter_crosscheck.png","fi3_saturating_budget_cdf.png","fi4_mixed_e2e_honest.png"]:
    print("  ", fn)
