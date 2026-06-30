#!/usr/bin/env python3
"""Generate graphs for PR #5923 metadata-hedging worst-case stress test."""
import csv
import os
import sys
import textwrap

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else "."


def add_analysis(fig, shows, proves, width=140):
    """Embed a 'what it shows / what it proves' analysis box at the bottom of the figure."""
    body = (textwrap.fill("WHAT IT SHOWS:   " + shows, width=width) + "\n\n" +
            textwrap.fill("WHAT IT PROVES:  " + proves, width=width))
    nlines = body.count("\n") + 1
    reserve_in = 0.95 + nlines * 0.145
    bottom = min(0.46, reserve_in / fig.get_size_inches()[1])
    fig.subplots_adjust(bottom=bottom)
    fig.text(0.012, 0.012, body, ha="left", va="bottom", fontsize=8.4, linespacing=1.3,
             bbox=dict(boxstyle="round,pad=0.6", facecolor="#f4f7f5", edgecolor="#1e8449", linewidth=1.0))

def load_scenarios(path):
    rows = []
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            rows.append(r)
    return rows

def load_regions(path):
    rows = []
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            rows.append(r)
    return rows

scen = load_scenarios(os.path.join(OUT, "scenarios.csv"))
regions = load_regions(os.path.join(OUT, "regions.csv"))

by_label = {r["label"]: r for r in scen}

# ---- Graph 1: calls per region (degraded, HEDGE_ON) for each partition count ----
part_counts = [100, 10000, 50000]
labels = [f"Degraded-P{p}-HEDGE_ON" for p in part_counts]
region_order = ["West US 2", "East US", "South Central US", "Central US", "North Central US"]

fig, axes = plt.subplots(1, len(labels), figsize=(16, 7.2), sharey=False)
for ax, lab, p in zip(axes, labels, part_counts):
    sends = {r["region"]: int(r["sendCount"]) for r in regions if r["label"] == lab}
    vals = [sends.get(rg, 0) for rg in region_order]
    colors = ["#c0392b"] + ["#2980b9"] * (len(region_order) - 1)
    bars = ax.bar(range(len(region_order)), vals, color=colors)
    ax.set_title(f"P={p:,} partitions", fontsize=11, fontweight="bold")
    ax.set_xticks(range(len(region_order)))
    ax.set_xticklabels([rg.replace(" ", "\n") for rg in region_order], fontsize=8)
    ax.set_ylabel("metadata calls (sends)")
    for b, v in zip(bars, vals):
        ax.text(b.get_x() + b.get_width()/2, v, f"{v:,}", ha="center", va="bottom", fontsize=8)
fig.suptitle("Per-region metadata calls under degraded hub (West US 2 = slow hub, red)\n"
             "Hedge fan-out concentrates on the 2nd preferred region (East US); other regions untouched",
             fontsize=12)
fig.tight_layout(rect=[0, 0, 1, 0.92])
add_analysis(fig,
    "[Simulated worst-case] Per-region metadata calls under a degraded hub (red = slow hub West US 2) for 100, 10k and "
    "50k partitions, hedging ON.",
    "Even at 50,000 partitions the hedge fan-out concentrates on the single 2nd-preferred region (East US); the other "
    "three regions receive nothing — fan-out does not scale with partition count.")
fig.savefig(os.path.join(OUT, "g1_calls_per_region.png"), dpi=130)
plt.close(fig)

# ---- Graph 2: PkRange refresh reads spawned vs hedged vs budget-exhausted ----
fig, ax = plt.subplots(figsize=(10, 7.8))
x = np.arange(len(part_counts))
w = 0.27
spawned = [int(by_label[l]["totalOps"]) for l in labels]
hedged = [int(by_label[l]["hedgeFired"]) for l in labels]
budget = [int(by_label[l]["budgetExhausted"]) for l in labels]
b1 = ax.bar(x - w, spawned, w, label="PkRange reads spawned", color="#34495e")
b2 = ax.bar(x, hedged, w, label="hedged (fired)", color="#27ae60")
b3 = ax.bar(x + w, budget, w, label="budget-exhausted (primary-only)", color="#e67e22")
ax.set_yscale("log")
ax.set_xticks(x)
ax.set_xticklabels([f"{p:,}" for p in part_counts])
ax.set_xlabel("number of partitions (PartitionKeyRange-Gone events)")
ax.set_ylabel("count (log scale)")
ax.set_title("PkRange refresh reads spawned vs hedged vs budget-exhausted\n(degraded hub, hedging ON)")
ax.legend()
for bars in (b1, b2, b3):
    for b in bars:
        h = b.get_height()
        ax.text(b.get_x() + b.get_width()/2, h, f"{int(h):,}", ha="center", va="bottom", fontsize=8)
fig.tight_layout()
add_analysis(fig,
    "[Simulated] PkRange refresh reads spawned vs how many hedged vs how many were budget-exhausted, by partition count "
    "(log scale), hedging ON.",
    "Hedges fired stay far below reads spawned as partitions grow (most spill to budget-exhausted primary-only) — the "
    "number of secondary requests is bounded by the budget, not by partition count.")
fig.savefig(os.path.join(OUT, "g2_spawned_vs_hedged.png"), dpi=130)
plt.close(fig)

# ---- Graph 3: REAL-timing latency win (production threshold 1.5s, hub 5-10s) ----
fig, ax = plt.subplots(figsize=(11, 7.8))
rt = by_label.get("Degraded-P100-REALTIME-HEDGE_ON")
if rt:
    pcts = ["p50", "p95", "p99", "max"]
    vals = [float(rt["p50Ms"]) / 1000, float(rt["p95Ms"]) / 1000,
            float(rt["p99Ms"]) / 1000, float(rt["maxMs"]) / 1000]
    bars = ax.bar(pcts, vals, color=["#1e8449", "#f39c12", "#cb4335", "#7b241c"], width=0.55)
    for b, v in zip(bars, vals):
        ax.text(b.get_x() + b.get_width()/2, v, f"{v:.2f}s", ha="center", va="bottom", fontsize=11, fontweight="bold")
    # OFF baseline band = uniform hub delay 5-10s (every op primary-only when hedging is OFF)
    ax.axhspan(5.0, 10.0, color="#cb4335", alpha=0.12)
    ax.axhline(7.5, color="#cb4335", linestyle="--", linewidth=1.5,
               label="HEDGING OFF baseline (hub delay 5-10s, every op)")
    ax.axhline(1.5, color="#1e8449", linestyle=":", linewidth=1.5, label="hedge threshold = 1.5s")
    ax.set_ylabel("PkRange refresh latency (seconds, REAL production timing)")
    ax.set_title("Production-timing run (threshold 1.5s, hub delay 5-10s), 100 partitions, hedging ON\n"
                 f"Median recovered to {vals[0]:.2f}s (hedge wins from East US) vs 5-10s with hedging OFF; "
                 f"{int(rt['hedgeFired'])}/100 ops hedged")
    ax.legend(loc="center right")
fig.tight_layout()
add_analysis(fig,
    "[Simulated] PkRange refresh latency percentiles at TRUE production timing (1.5 s threshold, 5-10 s hub delay, no "
    "compression), 100 partitions, hedging ON, against the OFF 5-10 s baseline band.",
    "At real production timing the median refresh recovers to ~1.5 s (hedge wins from East US) versus the 5-10 s every-op "
    "stall with hedging off — the win is not an artifact of the 10x time-compression used in the other graphs.")
fig.savefig(os.path.join(OUT, "g3_realtime_latency_win.png"), dpi=130)
plt.close(fig)

# ---- Graph 4: max concurrent secondary in-flight vs partitions (budget cap = 8) ----
fig, ax = plt.subplots(figsize=(10, 7.8))
maxsec = [int(by_label[l]["maxConcurrentSecondary"]) for l in labels]
bars = ax.bar([f"{p:,}" for p in part_counts], maxsec, color="#8e44ad", width=0.5)
ax.axhline(8, color="red", linestyle="--", linewidth=2, label="per-client budget cap = 8")
ax.set_xlabel("number of partitions (concurrent PartitionKeyRange-Gone storm)")
ax.set_ylabel("max concurrent secondary (hedge) requests in flight")
ax.set_title("Gateway is NOT bombarded: secondary fan-out is hard-bounded by the budget\n"
             "even at 50,000 simultaneous PkRange-Gone refreshes")
ax.set_ylim(0, 12)
for b, v in zip(bars, maxsec):
    ax.text(b.get_x() + b.get_width()/2, v, str(v), ha="center", va="bottom", fontsize=11, fontweight="bold")
ax.legend()
fig.tight_layout()
add_analysis(fig,
    "[Simulated] Maximum number of concurrent secondary (hedge) requests in flight for 100, 10k and 50k simultaneous "
    "PkRange-Gone refreshes; dashed line = budget cap 8.",
    "The concurrent secondary fan-out stays pinned at 8 (the per-client budget) even under a 50,000-refresh storm — the "
    "Gateway is structurally protected from a hedge flood.")
fig.savefig(os.path.join(OUT, "g4_secondary_inflight_cap.png"), dpi=130)
plt.close(fig)

# ---- Graph 5: healthy baseline — no amplification ----
fig, ax = plt.subplots(figsize=(9, 7.4))
hb = by_label.get("Healthy-baseline")
if hb:
    cats = ["ops", "hedges fired", "secondary calls"]
    sec_calls = sum(int(r["sendCount"]) for r in regions
                    if r["label"] == "Healthy-baseline" and r["region"] != "West US 2")
    vals = [int(hb["totalOps"]), int(hb["hedgeFired"]), sec_calls]
    bars = ax.bar(cats, vals, color=["#34495e", "#27ae60", "#2980b9"])
    for b, v in zip(bars, vals):
        ax.text(b.get_x() + b.get_width()/2, v, f"{v:,}", ha="center", va="bottom", fontsize=11)
    ax.set_title("Healthy baseline (hub fast): ZERO hedges, ZERO secondary calls\n"
                 "No amplification when the primary is healthy")
    ax.set_ylabel("count")
fig.tight_layout()
add_analysis(fig,
    "[Simulated] Healthy baseline (hub fast): total ops vs hedges fired vs secondary calls, hedging ON.",
    "With a healthy hub, hedges fired = 0 and secondary calls = 0 — confirming no amplification or extra load when the "
    "primary is responsive.")
fig.savefig(os.path.join(OUT, "g5_healthy_no_amplification.png"), dpi=130)
plt.close(fig)

print("graphs written to", OUT)
for fn in ["g1_calls_per_region.png", "g2_spawned_vs_hedged.png", "g3_realtime_latency_win.png",
           "g4_secondary_inflight_cap.png", "g5_healthy_no_amplification.png"]:
    print("  ", fn)
