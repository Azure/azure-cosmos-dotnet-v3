# Hedging benchmark: main vs change

**Unobserved task exceptions (whole run): main=0, change=0**

## Phase: steadyA

| Metric | main | change | delta (change-main) | delta % |
|---|---:|---:|---:|---:|
| Read ops/s (mean) | 2101 | 2209 | +107.64 | +5.1% |
| Read p99 ms (mean) | 43.1 | 40.1 | -2.94 | -6.8% |
| Read p99 ms (max) | 152.1 | 272.8 | +120.66 | +79.3% |
| CPU % (mean) | 12.4 | 11.8 | -0.52 | -4.2% |
| WorkingSet MB (mean) | 157 | 161 | +4.46 | +2.8% |
| WorkingSet MB (max) | 160 | 164 | +3.90 | +2.4% |
| Errors | 0 | 0 | +0.00 | n/a |
| Cancellations | 0 | 0 | +0.00 | n/a |
| Unobserved task exc | 0 | 0 | +0.00 | n/a |

## Phase: fault

| Metric | main | change | delta (change-main) | delta % |
|---|---:|---:|---:|---:|
| Read ops/s (mean) | 220 | 216 | -3.80 | -1.7% |
| Read p99 ms (mean) | 201.1 | 207.7 | +6.66 | +3.3% |
| Read p99 ms (max) | 262.5 | 511.2 | +248.68 | +94.7% |
| CPU % (mean) | 5.4 | 5.6 | +0.18 | +3.3% |
| WorkingSet MB (mean) | 369 | 386 | +17.06 | +4.6% |
| WorkingSet MB (max) | 537 | 582 | +45.10 | +8.4% |
| Errors | 0 | 0 | +0.00 | n/a |
| Cancellations | 0 | 0 | +0.00 | n/a |
| Unobserved task exc | 0 | 0 | +0.00 | n/a |

## Phase: recovery

| Metric | main | change | delta (change-main) | delta % |
|---|---:|---:|---:|---:|
| Read ops/s (mean) | 2372 | 2316 | -55.64 | -2.3% |
| Read p99 ms (mean) | 46.7 | 37.3 | -9.33 | -20.0% |
| Read p99 ms (max) | 200.7 | 200.7 | +0.00 | +0.0% |
| CPU % (mean) | 12.4 | 12.0 | -0.45 | -3.6% |
| WorkingSet MB (mean) | 346 | 559 | +212.69 | +61.4% |
| WorkingSet MB (max) | 662 | 650 | -11.90 | -1.8% |
| Errors | 0 | 0 | +0.00 | n/a |
| Cancellations | 0 | 0 | +0.00 | n/a |
| Unobserved task exc | 0 | 0 | +0.00 | n/a |
