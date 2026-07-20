# Hedging benchmark: main vs change

**Unobserved task exceptions (whole run): main=0, change=0**

## Phase: steadyA

| Metric | main | change | delta (change-main) | delta % |
|---|---:|---:|---:|---:|
| Read ops/s (mean) | 2212 | 2242 | +29.11 | +1.3% |
| Read p99 ms (mean) | 39.0 | 36.1 | -2.85 | -7.3% |
| Read p99 ms (max) | 99.2 | 77.1 | -22.13 | -22.3% |
| CPU % (mean) | 11.7 | 11.5 | -0.16 | -1.4% |
| WorkingSet MB (mean) | 156 | 156 | -0.22 | -0.1% |
| WorkingSet MB (max) | 158 | 159 | +0.20 | +0.1% |
| Errors | 0 | 0 | +0.00 | n/a |
| Cancellations | 0 | 0 | +0.00 | n/a |
| Unobserved task exc | 0 | 0 | +0.00 | n/a |

## Phase: fault

| Metric | main | change | delta (change-main) | delta % |
|---|---:|---:|---:|---:|
| Read ops/s (mean) | 225 | 220 | -4.44 | -2.0% |
| Read p99 ms (mean) | 207.2 | 208.9 | +1.67 | +0.8% |
| Read p99 ms (max) | 416.2 | 417.6 | +1.40 | +0.3% |
| CPU % (mean) | 5.6 | 5.9 | +0.24 | +4.4% |
| WorkingSet MB (mean) | 379 | 392 | +12.38 | +3.3% |
| WorkingSet MB (max) | 534 | 552 | +18.00 | +3.4% |
| Errors | 0 | 0 | +0.00 | n/a |
| Cancellations | 0 | 0 | +0.00 | n/a |
| Unobserved task exc | 0 | 0 | +0.00 | n/a |

## Phase: recovery

| Metric | main | change | delta (change-main) | delta % |
|---|---:|---:|---:|---:|
| Read ops/s (mean) | 2485 | 2168 | -317.60 | -12.8% |
| Read p99 ms (mean) | 35.9 | 49.5 | +13.54 | +37.7% |
| Read p99 ms (max) | 198.9 | 257.8 | +58.91 | +29.6% |
| CPU % (mean) | 12.6 | 11.8 | -0.78 | -6.2% |
| WorkingSet MB (mean) | 521 | 506 | -15.67 | -3.0% |
| WorkingSet MB (max) | 678 | 656 | -22.20 | -3.3% |
| Errors | 0 | 0 | +0.00 | n/a |
| Cancellations | 0 | 0 | +0.00 | n/a |
| Unobserved task exc | 0 | 0 | +0.00 | n/a |
