# Hedging benchmark: main vs change

**Unobserved task exceptions (whole run): main=1, change=1**

## Phase: steadyA

| Metric | main | change | delta (change-main) | delta % |
|---|---:|---:|---:|---:|
| Read ops/s (mean) | 1778 | 1790 | +11.51 | +0.6% |
| Read p99 ms (mean) | 62.9 | 66.0 | +3.10 | +4.9% |
| Read p99 ms (max) | 154.0 | 145.4 | -8.56 | -5.6% |
| CPU % (mean) | 11.7 | 13.0 | +1.30 | +11.2% |
| WorkingSet MB (mean) | 155 | 152 | -2.47 | -1.6% |
| WorkingSet MB (max) | 157 | 156 | -1.20 | -0.8% |
| Errors | 0 | 0 | +0.00 | n/a |
| Cancellations | 0 | 0 | +0.00 | n/a |
| Unobserved task exc | 0 | 0 | +0.00 | n/a |

## Phase: fault

| Metric | main | change | delta (change-main) | delta % |
|---|---:|---:|---:|---:|
| Read ops/s (mean) | 209 | 207 | -1.09 | -0.5% |
| Read p99 ms (mean) | 228.9 | 215.6 | -13.31 | -5.8% |
| Read p99 ms (max) | 253.8 | 257.2 | +3.42 | +1.3% |
| CPU % (mean) | 6.0 | 6.5 | +0.48 | +7.9% |
| WorkingSet MB (mean) | 385 | 382 | -2.92 | -0.8% |
| WorkingSet MB (max) | 578 | 564 | -13.80 | -2.4% |
| Errors | 0 | 0 | +0.00 | n/a |
| Cancellations | 0 | 0 | +0.00 | n/a |
| Unobserved task exc | 0 | 0 | +0.00 | n/a |

## Phase: recovery

| Metric | main | change | delta (change-main) | delta % |
|---|---:|---:|---:|---:|
| Read ops/s (mean) | 1784 | 1790 | +6.42 | +0.4% |
| Read p99 ms (mean) | 122.7 | 122.8 | +0.07 | +0.1% |
| Read p99 ms (max) | 214.2 | 204.5 | -9.67 | -4.5% |
| CPU % (mean) | 12.9 | 12.6 | -0.28 | -2.2% |
| WorkingSet MB (mean) | 409 | 582 | +173.14 | +42.4% |
| WorkingSet MB (max) | 606 | 643 | +37.30 | +6.2% |
| Errors | 0 | 0 | +0.00 | n/a |
| Cancellations | 0 | 0 | +0.00 | n/a |
| Unobserved task exc | 1 | 1 | +0.00 | +0.0% |
