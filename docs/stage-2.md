# Stage 2: foundation

`RandomAddress` composes purpose, scope IDs and zero-based ordinals as canonical JSON arrays. Encoding preserves component types and boundaries and is culture-independent. Include act/event/combat IDs when their policies scope an ordinal.

`AddressRandom` hashes a versioned domain, run seed, address and retry index with SHA-256. Strings use length-prefixed UTF-8; integers and hash extraction use little-endian order. Run UUIDs are provenance only. Sampling supports `[0,1)`, `(0,1)` and unbiased inclusive integer ranges; integer retries remain local to the addressed roll.

`RandomState` owns each run's sequence positions and opportunity counters. Reading an address leaves positions unchanged; committed draws advance only their sequence. Source policies determine commitment and reset boundaries. Stored sequence entries and selection clocks arrive with their owning policies.

Capture and restore copy state independently of the live run and loaded snapshot. Stage 2 introduced schema 2 with validated positions. Current compatibility follows the [stage 4 checkpoint contract](stage-4.md#checkpoint-contract).

[Verification](../README.md#build-and-validate) covers fixed reference vectors, address separation, culture, ranges, stream isolation, corruption and checkpoint copying. In-engine checks exercise setup, capture, reload rewriting and replay using test draws before/after checkpoints. Gameplay reconstruction checks accompany source integration, beginning with combat gold in step 3.

Current validation results: [integration checks](stage-1.md#cloud-and-verification-limits).
