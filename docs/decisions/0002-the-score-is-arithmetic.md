# 0002. The score is computed from the verdicts, never asked of a model

## Context

A single number is what people remember from a report, so it has to be explainable and repeatable.

## Decision

`Scoring` weighs a must-have twice a nice-to-have, gives full credit for proven and half for partly proven, and rounds the share to 0–100.

## Consequences

The same verdicts always give the same score, and the report can state the rule in one sentence. The score is only as good as the verdicts, which is why effort goes into those rather than into the number.
