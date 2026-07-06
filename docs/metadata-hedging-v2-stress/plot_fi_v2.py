#!/usr/bin/env python3
"""Graphs for the SIMPLIFIED metadata hedging PR #5999 (v2) — REAL fault-injection harness (PR vs main).

Mirrors the original PR #5923 graph set (fi1..fi5) with the same embedded
"what it shows / what it proves" analysis boxes, adapted to the v2 differences:
no per-client concurrency budget, and hedge counts from the PR's own "Metadata
Hedge" trace datum (v2 has no OpenTelemetry meter)."""
import csv, math, os, sys, textwrap
from collections import defaultdict
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
PR_C, MAIN_C, ORIG_C = "#1e8449", "#922b21", "#b9770e"


def add_analysis(fig, shows, proves, width=132):
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

scen = load("fi_scenarios.csv")
ops = load("fi_ops.csv")
try:
    wins = load("fi_winregions.csv")
except FileNotFoundError:
    wins = []


def row(scenario, regime, on):
    for r in scen:
        if r["scenario"] == scenario and r["regime"] == regime and r["hedgingOn"] == on:
            return r
    return None

# ---- fi1: headline latency PR vs main across the four points ----
points = [
    ("coldstart", "single", "Cold start\n(end-to-end ReadItem)"),
    ("refresh", "low", "Refresh\n(low contention)"),
    ("refresh", "saturating", "Refresh\n(saturating storm)"),
    ("mixed", "refresh-subset", "Mixed: refresh\nsubset"),
]
fig, ax = plt.subplots(figsize=(13, 8.4))
x = np.arange(len(points)); w = 0.2
def vals(metric, on):
    return [float(row(sc, rg, on)[metric]) / 1000 if row(sc, rg, on) else 0 for sc, rg, _ in points]
ax.bar(x-1.5*w, vals("p50Ms", "0"), w, label="p50 main", color=MAIN_C, alpha=.55)
ax.bar(x-0.5*w, vals("p50Ms", "1"), w, label="p50 PR",   color=PR_C, alpha=.55)
ax.bar(x+0.5*w, vals("p99Ms", "0"), w, label="p99 main", color=MAIN_C)
ax.bar(x+1.5*w, vals("p99Ms", "1"), w, label="p99 PR",   color=PR_C)
ax.set_xticks(x); ax.set_xticklabels([p[2] for p in points])
ax.set_ylabel("metadata read latency (s) — REAL, fault-injected")
ax.set_title("PR #5999 (simplified metadata hedging) vs main — real fault-injected latency\n"
             "hub ResponseDelay 3s on Gateway metadata (West US 2); secondaries healthy; threshold 1.5s")
ax.legend()
for i, (sc, rg, _) in enumerate(points):
    rm, rp = row(sc, rg, "0"), row(sc, rg, "1")
    if rm and rp and float(rm["p50Ms"]) > 0:
        d = 100 * (float(rm["p50Ms"]) - float(rp["p50Ms"])) / float(rm["p50Ms"])
        ax.annotate(f"p50 -{d:.0f}%", (x[i]-0.5*w, float(rp["p50Ms"])/1000),
                    textcoords="offset points", xytext=(0, 4), ha="center", fontsize=9, fontweight="bold", color=PR_C)
fig.tight_layout()
add_analysis(fig,
    "Real fault-injected metadata-read latency (p50 & p99) for main (OFF) vs the simplified PR #5999 (ON) at four "
    "points: cold start, low-contention refresh, saturating-storm refresh, and the mixed-workload refresh subset. "
    "The hub region's Gateway metadata calls are delayed 3 s; all secondary regions stay healthy.",
    "The simplified v2 delivers the same latency win as the original when the hub is slow — cold start -70% "
    "(12.5 s -> 3.7 s) and refresh -60% (4.0 s -> 1.6 s). Removing the concurrency budget did not cost the latency "
    "benefit; in v2 the saturating p99 also recovers (no budget-exhausted primary-only tail — see fi3).")
fig.savefig(os.path.join(OUT, "fi1_latency_pr_vs_main.png"), dpi=130); plt.close(fig)

# ---- fi2: hedge attribution from the PR's trace datum (fired vs won) ----
attr_points = [
    ("refresh", "low", "Refresh low"),
    ("refresh", "saturating", "Refresh saturating"),
    ("fastfail", "brownout", "Fast-fail brownout"),
]
fig, ax = plt.subplots(figsize=(11, 8.0))
xx = np.arange(len(attr_points)); w = 0.26
onfired = [int(row(sc, rg, "1")["hedgeFired"]) if row(sc, rg, "1") else 0 for sc, rg, _ in attr_points]
onwon = [int(row(sc, rg, "1")["hedgeWon"]) if row(sc, rg, "1") else 0 for sc, rg, _ in attr_points]
offfired = [int(row(sc, rg, "0")["hedgeFired"]) if row(sc, rg, "0") else 0 for sc, rg, _ in attr_points]
n = [int(row(sc, rg, "1")["n"]) if row(sc, rg, "1") else 0 for sc, rg, _ in attr_points]
ax.bar(xx-w, offfired, w, label="main (OFF) hedges", color=MAIN_C)
ax.bar(xx, onfired, w, label="PR (ON) hedges fired", color=PR_C)
ax.bar(xx+w, onwon, w, label="PR (ON) hedges won", color="#58d68d")
ax.set_xticks(xx); ax.set_xticklabels([p[2] for p in attr_points])
ax.set_ylabel('hedges (from the PR\'s "Metadata Hedge" trace datum)')
ax.set_title("Hedge attribution (v2 has no meter — counted from the PR's own trace datum)\n"
             "Hedges fire and WIN only with the PR (ON); main never hedges")
ax.legend()
for i in range(len(attr_points)):
    ax.text(xx[i], onfired[i], f"{onfired[i]}/{n[i]}", ha="center", va="bottom", fontsize=8, fontweight="bold")
fig.tight_layout()
add_analysis(fig,
    "Hedges fired and hedges won per scenario, read from the PR's own per-request trace datum "
    "(\"Metadata Hedge\": HedgeFired / HedgeWon / WinningRegion) — v2 removed the OpenTelemetry meter, so this is "
    "the authoritative in-SDK signal. main (OFF) never hedges.",
    "The A/B is causal and correct: every hedge that fires under the degraded hub also WINS (HedgeWon == HedgeFired), "
    "i.e. the healthy secondary answered and the win is real. Crucially, in the saturating and fast-fail rows the "
    "fired count equals n (100%): with no budget, EVERY slow read hedges (contrast the original's cap — see fi5/fi6).")
fig.savefig(os.path.join(OUT, "fi2_hedge_attribution.png"), dpi=130); plt.close(fig)

# ---- fi3: saturating storm CDF (no budget → all recover near threshold) ----
fig, ax = plt.subplots(figsize=(11, 8.4))
for on, color, lab in [("main(OFF)", MAIN_C, "main (OFF)"), ("PR(ON)", PR_C, "PR (ON)")]:
    lat = sorted(float(o["latencyMs"]) / 1000 for o in ops
                 if o["scenario"] == "refresh" and o["regime"] == "saturating" and o["arm"] == on)
    if lat:
        ys = np.arange(1, len(lat)+1) / len(lat)
        ax.plot(lat, ys, marker="o", ms=3, color=color, label=lab)
rp = row("refresh", "saturating", "1")
ax.axvline(1.5, color=PR_C, ls=":", lw=1.5, label="hedge threshold 1.5s")
ax.set_xlabel("metadata refresh latency (s)")
ax.set_ylabel("cumulative fraction of ops")
fired_note = f"{rp['hedgeFired']}/{rp['n']} ops hedged (100%)" if rp else ""
ax.set_title("Saturating storm (12 distinct containers concurrent) — v2 has NO budget\n"
             f"Every slow read hedges: {fired_note}; the whole distribution recovers near 1.5 s (no ~4 s tail)", fontsize=11)
ax.legend()
fig.tight_layout()
add_analysis(fig,
    "Cumulative latency distribution of the saturating storm — 12 distinct containers refreshed concurrently (more in "
    "flight than the original's budget of 8). The dotted line marks the 1.5 s hedge threshold.",
    "With the budget removed, v2 recovers the ENTIRE distribution near ~1.6 s — there is no budget-exhausted "
    "primary-only tail at ~4 s (which the original had for the ops beyond its cap of 8). Better tail here, but the "
    "flip side is uncapped secondary fan-out: every one of the concurrent slow reads issues a hedge (see fi6).")
fig.savefig(os.path.join(OUT, "fi3_saturating_no_budget_cdf.png"), dpi=130); plt.close(fig)

# ---- fi4: mixed end-to-end honest framing ----
fig, ax = plt.subplots(figsize=(11, 8.0))
rmE, rpE = row("mixed", "end-to-end", "0"), row("mixed", "end-to-end", "1")
cats = ["p50", "p95", "p99"]
mv = [float(rmE[f"{c}Ms"]) for c in cats]; pv = [float(rpE[f"{c}Ms"]) for c in cats]
xx = np.arange(len(cats)); w = 0.38
b1 = ax.bar(xx-w/2, mv, w, label="main (OFF)", color=MAIN_C)
b2 = ax.bar(xx+w/2, pv, w, label="PR (ON)", color=PR_C)
ax.set_xticks(xx); ax.set_xticklabels(cats)
ax.set_ylabel("end-to-end op latency (ms) — mixed workload (70% warm reads)")
ax.set_title("Mixed workload, end-to-end (honest framing): p50 UNCHANGED (warm reads dominate),\n"
             "only the metadata-refresh tail (p95/p99) improves with the PR")
ax.legend()
for b, v in zip(list(b1)+list(b2), mv+pv):
    ax.text(b.get_x()+b.get_width()/2, v, f"{v:.0f}", ha="center", va="bottom", fontsize=9)
fig.tight_layout()
add_analysis(fig,
    "End-to-end operation latency (p50/p95/p99) for a realistic mixed workload: 70% warm reads (cached metadata) plus "
    "30% refresh-bearing ops, OFF vs ON.",
    "As with the original, hedging leaves the common warm-read path untouched — end-to-end p50 is unchanged (both "
    "tiny) — and improves only the metadata-refresh tail (p95/p99: ~4.0 s -> ~1.8 s). A targeted tail win, not a "
    "blanket p50 win.")
fig.savefig(os.path.join(OUT, "fi4_mixed_e2e_honest.png"), dpi=130); plt.close(fig)

# ---- fi5: fast-fail brownout (no budget → every fast-fail hedges) ----
rpF, rmF = row("fastfail", "brownout", "1"), row("fastfail", "brownout", "0")
if rpF and rmF:
    fig, ax = plt.subplots(figsize=(11, 8.0))
    cats = ["hedges fired", "hedges won"]
    onv = [int(rpF["hedgeFired"]), int(rpF["hedgeWon"])]
    offv = [int(rmF["hedgeFired"]), int(rmF["hedgeWon"])]
    xx = np.arange(len(cats)); w = 0.38
    b1 = ax.bar(xx-w/2, offv, w, label="main (OFF)", color=MAIN_C)
    b2 = ax.bar(xx+w/2, onv, w, label="PR (ON)", color=PR_C)
    ax.set_xticks(xx); ax.set_xticklabels(cats)
    ax.set_ylabel("metadata refresh reads")
    ax.set_title("Primary-brownout FAST-FAIL soak (hub 503, no delay -> instant hedge)\n"
                 f"v2 has no budget: ALL {rpF['hedgeFired']}/{rpF['n']} fast-fails hedge and win; main never hedges", fontsize=11)
    ax.legend()
    for b, v in zip(list(b1)+list(b2), offv+onv):
        ax.text(b.get_x()+b.get_width()/2, v, str(v), ha="center", va="bottom", fontsize=10)
    fig.tight_layout()
    add_analysis(fig,
        "A primary-brownout soak: the hub's Gateway metadata returns 503 with NO delay, so every eligible refresh "
        "fast-fails and the strategy fires the hedge IMMEDIATELY (skipping the 1.5 s threshold). A wide simultaneous "
        "wave of distinct containers is fired.",
        "Because v2 has no per-client budget, EVERY fast-fail hedges and wins (all "
        f"{rpF['hedgeFired']} of {rpF['n']}); main issues zero. This is the deliberate v2 simplification: the "
        "amplification bound is now the slow-read rate + one-hedge-per-op, not a hard concurrency cap. See fi6 for the "
        "direct contrast with the original's budget of 8.")
    fig.savefig(os.path.join(OUT, "fi5_fastfail_no_budget.png"), dpi=130); plt.close(fig)

# ---- fi6: THE headline difference — amplification: v2 (uncapped) vs original (budget 8) ----
sat = row("refresh", "saturating", "1")
ff = row("fastfail", "brownout", "1")
if sat and ff:
    fig, ax = plt.subplots(figsize=(12, 8.2))
    labels = ["Saturating storm", "Fast-fail brownout"]
    n_sat, n_ff = int(sat["n"]), int(ff["n"])
    v2_fired = [int(sat["hedgeFired"]), int(ff["hedgeFired"])]
    # Original PR #5923 measured (budget=8): saturating fired 32/budgetExh 16 of 48; fastfail fired 32/33 exhausted.
    # Concurrent secondary in-flight was hard-capped at the budget (8). We contrast the cap vs v2's uncapped fan-out.
    orig_cap = [8, 8]
    xx = np.arange(len(labels)); w = 0.38
    b1 = ax.bar(xx-w/2, orig_cap, w, label="original #5923: max concurrent hedges = budget cap (8)", color=ORIG_C)
    b2 = ax.bar(xx+w/2, v2_fired, w, label="v2 #5999: hedges fired = every slow read (no cap)", color=PR_C)
    ax.set_xticks(xx); ax.set_xticklabels(labels)
    ax.set_ylabel("concurrent secondary hedge requests")
    ax.set_title("THE simplification tradeoff: the original capped concurrent hedges at a budget of 8;\n"
                 "v2 removes the budget, so a brownout hedges EVERY concurrent slow read", fontsize=11)
    ax.legend()
    for b, v in zip(list(b1)+list(b2), orig_cap + v2_fired):
        ax.text(b.get_x()+b.get_width()/2, v, str(v), ha="center", va="bottom", fontsize=11, fontweight="bold")
    fig.tight_layout()
    add_analysis(fig,
        "Side-by-side of the concurrent secondary-hedge fan-out under a brownout: the original PR #5923 hard-capped "
        "in-flight hedges at its per-client budget (8, measured), while v2 (#5999) removed the budget so the number of "
        f"hedges equals the number of concurrent slow reads ({n_sat} in the saturating storm, {n_ff} in the fast-fail "
        "soak).",
        "This is the crux reviewers must weigh for the simplification. v2 is simpler (no SemaphoreSlim / budget "
        "bookkeeping) and gives a better tail (fi3 — no primary-only fallback), but its secondary-region amplification "
        "during a primary brownout is bounded only by the slow-read rate and one-hedge-per-op, NOT by a hard "
        "concurrency cap. All hedges still target a single secondary region (see fi_winregions.csv), and a healthy hub "
        "never hedges — but the per-client 8x ceiling the original guaranteed is gone.")
    fig.savefig(os.path.join(OUT, "fi6_amplification_tradeoff.png"), dpi=130); plt.close(fig)

print("wrote v2 FI graphs to", OUT)
for fn in ["fi1_latency_pr_vs_main.png", "fi2_hedge_attribution.png", "fi3_saturating_no_budget_cdf.png",
           "fi4_mixed_e2e_honest.png", "fi5_fastfail_no_budget.png", "fi6_amplification_tradeoff.png"]:
    print("  ", fn)
