# Validation

## Tested results

Verified on **StS2 v0.113.1**:

- Automated checks passed standalone and with RitsuLib in both load orders.
- Silent A0 with RitsuLib passed save/load checks: reward-room reloads restored rewards and pre-claim deck/gold; later-room reloads retained collected rewards exactly once.
- Ironclad A0/A10 runs with RitsuLib completed with unchanged rewards/chests across reloads.
- Save backup/repair and run-history filtering passed isolated checks.

## Verification limits

Broader combat playtesting and live Steam synchronization remain unverified.

## Before publication

- [ ] Check links, headings and word limits.
- [ ] Run the [verification command](../README.md#build-and-validate) once on the final build, including RitsuLib when used. Pass: `All checks passed`.
