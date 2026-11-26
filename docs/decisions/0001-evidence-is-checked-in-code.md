# 0001. Evidence is checked in code, not trusted from the matcher

## Context

The product promises that a requirement is shown as met only when something in the resume or on GitHub backs it. The matcher that proposes verdicts can be a language model, and a model can cite a line that does not exist or a fact that was never gathered, however it is prompted.

## Decision

Every matcher returns proposals, not verdicts. `EvidenceValidator` keeps a cited resume line only if it exists and is not blank, and a GitHub fact only if it was gathered for this comparison. A proven or partly proven proposal left with no evidence becomes "no evidence". The validator can only take a claim away, never add one.

## Consequences

The promise holds whichever engine runs and whatever a prompt says; it is covered by example tests and by property tests over generated, deliberately wrong proposals. The validator cannot tell whether a real line is relevant to the requirement; that is measured by the evaluation set instead.
