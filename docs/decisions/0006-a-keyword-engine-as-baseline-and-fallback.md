# 0006. A keyword engine as the baseline and the fallback

## Context

The model has a small free daily quota and is sometimes overloaded, and a model's accuracy means little without something to compare it with.

## Decision

A deterministic keyword engine reads requirements from headed bullet lists, looks for catalogued skills in resume lines, and reads the context of each line (negation, other people's work, learning, instructions to a screener). It runs when no model is configured, takes over when the model fails mid-analysis, and is the baseline in the evaluation set.

## Consequences

A model outage never costs a user their report, which says which engine produced it. The evaluation can report the model's gain over a known floor, and CI fails if the baseline regresses.
